using UnityEngine;

public class Magnite : MonoBehaviour
{
    [SerializeField] private Vector2 targetOffset = new Vector2(0f, 0.3f);
    [SerializeField, Min(0f)] private float pickupSnapDistance = 0.08f;
    [SerializeField] private LayerMask affectedLayers = ~0;
    public float forceAmount = 10f;

    private readonly Collider2D[] hits = new Collider2D[32];
    private CircleCollider2D magnetZone;
    private ContactFilter2D contactFilter;

    private void Awake()
    {
        magnetZone = GetComponent<CircleCollider2D>();
        ConfigureContactFilter();
    }

    private void FixedUpdate()
    {
        if (magnetZone == null || !magnetZone.enabled)
            return;

        Vector2 center = GetMagnetCenter();
        float radius = GetMagnetRadius();
        int count = Physics2D.OverlapCircle(
            center,
            radius,
            contactFilter,
            hits);

        for (int i = 0; i < count; i++)
            TryPullBuff(hits[i]);
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

        Vector2 targetPosition = (Vector2)transform.position + targetOffset;
        Rigidbody2D targetBody =
            buff.GetComponent<Rigidbody2D>()
            ?? collision.attachedRigidbody;

        if (targetBody != null)
        {
            MoveBodyToTarget(targetBody, targetPosition);
            return;
        }

        MoveTransformToTarget(buff.transform, targetPosition);
    }

    private Vector2 GetMagnetCenter()
    {
        return transform.TransformPoint(magnetZone.offset);
    }

    private float GetMagnetRadius()
    {
        float maxScale = Mathf.Max(
            Mathf.Abs(transform.lossyScale.x),
            Mathf.Abs(transform.lossyScale.y));

        return magnetZone.radius * maxScale;
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
