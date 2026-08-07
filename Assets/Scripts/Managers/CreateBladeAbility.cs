using UnityEngine;
using System.Collections.Generic;
using System.Collections;

public class CreateBladeAbility : ActiveAbility
{
    [SerializeField] private GameObject blade;
    [SerializeField, Min(0.05f)] private float recordingDuration = 1f;
    [SerializeField] private float sampleInterval = 0.1f;
    [SerializeField] private float bladeWidth = 0.45f;
    [SerializeField] private float bladeSpeed = 12f;
    [SerializeField] private float bladeDamage = 50f;
    [SerializeField] private float bladeLifetime = 4f;
    [SerializeField] private int previewSortingOrder = 19;

    private readonly List<Vector2> savedPoints = new();
    private Coroutine collectCoroutine;
    private LineRenderer previewLine;
    private ParentShip intangibleOwner;
    private WeaponController suppressedWeaponController;

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
        if (savedPoints.Count < 2)
            return;

        GameObject trail = Instantiate(blade, savedPoints[0], Quaternion.identity);
        MaterializedBladeTrail materializedBlade =
            trail.GetComponent<MaterializedBladeTrail>();

        if (materializedBlade == null)
            materializedBlade = trail.AddComponent<MaterializedBladeTrail>();

        materializedBlade.Init(
            savedPoints,
            GetForwardDirection(owner),
            bladeWidth,
            bladeSpeed,
            bladeDamage,
            bladeLifetime);
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

public sealed class MaterializedBladeTrail : MonoBehaviour
{
    private readonly HashSet<Enemy> damagedEnemies = new();

    private float damage;
    private float lifetime;
    private float aliveTime;
    private float speed;
    private Rigidbody2D body;
    private Vector2 direction = Vector2.right;

    public void Init(
        IReadOnlyList<Vector2> worldPoints,
        Vector2 direction,
        float width,
        float speed,
        float damage,
        float lifetime)
    {
        this.speed = speed;
        this.damage = damage;
        this.lifetime = lifetime;
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
    }

    private void ConfigurePhysics(Vector2[] localPoints, float width)
    {
        PolygonCollider2D polygonCollider = GetComponent<PolygonCollider2D>();
        if (polygonCollider != null)
            polygonCollider.enabled = false;

        EdgeCollider2D edgeCollider = GetComponent<EdgeCollider2D>();
        if (edgeCollider == null)
            edgeCollider = gameObject.AddComponent<EdgeCollider2D>();

        edgeCollider.isTrigger = true;
        edgeCollider.edgeRadius = width * 0.5f;
        edgeCollider.points = localPoints;

        body = GetComponent<Rigidbody2D>();
        if (body == null)
            body = gameObject.AddComponent<Rigidbody2D>();

        body.bodyType = RigidbodyType2D.Kinematic;
        body.gravityScale = 0f;
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
        EnemyProjectile enemyProjectile =
            other.GetComponentInParent<EnemyProjectile>();
        if (enemyProjectile != null)
        {
            Destroy(enemyProjectile.gameObject);
            return;
        }

        Enemy enemy = other.GetComponentInParent<Enemy>();
        if (enemy == null || damagedEnemies.Contains(enemy))
            return;

        damagedEnemies.Add(enemy);
        enemy.TakeDamage(damage);
    }
}
