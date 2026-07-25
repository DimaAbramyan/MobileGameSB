using UnityEngine;

public sealed class ArkanoidPaddle : MonoBehaviour
{
    [SerializeField] private Rigidbody2D rb;
    [SerializeField] private Vector2 offsetFromShip = new Vector2(0f, -0.85f);
    [SerializeField, Min(0.01f)] private float followSpeed = 30f;

    private Transform followTarget;

    public Rigidbody2D Rb => rb;

    private void Awake()
    {
        if (rb == null)
            rb = GetComponent<Rigidbody2D>();
    }

    public void Configure(
        Transform target,
        Vector2 offset,
        float speed)
    {
        followTarget = target;
        offsetFromShip = offset;
        followSpeed = Mathf.Max(0.01f, speed);
        SnapToTarget();
    }

    private void FixedUpdate()
    {
        if (followTarget == null)
            return;

        Vector2 targetPosition = GetTargetPosition();

        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.MovePosition(Vector2.MoveTowards(
                rb.position,
                targetPosition,
                followSpeed * Time.fixedDeltaTime));
            return;
        }

        transform.position = Vector2.MoveTowards(
            transform.position,
            targetPosition,
            followSpeed * Time.fixedDeltaTime);
    }

    public void SnapToTarget()
    {
        if (followTarget == null)
            return;

        Vector2 targetPosition = GetTargetPosition();

        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.position = targetPosition;
        }
        else
        {
            transform.position = targetPosition;
        }
    }

    public float GetHalfWidth()
    {
        Collider2D paddleCollider = GetComponent<Collider2D>();
        if (paddleCollider == null)
            return Mathf.Max(0.1f, transform.lossyScale.x * 0.5f);

        return Mathf.Max(0.1f, paddleCollider.bounds.extents.x);
    }

    private Vector2 GetTargetPosition()
    {
        return (Vector2)followTarget.position + offsetFromShip;
    }
}
