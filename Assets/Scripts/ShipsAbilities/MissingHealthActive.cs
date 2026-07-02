using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MissingHealthActive : ActiveAbility
{
    public float healPercent = 0.2f;
    public float duration = 5f;

    private System.Action<float> handler;

    public override bool Activate(ParentShip owner)
    {
        if (handler != null)
            return false;

        // подписка на событие нанесения урона
        handler = (damage) => HealFromDamage(owner, damage);
        owner.OnDamageDealt += handler;

        Debug.Log("Buff ON");

        // запускаем таймер баффа
        StartCoroutine(BuffRoutine(owner));

        return true;
    }

    private IEnumerator BuffRoutine(ParentShip owner)
    {
        yield return new WaitForSeconds(duration);

        owner.OnDamageDealt -= handler;
        handler = null;

        Debug.Log("Buff OFF");
    }

    private void HealFromDamage(ParentShip owner, float damage)
    {
        float heal = damage * healPercent;
        owner.HealHealth(heal);
    }
}
