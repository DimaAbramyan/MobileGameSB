using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using Zenject;

public sealed class NewMainMenuLevelSelectionController : MonoBehaviour
{
    [Serializable]
    private sealed class ShipSlotView
    {
        [SerializeField] private Button button;
        [SerializeField] private Image icon;

        public Button Button => button;
        public Image Icon => icon != null
            ? icon
            : button != null ? button.image : null;
    }

    private static readonly List<NewMainMenuLevelSelectionController>
        SceneControllers = new();

    [Header("Level")]
    [SerializeField] private TMP_Text levelNumberText;
    [SerializeField] private string levelNumberFormat = "Уровень {0}";

    [Header("Equipment")]
    [SerializeField] private ShipSlotView[] shipSlots = Array.Empty<ShipSlotView>();
    [SerializeField] private Sprite emptyShipIcon;

    [Header("Buttons")]
    [SerializeField] private Button startButton;

    [Header("Navigation")]
    [SerializeField] private NewMainMenuTabsController tabsController;
    [SerializeField] private StudioShipSelectionController studioController;
    [SerializeField, Min(0)] private int studioTabIndex = 3;

    [InjectOptional] private TeamSelectionService selectedShipsService;
    [InjectOptional] private PrefabFactory prefabFactory;

    private UnityAction[] shipSlotHandlers;
    private LevelConfig selectedLevel;
    private LoadLevelConfig selectedLoader;
    private bool showRequested;

    public static bool TryGetSceneController(
        out NewMainMenuLevelSelectionController controller)
    {
        SceneControllers.RemoveAll(item => item == null);
        if (SceneControllers.Count > 0)
        {
            controller = SceneControllers[0];
            return true;
        }

        NewMainMenuLevelSelectionController[] candidates =
            Resources.FindObjectsOfTypeAll<NewMainMenuLevelSelectionController>();
        for (int index = 0; index < candidates.Length; index++)
        {
            NewMainMenuLevelSelectionController candidate = candidates[index];
            if (candidate == null || !candidate.gameObject.scene.IsValid())
                continue;

            SceneControllers.Add(candidate);
            controller = candidate;
            return true;
        }

        controller = null;
        return false;
    }

    private void Awake()
    {
        RegisterSceneController();
        ResolveDependencies();
        RegisterButtons();

        if (!showRequested)
            gameObject.SetActive(false);
    }

    private void OnEnable()
    {
        ResolveDependencies();
        if (selectedShipsService != null)
            selectedShipsService.Changed += RefreshEquipment;

        Refresh();
    }

    private void OnDisable()
    {
        if (selectedShipsService != null)
            selectedShipsService.Changed -= RefreshEquipment;
    }

    private void OnDestroy()
    {
        UnregisterButtons();
        SceneControllers.Remove(this);
    }

    public void Show(LevelConfig level, LoadLevelConfig loader)
    {
        if (level == null)
            return;

        showRequested = true;
        ResolveDependencies();

        if (tabsController != null)
            tabsController.OpenAdditionalWindow(gameObject);
        else
            gameObject.SetActive(true);

        selectedLevel = level;
        selectedLoader = loader;
        Refresh();
    }

    public void StartSelectedLevel()
    {
        if (selectedLoader == null)
        {
            Debug.LogError(
                "SelectLevel has no level loader for the selected level.",
                this);
            return;
        }

        selectedLoader.StartLevel();
    }

    private void Refresh()
    {
        if (selectedLevel != null && levelNumberText != null)
        {
            levelNumberText.text = string.Format(
                levelNumberFormat,
                selectedLevel.Id);
        }

        if (startButton != null)
            startButton.interactable = selectedLoader != null
                && selectedLoader.CanLoad();

        RefreshEquipment();
    }

    private void RefreshEquipment()
    {
        ResolveDependencies();
        if (selectedShipsService == null || prefabFactory == null)
            return;

        for (int index = 0; index < shipSlots.Length; index++)
        {
            ShipSlotView slot = shipSlots[index];
            if (slot == null)
                continue;

            SaveShip ship = selectedShipsService.GetSelectedShip(index);
            Sprite icon = ship != null
                ? prefabFactory.GetShipIcon(ship.shipId)
                : emptyShipIcon;
            Image image = slot.Icon;
            if (image == null)
                continue;

            image.sprite = icon;
            image.enabled = icon != null;
            image.color = Color.white;
            image.preserveAspect = icon != null;
        }
    }

    private void OpenStudioForShipSlot(int slotIndex)
    {
        ResolveDependencies();
        if (tabsController == null || studioController == null)
        {
            Debug.LogError(
                "SelectLevel requires menu tabs and Studio selection controllers.",
                this);
            return;
        }

        studioController.OpenForShipSlot(slotIndex);
        tabsController.NavigateToTab(studioTabIndex);
    }

    private void RegisterButtons()
    {
        startButton?.onClick.AddListener(StartSelectedLevel);

        shipSlotHandlers = new UnityAction[shipSlots.Length];
        for (int index = 0; index < shipSlots.Length; index++)
        {
            ShipSlotView slot = shipSlots[index];
            if (slot == null || slot.Button == null)
                continue;

            int slotIndex = index;
            UnityAction handler = () => OpenStudioForShipSlot(slotIndex);
            shipSlotHandlers[index] = handler;
            slot.Button.onClick.AddListener(handler);
        }
    }

    private void UnregisterButtons()
    {
        startButton?.onClick.RemoveListener(StartSelectedLevel);
        if (shipSlotHandlers == null)
            return;

        for (int index = 0; index < shipSlotHandlers.Length; index++)
        {
            ShipSlotView slot = shipSlots[index];
            if (slot != null && slot.Button != null
                && shipSlotHandlers[index] != null)
            {
                slot.Button.onClick.RemoveListener(shipSlotHandlers[index]);
            }
        }
    }

    private void RegisterSceneController()
    {
        if (!SceneControllers.Contains(this))
            SceneControllers.Add(this);
    }

    private void ResolveDependencies()
    {
        if (selectedShipsService == null || prefabFactory == null)
        {
            ProjectContext projectContext = ProjectContext.Instance;
            if (projectContext != null)
            {
                DiContainer container = projectContext.Container;
                if (selectedShipsService == null
                    && container.HasBinding<TeamSelectionService>())
                {
                    selectedShipsService = container.Resolve<TeamSelectionService>();
                }

                if (prefabFactory == null && container.HasBinding<PrefabFactory>())
                    prefabFactory = container.Resolve<PrefabFactory>();
            }
        }

        if (tabsController == null)
            tabsController = FindSceneComponent<NewMainMenuTabsController>();

        if (studioController == null)
            studioController = FindSceneComponent<StudioShipSelectionController>();
    }

    private T FindSceneComponent<T>() where T : Component
    {
        T[] candidates = Resources.FindObjectsOfTypeAll<T>();
        for (int index = 0; index < candidates.Length; index++)
        {
            T candidate = candidates[index];
            if (candidate != null && candidate.gameObject.scene == gameObject.scene)
                return candidate;
        }

        return null;
    }
}
