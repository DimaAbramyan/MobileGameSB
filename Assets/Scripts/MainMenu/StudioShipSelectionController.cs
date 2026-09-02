using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using Zenject;

public sealed class StudioShipSelectionController : MonoBehaviour
{
    [Serializable]
    private sealed class ShipSlotView
    {
        [SerializeField] private Button button;
        [SerializeField] private Image icon;
        [SerializeField] private TMP_Text nameText;

        public Button Button => button;
        public Image Icon => icon != null ? icon : button != null ? button.image : null;
        public TMP_Text NameText => nameText;
    }

    [SerializeField] private SavedCraftListController savedCraftList;
    [SerializeField] private ShipSlotView[] shipSlots = Array.Empty<ShipSlotView>();
    [SerializeField] private Sprite emptyShipIcon;
    [SerializeField] private Color selectedSlotColor = new Color(0.35f, 0.8f, 1f, 1f);
    [SerializeField] private Color unselectedSlotColor = Color.white;

    // Preserved for the existing scene until both Studio slots are assigned.
    [SerializeField] private Image previewImage;
    [SerializeField] private TMP_Text previewName;

    private TeamSelectionService selectedShipsService;
    private PrefabFactory prefabFactory;
    private UnityAction[] slotHandlers;
    private SaveShip selectedSavedShip;
    private int targetSlotIndex = -1;

    [Inject]
    private void Construct(TeamSelectionService selectedShipsService, PrefabFactory prefabFactory)
    {
        this.selectedShipsService = selectedShipsService;
        this.prefabFactory = prefabFactory;
    }

    private void Awake()
    {
        ResolveProjectDependencies();
        ResolveSavedCraftList();
        RegisterSlotButtons();
    }

    private void OnEnable()
    {
        if (!ValidateConfiguration())
            return;

        savedCraftList.ShipSelected += SelectShip;
        selectedShipsService.Changed += RefreshSlots;
        savedCraftList.Refresh();
        RefreshSlots();
    }

    private void OnDisable()
    {
        if (savedCraftList != null)
            savedCraftList.ShipSelected -= SelectShip;

        if (selectedShipsService != null)
            selectedShipsService.Changed -= RefreshSlots;
    }

    private void OnDestroy()
    {
        UnregisterSlotButtons();
    }

    public void OpenForShipSlot(int slotIndex)
    {
        SelectTargetSlot(slotIndex);
    }

    public void OpenForBrowsing()
    {
        targetSlotIndex = -1;
        RefreshSlots();
    }

    public void EditFocusedCraft()
    {
        ResolveProjectDependencies();
        ResolveSavedCraftList();

        SaveShip ship = GetCraftForEditing();
        if (ship == null)
        {
            Debug.LogWarning("Select a craft or an occupied ship slot before opening the editor.", this);
            return;
        }

        savedCraftList.EditCraft(ship);
    }

    private SaveShip GetCraftForEditing()
    {
        if (targetSlotIndex >= 0 && selectedShipsService != null)
        {
            SaveShip slotShip = selectedShipsService.GetSelectedShip(targetSlotIndex);
            if (slotShip != null)
                return slotShip;
        }

        if (savedCraftList != null && savedCraftList.SelectedShip != null)
            return savedCraftList.SelectedShip;

        return selectedSavedShip;
    }

    private void SelectShip(SaveShip ship)
    {
        if (selectedShipsService == null)
            return;

        if (ship == null)
        {
            selectedSavedShip = null;
            RefreshSlots();
            return;
        }

        selectedSavedShip = ship;
        TryAssignSelectedShip();
        RefreshSlots();
    }

    private void SelectTargetSlot(int slotIndex)
    {
        int clampedSlotIndex = Mathf.Clamp(slotIndex, 0, TeamSelectionService.SelectedShipCount - 1);
        if (targetSlotIndex == clampedSlotIndex)
        {
            targetSlotIndex = -1;
            savedCraftList.SetFocusedShip(null);
            RefreshSlots();
            return;
        }

        targetSlotIndex = clampedSlotIndex;
        SaveShip focusedSlotShip = selectedShipsService != null
            ? selectedShipsService.GetSelectedShip(targetSlotIndex)
            : null;
        savedCraftList.SetFocusedShip(focusedSlotShip);
        TryAssignSelectedShip();
        RefreshSlots();
    }

    private void RefreshSlots()
    {
        if (selectedShipsService == null || prefabFactory == null)
            return;

        for (int i = 0; i < shipSlots.Length; i++)
        {
            ShipSlotView slot = shipSlots[i];
            if (slot == null)
                continue;

            ShowShip(slot, selectedShipsService.GetSelectedShip(i));
            SetSlotColor(slot, i == targetSlotIndex ? selectedSlotColor : unselectedSlotColor);
        }

        if (shipSlots.Length == 0 && previewImage != null && targetSlotIndex >= 0)
            ShowShip(previewImage, previewName, selectedShipsService.GetSelectedShip(targetSlotIndex));
    }

    private void ShowShip(ShipSlotView slot, SaveShip ship)
    {
        ShowShip(slot.Icon, slot.NameText, ship);
    }

    private void ShowShip(Image image, TMP_Text nameText, SaveShip ship)
    {
        Sprite sprite = ship != null ? prefabFactory.GetShipIcon(ship.shipId) : emptyShipIcon;
        if (image != null)
        {
            image.sprite = sprite;
            image.enabled = sprite != null;
            image.color = Color.white;
            image.preserveAspect = sprite != null;
        }

        if (nameText != null)
            nameText.text = ship != null ? ship.shipName : string.Empty;
    }

    private void TryAssignSelectedShip()
    {
        ResolveProjectDependencies();

        SaveShip shipToAssign = selectedSavedShip ?? savedCraftList?.SelectedShip;
        if (shipToAssign == null || targetSlotIndex < 0 || selectedShipsService == null)
        {
            return;
        }

        selectedShipsService.AssignShipToSlot(targetSlotIndex, shipToAssign);

        if (targetSlotIndex >= shipSlots.Length || shipSlots[targetSlotIndex] == null)
            return;

        ShipSlotView assignedSlot = shipSlots[targetSlotIndex];
        ShowShip(assignedSlot, shipToAssign);
        SetSlotColor(assignedSlot, selectedSlotColor);

        Debug.Log(
            $"Корабль '{shipToAssign.shipName}' назначен в слот {targetSlotIndex + 1}.",
            this);

        selectedSavedShip = null;
        targetSlotIndex = -1;
        savedCraftList.ClearSelection();
    }

    private static void SetSlotColor(ShipSlotView slot, Color color)
    {
        Image image = slot.Icon;
        if (image != null)
            image.color = color;
    }

    private void RegisterSlotButtons()
    {
        slotHandlers = new UnityAction[shipSlots.Length];
        for (int i = 0; i < shipSlots.Length; i++)
        {
            ShipSlotView slot = shipSlots[i];
            if (slot == null || slot.Button == null)
                continue;

            int slotIndex = i;
            UnityAction handler = () => SelectTargetSlot(slotIndex);
            slotHandlers[i] = handler;
            slot.Button.onClick.AddListener(handler);
        }
    }

    private void UnregisterSlotButtons()
    {
        if (slotHandlers == null)
            return;

        for (int i = 0; i < slotHandlers.Length; i++)
        {
            ShipSlotView slot = shipSlots[i];
            if (slot != null && slot.Button != null && slotHandlers[i] != null)
                slot.Button.onClick.RemoveListener(slotHandlers[i]);
        }
    }

    private bool ValidateConfiguration()
    {
        ResolveProjectDependencies();
        ResolveSavedCraftList();

        if (selectedShipsService == null || prefabFactory == null)
        {
            Debug.LogError("Studio could not resolve its ProjectContext dependencies.", this);
            return false;
        }

        if (savedCraftList == null)
        {
            Debug.LogError("Studio requires a saved craft list.", this);
            return false;
        }

        return true;
    }

    private void ResolveSavedCraftList()
    {
        if (savedCraftList == null)
            savedCraftList = GetComponentInChildren<SavedCraftListController>(true);
    }

    private void ResolveProjectDependencies()
    {
        if (selectedShipsService != null && prefabFactory != null)
            return;

        ProjectContext projectContext = ProjectContext.Instance;
        if (projectContext == null)
            return;

        DiContainer container = projectContext.Container;
        if (selectedShipsService == null && container.HasBinding<TeamSelectionService>())
            selectedShipsService = container.Resolve<TeamSelectionService>();

        if (prefabFactory == null && container.HasBinding<PrefabFactory>())
            prefabFactory = container.Resolve<PrefabFactory>();
    }
}
