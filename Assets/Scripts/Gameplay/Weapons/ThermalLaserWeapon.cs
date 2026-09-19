using UnityEngine;
using Zenject;

public sealed class ThermalLaserWeapon : ContinuousBeamWeapon
{
    [Inject] private DealDamageManager dealDamageManager;
    [Inject] private EnemyDebuffController enemyDebuffController;

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
            || dealDamageManager == null)
        {
            return false;
        }

        EnemyDamageResult result = dealDamageManager.DealDamage(
            enemy,
            Owner,
            CurrentStats.Damage,
            weaponData.DamageType);
        if (result.DidDamageHull
            && !enemy.isDead
            && enemyDebuffController != null
            && data.HeatDebuffConfig != null)
        {
            enemyDebuffController.Apply(
                enemy,
                data.HeatDebuffConfig,
                data.GetHeatPerHitPercent(Level),
                Owner);
        }

        return true;
    }
}
