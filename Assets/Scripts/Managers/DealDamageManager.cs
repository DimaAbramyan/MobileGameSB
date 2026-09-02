using UnityEngine;

public class DealDamageManager
{
    public void DealDamage(iDamagable target, Projectile projectile)
    {
        if (projectile == null)
            return;

        DealDamage(
            target,
            projectile.Owner,
            projectile.GetDamage(),
            projectile.DamageType);
    }

    public void DealDamage(iDamagable target, ParentShip owner, float damage)
    {
        DealDamage(target, owner, damage, EnemyDamageType.Radiation);
    }

    public void DealDamage(
        iDamagable target,
        ParentShip owner,
        float damage,
        EnemyDamageType damageType)
    {
        DealDamage(target, owner, damage, damageType, false);
    }

    public void DealDamage(
        iDamagable target,
        ParentShip owner,
        float damage,
        EnemyDamageType damageType,
        bool bypassesEnemyShield)
    {
        if (target == null)
            return;

        if (owner != null)
            owner.NotifyDamageDealt(damage);

        if (target is Enemy enemy)
            enemy.TakeDamageWithType(damage, damageType, bypassesEnemyShield);
        else
            target.TakeDamage(damage);
    }

    public void DealDamage(
        iDamagable target,
        ParentShip owner,
        float damage,
        bool bypassesEnemyShield)
    {
        DealDamage(
            target,
            owner,
            damage,
            EnemyDamageType.Radiation,
            bypassesEnemyShield);
    }
}
