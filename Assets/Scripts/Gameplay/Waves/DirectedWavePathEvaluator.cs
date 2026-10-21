using UnityEngine;

public struct DirectedWaveRuntimeCheckpoint
{
    public Vector3 position;
    public float durationToNext;
    public DirectedWaveSegmentMotion motionToNext;
    public AnimationCurve easeToNext;
    public DirectedWaveCurveSettings curveSettings;
}

public static class DirectedWavePathEvaluator
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
            time, current.curveSettings, next.curveSettings);
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
            normalizedDistance, current.curveSettings, checkpoints[segmentIndex + 1].curveSettings);
    }

    public static float GetParameterAtNormalizedDistance(
        Vector3 previous,
        Vector3 current,
        Vector3 next,
        Vector3 following,
        DirectedWaveSegmentMotion motion,
        float normalizedDistance,
        DirectedWaveCurveSettings currentSettings = default,
        DirectedWaveCurveSettings nextSettings = default)
    {
        if (motion == DirectedWaveSegmentMotion.Linear
            || normalizedDistance <= 0f
            || normalizedDistance >= 1f)
        {
            return normalizedDistance;
        }

        System.Span<float> stepLengths = stackalloc float[ArcLengthSampleCount];
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
                parameter, currentSettings, nextSettings);
            float stepLength = Vector3.Distance(previousPoint, point);
            stepLengths[sample - 1] = stepLength;
            totalLength += stepLength;
            previousPoint = point;
        }

        if (totalLength <= Mathf.Epsilon)
            return normalizedDistance;

        float targetLength = totalLength * normalizedDistance;
        float accumulatedLength = 0f;
        for (int sample = 1; sample <= ArcLengthSampleCount; sample++)
        {
            float parameter = (float)sample / ArcLengthSampleCount;
            float stepLength = stepLengths[sample - 1];
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
        }

        return 1f;
    }

    public static Vector3 EvaluateSegment(
        Vector3 previous,
        Vector3 current,
        Vector3 next,
        Vector3 following,
        DirectedWaveSegmentMotion motion,
        float time,
        DirectedWaveCurveSettings currentSettings = default,
        DirectedWaveCurveSettings nextSettings = default)
    {
        return motion switch
        {
            DirectedWaveSegmentMotion.Bezier =>
                EvaluateBezierHandles(previous, current, next, following, time, currentSettings, nextSettings),
            DirectedWaveSegmentMotion.CatmullRom =>
                EvaluateCardinal(previous, current, next, following, time, currentSettings),
            DirectedWaveSegmentMotion.Arc => EvaluateArc(current, next, time, currentSettings.bulge, currentSettings.longArc),
            _ => Vector3.LerpUnclamped(current, next, time)
        };
    }

    private static Vector3 EvaluateBezierHandles(Vector3 previous, Vector3 current, Vector3 next,
        Vector3 following, float time, DirectedWaveCurveSettings start, DirectedWaveCurveSettings end)
    {
        if (start.handleMode == DirectedWaveBezierHandleMode.Auto && end.handleMode == DirectedWaveBezierHandleMode.Auto)
            return EvaluateBezier(previous, current, next, following, time);
        Vector3 outHandle = start.handleMode == DirectedWaveBezierHandleMode.Auto
            ? (next - previous) / 6f : start.outHandle;
        Vector3 inHandle = end.handleMode == DirectedWaveBezierHandleMode.Auto
            ? -(following - current) / 6f : end.inHandle;
        float t = Mathf.Clamp01(time), u = 1f - t;
        return u * u * u * current + 3f * u * u * t * (current + outHandle)
            + 3f * u * t * t * (next + inHandle) + t * t * t * next;
    }

    private static Vector3 EvaluateCardinal(Vector3 previous, Vector3 current, Vector3 next,
        Vector3 following, float time, DirectedWaveCurveSettings settings)
    {
        // Keep the exact old expression for all previously serialized routes.
        if (settings.parameterization == DirectedWaveCurveParameterization.Uniform && settings.tension == 0f)
            return EvaluateCatmullRom(previous, current, next, following, time);
        if ((next - current).sqrMagnitude < 1e-12f) return current;
        Vector3 m1, m2;
        if (settings.parameterization == DirectedWaveCurveParameterization.Uniform)
        {
            m1 = (next - previous) * .5f;
            m2 = (following - current) * .5f;
        }
        else
        {
            // Reflect repeated endpoint knots to avoid zero knot intervals.
            if ((current - previous).sqrMagnitude < 1e-12f) previous = 2f * current - next;
            if ((following - next).sqrMagnitude < 1e-12f) following = 2f * next - current;
            float alpha = settings.parameterization == DirectedWaveCurveParameterization.Centripetal ? .5f : 1f;
            float d0 = Mathf.Max(1e-5f, Mathf.Pow(Vector3.Distance(previous, current), alpha));
            float d1 = Mathf.Max(1e-5f, Mathf.Pow(Vector3.Distance(current, next), alpha));
            float d2 = Mathf.Max(1e-5f, Mathf.Pow(Vector3.Distance(next, following), alpha));
            m1 = d1 * ((current - previous) / d0 - (next - previous) / (d0 + d1) + (next - current) / d1);
            m2 = d1 * ((next - current) / d1 - (following - current) / (d1 + d2) + (following - next) / d2);
        }
        float scale = 1f - Mathf.Clamp01(settings.tension);
        float t = Mathf.Clamp01(time), t2 = t * t, t3 = t2 * t;
        return (2f * t3 - 3f * t2 + 1f) * current + (t3 - 2f * t2 + t) * (m1 * scale)
            + (-2f * t3 + 3f * t2) * next + (t3 - t2) * (m2 * scale);
    }

    public static Vector3 EvaluateArc(Vector3 current, Vector3 next, float time, float bulge, bool longArc)
    {
        Vector2 chord = (Vector2)(next - current);
        float length = chord.magnitude;
        float b = Mathf.Clamp(bulge, -1f, 1f);
        float t = Mathf.Clamp01(time);
        if (length < 1e-6f || Mathf.Abs(b) < 1e-5f) return Vector3.LerpUnclamped(current, next, t);
        if (t == 0f) return current;
        if (t == 1f) return next;
        Vector2 normal = new Vector2(-chord.y, chord.x) / length;
        Vector2 center = ((Vector2)current + (Vector2)next) * .5f
            - normal * (length * (1f - b * b) / (4f * b));
        Vector2 radial = (Vector2)current - center;
        float angle = 4f * Mathf.Atan(Mathf.Abs(b));
        float sweep = (longArc ? Mathf.Sign(b) * (2f * Mathf.PI - angle) : -Mathf.Sign(b) * angle) * t;
        float cos = Mathf.Cos(sweep), sin = Mathf.Sin(sweep);
        Vector2 position = center + new Vector2(radial.x * cos - radial.y * sin, radial.x * sin + radial.y * cos);
        return new Vector3(position.x, position.y, Mathf.Lerp(current.z, next.z, t));
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
