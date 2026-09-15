using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MissingHealthActive : ActiveAbility
{
    [HideInInspector] public float healPercent = 0.2f;
    [HideInInspector] public float duration = 5f;

    private System.Action<float> handler;
    private Coroutine buffRoutine;
    private ParentShip activeOwner;

    protected override void ApplySpecificShipMetaStats(
        ShipMetaRuntimeStats stats)
    {
        if (!stats.TryGetContract(out HuskarShipMetaContract contract))
            return;

        healPercent = contract.HealFromDamagePercent;
        duration = contract.HealDuration;
    }

    public override bool Activate(ParentShip owner)
    {
        if (handler != null || owner == null)
            return false;

        activeOwner = owner;
        owner.GetComponentInParent<PlayerController>()?.Effects.ApplyOrRefresh(
            PlayerEffectType.HuskarVampirism,
            PlayerEffectPolarity.Positive,
            PlayerEffectScope.SingleShip,
            owner,
            0f,
            PlayerEffectEndCondition.OwnerDied
                | PlayerEffectEndCondition.AbilityDeactivated,
            this);

        handler = (damage) => HealFromDamage(owner, damage);
        owner.OnDamageDealt += handler;

        Debug.Log("Buff ON");

        buffRoutine = StartCoroutine(BuffRoutine());

        return true;
    }

    private IEnumerator BuffRoutine()
    {
        yield return new WaitForSeconds(duration);
        buffRoutine = null;
        EndVampirism();
    }

    private void EndVampirism()
    {
        if (activeOwner != null && handler != null)
            activeOwner.OnDamageDealt -= handler;

        handler = null;
        activeOwner = null;
        NotifyAbilityDeactivated();

        Debug.Log("Buff OFF");
    }

    private void HealFromDamage(ParentShip owner, float damage)
    {
        float heal = damage * healPercent;
        owner.HealHealth(heal);
    }

    protected override void OnDisable()
    {
        base.OnDisable();

        if (buffRoutine != null)
        {
            StopCoroutine(buffRoutine);
            buffRoutine = null;
        }

        EndVampirism();
    }
}
