using UnityEngine;

public enum BossRadialAttackShape
{
    Circle,
    Arc,
    Spiral
}

public enum BossAttackAimMode
{
    WorldAngle,
    TowardPlayer
}

public enum BossProjectileFlightMode
{
    Straight,
    Curved,
    Homing
}

[CreateAssetMenu(
    fileName = "BossRadialAttack",
    menuName = "Boss/Attacks/Radial Attack")]
public sealed class BossRadialAttackPattern : ScriptableObject
{
    [Header("Projectile")]
    [SerializeField] private BossProjectile projectilePrefab;
    [SerializeField, Min(0f)] private float damage = 10f;
    [SerializeField, Min(0.05f)] private float lifetime = 6f;
    [SerializeField] private Vector2 projectileScale = Vector2.one;

    [Header("Shape")]
    [SerializeField] private BossRadialAttackShape shape =
        BossRadialAttackShape.Circle;
    [SerializeField] private BossAttackAimMode aimMode =
        BossAttackAimMode.WorldAngle;
    [SerializeField, Min(1)] private int projectileCount = 12;
    [SerializeField, Range(1f, 360f)] private float arcDegrees = 120f;
    [SerializeField] private float startAngleDegrees;
    [SerializeField, Min(0f)] private float spawnRadius = 0.2f;

    [Header("Volleys")]
    [SerializeField, Min(1)] private int volleyCount = 1;
    [SerializeField, Min(0f)] private float delayBetweenVolleys = 0.12f;
    [SerializeField] private float rotationPerVolleyDegrees = 10f;

    [Header("Flight")]
    [SerializeField] private BossProjectileFlightMode flightMode =
        BossProjectileFlightMode.Straight;
    [SerializeField, Min(0f)] private float initialSpeed = 4f;
    [SerializeField] private float acceleration;
    [SerializeField] private AnimationCurve speedOverLifetime =
        AnimationCurve.Linear(0f, 1f, 1f, 1f);
    [SerializeField] private float angularVelocityDegrees;
    [SerializeField, Min(0f)] private float homingTurnSpeedDegrees = 180f;

    public BossProjectile ProjectilePrefab => projectilePrefab;
    public BossRadialAttackShape Shape => shape;
    public BossAttackAimMode AimMode => aimMode;
    public BossProjectileFlightMode FlightMode => flightMode;
    public int ProjectileCount => projectileCount;
    public int VolleyCount => volleyCount;
    public float DelayBetweenVolleys => delayBetweenVolleys;
    public float SpawnRadius => spawnRadius;
    public float InitialSpeed => initialSpeed;
    public float Acceleration => acceleration;
    public float AngularVelocityDegrees => angularVelocityDegrees;
    public float HomingTurnSpeedDegrees => homingTurnSpeedDegrees;
    public float Lifetime => lifetime;
    public float Damage => damage;
    public Vector2 ProjectileScale => projectileScale;
    public AnimationCurve SpeedOverLifetime => speedOverLifetime;

    public float GetAimAngleDegrees(Vector2 origin, Vector2 target)
    {
        if (aimMode == BossAttackAimMode.WorldAngle)
            return startAngleDegrees;

        Vector2 direction = target - origin;
        if (direction.sqrMagnitude <= Mathf.Epsilon)
            return startAngleDegrees;

        return Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg
            + startAngleDegrees;
    }

    public float GetProjectileAngleDegrees(
        int projectileIndex,
        int volleyIndex,
        float aimAngleDegrees)
    {
        int safeCount = Mathf.Max(1, projectileCount);
        int safeIndex = Mathf.Clamp(projectileIndex, 0, safeCount - 1);
        float volleyRotation = shape == BossRadialAttackShape.Spiral
            ? volleyIndex * rotationPerVolleyDegrees
            : 0f;

        if (shape == BossRadialAttackShape.Arc)
        {
            if (safeCount == 1)
                return aimAngleDegrees + volleyRotation;

            float step = arcDegrees / (safeCount - 1);
            return aimAngleDegrees - arcDegrees * 0.5f
                + safeIndex * step
                + volleyRotation;
        }

        float circleStep = 360f / safeCount;
        return aimAngleDegrees + safeIndex * circleStep + volleyRotation;
    }

    private void OnValidate()
    {
        projectileCount = Mathf.Max(1, projectileCount);
        volleyCount = Mathf.Max(1, volleyCount);
        arcDegrees = Mathf.Clamp(arcDegrees, 1f, 360f);
        spawnRadius = Mathf.Max(0f, spawnRadius);
        initialSpeed = Mathf.Max(0f, initialSpeed);
        homingTurnSpeedDegrees = Mathf.Max(0f, homingTurnSpeedDegrees);
        lifetime = Mathf.Max(0.05f, lifetime);
        damage = Mathf.Max(0f, damage);

        if (speedOverLifetime == null || speedOverLifetime.length == 0)
            speedOverLifetime = AnimationCurve.Linear(0f, 1f, 1f, 1f);
    }
}
