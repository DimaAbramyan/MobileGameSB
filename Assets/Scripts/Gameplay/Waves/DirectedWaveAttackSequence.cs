using System;
using System.Collections.Generic;
using UnityEngine;

public enum DirectedWaveAttackExecutionMode { Patterns, Sequence }
public enum DirectedWaveAttackSequenceMode { Once, Loop, PingPong, Random }
public enum DirectedWaveAttackSequenceStepType { Attack, Pause, RepeatBlock }

[Serializable]
public sealed class DirectedWaveAttackSequenceStep
{
    public string name = "Attack";
    public bool enabled = true;
    public DirectedWaveAttackSequenceStepType type;
    public DirectedWaveAttackPattern pattern = new();
    [Min(0f)] public float startDelay;
    [Min(1)] public int repeatCount = 1;
    public bool waitForCompletion = true;
    [Min(0f)] public float delayAfterStep;
    [Min(.01f)] public float attackSpeedMultiplier = 1f;
    public bool overrideTargetSlots;
    public List<int> targetSlots = new();
    [Min(0f)] public float pauseDuration = 1f;
    public bool waitForChildAttacks;
    [Min(0f)] public float childWaitTimeout = 10f;
    public bool unlimitedChildWait;
    [SerializeReference] public List<DirectedWaveAttackSequenceStep> steps = new();

    public void CopySettingsTo(DirectedWaveAttackSettings destination)
    {
        (pattern ??= new DirectedWaveAttackPattern()).CopyResolvedSettingsTo(destination);
        destination.ScaleSequenceScheduling(Mathf.Max(.01f, attackSpeedMultiplier));
    }
}

[Serializable]
public sealed class DirectedWaveAttackSequenceSettings
{
    public DirectedWaveAttackSequenceMode sequenceMode;
    [Min(0), Tooltip("Number of passes. Zero repeats indefinitely except in Once mode.")]
    public int loopCount = 1;
    [Min(0)] public int startStep;
    public bool restartOnReentry = true;
    public bool waitForPreviousAttack = true;
    public int seed = 12345;
    [SerializeReference] public List<DirectedWaveAttackSequenceStep> steps = new();
}

// Iterates repeat blocks without expanding their contents or touching Unity's random state.
public sealed class DirectedWaveAttackSequenceCursor
{
    public const int MaximumDepth = 8;
    private sealed class Frame
    {
        public List<DirectedWaveAttackSequenceStep> Steps;
        public int Index, Remaining;
    }
    private readonly DirectedWaveAttackSequenceSettings settings;
    private readonly Frame[] frames = new Frame[MaximumDepth];
    private System.Random random;
    private int[] order = Array.Empty<int>();
    private int depth, pass, orderIndex;
    public bool IsComplete { get; private set; }
    public int PassLimit { get; set; } = int.MaxValue;
    public int CompletedPasses => pass;

    public DirectedWaveAttackSequenceCursor(DirectedWaveAttackSequenceSettings settings)
    {
        this.settings = settings;
        for (int i = 0; i < frames.Length; i++) frames[i] = new Frame();
        Reset();
    }
    public void Reset()
    {
        random = new System.Random(settings.seed);
        depth = pass = 0; IsComplete = false;
        BeginPass();
    }
    private void BeginPass()
    {
        int count = settings.steps?.Count ?? 0;
        if (count == 0) { IsComplete = true; return; }
        int length = settings.sequenceMode == DirectedWaveAttackSequenceMode.PingPong && count > 1 ? count * 2 - 1 : count;
        if (order.Length != length) order = new int[length];
        for (int i = 0; i < count; i++) order[i] = i;
        for (int i = count; i < length; i++) order[i] = length - 1 - i;
        if (settings.sequenceMode == DirectedWaveAttackSequenceMode.Random)
        {
            for (int i = count - 1; i > 0; i--) { int j = random.Next(i + 1); (order[i], order[j]) = (order[j], order[i]); }
            if (pass == 0)
            {
                int selected = Array.IndexOf(order, Mathf.Clamp(settings.startStep, 0, count - 1));
                (order[0], order[selected]) = (order[selected], order[0]);
            }
            orderIndex = 0;
        }
        else orderIndex = pass == 0 ? Mathf.Clamp(settings.startStep, 0, count - 1)
            : settings.sequenceMode == DirectedWaveAttackSequenceMode.PingPong && count > 1 ? 1 : 0;
    }
    public bool TryNext(out DirectedWaveAttackSequenceStep step)
    {
        step = null;
        for (int guard = 0; guard < 256 && !IsComplete; guard++)
        {
            if (depth > 0)
            {
                Frame frame = frames[depth - 1];
                if (frame.Index >= frame.Steps.Count)
                {
                    if (--frame.Remaining > 0) frame.Index = 0;
                    else depth--;
                    continue;
                }
                step = frame.Steps[frame.Index++];
            }
            else
            {
                if (orderIndex >= order.Length)
                {
                    pass++;
                    if (pass >= PassLimit || settings.sequenceMode == DirectedWaveAttackSequenceMode.Once
                        || settings.loopCount > 0 && pass >= settings.loopCount)
                    { IsComplete = true; return false; }
                    BeginPass(); continue;
                }
                int index = order[orderIndex++];
                if (settings.steps == null || index >= settings.steps.Count) continue;
                step = settings.steps[index];
            }
            if (step == null || !step.enabled) continue;
            if (step.type != DirectedWaveAttackSequenceStepType.RepeatBlock) return true;
            if (depth >= MaximumDepth || step.steps == null || step.steps.Count == 0) continue;
            Frame block = frames[depth++];
            block.Steps = step.steps; block.Index = 0; block.Remaining = Mathf.Clamp(step.repeatCount, 1, 10000);
        }
        step = null;
        return false;
    }
}

public interface IDirectedWaveAttackSequenceRun
{
    bool RootComplete { get; }
    bool ChildrenComplete { get; }
    void Tick();
    void Stop();
}

public interface IDirectedWaveAttackSequenceHost
{
    bool TryStart(DirectedWaveAttackSequenceStep step, out IDirectedWaveAttackSequenceRun run);
}

// Runtime and Preview share ordering, delay/repeat rules, overlap gates and child wait limits.
public sealed class DirectedWaveAttackSequencePlayer
{
    private sealed class Active
    {
        public IDirectedWaveAttackSequenceRun Run;
        public DirectedWaveAttackSequenceStep Step;
        public float ChildDeadline = float.NaN;
    }
    private readonly DirectedWaveAttackSequenceSettings settings;
    private readonly IDirectedWaveAttackSequenceHost host;
    private readonly DirectedWaveAttackSequenceCursor cursor;
    private readonly List<Active> active = new();
    private DirectedWaveAttackSequenceStep current;
    private Active waiting;
    private float deadline, pausedAt;
    private int remaining;
    private bool started, paused, afterStep;
    public bool IsComplete => started && cursor.IsComplete && current == null && active.Count == 0;
    public string CurrentStep => current != null ? current.name : IsComplete ? "Complete" : "Waiting";
    public int ActiveCount => active.Count;
    public int ChildWaitTimeouts { get; private set; }
    public bool AllowNewAttacks { get; set; } = true;
    public int CompletedPasses => cursor.CompletedPasses;
    public int PassLimit { get => cursor.PassLimit; set => cursor.PassLimit = value < 0 ? int.MaxValue : value; }

    public DirectedWaveAttackSequencePlayer(DirectedWaveAttackSequenceSettings settings, IDirectedWaveAttackSequenceHost host)
    { this.settings = settings; this.host = host; cursor = new DirectedWaveAttackSequenceCursor(settings); }

    public void Begin(float time, bool restart)
    {
        if (restart || !started)
        {
            Stop(); cursor.Reset(); ChildWaitTimeouts = 0; deadline = time;
        }
        else if (paused)
            deadline += Mathf.Max(0f, time - pausedAt);
        started = true; paused = false;
    }
    public void Pause(float time)
    {
        if (paused) return;
        pausedAt = time; paused = true;
        // Interrupted emissions restart their current repetition on reentry; completed steps stay consumed.
        if (waiting != null) { remaining++; waiting = null; }
        for (int i = 0; i < active.Count; i++) active[i].Run.Stop();
        active.Clear();
    }
    public void Stop()
    {
        for (int i = 0; i < active.Count; i++) active[i].Run.Stop();
        active.Clear(); current = null; waiting = null; remaining = 0;
        started = paused = afterStep = false;
    }
    private bool Complete(Active item, float time)
    {
        if (!item.Run.RootComplete) return false;
        if (!item.Step.waitForChildAttacks || item.Run.ChildrenComplete) return true;
        if (item.Step.unlimitedChildWait) return false;
        if (float.IsNaN(item.ChildDeadline)) item.ChildDeadline = time + Mathf.Max(0f, item.Step.childWaitTimeout);
        if (time < item.ChildDeadline) return false;
        ChildWaitTimeouts++;
        return true;
    }
    public void Tick(float time)
    {
        if (!started || paused) return;
        for (int i = active.Count - 1; i >= 0; i--)
        {
            Active item = active[i]; item.Run.Tick();
            if (!Complete(item, time)) continue;
            if (waiting == item) waiting = null;
            active.RemoveAt(i);
        }
        for (int guard = 0; guard < 64; guard++)
        {
            if (!AllowNewAttacks) return;
            if (waiting != null || time < deadline) return;
            if (afterStep) { afterStep = false; current = null; }
            if (current == null)
            {
                if (settings.waitForPreviousAttack && active.Count > 0) return;
                if (!cursor.TryNext(out current)) return;
                remaining = Mathf.Clamp(current.repeatCount, 1, 10000);
                deadline = time + Mathf.Max(0f, current.startDelay);
                if (time < deadline) return;
            }
            if (remaining <= 0)
            { deadline = time + Mathf.Max(0f, current.delayAfterStep); afterStep = true; continue; }
            if (settings.waitForPreviousAttack && active.Count > 0 || active.Count >= 32) return;
            if (current.type == DirectedWaveAttackSequenceStepType.Pause)
            { remaining--; deadline = time + Mathf.Max(0f, current.pauseDuration); continue; }
            if (!host.TryStart(current, out var run)) return;
            var execution = new Active { Run = run, Step = current };
            active.Add(execution); remaining--;
            if (current.waitForCompletion) { waiting = execution; return; }
        }
    }
}
