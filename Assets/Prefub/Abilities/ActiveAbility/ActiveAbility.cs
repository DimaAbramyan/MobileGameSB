using UnityEngine;
using Zenject;

public enum UltimateAbilityMode
{
    Active,
    Toggle,
    Charges
}

public abstract class ActiveAbility : MonoBehaviour
{
    [Inject] protected AudioDatabase audioDatabase;
    [Inject] protected SoundManager audioManager;

    [SerializeField]
    private UltimateAbilityMode abilityMode = UltimateAbilityMode.Active;

    [SerializeField] protected float cooldown;
    protected float cooldownTimer;

    [Header("Toggle")]
    [SerializeField, Min(0.01f)]
    private float toggleMaximumTime = 3f;

    [SerializeField, Min(0.01f)]
    private float toggleTimeCostPerSecond = 1f;

    [SerializeField, Min(0f)]
    private float toggleRechargeStartTime;

    [SerializeField, Min(0.01f)]
    private float toggleRechargeDuration = 3f;

    [SerializeField]
    private float toggleTimeRemaining = 3f;

    [Header("Charges")]
    [SerializeField, Min(1)]
    private int maxCharges = 1;

    [SerializeField]
    private int currentCharges = 1;

    [SerializeField]
    protected ParentShip owner;

    private ParentShip activeToggleOwner;
    private bool toggleIsActive;
    private float toggleRechargeElapsed;

    public UltimateAbilityMode AbilityMode => abilityMode;
    public float CooldownDuration => Mathf.Max(0f, cooldown);
    public float CooldownRemaining => Mathf.Max(0f, cooldownTimer);
    public bool IsCoolingDown => cooldownTimer > 0f;
    public float CooldownProgress01 =>
        CooldownDuration <= 0f
            ? 1f
            : Mathf.Clamp01(1f - CooldownRemaining / CooldownDuration);
    public float CooldownRemaining01 =>
        CooldownDuration <= 0f
            ? 0f
            : Mathf.Clamp01(CooldownRemaining / CooldownDuration);
    public float ToggleTimeRemaining => Mathf.Max(0f, toggleTimeRemaining);
    public float ToggleMaximumTime => Mathf.Max(0.01f, toggleMaximumTime);
    public float ToggleTimeRemaining01 =>
        Mathf.Clamp01(ToggleTimeRemaining / ToggleMaximumTime);
    public bool IsToggleActive => toggleIsActive;
    public int CurrentCharges => Mathf.Clamp(currentCharges, 0, maxCharges);
    public int MaxCharges => Mathf.Max(1, maxCharges);
    public float ChargeRechargeProgress01 =>
        CooldownDuration <= 0f || currentCharges >= maxCharges
            ? 1f
            : Mathf.Clamp01(1f - CooldownRemaining / CooldownDuration);
    public float ChargeFill01
    {
        get
        {
            if (MaxCharges <= 0)
                return 0f;

            float restoredCharges = CurrentCharges;
            if (CurrentCharges < MaxCharges)
                restoredCharges += ChargeRechargeProgress01;

            return Mathf.Clamp01(restoredCharges / MaxCharges);
        }
    }

    protected virtual void Awake()
    {
        owner = GetComponent<ParentShip>();
        ValidateRuntimeValues();
    }

    public abstract bool Activate(ParentShip owner);

    protected virtual bool StartsCooldownOnActivation => true;

    public void TryActivate(ParentShip owner)
    {
        ValidateRuntimeValues();

        switch (abilityMode)
        {
            case UltimateAbilityMode.Toggle:
                TryActivateToggle(owner);
                break;
            case UltimateAbilityMode.Charges:
                TryActivateCharge(owner);
                break;
            default:
                TryActivateActive(owner);
                break;
        }
    }

    public virtual void Release(ParentShip owner) { }

    public void TryRelease(ParentShip owner)
    {
        if (abilityMode != UltimateAbilityMode.Toggle || !toggleIsActive)
        {
            Release(owner);
            return;
        }

        StopToggle(owner, true);
    }

    protected void StartCooldown()
    {
        cooldownTimer = Mathf.Max(0f, cooldown);

        if (abilityMode != UltimateAbilityMode.Toggle || toggleIsActive)
            return;

        toggleRechargeElapsed = 0f;
    }

    protected virtual void Update()
    {
        float deltaTime = Time.deltaTime;

        if (abilityMode == UltimateAbilityMode.Charges)
        {
            UpdateCharges(deltaTime);
            return;
        }

        if (cooldownTimer > 0f)
        {
            cooldownTimer = Mathf.Max(0f, cooldownTimer - deltaTime);

        }

        if (abilityMode == UltimateAbilityMode.Toggle && !toggleIsActive)
            UpdateToggleRecharge(deltaTime);

        if (!toggleIsActive)
            return;

        toggleTimeRemaining = Mathf.Max(
            0f,
            toggleTimeRemaining - toggleTimeCostPerSecond * deltaTime);

        if (toggleTimeRemaining <= 0f)
            StopToggle(activeToggleOwner, true);
    }

    private void UpdateToggleRecharge(float deltaTime)
    {
        if (toggleTimeRemaining >= toggleMaximumTime)
            return;

        toggleRechargeElapsed += deltaTime;
        if (toggleRechargeElapsed < toggleRechargeStartTime)
            return;

        float rechargePerSecond = toggleMaximumTime / toggleRechargeDuration;
        toggleTimeRemaining = Mathf.Min(
            toggleMaximumTime,
            toggleTimeRemaining + rechargePerSecond * deltaTime);
    }

    protected virtual void OnDisable()
    {
        if (toggleIsActive)
            StopToggle(activeToggleOwner, false);
    }

    private void TryActivateActive(ParentShip owner)
    {
        if (cooldownTimer > 0f)
            return;

        if (Activate(owner) && StartsCooldownOnActivation)
            StartCooldown();
    }

    private void TryActivateToggle(ParentShip owner)
    {
        if (toggleIsActive || cooldownTimer > 0f)
            return;

        if (toggleTimeRemaining <= 0f)
            return;

        if (!Activate(owner))
            return;

        activeToggleOwner = owner;
        toggleIsActive = true;
    }

    private void TryActivateCharge(ParentShip owner)
    {
        if (currentCharges <= 0)
            return;

        if (!Activate(owner))
            return;

        currentCharges = Mathf.Max(0, currentCharges - 1);

        if (currentCharges < maxCharges && cooldownTimer <= 0f)
            StartCooldown();
    }

    private void StopToggle(ParentShip releaseOwner, bool startCooldown)
    {
        toggleIsActive = false;
        activeToggleOwner = null;
        Release(releaseOwner);

        if (startCooldown)
            StartCooldown();
    }

    private void UpdateCharges(float deltaTime)
    {
        if (currentCharges >= maxCharges)
        {
            cooldownTimer = 0f;
            return;
        }

        if (cooldownTimer <= 0f)
            StartCooldown();

        cooldownTimer = Mathf.Max(0f, cooldownTimer - deltaTime);
        if (cooldownTimer > 0f)
            return;

        currentCharges = Mathf.Min(maxCharges, currentCharges + 1);

        if (currentCharges < maxCharges)
            StartCooldown();
    }

    private void ValidateRuntimeValues()
    {
        cooldown = Mathf.Max(0f, cooldown);
        toggleMaximumTime = Mathf.Max(0.01f, toggleMaximumTime);
        toggleRechargeDuration = Mathf.Max(0.01f, toggleRechargeDuration);
        toggleTimeCostPerSecond = Mathf.Max(0.01f, toggleTimeCostPerSecond);
        toggleRechargeStartTime = Mathf.Clamp(
            toggleRechargeStartTime,
            0f,
            toggleMaximumTime);
        toggleTimeRemaining = Mathf.Clamp(
            toggleTimeRemaining,
            0f,
            toggleMaximumTime);
        maxCharges = Mathf.Max(1, maxCharges);
        currentCharges = Mathf.Clamp(currentCharges, 0, maxCharges);
    }

#if UNITY_EDITOR
    protected virtual void OnValidate()
    {
        ValidateRuntimeValues();
    }
#endif
}
