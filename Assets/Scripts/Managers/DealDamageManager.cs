using System.Collections.Generic;
using UnityEngine;

public class DealDamageManager
{
    [Zenject.Inject] private EnemyDebuffController enemyDebuffController;

    public EnemyDamageResult DealDamage(
        iDamagable target,
        Projectile projectile)
    {
        if (projectile == null)
            return EnemyDamageResult.None;

        EnemyDamageResult result = DealDamage(
            target,
            projectile.Owner,
            projectile.GetDamage(),
            projectile.DamageType);

        if (result.DidDamageHull
            && target is Enemy enemy
            && enemyDebuffController != null)
        {
            IReadOnlyList<EnemyDebuffApplication> debuffs =
                projectile.EnemyDebuffs;
            if (debuffs != null)
            {
                for (int index = 0; index < debuffs.Count; index++)
                {
                    enemyDebuffController.Apply(
                        enemy,
                        debuffs[index],
                        projectile.Owner);
                }
            }
        }

        return result;
    }

    public EnemyDamageResult DealDamage(
        iDamagable target,
        ParentShip owner,
        float damage)
    {
        return DealDamage(target, owner, damage, EnemyDamageType.Radiation);
    }

    public EnemyDamageResult DealDamage(
        iDamagable target,
        ParentShip owner,
        float damage,
        EnemyDamageType damageType)
    {
        return DealDamage(target, owner, damage, damageType, false);
    }

    public EnemyDamageResult DealDamage(
        iDamagable target,
        ParentShip owner,
        float damage,
        EnemyDamageType damageType,
        bool bypassesEnemyShield)
    {
        if (target == null)
            return EnemyDamageResult.None;

        if (owner?.PassiveAbility is IOutgoingDamageModifier modifier)
            damage = modifier.ModifyOutgoingDamage(damageType, damage);

        if (damage <= 0f)
            return EnemyDamageResult.None;

        if (target is Enemy enemy)
        {
            EnemyDamageResult result = enemy.TakeDamageWithType(
                damage,
                damageType,
                bypassesEnemyShield);
            if (owner != null && result.DidDamageHull)
                owner.NotifyDamageDealt(result.HullDamage);

            return result;
        }

        target.TakeDamage(damage);
        if (owner != null)
            owner.NotifyDamageDealt(damage);

        return EnemyDamageResult.None;
    }

    public EnemyDamageResult DealDamage(
        iDamagable target,
        ParentShip owner,
        float damage,
        bool bypassesEnemyShield)
    {
        return DealDamage(
            target,
            owner,
            damage,
            EnemyDamageType.Radiation,
            bypassesEnemyShield);
    }
}
