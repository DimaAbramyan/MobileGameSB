using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Zenject;

public class BlackHoleActive : ActiveAbility
{
    [Inject] private EnemyProjectileCollisionRegistry projectileCollisionRegistry;

    [SerializeField]
    BlackHolePrefab blackHole;
    [SerializeField, HideInInspector] float duration;
      private float metaDuration = -1f;
      private float metaDamage = -1f;
      private float abilityDamageMultiplier = 1f;

      protected override void ApplySpecificShipMetaStats(
        ShipMetaRuntimeStats stats)
    {
        if (!stats.TryGetContract(out BlackHoleShipMetaContract contract))
            return;

        metaDuration = contract.Duration;
        metaDamage = contract.Damage;
      }

      public override void ApplyBattleAbilityDamageMultiplier(
          float damageMultiplier)
      {
          abilityDamageMultiplier = Mathf.Max(1f, damageMultiplier);
      }

    public override bool Activate(ParentShip owner)
    {
        float activeDuration = metaDuration > 0f ? metaDuration : duration;
        PlayerEffectController effectController = owner != null
            ? owner.GetComponentInParent<PlayerController>()?.Effects
            : null;
        effectController?.ApplyOrRefresh(
            PlayerEffectType.BlackHoleActive,
            PlayerEffectPolarity.Positive,
            PlayerEffectScope.SingleShip,
            owner,
            activeDuration);

        owner?.LockShipSwitching(activeDuration);
        owner?.SetIntangibleForSeconds(activeDuration);

        BlackHolePrefab currentBlackHole = Instantiate(blackHole, transform);
          currentBlackHole.Init(
              activeDuration,
              metaDamage,
              owner,
              abilityDamageMultiplier,
              projectileCollisionRegistry);
        audioManager.PlaySound(audioDatabase.blackHole, transform.position);
        GetComponent<WeaponController>().StopShootingForSeconds(activeDuration);
        return true;
    }
}
