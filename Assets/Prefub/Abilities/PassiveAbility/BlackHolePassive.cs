using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

public class BlackHolePassive : PassiveAbility
{
    List<EnemyProjectile> enemyProjectiles;
    float maxLenght = 3;
    private float minimumProjectileSpeedMultiplier = 0.1f;

    public override void Init(ParentShip ship)
    {
        owner = ship;
    }

    public override void On()
    {
        if (owner == null)
            owner = GetComponentInParent<ParentShip>();
        base.On();
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
        field = new EnemyProjectileSlowField
        {
            Center = new float2(center.x, center.y),
            Radius = Mathf.Max(0.01f, maxLenght),
            MinimumSpeedMultiplier = Mathf.Clamp(
                minimumProjectileSpeedMultiplier, 0.01f, 1f)
        };
        return true;
    }

    public override void ApplyShipMetaStats(ShipMetaRuntimeStats stats)
    {
        if (!stats.TryGetContract(out BlackHoleShipMetaContract contract))
            return;

        maxLenght = contract.ProjectileSlowRadius;
        minimumProjectileSpeedMultiplier =
            contract.MinimumProjectileSpeedMultiplier;
    }
    public void Awake()
    {
        enemyProjectiles = new List<EnemyProjectile>();
    }
    public override void Off()
    {
        base.Off();
        if (enemyProjectiles != null && enemyProjectiles.Count > 0 )
        foreach ( var EnemyProjectile  in enemyProjectiles)
        {
            if (EnemyProjectile != null)
                EnemyProjectile.SetMultiplier(1);
        }
        enemyProjectiles?.Clear();
    }

    private void OnDisable()
    {
        Off();
    }
    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (!isActive)
            return;
        EnemyProjectile enemyProjectile = collision.gameObject.GetComponent<EnemyProjectile>();
        if (enemyProjectile != null && !enemyProjectiles.Contains(enemyProjectile))
        enemyProjectiles.Add(enemyProjectile);
    }
    private void OnTriggerStay2D(Collider2D collision)
    {
        if (!isActive)
            return;
        EnemyProjectile enemyProjectile = collision.GetComponent<EnemyProjectile>();

        if (enemyProjectile != null)
        {
            enemyProjectile.SetMultiplier(
                CountMultiplierPerDistance(transform.position, collision.transform.position));
        }
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        EnemyProjectile enemyProjectile = collision.GetComponent<EnemyProjectile>();
        if (enemyProjectile!=null && enemyProjectiles.Contains(enemyProjectile))
        enemyProjectiles.Remove(enemyProjectile);
        if (enemyProjectile != null)
            enemyProjectile.SetMultiplier(1);
    }
    private float CountMultiplierPerDistance(Vector2 from, Vector2 to)
    {
        float distance = Vector2.Distance(from, to);

        float normalized = Mathf.Clamp01(distance / maxLenght);

        return Mathf.Max(minimumProjectileSpeedMultiplier, normalized);
    }
}
