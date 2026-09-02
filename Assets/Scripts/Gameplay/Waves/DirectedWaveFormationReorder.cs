using System.Collections.Generic;
using UnityEngine;

public sealed partial class DirectedEnemySubWave
{
    private sealed class FormationReorderCache
    {
        public int enemyCount;
        public DirectedWaveFormationReorderMode mode;
        public int randomSeed;
        public int[] targetIndices = System.Array.Empty<int>();
    }

    private readonly Dictionary<DirectedWavePostCommand, FormationReorderCache>
        formationReorderCaches = new();

    private readonly List<Vector3> formationReorderCenters = new();

    private float GetFormationReorderDuration(
        DirectedWavePostCommand command)
    {
        int enemyCount = GetEffectiveEnemyCount();
        if (command == null || enemyCount <= 0)
            return 0.01f;

        int[] targetIndices = GetFormationReorderTargets(command, enemyCount);
        float speed = Mathf.Max(0.01f, command.formationReorderSpeed);
        float startInterval = Mathf.Max(
            0f,
            command.formationReorderStartInterval);
        int shipsPerBatch = Mathf.Max(
            1,
            command.formationReorderShipsPerBatch);
        float maxTravelDistance = command.formationReorderUseTargetCenter
            ? GetMaximumTargetCenterReorderDistance(
                command,
                enemyCount)
            : 0f;

        if (!command.formationReorderUseTargetCenter)
        {
            for (int sourceIndex = 0; sourceIndex < enemyCount; sourceIndex++)
            {
                int targetIndex = targetIndices[sourceIndex];
                maxTravelDistance = Mathf.Max(
                    maxTravelDistance,
                    Vector3.Distance(
                        GetFormationPosition(sourceIndex),
                        GetFormationPosition(targetIndex)));
            }
        }

        float lastStartTime = (enemyCount - 1) / shipsPerBatch
            * startInterval;
        return Mathf.Max(
            0.01f,
            lastStartTime + maxTravelDistance / speed);
    }

    private float GetMaximumTargetCenterReorderDistance(
        DirectedWavePostCommand command,
        int enemyCount)
    {
        Vector3 formationCenter = GetFormationReorderCenter(enemyCount);
        CollectFormationReorderCenters(formationCenter);

        float maxDistance = 0f;
        for (int centerIndex = 0;
             centerIndex < formationReorderCenters.Count;
             centerIndex++)
        {
            Vector3 sourceOffset = formationReorderCenters[centerIndex]
                - formationCenter;
            for (int sourceIndex = 0;
                 sourceIndex < enemyCount;
                 sourceIndex++)
            {
                Vector3 source = GetFormationPosition(sourceIndex)
                    + sourceOffset;
                for (int targetIndex = 0;
                     targetIndex < enemyCount;
                     targetIndex++)
                {
                    Vector3 target = GetFormationPosition(targetIndex)
                        + command.formationReorderTargetCenter
                        - formationCenter;
                    maxDistance = Mathf.Max(
                        maxDistance,
                        Vector3.Distance(source, target));
                }
            }
        }

        return maxDistance;
    }

    private void CollectFormationReorderCenters(Vector3 formationCenter)
    {
        formationReorderCenters.Clear();
        AddFormationReorderCenter(formationCenter);
        CollectFormationReorderCenters(postCommands);
    }

    private void CollectFormationReorderCenters(
        DirectedWavePostCommand[] commands)
    {
        if (commands == null)
            return;

        for (int i = 0; i < commands.Length; i++)
        {
            DirectedWavePostCommand command = commands[i];
            if (command == null || !command.enabled)
                continue;

            if (command.type == DirectedWavePostCommandType.FormationReorder
                && command.formationReorderUseTargetCenter)
            {
                AddFormationReorderCenter(command.formationReorderTargetCenter);
            }

            CollectFormationReorderCenters(command.parallelCommands);
            CollectFormationReorderCenters(command.loopCommands);
        }
    }

    private void AddFormationReorderCenter(Vector3 center)
    {
        for (int i = 0; i < formationReorderCenters.Count; i++)
        {
            if (formationReorderCenters[i] == center)
                return;
        }

        formationReorderCenters.Add(center);
    }

    private Vector3 GetFormationReorderCenter(int enemyCount)
    {
        if (enemyCount <= 0)
            return transform.position;

        Vector3 center = Vector3.zero;
        for (int i = 0; i < enemyCount; i++)
            center += GetFormationPosition(i);

        return center / enemyCount;
    }

    private int[] GetFormationReorderTargets(
        DirectedWavePostCommand command,
        int enemyCount)
    {
        if (command == null || enemyCount <= 0)
            return System.Array.Empty<int>();

        if (!formationReorderCaches.TryGetValue(
                command,
                out FormationReorderCache cache))
        {
            cache = new FormationReorderCache();
            formationReorderCaches.Add(command, cache);
        }

        if (cache.enemyCount == enemyCount
            && cache.mode == command.formationReorderMode
            && cache.randomSeed == command.formationReorderRandomSeed)
        {
            return cache.targetIndices;
        }

        cache.enemyCount = enemyCount;
        cache.mode = command.formationReorderMode;
        cache.randomSeed = command.formationReorderRandomSeed;
        if (cache.targetIndices.Length != enemyCount)
            cache.targetIndices = new int[enemyCount];

        for (int i = 0; i < enemyCount; i++)
            cache.targetIndices[i] = i;

        switch (command.formationReorderMode)
        {
            case DirectedWaveFormationReorderMode.Mirror:
                for (int i = 0; i < enemyCount; i++)
                    cache.targetIndices[i] = enemyCount - 1 - i;
                break;

            case DirectedWaveFormationReorderMode.Random:
                ShuffleFormationReorderTargets(
                    cache.targetIndices,
                    command.formationReorderRandomSeed);
                break;
        }

        return cache.targetIndices;
    }

    private static void ShuffleFormationReorderTargets(
        int[] targetIndices,
        int seed)
    {
        uint state = unchecked((uint)seed);
        if (state == 0u)
            state = 0x6D2B79F5u;

        for (int i = targetIndices.Length - 1; i > 0; i--)
        {
            state = state * 1664525u + 1013904223u;
            int swapIndex = (int)(state % (uint)(i + 1));
            (targetIndices[i], targetIndices[swapIndex]) =
                (targetIndices[swapIndex], targetIndices[i]);
        }

        if (targetIndices.Length <= 1 || !IsIdentityReorder(targetIndices))
            return;

        int first = targetIndices[0];
        for (int i = 0; i < targetIndices.Length - 1; i++)
            targetIndices[i] = targetIndices[i + 1];
        targetIndices[targetIndices.Length - 1] = first;
    }

    private static bool IsIdentityReorder(int[] targetIndices)
    {
        for (int i = 0; i < targetIndices.Length; i++)
        {
            if (targetIndices[i] != i)
                return false;
        }

        return true;
    }

    private void ClearFormationReorderCache()
    {
        formationReorderCaches.Clear();
    }
}
