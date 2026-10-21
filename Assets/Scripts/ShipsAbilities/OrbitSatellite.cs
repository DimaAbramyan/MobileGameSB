using UnityEngine;

[RequireComponent(typeof(CircleCollider2D), typeof(Rigidbody2D))]
public sealed class OrbitSatellite : EnemyProjectileInterceptor
{
    private EnemyProjectileDestructionPolicy projectileDestructionPolicy = EnemyProjectileDestructionPolicy.FragileOnly;
    public override EnemyProjectileDestructionPolicy ProjectileDestructionPolicy => projectileDestructionPolicy;
    public void ApplyShipMetaStats(OrbitShipMetaContract contract)
    {
        if (contract != null)
            projectileDestructionPolicy = contract.ProjectileDestructionPolicy;
    }
    [SerializeField] private SpriteRenderer visual;
    private CircleCollider2D hitbox;
    private Rigidbody2D body;
    private OrbitPassiveAbility ability;
    private EnemyProjectileCollisionRegistry registry;
    private Vector2 lastSnapshotCenter;
    private bool hasSnapshot;
    private Vector2 visualScalePerRadius;

    private void Awake()
    {
        hitbox = GetComponent<CircleCollider2D>();
        body = GetComponent<Rigidbody2D>();
    }

    public void Configure(OrbitPassiveAbility source,
        EnemyProjectileCollisionRegistry collisionRegistry, float radius,
        EnemyProjectileDestructionPolicy destructionPolicy = EnemyProjectileDestructionPolicy.FragileOnly)
    {
        ability = source;
        projectileDestructionPolicy = destructionPolicy;
        registry = collisionRegistry;
        if (hitbox == null)
            Awake();
        hitbox.offset = Vector2.zero;
        hitbox.isTrigger = true;
        body.bodyType = RigidbodyType2D.Kinematic;
        body.gravityScale = 0f;
        body.useFullKinematicContacts = true;
        if (visual != null && visual.sprite != null)
        {
            Vector3 size = visual.sprite.bounds.size;
            visualScalePerRadius = new Vector2(2f / size.x, 2f / size.y);
        }
        SetRadius(radius);
    }

    public void SetRadius(float radius)
    {
        if (hitbox.radius != radius)
            hitbox.radius = radius;
        if (visual != null && visualScalePerRadius != Vector2.zero)
        {
            Vector3 scale = new Vector3(radius * visualScalePerRadius.x,
                radius * visualScalePerRadius.y, 1f);
            if (visual.transform.localScale != scale)
                visual.transform.localScale = scale;
        }
    }

    public void SetPosition(Vector3 position)
    {
        transform.position = position;
        if (body != null)
            body.position = position;
    }

    private void OnEnable()
    {
        hasSnapshot = false;
        registry?.RegisterInterceptor(this);
    }

    private void OnDisable() => registry?.UnregisterInterceptor(this);

    public override bool TryGetCircularSweep(
        out Vector2 previousCenter, out Vector2 center, out float radius)
    {
        center = transform.TransformPoint(hitbox.offset);
        previousCenter = hasSnapshot ? lastSnapshotCenter : center;
        lastSnapshotCenter = center;
        hasSnapshot = true;
        Vector3 scale = transform.lossyScale;
        radius = hitbox.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y));
        return hitbox.isActiveAndEnabled;
    }

    public override void NotifyEnemyProjectileIntercepted()
    {
        if (isActiveAndEnabled)
            ability?.NotifyEnemyProjectileIntercepted();
    }

    private void OnTriggerEnter2D(Collider2D other) => HandleContact(other);
    private void OnTriggerStay2D(Collider2D other) => HandleContact(other);

    private void HandleContact(Collider2D other)
    {
        if (!isActiveAndEnabled)
            return;
        if (TryDestroyEnemyProjectile(other.GetComponentInParent<EnemyProjectile>()))
            return;
        ability?.TryDamageEnemy(other.GetComponentInParent<Enemy>());
    }
}
