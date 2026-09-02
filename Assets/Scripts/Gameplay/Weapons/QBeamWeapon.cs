using UnityEngine;
using Zenject;

public sealed class QBeamWeapon : ContinuousBeamWeapon
{
    [Inject] private EnemyDisintegrationSystem enemyDisintegrationSystem;

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

    protected override bool ApplyBeamEffect(Enemy enemy)
    {
        QBeamData data = weaponData as QBeamData;
        if (data == null || enemyDisintegrationSystem == null)
            return false;

        enemyDisintegrationSystem.ApplyCharge(
            enemy,
            data.GetChargePerHit(Level),
            data.CreateDisintegrationProfile());
        return true;
    }
}
