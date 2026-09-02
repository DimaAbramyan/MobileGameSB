using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public sealed partial class DirectedEnemySubWave
{
    // Entrance routes keep advancing while an attack temporarily owns the visible position.
    private readonly Dictionary<Enemy, Vector3> entranceRoutePositions = new();
    private readonly Dictionary<Enemy, Vector3> attackMotionPositions = new();

    private void SetEntranceRoutePosition(
        Enemy enemy,
        Transform target,
        Rigidbody2D body,
        Vector3 position)
    {
        if (enemy == null)
        {
            SetEnemyPosition(target, body, position);
            return;
        }

        entranceRoutePositions[enemy] = position;
        if (timelineDetachedEnemies.Contains(enemy)
            && attackMotionPositions.TryGetValue(enemy, out Vector3 attackPosition))
        {
            position = attackPosition;
        }

        SetEnemyPosition(target, body, position);
    }

    internal void SetAttackMotionPosition(Enemy enemy, Vector3 position)
    {
        if (enemy == null || enemy.isDead)
            return;

        attackMotionPositions[enemy] = position;
        SetEnemyPosition(
            enemy.transform,
            GetCachedEnemyBody(enemy),
            position);
    }

    internal IEnumerator ReturnAttackEnemyAlongEntrancePath(
        Enemy enemy,
        float speedMultiplier)
    {
        if (enemy == null || enemy.isDead)
            yield break;

        float safeSpeedMultiplier = Mathf.Max(0.01f, speedMultiplier);
        if (UsesIndividualEntrancePoints())
        {
            Vector3 startPosition = GetSpawnPosition();
            MoveAttackEnemy(enemy, startPosition);
            yield return MoveAttackEnemyBetween(
                enemy,
                startPosition,
                GetTimelineReturnPosition(enemy),
                individualPointMovementDuration / safeSpeedMultiplier,
                individualPointMovementCurve);
            yield break;
        }

        DirectedWaveRuntimeCheckpoint[] checkpoints = GetWorldPathCheckpoints();
        if (checkpoints.Length == 0)
        {
            MoveAttackEnemy(enemy, GetTimelineReturnPosition(enemy));
            yield break;
        }

        MoveAttackEnemy(enemy, checkpoints[0].position);
        for (int segmentIndex = 0;
             segmentIndex < checkpoints.Length - 1;
             segmentIndex++)
        {
            float duration = DirectedWaveEntranceLoopEvaluator.GetSegmentDuration(
                checkpoints,
                segmentIndex) / safeSpeedMultiplier;
            float elapsed = 0f;
            while (elapsed < duration && enemy != null && !enemy.isDead)
            {
                elapsed += Mathf.Min(Time.deltaTime, duration - elapsed);
                float curvedTime = EvaluateCurve(
                    checkpoints[segmentIndex].easeToNext,
                    Mathf.Clamp01(elapsed / duration));
                float pathTime =
                    DirectedWavePathEvaluator.GetParameterAtNormalizedDistance(
                        checkpoints,
                        segmentIndex,
                        curvedTime);
                MoveAttackEnemy(
                    enemy,
                    DirectedWavePathEvaluator.EvaluateSegment(
                        checkpoints,
                        segmentIndex,
                        pathTime));
                yield return null;
            }

            if (enemy == null || enemy.isDead)
                yield break;

            MoveAttackEnemy(enemy, checkpoints[segmentIndex + 1].position);
        }

        Vector3 routeEnd = checkpoints[checkpoints.Length - 1].position;
        yield return MoveAttackEnemyBetween(
            enemy,
            routeEnd,
            GetTimelineReturnPosition(enemy),
            settleDuration / safeSpeedMultiplier,
            settleCurve);
    }

    private IEnumerator MoveAttackEnemyBetween(
        Enemy enemy,
        Vector3 from,
        Vector3 to,
        float duration,
        AnimationCurve curve)
    {
        if (enemy == null || enemy.isDead)
            yield break;

        if (duration <= 0f)
        {
            MoveAttackEnemy(enemy, to);
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration && enemy != null && !enemy.isDead)
        {
            elapsed += Mathf.Min(Time.deltaTime, duration - elapsed);
            float progress = EvaluateCurve(
                curve,
                Mathf.Clamp01(elapsed / duration));
            MoveAttackEnemy(enemy, Vector3.LerpUnclamped(from, to, progress));
            yield return null;
        }

        if (enemy != null && !enemy.isDead)
            MoveAttackEnemy(enemy, to);
    }

    private Vector3 GetEntranceRoutePosition(
        Enemy enemy,
        Vector3 fallbackPosition)
    {
        return enemy != null
            && entranceRoutePositions.TryGetValue(enemy, out Vector3 position)
            ? position
            : fallbackPosition;
    }

    private void ClearAttackMotion(Enemy enemy)
    {
        if (enemy == null)
            return;

        attackMotionPositions.Remove(enemy);
    }

    private void ClearEnemyMotionState(Enemy enemy)
    {
        if (enemy == null)
            return;

        entranceRoutePositions.Remove(enemy);
        attackMotionPositions.Remove(enemy);
        timelineDetachedEnemies.Remove(enemy);
    }
}
