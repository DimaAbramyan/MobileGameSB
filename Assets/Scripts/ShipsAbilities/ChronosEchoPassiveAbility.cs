using System.Collections.Generic;
using UnityEngine;

public sealed class ChronosEchoPassiveAbility : PassiveAbility
{
    [Header("Echo")]
    [SerializeField, Min(0.1f)] private float rewindSeconds = 2f;
    [SerializeField, Min(0.02f)] private float sampleInterval = 0.1f;
    [SerializeField, Min(0f)] private float passiveCooldown = 25f;
    [SerializeField, Range(0f, 1f)] private float restoredHealthPercent = 0.2f;
    [SerializeField, Range(0f, 1f)] private float restoredShieldPercent = 0.2f;

    [Header("Projectile Purge")]
    [SerializeField, Min(0f)] private float projectilePurgeRadius = 2f;
    [SerializeField] private LayerMask enemyProjectileLayers = ~0;

    private readonly Queue<EchoSnapshot> snapshots = new();
    private readonly Collider2D[] purgeHits = new Collider2D[64];
    private ContactFilter2D projectileFilter;
    private Rigidbody2D ownerBody;
    private float sampleTimer;
    private float cooldownTimer;

    private struct EchoSnapshot
    {
        public Vector3 Position;
        public float Time;
    }

    public override void Init(ParentShip ship)
    {
        owner = ship;
        ownerBody = owner != null ? owner.GetComponentInParent<Rigidbody2D>() : null;
        ConfigureFilter();
    }

    public override void On()
    {
        base.On();

        if (owner == null)
            owner = GetComponent<ParentShip>();
        if (ownerBody == null && owner != null)
            ownerBody = owner.GetComponentInParent<Rigidbody2D>();

        if (owner != null)
            owner.OnDamagePipeline += TryPreventLethalDamage;
    }

    public override void Off()
    {
        base.Off();

        if (owner != null)
            owner.OnDamagePipeline -= TryPreventLethalDamage;
    }

    private void Update()
    {
        if (!isActive || owner == null)
            return;

        if (cooldownTimer > 0f)
            cooldownTimer -= Time.deltaTime;

        sampleTimer -= Time.deltaTime;
        if (sampleTimer <= 0f)
        {
            sampleTimer = sampleInterval;
            RecordSnapshot();
        }
    }

    private void RecordSnapshot()
    {
        snapshots.Enqueue(new EchoSnapshot
        {
            Position = owner.transform.position,
            Time = Time.time
        });

        while (snapshots.Count > 0
            && Time.time - snapshots.Peek().Time > rewindSeconds)
        {
            snapshots.Dequeue();
        }
    }

    private float TryPreventLethalDamage(float damage)
    {
        if (!isActive || cooldownTimer > 0f || owner == null)
            return damage;

        float effectiveHealthPool =
            owner.CurrentHealthPoints + owner.CurrentShieldPoints;
        if (damage < effectiveHealthPool)
            return damage;

        RewindOwner();
        cooldownTimer = passiveCooldown;
        return 0f;
    }

    private void RewindOwner()
    {
        Vector3 rewindPosition = owner.transform.position;

        if (snapshots.Count > 0)
            rewindPosition = snapshots.Peek().Position;

        if (ownerBody != null)
        {
            ownerBody.linearVelocity = Vector2.zero;
            ownerBody.angularVelocity = 0f;
            ownerBody.position = rewindPosition;
        }

        owner.transform.position = rewindPosition;
        owner.SetHealthPoints(Mathf.Max(
            owner.CurrentHealthPoints,
            owner.MaximumHealthPoints * restoredHealthPercent));
        owner.SetShieldPoints(Mathf.Max(
            owner.CurrentShieldPoints,
            owner.MaximumShieldPoints * restoredShieldPercent));

        PurgeEnemyProjectiles();
        snapshots.Clear();
    }

    private void PurgeEnemyProjectiles()
    {
        ConfigureFilter();

        int count = Physics2D.OverlapCircle(
            owner.transform.position,
            projectilePurgeRadius,
            projectileFilter,
            purgeHits);

        for (int i = 0; i < count; i++)
        {
            EnemyProjectile projectile = purgeHits[i] != null
                ? purgeHits[i].GetComponentInParent<EnemyProjectile>()
                : null;

            if (projectile != null)
                Destroy(projectile.gameObject);
        }
    }

    private void ConfigureFilter()
    {
        projectileFilter = new ContactFilter2D
        {
            useLayerMask = true,
            layerMask = enemyProjectileLayers,
            useTriggers = true
        };
    }

    private void OnDestroy()
    {
        if (owner != null)
            owner.OnDamagePipeline -= TryPreventLethalDamage;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        ConfigureFilter();
    }
#endif
}
