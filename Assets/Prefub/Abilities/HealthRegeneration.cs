using UnityEngine;

public class HealthRegeneration : MonoBehaviour
{
    private ParentShip parentShip;
    private float healthRegenCooldown;
    float healthRegenRate;
    private float lastDamageTime;
    private bool isRegenerating;

    private bool CanRegenerate => Time.time - lastDamageTime >= healthRegenCooldown;

    private void Awake()
    {
        parentShip = GetComponent<ParentShip>();

        if (parentShip == null)
        {
            return;
        }

        healthRegenCooldown = parentShip.ShipData.HealthRegenCooldown;
        healthRegenRate = parentShip.ShipData.HealthRegenRatePercent;
        lastDamageTime = float.NegativeInfinity;

        parentShip.OnDamagePipeline += OnDamageTaken;
    }

    private float OnDamageTaken(float damage)
    {
        lastDamageTime = Time.time;
        isRegenerating = false;
        UpdateCooldownProgress();
        return damage;
    }

    private void Update()
    {
        if (parentShip == null)
            return;

        UpdateCooldownProgress();

        if (!CanRegenerate)
        {
            isRegenerating = false;
            return;
        }

        if (parentShip.IsVisible)
        {
            isRegenerating = false;
            return;
        }

        if (healthRegenRate <= 0f
            || parentShip.CurrentHealthPoints >= parentShip.MaximumHealthPoints)
        {
            isRegenerating = false;
            return;
        }

        if (!isRegenerating)
        {
            isRegenerating = true;
            parentShip.NotifyHealthRegenerationStarted();
        }

        float restoredHealth = parentShip.MaximumHealthPoints
            * healthRegenRate
            / 100f
            * Time.deltaTime;
        parentShip.HealHealth(restoredHealth);

        if (parentShip.CurrentHealthPoints < parentShip.MaximumHealthPoints)
            return;

        isRegenerating = false;
        parentShip.NotifyHealthFullyRegenerated();
    }

    private void OnDestroy()
    {
        if (parentShip != null)
            parentShip.OnDamagePipeline -= OnDamageTaken;
    }

    private void UpdateCooldownProgress()
    {
        if (parentShip == null)
            return;

        float progress = healthRegenCooldown <= 0f
            ? 1f
            : (Time.time - lastDamageTime) / healthRegenCooldown;
        parentShip.SetHealthRegenerationCooldownProgress(progress);
    }
}
