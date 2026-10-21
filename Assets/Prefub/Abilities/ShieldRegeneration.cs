using UnityEngine;

public class ShieldRegeneration : MonoBehaviour
{
    private ParentShip parentShip;
    private float shieldRegenCooldown;
    // Keep the existing zero-rate switch for configurations with regeneration disabled.
    private bool regenerationEnabled;
    // Restarted by either incoming damage or a completed recovery step.
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

        shieldRegenCooldown = parentShip.ShipData.ShieldRegenCooldown;
        regenerationEnabled = parentShip.ShipData.ShieldRegenRatePercent > 0f;
        lastDamageTime = Time.time;

        parentShip.OnDamagePipeline += OnDamageTaken;
    }

    private float OnDamageTaken(float damage)
    {
        RestartCooldown();
        return damage;
    }

    public void RestartCooldown()
    {
        lastDamageTime = Time.time;
        UpdateCooldownProgress();
    }

    private void Update()
    {
        if (parentShip == null)
            return;

        if (parentShip.IsDestroyed || parentShip.CurrentHealthPoints <= 0f)
            return;

        UpdateCooldownProgress();

        if (!CanRegenerate)
            return;

        if (!regenerationEnabled
            || parentShip.CurrentShieldPoints >= parentShip.MaximumShieldPoints)
            return;

        parentShip.NotifyShieldRegenerationStarted();
        parentShip.HealShield(1f);
        lastDamageTime = Time.time;
        UpdateCooldownProgress();

        if (parentShip.CurrentShieldPoints < parentShip.MaximumShieldPoints)
            return;

        parentShip.NotifyShieldFullyRegenerated();
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

        float progress = shieldRegenCooldown <= 0f
            ? 1f
            : (Time.time - lastDamageTime) / shieldRegenCooldown;
        parentShip.SetShieldRegenerationCooldownProgress(progress);
    }
}
