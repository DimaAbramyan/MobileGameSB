using UnityEngine;
using UnityEngine.UI;

public sealed class CraftNavigationController : MonoBehaviour
{
    [Header("Flow")]
    [SerializeField] private CraftCreationFlowController craftCreationFlow;
    [SerializeField] private CraftSaveController craftSaveController;

    [Header("Navigation Buttons")]
    [SerializeField] private Button bodyButton;
    [SerializeField] private Button weaponButton;
    [SerializeField] private Button colourButton;
    [SerializeField] private Button saveButton;

    private void OnEnable()
    {
        RegisterButtons();
        RegisterFlowEvents();
        RefreshSaveButton();
    }

    private void OnDisable()
    {
        UnregisterButtons();
        UnregisterFlowEvents();
    }

    private void RegisterButtons()
    {
        if (bodyButton != null)
            bodyButton.onClick.AddListener(ShowBodySelection);

        if (weaponButton != null)
            weaponButton.onClick.AddListener(ShowWeaponSelection);

        if (colourButton != null)
            colourButton.onClick.AddListener(ShowColourSelection);

        if (saveButton != null)
            saveButton.onClick.AddListener(OpenSaveDialog);
    }

    private void UnregisterButtons()
    {
        if (bodyButton != null)
            bodyButton.onClick.RemoveListener(ShowBodySelection);

        if (weaponButton != null)
            weaponButton.onClick.RemoveListener(ShowWeaponSelection);

        if (colourButton != null)
            colourButton.onClick.RemoveListener(ShowColourSelection);

        if (saveButton != null)
            saveButton.onClick.RemoveListener(OpenSaveDialog);
    }

    private void RegisterFlowEvents()
    {
        if (craftCreationFlow == null)
            return;

        craftCreationFlow.HullSelectionChanged += HandleCraftChanged;
        craftCreationFlow.WeaponAssignmentChanged += HandleWeaponAssignmentChanged;
    }

    private void UnregisterFlowEvents()
    {
        if (craftCreationFlow == null)
            return;

        craftCreationFlow.HullSelectionChanged -= HandleCraftChanged;
        craftCreationFlow.WeaponAssignmentChanged -= HandleWeaponAssignmentChanged;
    }

    private void ShowBodySelection()
    {
        craftCreationFlow?.ShowBodySelection();
    }

    private void ShowWeaponSelection()
    {
        craftCreationFlow?.ShowWeaponSelection();
    }

    private void ShowColourSelection()
    {
        craftCreationFlow?.ShowColourSelection();
    }

    private void OpenSaveDialog()
    {
        if (craftCreationFlow == null
            || !craftCreationFlow.TryValidateCurrentCraft(out _))
        {
            RefreshSaveButton();
            return;
        }

        craftSaveController?.OpenSaveDialog();
    }

    private void HandleCraftChanged(HullContentDefinition _)
    {
        RefreshSaveButton();
    }

    private void HandleWeaponAssignmentChanged(
        string _,
        WeaponContentDefinition __)
    {
        RefreshSaveButton();
    }

    private void RefreshSaveButton()
    {
        if (saveButton != null)
        {
            saveButton.interactable = craftCreationFlow != null
                && craftCreationFlow.TryValidateCurrentCraft(out _);
        }
    }
}
