using System.Collections.Generic;
using UnityEngine;

internal sealed partial class DirectedWaveAttackController
{
    private readonly DirectedWaveAttackOrderPlan orderPlan = new();
    private readonly List<DirectedWaveAttackOrderSlot> orderSlots = new();
    private readonly Dictionary<int, Enemy> orderEnemies = new();
    private readonly List<Enemy> orderBatchEnemies = new();
    private bool orderBatchPending, orderFinished;

    private void ResetAttackOrder()
    {
        orderPlan.Reset(); orderSlots.Clear(); orderEnemies.Clear(); orderBatchEnemies.Clear();
        orderBatchPending = orderFinished = false;
    }

    private bool BuildOrderedAttackCycle()
    {
        if (singleCycle && cyclePrepared || completedCycles >= cycleLimit) return false;
        cycleCounted = false;
        cyclePrepared = true;
        wave.GetWaveAttackCandidates(candidates);
        orderSlots.Clear(); orderEnemies.Clear();
        for (int i = 0; i < candidates.Count; i++)
        {
            Enemy enemy = candidates[i];
            if (!CanUseAttackCandidate(enemy) || !HasFireMode(enemy) && !settings.UsesDiveMovement
                || !wave.TryGetFormationIndex(enemy, out int slot)) continue;
            Vector2 position = settings.AttackOrder.orderSource == DirectedWaveAttackOrderSource.SpatialOrder
                ? enemy.transform.position : wave.GetConfiguredFormationSlotPosition(slot);
            orderSlots.Add(new DirectedWaveAttackOrderSlot(slot, position)); orderEnemies[slot] = enemy;
        }
        return orderPlan.BuildCycle(orderSlots, settings.AttackOrder, settings.SampleAttacksPerCycle(schedulingNumeric), cycleLimit != int.MaxValue);
    }

    private void TickOrderedAttacks()
    {
        if (!isRunning || orderFinished || !settings.IsEnabled || DispatchTime < nextAttackTime) return;
        if (settings.UsesDiveMovement && settings.RequiresPlayerTarget && !wave.HasAttackTarget) return;
        if (orderBatchPending)
        {
            if (settings.AttackOrder.waitForPreviousEnemy)
                for (int i = 0; i < orderBatchEnemies.Count; i++)
                {
                    Enemy previous = orderBatchEnemies[i];
                    if (previous != null && !previous.isDead && previous.isActiveAndEnabled
                        && (IsAttackInProgress(previous) || !IsAttackReady(previous))) return;
                }
            orderBatchPending = false; orderBatchEnemies.Clear();
        }
        if (orderPlan.Exhausted && !BuildOrderedAttackCycle()) { orderFinished = true; return; }
        // Skip empty/dead batches without charging an interval or changing the remaining order.
        for (int guard = 0; guard < 256 && !orderPlan.Exhausted; guard++)
        {
            int batch = orderPlan.Entries[orderPlan.Cursor].Batch;
            while (!orderPlan.Exhausted && orderPlan.Entries[orderPlan.Cursor].Batch == batch)
            {
                int slot = orderPlan.Entries[orderPlan.Cursor].Slot;
                if (!orderEnemies.TryGetValue(slot, out Enemy enemy) || !CanUseAttackCandidate(enemy))
                { orderPlan.Cursor++; continue; }
                // Keep the head in place while its burst/cooldown finishes; never reorder ready enemies around it.
                if (IsAttackInProgress(enemy) || !IsAttackReady(enemy)) return;
                if (settings.UsesDiveMovement && !settings.AllowsConcurrentMovements && activeMovementEnemies.Count > 0) return;
                IWaveAttackExecutor executor = null;
                if (HasFireMode(enemy) && !TryGetExecutor(enemy, out executor)) { orderPlan.Cursor++; continue; }
                orderPlan.Cursor++; orderBatchEnemies.Add(enemy);
                StartEnemyAttack(enemy, executor);
            }
            if (orderBatchEnemies.Count == 0) continue;
            Enemy clock = orderBatchEnemies[0];
            dispatchClockOwner = clock; waitingForReady = false;
            nextAttackTime = clock.AttackTime + Mathf.Max(0f, settings.AttackOrder.attackIntervalBetweenEnemies)
                / GetFireRateMultiplier(clock);
            orderBatchPending = true;
            return;
        }
    }
}
