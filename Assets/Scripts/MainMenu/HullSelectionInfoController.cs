using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zenject;

public sealed class HullSelectionInfoController : MonoBehaviour
{
    [Header("Flow")]
    [SerializeField] private CraftCreationFlowController craftCreationFlow;

    [Header("Top")]
    [SerializeField] private Image iconImage;
    [SerializeField] private TMP_Text hullNameText;
    [SerializeField] private TMP_Text weaponSlotsText;
    [SerializeField] private TMP_Text energyCapacityText;

    [Header("Stats")]
    [SerializeField] private TMP_Text healthText;
    [SerializeField] private TMP_Text shieldText;
    [SerializeField] private TMP_Text speedText;

    [Header("Descriptions")]
    [SerializeField] private TMP_Text activeAbilityDescriptionText;
    [SerializeField] private TMP_Text passiveAbilityDescriptionText;

    private ContentProgressService contentProgressService;

    [Inject]
    private void Construct(ContentProgressService progressService)
    {
        contentProgressService = progressService;

        if (isActiveAndEnabled)
            Refresh(craftCreationFlow != null ? craftCreationFlow.SelectedHull : null);
    }

    private void OnEnable()
    {
        if (craftCreationFlow != null)
            craftCreationFlow.HullSelectionChanged += Refresh;

        Refresh(craftCreationFlow != null ? craftCreationFlow.SelectedHull : null);
    }

    private void OnDisable()
    {
        if (craftCreationFlow != null)
            craftCreationFlow.HullSelectionChanged -= Refresh;
    }

    private void Refresh(HullContentDefinition hull)
    {
        if (hull == null || hull.Data == null)
        {
            Clear();
            return;
        }

        ShipData data = hull.Data;
        ShipMetaRuntimeStats stats = CraftProgressionText.GetHullStats(
            hull,
            contentProgressService);
        if (iconImage != null)
        {
            iconImage.sprite = hull.Icon;
            iconImage.enabled = hull.Icon != null;
        }

        int weaponSlots = craftCreationFlow != null
            ? craftCreationFlow.WeaponSlotCount
            : data.maximumWeaponCount;
        SetText(hullNameText, hull.DisplayName);
        SetText(weaponSlotsText, $"Weapon slots: {weaponSlots}");
        SetText(energyCapacityText, $"Energy: {stats.MaximumEnergy}");
        SetText(healthText, $"HP: {stats.MaximumHealthPoints:0.#}");
        SetText(shieldText, $"SP: {stats.MaximumShieldPoints:0.#}");
        SetText(speedText, $"Speed: {stats.Speed:0.#}");
        SetText(activeAbilityDescriptionText, hull.ActiveAbilityDescription);
        SetText(passiveAbilityDescriptionText, hull.PassiveAbilityDescription);
    }

    private void Clear()
    {
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
        SetText(activeAbilityDescriptionText, string.Empty);
        SetText(passiveAbilityDescriptionText, string.Empty);
    }

    private static void SetText(TMP_Text text, string value)
    {
        if (text != null)
            text.text = value ?? string.Empty;
    }
}
