using UnityEngine;

internal readonly struct DirectedWaveFormationSettings
{
    public readonly bool frozen;
    public readonly DirectedWaveFormationLayout layout;
    public readonly DirectedWaveCoordinateSpace coordinateSpace;
    public readonly Vector3 center;
    public readonly Vector2 spacing;
    public readonly int columns;
    public readonly int rows;
    public readonly float arcRadius;
    public readonly float arcDegrees;
    public readonly float shapeRadius;
    public readonly Vector2 shapeFlattening;
    public readonly Vector3[] customPoints;
    public readonly Transform pointsRoot;
    public readonly Transform subWaveTransform;
    public readonly Transform spawnPoint;
    public readonly int enemyCount;

    public DirectedWaveFormationSettings(
        bool frozen,
        DirectedWaveFormationLayout layout,
        DirectedWaveCoordinateSpace coordinateSpace,
        Vector3 center,
        Vector2 spacing,
        int columns,
        int rows,
        float arcRadius,
        float arcDegrees,
        float shapeRadius,
        Vector2 shapeFlattening,
        Vector3[] customPoints,
        Transform pointsRoot,
        Transform subWaveTransform,
        Transform spawnPoint,
        int enemyCount)
    {
        this.frozen = frozen;
        this.layout = layout;
        this.coordinateSpace = coordinateSpace;
        this.center = center;
        this.spacing = spacing;
        this.columns = columns;
        this.rows = rows;
        this.arcRadius = arcRadius;
        this.arcDegrees = arcDegrees;
        this.shapeRadius = shapeRadius;
        this.shapeFlattening = shapeFlattening;
        this.customPoints = customPoints;
        this.pointsRoot = pointsRoot;
        this.subWaveTransform = subWaveTransform;
        this.spawnPoint = spawnPoint;
        this.enemyCount = enemyCount;
    }
}

internal static class DirectedWaveFormationSolver
{
    public static Vector3 GetPosition(
        int index,
        in DirectedWaveFormationSettings settings)
    {
        if (settings.frozen)
            return GetTransformPosition(index, settings);

        Vector3 localPosition = settings.layout switch
        {
            DirectedWaveFormationLayout.VerticalLine =>
                GetVerticalLinePosition(index, settings),
            DirectedWaveFormationLayout.Grid =>
                GetGridPosition(index, settings),
            DirectedWaveFormationLayout.VShape =>
                GetVShapePosition(index, settings),
            DirectedWaveFormationLayout.Arc =>
                GetArcPosition(index, settings),
            DirectedWaveFormationLayout.Circle =>
                GetCirclePosition(index, settings),
            DirectedWaveFormationLayout.Triangle =>
                GetPolygonPerimeterPosition(
                    index,
                    GetTriangleVertices(settings),
                    settings),
            DirectedWaveFormationLayout.Square =>
                GetPolygonPerimeterPosition(
                    index,
                    GetSquareVertices(settings),
                    settings),
            DirectedWaveFormationLayout.Diamond =>
                GetPolygonPerimeterPosition(
                    index,
                    GetDiamondVertices(settings),
                    settings),
            DirectedWaveFormationLayout.CustomPoints =>
                GetCustomPosition(index, settings),
            DirectedWaveFormationLayout.TransformPoints =>
                GetTransformPosition(index, settings),
            _ => GetHorizontalLinePosition(index, settings)
        };

        if (settings.layout == DirectedWaveFormationLayout.TransformPoints)
            return localPosition;

        return ToWorld(localPosition, settings);
    }

    private static Vector3 GetHorizontalLinePosition(
        int index,
        in DirectedWaveFormationSettings settings)
    {
        float offset = (settings.enemyCount - 1) * settings.spacing.x * 0.5f;
        return settings.center
            + new Vector3(index * settings.spacing.x - offset, 0f, 0f);
    }

    private static Vector3 GetVerticalLinePosition(
        int index,
        in DirectedWaveFormationSettings settings)
    {
        float offset = (settings.enemyCount - 1) * settings.spacing.y * 0.5f;
        return settings.center
            + new Vector3(0f, offset - index * settings.spacing.y, 0f);
    }

    private static Vector3 GetGridPosition(
        int index,
        in DirectedWaveFormationSettings settings)
    {
        int safeColumns = Mathf.Max(1, settings.columns);
        int safeRows = Mathf.Max(1, settings.rows);
        int column = index % safeColumns;
        int row = Mathf.Min(index / safeColumns, safeRows - 1);
        int usedRows = Mathf.Min(
            safeRows,
            Mathf.CeilToInt(settings.enemyCount / (float)safeColumns));
        float xOffset = (safeColumns - 1) * settings.spacing.x * 0.5f;
        float yOffset = (usedRows - 1) * settings.spacing.y * 0.5f;

        return settings.center
            + new Vector3(
                column * settings.spacing.x - xOffset,
                yOffset - row * settings.spacing.y,
                0f);
    }

    private static Vector3 GetVShapePosition(
        int index,
        in DirectedWaveFormationSettings settings)
    {
        if (index == 0)
            return settings.center;

        int pairIndex = (index + 1) / 2;
        float side = index % 2 == 0 ? 1f : -1f;
        return settings.center
            + new Vector3(
                side * pairIndex * settings.spacing.x,
                -pairIndex * settings.spacing.y,
                0f);
    }

    private static Vector3 GetArcPosition(
        int index,
        in DirectedWaveFormationSettings settings)
    {
        if (settings.enemyCount <= 1)
            return settings.center + Vector3.up * settings.arcRadius;

        float halfArc = settings.arcDegrees * 0.5f;
        float angle = Mathf.Lerp(
            -halfArc,
            halfArc,
            index / (settings.enemyCount - 1f));
        float radians = (90f + angle) * Mathf.Deg2Rad;
        return settings.center
            + new Vector3(
                Mathf.Cos(radians) * settings.arcRadius,
                Mathf.Sin(radians) * settings.arcRadius,
                0f);
    }

    private static Vector3 GetCirclePosition(
        int index,
        in DirectedWaveFormationSettings settings)
    {
        int count = Mathf.Max(1, settings.enemyCount);
        if (count <= 1)
            return settings.center;

        float angle = 90f - 360f * index / count;
        float radians = angle * Mathf.Deg2Rad;
        Vector2 flattening = GetSafeShapeFlattening(settings);
        return settings.center
            + new Vector3(
                Mathf.Cos(radians) * settings.shapeRadius * flattening.x,
                Mathf.Sin(radians) * settings.shapeRadius * flattening.y,
                0f);
    }

    private static Vector3 GetPolygonPerimeterPosition(
        int index,
        Vector3[] vertices,
        in DirectedWaveFormationSettings settings)
    {
        int count = Mathf.Max(1, settings.enemyCount);
        if (count <= 1 || vertices == null || vertices.Length == 0)
            return settings.center;

        float totalLength = 0f;
        for (int i = 0; i < vertices.Length; i++)
        {
            totalLength += Vector3.Distance(
                vertices[i],
                vertices[(i + 1) % vertices.Length]);
        }

        if (totalLength <= 0.0001f)
            return vertices[0];

        float remaining = totalLength * index / count;
        for (int i = 0; i < vertices.Length; i++)
        {
            Vector3 from = vertices[i];
            Vector3 to = vertices[(i + 1) % vertices.Length];
            float edgeLength = Vector3.Distance(from, to);
            if (remaining <= edgeLength)
            {
                float time = edgeLength <= 0.0001f
                    ? 0f
                    : remaining / edgeLength;
                return Vector3.LerpUnclamped(from, to, time);
            }

            remaining -= edgeLength;
        }

        return vertices[0];
    }

    private static Vector3[] GetTriangleVertices(
        in DirectedWaveFormationSettings settings)
    {
        Vector2 flattening = GetSafeShapeFlattening(settings);
        return new[]
        {
            GetShapePoint(90f, flattening, settings),
            GetShapePoint(210f, flattening, settings),
            GetShapePoint(330f, flattening, settings)
        };
    }

    private static Vector3[] GetSquareVertices(
        in DirectedWaveFormationSettings settings)
    {
        Vector2 flattening = GetSafeShapeFlattening(settings);
        float x = settings.shapeRadius * flattening.x;
        float y = settings.shapeRadius * flattening.y;
        return new[]
        {
            settings.center + new Vector3(-x, y, 0f),
            settings.center + new Vector3(x, y, 0f),
            settings.center + new Vector3(x, -y, 0f),
            settings.center + new Vector3(-x, -y, 0f)
        };
    }

    private static Vector3[] GetDiamondVertices(
        in DirectedWaveFormationSettings settings)
    {
        Vector2 flattening = GetSafeShapeFlattening(settings);
        return new[]
        {
            settings.center + Vector3.up * settings.shapeRadius * flattening.y,
            settings.center + Vector3.right * settings.shapeRadius * flattening.x,
            settings.center + Vector3.down * settings.shapeRadius * flattening.y,
            settings.center + Vector3.left * settings.shapeRadius * flattening.x
        };
    }

    private static Vector3 GetShapePoint(
        float angleDegrees,
        Vector2 flattening,
        in DirectedWaveFormationSettings settings)
    {
        float radians = angleDegrees * Mathf.Deg2Rad;
        return settings.center
            + new Vector3(
                Mathf.Cos(radians) * settings.shapeRadius * flattening.x,
                Mathf.Sin(radians) * settings.shapeRadius * flattening.y,
                0f);
    }

    private static Vector2 GetSafeShapeFlattening(
        in DirectedWaveFormationSettings settings)
    {
        return new Vector2(
            Mathf.Max(0.01f, settings.shapeFlattening.x),
            Mathf.Max(0.01f, settings.shapeFlattening.y));
    }

    private static Vector3 GetCustomPosition(
        int index,
        in DirectedWaveFormationSettings settings)
    {
        if (settings.customPoints == null || settings.customPoints.Length == 0)
            return GetHorizontalLinePosition(index, settings);

        if (index < settings.customPoints.Length)
            return settings.customPoints[index];

        return settings.customPoints[settings.customPoints.Length - 1];
    }

    private static Vector3 GetTransformPosition(
        int index,
        in DirectedWaveFormationSettings settings)
    {
        if (settings.pointsRoot == null || settings.pointsRoot.childCount == 0)
        {
            return ToWorld(
                GetHorizontalLinePosition(index, settings),
                settings);
        }

        int safeIndex = Mathf.Clamp(index, 0, settings.pointsRoot.childCount - 1);
        return settings.pointsRoot.GetChild(safeIndex).position;
    }

    private static Vector3 ToWorld(
        Vector3 position,
        in DirectedWaveFormationSettings settings)
    {
        return settings.coordinateSpace switch
        {
            DirectedWaveCoordinateSpace.LocalToSpawnPoint
                when settings.spawnPoint != null =>
                settings.spawnPoint.TransformPoint(position),
            DirectedWaveCoordinateSpace.LocalToSubWave
                when settings.subWaveTransform != null =>
                settings.subWaveTransform.TransformPoint(position),
            _ => position
        };
    }
}
