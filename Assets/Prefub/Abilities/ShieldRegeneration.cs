using UnityEngine;

public class ShieldRegeneration : MonoBehaviour
{
    private ParentShip parentShip;
    private float shieldRegenCooldown;
    private float shieldRegenRate;
    private float lastDamageTime;
    private bool isRegenerating;

    private bool CanRegenerate => Time.time - lastDamageTime >= shieldRegenCooldown;

    private void Awake()
    {
        parentShip = GetComponent<ParentShip>();

        if (parentShip == null)
        {
            Debug.LogError("ShieldRegeneration requires ParentShip component!");
            return;
        }

        shieldRegenCooldown = parentShip.ShipData.ShieldRegenCooldown;
        shieldRegenRate = parentShip.ShipData.ShieldRegenRatePercent;

        parentShip.OnDamagePipeline += OnDamageTaken;
    }

    private float OnDamageTaken(float damage)
    {
        lastDamageTime = Time.time;
        isRegenerating = false;
        return damage;
    }

    private void Update()
    {
        if (parentShip == null)
            return;

        if (!CanRegenerate)
        {
            isRegenerating = false;
            return;
        }

        if (shieldRegenRate <= 0f
            || parentShip.CurrentShieldPoints >= parentShip.MaximumShieldPoints)
        {
            isRegenerating = false;
            return;
        }

        if (!isRegenerating)
        {
            isRegenerating = true;
            parentShip.NotifyShieldRegenerationStarted();
        }

        float restoredShield = parentShip.MaximumShieldPoints
            * shieldRegenRate
            / 100f
            * Time.deltaTime;
        parentShip.HealShield(restoredShield);

        if (parentShip.CurrentShieldPoints < parentShip.MaximumShieldPoints)
            return;

        isRegenerating = false;
        parentShip.NotifyShieldFullyRegenerated();
    }

    private void OnDestroy()
    {
        if (parentShip != null)
            parentShip.OnDamagePipeline -= OnDamageTaken;
    }
}
