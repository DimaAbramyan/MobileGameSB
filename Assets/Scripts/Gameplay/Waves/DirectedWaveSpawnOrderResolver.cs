using UnityEngine;

public static class DirectedWaveSpawnOrderResolver
{
    public static int[] Build(
        Vector3[] positions,
        DirectedWaveSpawnOrderMode mode,
        float directionAngle,
        float startAngle)
    {
        int count = positions != null ? positions.Length : 0;
        int[] order = new int[count];
        for (int i = 0; i < count; i++)
            order[i] = i;

        if (count <= 1 || mode == DirectedWaveSpawnOrderMode.Manual)
            return order;

        Vector3 center = Vector3.zero;
        for (int i = 0; i < count; i++)
            center += positions[i];

        center /= count;
        System.Array.Sort(
            order,
            (left, right) => Compare(
                left,
                right,
                positions,
                center,
                mode,
                directionAngle,
                startAngle));
        return order;
    }

    private static int Compare(
        int left,
        int right,
        Vector3[] positions,
        Vector3 center,
        DirectedWaveSpawnOrderMode mode,
        float directionAngle,
        float startAngle)
    {
        int result = mode switch
        {
            DirectedWaveSpawnOrderMode.DirectionAngle =>
                CompareByDirectionProjection(
                    positions[left],
                    positions[right],
                    directionAngle),
            DirectedWaveSpawnOrderMode.CenterToOutside =>
                CompareByDistanceFromCenter(
                    positions[left],
                    positions[right],
                    center,
                    false),
            DirectedWaveSpawnOrderMode.OutsideToCenter =>
                CompareByDistanceFromCenter(
                    positions[left],
                    positions[right],
                    center,
                    true),
            DirectedWaveSpawnOrderMode.Clockwise =>
                CompareByAngleAroundCenter(
                    positions[left],
                    positions[right],
                    center,
                    startAngle,
                    true),
            DirectedWaveSpawnOrderMode.CounterClockwise =>
                CompareByAngleAroundCenter(
                    positions[left],
                    positions[right],
                    center,
                    startAngle,
                    false),
            _ => left.CompareTo(right)
        };

        return result != 0 ? result : left.CompareTo(right);
    }

    private static int CompareByDirectionProjection(
        Vector3 left,
        Vector3 right,
        float angleDegrees)
    {
        float radians = angleDegrees * Mathf.Deg2Rad;
        Vector2 direction = new(Mathf.Cos(radians), Mathf.Sin(radians));
        float leftProjection = Vector2.Dot(left, direction);
        float rightProjection = Vector2.Dot(right, direction);
        return leftProjection.CompareTo(rightProjection);
    }

    private static int CompareByDistanceFromCenter(
        Vector3 left,
        Vector3 right,
        Vector3 center,
        bool outsideFirst)
    {
        float leftDistance = ((Vector2)(left - center)).sqrMagnitude;
        float rightDistance = ((Vector2)(right - center)).sqrMagnitude;
        int result = leftDistance.CompareTo(rightDistance);
        return outsideFirst ? -result : result;
    }

    private static int CompareByAngleAroundCenter(
        Vector3 left,
        Vector3 right,
        Vector3 center,
        float startAngle,
        bool clockwise)
    {
        float leftAngle = GetNormalizedAngle(left - center, startAngle);
        float rightAngle = GetNormalizedAngle(right - center, startAngle);
        int result = leftAngle.CompareTo(rightAngle);
        return clockwise ? result : -result;
    }

    private static float GetNormalizedAngle(Vector3 offset, float startAngle)
    {
        float angle = Mathf.Atan2(offset.y, offset.x) * Mathf.Rad2Deg;
        float delta = Mathf.DeltaAngle(startAngle, angle);
        return Mathf.Repeat(-delta, 360f);
    }
}
