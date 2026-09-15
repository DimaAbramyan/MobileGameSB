using UnityEngine;

using Zenject;
public class Projectile : MonoBehaviour
{
    [Inject] protected AudioDatabase audioDatabase;
    [Inject] protected SoundManager audioManager;
    [Inject] private DealDamageManager dealDamageManager;
    [Inject] private EnemyManager enemyManager;

    private ProjectilePoolController poolController;
    private bool isActive;
    public float speed { get; private set; }
    public float damage { get; private set; }
    public float baseDamage;
    public float maxLength { get; private set; }
    public EnemyDamageType DamageType { get; private set; } = EnemyDamageType.Kinetic;
    public Vector3 direction{ get; private set; }
    public float fadeTime = 1f;
    private ParentShip owner;
    private ProjectileRuntimeBehaviorSet runtimeBehaviors;
    private SpriteRenderer spriteRenderer;
    private Rigidbody2D physicsBody;
    private Collider2D[] projectileColliders;
    private bool[] initialColliderStates;
    private Vector3 initialScale;
    private Color initialColor;
    private float remainingLifetime;
    private float lifetimeFadeDuration;
    private bool fadeDuringLifetime;
    private bool disableColliderAfterFirstPhysicsStep;
    private int completedPhysicsSteps;
    private iDamagable ignoredDamageTarget;
    private bool hasSecondaryProjectileRuntimeStats;
    private ProjectileRuntimeStats secondaryProjectileRuntimeStats;
    public ParentShip Owner { get; set; }
    Vector3 startPosition;
    public float GetDamage() => damage;
    public float GetSpeed() => speed;

    public void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        physicsBody = GetComponent<Rigidbody2D>();
        projectileColliders = GetComponentsInChildren<Collider2D>(true);
        initialColliderStates = new bool[projectileColliders.Length];
        for (int i = 0; i < projectileColliders.Length; i++)
            initialColliderStates[i] = projectileColliders[i].enabled;

        initialScale = transform.localScale;
        initialColor = spriteRenderer != null ? spriteRenderer.color : Color.white;
    }

    public void SetPoolController(ProjectilePoolController controller)
    {
        poolController = controller;
    }

    public void ResetState()
    {
        isActive = false;
        fadeTime = 1f;
        speed = 0f;
        damage = 0f;
        baseDamage = 0f;
        maxLength = 0f;
        DamageType = EnemyDamageType.Kinetic;
        direction = Vector3.zero;
        remainingLifetime = 0f;
        lifetimeFadeDuration = 0f;
        fadeDuringLifetime = false;
        disableColliderAfterFirstPhysicsStep = false;
        completedPhysicsSteps = 0;
        Owner = null;
        ignoredDamageTarget = null;
        hasSecondaryProjectileRuntimeStats = false;
        secondaryProjectileRuntimeStats = default;
        runtimeBehaviors?.Reset();
        runtimeBehaviors = null;
        transform.localScale = initialScale;

        if (physicsBody != null)
        {
            physicsBody.linearVelocity = Vector2.zero;
            physicsBody.angularVelocity = 0f;
        }

        if (spriteRenderer != null)
            spriteRenderer.color = initialColor;

        RestoreColliderStates();
    }
    public void SetDamage(float newDamage)
    {
        damage = newDamage;
    }

    public void SetSpeed(float newSpeed)
    {
        speed = Mathf.Max(0f, newSpeed);
    }

    public void SetDirection(Vector3 newDirection)
    {
        if (newDirection.sqrMagnitude <= Mathf.Epsilon)
            return;

        direction = newDirection.normalized;
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, 0f, angle + 90f);
    }

    public void Fade(float speed)
    {
        if (!isActive)
            return;

        fadeTime -= speed * Time.deltaTime;

        if (spriteRenderer != null)
        {
            var c = spriteRenderer.color;
            c.a = Mathf.Clamp01(fadeTime);
            spriteRenderer.color = c;
        }

        if (fadeTime <= 0f)
            ReturnToPool();
    }
    public void Init(ProjectileParams param, ProjectileRuntimeConfig runtimeConfig, ParentShip owner)
    {
        isActive = true;
        speed = param.speed;
        damage = param.damage;
        baseDamage = param.damage;
        maxLength = runtimeConfig.secondaryProjectile != null
                    && runtimeConfig.secondarySpawnTrigger
                    == SecondaryProjectileSpawnTrigger.AfterTravelDistance
            ? Mathf.Max(param.maxLength, runtimeConfig.secondaryTravelDistance)
            : param.maxLength;
        DamageType = runtimeConfig.damageType;
        remainingLifetime = Mathf.Max(0.02f, runtimeConfig.projectileLifetime);
        lifetimeFadeDuration = Mathf.Clamp(
            runtimeConfig.fadeDuration,
            0.02f,
            remainingLifetime);
        fadeDuringLifetime = runtimeConfig.fadeDuringLifetime;
        disableColliderAfterFirstPhysicsStep =
            runtimeConfig.disableColliderAfterFirstPhysicsStep;
        completedPhysicsSteps = 0;
        ignoredDamageTarget = null;
        hasSecondaryProjectileRuntimeStats =
            runtimeConfig.hasSecondaryProjectileRuntimeStats;
        secondaryProjectileRuntimeStats = new ProjectileRuntimeStats(
            runtimeConfig.secondaryProjectileDamage,
            runtimeConfig.secondaryProjectileRange,
            runtimeConfig.secondaryProjectileSpeed);
        RestoreColliderStates();

        if (spriteRenderer != null)
            spriteRenderer.color = initialColor;

        direction = (param.maxAngle > 0)
            ? Quaternion.Euler(0, 0, Random.Range(-param.maxAngle, param.maxAngle)) * param.direction
            : param.direction;
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0, 0, angle+90);
        startPosition = transform.position;
        runtimeBehaviors = new ProjectileRuntimeBehaviorSet(dealDamageManager, enemyManager);
        runtimeBehaviors.Build(runtimeConfig, this);
        Owner = owner;
    }

    public bool HasReachedTravelDistance(float travelDistance)
    {
        float clampedDistance = Mathf.Max(0f, travelDistance);
        return (transform.position - startPosition).sqrMagnitude
            >= clampedDistance * clampedDistance;
    }

    public bool TrySpawnSecondaryProjectile(
        ProjectileData projectileData,
        iDamagable ignoredTarget)
    {
        if (!isActive
            || projectileData == null
            || projectileData.DeliveryType != ProjectileDeliveryType.Projectile
            || projectileData.ProjectilePrefab == null
            || poolController == null)
        {
            return false;
        }

        ProjectileRuntimeStats stats = hasSecondaryProjectileRuntimeStats
            ? secondaryProjectileRuntimeStats
            : projectileData.GetRuntimeStats();
        ProjectileRuntimeConfig runtimeConfig = projectileData.CreateRuntimeConfig();
        if (hasSecondaryProjectileRuntimeStats)
        {
            float baseDamage = projectileData.Damage;
            if (baseDamage > 0f)
            {
                runtimeConfig.explosionDamage *= stats.Damage / baseDamage;
            }
        }

        Projectile spawnedProjectile = poolController.Spawn(
            projectileData.ProjectilePrefab,
            transform.position,
            transform.rotation);
        if (spawnedProjectile == null)
            return false;

        spawnedProjectile.Init(
            new ProjectileParams
            {
                speed = stats.Speed,
                damage = stats.Damage,
                maxLength = stats.Range,
                direction = direction,
                maxAngle = 0f
            },
            runtimeConfig,
            Owner);
        spawnedProjectile.ignoredDamageTarget = ignoredTarget;
        return true;
    }

    private void Update()
    {
        if (!isActive)
            return;

        remainingLifetime -= Time.deltaTime;

        if (fadeDuringLifetime && spriteRenderer != null)
        {
            Color color = initialColor;
            color.a *= Mathf.Clamp01(
                remainingLifetime / lifetimeFadeDuration);
            spriteRenderer.color = color;
        }

        if (remainingLifetime <= 0f)
            ReturnToPool();
    }

    void FixedUpdate()
    {
        if (!isActive)
            return;

        UpdateColliderLifetime();
        runtimeBehaviors?.Move(this);
        runtimeBehaviors?.Tick(this);
        if ((transform.position - startPosition).sqrMagnitude > maxLength * maxLength)
        {
            if (runtimeBehaviors == null
                || !runtimeBehaviors.TryHandleMaximumRange(this))
            {
                ReturnToPool();
            }
        }
    }

    private void UpdateColliderLifetime()
    {
        if (!disableColliderAfterFirstPhysicsStep)
            return;

        if (completedPhysicsSteps == 0)
        {
            completedPhysicsSteps = 1;
            return;
        }

        for (int i = 0; i < projectileColliders.Length; i++)
            projectileColliders[i].enabled = false;
    }

    private void RestoreColliderStates()
    {
        for (int i = 0; i < projectileColliders.Length; i++)
            projectileColliders[i].enabled = initialColliderStates[i];
    }

    public void ReturnToPool()
    {
        if (!isActive)
            return;

        isActive = false;
        runtimeBehaviors?.Reset();
        runtimeBehaviors = null;

        if (poolController != null)
            poolController.Release(this);
        else
            Destroy(gameObject);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.TryGetComponent<iDamagable>(out var target))
        {
            if (target == ignoredDamageTarget)
                return;

            runtimeBehaviors?.OnContactEnter(target, this);
        }
    }
    void OnTriggerStay2D(Collider2D other)
    {
        if (other.TryGetComponent<iDamagable>(out var target))
        {
            if (target == ignoredDamageTarget)
                return;

            runtimeBehaviors?.OnContactStay(target, this);
        }
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (other.TryGetComponent<iDamagable>(out var target))
        {
            if (target == ignoredDamageTarget)
                return;

            runtimeBehaviors?.OnContactExit(target, this);
        }
    }
}
