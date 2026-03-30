using UnityEngine;

public class HealthRegeneration : MonoBehaviour
{
    private ParentShip parentShip;
    private float healthRegenCooldown;
    float healthRegenRate;
    private float lastDamageTime;

    private bool CanRegenerate => Time.time - lastDamageTime >= healthRegenCooldown;

    private void Awake()
    {
        parentShip = GetComponent<ParentShip>();

        if (parentShip == null)
        {
            return;
        }

        healthRegenCooldown = parentShip.ShipData.shieldRegenCooldown;
        healthRegenRate = parentShip.ShipData.shieldRegenRate;

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

        if (parentShip.IsVisible) return;

        parentShip.HealHealth(healthRegenRate * Time.deltaTime);
    }

    private void OnDestroy()
    {
        if (parentShip != null)
            parentShip.OnDamagePipeline -= OnDamageTaken;
    }
}