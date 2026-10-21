using UnityEngine;

public sealed partial class WaveShootingPreviewSession
{
    private void TickOrderedGroup(Group group, float time)
    {
        if (group.OrderFinished || time + .00001f < group.NextTime) return;
        var order = group.Settings.AttackOrder;
        if (group.OrderBatchPending)
        {
            if (order.waitForPreviousEnemy)
                for (int i = 0; i < group.OrderBatch.Count; i++)
                    if (group.OrderBatch[i].Active || time + .00001f < group.OrderBatch[i].ReadyTime) return;
            group.OrderBatchPending = false; group.OrderBatch.Clear();
        }
        if (group.OrderPlan.Exhausted)
        {
            if ((group.SequenceOwned || coordinated && coordinatorCycleLimit >= 0 && !group.CoordinatorCounted) && group.OrderInitialized) { group.OrderFinished = true; return; }
            group.OrderSlots.Clear(); group.OrderedSlots.Clear();
            for (int i = 0; i < group.Slots.Count; i++)
            {
                var slot = group.Slots[i];
                if (slot.FireMode == DirectedWaveAttackFireMode.None) continue;
                Vector2 position = order.orderSource == DirectedWaveAttackOrderSource.SpatialOrder
                    ? slot.Position : wave.GetSimulationFormationPosition(slot.Index, previewParent);
                group.OrderSlots.Add(new DirectedWaveAttackOrderSlot(slot.Index, position));
                group.OrderedSlots[slot.Index] = slot;
            }
            group.OrderInitialized = true;
            PrepareGroupCycle(group);
            if (!group.OrderPlan.BuildCycle(group.OrderSlots, order, group.CycleAttacks, coordinated && coordinatorCycleLimit >= 0))
            { group.OrderFinished = true; return; }
        }
        for (int guard = 0; guard < 256 && !group.OrderPlan.Exhausted; guard++)
        {
            int batch = group.OrderPlan.Entries[group.OrderPlan.Cursor].Batch;
            while (!group.OrderPlan.Exhausted && group.OrderPlan.Entries[group.OrderPlan.Cursor].Batch == batch)
            {
                int index = group.OrderPlan.Entries[group.OrderPlan.Cursor].Slot;
                if (!group.OrderedSlots.TryGetValue(index, out var slot)) { group.OrderPlan.Cursor++; continue; }
                if (slot.Active || (coordinated ? positionsTime : time) < slot.SpawnTime || time + .00001f < slot.ReadyTime || !positions.ContainsKey(index)) return;
                group.OrderPlan.Cursor++; group.OrderBatch.Add(slot);
                StartPreviewAttack(slot, group, time);
            }
            if (group.OrderBatch.Count == 0) continue;
            group.NextTime = time + Mathf.Max(0f, order.attackIntervalBetweenEnemies);
            group.OrderBatchPending = true;
            return;
        }
    }

    private void DrawAttackOrders()
    {
        for (int i = 0; i < groups.Count; i++)
        {
            var group = groups[i];
            if (group.Settings.UsesAttackOrder && !group.Autonomous && !group.Entrance)
                DirectedWaveAttackOrderScenePreview.DrawPlan(group.OrderPlan, positions, group.OrderPlan.Cursor);
        }
    }
}
