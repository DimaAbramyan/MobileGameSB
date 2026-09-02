using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public sealed partial class DirectedEnemySubWave
{
    private readonly HashSet<Enemy> timelineDetachedEnemies = new();

    private Dictionary<int, Vector3> runtimeTimelineFrame;
    private bool runtimeTimelineRunning;

    private IEnumerator RunUnifiedTimeline()
    {
        if (postStartDelay > 0f)
            yield return new WaitForSeconds(postStartDelay);

        if (!HasRuntimePostBehavior())
            yield break;

        ResetRuntimeTimelineState();
        runtimeTimelineRunning = true;
        BeginPostTimelineBehaviours();
        float timelineStartTime = Time.time;

        while (HasAliveEnemies())
        {
            float runtimeTimelineElapsed = Mathf.Max(
                0f,
                Time.time - timelineStartTime);
            Dictionary<int, Vector3> frame = EvaluateSimulationPipeline(
                runtimeTimelineElapsed,
                null,
                runtimeTimelineEvaluationContext);
            runtimeTimelineFrame = frame;

            TickPostTimelineBehaviours();
            ApplyPipelinePositions(frame);
            yield return null;
        }

        runtimeTimelineRunning = false;
        StopPostTimelineBehaviours();
    }

    internal bool HasAttackTarget => playerController != null;

    internal void GetWaveAttackCandidates(List<Enemy> result)
    {
        result.Clear();
        foreach (Enemy enemy in aliveEnemies)
        {
            if (enemy != null && !enemy.isDead && enemy.isActiveAndEnabled)
                result.Add(enemy);
        }
    }

    internal void SetTimelineEnemyDetached(Enemy enemy, bool isDetached)
    {
        if (enemy == null)
            return;

        if (isDetached)
        {
            timelineDetachedEnemies.Add(enemy);
            return;
        }

        timelineDetachedEnemies.Remove(enemy);
        ClearAttackMotion(enemy);
    }

    internal void MoveAttackEnemy(Enemy enemy, Vector3 position)
    {
        if (enemy == null || enemy.isDead)
            return;

        SetAttackMotionPosition(enemy, position);
    }

    internal Vector3 GetTimelineReturnPosition(Enemy enemy)
    {
        if (TryGetRuntimeTimelinePosition(enemy, out Vector3 timelinePosition))
            return timelinePosition;

        if (enemy != null
            && entranceRoutePositions.TryGetValue(enemy, out Vector3 routePosition))
        {
            return routePosition;
        }

        if (enemy != null
            && formationPositions.TryGetValue(enemy, out Vector3 formationPosition))
        {
            return formationPosition;
        }

        return enemy != null ? enemy.transform.position : GetFormationPosition(0);
    }

    private bool TryGetRuntimeTimelinePosition(
        Enemy enemy,
        out Vector3 position)
    {
        position = Vector3.zero;
        if (!runtimeTimelineRunning
            || enemy == null
            || !formationIndices.TryGetValue(enemy, out int formationIndex))
        {
            return false;
        }

        return runtimeTimelineFrame != null
            && runtimeTimelineFrame.TryGetValue(formationIndex, out position);
    }

    private void ResetRuntimeTimelineState()
    {
        StopPostTimelineBehaviours();
        timelineDetachedEnemies.Clear();
        entranceRoutePositions.Clear();
        attackMotionPositions.Clear();
        runtimeTimelineFrame = null;
        runtimeTimelineRunning = false;
        runtimeTimelineEvaluationContext.Reset();
    }
}
