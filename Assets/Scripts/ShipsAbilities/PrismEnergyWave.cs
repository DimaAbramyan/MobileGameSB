using System;
using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(-550)]
[RequireComponent(typeof(BoxCollider2D), typeof(Rigidbody2D))]
public sealed class PrismEnergyWave : EnemyProjectileInterceptor
{
    public static float GetDamage(PrismShipMetaContract settings, float multiplier) => settings.Damage * multiplier;
    private readonly HashSet<Enemy> damagedEnemies = new();
    private Collider2D[] overlaps = new Collider2D[32];
    private RaycastHit2D[] castHits = new RaycastHit2D[32];
    private readonly ContactFilter2D contactFilter = new() { useTriggers = true };
    private ParentShip owner;
    private DealDamageManager damageManager;
    private EnemyProjectileCollisionRegistry projectileRegistry;
    private PrismShipMetaContract settings;
    private BoxCollider2D hitbox;
    private Rigidbody2D body;
    private Vector2 direction;
    private Vector2 previousCenter;
    private float damage;
    private bool initialized;

    public float TraveledDistance { get; private set; }
    public bool IsFinished { get; private set; }
    public Vector2 Direction => direction;
    public override EnemyProjectileDestructionPolicy ProjectileDestructionPolicy => settings != null
        ? settings.ProjectileDestructionPolicy : EnemyProjectileDestructionPolicy.FragileOnly;

    public override bool TryDestroyEnemyProjectile(EnemyProjectile projectile)
        => initialized && settings.DestroyProjectiles && base.TryDestroyEnemyProjectile(projectile);

    public void Initialize(ParentShip source, PrismShipMetaContract config, float damageMultiplier,
        DealDamageManager manager, EnemyProjectileCollisionRegistry registry)
    {
        owner = source; settings = (PrismShipMetaContract)config.Clone();
        damageManager = manager; projectileRegistry = registry;
        direction = ((Vector2)transform.up).normalized;
        previousCenter = transform.position;
        damage = GetDamage(settings, damageMultiplier);
        hitbox = GetComponent<BoxCollider2D>();
        hitbox.isTrigger = true; hitbox.size = new Vector2(settings.WaveWidth, settings.WaveThickness);
        body = GetComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Kinematic; body.gravityScale = 0f;
        body.useFullKinematicContacts = true;
        body.constraints = RigidbodyConstraints2D.FreezeRotation;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        var visual = Instantiate(settings.WavePrefab, transform, false);
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localRotation = Quaternion.identity;
        foreach (var collider in visual.GetComponentsInChildren<Collider2D>(true)) collider.enabled = false;
        FitVisual(visual.transform);
        initialized = true;
        if (settings.DestroyProjectiles) projectileRegistry.RegisterInterceptor(this);
        CheckOverlaps(previousCenter);
    }

    private void FitVisual(Transform visual)
    {
        var renderers = visual.GetComponentsInChildren<Renderer>(true);
        bool hasBounds = false; Bounds bounds = default;
        foreach (var renderer in renderers)
        {
            Bounds local = renderer.localBounds;
            for (int corner = 0; corner < 8; corner++)
            {
                Vector3 point = local.center + Vector3.Scale(local.extents,
                    new Vector3((corner & 1) == 0 ? -1f : 1f, (corner & 2) == 0 ? -1f : 1f, (corner & 4) == 0 ? -1f : 1f));
                point = visual.InverseTransformPoint(renderer.transform.TransformPoint(point));
                if (!hasBounds) { bounds = new Bounds(point, Vector3.zero); hasBounds = true; }
                else bounds.Encapsulate(point);
            }
        }
        if (!hasBounds || bounds.size.x <= .0001f || bounds.size.y <= .0001f)
        {
            Debug.LogError("Prism WavePrefab needs a visual with non-zero local width and thickness.", this);
            return;
        }
        visual.localScale = new Vector3(settings.WaveWidth / bounds.size.x, settings.WaveThickness / bounds.size.y, 1f);
        visual.localPosition = -Vector3.Scale(bounds.center, visual.localScale);
    }

    private void FixedUpdate() => Step(Time.fixedDeltaTime);

    public void Step(float deltaTime)
    {
        if (!initialized || IsFinished || deltaTime <= 0f) return;
        previousCenter = transform.position;
        float distance = Mathf.Min(settings.WaveSpeed * deltaTime, settings.MaxDistance - TraveledDistance);
        if (distance > 0f)
        {
            SweepContacts(previousCenter, distance);
            Vector2 nextCenter = previousCenter + direction * distance;
            transform.position = new Vector3(nextCenter.x, nextCenter.y, transform.position.z);
            body.position = nextCenter;
            TraveledDistance += distance;
        }
        CheckOverlaps(transform.position);
        if (TraveledDistance >= settings.MaxDistance)
        {
            IsFinished = true;
            if (Application.isPlaying) Destroy(gameObject);
            else DestroyImmediate(gameObject);
        }
    }

    private void CheckOverlaps(Vector2 center)
    {
        int count;
        while ((count = Physics2D.OverlapBox(center, hitbox.size, transform.eulerAngles.z, contactFilter, overlaps)) == overlaps.Length)
            Array.Resize(ref overlaps, overlaps.Length * 2);
        for (int i = 0; i < count; i++) HandleContact(overlaps[i]);
    }

    private void SweepContacts(Vector2 center, float distance)
    {
        int count;
        while ((count = Physics2D.BoxCast(center, hitbox.size, transform.eulerAngles.z, direction, contactFilter, castHits, distance)) == castHits.Length)
            Array.Resize(ref castHits, castHits.Length * 2);
        for (int i = 0; i < count; i++) HandleContact(castHits[i].collider);
    }

    private void OnTriggerEnter2D(Collider2D other) => HandleContact(other);
    private void OnTriggerStay2D(Collider2D other) => HandleContact(other);

    private void HandleContact(Collider2D other)
    {
        if (!initialized || other == null) return;
        if (settings.DestroyProjectiles)
        {
            var projectile = other.GetComponentInParent<EnemyProjectile>();
            if (projectile != null) { TryDestroyEnemyProjectile(projectile); return; }
        }
        var enemy = other.GetComponentInParent<Enemy>();
        if (enemy == null || enemy.isDead || !enemy.isActiveAndEnabled || !damagedEnemies.Add(enemy)) return;
        damageManager.DealDamage(enemy, owner, damage, settings.DamageType);
    }

    public override bool TryGetBoxSweep(out Vector2 previous, out Vector2 center, out Vector2 size, out Vector2 right)
    {
        previous = previousCenter; center = transform.position;
        size = hitbox != null ? hitbox.size : Vector2.zero;
        right = transform.right;
        return initialized && isActiveAndEnabled && settings.DestroyProjectiles;
    }

    private void OnEnable()
    {
        previousCenter = transform.position;
        if (initialized && settings.DestroyProjectiles) projectileRegistry?.RegisterInterceptor(this);
    }
    private void OnDisable() => projectileRegistry?.UnregisterInterceptor(this);
    private void OnDestroy() => projectileRegistry?.UnregisterInterceptor(this);
}
