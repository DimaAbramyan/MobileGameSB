using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class PlayerHitboxIndicator : MonoBehaviour
{
    private const float LineWidth = 0.012f;
    private const int CircleSegments = 24;

    [SerializeField] private Color color = new(0.1f, 1f, 0.7f, 0.95f);

    private readonly List<LineRenderer> lines = new();
    private ParentShip ship;
    private Collider2D hitbox;
    private Material material;

    public void Initialize(ParentShip owner, Collider2D collider)
    {
        ship = owner;
        hitbox = collider;
        Rebuild();
    }

    private void LateUpdate()
    {
        bool visible = ship != null && ship.IsVisible
            && hitbox != null && hitbox.enabled;
        for (int index = 0; index < lines.Count; index++)
            if (lines[index] != null)
                lines[index].enabled = visible;
    }

    private void OnDestroy()
    {
        if (material != null)
            Destroy(material);
    }

    private void Rebuild()
    {
        ClearLines();
        if (hitbox == null || !EnsureMaterial())
            return;

        switch (hitbox)
        {
            case BoxCollider2D box:
                DrawBox(box);
                break;
            case CircleCollider2D circle:
                DrawCircle(circle);
                break;
            case PolygonCollider2D polygon:
                DrawPolygon(polygon);
                break;
            case EdgeCollider2D edge:
                DrawPath(edge.points, false);
                break;
            default:
                DrawBounds();
                break;
        }
    }

    private bool EnsureMaterial()
    {
        if (material != null)
            return true;

        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null)
        {
            Debug.LogError("Sprites/Default shader is unavailable for the player hitbox indicator.", this);
            return false;
        }

        material = new Material(shader)
        {
            name = "Player Hitbox Indicator Material"
        };
        material.color = color;
        return true;
    }

    private void DrawBox(BoxCollider2D box)
    {
        Vector2 halfSize = box.size * 0.5f;
        Vector2 center = box.offset;
        DrawPath(new[]
        {
            center + new Vector2(-halfSize.x, -halfSize.y),
            center + new Vector2(-halfSize.x, halfSize.y),
            center + new Vector2(halfSize.x, halfSize.y),
            center + new Vector2(halfSize.x, -halfSize.y)
        }, true);
    }

    private void DrawCircle(CircleCollider2D circle)
    {
        var points = new Vector2[CircleSegments];
        for (int index = 0; index < points.Length; index++)
        {
            float angle = index * Mathf.PI * 2f / points.Length;
            points[index] = circle.offset + new Vector2(
                Mathf.Cos(angle),
                Mathf.Sin(angle)) * circle.radius;
        }

        DrawPath(points, true);
    }

    private void DrawPolygon(PolygonCollider2D polygon)
    {
        for (int index = 0; index < polygon.pathCount; index++)
            DrawPath(polygon.GetPath(index), true);
    }

    private void DrawBounds()
    {
        Bounds bounds = hitbox.bounds;
        Vector3[] worldPoints =
        {
            new(bounds.min.x, bounds.min.y, 0f),
            new(bounds.min.x, bounds.max.y, 0f),
            new(bounds.max.x, bounds.max.y, 0f),
            new(bounds.max.x, bounds.min.y, 0f)
        };

        var localPoints = new Vector2[worldPoints.Length];
        for (int index = 0; index < worldPoints.Length; index++)
            localPoints[index] = transform.InverseTransformPoint(worldPoints[index]);
        DrawPath(localPoints, true);
    }

    private void DrawPath(Vector2[] points, bool closed)
    {
        if (points == null || points.Length < 2)
            return;

        var lineObject = new GameObject("Hitbox Outline");
        lineObject.transform.SetParent(transform, false);
        LineRenderer line = lineObject.AddComponent<LineRenderer>();
        line.sharedMaterial = material;
        line.useWorldSpace = false;
        line.loop = closed;
        line.positionCount = points.Length;
        line.startWidth = LineWidth;
        line.endWidth = LineWidth;
        line.numCapVertices = 2;
        line.numCornerVertices = 2;
        line.sortingOrder = 1000;
        line.startColor = color;
        line.endColor = color;

        for (int index = 0; index < points.Length; index++)
            line.SetPosition(index, points[index]);

        lines.Add(line);
    }

    private void ClearLines()
    {
        for (int index = 0; index < lines.Count; index++)
            if (lines[index] != null)
                Destroy(lines[index].gameObject);
        lines.Clear();
    }
}
