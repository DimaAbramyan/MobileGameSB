using UnityEngine;

public enum ProjectileDestroyBoundaryEdge
{
    Auto,
    Left,
    Right,
    Bottom,
    Top
}

public interface IProjectileDestroyBoundary
{
}

public class Destroy : MonoBehaviour, IProjectileDestroyBoundary
{
    [Header("Camera Bounds")]
    [SerializeField] private bool keepOutsideCamera = true;
    [SerializeField] private ProjectileDestroyBoundaryEdge edge = ProjectileDestroyBoundaryEdge.Auto;
    [SerializeField, Min(0f)] private float outsidePadding = 0.75f;
    [SerializeField] private bool resizeToCameraSpan = true;
    [SerializeField] private Camera targetCamera;

    private Collider2D boundaryCollider;
    private int lastScreenWidth;
    private int lastScreenHeight;
    private float lastCameraAspect;
    private float lastCameraSize;
    private Vector3 lastCameraPosition;

    private void Awake()
    {
        boundaryCollider = GetComponent<Collider2D>();
        ApplyCameraBounds();
    }

    private void OnEnable()
    {
        ApplyCameraBounds();
    }

    private void LateUpdate()
    {
        if (!keepOutsideCamera)
            return;

        Camera cameraToUse = ResolveCamera();
        if (cameraToUse == null)
            return;

        if (Screen.width == lastScreenWidth
            && Screen.height == lastScreenHeight
            && Mathf.Approximately(cameraToUse.aspect, lastCameraAspect)
            && Mathf.Approximately(cameraToUse.orthographicSize, lastCameraSize)
            && cameraToUse.transform.position == lastCameraPosition)
        {
            return;
        }

        ApplyCameraBounds(cameraToUse);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision != null
            && collision.gameObject.TryGetComponent(out Projectile projectile))
            projectile.ReturnToPool();
    }

    private void ApplyCameraBounds()
    {
        if (!keepOutsideCamera)
            return;

        Camera cameraToUse = ResolveCamera();
        if (cameraToUse != null)
            ApplyCameraBounds(cameraToUse);
    }

    private void ApplyCameraBounds(Camera cameraToUse)
    {
        if (!cameraToUse.orthographic)
            return;

        ProjectileDestroyBoundaryEdge resolvedEdge = ResolveEdge(cameraToUse);
        if (resolvedEdge == ProjectileDestroyBoundaryEdge.Auto)
            return;

        if (boundaryCollider == null)
            boundaryCollider = GetComponent<Collider2D>();

        Vector3 min = cameraToUse.ViewportToWorldPoint(Vector3.zero);
        Vector3 max = cameraToUse.ViewportToWorldPoint(Vector3.one);
        Vector3 center = (min + max) * 0.5f;
        Vector3 position = transform.position;

        Bounds colliderBounds = boundaryCollider != null
            ? boundaryCollider.bounds
            : new Bounds(position, Vector3.one);

        float halfWidth = Mathf.Max(0.01f, colliderBounds.extents.x);
        float halfHeight = Mathf.Max(0.01f, colliderBounds.extents.y);

        switch (resolvedEdge)
        {
            case ProjectileDestroyBoundaryEdge.Left:
                position.x = min.x - outsidePadding - halfWidth;
                position.y = center.y;
                ResizeBoxCollider(max.y - min.y + outsidePadding * 2f, false);
                break;
            case ProjectileDestroyBoundaryEdge.Right:
                position.x = max.x + outsidePadding + halfWidth;
                position.y = center.y;
                ResizeBoxCollider(max.y - min.y + outsidePadding * 2f, false);
                break;
            case ProjectileDestroyBoundaryEdge.Bottom:
                position.x = center.x;
                position.y = min.y - outsidePadding - halfHeight;
                ResizeBoxCollider(max.x - min.x + outsidePadding * 2f, true);
                break;
            case ProjectileDestroyBoundaryEdge.Top:
                position.x = center.x;
                position.y = max.y + outsidePadding + halfHeight;
                ResizeBoxCollider(max.x - min.x + outsidePadding * 2f, true);
                break;
        }

        transform.position = position;

        lastScreenWidth = Screen.width;
        lastScreenHeight = Screen.height;
        lastCameraAspect = cameraToUse.aspect;
        lastCameraSize = cameraToUse.orthographicSize;
        lastCameraPosition = cameraToUse.transform.position;
    }

    private void ResizeBoxCollider(float cameraSpan, bool horizontal)
    {
        if (!resizeToCameraSpan || boundaryCollider is not BoxCollider2D boxCollider)
            return;

        Vector2 size = boxCollider.size;
        Vector3 scale = transform.lossyScale;

        if (horizontal)
            size.x = cameraSpan / Mathf.Max(0.01f, Mathf.Abs(scale.x));
        else
            size.y = cameraSpan / Mathf.Max(0.01f, Mathf.Abs(scale.y));

        boxCollider.size = size;
    }

    private Camera ResolveCamera()
    {
        if (targetCamera != null)
            return targetCamera;

        targetCamera = Camera.main;
        return targetCamera;
    }

    private ProjectileDestroyBoundaryEdge ResolveEdge(Camera cameraToUse)
    {
        if (edge != ProjectileDestroyBoundaryEdge.Auto)
            return edge;

        string lowerName = name.ToLowerInvariant();
        if (lowerName.Contains("left"))
            return ProjectileDestroyBoundaryEdge.Left;
        if (lowerName.Contains("right"))
            return ProjectileDestroyBoundaryEdge.Right;
        if (lowerName.Contains("bottom") || lowerName.Contains("down"))
            return ProjectileDestroyBoundaryEdge.Bottom;
        if (lowerName.Contains("top") || lowerName.Contains("up"))
            return ProjectileDestroyBoundaryEdge.Top;

        Vector3 min = cameraToUse.ViewportToWorldPoint(Vector3.zero);
        Vector3 max = cameraToUse.ViewportToWorldPoint(Vector3.one);
        Vector3 position = transform.position;

        float leftDistance = Mathf.Abs(position.x - min.x);
        float rightDistance = Mathf.Abs(position.x - max.x);
        float bottomDistance = Mathf.Abs(position.y - min.y);
        float topDistance = Mathf.Abs(position.y - max.y);
        float minDistance = Mathf.Min(leftDistance, rightDistance, bottomDistance, topDistance);

        if (Mathf.Approximately(minDistance, leftDistance))
            return ProjectileDestroyBoundaryEdge.Left;
        if (Mathf.Approximately(minDistance, rightDistance))
            return ProjectileDestroyBoundaryEdge.Right;
        if (Mathf.Approximately(minDistance, bottomDistance))
            return ProjectileDestroyBoundaryEdge.Bottom;

        return ProjectileDestroyBoundaryEdge.Top;
    }
}
