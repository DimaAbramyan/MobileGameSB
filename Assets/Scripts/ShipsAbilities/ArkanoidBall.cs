using System.Collections.Generic;
using UnityEngine;
using Zenject;

public sealed class ArkanoidBall : EnemyProjectileInterceptor
{
    private EnemyProjectileDestructionPolicy projectileDestructionPolicy = EnemyProjectileDestructionPolicy.FragileOnly;
    public override EnemyProjectileDestructionPolicy ProjectileDestructionPolicy => projectileDestructionPolicy;
    [Header("References")]
    [SerializeField] private Rigidbody2D rb;
    [SerializeField] private CircleCollider2D ballCollider;

    [Header("Movement")]
    [SerializeField, Min(0.01f)] private float speed = 5f;
    [SerializeField, Range(5f, 85f)] private float maxPaddleBounceAngle = 60f;
    [SerializeField, Min(0f)] private float screenPadding = 0.15f;
    [SerializeField, Min(0f)] private float bottomFallPadding = 0.5f;
    [SerializeField, Min(0f)] private float respawnDelay = 1.5f;
    [SerializeField, Min(0f)] private float minimumRespawnForwardOffset = 0.75f;
    [SerializeField, Range(0f, 80f)] private float respawnRandomLaunchAngle = 45f;

    [Header("Paddle Attraction")]
    [SerializeField, Min(0f)] private float paddleAttractionRadius = 2f;
    [SerializeField, Min(0f)] private float paddleAttractionForce = 10f;

    [Header("Damage")]
    [SerializeField, Min(0f)] private float contactDamage = 10f;
    [SerializeField] private EnemyDamageType contactDamageType = EnemyDamageType.Default;
    [SerializeField, Min(0f)] private float sameEnemyHitCooldown = 0.15f;
    [SerializeField] private LayerMask contactEnemyLayers = ~0;

    [Header("Stasis")]
    [SerializeField, Min(0.05f)] private float stasisDuration = 3f;
    [SerializeField, Min(1f)] private float stasisRadiusMultiplier = 1.6f;
    [SerializeField, Min(0f)] private float stasisDamagePerSecond = 30f;
    [SerializeField] private EnemyDamageType stasisDamageType = EnemyDamageType.Default;
    [SerializeField, Min(0.02f)] private float stasisTickInterval = 0.1f;
    [SerializeField] private LayerMask stasisEnemyLayers = ~0;
    [SerializeField] private LayerMask stasisProjectileLayers = ~0;

    private readonly Dictionary<Enemy, float> nextEnemyHitTimes = new();
    private readonly Collider2D[] contactHits = new Collider2D[32];
    private readonly Collider2D[] stasisHits = new Collider2D[64];
    private ContactFilter2D contactEnemyFilter;
    private ContactFilter2D stasisEnemyFilter;
    private ContactFilter2D stasisProjectileFilter;

    private ParentShip owner;
    [Inject] private DealDamageManager damageManager;
    private bool hasBouncedFromTop;
    public bool CanBeAttractedToPaddle => hasBouncedFromTop && !isStasisActive && !isRespawning
        && isActiveAndEnabled && owner != null && owner.IsVisible && !owner.IsDestroyed
        && paddle != null && paddle.IsActiveForBounce;
    private readonly HashSet<Enemy> stasisDamagedEnemies = new();
    [InjectOptional] private EnemyProjectileCollisionRegistry enemyProjectileCollisionRegistry;
    private ArkanoidPaddle paddle;
    private Camera mainCamera;
    private Vector2 direction = Vector2.up;
    private float baseColliderRadius;
    private float stasisTimer;
    private float stasisTickTimer;
    private float respawnTimer;
    private Vector2 spawnOffset;
    private float abilityDamageMultiplier = 1f;
    private bool isStasisActive;
    private bool isRespawning;
    private ArkanoidShipMetaContract bounceSettings;
    private float bounceBoostElapsed = float.PositiveInfinity;
    public float CurrentSpeed => speed * (bounceSettings != null
        ? bounceSettings.EvaluatePaddleBounceSpeedMultiplier(bounceBoostElapsed) : 1f);

    private void OnEnable()
    {
        enemyProjectileCollisionRegistry?.RegisterInterceptor(this);
    }

    private void Awake()
    {
        if (rb == null)
            rb = GetComponent<Rigidbody2D>();
        if (ballCollider == null)
            ballCollider = GetComponent<CircleCollider2D>();

        if (ballCollider != null)
            baseColliderRadius = ballCollider.radius;

        mainCamera = Camera.main;
        ConfigureFilters();
    }

    public void Configure(
        ParentShip shipOwner,
        ArkanoidPaddle reboundPaddle,
        float ballSpeed,
        float ballDamage,
        Vector2 ballSpawnOffset)
    {
        owner = shipOwner;
        paddle = reboundPaddle;
        speed = Mathf.Max(0.01f, ballSpeed);
        contactDamage = Mathf.Max(0f, ballDamage);
        spawnOffset = ballSpawnOffset;
        enemyProjectileCollisionRegistry?.RegisterInterceptor(this);
    }

    public void ApplyShipMetaStats(ArkanoidShipMetaContract contract)
    {
        if (contract == null)
            return;

        speed = contract.BallSpeed;
        bounceSettings = contract;
        paddleAttractionRadius = contract.PaddleAttractionRadius;
        paddleAttractionForce = contract.PaddleAttractionForce;
        projectileDestructionPolicy = contract.ProjectileDestructionPolicy;
        contactDamage = contract.BallDamage;
        contactDamageType = contract.BallDamageType;
        stasisDamageType = contract.StasisDamageType;
        stasisDuration = contract.StasisDuration;
        stasisRadiusMultiplier = contract.StasisRadiusMultiplier;
        stasisDamagePerSecond = contract.StasisDamagePerSecond;
        stasisTickInterval = contract.StasisTickInterval;
    }

    public void ApplyBattleAbilityDamageMultiplier(float damageMultiplier)
    {
        abilityDamageMultiplier = Mathf.Max(1f, damageMultiplier);
    }

    public void ResetAndLaunch(
        Vector3 startPosition,
        Vector2 launchDirection)
    {
        bounceBoostElapsed = float.PositiveInfinity;
        hasBouncedFromTop = false;
        EndStasis();
        SetBallVisible(true);
        isRespawning = false;
        respawnTimer = 0f;
        transform.position = startPosition;
        direction = launchDirection.sqrMagnitude > 0.001f
            ? launchDirection.normalized
            : Vector2.up;

        if (rb != null)
        {
            rb.linearVelocity = direction * CurrentSpeed;
            rb.angularVelocity = 0f;
        }

        gameObject.SetActive(true);
    }

    public bool TryActivateStasis()
    {
        if (!gameObject.activeInHierarchy || isStasisActive || isRespawning)
            return false;

        isStasisActive = true;
        stasisTimer = stasisDuration;
        stasisTickTimer = 0f;

        if (rb != null)
            rb.linearVelocity = Vector2.zero;

        if (ballCollider != null)
            ballCollider.radius = baseColliderRadius * stasisRadiusMultiplier;

        return true;
    }

    public bool TryRespawnIfReady()
    {
        if (!isRespawning || respawnTimer > 0f || !CanRespawnNow())
            return false;

        RespawnBall();
        return true;
    }

    private void FixedUpdate()
    {
        if (bounceSettings != null && bounceBoostElapsed < bounceSettings.PaddleBounceRecoveryDuration)
            bounceBoostElapsed = Mathf.Min(bounceSettings.PaddleBounceRecoveryDuration,
                bounceBoostElapsed + Time.fixedDeltaTime);
        if (isRespawning)
        {
            TickRespawn();
            return;
        }

        if (isStasisActive)
        {
            TickStasis();
            return;
        }

        TryApplyPaddleAttraction(Time.fixedDeltaTime);
        MoveBall();
        TryBounceFromPaddleOverlap();
        DamageEnemiesByOverlap();
        BounceFromScreenBounds();
    }

    private void MoveBall()
    {
        direction = direction.sqrMagnitude > 0.001f
            ? direction.normalized
            : Vector2.up;

        if (rb != null)
            rb.linearVelocity = direction * CurrentSpeed;
        else
            transform.position += (Vector3)(direction * CurrentSpeed * Time.fixedDeltaTime);
    }

    private void BounceFromScreenBounds()
    {
        if (mainCamera == null)
            mainCamera = Camera.main;
        if (mainCamera == null)
            return;

        Vector3 min = mainCamera.ViewportToWorldPoint(Vector3.zero);
        Vector3 max = mainCamera.ViewportToWorldPoint(Vector3.one);
        Vector3 position = transform.position;
        float radius = GetWorldRadius();
        bool bounced = false;

        if (position.x - radius < min.x + screenPadding)
        {
            position.x = min.x + screenPadding + radius;
            direction.x = Mathf.Abs(direction.x);
            bounced = true;
        }
        else if (position.x + radius > max.x - screenPadding)
        {
            position.x = max.x - screenPadding - radius;
            direction.x = -Mathf.Abs(direction.x);
            bounced = true;
        }

        if (position.y + radius < min.y - bottomFallPadding)
        {
            LoseBall();
            return;
        }
        else if (position.y + radius > max.y - screenPadding)
        {
            position.y = max.y - screenPadding - radius;
            if (direction.y > 0f)
            {
                direction.y = -direction.y;
                hasBouncedFromTop = true;
            }
            bounced = true;
        }

        if (!bounced)
            return;

        transform.position = position;
        if (rb != null)
            rb.linearVelocity = direction.normalized * CurrentSpeed;
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (IsDestroyBoundary(collision.collider))
        {
            IgnoreDestroyBoundaryCollision(collision.collider);
            return;
        }

        EnemyProjectile projectile =
            collision.collider.GetComponentInParent<EnemyProjectile>();
        if (TryDestroyEnemyProjectile(projectile))
            return;

        ArkanoidPaddle hitPaddle =
            collision.collider.GetComponentInParent<ArkanoidPaddle>();
        if (hitPaddle != null)
        {
            BounceFromPaddle(hitPaddle, IsBallAbovePaddle(hitPaddle));
            return;
        }

        Enemy enemy = collision.collider.GetComponentInParent<Enemy>();
        if (enemy != null)
        {
            DealContactDamage(enemy);
            return;
        }

        if (collision.contactCount > 0)
        {
            Vector2 normal = collision.GetContact(0).normal;
            direction = Vector2.Reflect(direction, normal).normalized;
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (IsDestroyBoundary(other))
            return;

        EnemyProjectile projectile =
            other.GetComponentInParent<EnemyProjectile>();
        if (TryDestroyEnemyProjectile(projectile))
            return;

        ArkanoidPaddle hitPaddle = other.GetComponentInParent<ArkanoidPaddle>();
        if (hitPaddle != null)
        {
            BounceFromPaddle(hitPaddle, IsBallAbovePaddle(hitPaddle));
            return;
        }

        Enemy enemy = other.GetComponentInParent<Enemy>();
        if (enemy != null)
            DealContactDamage(enemy);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        if (IsDestroyBoundary(other))
            return;

        Enemy enemy = other.GetComponentInParent<Enemy>();
        if (enemy != null)
            DealContactDamage(enemy);
    }

    public override void NotifyEnemyProjectileIntercepted()
    {
        if (!isStasisActive && !isRespawning && rb != null)
            rb.linearVelocity = direction.normalized * CurrentSpeed;
    }

    private bool IsDestroyBoundary(Collider2D other)
    {
        return other != null
            && other.GetComponentInParent<IProjectileDestroyBoundary>() != null;
    }

    private void IgnoreDestroyBoundaryCollision(Collider2D boundaryCollider)
    {
        if (ballCollider != null && boundaryCollider != null)
            Physics2D.IgnoreCollision(ballCollider, boundaryCollider, true);
    }

    private void BounceFromPaddle(ArkanoidPaddle hitPaddle, bool bounceUp)
    {
        hasBouncedFromTop = false;
        bounceBoostElapsed = 0f;
        float halfWidth = hitPaddle.GetHalfWidth();
        float normalizedOffset = Mathf.Clamp(
            (transform.position.x - hitPaddle.transform.position.x) / halfWidth,
            -1f,
            1f);

        float angle = normalizedOffset * maxPaddleBounceAngle;
        Vector2 baseDirection = bounceUp ? Vector2.up : Vector2.down;
        float signedAngle = bounceUp ? -angle : angle;
        direction = Quaternion.Euler(0f, 0f, signedAngle) * baseDirection;
        direction.Normalize();

        if (rb != null)
            rb.linearVelocity = direction * CurrentSpeed;
    }

    private void TryBounceFromPaddleOverlap()
    {
        if (paddle == null || !paddle.IsActiveForBounce)
            return;

        Collider2D paddleCollider = paddle.GetComponent<Collider2D>();
        if (paddleCollider == null)
            return;

        Bounds bounds = paddleCollider.bounds;
        Vector3 position = transform.position;
        float radius = GetWorldRadius();
        bool overlapsX = position.x + radius >= bounds.min.x
            && position.x - radius <= bounds.max.x;
        bool overlapsY = position.y - radius <= bounds.max.y
            && position.y + radius >= bounds.min.y;

        if (!overlapsX || !overlapsY)
            return;

        bool bounceUp = IsBallAbovePaddle(bounds);
        position.y = bounceUp
            ? bounds.max.y + radius
            : bounds.min.y - radius;
        transform.position = position;
        BounceFromPaddle(paddle, bounceUp);
    }

    private bool IsBallAbovePaddle(ArkanoidPaddle hitPaddle)
    {
        Collider2D paddleCollider = hitPaddle != null
            ? hitPaddle.GetComponent<Collider2D>()
            : null;

        return paddleCollider == null || IsBallAbovePaddle(paddleCollider.bounds);
    }

    private bool IsBallAbovePaddle(Bounds paddleBounds)
    {
        if (transform.position.y > paddleBounds.center.y)
            return true;
        if (transform.position.y < paddleBounds.center.y)
            return false;

        return direction.y <= 0f;
    }

    private void DamageEnemiesByOverlap()
    {
        int count = Physics2D.OverlapCircle(
            transform.position,
            GetWorldRadius(),
            contactEnemyFilter,
            contactHits);

        for (int i = 0; i < count; i++)
        {
            Enemy enemy = contactHits[i] != null
                ? contactHits[i].GetComponentInParent<Enemy>()
                : null;

            if (enemy != null)
                DealContactDamage(enemy);
        }
    }

    private void DealContactDamage(Enemy enemy)
    {
        if (enemy == null || enemy.isDead)
            return;

        if (nextEnemyHitTimes.TryGetValue(enemy, out float nextHitTime)
            && Time.time < nextHitTime)
        {
            return;
        }

        nextEnemyHitTimes[enemy] = Time.time + sameEnemyHitCooldown;
        float damage = contactDamage * abilityDamageMultiplier;
        damageManager.DealDamage(enemy, owner, damage, contactDamageType);
    }

    private void TryApplyPaddleAttraction(float deltaTime)
    {
        if (!CanBeAttractedToPaddle || paddleAttractionForce <= 0f || deltaTime <= 0f)
            return;

        Vector2 offset = (Vector2)paddle.transform.position - (Vector2)transform.position;
        if (offset.sqrMagnitude > paddleAttractionRadius * paddleAttractionRadius
            || offset.sqrMagnitude < 1e-8f)
            return;

        float angle = Vector2.SignedAngle(direction, offset);
        float maximumTurn = paddleAttractionForce / Mathf.Max(.01f, CurrentSpeed) * Mathf.Rad2Deg * deltaTime;
        float turn = Mathf.Clamp(angle, -maximumTurn, maximumTurn);
        direction = ((Vector2)(Quaternion.Euler(0f, 0f, turn) * direction)).normalized;
    }

    private void TickStasis()
    {
        if (rb != null)
            rb.linearVelocity = Vector2.zero;

        stasisTimer -= Time.fixedDeltaTime;
        stasisTickTimer -= Time.fixedDeltaTime;

        if (stasisTickTimer <= 0f)
        {
            stasisTickTimer = stasisTickInterval;
            DamageEnemiesInStasis();
            DestroyProjectilesInStasis();
        }

        if (stasisTimer <= 0f)
            EndStasis();
    }

    private void DamageEnemiesInStasis()
    {
        stasisDamagedEnemies.Clear();
        int count = Physics2D.OverlapCircle(
            transform.position,
            GetWorldRadius(),
            stasisEnemyFilter,
            stasisHits);
        float damage = stasisDamagePerSecond
            * stasisTickInterval
            * abilityDamageMultiplier;

        for (int i = 0; i < count; i++)
        {
            Enemy enemy = stasisHits[i] != null
                ? stasisHits[i].GetComponentInParent<Enemy>()
                : null;

            if (enemy == null || enemy.isDead || !stasisDamagedEnemies.Add(enemy))
                continue;

            damageManager.DealDamage(enemy, owner, damage, stasisDamageType);
        }
    }

    private void DestroyProjectilesInStasis()
    {
        int count = Physics2D.OverlapCircle(
            transform.position,
            GetWorldRadius(),
            stasisProjectileFilter,
            stasisHits);

        for (int i = 0; i < count; i++)
        {
            EnemyProjectile projectile = stasisHits[i] != null
                ? stasisHits[i].GetComponentInParent<EnemyProjectile>()
                : null;

            if (projectile != null)
                TryDestroyEnemyProjectile(projectile);
        }
    }

    private void EndStasis()
    {
        isStasisActive = false;

        if (ballCollider != null)
            ballCollider.radius = baseColliderRadius;

        if (gameObject.activeInHierarchy && !isRespawning && rb != null)
            rb.linearVelocity = direction.normalized * CurrentSpeed;
    }

    private void LoseBall()
    {
        EndStasis();
        hasBouncedFromTop = false;
        isRespawning = true;
        respawnTimer = respawnDelay;

        if (rb != null)
            rb.linearVelocity = Vector2.zero;

        SetBallVisible(false);
    }

    private void TickRespawn()
    {
        respawnTimer = Mathf.Max(0f, respawnTimer - Time.fixedDeltaTime);
        if (respawnTimer > 0f || !CanRespawnNow())
            return;

        RespawnBall();
    }

    private void RespawnBall()
    {
        ResetAndLaunch(GetOwnerFrontSpawnPosition(), CreateRandomLaunchDirection());
    }

    private bool CanRespawnNow()
    {
        return owner == null || owner.IsVisible;
    }

    public Vector3 GetOwnerFrontSpawnPosition()
    {
        if (owner == null)
            return transform.position;

        Vector2 localOffset = spawnOffset;
        localOffset.y = Mathf.Max(localOffset.y, minimumRespawnForwardOffset);

        return owner.transform.position
            + owner.transform.TransformVector(localOffset);
    }

    private Vector2 CreateRandomLaunchDirection()
    {
        float angle = Random.Range(-respawnRandomLaunchAngle, respawnRandomLaunchAngle);
        return Quaternion.Euler(0f, 0f, angle) * Vector2.up;
    }

    private void SetBallVisible(bool isVisible)
    {
        if (ballCollider != null)
            ballCollider.enabled = isVisible;

        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
            renderers[i].enabled = isVisible;
    }

    private float GetWorldRadius()
    {
        if (ballCollider == null)
            return Mathf.Max(transform.lossyScale.x, transform.lossyScale.y) * 0.5f;

        return ballCollider.radius
            * Mathf.Max(Mathf.Abs(transform.lossyScale.x), Mathf.Abs(transform.lossyScale.y));
    }

    private void OnDisable()
    {
        enemyProjectileCollisionRegistry?.UnregisterInterceptor(this);

        if (rb != null)
            rb.linearVelocity = Vector2.zero;
    }

    private void ConfigureFilters()
    {
        contactEnemyFilter = new ContactFilter2D
        {
            useLayerMask = true,
            layerMask = contactEnemyLayers,
            useTriggers = true
        };

        stasisEnemyFilter = new ContactFilter2D
        {
            useLayerMask = true,
            layerMask = stasisEnemyLayers,
            useTriggers = true
        };

        stasisProjectileFilter = new ContactFilter2D
        {
            useLayerMask = true,
            layerMask = stasisProjectileLayers,
            useTriggers = true
        };
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        ConfigureFilters();
    }
#endif
}
