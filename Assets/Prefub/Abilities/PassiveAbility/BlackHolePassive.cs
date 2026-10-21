using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

public class BlackHolePassive : PassiveAbility
{
    // World-space radius of BlackHoleCraft's real CircleCollider2D hitbox.
    public const float ShipHitboxRadius = 0.075f;
    private readonly List<EnemyProjectile> enemyProjectiles = new List<EnemyProjectile>();
    private readonly Dictionary<EnemyProjectile, CircleCollider2D> trackedProjectiles =
        new Dictionary<EnemyProjectile, CircleCollider2D>();
    private CircleCollider2D fieldCollider;
    private EnemyProjectileSlowField fieldSettings = new BlackHoleShipMetaContract()
        .CreateProjectileField(default, new float2(0f, 1f));

    public override void Init(ParentShip ship)
    {
        owner = ship;
        fieldCollider = GetComponent<CircleCollider2D>();
        SyncFieldCollider();
    }

    public override void On()
    {
        if (owner == null)
            owner = GetComponentInParent<ParentShip>();
        base.On();
        SyncFieldCollider();
    }

    public bool TryGetSlowField(out EnemyProjectileSlowField field)
    {
        field = default;
        if (!isActive || !isActiveAndEnabled || owner == null
            || !owner.isActiveAndEnabled || !owner.IsVisible)
        {
            return false;
        }

        Vector3 center = owner.transform.position;
        // In this 2D project local +Y is the ship's longitudinal Forward.
        Vector3 forward = owner.transform.up;
        field = fieldSettings;
        field.ShipRadius = ShipHitboxRadius;
        field.Center = new float2(center.x, center.y);
        field.Forward = math.normalizesafe(new float2(forward.x, forward.y), new float2(0f, 1f));
        return true;
    }

    public override void ApplyShipMetaStats(ShipMetaRuntimeStats stats)
    {
        if (!stats.TryGetContract(out BlackHoleShipMetaContract contract))
            return;

        fieldSettings = contract.CreateProjectileField(default, new float2(0f, 1f));
        SyncFieldCollider();
    }

    public void Awake()
    {
        fieldCollider = GetComponent<CircleCollider2D>();
    }

    private void FixedUpdate()
    {
        SyncFieldCollider();
        bool hasField = TryGetSlowField(out EnemyProjectileSlowField field);
        for (int index = enemyProjectiles.Count - 1; index >= 0; index--)
        {
            EnemyProjectile projectile = enemyProjectiles[index];
            if (projectile == null)
            {
                trackedProjectiles.Remove(projectile);
                enemyProjectiles.RemoveAt(index);
                continue;
            }

            Vector3 position = projectile.transform.position;
            projectile.SetMultiplier(hasField
                ? field.EvaluateMultiplier(new float2(position.x, position.y),
                    GetProjectileRadius(trackedProjectiles[projectile])) : 1f);
        }
    }

    private void SyncFieldCollider()
    {
        if (fieldCollider == null || owner == null)
            return;

        Vector3 scale = transform.lossyScale;
        float minimumScale = Mathf.Min(Mathf.Abs(scale.x), Mathf.Abs(scale.y));
        if (minimumScale <= 0.00001f)
            return;

        // This dedicated trigger only collects legacy projectiles. Its offset
        // and coverage must match the same owner-centred ECS field.
        Vector3 center = transform.InverseTransformPoint(owner.transform.position);
        Vector2 offset = new Vector2(center.x, center.y);
        float radius = (fieldSettings.Radius + ShipHitboxRadius) / minimumScale;
        if (fieldCollider.offset != offset)
            fieldCollider.offset = offset;
        if (!Mathf.Approximately(fieldCollider.radius, radius))
            fieldCollider.radius = radius;
    }
    public override void Off()
    {
        base.Off();
        for (int index = 0; index < enemyProjectiles.Count; index++)
        {
            EnemyProjectile projectile = enemyProjectiles[index];
            if (projectile != null)
                projectile.SetMultiplier(1f);
        }
        enemyProjectiles.Clear();
        trackedProjectiles.Clear();
    }

    private void OnDisable()
    {
        Off();
    }
    private void OnTriggerEnter2D(Collider2D collision)
    {
        TrackProjectile(collision);
    }
    private void OnTriggerStay2D(Collider2D collision)
    {
        // Stay also restores tracking after Off/On while already in the trigger.
        TrackProjectile(collision);
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        EnemyProjectile enemyProjectile = collision.GetComponent<EnemyProjectile>();
        if (enemyProjectile != null)
        {
            trackedProjectiles.Remove(enemyProjectile);
            enemyProjectiles.Remove(enemyProjectile);
        }
        if (enemyProjectile != null)
            enemyProjectile.SetMultiplier(1);
    }
    private void TrackProjectile(Collider2D collision)
    {
        if (!TryGetSlowField(out EnemyProjectileSlowField field)
            || !collision.TryGetComponent(out EnemyProjectile projectile))
            return;

        if (!trackedProjectiles.ContainsKey(projectile))
        {
            trackedProjectiles.Add(projectile, collision as CircleCollider2D);
            enemyProjectiles.Add(projectile);
        }
        Vector3 position = projectile.transform.position;
        projectile.SetMultiplier(field.EvaluateMultiplier(new float2(position.x, position.y),
            GetProjectileRadius(trackedProjectiles[projectile])));
    }

    private static float GetProjectileRadius(CircleCollider2D collider)
    {
        if (collider == null)
            return 0f;
        Vector3 scale = collider.transform.lossyScale;
        return collider.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y));
    }
}
