using UnityEngine;
using Zenject;

public sealed class ShipColorCustomizationController : MonoBehaviour
{
    [SerializeField] private SavedCraftListController savedCraftList;
    [SerializeField] private ShipColorPaletteSelectionController colorPicker;
    [SerializeField] private CraftCreationFlowController craftCreationFlow;

    private SaveManager saveManager;
    private TeamSelectionService selectedShipsService;
    private SaveShip focusedShip;

    [Inject]
    private void Construct(SaveManager saveManager, TeamSelectionService selectedShipsService)
    {
        this.saveManager = saveManager;
        this.selectedShipsService = selectedShipsService;
    }

    private void OnEnable()
    {
        if (savedCraftList == null || colorPicker == null)
        {
            Debug.LogError("Ship color customization requires a craft list and a palette selector.", this);
            enabled = false;
            return;
        }

        savedCraftList.FocusedShipChanged += SetFocusedShip;
        colorPicker.PaletteChanged += UpdatePalette;
        colorPicker.PaletteEditCompleted += SavePalette;
    }

    private void OnDisable()
    {
        if (savedCraftList != null)
            savedCraftList.FocusedShipChanged -= SetFocusedShip;

        if (colorPicker != null)
        {
            colorPicker.PaletteChanged -= UpdatePalette;
            colorPicker.PaletteEditCompleted -= SavePalette;
        }
    }

    private void SetFocusedShip(SaveShip ship)
    {
        if (IsCreatingNewCraft())
            return;

        focusedShip = ship;
        if (focusedShip == null)
            return;

        focusedShip.EnsureColorPalette();
        colorPicker.SetPalette(focusedShip.colors);
    }

    private void UpdatePalette(ShipColorPalette newPalette)
    {
        if (IsCreatingNewCraft())
            return;

        if (focusedShip == null || newPalette == null)
            return;

        focusedShip.colors = newPalette;
    }

    private void SavePalette()
    {
        if (IsCreatingNewCraft())
            return;

        if (focusedShip == null || saveManager == null)
            return;

        focusedShip.EnsureColorPalette();
        saveManager.SaveShip(focusedShip);
        selectedShipsService?.UpdateSelectedShip(focusedShip);
    }

    private bool IsCreatingNewCraft()
    {
        return craftCreationFlow != null && craftCreationFlow.IsCreatingNewCraft;
    }
}
