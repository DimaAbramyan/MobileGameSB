using UnityEngine;

public class ShieldRegeneration : MonoBehaviour
{
    private ParentShip parentShip;
    private float shieldRegenCooldown;
    private float shieldRegenRate;
    private float lastDamageTime;

    private bool CanRegenerate => Time.time - lastDamageTime >= shieldRegenCooldown;

    private void Awake()
    {
        parentShip = GetComponent<ParentShip>();

        if (parentShip == null)
        {
            Debug.LogError("ShieldRegeneration requires ParentShip component!");
            return;
        }

        shieldRegenCooldown = parentShip.ShipData.shieldRegenCooldown;
        shieldRegenRate = parentShip.ShipData.shieldRegenRate;

        parentShip.OnDamagePipeline += OnDamageTaken;
    }

    private float OnDamageTaken(float damage)
    {
        lastDamageTime = Time.time;
        return damage;
    }

    private void Update()
    {
        if (parentShip == null) return;

        if (!CanRegenerate) return;

        if (parentShip.CurrentShieldPoints >= parentShip.ShipData.maximumShieldPoints)
            return;

        parentShip.HealShield(shieldRegenRate*Time.deltaTime);
    }

    private void OnDestroy()
    {
        if (parentShip != null)
            parentShip.OnDamagePipeline -= OnDamageTaken;
    }
}