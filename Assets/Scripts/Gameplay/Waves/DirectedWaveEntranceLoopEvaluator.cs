using UnityEngine;

internal static class DirectedWaveEntranceLoopEvaluator
{
    private const float MinimumSegmentDuration = 0.01f;

    public static bool IsValid(
        DirectedWaveRuntimeCheckpoint[] checkpoints,
        int loopStartCheckpointIndex)
    {
        return checkpoints != null
            && checkpoints.Length >= 2
            && loopStartCheckpointIndex >= 0
            && loopStartCheckpointIndex < checkpoints.Length - 1;
    }

    public static int GetLoopStartCheckpointIndex(
        int requestedIndex,
        int checkpointCount)
    {
        return checkpointCount >= 2
            ? Mathf.Clamp(requestedIndex, 0, checkpointCount - 2)
            : 0;
    }

    public static float GetInitialTraversalDuration(
        DirectedWaveRuntimeCheckpoint[] checkpoints)
    {
        if (checkpoints == null || checkpoints.Length < 2)
            return 0f;

        float duration = 0f;
        for (int i = 0; i < checkpoints.Length - 1; i++)
            duration += GetSegmentDuration(checkpoints, i);

        return duration;
    }

    public static float GetLoopDuration(
        DirectedWaveRuntimeCheckpoint[] checkpoints,
        int loopStartCheckpointIndex,
        bool teleportToLoopStart = false,
        float teleportDelay = 0f)
    {
        if (!IsValid(checkpoints, loopStartCheckpointIndex))
            return 0f;

        int lastIndex = checkpoints.Length - 1;
        float duration = teleportToLoopStart
            ? Mathf.Max(0f, teleportDelay)
            : GetSegmentDuration(checkpoints, lastIndex);
        for (int i = loopStartCheckpointIndex; i < lastIndex; i++)
            duration += GetSegmentDuration(checkpoints, i);

        return duration;
    }

    public static Vector3 EvaluatePosition(
        DirectedWaveRuntimeCheckpoint[] checkpoints,
        float elapsed,
        int loopStartCheckpointIndex,
        bool teleportToLoopStart = false,
        float teleportDelay = 0f)
    {
        if (checkpoints == null || checkpoints.Length == 0)
            return Vector3.zero;

        if (checkpoints.Length == 1)
            return checkpoints[0].position;

        float remaining = Mathf.Max(0f, elapsed);
        int lastIndex = checkpoints.Length - 1;
        for (int i = 0; i < lastIndex; i++)
        {
            float duration = GetSegmentDuration(checkpoints, i);
            if (remaining <= duration)
                return EvaluateInitialSegment(checkpoints, i, remaining / duration);

            remaining -= duration;
        }

        if (!IsValid(checkpoints, loopStartCheckpointIndex))
            return checkpoints[lastIndex].position;

        float loopDuration = GetLoopDuration(
            checkpoints,
            loopStartCheckpointIndex,
            teleportToLoopStart,
            teleportDelay);
        if (loopDuration <= 0f)
            return checkpoints[lastIndex].position;

        return EvaluateLoopCycle(
            checkpoints,
            Mathf.Repeat(remaining, loopDuration),
            loopStartCheckpointIndex,
            teleportToLoopStart,
            teleportDelay);
    }

    public static Vector3 EvaluateLoopSegment(
        DirectedWaveRuntimeCheckpoint[] checkpoints,
        int previousIndex,
        int currentIndex,
        int nextIndex,
        int followingIndex,
        float time)
    {
        return DirectedWavePathEvaluator.EvaluateSegment(
            checkpoints[previousIndex].position,
            checkpoints[currentIndex].position,
            checkpoints[nextIndex].position,
            checkpoints[followingIndex].position,
            checkpoints[currentIndex].motionToNext,
            time);
    }

    public static float GetSegmentDuration(
        DirectedWaveRuntimeCheckpoint[] checkpoints,
        int segmentStartIndex)
    {
        return Mathf.Max(
            MinimumSegmentDuration,
            checkpoints[segmentStartIndex].durationToNext);
    }

    private static Vector3 EvaluateInitialSegment(
        DirectedWaveRuntimeCheckpoint[] checkpoints,
        int segmentIndex,
        float time)
    {
        float curvedTime = EvaluateCurve(
            checkpoints[segmentIndex].easeToNext,
            Mathf.Clamp01(time));
        float pathTime =
            DirectedWavePathEvaluator.GetParameterAtNormalizedDistance(
                checkpoints,
                segmentIndex,
                curvedTime);
        return DirectedWavePathEvaluator.EvaluateSegment(
            checkpoints,
            segmentIndex,
            pathTime);
    }

    private static Vector3 EvaluateLoopCycle(
        DirectedWaveRuntimeCheckpoint[] checkpoints,
        float elapsed,
        int loopStartCheckpointIndex,
        bool teleportToLoopStart,
        float teleportDelay)
    {
        int lastIndex = checkpoints.Length - 1;
        float remaining = Mathf.Max(0f, elapsed);

        if (teleportToLoopStart)
        {
            float delay = Mathf.Max(0f, teleportDelay);
            if (remaining < delay)
                return checkpoints[lastIndex].position;

            remaining -= delay;
        }
        else
        {
            float returnDuration = GetSegmentDuration(checkpoints, lastIndex);
            if (remaining <= returnDuration)
            {
                float curvedTime = EvaluateCurve(
                    checkpoints[lastIndex].easeToNext,
                    remaining / returnDuration);
                float pathTime = GetLoopPathTime(
                    checkpoints,
                    Mathf.Max(0, lastIndex - 1),
                    lastIndex,
                    loopStartCheckpointIndex,
                    Mathf.Min(lastIndex, loopStartCheckpointIndex + 1),
                    curvedTime);
                return EvaluateLoopSegment(
                    checkpoints,
                    Mathf.Max(0, lastIndex - 1),
                    lastIndex,
                    loopStartCheckpointIndex,
                    Mathf.Min(lastIndex, loopStartCheckpointIndex + 1),
                    pathTime);
            }

            remaining -= returnDuration;
        }
        for (int currentIndex = loopStartCheckpointIndex;
             currentIndex < lastIndex;
             currentIndex++)
        {
            float duration = GetSegmentDuration(checkpoints, currentIndex);
            if (remaining <= duration)
            {
                float curvedTime = EvaluateCurve(
                    checkpoints[currentIndex].easeToNext,
                    remaining / duration);
                int previousIndex = currentIndex == loopStartCheckpointIndex
                    ? (teleportToLoopStart ? currentIndex : lastIndex)
                    : currentIndex - 1;
                int followingIndex = currentIndex + 1 == lastIndex
                    ? (teleportToLoopStart ? lastIndex : loopStartCheckpointIndex)
                    : currentIndex + 2;
                float pathTime = GetLoopPathTime(
                    checkpoints,
                    previousIndex,
                    currentIndex,
                    currentIndex + 1,
                    followingIndex,
                    curvedTime);
                return EvaluateLoopSegment(
                    checkpoints,
                    previousIndex,
                    currentIndex,
                    currentIndex + 1,
                    followingIndex,
                    pathTime);
            }

            remaining -= duration;
        }

        return checkpoints[lastIndex].position;
    }

    private static float EvaluateCurve(AnimationCurve curve, float time)
    {
        return curve != null ? curve.Evaluate(time) : time;
    }

    private static float GetLoopPathTime(
        DirectedWaveRuntimeCheckpoint[] checkpoints,
        int previousIndex,
        int currentIndex,
        int nextIndex,
        int followingIndex,
        float normalizedDistance)
    {
        return DirectedWavePathEvaluator.GetParameterAtNormalizedDistance(
            checkpoints[previousIndex].position,
            checkpoints[currentIndex].position,
            checkpoints[nextIndex].position,
            checkpoints[followingIndex].position,
            checkpoints[currentIndex].motionToNext,
            normalizedDistance);
    }
}
