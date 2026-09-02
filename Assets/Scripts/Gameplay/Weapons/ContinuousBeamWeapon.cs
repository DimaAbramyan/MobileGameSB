using UnityEngine;

public abstract class ContinuousBeamWeapon : Weapon
{
    private const int InitialRaycastBufferSize = 16;
    private const int MaximumRaycastBufferSize = 128;

    [Header("Beam Visual")]
    [SerializeField] private LineRenderer beamRenderer;
    [SerializeField] private Material beamMaterial;
    [SerializeField] private Color beamColor = new(1f, 0.25f, 0.05f, 0.9f);
    [SerializeField, Min(0.01f)] private float beamWidth = 0.16f;
    [SerializeField] private int beamSortingOrder = 4;

    [Header("Beam Collision")]
    [SerializeField, Tooltip(
        "The projectile layer whose Physics 2D collision rules this beam follows.")]
    private LayerMask projectileCollisionLayer;

    private RaycastHit2D[] raycastHits =
        new RaycastHit2D[InitialRaycastBufferSize];
    private Enemy beamTarget;
    private int projectileCollisionLayerIndex = -1;

    protected override void Awake()
    {
        base.Awake();
        projectileCollisionLayerIndex = GetLayerIndex(projectileCollisionLayer);
        EnsureBeamRenderer();
    }

    protected virtual void Update()
    {
        if (!IsAbleToShoot)
        {
            SetBeamVisible(false);
            return;
        }

        if (Time.timeScale <= 0f)
            return;

        if (!TryGetBeamBlockingLayers(out LayerMask blockingLayers)
            || GetBeamTransform() == null)
        {
            ClearBeam();
            return;
        }

        RefreshBeam(blockingLayers);
    }

    protected virtual void OnDisable()
    {
        SetBeamVisible(false);
    }

    protected override bool Fire()
    {
        if (Time.timeScale <= 0f)
            return false;

        if (!TryGetBeamBlockingLayers(out LayerMask blockingLayers)
            || GetBeamTransform() == null)
        {
            ClearBeam();
            return false;
        }

        RefreshBeam(blockingLayers);
        return beamTarget == null || ApplyBeamEffect(beamTarget);
    }

    protected abstract bool TryGetBeamBlockingLayers(
        out LayerMask blockingLayers);

    protected abstract bool ApplyBeamEffect(Enemy enemy);

    protected virtual Transform GetBeamTransform()
    {
        return projectileSpawn;
    }

    private void RefreshBeam(LayerMask blockingLayers)
    {
        Transform beamTransform = GetBeamTransform();
        if (beamTransform == null)
        {
            ClearBeam();
            return;
        }

        Vector3 origin = beamTransform.position;
        Vector2 direction = beamTransform.up;
        float range = Mathf.Max(0f, CurrentStats.Range);
        if (range <= 0f || direction.sqrMagnitude <= Mathf.Epsilon)
        {
            ClearBeam();
            return;
        }

        ContactFilter2D filter = new ContactFilter2D
        {
            useLayerMask = true,
            layerMask = blockingLayers,
            useTriggers = true
        };
        int hitCount = FindBeamHits(
            origin,
            direction.normalized,
            filter,
            range);

        float beamLength = range;
        Enemy hitEnemy = null;
        for (int index = 0; index < hitCount; index++)
        {
            RaycastHit2D hit = raycastHits[index];
            Collider2D collider = hit.collider;
            if (collider == null
                || IsOwnerCollider(collider)
                || collider.GetComponentInParent<Buff>() != null
                || IsIgnoredByProjectilePhysics(collider))
                continue;

            if (hit.distance < 0f || hit.distance >= beamLength)
                continue;

            beamLength = hit.distance;
            Enemy enemy = collider.GetComponentInParent<Enemy>();
            hitEnemy = enemy != null && !enemy.isDead ? enemy : null;
        }

        beamTarget = hitEnemy;
        target = hitEnemy;
        UpdateBeamVisual(origin, origin + (Vector3)direction.normalized * beamLength);
    }

    private bool IsOwnerCollider(Collider2D collider)
    {
        if (collider.transform.IsChildOf(transform))
            return true;

        return Owner != null && collider.transform.IsChildOf(Owner.transform);
    }

    private bool IsIgnoredByProjectilePhysics(Collider2D collider)
    {
        return projectileCollisionLayerIndex >= 0
            && Physics2D.GetIgnoreLayerCollision(
                projectileCollisionLayerIndex,
                collider.gameObject.layer);
    }

    private static int GetLayerIndex(LayerMask layerMask)
    {
        int mask = layerMask.value;
        for (int layer = 0; layer < 32; layer++)
        {
            if ((mask & (1 << layer)) != 0)
                return layer;
        }

        return -1;
    }

    private int FindBeamHits(
        Vector3 origin,
        Vector2 direction,
        ContactFilter2D filter,
        float range)
    {
        int hitCount = Physics2D.Raycast(
            origin,
            direction,
            filter,
            raycastHits,
            range);

        while (hitCount == raycastHits.Length
               && raycastHits.Length < MaximumRaycastBufferSize)
        {
            System.Array.Resize(ref raycastHits, raycastHits.Length * 2);
            hitCount = Physics2D.Raycast(
                origin,
                direction,
                filter,
                raycastHits,
                range);
        }

        return hitCount;
    }

    private void EnsureBeamRenderer()
    {
        if (beamRenderer == null)
            beamRenderer = GetComponentInChildren<LineRenderer>(true);

        if (beamRenderer == null)
        {
            GameObject beamObject = new GameObject("Continuous Beam");
            beamObject.transform.SetParent(transform, false);
            beamRenderer = beamObject.AddComponent<LineRenderer>();
        }

        beamRenderer.useWorldSpace = true;
        beamRenderer.positionCount = 2;
        beamRenderer.startWidth = beamWidth;
        beamRenderer.endWidth = beamWidth;
        beamRenderer.startColor = beamColor;
        beamRenderer.endColor = beamColor;
        beamRenderer.numCapVertices = 2;
        beamRenderer.sortingOrder = beamSortingOrder;

        if (beamMaterial != null)
        {
            beamRenderer.sharedMaterial = beamMaterial;
        }
        else if (TryGetComponent(out SpriteRenderer spriteRenderer))
        {
            beamRenderer.sharedMaterial = spriteRenderer.sharedMaterial;
        }

        beamRenderer.enabled = false;
    }

    private void UpdateBeamVisual(Vector3 origin, Vector3 end)
    {
        if (beamRenderer == null)
            return;

        beamRenderer.startWidth = beamWidth;
        beamRenderer.endWidth = beamWidth;
        beamRenderer.SetPosition(0, origin);
        beamRenderer.SetPosition(1, end);
        beamRenderer.enabled = true;
    }

    private void ClearBeam()
    {
        beamTarget = null;
        target = null;
        SetBeamVisible(false);
    }

    private void SetBeamVisible(bool isVisible)
    {
        if (beamRenderer != null)
            beamRenderer.enabled = isVisible;
    }
}
