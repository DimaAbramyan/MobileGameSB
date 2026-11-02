using UnityEngine;

public sealed partial class WaveShootingPreviewSession : IWaveAttackCoordinatorParticipant
{
    private readonly WaveAttackPermissions coordinatorPermissions = new();
    private bool coordinated;
    private int coordinatorCycleLimit = -1;
    private int coordinatorSequenceCycles;
    private float coordinatorOffset, coordinatorPrevious = -1f;
    private bool coordinatorPostClockBound;
    public string CoordinatorStatus => State == WaveAttackParticipantState.Pending ? "Entry / not ready"
        : coordinatorPermissions.Interrupted ? "Interrupted / waiting"
        : !coordinatorPermissions.AllowNewAttacks ? "Finishing current attack" : $"Attacking · cycles {CompletedCycles}";
    public WaveAttackParticipantState State => disposed ? WaveAttackParticipantState.Gone
        : Time >= postStart ? WaveAttackParticipantState.Ready : WaveAttackParticipantState.Pending;
    public int CompletedCycles
    {
        get
        {
            if (usesAttackSequence) return coordinatorCycleLimit >= 0 ? coordinatorSequenceCycles + (sequencePlayer != null && sequencePlayer.IsComplete ? 1 : 0)
                : sequencePlayer != null && sequencePlayer.ActiveCount == 0 ? sequencePlayer.CompletedPasses : 0;
            int result = int.MaxValue;
            for (int i = 0; i < groups.Count; i++)
                if (!groups[i].Entrance && !groups[i].Autonomous && !groups[i].SequenceOwned)
                    result = Mathf.Min(result, groups[i].CoordinatorCycles);
            return result == int.MaxValue ? 0 : result;
        }
    }
    public bool IsIdle
    {
        get
        {
            for (int i = 0; i < slots.Count; i++) if (slots[i].Active) return false;
            if (Time < postStart) for (int i = 0; i < entranceSequences.Count; i++)
                if (entranceSequences[i].Next > 0 && entranceSequences[i].Next < entranceSequences[i].Count) return false;
            return volleys.Count == 0;
        }
    }
    public void SetPermission(object owner, WaveAttackPermission permission)
    { coordinated = true; coordinatorPermissions.Set(owner, permission); }
    public void ReleasePermission(object owner) => coordinatorPermissions.Release(owner);
    public void SetCycleLimit(int limit)
    {
        if (!coordinated) InitializeCoordinatorCycles();
        coordinated = true; coordinatorCycleLimit = limit;
        if (sequencePlayer != null) sequencePlayer.PassLimit = limit < 0 ? -1 : 1;
    }
    private void InitializeCoordinatorCycles()
    {
        for (int i = 0; i < groups.Count; i++) groups[i].CycleAttacks = 0;
        for (int i = 0; i < slots.Count; i++)
            if (!slots[i].Entrance && slots[i].Group != null && !slots[i].Group.SequenceOwned)
                slots[i].RemainingSequenceAttacks = int.MaxValue;
    }
    private void PrepareGroupCycle(Group group)
    {
        if (group.CycleAttacks > 0) return;
        group.CycleAttacks = group.Settings.SampleAttacksPerCycle(group.Numeric);
        foreach (var slot in group.Slots)
            slot.RemainingSequenceAttacks = slot.FireMode == DirectedWaveAttackFireMode.None ? 0 : group.CycleAttacks;
    }
    public void Reactivate(WaveAttackReactivation mode)
    {
        coordinatorSequenceCycles = 0;
        if (mode != WaveAttackReactivation.Restart)
        {
            for (int i = 0; i < groups.Count; i++) groups[i].CoordinatorCycles = 0;
            return;
        }
        float time = Time - coordinatorOffset;
        sequencePlayer?.Begin(time, true);
        for (int i = 0; i < groups.Count; i++)
        {
            var group = groups[i];
            if (group.Entrance || group.Autonomous || group.SequenceOwned) continue;
            group.CoordinatorCycles = 0; group.CoordinatorCounted = false;
            group.NextSlot = group.Round = group.Active = 0; group.NextTime = time + group.Settings.SampleStartDelay(group.Numeric);
            group.OrderPlan.Reset(); group.OrderInitialized = group.OrderFinished = group.OrderBatchPending = false;
            group.OrderBatch.Clear();
            // Match runtime Restart: reset the shot sequence while retaining each slot's cooldown.
            foreach (var slot in group.Slots)
            {
                slot.AttackProgram?.Pause(time);
                slot.Active = false;
                slot.Numeric.ReleaseAll();
                foreach (var pattern in slot.Patterns) pattern.Numeric = null;
            }
        }
        for (int i = volleys.Count - 1; i >= 0; i--)
            if (!volleys[i].Slot.Entrance) { volleyPool.Push(volleys[i]); volleys.RemoveAt(i); }
        InitializeCoordinatorCycles();
    }
    private void TickCoordinatorSequence(float time)
    {
        using var profileScope = WavePreviewProfiler.Measure(WavePreviewProfiler.Area.AttackSequence);
        sequenceTime = time;
        sequencePlayer?.Tick(time);
        if (sequencePlayer != null && sequencePlayer.IsComplete)
        {
            foreach (var pose in sequenceRotationPoses) pose.EndLayout(time, true);
            sequenceRotationPoses.Clear();
        }
        if (coordinated && coordinatorCycleLimit >= 0 && sequencePlayer != null && sequencePlayer.IsComplete
            && coordinatorPermissions.AllowNewAttacks && coordinatorSequenceCycles + 1 < coordinatorCycleLimit)
        { coordinatorSequenceCycles++; sequencePlayer.Begin(time, true); }
    }
    private float CoordinatorAttackTime(float time)
    {
        if (!coordinated) return time;
        if (coordinatorPrevious >= 0f && coordinatorPermissions.Interrupted)
        {
            float paused = Mathf.Max(0f, time - coordinatorPrevious);
            coordinatorOffset += paused;
            for (int i = 0; i < entranceSequences.Count; i++)
                if (entranceSequences[i].Next > 0 && entranceSequences[i].Next < entranceSequences[i].Count)
                    entranceSequences[i].Start += paused;
        }
        coordinatorPrevious = time;
        return time - coordinatorOffset;
    }
    private void BindCoordinatorPostClock(float worldTime, float attackTime)
    {
        if (!coordinated || coordinatorPostClockBound || worldTime < postStart) return;
        coordinatorPostClockBound = true;
        for (int i = 0; i < groups.Count; i++)
            if (!groups[i].Entrance && !groups[i].SequenceOwned) groups[i].NextTime -= coordinatorOffset;
        sequencePlayer?.Begin(postStart - coordinatorOffset, true);
    }
    private void UpdateCoordinatorCycles(float time)
    {
        if (!coordinated || usesAttackSequence) return;
        for (int i = 0; i < groups.Count; i++)
        {
            var group = groups[i];
            if (group.Entrance || group.Autonomous || group.SequenceOwned) continue;
            bool complete = group.Settings.UsesAttackOrder ? group.OrderInitialized && group.OrderPlan.Exhausted : true;
            foreach (var slot in group.Slots)
                complete &= !slot.Active && time + .00001f >= slot.ReadyTime
                    && (group.Settings.UsesAttackOrder || slot.RemainingSequenceAttacks <= 0 || slot.FireMode == DirectedWaveAttackFireMode.None);
            if (complete && !group.CoordinatorCounted) { group.CoordinatorCycles++; group.CoordinatorCounted = true; }
            if (!group.CoordinatorCounted || !coordinatorPermissions.AllowNewAttacks
                || coordinatorCycleLimit >= 0 && group.CoordinatorCycles >= coordinatorCycleLimit) continue;
            group.CoordinatorCounted = false;
            group.CycleAttacks = 0;
            foreach (var slot in group.Slots) slot.RemainingSequenceAttacks = int.MaxValue;
            group.OrderFinished = false;
            if (group.Settings.UsesAttackOrder) group.OrderInitialized = false;
        }
    }
}
