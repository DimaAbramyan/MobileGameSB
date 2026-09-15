using UnityEngine;
using Zenject;

public sealed class ThermalLaserWeapon : ContinuousBeamWeapon
{
    [Inject] private DealDamageManager dealDamageManager;
    [Inject] private EnemyHeatSystem enemyHeatSystem;

    protected override bool TryGetBeamBlockingLayers(
        out LayerMask blockingLayers)
    {
        ThermalLaserData data = weaponData as ThermalLaserData;
        if (data == null)
        {
            blockingLayers = 0;
            return false;
        }

        blockingLayers = data.BeamBlockingLayers;
        return true;
    }

    protected override Transform GetBeamTransform()
    {
        return transform;
    }

    protected override bool ApplyBeamEffect(Enemy enemy)
    {
        ThermalLaserData data = weaponData as ThermalLaserData;
        if (data == null
            || enemyHeatSystem == null
            || dealDamageManager == null)
        {
            return false;
        }

        EnemyDamageResult result = dealDamageManager.DealDamage(
            enemy,
            Owner,
            CurrentStats.Damage,
            weaponData.DamageType);
        if (result.DidDamageHull && !enemy.isDead)
        {
            enemyHeatSystem.ApplyHeat(
                enemy,
                data.GetHeatPerHitPercent(Level),
                data.CreateHeatProfile(Owner));
        }

        return true;
    }
}
