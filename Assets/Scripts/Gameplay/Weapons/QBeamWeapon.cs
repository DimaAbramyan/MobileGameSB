using UnityEngine;
using Zenject;

public sealed class QBeamWeapon : ContinuousBeamWeapon
{
    [Inject] private EnemyDebuffController enemyDebuffController;

    protected override bool TryGetBeamBlockingLayers(
        out LayerMask blockingLayers)
    {
        QBeamData data = weaponData as QBeamData;
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
        QBeamData data = weaponData as QBeamData;
        if (data == null
            || enemyDebuffController == null
            || data.DisintegrationDebuffConfig == null
            || enemy.HasActiveShield)
            return false;

        enemyDebuffController.Apply(
            enemy,
            data.DisintegrationDebuffConfig,
            data.GetChargePerHit(Level),
            Owner);
        return true;
    }
}
