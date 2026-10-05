using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using Zenject;

public enum HangarCatalogTab
{
    Hulls,
    Weapons
}

public sealed class HangarCatalogController : MonoBehaviour
{
    private sealed class ContentButtonBinding
    {
        public CraftContentDefinition Content;
        public CraftUIButton Button;
        public ContentProgressState State;
    }

    [Header("Tabs")]
    [SerializeField] private Button hullsTabButton;
    [SerializeField] private Button weaponsTabButton;
    [SerializeField] private Color inactiveTabColor = Color.white;
    [SerializeField] private Color activeTabColor = Color.blue;
    [SerializeField] private HangarCatalogTab initialTab = HangarCatalogTab.Hulls;

    [Header("Catalog List")]
    [SerializeField] private CraftUIButton contentButtonPrefab;
    [SerializeField] private Transform contentRoot;
    [SerializeField] private bool clearExistingContentOnStart = true;

    [Header("Catalog Layout")]
    [SerializeField, Min(1)] private int hullColumnCount = 3;
    [SerializeField, Min(1)] private int weaponColumnCount = 4;

    [Header("Details")]
    [SerializeField] private GameObject detailsRoot;
    [SerializeField] private Image selectedIcon;
    [SerializeField] private TMP_Text selectedNameText;
    [SerializeField] private TMP_Text passiveAbilityDescriptionText;
    [SerializeField] private TMP_Text activeAbilityDescriptionText;
    [SerializeField] private TMP_Text availabilityText;
    [SerializeField] private TMP_Text purchasePriceText;
    [SerializeField] private TMP_Text maxUpgradeLevelText;
    [SerializeField] private Button upgradeButton;
      [SerializeField] private TMP_Text actionButtonLabel;
    [SerializeField] private UnityEvent onUpgradeRequested = new();

      [Header("Content Info Views")]
      [SerializeField] private HullSelectionInfoController hullInfoView;
      [SerializeField] private WeaponSelectionInfoController weaponInfoView;

    private readonly List<ContentButtonBinding> buttonBindings = new();
    private ContentCatalogService catalogService;
    private ContentProgressService contentProgressService;
    private HangarCatalogTab activeTab;
    private CraftContentDefinition selectedContent;
    private ContentProgressState selectedState;
    private bool hasStarted;
    private bool hasClearedExistingContent;
    private bool isProgressSubscribed;
    private GridLayoutGroup catalogGrid;
    private bool hasCatalogCellAspect;
    private float catalogCellAspect = 1f;

    public HangarCatalogTab ActiveTab => activeTab;
    public CraftContentDefinition SelectedContent => selectedContent;
    public bool IsSelectedContentAvailable => selectedState.IsOwned;

    public event Action<CraftContentDefinition> ContentSelected;
    public event Action<CraftContentDefinition> UpgradeRequested;

    [Inject]
    private void Construct(
        ContentCatalogService catalogService,
        ContentProgressService contentProgressService)
    {
        this.catalogService = catalogService;
        this.contentProgressService = contentProgressService;
    }

    private void Awake()
    {
        RegisterButtons();
    }

    private void Start()
    {
        hasStarted = true;
        activeTab = initialTab;
        ResolveDependencies();
        RegisterProgressService();
        Refresh();
    }

    private void OnEnable()
    {
        if (hasStarted)
            Refresh();
    }

    private void OnRectTransformDimensionsChange()
    {
        if (hasStarted)
            ApplyCatalogGrid();
    }

    private void OnDestroy()
    {
        UnregisterButtons();
        UnregisterProgressService();
        ClearButtonBindings();
    }

    public void Refresh()
    {
        ResolveDependencies();
        if (!ValidateConfiguration())
            return;

        ApplyCatalogGrid();
        PopulateActiveTab();
        ApplyTabVisuals();
        RestoreSelectionForActiveTab();
    }

    public void ShowHulls()
    {
        SetActiveTab(HangarCatalogTab.Hulls);
    }

    public void ShowWeapons()
    {
        SetActiveTab(HangarCatalogTab.Weapons);
    }

    public void SelectContent(CraftContentDefinition content)
    {
        if (content == null || contentProgressService == null)
            return;

        selectedContent = content;
        selectedState = contentProgressService.GetState(content);
        RefreshButtonVisuals();
        RefreshDetails();
        ContentSelected?.Invoke(selectedContent);
    }

    public bool RequestSelectedAction()
    {
        if (selectedContent == null || contentProgressService == null)
            return false;

        if (selectedState.CanPurchase)
            return contentProgressService.TryPurchase(selectedContent);

        if (selectedState.CanUpgrade)
            return RequestUpgradeSelected();

        return false;
    }

    public bool RequestUpgradeSelected()
    {
        if (selectedContent == null || contentProgressService == null)
            return false;

        if (!contentProgressService.TryUpgrade(selectedContent))
            return false;

        UpgradeRequested?.Invoke(selectedContent);
        onUpgradeRequested?.Invoke();
        return true;
    }

    private void PopulateActiveTab()
    {
        ClearButtonBindings();
        ClearExistingContentIfNeeded();

        if (activeTab == HangarCatalogTab.Hulls)
        {
            Populate(catalogService.Hulls);
            return;
        }

        Populate(catalogService.Weapons);
    }

    private void ApplyCatalogGrid()
    {
        ApplyCatalogGrid(activeTab);
    }

    private void ApplyCatalogGrid(HangarCatalogTab tab)
    {
        if (contentRoot == null)
            return;

        catalogGrid ??= contentRoot.GetComponent<GridLayoutGroup>();
        if (catalogGrid == null || contentRoot is not RectTransform contentRect)
            return;

        if (!hasCatalogCellAspect)
        {
            catalogCellAspect = catalogGrid.cellSize.x > Mathf.Epsilon
                ? catalogGrid.cellSize.y / catalogGrid.cellSize.x
                : 1f;
            hasCatalogCellAspect = true;
        }

        int columnCount = tab == HangarCatalogTab.Hulls
            ? Mathf.Max(1, hullColumnCount)
            : Mathf.Max(1, weaponColumnCount);
        float availableWidth = contentRect.rect.width
            - catalogGrid.padding.left
            - catalogGrid.padding.right
            - catalogGrid.spacing.x * (columnCount - 1);
        if (availableWidth <= 0f)
            return;

        float cellWidth = availableWidth / columnCount;
        catalogGrid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        catalogGrid.constraintCount = columnCount;
        catalogGrid.cellSize = new Vector2(
            cellWidth,
            cellWidth * catalogCellAspect);
    }

    private void Populate<T>(IReadOnlyList<T> contents)
        where T : CraftContentDefinition
    {
        if (contents == null)
            return;

        for (int i = 0; i < contents.Count; i++)
        {
            T content = contents[i];
            if (content == null)
                continue;

            ContentProgressState state = contentProgressService.GetState(content);
            CraftUIButton button = Instantiate(contentButtonPrefab, contentRoot);
            button.SetContent(content);
            button.SetSelected(content == selectedContent);
            button.SetAvailability(state.IsOwned);

            CraftContentDefinition selectedByButton = content;
            button.SetClickAction(() => SelectContent(selectedByButton));
            buttonBindings.Add(new ContentButtonBinding
            {
                Content = content,
                Button = button,
                State = state
            });
        }
    }

    private void SetActiveTab(HangarCatalogTab tab)
    {
        activeTab = tab;
        if (!hasStarted)
            return;

        Refresh();
    }

    private void RestoreSelectionForActiveTab()
    {
        if (ContainsContent(buttonBindings, selectedContent))
        {
            SelectContent(selectedContent);
            return;
        }

        if (buttonBindings.Count > 0)
            SelectContent(buttonBindings[0].Content);
        else
            ClearSelection();
    }

    private void ApplyTabVisuals()
    {
        bool showHulls = activeTab == HangarCatalogTab.Hulls;
        SetButtonColor(hullsTabButton, showHulls ? activeTabColor : inactiveTabColor);
        SetButtonColor(weaponsTabButton, showHulls ? inactiveTabColor : activeTabColor);
    }

    private void RefreshButtonVisuals()
    {
        for (int i = 0; i < buttonBindings.Count; i++)
        {
            ContentButtonBinding binding = buttonBindings[i];
            if (binding == null || binding.Button == null || binding.Content == null)
                continue;

            binding.State = contentProgressService.GetState(binding.Content);
            binding.Button.SetSelected(binding.Content == selectedContent);
            binding.Button.SetAvailability(binding.State.IsOwned);
        }
    }

    private void RefreshDetails()
    {
        bool hasSelection = selectedContent != null;
        SetWindowActive(detailsRoot, hasSelection);
          SetInfoViewsActive(hasSelection);
        if (!hasSelection)
            return;

        if (selectedIcon != null)
        {
            selectedIcon.sprite = selectedContent.Icon;
            selectedIcon.enabled = selectedContent.Icon != null;
        }

        SetText(selectedNameText, selectedContent.DisplayName);
        SetText(
            passiveAbilityDescriptionText,
            selectedContent.PassiveAbilityDescription);
        SetText(
            activeAbilityDescriptionText,
            selectedContent.ActiveAbilityDescription);
        SetText(availabilityText, selectedState.Reason);
        SetText(purchasePriceText, GetSelectedActionCostText());
        SetText(
            maxUpgradeLevelText,
            selectedContent.MaxUpgradeLevel > 0
                ? $"Уровень улучшения: {selectedState.UpgradeLevel}/{selectedContent.MaxUpgradeLevel}"
                : "Улучшения не настроены");

        if (upgradeButton != null)
        {
            upgradeButton.interactable = selectedState.CanPurchase
                || selectedState.CanUpgrade;
        }

          SetText(actionButtonLabel, GetSelectedActionLabel());
          if (selectedContent is HullContentDefinition hull)
          {
              hullInfoView?.Show(hull);
              return;
          }

          if (selectedContent is WeaponContentDefinition weapon)
              weaponInfoView?.Show(weapon);
    }

      private void SetInfoViewsActive(bool hasSelection)
      {
          bool showHullInfo = hasSelection && selectedContent is HullContentDefinition;
          bool showWeaponInfo = hasSelection && selectedContent is WeaponContentDefinition;
          SetWindowActive(hullInfoView != null ? hullInfoView.gameObject : null, showHullInfo);
          SetWindowActive(weaponInfoView != null ? weaponInfoView.gameObject : null, showWeaponInfo);
      }

    private string GetSelectedActionCostText()
    {
        if (selectedState.CanPurchase)
            return $"Стоимость разблокировки: {selectedState.ActionCost.ToDisplayString()}";

        if (selectedState.CanUpgrade)
            return $"Стоимость улучшения: {selectedState.ActionCost.ToDisplayString()}";

        return string.Empty;
    }

      private string GetSelectedActionLabel()
      {
          if (selectedState.CanPurchase)
              return "Купить";

          if (selectedState.CanUpgrade)
              return "Улучшить";

          return selectedState.IsOwned
              ? "Макс. уровень"
              : "Недоступно";
      }

    private void ClearSelection()
    {
        selectedContent = null;
        selectedState = default;
        RefreshButtonVisuals();
        RefreshDetails();
        ContentSelected?.Invoke(null);
    }

    private static bool ContainsContent(
        List<ContentButtonBinding> bindings,
        CraftContentDefinition content)
    {
        if (content == null)
            return false;

        for (int i = 0; i < bindings.Count; i++)
        {
            ContentButtonBinding binding = bindings[i];
            if (binding != null && binding.Content == content)
                return true;
        }

        return false;
    }

    private void RegisterButtons()
    {
        if (hullsTabButton != null)
            hullsTabButton.onClick.AddListener(ShowHulls);

        if (weaponsTabButton != null)
            weaponsTabButton.onClick.AddListener(ShowWeapons);

        if (upgradeButton != null)
            upgradeButton.onClick.AddListener(RequestSelectedActionFromButton);
    }

    private void RequestSelectedActionFromButton()
    {
        RequestSelectedAction();
    }

    private void UnregisterButtons()
    {
        if (hullsTabButton != null)
            hullsTabButton.onClick.RemoveListener(ShowHulls);

        if (weaponsTabButton != null)
            weaponsTabButton.onClick.RemoveListener(ShowWeapons);

        if (upgradeButton != null)
            upgradeButton.onClick.RemoveListener(RequestSelectedActionFromButton);
    }

    private void RegisterProgressService()
    {
        if (isProgressSubscribed || contentProgressService == null)
            return;

        contentProgressService.ProgressChanged += HandleProgressChanged;
        isProgressSubscribed = true;
    }

    private void UnregisterProgressService()
    {
        if (!isProgressSubscribed || contentProgressService == null)
            return;

        contentProgressService.ProgressChanged -= HandleProgressChanged;
        isProgressSubscribed = false;
    }

    private void HandleProgressChanged()
    {
        if (hasStarted)
            Refresh();
    }

    private void ClearButtonBindings()
    {
        for (int i = 0; i < buttonBindings.Count; i++)
        {
            ContentButtonBinding binding = buttonBindings[i];
            if (binding?.Button != null)
                Destroy(binding.Button.gameObject);
        }

        buttonBindings.Clear();
    }

    private void ClearExistingContentIfNeeded()
    {
        if (hasClearedExistingContent || !clearExistingContentOnStart)
            return;

        for (int i = contentRoot.childCount - 1; i >= 0; i--)
            Destroy(contentRoot.GetChild(i).gameObject);

        hasClearedExistingContent = true;
    }

    private void ResolveDependencies()
    {
        if (catalogService != null && contentProgressService != null)
            return;

        ProjectContext projectContext = ProjectContext.Instance;
        if (projectContext == null)
            return;

        DiContainer container = projectContext.Container;
        if (catalogService == null && container.HasBinding<ContentCatalogService>())
            catalogService = container.Resolve<ContentCatalogService>();

        if (contentProgressService == null
            && container.HasBinding<ContentProgressService>())
        {
            contentProgressService = container.Resolve<ContentProgressService>();
            RegisterProgressService();
        }
    }

    private bool ValidateConfiguration()
    {
        if (catalogService == null || contentProgressService == null)
        {
            Debug.LogError("Hangar could not resolve its content services.", this);
            return false;
        }

        if (contentButtonPrefab == null || contentRoot == null)
        {
            Debug.LogError("Configure Hangar item prefab and content root.", this);
            return false;
        }

        return true;
    }

    private static void SetWindowActive(GameObject window, bool isActive)
    {
        if (window != null)
            window.SetActive(isActive);
    }

    private static void SetButtonColor(Button button, Color color)
    {
        if (button != null && button.image != null)
            button.image.color = color;
    }

    private static void SetText(TMP_Text text, string value)
    {
        if (text != null)
            text.text = value ?? string.Empty;
    }
}
