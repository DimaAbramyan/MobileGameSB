using System.Collections.Generic;
using TMPro;
using UnityEngine;

public sealed class CraftMenuEnergyCostController : MonoBehaviour
{
    [SerializeField] private CraftCreationFlowController craftCreationFlow;
    [SerializeField] private TMP_Text energyCostText;
    [SerializeField] private Color normalColor = Color.black;
    [SerializeField] private Color overflowColor = Color.red;

    private void Awake()
    {
        if (energyCostText == null)
            energyCostText = GetComponent<TMP_Text>();
    }

    private void OnEnable()
    {
        Subscribe();
        Refresh();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void OnDestroy()
    {
        Unsubscribe();
    }

    public void Refresh()
    {
        if (energyCostText == null)
            return;

        HullContentDefinition hull = craftCreationFlow != null
            ? craftCreationFlow.SelectedHull
            : null;
        if (hull == null || hull.Data == null)
        {
            energyCostText.text = "NaN";
            energyCostText.color = normalColor;
            return;
        }

        int usedEnergy = GetUsedEnergy(craftCreationFlow.SelectedWeapons);
        int maximumEnergy = Mathf.Max(0, hull.Data.maximumEnergy);
        energyCostText.text = $"{usedEnergy}/{maximumEnergy}";
        energyCostText.color = usedEnergy > maximumEnergy
            ? overflowColor
            : normalColor;
    }

    private void Subscribe()
    {
        if (craftCreationFlow == null)
            return;

        craftCreationFlow.HullSelectionChanged -= HandleHullSelectionChanged;
        craftCreationFlow.WeaponAssignmentChanged -= HandleWeaponAssignmentChanged;
        craftCreationFlow.HullSelectionChanged += HandleHullSelectionChanged;
        craftCreationFlow.WeaponAssignmentChanged += HandleWeaponAssignmentChanged;
    }

    private void Unsubscribe()
    {
        if (craftCreationFlow == null)
            return;

        craftCreationFlow.HullSelectionChanged -= HandleHullSelectionChanged;
        craftCreationFlow.WeaponAssignmentChanged -= HandleWeaponAssignmentChanged;
    }

    private void HandleHullSelectionChanged(HullContentDefinition _)
    {
        Refresh();
    }

    private void HandleWeaponAssignmentChanged(string _, WeaponContentDefinition __)
    {
        Refresh();
    }

    private static int GetUsedEnergy(IReadOnlyList<WeaponContentDefinition> weapons)
    {
        if (weapons == null)
            return 0;

        int usedEnergy = 0;
        for (int i = 0; i < weapons.Count; i++)
        {
            WeaponContentDefinition weapon = weapons[i];
            if (weapon != null && weapon.Data != null)
                usedEnergy += Mathf.Max(0, weapon.Data.EnergyCost);
        }

        return usedEnergy;
    }
}
