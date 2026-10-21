using UnityEngine;

public class HealthRegeneration : MonoBehaviour
{
    private ParentShip parentShip;
    private float healthRegenCooldown;
    // Keep the existing zero-rate switch for configurations with regeneration disabled.
    private bool regenerationEnabled;
    // Restarted by health damage or a completed recovery step.
    private float lastDamageTime;
    private float pauseStartedAt;
    private bool isPaused;

    private float ElapsedTime => (isPaused ? pauseStartedAt : Time.time) - lastDamageTime;
    private bool CanRegenerate => ElapsedTime >= healthRegenCooldown;

    private void Awake()
    {
        parentShip = GetComponent<ParentShip>();

        if (parentShip == null)
        {
            return;
        }

        healthRegenCooldown = parentShip.ShipData.HealthRegenCooldown;
        regenerationEnabled = parentShip.ShipData.HealthRegenRatePercent > 0f;
        lastDamageTime = Time.time;
        SetPaused(parentShip.IsVisible || parentShip.IsDestroyed);

        parentShip.OnHealthDamageTaken += OnHealthDamageTaken;
    }

    private void OnHealthDamageTaken(float damage)
    {
        RestartCooldown();
    }

    public void RestartCooldown()
    {
        lastDamageTime = Time.time;
        if (isPaused)
            pauseStartedAt = Time.time;
        UpdateCooldownProgress();
    }

    public void SetPaused(bool paused)
    {
        if (isPaused == paused)
            return;
        if (paused)
            pauseStartedAt = Time.time;
        else
            lastDamageTime += Time.time - pauseStartedAt;
        isPaused = paused;
        UpdateCooldownProgress();
    }

    private void OnDisable()
    {
        SetPaused(true);
    }

    private void OnEnable()
    {
        if (parentShip != null)
            SetPaused(parentShip.IsVisible || parentShip.IsDestroyed || parentShip.CurrentHealthPoints <= 0f);
    }

    private void Update()
    {
        if (parentShip == null)
            return;

        SetPaused(parentShip.IsVisible || parentShip.IsDestroyed || parentShip.CurrentHealthPoints <= 0f);
        if (isPaused)
            return;

        UpdateCooldownProgress();

        if (!CanRegenerate)
            return;

        if (!regenerationEnabled
            || parentShip.CurrentHealthPoints >= parentShip.MaximumHealthPoints)
            return;

        parentShip.NotifyHealthRegenerationStarted();
        parentShip.HealHealth(1f);
        RestartCooldown();

        if (parentShip.CurrentHealthPoints < parentShip.MaximumHealthPoints)
            return;

        parentShip.NotifyHealthFullyRegenerated();
    }

    private void OnDestroy()
    {
        if (parentShip != null)
            parentShip.OnHealthDamageTaken -= OnHealthDamageTaken;
    }

    private void UpdateCooldownProgress()
    {
        if (parentShip == null)
            return;

        float progress = healthRegenCooldown <= 0f
            ? 1f
            : ElapsedTime / healthRegenCooldown;
        parentShip.SetHealthRegenerationCooldownProgress(progress);
    }
}
