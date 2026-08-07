using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class AbilityButtonView : MonoBehaviour
{
    [System.Serializable]
    public sealed class ChargeRestoredEvent : UnityEvent<int> { }

    [Header("Source")]
    [SerializeField] private PlayerController playerController;

    [Header("UI")]
    [SerializeField] private Image progressImage;
    [SerializeField] private AbilityChargeRingGraphic chargeRing;
    [SerializeField] private TMP_Text timerText;
    [SerializeField] private TMP_Text chargesText;
    [SerializeField] private GameObject activeModeRoot;
    [SerializeField] private GameObject toggleModeRoot;
    [SerializeField] private GameObject chargesModeRoot;

    [Header("Text")]
    [SerializeField] private bool hideReadyTimerText = true;
    [SerializeField] private bool showChargeNumberText;
    [SerializeField] private string timeFormat = "0.0";
    [SerializeField] private string chargesFormat = "{0}/{1}";

    [Header("Events")]
    [SerializeField] private UnityEvent onActiveCooldownCompleted;
    [SerializeField] private UnityEvent onToggleTimeDepleted;
    [SerializeField] private ChargeRestoredEvent onChargeRestored;
    [SerializeField] private UnityEvent onChargesFullyRestored;

    private ActiveAbility currentAbility;
    private UltimateAbilityMode currentMode;
    private bool hasAbilityState;
    private bool activeCooldownWasRunning;
    private bool toggleHadTime;
    private int lastCharges;
    private string lastTimerText;
    private string lastChargesText;

    private void Awake()
    {
        ConfigureProgressImage();
    }

    private void OnEnable()
    {
        if (playerController != null)
            playerController.OnCurrentShipChanged += HandleCurrentShipChanged;

        BindAbility(GetCurrentAbility());
        RefreshImmediate();
    }

    private void OnDisable()
    {
        if (playerController != null)
            playerController.OnCurrentShipChanged -= HandleCurrentShipChanged;
    }

    private void OnValidate()
    {
        ConfigureProgressImage();
    }

    private void Update()
    {
        ActiveAbility ability = GetCurrentAbility();
        if (ability != currentAbility)
            BindAbility(ability);

        Refresh();
    }

    public void SetPlayerController(PlayerController controller)
    {
        if (playerController == controller)
            return;

        if (isActiveAndEnabled && playerController != null)
            playerController.OnCurrentShipChanged -= HandleCurrentShipChanged;

        playerController = controller;

        if (isActiveAndEnabled && playerController != null)
            playerController.OnCurrentShipChanged += HandleCurrentShipChanged;

        BindAbility(GetCurrentAbility());
        RefreshImmediate();
    }

    private void HandleCurrentShipChanged(ParentShip ship)
    {
        BindAbility(ship != null ? ship.ActiveAbility : null);
        RefreshImmediate();
    }

    private ActiveAbility GetCurrentAbility()
    {
        if (playerController == null || playerController.CurrentShip == null)
            return null;

        return playerController.CurrentShip.ActiveAbility;
    }

    private void BindAbility(ActiveAbility ability)
    {
        currentAbility = ability;
        hasAbilityState = ability != null;

        if (ability == null)
        {
            currentMode = UltimateAbilityMode.Active;
            activeCooldownWasRunning = false;
            toggleHadTime = false;
            lastCharges = 0;
            return;
        }

        currentMode = ability.AbilityMode;
        activeCooldownWasRunning = ability.IsCoolingDown;
        toggleHadTime = ability.ToggleTimeRemaining > 0f;
        lastCharges = ability.CurrentCharges;
    }

    private void RefreshImmediate()
    {
        RefreshModeRoots();
        RefreshVisuals(currentAbility != null);
    }

    private void Refresh()
    {
        if (!hasAbilityState)
        {
            RefreshVisuals(false);
            return;
        }

        if (currentAbility.AbilityMode != currentMode)
            BindAbility(currentAbility);

        RefreshModeRoots();
        RefreshEvents();
        RefreshVisuals(true);
    }

    private void RefreshEvents()
    {
        switch (currentAbility.AbilityMode)
        {
            case UltimateAbilityMode.Toggle:
                RefreshToggleEvents();
                break;
            case UltimateAbilityMode.Charges:
                RefreshChargeEvents();
                break;
            default:
                RefreshActiveEvents();
                break;
        }
    }

    private void RefreshActiveEvents()
    {
        bool isCoolingDown = currentAbility.IsCoolingDown;
        if (activeCooldownWasRunning && !isCoolingDown)
            onActiveCooldownCompleted?.Invoke();

        activeCooldownWasRunning = isCoolingDown;
    }

    private void RefreshToggleEvents()
    {
        bool hasTime = currentAbility.ToggleTimeRemaining > 0f;
        if (toggleHadTime && !hasTime)
            onToggleTimeDepleted?.Invoke();

        toggleHadTime = hasTime;
    }

    private void RefreshChargeEvents()
    {
        int charges = currentAbility.CurrentCharges;
        int maxCharges = currentAbility.MaxCharges;

        if (charges > lastCharges)
        {
            for (int charge = lastCharges + 1; charge <= charges; charge++)
                onChargeRestored?.Invoke(charge);
        }

        if (charges >= maxCharges && lastCharges < maxCharges)
            onChargesFullyRestored?.Invoke();

        lastCharges = charges;
    }

    private void RefreshVisuals(bool hasAbility)
    {
        if (!hasAbility || currentAbility == null)
        {
            SetProgress(0f);
            SetChargeRing(0, 0f, false);
            SetTimerText(string.Empty, false);
            SetChargesText(string.Empty, false);
            RefreshModeRoots();
            return;
        }

        switch (currentAbility.AbilityMode)
        {
            case UltimateAbilityMode.Toggle:
                RefreshToggleVisuals();
                break;
            case UltimateAbilityMode.Charges:
                RefreshChargeVisuals();
                break;
            default:
                RefreshActiveVisuals();
                break;
        }
    }

    private void RefreshActiveVisuals()
    {
        float remaining = currentAbility.CooldownRemaining;
        SetProgress(currentAbility.CooldownProgress01);
        SetChargeRing(0, 0f, false);

        bool showTimer = remaining > 0f || !hideReadyTimerText;
        SetTimerText(showTimer ? FormatTime(remaining) : string.Empty, showTimer);
        SetChargesText(string.Empty, false);
    }

    private void RefreshToggleVisuals()
    {
        SetProgress(currentAbility.IsToggleActive
            ? 0f
            : currentAbility.CooldownRemaining01);
        SetChargeRing(1, currentAbility.ToggleTimeRemaining01, true);
        SetTimerText(string.Empty, false);
        SetChargesText(string.Empty, false);
    }

    private void RefreshChargeVisuals()
    {
        int charges = currentAbility.CurrentCharges;
        int maxCharges = currentAbility.MaxCharges;
        bool showTimer = charges <= 0 && currentAbility.CooldownRemaining > 0f;

        SetProgress(currentAbility.ChargeRechargeProgress01);
        SetChargeRing(maxCharges, currentAbility.ChargeFill01 * maxCharges, true);
        SetTimerText(showTimer ? FormatTime(currentAbility.CooldownRemaining) : string.Empty, showTimer);
        SetChargesText(string.Format(chargesFormat, charges, maxCharges), showChargeNumberText);
    }

    private void RefreshModeRoots()
    {
        bool hasAbility = currentAbility != null;
        SetRoot(activeModeRoot, hasAbility && currentAbility.AbilityMode == UltimateAbilityMode.Active);
        SetRoot(toggleModeRoot, hasAbility && currentAbility.AbilityMode == UltimateAbilityMode.Toggle);
        SetRoot(chargesModeRoot, hasAbility && currentAbility.AbilityMode == UltimateAbilityMode.Charges);
    }

    private void ConfigureProgressImage()
    {
        if (progressImage == null)
            return;

        progressImage.type = Image.Type.Filled;
        progressImage.fillMethod = Image.FillMethod.Radial360;
        progressImage.fillOrigin = (int)Image.Origin360.Top;
        progressImage.fillClockwise = false;
    }

    private void SetProgress(float value)
    {
        if (progressImage == null)
            return;

        progressImage.fillAmount = Mathf.Clamp01(value);
    }

    private void SetChargeRing(int maxCharges, float filledCharges, bool visible)
    {
        if (chargeRing == null)
            return;

        if (chargeRing.gameObject.activeSelf != visible)
            chargeRing.gameObject.SetActive(visible);

        if (!visible)
            return;

        chargeRing.SetChargeState(maxCharges, filledCharges);
    }

    private void SetTimerText(string value, bool visible)
    {
        if (timerText == null)
            return;

        if (timerText.gameObject.activeSelf != visible)
            timerText.gameObject.SetActive(visible);

        if (lastTimerText == value)
            return;

        timerText.text = value;
        lastTimerText = value;
    }

    private void SetChargesText(string value, bool visible)
    {
        if (chargesText == null)
            return;

        if (chargesText.gameObject.activeSelf != visible)
            chargesText.gameObject.SetActive(visible);

        if (lastChargesText == value)
            return;

        chargesText.text = value;
        lastChargesText = value;
    }

    private string FormatTime(float time)
    {
        return Mathf.Ceil(time * 10f) * 0.1f < 1f
            ? time.ToString(timeFormat)
            : Mathf.CeilToInt(time).ToString();
    }

    private void SetRoot(GameObject root, bool active)
    {
        if (root == null || root.activeSelf == active)
            return;

        root.SetActive(active);
    }
}
