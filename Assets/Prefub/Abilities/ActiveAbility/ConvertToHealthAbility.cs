using System.Collections;
using UnityEngine;

public class ConvertToHealthAbility : ActiveAbility
{
    private ParentShip ship;


    [SerializeField]
    private float duration = 4f;

    private Coroutine activeRoutine;

    public void Init(ParentShip ship)
    {
        this.ship = ship;
    }

    public override bool Activate(ParentShip owner)
    {
        if (ship == null)
            ship = owner;

        if (activeRoutine != null)
            ship.StopCoroutine(activeRoutine);

        activeRoutine = ship.StartCoroutine(AbilityRoutine());

        return true;
    }

    IEnumerator AbilityRoutine()
    {
        Debug.Log("Õ¿◊¿ÀŒ");
        ship.OnDamagePipeline += ConvertDamageToHeal;

        yield return new WaitForSeconds(duration);

        ship.OnDamagePipeline -= ConvertDamageToHeal;

        Debug.Log(" ŒÕ≈÷");
        activeRoutine = null;
    }

    float ConvertDamageToHeal(float damage)
    {
        if (damage <= 0)
            return damage;


        ship.HealHealth(damage);

        return 0f;
    }

    private void OnDestroy()
    {
        if (ship != null)
            ship.OnDamagePipeline -= ConvertDamageToHeal;
    }
}