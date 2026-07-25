using System.Collections;
using UnityEngine;

public class ConvertToHealthAbility : ActiveAbility
{
    private ParentShip ship;

    [SerializeField]
    private float duration = 4f;

    private Coroutine activeRoutine;
    private bool autoActivationSubscribed;
    private bool conversionSubscribed;

    protected override void Awake()
    {
        base.Awake();

        if (owner != null)
            Init(owner);
    }

    public void Init(ParentShip ship)
    {
        this.ship = ship;
        SubscribeAutoActivation();
    }

    public override bool Activate(ParentShip owner)
    {
        if (ship == null && owner != null)
            Init(owner);

        if (activeRoutine != null)
            ship.StopCoroutine(activeRoutine);

        activeRoutine = ship.StartCoroutine(AbilityRoutine());

        return true;
    }

    IEnumerator AbilityRoutine()
    {
        Debug.Log("НАЧАЛО");
        SubscribeConversion();

        yield return new WaitForSeconds(duration);

        UnsubscribeConversion();

        Debug.Log("КОНЕЦ");
        activeRoutine = null;
    }

    float TryAutoActivateOnLethalDamage(float damage)
    {
        if (damage <= 0 || ship == null || activeRoutine != null || cooldownTimer > 0)
            return damage;

        if (!IsLethalDamage(damage))
            return damage;

        activeRoutine = ship.StartCoroutine(AbilityRoutine());
        StartCooldown();

        return ConvertDamageToHeal(damage);
    }

    bool IsLethalDamage(float damage)
    {
        float health = Mathf.Max(0f, ship.CurrentHealthPoints);
        float shield = Mathf.Max(0f, ship.CurrentShieldPoints);
        return damage >= health + shield;
    }

    float ConvertDamageToHeal(float damage)
    {
        if (damage <= 0)
            return damage;

        ship.HealHealth(damage);

        return 0f;
    }

    void SubscribeAutoActivation()
    {
        if (ship == null || autoActivationSubscribed)
            return;

        ship.OnDamagePipeline += TryAutoActivateOnLethalDamage;
        autoActivationSubscribed = true;
    }

    void SubscribeConversion()
    {
        if (ship == null || conversionSubscribed)
            return;

        ship.OnDamagePipeline += ConvertDamageToHeal;
        conversionSubscribed = true;
    }

    void UnsubscribeConversion()
    {
        if (ship == null || !conversionSubscribed)
            return;

        ship.OnDamagePipeline -= ConvertDamageToHeal;
        conversionSubscribed = false;
    }

    private void OnDestroy()
    {
        if (ship == null)
            return;

        if (autoActivationSubscribed)
            ship.OnDamagePipeline -= TryAutoActivateOnLethalDamage;

        UnsubscribeConversion();
    }
}
