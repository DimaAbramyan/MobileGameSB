using UnityEngine;

[DisallowMultipleComponent]
public sealed class PlayerHitboxIndicator : MonoBehaviour
{
    private const float LineWidth = 0.012f;
    private const int CircleSegments = 32;

    [SerializeField, Min(0f), Tooltip("Radius of the visual marker in ship-local units. Does not change the damage collider.")]
    private float visualRadius = 0.075f;
    [SerializeField] private Color color = new(0.1f, 1f, 0.7f, 0.95f);

    private ParentShip ship;
    private Collider2D hitbox;
    private LineRenderer visual;
    private Material material;

    public float VisualRadius => Mathf.Max(0f, visualRadius);

    public void Initialize(ParentShip owner, Collider2D collider)
    {
        ship = owner;
        hitbox = collider;
        Rebuild();
    }

    private void LateUpdate()
    {
        if (visual != null)
            visual.enabled = ship != null && ship.IsVisible && !ship.IsDestroyed
                && hitbox != null && hitbox.enabled && VisualRadius > 0f;
    }

    private void OnDisable()
    {
        if (visual != null)
            visual.enabled = false;
    }

    private void OnValidate()
    {
        visualRadius = Mathf.Max(0f, visualRadius);
        if (Application.isPlaying && visual != null)
            Rebuild();
    }

    private void OnDestroy()
    {
        if (material != null)
        {
            if (Application.isPlaying)
                Destroy(material);
            else
                DestroyImmediate(material);
        }
    }

    private void Rebuild()
    {
        if (hitbox == null || !EnsureMaterial())
            return;

        if (visual == null)
        {
            var visualObject = new GameObject("Visual Hitbox");
            visualObject.transform.SetParent(transform, false);
            visual = visualObject.AddComponent<LineRenderer>();
            visual.useWorldSpace = false;
            visual.loop = true;
            visual.positionCount = CircleSegments;
            visual.startWidth = LineWidth;
            visual.endWidth = LineWidth;
            visual.numCapVertices = 2;
            visual.numCornerVertices = 2;
            visual.sortingOrder = 1000;
        }

        visual.sharedMaterial = material;
        visual.startColor = color;
        visual.endColor = color;
        Vector3 center = transform.InverseTransformPoint(hitbox.transform.TransformPoint(hitbox.offset));
        for (int index = 0; index < CircleSegments; index++)
        {
            float angle = index * Mathf.PI * 2f / CircleSegments;
            visual.SetPosition(index, center + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * VisualRadius);
        }
        LateUpdate();
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
        material = new Material(shader) { name = "Player Visual Hitbox Material" };
        return true;
    }

    private void OnDrawGizmosSelected()
    {
        Collider2D collider = hitbox != null ? hitbox : GetComponent<Collider2D>();
        if (collider == null)
            return;

        Vector3 center = transform.InverseTransformPoint(collider.transform.TransformPoint(collider.offset));
        Matrix4x4 previousMatrix = Gizmos.matrix;
        Color previousColor = Gizmos.color;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = color;
        Vector3 previous = center + Vector3.right * VisualRadius;
        for (int index = 1; index <= CircleSegments; index++)
        {
            float angle = index * Mathf.PI * 2f / CircleSegments;
            Vector3 next = center + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * VisualRadius;
            Gizmos.DrawLine(previous, next);
            previous = next;
        }
        Gizmos.matrix = previousMatrix;
        Gizmos.color = previousColor;
    }
}
