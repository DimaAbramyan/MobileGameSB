using System.Collections.Generic;
using UnityEngine;

internal sealed class DirectedWaveAttackSequenceRuntime : IDirectedWaveAttackSequenceHost
{
    private readonly DirectedEnemySubWave wave;
    private readonly DirectedWaveAttackBehaviour behaviour;
    private readonly List<Run> runs = new();
    private DirectedWaveAttackSequenceStep pendingStep;
    private HashSet<int> pendingSlots;
    private readonly Dictionary<Enemy, IWaveAttackExecutor> controlled = new();
    private readonly List<Enemy> candidates = new();
    private readonly List<MonoBehaviour> components = new();
    public bool IsIdle
    {
        get { for (int i = 0; i < runs.Count; i++) if (!runs[i].IsIdle) return false; return true; }
    }
    public DirectedWaveAttackSequenceRuntime(DirectedEnemySubWave wave, DirectedWaveAttackBehaviour behaviour)
    { this.wave = wave; this.behaviour = behaviour; }

    public bool TryStart(DirectedWaveAttackSequenceStep step, out IDirectedWaveAttackSequenceRun execution)
    {
        execution = null;
        if (!ReferenceEquals(pendingStep, step))
        {
            pendingStep = step; pendingSlots = new HashSet<int>();
            for (int i = 0; i < wave.GetConfiguredEnemySlotCount(); i++)
                if (step.overrideTargetSlots ? step.targetSlots.Contains(i) : behaviour.AllowsSequenceSlot(i)) pendingSlots.Add(i);
        }
        var slots = pendingSlots;
        for (int i = runs.Count - 1; i >= 0; i--)
        {
            if (runs[i].RootComplete) { runs.RemoveAt(i); continue; }
            if (runs[i].Slots.Overlaps(slots)) return false;
        }
        var settings = new DirectedWaveAttackSettings(); step.CopySettingsTo(settings);
        var run = new Run(wave, slots, settings, settings.RuntimeAttackSpeedMultiplier);
        runs.Add(run); execution = run;
        return true;
    }
    public void NotifyEnemyDestroyed(Enemy enemy)
    {
        controlled.Remove(enemy);
        for (int i = 0; i < runs.Count; i++) runs[i].NotifyEnemyDestroyed(enemy);
    }
    public void ReserveControl()
    {
        wave.GetWaveAttackCandidates(candidates);
        for (int i = 0; i < candidates.Count; i++)
        {
            Enemy enemy = candidates[i];
            if (!wave.TryGetFormationIndex(enemy, out int index) || !behaviour.ControlsSequenceSlot(index)) continue;
            components.Clear(); enemy.GetComponents(components);
            for (int j = 0; j < components.Count; j++)
                if (components[j] is IWaveAttackExecutor executor) { controlled[enemy] = executor; break; }
        }
        KeepControl();
    }
    public void KeepControl()
    {
        // Step completion must not let an enemy's autonomous attack fire during Sequence pauses.
        foreach (var entry in controlled)
            if (entry.Key != null && !entry.Key.isDead) entry.Value.SetWaveAttackControl(true);
    }
    public void ReleaseControl()
    {
        foreach (var entry in controlled)
            if (entry.Key != null) entry.Value.SetWaveAttackControl(false);
        controlled.Clear();
    }

    private sealed class Run : IDirectedWaveAttackSequenceRun
    {
        public readonly HashSet<int> Slots;
        private readonly DirectedWaveAttackController controller;
        private readonly EnemyAttackCompletionTracker completion = new();
        private bool released;
        public bool RootComplete => released;
        public bool IsIdle => released || controller.IsIdle;
        public bool ChildrenComplete => completion.IsComplete;
        public Run(DirectedEnemySubWave wave, HashSet<int> slots, DirectedWaveAttackSettings settings, float speed)
        {
            Slots = slots;
            controller = new DirectedWaveAttackController(wave, settings,
                enemy => wave.TryGetFormationIndex(enemy, out int index) && slots.Contains(index));
            controller.BeginSingleCycle(speed, completion);
        }
        public void Tick()
        {
            if (released) return;
            controller.Tick();
            if (controller.HasCompletedSingleCycle) Stop();
        }
        public void Stop()
        {
            if (released) return;
            controller.Stop(); released = true;
        }
        public void NotifyEnemyDestroyed(Enemy enemy)
        { if (!released) controller.NotifyEnemyDestroyed(enemy); }
    }
}
