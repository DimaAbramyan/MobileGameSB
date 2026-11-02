using System.Collections.Generic;

public sealed partial class WaveShootingPreviewSession
{
    private float sequenceTime;
    private readonly List<SequenceRun> sequenceRuns = new();
    private readonly List<VisualPose> sequenceRotationPoses = new();
    private sealed class SequenceHost : IDirectedWaveAttackSequenceHost
    {
        private readonly WaveShootingPreviewSession owner;
        private readonly Slot groupSlot;
        private DirectedWaveAttackSequenceStep pendingStep;
        private List<Slot> chosen;
        private HashSet<int> indices;
        public SequenceHost(WaveShootingPreviewSession owner, Slot groupSlot = null) { this.owner = owner; this.groupSlot = groupSlot; }
        public bool TryStart(DirectedWaveAttackSequenceStep step, out IDirectedWaveAttackSequenceRun execution)
        {
            execution = null;
            if (!ReferenceEquals(pendingStep, step))
            {
                pendingStep = step; chosen = new List<Slot>(); indices = new HashSet<int>();
                for (int i = 0; i < owner.slots.Count; i++)
                {
                    var slot = owner.slots[i];
                    if (slot.Entrance || (groupSlot != null ? slot != groupSlot : slot.Group != null)) continue;
                    if (!(step.overrideTargetSlots ? step.targetSlots.Contains(slot.Index)
                        : groupSlot != null || owner.wave.AttackPatternBehaviour.AllowsSequenceSlot(slot.Index))) continue;
                    chosen.Add(slot); indices.Add(slot.Index);
                    if (groupSlot == null && !owner.sequenceRotationPoses.Contains(slot.Pose))
                    {
                        owner.sequenceRotationPoses.Add(slot.Pose);
                        slot.Pose.LayoutPrograms++; slot.Pose.LayoutProgramHasAttack = false;
                    }
                }
            }
            for (int i = owner.sequenceRuns.Count - 1; i >= 0; i--)
            {
                var previous = owner.sequenceRuns[i];
                if (previous.RootComplete) { owner.sequenceRuns.RemoveAt(i); continue; }
                if (previous.Indices.Overlaps(indices)) return false;
            }
            var settings = new DirectedWaveAttackSettings(); step.CopySettingsTo(settings);
            if (groupSlot != null) settings.UseSingleDispatchScheduling();
            var group = new Group { Settings = settings, SequenceOwned = true, GrantedGroupDispatch = groupSlot != null, Completion = new EnemyAttackCompletionTracker(),
                Numeric = settings.CreateSchedulingNumeric(NumericRandomPreviewControls.SeedOffset) };
            group.NextTime = owner.sequenceTime + settings.SampleStartDelay(group.Numeric);
            owner.groups.Add(group);
            for (int i = 0; i < chosen.Count; i++)
                owner.BuildSlot(chosen[i].Index, visual: chosen[i].Visual, suppliedSettings: settings,
                    suppliedGroup: group, sequenceSpeed: settings.RuntimeAttackSpeedMultiplier);
            var run = new SequenceRun(owner, group, indices);
            owner.sequenceRuns.Add(run); execution = run;
            return true;
        }
    }
    private sealed class SequenceRun : IDirectedWaveAttackSequenceRun
    {
        private readonly WaveShootingPreviewSession owner;
        private readonly Group group;
        public readonly HashSet<int> Indices;
        private bool released;
        public bool RootComplete => released;
        public bool ChildrenComplete => group.Completion.IsComplete;
        public SequenceRun(WaveShootingPreviewSession owner, Group group, HashSet<int> indices)
        { this.owner = owner; this.group = group; Indices = indices; }
        public void Tick()
        {
            if (released) return;
            float time = owner.sequenceTime;
            owner.TickGroup(group, time);
            bool pending = false;
            for (int i = 0; i < group.Slots.Count; i++)
            {
                Slot slot = group.Slots[i]; owner.TickSlot(slot, time);
                pending |= slot.Active || !group.Settings.UsesAttackOrder && slot.RemainingSequenceAttacks > 0 || time < slot.ReadyTime;
            }
            if (group.Settings.UsesAttackOrder) pending |= !group.OrderInitialized || !group.OrderPlan.Exhausted;
            if (!pending) Stop();
        }
        public void Stop()
        {
            if (released) return;
            released = true;
            for (int i = owner.volleys.Count - 1; i >= 0; i--)
                if (owner.volleys[i].Slot.Group == group)
                { owner.volleys[i].LaunchOrder.Cancel(); owner.volleyPool.Push(owner.volleys[i]); owner.volleys.RemoveAt(i); }
            for (int i = 0; i < group.Slots.Count; i++)
            { EndLayoutRotation(group.Slots[i], owner.sequenceTime); owner.slots.Remove(group.Slots[i]); }
            owner.groups.Remove(group);
        }
    }
}
