using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using Zenject;

// Legacy component name retained so the current scene keeps its script reference.
public sealed class TeamSelectionMenuController : MonoBehaviour
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

    [Header("Selected ships")]
    [SerializeField] private ShipSlotView[] shipSlots = Array.Empty<ShipSlotView>();
    [SerializeField] private Sprite emptyShipIcon;

    [Header("Windows")]
    [SerializeField] private GameObject mainMenuWindow;
    [SerializeField] private GameObject studioWindow;
    [SerializeField] private StudioShipSelectionController studioController;
    [SerializeField] private NewMainMenuTabsController tabsController;
    [SerializeField, Min(0)] private int studioTabIndex = 3;

    private TeamSelectionService selectedShipsService;
    private PrefabFactory prefabFactory;
    private UnityAction[] shipSlotHandlers;

    [Inject]
    private void Construct(TeamSelectionService selectedShipsService, PrefabFactory prefabFactory)
    {
        this.selectedShipsService = selectedShipsService;
        this.prefabFactory = prefabFactory;
    }

    private void Awake()
    {
        ResolveProjectDependencies();
    }

    private void Start()
    {
        if (!ValidateConfiguration())
            return;

        RegisterButtons();
        selectedShipsService.Changed += Refresh;
        Refresh();
    }

    private void OnDestroy()
    {
        if (selectedShipsService != null)
            selectedShipsService.Changed -= Refresh;

        UnregisterButtons();
    }

    public void Refresh()
    {
        if (selectedShipsService == null)
            return;

        for (int i = 0; i < shipSlots.Length; i++)
        {
            ShipSlotView slot = shipSlots[i];
            if (slot == null)
                continue;

            RefreshShipSlot(slot, selectedShipsService.GetSelectedShip(i));
        }
    }

    private void OpenStudioForShipSlot(int shipSlotIndex)
    {
        if (studioController == null)
            return;

        studioController.OpenForShipSlot(shipSlotIndex);

        tabsController.NavigateToTab(studioTabIndex);
    }

    private void RefreshShipSlot(ShipSlotView slot, SaveShip ship)
    {
        Sprite sprite = ship != null ? prefabFactory.GetShipIcon(ship.shipId) : emptyShipIcon;
        Image icon = slot.Icon;
        if (icon != null)
        {
            icon.sprite = sprite;
            icon.enabled = sprite != null;
            icon.color = Color.white;
            icon.preserveAspect = sprite != null;
        }

        if (slot.NameText != null)
            slot.NameText.text = ship != null ? ship.shipName : string.Empty;
    }

    private void RegisterButtons()
    {
        shipSlotHandlers = new UnityAction[shipSlots.Length];
        for (int i = 0; i < shipSlots.Length; i++)
        {
            ShipSlotView slot = shipSlots[i];
            if (slot == null || slot.Button == null)
                continue;

            int slotIndex = i;
            UnityAction handler = () => OpenStudioForShipSlot(slotIndex);
            shipSlotHandlers[i] = handler;
            slot.Button.onClick.AddListener(handler);
        }
    }

    private void UnregisterButtons()
    {
        if (shipSlotHandlers == null)
            return;

        for (int i = 0; i < shipSlotHandlers.Length; i++)
        {
            ShipSlotView slot = shipSlots[i];
            if (slot != null && slot.Button != null && shipSlotHandlers[i] != null)
                slot.Button.onClick.RemoveListener(shipSlotHandlers[i]);
        }
    }

    private bool ValidateConfiguration()
    {
        ResolveProjectDependencies();

        if (selectedShipsService == null || prefabFactory == null)
        {
            Debug.LogError("Selected ships menu could not resolve its ProjectContext dependencies.", this);
            return false;
        }

        if (shipSlots.Length != TeamSelectionService.SelectedShipCount)
        {
            Debug.LogError("Selected ships menu requires exactly two ship slots.", this);
            return false;
        }

        if (studioController == null)
        {
            Debug.LogError("Selected ships menu requires a Studio ship selection controller.", this);
            return false;
        }

        if (tabsController == null)
        {
            Debug.LogError("Selected ships menu requires a menu tabs controller.", this);
            return false;
        }

        return true;
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
