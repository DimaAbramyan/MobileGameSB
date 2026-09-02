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
    private readonly HashSet<Buff> attractedBuffs = new();
    private readonly List<Buff> invalidAttractedBuffs = new();
    private CircleCollider2D magnetZone;
    private ParentShip ownerShip;
    private ContactFilter2D contactFilter;

    private void Awake()
    {
        magnetZone = GetComponent<CircleCollider2D>();
        ownerShip = GetComponent<ParentShip>();
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
            TryPullBuff(hits[i]);

        PullAttractedBuffs();
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
        TryPullBuff(collision);
    }

    private void TryPullBuff(Collider2D collision)
    {
        if (collision == null)
            return;

        Buff buff = collision.GetComponentInParent<Buff>();
        if (buff == null)
            return;

        if (buff is MetalPickup metalPickup)
            metalPickup.StartMagneticAttraction();

        attractedBuffs.Add(buff);
        PullBuff(buff, collision.attachedRigidbody);
    }

    private void PullAttractedBuffs()
    {
        invalidAttractedBuffs.Clear();

        foreach (Buff buff in attractedBuffs)
        {
            if (buff == null || !buff.isActiveAndEnabled)
            {
                invalidAttractedBuffs.Add(buff);
                continue;
            }

            PullBuff(buff, null);
        }

        foreach (Buff buff in invalidAttractedBuffs)
            attractedBuffs.Remove(buff);
    }

    private void PullBuff(Buff buff, Rigidbody2D fallbackBody)
    {
        if (TryCollectMetal(buff))
            return;

        Vector2 targetPosition = GetMagnetCenter();
        Rigidbody2D targetBody =
            buff.GetComponent<Rigidbody2D>()
            ?? fallbackBody;

        if (targetBody != null)
        {
            MoveBodyToTarget(targetBody, targetPosition);
            TryCollectMetal(buff);
            return;
        }

        MoveTransformToTarget(buff.transform, targetPosition);
        TryCollectMetal(buff);
    }

    private void OnDisable()
    {
        attractedBuffs.Clear();
        invalidAttractedBuffs.Clear();
    }

    public Vector2 GetMagnetCenter()
    {
        return transform.TransformPoint(targetOffset);
    }

    public float MagnetRadius => Mathf.Max(0f, magnetRadius);
    public float AttractionRadius => Mathf.Max(MagnetRadius, attractionRadius);

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

    private bool TryCollectMetal(Buff buff)
    {
        if (ownerShip == null || buff is not MetalPickup metalPickup)
            return false;

        if (Vector2.Distance(buff.transform.position, GetMagnetCenter())
            > MagnetRadius)
        {
            return false;
        }

        return metalPickup.TryCollect(ownerShip);
    }

    private void MoveBodyToTarget(
        Rigidbody2D targetBody,
        Vector2 targetPosition)
    {
        Vector2 currentPosition = targetBody.position;
        float distance = Vector2.Distance(currentPosition, targetPosition);

        if (distance <= pickupSnapDistance)
        {
            targetBody.linearVelocity = Vector2.zero;
            targetBody.transform.position = new Vector3(
                targetPosition.x,
                targetPosition.y,
                targetBody.transform.position.z);
            return;
        }

        targetBody.linearVelocity = Vector2.zero;
        Vector2 nextPosition = Vector2.MoveTowards(
            currentPosition,
            targetPosition,
            forceAmount * Time.fixedDeltaTime);
        targetBody.transform.position = new Vector3(
            nextPosition.x,
            nextPosition.y,
            targetBody.transform.position.z);
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
