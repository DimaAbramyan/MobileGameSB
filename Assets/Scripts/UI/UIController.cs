using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIController : MonoBehaviour
{
    [System.Serializable]
    private sealed class PlayerShipHUD
    {
        [SerializeField] private RectTransform hudRoot;
        [SerializeField] private StatBar healthBar;
        [SerializeField] private StatBar shieldBar;
        [SerializeField] private UnityEngine.UI.Image shipPreviewFill;
        [Header("Health values")]
        [SerializeField] private TMP_Text healthValueText;
        [SerializeField] private TMP_Text shieldValueText;
        [Header("Regeneration cooldown")]
        [SerializeField] private Slider healthRegenerationCooldownSlider;
        [SerializeField] private Slider shieldRegenerationCooldownSlider;
        [Header("Ultimate ability")]
        [SerializeField] private Slider ultimateAbilityCooldownSlider;
        [SerializeField] private Image abilityChargesCountImage;
        [SerializeField] private TMP_Text abilityChargesCountText;

        private ParentShip boundShip;
        private int displayedAbilityCharges = -1;
        private bool abilityChargesCountVisible;

        public void Bind(ParentShip ship, Sprite shipIcon)
        {
            Unbind();

            boundShip = ship;
            if (boundShip == null)
                return;

            if (shipPreviewFill != null)
            {
                shipPreviewFill.sprite = shipIcon;
                shipPreviewFill.enabled = shipIcon != null;
            }

            if (healthBar == null || shieldBar == null)
            {
                Debug.LogError(
                    "Both health and shield bars must be configured for a ship status row.");
                boundShip = null;
                return;
            }

            healthBar.Setup(
                boundShip,
                () => boundShip.MaximumHealthPoints,
                boundShip.SubscribeHealth,
                boundShip.UnsubscribeHealth,
                () => boundShip.CurrentHealthPoints);
            healthBar.UpdateMax(boundShip.MaximumHealthPoints);
            boundShip.OnMaxHealthChanged += healthBar.UpdateMax;
            boundShip.SubscribeHealth(UpdateHealthValueText);
            boundShip.OnMaxHealthChanged += UpdateHealthValueText;

            shieldBar.Setup(
                boundShip,
                () => boundShip.MaximumShieldPoints,
                boundShip.SubscribeShield,
                boundShip.UnsubscribeShield,
                () => boundShip.CurrentShieldPoints);
            shieldBar.UpdateMax(boundShip.MaximumShieldPoints);
            boundShip.OnMaxShieldChanged += shieldBar.UpdateMax;
            boundShip.SubscribeShield(UpdateShieldValueText);
            boundShip.OnMaxShieldChanged += UpdateShieldValueText;

            UpdateHealthValueText(boundShip.CurrentHealthPoints);
            UpdateShieldValueText(boundShip.CurrentShieldPoints);

            SetCooldownSliderValue(
                healthRegenerationCooldownSlider,
                boundShip.HealthRegenerationCooldownProgress);
            SetCooldownSliderValue(
                shieldRegenerationCooldownSlider,
                boundShip.ShieldRegenerationCooldownProgress);
            boundShip.OnHealthRegenerationCooldownProgressChanged +=
                UpdateHealthRegenerationCooldown;
            boundShip.OnShieldRegenerationCooldownProgressChanged +=
                UpdateShieldRegenerationCooldown;
            boundShip.OnDied += Clear;
            RefreshAbilityState();
        }

        public void Unbind()
        {
            if (boundShip != null)
            {
                if (healthBar != null)
                    boundShip.OnMaxHealthChanged -= healthBar.UpdateMax;
                if (shieldBar != null)
                    boundShip.OnMaxShieldChanged -= shieldBar.UpdateMax;
                boundShip.UnsubscribeHealth(UpdateHealthValueText);
                boundShip.OnMaxHealthChanged -= UpdateHealthValueText;
                boundShip.UnsubscribeShield(UpdateShieldValueText);
                boundShip.OnMaxShieldChanged -= UpdateShieldValueText;
                boundShip.OnHealthRegenerationCooldownProgressChanged -=
                    UpdateHealthRegenerationCooldown;
                boundShip.OnShieldRegenerationCooldownProgressChanged -=
                    UpdateShieldRegenerationCooldown;
                boundShip.OnDied -= Clear;
            }

            healthBar?.Clear();
            shieldBar?.Clear();
            if (shipPreviewFill != null)
            {
                shipPreviewFill.sprite = null;
                shipPreviewFill.enabled = false;
            }
            SetCooldownSliderValue(ultimateAbilityCooldownSlider, 0f);
            SetAbilityChargesCount(0, false);
            SetValueText(healthValueText, 0f, 0f);
            SetValueText(shieldValueText, 0f, 0f);
            boundShip = null;
        }

        public void RefreshAbilityState()
        {
            ActiveAbility ability = boundShip != null
                ? boundShip.ActiveAbility
                : null;
            if (ability == null)
            {
                SetCooldownSliderValue(ultimateAbilityCooldownSlider, 0f);
                SetAbilityChargesCount(0, false);
                return;
            }

            SetCooldownSliderValue(
                ultimateAbilityCooldownSlider,
                ability.CooldownRemaining01);

            bool hasCharges = ability.AbilityMode == UltimateAbilityMode.Charges;
            SetAbilityChargesCount(
                ability.CurrentCharges,
                hasCharges);
        }

        private void UpdateHealthRegenerationCooldown(float progress)
        {
            SetCooldownSliderValue(healthRegenerationCooldownSlider, progress);
        }

        private void UpdateShieldRegenerationCooldown(float progress)
        {
            SetCooldownSliderValue(shieldRegenerationCooldownSlider, progress);
        }

        private void UpdateHealthValueText(float currentHealth)
        {
            if (boundShip == null)
                return;

            SetValueText(
                healthValueText,
                currentHealth,
                boundShip.MaximumHealthPoints);
        }

        private void UpdateShieldValueText(float currentShield)
        {
            if (boundShip == null)
                return;

            SetValueText(
                shieldValueText,
                currentShield,
                boundShip.MaximumShieldPoints);
        }

        private static void SetValueText(TMP_Text text, float current, float maximum)
        {
            if (text == null)
                return;

            text.SetText(
                "{0}/{1}",
                Mathf.CeilToInt(Mathf.Max(0f, current)),
                Mathf.CeilToInt(Mathf.Max(0f, maximum)));
        }

        private void SetAbilityChargesCount(
            int currentCharges,
            bool visible)
        {
            if (abilityChargesCountImage != null
                && abilityChargesCountImage.gameObject.activeSelf != visible)
            {
                abilityChargesCountImage.gameObject.SetActive(visible);
            }

            if (!visible)
            {
                if (abilityChargesCountText != null
                    && abilityChargesCountVisible)
                {
                    abilityChargesCountText.text = string.Empty;
                }

                displayedAbilityCharges = -1;
                abilityChargesCountVisible = false;
                return;
            }

            if (abilityChargesCountVisible
                && displayedAbilityCharges == currentCharges)
            {
                return;
            }

            if (abilityChargesCountText != null)
                abilityChargesCountText.text = currentCharges.ToString();

            displayedAbilityCharges = currentCharges;
            abilityChargesCountVisible = true;
        }

        private static void SetCooldownSliderValue(Slider slider, float progress)
        {
            if (slider == null)
                return;

            slider.SetValueWithoutNotify(Mathf.Lerp(
                slider.minValue,
                slider.maxValue,
                Mathf.Clamp01(progress)));
        }

        private void Clear()
        {
            Unbind();
        }
    }

    [SerializeField]
    StatBar healthBar;
    [SerializeField]
    StatBar shieldBar;
    [SerializeField]
    StatBar extraHealthBar;
    [SerializeField] private PlayerController playerController;
    [Header("Fighting player HUD")]
    [SerializeField] private PlayerShipHUD playerShip1Hud;
    [SerializeField] private PlayerShipHUD playerShip2Hud;
    [SerializeField, HideInInspector] private PlayerShipHUD[] shipStatusBars;
    [SerializeField] private HullCatalog hullCatalog;
    private ParentShip boundShip;

    private bool HasExplicitTeamStatusBars => playerShip1Hud != null
        || playerShip2Hud != null;

    private bool HasTeamStatusBars => HasExplicitTeamStatusBars
        || shipStatusBars != null && shipStatusBars.Length > 0;

    private void OnEnable()
    {
        if (playerController == null)
            return;

        if (!HasTeamStatusBars)
        {
            playerController.OnCurrentShipChanged += BindUI;
            BindCurrentShip();
        }
    }

    private void Start()
    {
        if (HasTeamStatusBars)
            BindTeamStatusBars();
        else
            BindCurrentShip();
    }

    private void Update()
    {
        if (HasExplicitTeamStatusBars)
        {
            playerShip1Hud?.RefreshAbilityState();
            playerShip2Hud?.RefreshAbilityState();
            return;
        }

        if (shipStatusBars == null)
            return;

        for (int i = 0; i < shipStatusBars.Length; i++)
            shipStatusBars[i].RefreshAbilityState();
    }

    private void OnDisable()
    {
        if (playerController != null && !HasTeamStatusBars)
            playerController.OnCurrentShipChanged -= BindUI;

        UnbindMaxValueEvents();

        playerShip1Hud?.Unbind();
        playerShip2Hud?.Unbind();

        if (shipStatusBars != null)
        {
            for (int i = 0; i < shipStatusBars.Length; i++)
                shipStatusBars[i].Unbind();
        }

        boundShip = null;
    }

    private void BindTeamStatusBars()
    {
        if (playerController == null)
            return;

        ParentShip[] ships =
            playerController.GetComponentsInChildren<ParentShip>(true);
        if (HasExplicitTeamStatusBars)
        {
            BindTeamStatusBar(playerShip1Hud, ships, 0);
            BindTeamStatusBar(playerShip2Hud, ships, 1);
            return;
        }

        if (shipStatusBars == null)
            return;

        for (int i = 0; i < shipStatusBars.Length; i++)
            BindTeamStatusBar(shipStatusBars[i], ships, i);
    }

    private void BindTeamStatusBar(
        PlayerShipHUD playerShipHud,
        ParentShip[] ships,
        int shipIndex)
    {
        if (playerShipHud == null)
            return;

        ParentShip ship = shipIndex < ships.Length ? ships[shipIndex] : null;
        playerShipHud.Bind(ship, GetHullIcon(ship));
    }

    private Sprite GetHullIcon(ParentShip ship)
    {
        if (ship == null || ship.ShipData == null || hullCatalog == null)
            return null;

        var hulls = hullCatalog.Hulls;
        for (int i = 0; i < hulls.Count; i++)
        {
            HullContentDefinition hull = hulls[i];
            if (hull != null && hull.Data == ship.ShipData)
                return hull.Icon;
        }

        Debug.LogWarning(
            $"Hull icon for ship '{ship.name}' is not configured in the hull catalog.",
            ship);
        return null;
    }

    private void BindCurrentShip()
    {
        if (playerController != null && playerController.CurrentShip != null)
            BindUI(playerController.CurrentShip);
    }

    private void BindUI(ParentShip ship)
    {
        if (ship == null || ship == boundShip)
            return;

        UnbindMaxValueEvents();
        boundShip = ship;

        healthBar.Setup(
            ship,
            () => ship.MaximumHealthPoints,
            ship.SubscribeHealth,
            ship.UnsubscribeHealth,
            ()=>ship.CurrentHealthPoints);
        healthBar.UpdateMax(ship.MaximumHealthPoints); 
        ship.OnMaxHealthChanged += healthBar.UpdateMax;

        shieldBar.Setup(
            ship,
            () => ship.MaximumShieldPoints,
            ship.SubscribeShield,
            ship.UnsubscribeShield,
            () => ship.CurrentShieldPoints);

        shieldBar.UpdateMax(ship.MaximumShieldPoints); 
        ship.OnMaxShieldChanged += shieldBar.UpdateMax;

        ExtraHealthPassive extraHealth = ship.GetComponent<ExtraHealthPassive>();

        if (extraHealth != null)
        {
            extraHealthBar.Setup(
                ship,
                () => extraHealth.MaximumExtraHealth,
                extraHealth.SubscribeExtraHealth,
                extraHealth.UnsubscribeExtraHealth,
                () => extraHealth.ExtraHealth);
        }
        else
        {
            extraHealthBar.SetValue(0);
        }

    }

    private void UnbindMaxValueEvents()
    {
        if (boundShip == null)
            return;

        boundShip.OnMaxHealthChanged -= healthBar.UpdateMax;
        boundShip.OnMaxShieldChanged -= shieldBar.UpdateMax;
    }

}
