using UnityEngine;
using System.Collections.Generic;
using System.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Zenject;

public class CreateBladeAbility : ActiveAbility, IStatValueResolver
{
    private BladeShipMetaContract displayStatSnapshot;
    public override IStatValueProvider DisplayStatSnapshot => displayStatSnapshot;
    public bool IsRuntime => true;
    public bool TryRead(StatValueDefinition definition, out StatValueResult result)
    {
        bool found = displayStatSnapshot != null && definition.ProviderType == typeof(BladeShipMetaContract)
            && definition.Property == nameof(BladeShipMetaContract.Damage);
        result = new StatValueResult(MaterializedBladeTrail.GetDamage(bladeDamage, owner, abilityDamageMultiplier), StatValueState.Runtime);
        return found;
    }
    [Inject] private EnemyProjectileCollisionRegistry projectileCollisionRegistry;
    [Inject] private DealDamageManager damageManager;
    private EnemyDamageType damageType = EnemyDamageType.Default;
    [SerializeField] private GameObject blade;
    private EnemyProjectileDestructionPolicy projectileDestructionPolicy = EnemyProjectileDestructionPolicy.NormalAndFragile;
    [SerializeField, HideInInspector, Min(0.05f)]
    private float recordingDuration = 1f;
    [SerializeField, HideInInspector] private float sampleInterval = 0.1f;
    [SerializeField, HideInInspector] private float bladeWidth = 0.45f;
    [SerializeField, HideInInspector] private float bladeSpeed = 12f;
    [SerializeField, HideInInspector] private float bladeDamage = 50f;
    [SerializeField, HideInInspector] private float bladeLifetime = 4f;
    [SerializeField] private int previewSortingOrder = 19;

    private readonly List<Vector2> savedPoints = new();
    private Coroutine collectCoroutine;
    private LineRenderer previewLine;
    private ParentShip intangibleOwner;
      private WeaponController suppressedWeaponController;
      private float abilityDamageMultiplier = 1f;

      protected override void ApplySpecificShipMetaStats(
        ShipMetaRuntimeStats stats)
    {
        if (!stats.TryGetContract(out BladeShipMetaContract contract))
            return;

        recordingDuration = contract.RecordingDuration;
        displayStatSnapshot = contract;
        sampleInterval = contract.SampleInterval;
        bladeWidth = contract.Width;
        bladeSpeed = contract.Speed;
        bladeDamage = contract.Damage;
        damageType = contract.DamageType;
        bladeLifetime = contract.Lifetime;
        projectileDestructionPolicy = contract.ProjectileDestructionPolicy;
      }

      public override void ApplyBattleAbilityDamageMultiplier(
          float damageMultiplier)
      {
          abilityDamageMultiplier = Mathf.Max(1f, damageMultiplier);
      }

    public override bool Activate(ParentShip owner)
    {
        if (blade == null || collectCoroutine != null)
            return false;

        owner?.LockShipSwitching(recordingDuration);
        BeginAbilityRestrictions(owner);
        collectCoroutine = StartCoroutine(CollectAndSpawn(owner));
        return true;
    }

    private IEnumerator CollectAndSpawn(ParentShip owner)
    {
        savedPoints.Clear();
        EnsurePreviewLine();
        UpdatePreviewLine();
        yield return StartCoroutine(CollectPoints(owner));
        SpawnBlade(owner);
        HidePreviewLine();
        EndAbilityRestrictions();
        collectCoroutine = null;
    }

    private IEnumerator CollectPoints(ParentShip owner)
    {
        float elapsed = 0f;
        while (elapsed < recordingDuration)
        {
            Transform pointSource = owner != null ? owner.transform : transform;
            Vector2 currentPoint = pointSource.position;

            if (savedPoints.Count == 0
                || Vector2.Distance(savedPoints[^1], currentPoint) > 0.05f)
            {
                savedPoints.Add(currentPoint);
                UpdatePreviewLine();
            }

            yield return new WaitForSeconds(sampleInterval);
            elapsed += sampleInterval;
        }
    }

    public void SpawnBlade(ParentShip owner = null)
    {
        if (savedPoints.Count == 0)
            return;

        if (savedPoints.Count == 1)
        {
            Vector2 center = savedPoints[0];
            Vector2 forward = GetForwardDirection(owner);
            Vector2 right = new Vector2(forward.y, -forward.x) * bladeWidth * .5f;
            savedPoints[0] = center - right;
            savedPoints.Add(center + right);
        }

        GameObject trail = Instantiate(blade, savedPoints[0], Quaternion.identity);
        MaterializedBladeTrail materializedBlade =
            trail.GetComponent<MaterializedBladeTrail>();

        if (materializedBlade == null)
            materializedBlade = trail.AddComponent<MaterializedBladeTrail>();

          materializedBlade.SetProjectileDestructionPolicy(projectileDestructionPolicy);
          materializedBlade.Init(
              savedPoints,
              GetForwardDirection(owner),
              bladeWidth,
              bladeSpeed,
              bladeDamage,
              bladeLifetime,
              owner,
              abilityDamageMultiplier,
              projectileCollisionRegistry, damageManager, damageType);
        savedPoints.Clear();
    }

    private Vector2 GetForwardDirection(ParentShip owner)
    {
        Transform directionSource = owner != null ? owner.transform : transform;
        Vector2 forward = directionSource.up;

        return forward.sqrMagnitude > 0.001f
            ? forward.normalized
            : Vector2.right;
    }

    private void EnsurePreviewLine()
    {
        if (previewLine != null)
            return;

        var previewObject = new GameObject("Blade Trail Preview");
        previewObject.transform.SetParent(transform, false);

        previewLine = previewObject.AddComponent<LineRenderer>();
        previewLine.useWorldSpace = true;
        previewLine.startWidth = bladeWidth;
        previewLine.endWidth = bladeWidth;
        previewLine.numCapVertices = 4;
        previewLine.numCornerVertices = 4;
        previewLine.sortingOrder = previewSortingOrder;

        SpriteRenderer bladeRenderer =
            blade != null ? blade.GetComponent<SpriteRenderer>() : null;
        if (bladeRenderer != null)
            previewLine.sharedMaterial = bladeRenderer.sharedMaterial;
    }

    private void UpdatePreviewLine()
    {
        if (previewLine == null)
            return;

        previewLine.enabled = savedPoints.Count > 0;
        previewLine.positionCount = savedPoints.Count;

        for (int i = 0; i < savedPoints.Count; i++)
            previewLine.SetPosition(i, savedPoints[i]);
    }

    private void HidePreviewLine()
    {
        if (previewLine == null)
            return;

        previewLine.positionCount = 0;
        previewLine.enabled = false;
    }

    private void BeginAbilityRestrictions(ParentShip owner)
    {
        intangibleOwner = owner;
        intangibleOwner?.EnterIntangibleState();

        suppressedWeaponController = owner != null
            ? owner.GetComponent<WeaponController>()
            : GetComponent<WeaponController>();
        suppressedWeaponController?.BeginShootingSuppression();
    }

    private void EndAbilityRestrictions()
    {
        if (intangibleOwner != null)
            intangibleOwner.ExitIntangibleState();

        intangibleOwner = null;

        if (suppressedWeaponController != null)
            suppressedWeaponController.EndShootingSuppression();

        suppressedWeaponController = null;
    }

    protected override void OnDisable()
    {
        base.OnDisable();

        if (collectCoroutine != null)
        {
            StopCoroutine(collectCoroutine);
            collectCoroutine = null;
        }

        savedPoints.Clear();
        HidePreviewLine();
        EndAbilityRestrictions();
    }
}

public sealed class MaterializedBladeTrail : EnemyProjectileInterceptor
{
    public static float GetDamage(float baseDamage, ParentShip source, float fallbackMultiplier)
        => baseDamage * (source != null && source.ShipData != null
            ? source.ShipData.GetAbilityDamageMultiplier(source.GetLevel()) : fallbackMultiplier);
    private DealDamageManager damageManager;
    private EnemyDamageType damageType = EnemyDamageType.Default;
    private readonly ContactFilter2D damageFilter = new ContactFilter2D { useTriggers = true };
    private Collider2D[] overlapHits = new Collider2D[32];
    private RaycastHit2D[] sweepHits = new RaycastHit2D[32];
    private Vector2 previousDamagePosition;
    private EnemyProjectileCollisionRegistry projectileRegistry;
    private Vector2[] projectileEdgePoints;
    private Vector2 previousPosition;
    private float projectileEdgeRadius;
    private EnemyProjectileDestructionPolicy projectileDestructionPolicy = EnemyProjectileDestructionPolicy.NormalAndFragile;
    public override EnemyProjectileDestructionPolicy ProjectileDestructionPolicy =>
        projectileDestructionPolicy;
    public void SetProjectileDestructionPolicy(EnemyProjectileDestructionPolicy policy)
        => projectileDestructionPolicy = policy;
    private readonly HashSet<Enemy> damagedEnemies = new();

    private float damage;
    private float lifetime;
    private float aliveTime;
      private float speed;
      private Rigidbody2D body;
      private Vector2 direction = Vector2.right;
      private ParentShip owner;
      private float fallbackDamageMultiplier = 1f;

    public void Init(
        IReadOnlyList<Vector2> worldPoints,
        Vector2 direction,
        float width,
          float speed,
          float damage,
          float lifetime,
          ParentShip sourceOwner,
          float damageMultiplier,
          EnemyProjectileCollisionRegistry collisionRegistry = null,
          DealDamageManager damageManager = null,
          EnemyDamageType damageType = EnemyDamageType.Default)
    {
        this.speed = speed;
        this.damage = damage;
        this.damageManager = damageManager;
        this.damageType = damageType;
          this.lifetime = lifetime;
          owner = sourceOwner;
          fallbackDamageMultiplier = Mathf.Max(1f, damageMultiplier);
        this.direction = direction.sqrMagnitude > 0.001f
            ? direction.normalized
            : Vector2.right;
        aliveTime = 0f;
        damagedEnemies.Clear();

        Vector2 origin = worldPoints[0];
        transform.position = origin;
        transform.rotation = Quaternion.identity;

        Vector2[] localPoints = new Vector2[worldPoints.Count];
        for (int i = 0; i < worldPoints.Count; i++)
            localPoints[i] = worldPoints[i] - origin;

        ConfigurePhysics(localPoints, width);
        ConfigureVisual(localPoints, width);
        projectileRegistry?.UnregisterInterceptor(this);
        projectileRegistry = collisionRegistry;
        projectileEdgePoints = localPoints;
        projectileEdgeRadius = width * 0.5f;
        previousPosition = transform.position;
        previousDamagePosition = transform.position;
        projectileRegistry?.RegisterInterceptor(this);
        ScanDamageContacts();
    }

    public void AppendProjectileCollisionTargets(DynamicBuffer<EnemyProjectileCollisionTarget> targets, int targetId)
    {
        if (!isActiveAndEnabled || projectileEdgePoints == null) return;
        Vector2 currentPosition = transform.position;
        Vector2 displacement = currentPosition - previousPosition;
        float radius = projectileEdgeRadius * Mathf.Max(Mathf.Abs(transform.lossyScale.x), Mathf.Abs(transform.lossyScale.y));
        for (int i = 1; i < projectileEdgePoints.Length; i++)
        {
            Vector3 a = transform.TransformPoint(projectileEdgePoints[i - 1]);
            Vector3 b = transform.TransformPoint(projectileEdgePoints[i]);
            targets.Add(new EnemyProjectileCollisionTarget
            {
                TargetId = targetId,
                Kind = EnemyProjectileCollisionTargetKind.Interceptor,
                DestroysNormal = DestroysNormalProjectiles ? (byte)1 : (byte)0,
                IsSegment = 1,
                SegmentStart = new float2(a.x, a.y),
                SegmentEnd = new float2(b.x, b.y),
                SegmentDisplacement = new float2(displacement.x, displacement.y),
                Radius = radius
            });
        }
        previousPosition = currentPosition;
    }

    private void OnEnable() => projectileRegistry?.RegisterInterceptor(this);
    private void OnDisable() => projectileRegistry?.UnregisterInterceptor(this);
    private void OnDestroy() => projectileRegistry?.UnregisterInterceptor(this);

    private void ConfigurePhysics(Vector2[] localPoints, float width)
    {
        PolygonCollider2D polygonCollider = GetComponent<PolygonCollider2D>();
        if (polygonCollider != null)
            polygonCollider.enabled = false;

        EdgeCollider2D edgeCollider = GetComponent<EdgeCollider2D>();
        if (edgeCollider == null)
            edgeCollider = gameObject.AddComponent<EdgeCollider2D>();

        edgeCollider.isTrigger = true;
        edgeCollider.enabled = true;
        edgeCollider.edgeRadius = width * 0.5f;
        edgeCollider.points = localPoints;

        body = GetComponent<Rigidbody2D>();
        if (body == null)
            body = gameObject.AddComponent<Rigidbody2D>();

        body.bodyType = RigidbodyType2D.Kinematic;
        body.gravityScale = 0f;
        body.useFullKinematicContacts = true;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        body.linearVelocity = direction * speed;
    }

    private void ConfigureVisual(Vector2[] localPoints, float width)
    {
        SpriteRenderer spriteRenderer = GetComponent<SpriteRenderer>();
        Material material = spriteRenderer != null
            ? spriteRenderer.sharedMaterial
            : null;

        if (spriteRenderer != null)
            spriteRenderer.enabled = false;

        LineRenderer lineRenderer = GetComponent<LineRenderer>();
        if (lineRenderer == null)
            lineRenderer = gameObject.AddComponent<LineRenderer>();

        lineRenderer.useWorldSpace = false;
        lineRenderer.positionCount = localPoints.Length;
        lineRenderer.startWidth = width;
        lineRenderer.endWidth = width;
        lineRenderer.numCapVertices = 4;
        lineRenderer.numCornerVertices = 4;
        lineRenderer.sortingOrder = 20;

        if (material != null)
            lineRenderer.sharedMaterial = material;

        for (int i = 0; i < localPoints.Length; i++)
            lineRenderer.SetPosition(i, localPoints[i]);
    }

    private void FixedUpdate()
    {
        ScanDamageContacts();
        aliveTime += Time.fixedDeltaTime;
        if (aliveTime >= lifetime)
        {
            Destroy(gameObject);
            return;
        }

        if (body != null)
            body.linearVelocity = direction * speed;
        else
            transform.position +=
                (Vector3)direction * speed * Time.fixedDeltaTime;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        HandleContact(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        HandleContact(other);
    }

    private void HandleContact(Collider2D other)
    {
        if (other == null) return;
        EnemyProjectile enemyProjectile =
            other.GetComponentInParent<EnemyProjectile>();
        if (enemyProjectile != null)
        {
            TryDestroyEnemyProjectile(enemyProjectile);
            return;
        }

        Enemy enemy = other.GetComponentInParent<Enemy>();
        if (enemy == null || enemy.isDead || damagedEnemies.Contains(enemy))
            return;

        damagedEnemies.Add(enemy);
        float amount = GetDamage(damage, owner, fallbackDamageMultiplier);
        if (damageManager != null) damageManager.DealDamage(enemy, owner, amount, damageType);
        else
        {
            var result = enemy.TakeDamageWithType(amount, damageType);
            if (result.DidDamageHull) owner?.NotifyDamageDealt(result.HullDamage);
        }
      }

    private void ScanDamageContacts()
    {
        if (projectileEdgePoints == null) return;
        Vector2 currentPosition = transform.position;
        Vector2 displacement = currentPosition - previousDamagePosition;
        float distance = displacement.magnitude;
        float radius = projectileEdgeRadius * Mathf.Max(Mathf.Abs(transform.lossyScale.x), Mathf.Abs(transform.lossyScale.y));
        for (int i = 1; i < projectileEdgePoints.Length; i++)
        {
            Vector2 a = transform.TransformPoint(projectileEdgePoints[i - 1]);
            Vector2 b = transform.TransformPoint(projectileEdgePoints[i]);
            Vector2 center = (a + b) * .5f;
            Vector2 edge = b - a;
            Vector2 size = new Vector2(edge.magnitude + radius * 2f, radius * 2f);
            float angle = Mathf.Atan2(edge.y, edge.x) * Mathf.Rad2Deg;
            if (distance > 0f)
            {
                int count;
                while ((count = Physics2D.CapsuleCast(center - displacement, size, CapsuleDirection2D.Horizontal,
                    angle, displacement / distance, damageFilter, sweepHits, distance)) == sweepHits.Length)
                    System.Array.Resize(ref sweepHits, sweepHits.Length * 2);
                for (int hit = 0; hit < count; hit++) HandleContact(sweepHits[hit].collider);
            }
            int overlaps;
            while ((overlaps = Physics2D.OverlapCapsule(center, size, CapsuleDirection2D.Horizontal,
                angle, damageFilter, overlapHits)) == overlapHits.Length)
                System.Array.Resize(ref overlapHits, overlapHits.Length * 2);
            for (int hit = 0; hit < overlaps; hit++) HandleContact(overlapHits[hit]);
        }
        previousDamagePosition = currentPosition;
    }

  }
