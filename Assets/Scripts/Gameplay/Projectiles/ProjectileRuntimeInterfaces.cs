public interface IProjectileRuntimeLifecycleHandler
{
    void Initialize(Projectile projectile, ProjectileRuntimeConfig runtimeConfig);
    bool TryHandleLifetimeExpired(Projectile projectile);
    void ResetProjectile();
}

public interface IProjectileDamageReceiver
{
    bool CanReceiveProjectileDamage(Projectile projectile);
    void ReceiveProjectileDamage(Projectile projectile, float damage);
}

/// <summary>
/// Optional bridge for legacy runtime objects that can be hit by an Entity player projectile.
/// </summary>
public interface IEntityProjectileDamageReceiver
{
    bool CanReceiveEntityProjectileDamage(
        ParentShip owner,
        EnemyDamageType damageType);
    void ReceiveEntityProjectileDamage(
        ParentShip owner,
        EnemyDamageType damageType,
        float damage);
}
