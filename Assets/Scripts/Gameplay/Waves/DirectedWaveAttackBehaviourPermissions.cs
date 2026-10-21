using System.Collections.Generic;
using UnityEngine;

public sealed partial class DirectedWaveAttackBehaviour
{
    public WaveAttackPermissions AttackPermissions { get; } = new();
    private float externalClockOffset, externalClockPrevious = -1f;
    private int externalClockFrame = -1;
    private int coordinatedCycleLimit = -1;
    private int coordinatedSequenceCycles;
    private readonly System.Collections.Generic.Dictionary<DirectedWaveAttackController, int> cycleBaselines = new();
    private readonly System.Collections.Generic.Dictionary<Enemy, IEnemyAttackActivity> attackActivities = new();
    internal void RegisterAttackActivity(Enemy enemy, IEnemyAttackActivity activity) => attackActivities[enemy] = activity;
    private float ExternalAttackTime => Time.time - externalClockOffset;
    private void TickExternalPermissionClock()
    {
        if (externalClockFrame == Time.frameCount) return;
        externalClockFrame = Time.frameCount;
        if (externalClockPrevious >= 0f && AttackPermissions.Interrupted)
            externalClockOffset += Mathf.Max(0f, Time.time - externalClockPrevious);
        externalClockPrevious = Time.time;
    }
    public bool IsAttackIdle
    {
        get
        {
            foreach (var entry in attackActivities)
                if (entry.Key != null && !entry.Key.isDead && entry.Value.HasActiveAttack) return false;
            if (entranceAttackController != null && !entranceAttackController.IsIdle) return false;
            if (continuousEntranceAttackController != null && !continuousEntranceAttackController.IsIdle) return false;
            if (UsesAttackSequence) return sequenceRuntime == null || sequenceRuntime.IsIdle;
            if (attackController != null && !attackController.IsIdle) return false;
            for (int i = 0; i < attackPatternControllers.Count; i++)
                if (!attackPatternControllers[i].IsIdle) return false;
            return true;
        }
    }
    public int CompletedAttackCycles
    {
        get
        {
            if (UsesAttackSequence) return coordinatedCycleLimit >= 0 ? coordinatedSequenceCycles + (sequencePlayer != null && sequencePlayer.IsComplete ? 1 : 0)
                : sequencePlayer != null && sequencePlayer.ActiveCount == 0 ? sequencePlayer.CompletedPasses : 0;
            int count = int.MaxValue;
            if (!UsesAttackPatterns() && attackController != null) count = RelativeCycles(attackController);
            if (UsesAttackPatterns()) for (int i = 0; i < attackPatternControllers.Count; i++)
                    count = Mathf.Min(count, RelativeCycles(attackPatternControllers[i]));
            return count == int.MaxValue ? 0 : count;
        }
    }
    public void SetExternalAttackPermission(object owner, WaveAttackPermission permission)
    {
        TickExternalPermissionClock();
        AttackPermissions.Set(owner, permission);
        entranceAttackController?.SetSuspended(AttackPermissions.Interrupted);
    }
    public void ReleaseExternalAttackPermission(object owner)
    { TickExternalPermissionClock(); AttackPermissions.Release(owner); entranceAttackController?.SetSuspended(AttackPermissions.Interrupted); }
    public void SetCoordinatedCycleLimit(int limit)
    {
        coordinatedCycleLimit = limit;
        if (attackController != null) attackController.SetCycleLimit(AbsoluteLimit(attackController, limit));
        for (int i = 0; i < attackPatternControllers.Count; i++) attackPatternControllers[i].SetCycleLimit(AbsoluteLimit(attackPatternControllers[i], limit));
        if (sequencePlayer != null) sequencePlayer.PassLimit = limit < 0 ? -1 : 1;
    }
    public void ReactivateCoordinatedAttack(WaveAttackReactivation mode)
    {
        wave ??= GetComponent<DirectedEnemySubWave>();
        coordinatedSequenceCycles = 0;
        cycleBaselines.Clear();
        if (mode == WaveAttackReactivation.Restart && wave != null && wave.IsPostTimelineRunning)
        {
            // Restart the firing sequence without erasing cooldowns from completed attacks.
            // Deadlines use each enemy's attack clock, which survives the controller restart.
            var cooldowns = attackController?.CaptureAttackCooldowns();
            var patternCooldowns = attackPatternControllers.Count > 0
                ? new List<Dictionary<Enemy, float>>(attackPatternControllers.Count)
                : null;
            var patternNumerics = attackPatternControllers.Count > 0 ? new List<EnemyAttackNumericState>(attackPatternControllers.Count) : null;
            for (int i = 0; i < attackPatternControllers.Count; i++)
            {
                patternCooldowns.Add(attackPatternControllers[i].CaptureAttackCooldowns());
                patternNumerics.Add(attackPatternControllers[i].SchedulingNumeric);
            }
            sequencePlayer?.Stop();
            ((IDirectedWavePostTimelineBehaviour)this).OnPostTimelineStarted(wave);
            attackController?.RestoreAttackCooldowns(cooldowns);
            for (int i = 0; patternCooldowns != null && i < patternCooldowns.Count && i < attackPatternControllers.Count; i++)
            {
                attackPatternControllers[i].RestoreAttackCooldowns(patternCooldowns[i]);
                attackPatternControllers[i].RestoreSchedulingNumeric(patternNumerics[i]);
            }
        }
        if (attackController != null) cycleBaselines[attackController] = attackController.CompletedCycles;
        for (int i = 0; i < attackPatternControllers.Count; i++) cycleBaselines[attackPatternControllers[i]] = attackPatternControllers[i].CompletedCycles;
    }
    private int RelativeCycles(DirectedWaveAttackController controller)
        => controller.CompletedCycles - (cycleBaselines.TryGetValue(controller, out int baseline) ? baseline : 0);
    private int AbsoluteLimit(DirectedWaveAttackController controller, int limit)
        => limit < 0 ? -1 : limit + (cycleBaselines.TryGetValue(controller, out int baseline) ? baseline : 0);
    private void TickCoordinatedSequence()
    {
        if (sequencePlayer == null) return;
        sequencePlayer.AllowNewAttacks = AttackPermissions.AllowNewAttacks;
        sequencePlayer.Tick(ExternalAttackTime);
        if (coordinatedCycleLimit >= 0 && sequencePlayer.IsComplete && AttackPermissions.AllowNewAttacks
            && coordinatedSequenceCycles + 1 < coordinatedCycleLimit)
        { coordinatedSequenceCycles++; sequencePlayer.Begin(ExternalAttackTime, true); }
    }
    private void ApplyCoordinatedCycleLimit()
    { if (coordinatedCycleLimit >= 0) SetCoordinatedCycleLimit(coordinatedCycleLimit); }
}
