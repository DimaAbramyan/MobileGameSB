using UnityEngine;
using Zenject;

public sealed class LaserWeapon : ContinuousBeamWeapon
{
    [Header("Beam Collision")]
    [SerializeField] private LayerMask beamBlockingLayers = ~0;

    [Inject] private DealDamageManager dealDamageManager;

    protected override bool TryGetBeamBlockingLayers(
        out LayerMask blockingLayers)
    {
        blockingLayers = beamBlockingLayers;
        return true;
    }

    protected override Transform GetBeamTransform()
    {
        return transform;
    }

    protected override bool ApplyBeamEffect(Enemy enemy)
    {
        if (enemy == null || enemy.isDead || dealDamageManager == null)
            return false;

        dealDamageManager.DealDamage(
            enemy,
            Owner,
            CurrentStats.Damage,
            weaponData.DamageType);
        return true;
    }
}
