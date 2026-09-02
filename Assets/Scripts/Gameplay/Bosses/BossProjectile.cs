using UnityEngine;
using Zenject;

public readonly struct BossProjectileLaunchData
{
    public BossProjectileLaunchData(
        Vector2 direction,
        Transform target,
        BossProjectileFlightMode flightMode,
        float initialSpeed,
        float acceleration,
        float angularVelocityDegrees,
        float homingTurnSpeedDegrees,
        float lifetime,
        float damage,
        Vector2 scale,
        AnimationCurve speedOverLifetime)
    {
        Direction = direction;
        Target = target;
        FlightMode = flightMode;
        InitialSpeed = initialSpeed;
        Acceleration = acceleration;
        AngularVelocityDegrees = angularVelocityDegrees;
        HomingTurnSpeedDegrees = homingTurnSpeedDegrees;
        Lifetime = lifetime;
        Damage = damage;
        Scale = scale;
        SpeedOverLifetime = speedOverLifetime;
    }

    public Vector2 Direction { get; }
    public Transform Target { get; }
    public BossProjectileFlightMode FlightMode { get; }
    public float InitialSpeed { get; }
    public float Acceleration { get; }
    public float AngularVelocityDegrees { get; }
    public float HomingTurnSpeedDegrees { get; }
    public float Lifetime { get; }
    public float Damage { get; }
    public Vector2 Scale { get; }
    public AnimationCurve SpeedOverLifetime { get; }
}

public sealed class BossProjectile : MonoBehaviour
{
    [Inject] private GameSettings gameSettings;

    private BossProjectilePool pool;
    private Rigidbody2D body;
    private Vector3 defaultScale;
    private Vector2 direction;
    private Transform target;
    private BossProjectileFlightMode flightMode;
    private AnimationCurve speedOverLifetime;
    private float initialSpeed;
    private float acceleration;
    private float angularVelocityDegrees;
    private float homingTurnSpeedDegrees;
    private float lifetime;
    private float damage;
    private float elapsed;
    private bool active;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        defaultScale = transform.localScale;
    }

    public void Launch(
        BossProjectilePool owner,
        in BossProjectileLaunchData data)
    {
        pool = owner;
        direction = data.Direction.sqrMagnitude > Mathf.Epsilon
            ? data.Direction.normalized
            : Vector2.down;
        target = data.Target;
        flightMode = data.FlightMode;
        initialSpeed = Mathf.Max(0f, data.InitialSpeed);
        acceleration = data.Acceleration;
        angularVelocityDegrees = data.AngularVelocityDegrees;
        homingTurnSpeedDegrees = Mathf.Max(0f, data.HomingTurnSpeedDegrees);
        lifetime = Mathf.Max(0.05f, data.Lifetime);
        damage = Mathf.Max(0f, data.Damage);
        speedOverLifetime = data.SpeedOverLifetime;
        elapsed = 0f;
        active = true;

        transform.localScale = new Vector3(
            defaultScale.x * data.Scale.x,
            defaultScale.y * data.Scale.y,
            defaultScale.z);

        if (body != null)
        {
            body.simulated = true;
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
        }
    }

    private void FixedUpdate()
    {
        if (!active)
            return;

        float deltaTime = Time.fixedDeltaTime;
        elapsed += deltaTime;

        if (elapsed >= lifetime)
        {
            ReturnToPool();
            return;
        }

        UpdateDirection(deltaTime);

        float normalizedTime = Mathf.Clamp01(elapsed / lifetime);
        float speedMultiplier = speedOverLifetime != null
            ? speedOverLifetime.Evaluate(normalizedTime)
            : 1f;
        float speed = Mathf.Max(
            0f,
            (initialSpeed + acceleration * elapsed) * speedMultiplier);
        Vector2 nextPosition = (Vector2)transform.position
            + direction * speed * deltaTime;

        if (body != null && body.simulated)
            body.MovePosition(nextPosition);
        else
            transform.position = nextPosition;

        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, 0f, angle - 90f);
    }

    private void UpdateDirection(float deltaTime)
    {
        if (flightMode == BossProjectileFlightMode.Curved)
        {
            direction = Rotate(direction, angularVelocityDegrees * deltaTime);
            return;
        }

        if (flightMode != BossProjectileFlightMode.Homing || target == null)
            return;

        Vector2 desired = (Vector2)target.position - (Vector2)transform.position;
        if (desired.sqrMagnitude <= Mathf.Epsilon)
            return;

        float currentAngle = Mathf.Atan2(direction.y, direction.x)
            * Mathf.Rad2Deg;
        float desiredAngle = Mathf.Atan2(desired.y, desired.x)
            * Mathf.Rad2Deg;
        float angle = Mathf.MoveTowardsAngle(
            currentAngle,
            desiredAngle,
            homingTurnSpeedDegrees * deltaTime);
        direction = DirectionFromAngle(angle);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        HandleHit(collision.collider);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        HandleHit(other);
    }

    private void HandleHit(Collider2D other)
    {
        if (!active)
            return;

        ParentShip receiver = other.GetComponentInParent<ParentShip>();
        if (receiver != null && !gameSettings.IsGodModeOn)
            receiver.TakeDamage(damage);

        ReturnToPool();
    }

    public void ResetState()
    {
        active = false;
        target = null;
        elapsed = 0f;
        transform.localScale = defaultScale;

        if (body != null)
        {
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
            body.simulated = false;
        }
    }

    private void ReturnToPool()
    {
        if (!active)
            return;

        active = false;
        if (pool != null)
            pool.Release(this);
        else
            Destroy(gameObject);
    }

    private static Vector2 Rotate(Vector2 value, float angleDegrees)
    {
        float radians = angleDegrees * Mathf.Deg2Rad;
        float sin = Mathf.Sin(radians);
        float cos = Mathf.Cos(radians);
        return new Vector2(
            value.x * cos - value.y * sin,
            value.x * sin + value.y * cos);
    }

    private static Vector2 DirectionFromAngle(float angleDegrees)
    {
        float radians = angleDegrees * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
    }
}
