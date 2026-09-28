using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;
using Zenject;

public sealed class PrismBeamActiveAbility : ActiveAbility
{
    private const float ChargeDuration = 1f;
    private const float BeamRange = 30f;
    private const float BeamHalfWidth = 0.35f;

    [Inject] private EnemyManager enemyManager;
    [Inject] private DealDamageManager dealDamageManager;

    [Header("Default Meta Contract")]
    [FormerlySerializedAs("defaultBeamDamage")]
    [SerializeField, HideInInspector, Min(0f)]
    private float defaultPlasmaDamage = 150f;
    [FormerlySerializedAs("defaultBeamDamageBonusPercent")]
    [SerializeField, HideInInspector, Min(0f)]
    private float defaultPlasmaDamageBonusPercent = 15f;
    [FormerlySerializedAs("defaultEnergyDamageBonusPercent")]
    [SerializeField, HideInInspector, Min(0f)]
    private float defaultElectricDamageBonusPercent = 10f;

      private PrismShipMetaContract metaContract;
      private Coroutine chargeCoroutine;
      private float abilityDamageMultiplier = 1f;

    protected override void Awake()
    {
        base.Awake();
        if (cooldown <= 0f)
            cooldown = 12f;
    }

      protected override void ApplySpecificShipMetaStats(
          ShipMetaRuntimeStats stats)
    {
        stats.TryGetContract(out metaContract);
      }

      public override void ApplyBattleAbilityDamageMultiplier(
          float damageMultiplier)
      {
          abilityDamageMultiplier = Mathf.Max(1f, damageMultiplier);
      }

    public PrismShipMetaContract CreateDefaultMetaContract()
    {
        var contract = new PrismShipMetaContract();
        contract.Configure(
            defaultPlasmaDamage,
            defaultPlasmaDamageBonusPercent,
            defaultElectricDamageBonusPercent);
        return contract;
    }

    public override bool Activate(ParentShip activationOwner)
    {
        if (activationOwner == null
            || metaContract == null
            || enemyManager == null
            || dealDamageManager == null
            || chargeCoroutine != null)
        {
            return false;
        }

        activationOwner.LockShipSwitching(ChargeDuration);
        chargeCoroutine = StartCoroutine(ChargeAndFire(activationOwner));
        return true;
    }

    private IEnumerator ChargeAndFire(ParentShip activationOwner)
    {
        yield return new WaitForSeconds(ChargeDuration);

        if (activationOwner != null)
            FireBeam(activationOwner);

        chargeCoroutine = null;
    }

    private void FireBeam(ParentShip activationOwner)
    {
        Vector2 origin = activationOwner.transform.position;
        Vector2 direction = activationOwner.transform.up;
        if (direction.sqrMagnitude <= Mathf.Epsilon)
            return;

        direction.Normalize();
        DamageEnemiesOnBeam(activationOwner, origin, direction);

        Material material = activationOwner.TryGetComponent(
            out SpriteRenderer spriteRenderer)
            ? spriteRenderer.sharedMaterial
            : null;
        PrismBeamVisual.Create(
            origin,
            origin + direction * BeamRange,
            material);
    }

    private void DamageEnemiesOnBeam(
        ParentShip activationOwner,
        Vector2 origin,
        Vector2 direction)
    {
        if (enemyManager.enemyList == null)
            return;

        for (int index = enemyManager.enemyList.Count - 1;
             index >= 0;
             index--)
        {
            Enemy enemy = enemyManager.enemyList[index];
            if (enemy == null || enemy.isDead || !enemy.isActiveAndEnabled)
                continue;

            Vector2 offset = enemy.transform.position - (Vector3)origin;
            float forwardDistance = Vector2.Dot(offset, direction);
            if (forwardDistance < 0f || forwardDistance > BeamRange)
                continue;

            float lateralDistance = Mathf.Abs(
                direction.x * offset.y - direction.y * offset.x);
            if (lateralDistance > BeamHalfWidth)
                continue;

            dealDamageManager.DealDamage(
                  enemy,
                  activationOwner,
                  metaContract.PlasmaDamage * abilityDamageMultiplier,
                  EnemyDamageType.Plasma);
        }
    }

    protected override void OnDisable()
    {
        base.OnDisable();

        if (chargeCoroutine == null)
            return;

        StopCoroutine(chargeCoroutine);
        chargeCoroutine = null;
    }
}
