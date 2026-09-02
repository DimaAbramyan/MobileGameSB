using UnityEngine;

internal struct DirectedWaveRuntimeCheckpoint
{
    public Vector3 position;
    public float durationToNext;
    public DirectedWaveSegmentMotion motionToNext;
    public AnimationCurve easeToNext;
}

internal static class DirectedWavePathEvaluator
{
    private const int ArcLengthSampleCount = 24;

    public static Vector3 EvaluateSegment(
        DirectedWaveRuntimeCheckpoint[] checkpoints,
        int segmentIndex,
        float time)
    {
        DirectedWaveRuntimeCheckpoint current = checkpoints[segmentIndex];
        DirectedWaveRuntimeCheckpoint next = checkpoints[segmentIndex + 1];

        Vector3 previous = segmentIndex > 0
            ? checkpoints[segmentIndex - 1].position
            : current.position;
        Vector3 following = segmentIndex + 2 < checkpoints.Length
            ? checkpoints[segmentIndex + 2].position
            : next.position;

        return EvaluateSegment(
            previous,
            current.position,
            next.position,
            following,
            current.motionToNext,
            time);
    }

    public static float GetParameterAtNormalizedDistance(
        DirectedWaveRuntimeCheckpoint[] checkpoints,
        int segmentIndex,
        float normalizedDistance)
    {
        DirectedWaveRuntimeCheckpoint current = checkpoints[segmentIndex];
        Vector3 previous = segmentIndex > 0
            ? checkpoints[segmentIndex - 1].position
            : current.position;
        Vector3 next = checkpoints[segmentIndex + 1].position;
        Vector3 following = segmentIndex + 2 < checkpoints.Length
            ? checkpoints[segmentIndex + 2].position
            : next;

        return GetParameterAtNormalizedDistance(
            previous,
            current.position,
            next,
            following,
            current.motionToNext,
            normalizedDistance);
    }

    public static float GetParameterAtNormalizedDistance(
        Vector3 previous,
        Vector3 current,
        Vector3 next,
        Vector3 following,
        DirectedWaveSegmentMotion motion,
        float normalizedDistance)
    {
        if (motion == DirectedWaveSegmentMotion.Linear
            || normalizedDistance <= 0f
            || normalizedDistance >= 1f)
        {
            return normalizedDistance;
        }

        Vector3 previousPoint = current;
        float totalLength = 0f;
        for (int sample = 1; sample <= ArcLengthSampleCount; sample++)
        {
            float parameter = (float)sample / ArcLengthSampleCount;
            Vector3 point = EvaluateSegment(
                previous,
                current,
                next,
                following,
                motion,
                parameter);
            totalLength += Vector3.Distance(previousPoint, point);
            previousPoint = point;
        }

        if (totalLength <= Mathf.Epsilon)
            return normalizedDistance;

        float targetLength = totalLength * normalizedDistance;
        float accumulatedLength = 0f;
        previousPoint = current;
        for (int sample = 1; sample <= ArcLengthSampleCount; sample++)
        {
            float parameter = (float)sample / ArcLengthSampleCount;
            Vector3 point = EvaluateSegment(
                previous,
                current,
                next,
                following,
                motion,
                parameter);
            float stepLength = Vector3.Distance(previousPoint, point);
            if (accumulatedLength + stepLength >= targetLength)
            {
                float stepProgress = stepLength <= Mathf.Epsilon
                    ? 0f
                    : (targetLength - accumulatedLength) / stepLength;
                float previousParameter =
                    (float)(sample - 1) / ArcLengthSampleCount;
                return Mathf.Lerp(previousParameter, parameter, stepProgress);
            }

            accumulatedLength += stepLength;
            previousPoint = point;
        }

        return 1f;
    }

    public static Vector3 EvaluateSegment(
        Vector3 previous,
        Vector3 current,
        Vector3 next,
        Vector3 following,
        DirectedWaveSegmentMotion motion,
        float time)
    {
        return motion switch
        {
            DirectedWaveSegmentMotion.Bezier =>
                EvaluateBezier(previous, current, next, following, time),
            DirectedWaveSegmentMotion.CatmullRom =>
                EvaluateCatmullRom(previous, current, next, following, time),
            _ => Vector3.LerpUnclamped(current, next, time)
        };
    }

    private static Vector3 EvaluateBezier(
        Vector3 previous,
        Vector3 current,
        Vector3 next,
        Vector3 following,
        float time)
    {
        Vector3 p0 = current;
        Vector3 p3 = next;

        Vector3 p1 = p0 + (p3 - previous) / 6f;
        Vector3 p2 = p3 - (following - p0) / 6f;
        float t = Mathf.Clamp01(time);
        float oneMinusT = 1f - t;

        return oneMinusT * oneMinusT * oneMinusT * p0
            + 3f * oneMinusT * oneMinusT * t * p1
            + 3f * oneMinusT * t * t * p2
            + t * t * t * p3;
    }

    private static Vector3 EvaluateCatmullRom(
        Vector3 previous,
        Vector3 current,
        Vector3 next,
        Vector3 following,
        float time)
    {
        float t = Mathf.Clamp01(time);

        return 0.5f * (
            2f * current
            + (-previous + next) * t
            + (2f * previous - 5f * current
                + 4f * next - following)
            * t * t
            + (-previous + 3f * current
                - 3f * next + following)
            * t * t * t);
    }
}
