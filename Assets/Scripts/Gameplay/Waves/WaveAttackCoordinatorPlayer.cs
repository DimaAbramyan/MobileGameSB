using System;
using System.Collections.Generic;
using UnityEngine;

public interface IWaveAttackCoordinatorParticipant
{
    WaveAttackParticipantState State { get; }
    int CompletedCycles { get; }
    bool IsIdle { get; }
    void SetPermission(object owner, WaveAttackPermission permission);
    void ReleasePermission(object owner);
    void Reactivate(WaveAttackReactivation mode);
    void SetCycleLimit(int limit);
}

// Runtime and conductor Preview share readiness, handoff, budgets and ownership.
public sealed class WaveAttackCoordinatorPlayer : IDisposable
{
    public sealed class GroupState
    {
        public int Index;
        public WaveAttackCoordinatorGroup Settings;
        public string Status { get; internal set; } = "Waiting";
        public float StartedAt { get; internal set; } = float.NaN;
        public float Remaining { get; internal set; }
        internal float RequestedAt, CompleteAt;
        internal bool NextScheduled, Draining, Done;
        internal readonly Dictionary<int, int> Baselines = new();
    }
    private readonly WaveAttackCoordinatorSettings settings;
    private readonly Func<int, IWaveAttackCoordinatorParticipant> resolve;
    private readonly Dictionary<int, IWaveAttackCoordinatorParticipant> members = new();
    private readonly List<GroupState> active = new();
    private int nextIndex, pass;
    private float nextAt;
    private bool started, disposed, exhausted;
    public IReadOnlyList<GroupState> ActiveGroups => active;
    public bool IsComplete => exhausted && active.Count == 0;
    public int Timeouts { get; private set; }
    public int NextGroupIndex => exhausted ? -1 : nextIndex;

    public WaveAttackCoordinatorPlayer(WaveAttackCoordinatorSettings settings, Func<int, IWaveAttackCoordinatorParticipant> resolve)
    { this.settings = settings; this.resolve = resolve; }
    public void Begin(float time)
    {
        if (started) throw new InvalidOperationException("Dispose the previous coordinator run before restarting.");
        started = true;
        nextIndex = Mathf.Clamp(settings.startGroupIndex, 0, Mathf.Max(0, settings.groups.Count - 1));
        nextAt = time + Mathf.Max(0f, settings.startDelay);
        for (int i = 0; i < settings.groups.Count; i++)
            if (settings.groups[i]?.controlledSubWaves != null)
                foreach (var reference in settings.groups[i].controlledSubWaves)
                    if (reference != null && !members.ContainsKey(reference.scheduleIndex))
                        members.Add(reference.scheduleIndex, resolve(reference.scheduleIndex));
        exhausted = settings.groups.Count == 0;
        ApplyPermissions();
    }
    public void Tick(float time)
    {
        if (!started || disposed) return;
        for (int i = 0; i < active.Count; i++) TickGroup(active[i], time);
        for (int i = active.Count - 1; i >= 0; i--) if (active[i].Done) active.RemoveAt(i);
        // A bounded amount of scheduling work; zero-duration loops cannot stall a frame.
        for (int guard = 0; guard < 64 && !exhausted && time >= nextAt; guard++)
        {
            var group = settings.groups[nextIndex];
            var state = new GroupState { Index = nextIndex, Settings = group ?? new(), RequestedAt = time };
            nextAt = float.PositiveInfinity;
            active.Add(state);
            TickGroup(state, time);
            if (!state.Done) break;
            active.Remove(state);
        }
        ApplyPermissions();
    }
    private void TickGroup(GroupState run, float time)
    {
        var group = run.Settings;
        float age = Mathf.Max(0f, time - run.RequestedAt);
        bool timeout = group.maxWaitDuration > 0f && age >= group.maxWaitDuration;
        run.Remaining = group.maxWaitDuration > 0f ? Mathf.Max(0f, group.maxWaitDuration - age) : 0f;
        if (run.Draining)
        {
            bool idle = true;
            foreach (int index in run.Baselines.Keys)
                if (members.TryGetValue(index, out var member) && member != null && member.State != WaveAttackParticipantState.Gone && !member.IsIdle) idle = false;
            if (idle || timeout || group.overlapDuration > 0f && time >= run.CompleteAt + group.overlapDuration)
            { if (timeout) Timeouts++; run.Done = true; run.Status = timeout ? "Timed out" : "Completed"; ScheduleNext(run, time); }
            return;
        }
        if (timeout)
        {
            Timeouts++; run.Status = "Timed out"; run.Done = true; ScheduleNext(run, time); return;
        }
        if (age < group.startDelay) { run.Status = "Start delay"; run.Remaining = group.startDelay - age; return; }
        if (float.IsNaN(run.StartedAt))
        {
            if (group.waitUntilReady)
                foreach (var reference in group.controlledSubWaves)
                    if (reference != null && members.TryGetValue(reference.scheduleIndex, out var pending)
                        && pending != null && pending.State == WaveAttackParticipantState.Pending)
                    { run.Status = "Waiting for spawn / entry"; return; }
            foreach (var reference in group.controlledSubWaves)
            {
                // Only a participant lost during this activation may complete it through the death policy.
                if (reference == null || run.Baselines.ContainsKey(reference.scheduleIndex)
                    || !members.TryGetValue(reference.scheduleIndex, out var member) || member == null
                    || member.State == WaveAttackParticipantState.Gone) continue;
                bool alreadyActive = false;
                for (int i = 0; i < active.Count; i++)
                    if (active[i] != run && !active[i].Done && active[i].Baselines.ContainsKey(reference.scheduleIndex)) alreadyActive = true;
                if (!alreadyActive) member.Reactivate(settings.reactivation);
                run.Baselines.Add(reference.scheduleIndex, member.CompletedCycles);
            }
            run.StartedAt = time;
        }
        run.Status = "Attacking";
        if (group.completionMode == WaveAttackGroupCompletion.Timed)
            run.Remaining = Mathf.Max(0f, group.duration - (time - run.StartedAt));
        if (group.completionMode == WaveAttackGroupCompletion.Timed && group.overlapDuration > 0f)
        {
            float handoff = run.StartedAt + Mathf.Max(0f, group.duration) + group.delayAfterGroup + settings.globalCooldown - group.overlapDuration;
            if (time >= handoff) ScheduleNext(run, Mathf.Max(run.StartedAt, handoff), false);
        }
        bool all = true, first = false, anyAlive = false, anyGone = false;
        int cycles = group.completionMode == WaveAttackGroupCompletion.AttackCycles ? Mathf.Max(1, group.attackCyclesCount) : 1;
        foreach (var entry in run.Baselines)
        {
            var member = members[entry.Key];
            if (member.State == WaveAttackParticipantState.Gone) { anyGone = true; continue; }
            anyAlive = true;
            bool complete = member.CompletedCycles >= entry.Value + cycles;
            all &= complete; first |= complete;
        }
        bool finished = !anyAlive || anyGone && group.onEnemyDeath == WaveAttackEnemyDeath.CompleteGroup
            || (group.completionMode == WaveAttackGroupCompletion.Timed ? time - run.StartedAt >= group.duration
                : group.completionMode == WaveAttackGroupCompletion.FirstCompleted ? first : all);
        if (!finished) return;
        run.CompleteAt = time;
        run.Draining = settings.switchMode == WaveAttackSwitchMode.FinishCurrentAttack;
        run.Done = !run.Draining;
        run.Status = run.Draining ? "Finishing current attacks" : "Completed";
        if (group.overlapDuration > 0f || run.Done) ScheduleNext(run, time);
    }
    private void ScheduleNext(GroupState run, float time, bool includeDelay = true)
    {
        if (run.NextScheduled) return;
        run.NextScheduled = true;
        nextIndex = run.Index + 1;
        if (nextIndex >= settings.groups.Count)
        {
            nextIndex = 0; pass++;
            if (!settings.loopSequence && pass >= Mathf.Max(1, settings.loopCount)) { exhausted = true; return; }
        }
        nextAt = time + (includeDelay ? Mathf.Max(0f, run.Settings.delayAfterGroup) + Mathf.Max(0f, settings.globalCooldown) : 0f);
    }
    private void ApplyPermissions()
    {
        foreach (var entry in members)
        {
            var member = entry.Value;
            if (member == null) continue;
            bool allowed = false, draining = false, unlimited = false;
            int target = 0;
            for (int i = 0; i < active.Count; i++)
            {
                var run = active[i];
                if (run.Done || !run.Baselines.TryGetValue(entry.Key, out int baseline)) continue;
                if (run.Draining) { draining = true; continue; }
                allowed = true;
                if (run.Settings.completionMode == WaveAttackGroupCompletion.Timed) unlimited = true;
                else target = Mathf.Max(target, baseline + (run.Settings.completionMode == WaveAttackGroupCompletion.AttackCycles ? Mathf.Max(1, run.Settings.attackCyclesCount) : 1));
            }
            member.SetCycleLimit(unlimited ? -1 : target);
            member.SetPermission(this, allowed ? WaveAttackPermission.AllowAttack
                : draining ? WaveAttackPermission.BlockNewAttacks : WaveAttackPermission.InterruptAttacks);
        }
    }
    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        foreach (var member in members.Values)
            if (member != null) { member.SetCycleLimit(-1); member.ReleasePermission(this); }
        active.Clear(); members.Clear();
    }
}
