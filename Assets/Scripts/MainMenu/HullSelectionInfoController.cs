using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zenject;

public sealed class HullSelectionInfoController : MonoBehaviour
{
    [Header("Flow")]
    [SerializeField] private CraftCreationFlowController craftCreationFlow;

    [Header("Stat Blocks")]
    [SerializeField] private StatBlocksView statBlocksView;
    [SerializeField] private StatDescriptionView activeDescriptionView;
    [SerializeField] private StatDescriptionView passiveDescriptionView;
    [SerializeField] private ParentShip runtimeHull;
    private HullContentDefinition shownHull;
    private CraftUIButton pointerPreview, navigationPreview;
    public HullContentDefinition DisplayedHull { get; private set; }
    public StatBlocksView StatBlocks => statBlocksView;

    [Header("Top")]
    [SerializeField] private Image iconImage;
    [SerializeField] private TMP_Text hullNameText;
    [SerializeField] private TMP_Text weaponSlotsText;
    [SerializeField] private TMP_Text energyCapacityText;

    [Header("Stats")]
    [SerializeField] private TMP_Text healthText;
    [SerializeField] private TMP_Text shieldText;
    [SerializeField] private TMP_Text speedText;

    [Header("Additional Stats")]
    [SerializeField] private TMP_Text weaponSlotsInfoText;
    [SerializeField] private TMP_Text energyCapacityInfoText;

    [Header("Descriptions")]
    [SerializeField] private TMP_Text activeAbilityDescriptionText;
    [SerializeField] private TMP_Text passiveAbilityDescriptionText;

    private ContentProgressService contentProgressService;

    [Inject]
    private void Construct(ContentProgressService progressService) => SetProgressService(progressService);

    public void SetProgressService(ContentProgressService progressService)
    {
        if (ReferenceEquals(contentProgressService, progressService)) return;
        if (contentProgressService != null) contentProgressService.ProgressChanged -= RefreshCurrent;
        contentProgressService = progressService;

        if (isActiveAndEnabled)
        {
            if (contentProgressService != null) contentProgressService.ProgressChanged += RefreshCurrent;
            Refresh(craftCreationFlow != null ? craftCreationFlow.SelectedHull : null);
        }
    }

    private void OnEnable()
    {
        if (contentProgressService != null) contentProgressService.ProgressChanged += RefreshCurrent;
        if (craftCreationFlow != null)
            craftCreationFlow.HullSelectionChanged += Refresh;

        Refresh(craftCreationFlow != null ? craftCreationFlow.SelectedHull : null);
    }

    private void OnDisable()
    {
        if (contentProgressService != null) contentProgressService.ProgressChanged -= RefreshCurrent;
        pointerPreview = navigationPreview = null;
        if (craftCreationFlow != null)
            craftCreationFlow.HullSelectionChanged -= Refresh;
    }

    public void Show(HullContentDefinition hull)
    {
        shownHull = hull;
        RefreshCurrent();
    }

    public void SetRuntimeSource(ParentShip ship) { runtimeHull = ship; RefreshCurrent(); }
    public void Preview(CraftUIButton button, bool pointer)
    {
        if (button == null || !(button.Content is HullContentDefinition)) return;
        if (pointer) pointerPreview = button; else navigationPreview = button;
        RefreshCurrent();
    }
    public void EndPreview(CraftUIButton button, bool pointer)
    {
        if (pointer) { if (pointerPreview != button) return; pointerPreview = null; }
        else { if (navigationPreview != button) return; navigationPreview = null; }
        RefreshCurrent();
    }
    private void RefreshCurrent()
    {
        HullContentDefinition hull = craftCreationFlow != null ? craftCreationFlow.SelectedHull : shownHull;
        var preview = pointerPreview != null ? pointerPreview : navigationPreview;
        if (preview != null && preview.Content is HullContentDefinition hovered) hull = hovered;
        RefreshDetails(hull);
    }

    private void Refresh(HullContentDefinition _) => RefreshCurrent();
    private void RefreshDetails(HullContentDefinition hull)
    {
        if (hull == null || hull.Data == null)
        {
            Clear();
            return;
        }

        ShipData data = hull.Data;
        DisplayedHull = hull;
        int upgradeLevel = CraftProgressionText.GetUpgradeLevel(
            hull,
            contentProgressService);
        if (statBlocksView != null)
        {
            if (runtimeHull != null && runtimeHull.gameObject.scene.IsValid() && runtimeHull.ShipData == data)
                statBlocksView.Bind(runtimeHull);
            else statBlocksView.Preview(data.ShipMetaConfig, upgradeLevel);
        }
        bool canPreviewUpgrade = craftCreationFlow == null
            && contentProgressService != null
            && contentProgressService.GetState(hull).CanUpgrade;
        int nextUpgradeLevel = Mathf.Min(
            upgradeLevel + 1,
            hull.MaxUpgradeLevel);
        ShipMetaRuntimeStats stats = CraftProgressionText.GetHullStatsAtLevel(
            hull,
            upgradeLevel);
        ShipMetaRuntimeStats nextStats = CraftProgressionText.GetHullStatsAtLevel(
            hull,
            nextUpgradeLevel);
        if (iconImage != null)
        {
            iconImage.sprite = hull.Icon;
            iconImage.enabled = hull.Icon != null;
        }

        int weaponSlots = craftCreationFlow != null
            ? craftCreationFlow.WeaponSlotCount
            : data.maximumWeaponCount;
        SetText(hullNameText, hull.DisplayName);
        SetText(
            weaponSlotsText,
            FormatLevel(upgradeLevel, nextUpgradeLevel, canPreviewUpgrade));
        SetText(energyCapacityText, $"Rarity: {hull.Rarity}");
        SetText(
            healthText,
            FormatStat(
                "HP",
                stats.MaximumHealthPoints,
                nextStats.MaximumHealthPoints,
                canPreviewUpgrade));
        SetText(
            shieldText,
            FormatStat(
                "SP",
                stats.MaximumShieldPoints,
                nextStats.MaximumShieldPoints,
                canPreviewUpgrade));
        SetText(
            speedText,
            FormatStat("Speed", stats.Speed, nextStats.Speed, canPreviewUpgrade));
        SetText(weaponSlotsInfoText, $"Weapon slots: {weaponSlots}");
        SetText(
            energyCapacityInfoText,
            FormatStat(
                "Energy",
                stats.MaximumEnergy,
                nextStats.MaximumEnergy,
                canPreviewUpgrade));
        if (activeDescriptionView != null) activeDescriptionView.SetFallback(hull.ActiveAbilityDescription);
        else SetText(activeAbilityDescriptionText, hull.ActiveAbilityDescription);
        if (passiveDescriptionView != null) passiveDescriptionView.SetFallback(hull.PassiveAbilityDescription);
        else SetText(passiveAbilityDescriptionText, hull.PassiveAbilityDescription);
    }

    private void Clear()
    {
        DisplayedHull = null;
        if (statBlocksView != null && statBlocksView.Meta != null) statBlocksView.Show(null);
        if (iconImage != null)
        {
            iconImage.sprite = null;
            iconImage.enabled = false;
        }

        SetText(hullNameText, string.Empty);
        SetText(weaponSlotsText, string.Empty);
        SetText(energyCapacityText, string.Empty);
        SetText(healthText, string.Empty);
        SetText(shieldText, string.Empty);
        SetText(speedText, string.Empty);
        SetText(weaponSlotsInfoText, string.Empty);
        SetText(energyCapacityInfoText, string.Empty);
        if (activeDescriptionView != null) activeDescriptionView.SetFallback("");
        else SetText(activeAbilityDescriptionText, string.Empty);
        if (passiveDescriptionView != null) passiveDescriptionView.SetFallback("");
        else SetText(passiveAbilityDescriptionText, string.Empty);
    }

    private static void SetText(TMP_Text text, string value)
    {
        if (text != null)
            text.text = value ?? string.Empty;
    }

    private static string FormatLevel(
        int currentLevel,
        int nextLevel,
        bool canPreviewUpgrade)
    {
        return canPreviewUpgrade
            ? $"Level {currentLevel} → {nextLevel}"
            : $"Level {currentLevel}";
    }

    private static string FormatStat(
        string label,
        float currentValue,
        float nextValue,
        bool canPreviewUpgrade)
    {
        if (!canPreviewUpgrade || Mathf.Approximately(currentValue, nextValue))
            return $"{label}: {currentValue:0.#}";

        return $"{label}: {currentValue:0.#} → {nextValue:0.#}";
    }

    private static string FormatStat(
        string label,
        int currentValue,
        int nextValue,
        bool canPreviewUpgrade)
    {
        if (!canPreviewUpgrade || currentValue == nextValue)
            return $"{label}: {currentValue}";

        return $"{label}: {currentValue} → {nextValue}";
    }
}
