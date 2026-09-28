using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(PolygonCollider2D))]
public sealed class ContactArea : MonoBehaviour
{
    [SerializeField] private PolygonCollider2D polygonCollider;

    public PolygonCollider2D PolygonCollider
    {
        get
        {
            EnsurePolygonCollider();
            return polygonCollider;
        }
    }

    /// <summary>
    /// Copies the one supported collider path into the ContactArea root space.
    /// The copy is made only when compiling a ProjectileData configuration.
    /// </summary>
    public bool TryGetLocalPolygon(out Vector2[] vertices)
    {
        EnsurePolygonCollider();
        vertices = null;
        if (polygonCollider == null || polygonCollider.pathCount != 1)
            return false;

        Vector2[] colliderVertices = polygonCollider.GetPath(0);
        if (colliderVertices == null || colliderVertices.Length < 3)
            return false;

        vertices = new Vector2[colliderVertices.Length];
        for (int index = 0; index < colliderVertices.Length; index++)
        {
            Vector3 colliderPoint = colliderVertices[index] + polygonCollider.offset;
            Vector3 worldPoint = polygonCollider.transform.TransformPoint(colliderPoint);
            vertices[index] = transform.InverseTransformPoint(worldPoint);
        }

        return true;
    }

    private void OnValidate()
    {
        EnsurePolygonCollider();
        if (polygonCollider != null)
            polygonCollider.isTrigger = true;
    }

    private void OnDrawGizmos()
    {
        EnsurePolygonCollider();
        if (polygonCollider == null || polygonCollider.pathCount != 1)
            return;

        Vector2[] vertices = polygonCollider.GetPath(0);
        if (vertices == null || vertices.Length < 2)
            return;

        Gizmos.color = new Color(0.2f, 0.9f, 1f, 0.9f);
        for (int index = 0; index < vertices.Length; index++)
        {
            Vector3 from = polygonCollider.transform.TransformPoint(
                vertices[index] + polygonCollider.offset);
            Vector3 to = polygonCollider.transform.TransformPoint(
                vertices[(index + 1) % vertices.Length] + polygonCollider.offset);
            Gizmos.DrawLine(from, to);
        }
    }

    private void EnsurePolygonCollider()
    {
        if (polygonCollider == null)
            polygonCollider = GetComponent<PolygonCollider2D>();
    }
}
