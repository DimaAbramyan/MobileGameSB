using System.Collections.Generic;
using UnityEngine;

public class Magnite : MonoBehaviour
{
    [SerializeField] private Vector2 targetOffset = new Vector2(0f, 0.3f);
    [SerializeField, Min(0f)] private float pickupSnapDistance = 0.08f;
    [SerializeField] private LayerMask affectedLayers = ~0;
    [SerializeField, Min(0f), InspectorName("Absorption Radius"), Tooltip(
        "Radius in world units in which metal is absorbed and collected.")]
    private float magnetRadius = 1f;
    [SerializeField, Min(0f), InspectorName("Attraction Radius"), Tooltip(
        "Radius in world units in which pickups begin moving toward the magnet. Must be at least Absorption Radius.")]
    private float attractionRadius = 3f;
    public float forceAmount = 10f;

    private readonly Collider2D[] hits = new Collider2D[32];
    private readonly HashSet<CollectiblePickup> attractedPickups = new();
    private readonly List<CollectiblePickup> invalidAttractedPickups = new();
    private CircleCollider2D magnetZone;
    private ParentShip ownerShip;
    private ContactFilter2D contactFilter;
    private float temporaryRadiusMultiplier = 1f;
    private float temporaryRadiusMultiplierUntil;
    private bool wasTemporaryRadiusMultiplierActive;

    private void Awake()
    {
        magnetZone = GetComponent<CircleCollider2D>();
        ownerShip = GetComponentInParent<ParentShip>();
        SyncMagnetZoneRadius();
        ConfigureContactFilter();
    }

    private void FixedUpdate()
    {
        if (ownerShip != null && !ownerShip.IsVisible)
            return;

        if (magnetZone != null && !magnetZone.enabled)
            return;

        Vector2 center = GetMagnetCenter();
        float radius = AttractionRadius;
        int count = Physics2D.OverlapCircle(
            center,
            radius,
            contactFilter,
            hits);

        for (int i = 0; i < count; i++)
            TryPullPickup(hits[i]);

        PullAttractedPickups();
    }

    private void Update()
    {
        bool isTemporaryRadiusMultiplierActive =
            HasTemporaryRadiusMultiplier;
        if (isTemporaryRadiusMultiplierActive
            == wasTemporaryRadiusMultiplierActive)
        {
            return;
        }

        wasTemporaryRadiusMultiplierActive =
            isTemporaryRadiusMultiplierActive;
        SyncMagnetZoneRadius();
    }

    private void ConfigureContactFilter()
    {
        contactFilter = new ContactFilter2D
        {
            useLayerMask = true,
            layerMask = affectedLayers,
            useTriggers = true
        };
    }

    private void OnValidate()
    {
        pickupSnapDistance = Mathf.Max(0f, pickupSnapDistance);
        magnetRadius = Mathf.Max(0f, magnetRadius);
        attractionRadius = Mathf.Max(magnetRadius, attractionRadius);
        forceAmount = Mathf.Max(0f, forceAmount);
        magnetZone = GetComponent<CircleCollider2D>();
        SyncMagnetZoneRadius();
        ConfigureContactFilter();
    }

    private void OnTriggerStay2D(Collider2D collision)
    {
        TryPullPickup(collision);
    }

    private void TryPullPickup(Collider2D collision)
    {
        if (collision == null)
            return;

        CollectiblePickup pickup =
            collision.GetComponentInParent<CollectiblePickup>();
        if (pickup == null)
            return;

        pickup.StartMagneticAttraction();
        attractedPickups.Add(pickup);
        PullPickup(pickup, collision.attachedRigidbody);
    }

    private void PullAttractedPickups()
    {
        invalidAttractedPickups.Clear();

        foreach (CollectiblePickup pickup in attractedPickups)
        {
            if (pickup == null || !pickup.isActiveAndEnabled)
            {
                invalidAttractedPickups.Add(pickup);
                continue;
            }

            PullPickup(pickup, null);
        }

        foreach (CollectiblePickup pickup in invalidAttractedPickups)
            attractedPickups.Remove(pickup);
    }

    private void PullPickup(
        CollectiblePickup pickup,
        Rigidbody2D fallbackBody)
    {
        if (TryCollectPickup(pickup))
            return;

        Vector2 targetPosition = GetMagnetCenter();
        Rigidbody2D targetBody =
            pickup.GetComponent<Rigidbody2D>()
            ?? fallbackBody;

        if (targetBody != null)
        {
            MoveBodyToTarget(targetBody, targetPosition);
            TryCollectPickup(pickup);
            return;
        }

        MoveTransformToTarget(pickup.transform, targetPosition);
        TryCollectPickup(pickup);
    }

    private void OnDisable()
    {
        attractedPickups.Clear();
        invalidAttractedPickups.Clear();
    }

    public Vector2 GetMagnetCenter()
    {
        return transform.TransformPoint(targetOffset);
    }

    public float MagnetRadius => Mathf.Max(0f, magnetRadius)
        * CurrentRadiusMultiplier;
    public float AttractionRadius => Mathf.Max(MagnetRadius,
        Mathf.Max(0f, attractionRadius) * CurrentRadiusMultiplier);

    private bool HasTemporaryRadiusMultiplier =>
        Time.time < temporaryRadiusMultiplierUntil;

    private float CurrentRadiusMultiplier => HasTemporaryRadiusMultiplier
        ? temporaryRadiusMultiplier
        : 1f;

    public void ActivateRadiusMultiplier(float multiplier, float duration)
    {
        if (multiplier <= 0f || duration <= 0f)
            return;

        temporaryRadiusMultiplier = HasTemporaryRadiusMultiplier
            ? Mathf.Max(temporaryRadiusMultiplier, multiplier)
            : multiplier;
        temporaryRadiusMultiplierUntil = Mathf.Max(
            temporaryRadiusMultiplierUntil,
            Time.time + duration);
        wasTemporaryRadiusMultiplierActive = true;
        SyncMagnetZoneRadius();
    }

    private float GetMagnetRadius()
    {
        return MagnetRadius;
    }

    private void SyncMagnetZoneRadius()
    {
        if (magnetZone == null)
            return;

        magnetZone.offset = targetOffset;
        magnetZone.radius = MagnetRadius;
    }

    private bool TryCollectPickup(CollectiblePickup pickup)
    {
        if (ownerShip == null || pickup == null)
            return false;

        if (Vector2.Distance(pickup.transform.position, GetMagnetCenter())
            > MagnetRadius)
        {
            return false;
        }

        return pickup.TryCollect(ownerShip);
    }

    private void MoveBodyToTarget(
        Rigidbody2D targetBody,
        Vector2 targetPosition)
    {
        Vector2 currentPosition = targetBody.transform.position;
        float distance = Vector2.Distance(currentPosition, targetPosition);

        if (distance <= pickupSnapDistance)
        {
            targetBody.linearVelocity = Vector2.zero;
            targetBody.position = targetPosition;
            return;
        }

        targetBody.linearVelocity = Vector2.zero;
        Vector2 nextPosition = Vector2.MoveTowards(
            currentPosition,
            targetPosition,
            forceAmount * Time.fixedDeltaTime);
        targetBody.position = nextPosition;
    }

    private void MoveTransformToTarget(
        Transform targetTransform,
        Vector2 targetPosition)
    {
        Vector3 currentPosition = targetTransform.position;
        Vector3 nextPosition = Vector2.MoveTowards(
            currentPosition,
            targetPosition,
            forceAmount * Time.fixedDeltaTime);

        nextPosition.z = currentPosition.z;
        targetTransform.position = nextPosition;
    }
}
