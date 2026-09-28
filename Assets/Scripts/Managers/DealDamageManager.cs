using System;
using System.Collections.Generic;
using UnityEngine;

public class DealDamageManager
{
    [Zenject.Inject] private EnemyDebuffController enemyDebuffController;

    public event Action<DamageNumberRequest> DamageApplied;

    public EnemyDamageResult DealDamage(
        iDamagable target,
        Projectile projectile)
    {
        if (projectile == null)
            return EnemyDamageResult.None;

        if (target is IProjectileDamageReceiver receiver)
        {
            if (!receiver.CanReceiveProjectileDamage(projectile))
                return EnemyDamageResult.None;

            float damage = projectile.GetDamage();
            if (projectile.Owner?.PassiveAbility
                is IOutgoingDamageModifier modifier)
            {
                damage = modifier.ModifyOutgoingDamage(
                    projectile.DamageType,
                    damage);
            }

            receiver.ReceiveProjectileDamage(projectile, damage);
            return EnemyDamageResult.None;
        }

        EnemyDamageResult result = DealDamageInternal(
            target,
            projectile.Owner,
            projectile.GetDamage(),
            projectile.DamageType,
            false,
            projectile.transform.position);

        ApplyEnemyDebuffs(
            result,
            target,
            projectile.Owner,
            projectile.EnemyDebuffs);

        return result;
    }

    public EnemyDamageResult DealDamage(
        iDamagable target,
        ParentShip owner,
        float damage)
    {
        return DealDamage(target, owner, damage, EnemyDamageType.Resonance);
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
        return DealDamageInternal(
            target,
            owner,
            damage,
            damageType,
            bypassesEnemyShield,
            null);
    }

    public EnemyDamageResult DealDamage(
        iDamagable target,
        ParentShip owner,
        float damage,
        EnemyDamageType damageType,
        bool bypassesEnemyShield,
        Vector3 impactPosition)
    {
        return DealDamageInternal(
            target,
            owner,
            damage,
            damageType,
            bypassesEnemyShield,
            impactPosition);
    }

    /// <summary>
    /// Used by the Entity-projectile bridge after it resolves its compact
    /// DebuffSetId back to the designer-configured applications.
    /// </summary>
    public EnemyDamageResult DealDamage(
        iDamagable target,
        ParentShip owner,
        float damage,
        EnemyDamageType damageType,
        bool bypassesEnemyShield,
        Vector3 impactPosition,
        IReadOnlyList<EnemyDebuffApplication> debuffs)
    {
        EnemyDamageResult result = DealDamageInternal(
            target,
            owner,
            damage,
            damageType,
            bypassesEnemyShield,
            impactPosition);
        ApplyEnemyDebuffs(result, target, owner, debuffs);
        return result;
    }

    private EnemyDamageResult DealDamageInternal(
        iDamagable target,
        ParentShip owner,
        float damage,
        EnemyDamageType damageType,
        bool bypassesEnemyShield,
        Vector3? impactPosition)
    {
        if (target == null)
            return EnemyDamageResult.None;

        float damageBeforeModifier = damage;
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

            if (result.DidDamage)
            {
                        DamageApplied?.Invoke(new DamageNumberRequest(
                            enemy.DamageNumberCenter,
                            result.TotalDamage,
                            CalculateDamageModifierPercent(
                                damageBeforeModifier,
                                result.TotalDamage),
                            enemy.GetInstanceID(),
                            enemy));
            }

            return result;
        }

        target.TakeDamage(damage);
        if (owner != null)
            owner.NotifyDamageDealt(damage);

        return EnemyDamageResult.None;
    }

    private static float CalculateDamageModifierPercent(
        float originalDamage,
        float actualDamage)
    {
        if (originalDamage <= Mathf.Epsilon)
            return 0f;

        return (actualDamage / originalDamage - 1f) * 100f;
    }

    private void ApplyEnemyDebuffs(
        EnemyDamageResult result,
        iDamagable target,
        ParentShip owner,
        IReadOnlyList<EnemyDebuffApplication> debuffs)
    {
        if (!result.DidDamageHull
            || target is not Enemy enemy
            || enemyDebuffController == null
            || debuffs == null)
        {
            return;
        }

        for (int index = 0; index < debuffs.Count; index++)
            enemyDebuffController.Apply(enemy, debuffs[index], owner);
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
            EnemyDamageType.Resonance,
            bypassesEnemyShield);
    }
}
