using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Zenject;

public sealed class SavedCraftListController : MonoBehaviour
{
    public const string NoFocusedShipName = "none";

    [SerializeField] private CraftUIButton craftPrefab;
    [SerializeField] private CraftUIButton craftNewShipPrefab;
    [SerializeField] private CraftCreationFlowController craftCreationFlow;
    [SerializeField] private Transform contentRoot;
    [SerializeField] private TMPro.TMP_Text focusedShipNameText;

    private readonly List<CraftUIButton> createdCrafts = new List<CraftUIButton>();
    private CraftUIButton createdNewShipButton;

    private SaveManager saveManager;
    private PrefabFactory prefabFactory;
    private bool hasStarted;
    private string selectedShipName;
    private SaveShip selectedShip;
    private string focusedShipName;
    private SaveShip focusedShip;

    public event Action<SaveShip> ShipSelected;
    public event Action<SaveShip> FocusedShipChanged;
    public SaveShip SelectedShip => selectedShip;

    public string GetFocusedShipName()
    {
        return string.IsNullOrEmpty(focusedShipName)
            ? NoFocusedShipName
            : focusedShipName;
    }

    public void SetFocusedShip(SaveShip ship)
    {
        focusedShip = ship;
        focusedShipName = ship != null ? ship.shipName : null;
        RefreshFocusedShipNameText();
        FocusedShipChanged?.Invoke(focusedShip);
    }

    [Inject]
    private void Construct(SaveManager saveManager, PrefabFactory prefabFactory)
    {
        this.saveManager = saveManager;
        this.prefabFactory = prefabFactory;
    }

    private void Awake()
    {
        ResolveProjectDependencies();
    }

    private void ResolveProjectDependencies()
    {
        if (saveManager != null && prefabFactory != null)
            return;

        ProjectContext projectContext = ProjectContext.Instance;
        if (projectContext == null)
            return;

        DiContainer container = projectContext.Container;
        if (saveManager == null && container.HasBinding<SaveManager>())
            saveManager = container.Resolve<SaveManager>();

        if (prefabFactory == null && container.HasBinding<PrefabFactory>())
            prefabFactory = container.Resolve<PrefabFactory>();
    }

    private void Start()
    {
        hasStarted = true;
        Refresh();
    }

    private void OnEnable()
    {
        if (hasStarted)
            Refresh();
    }

    public void Refresh()
    {
        if (!ValidateConfiguration())
            return;

        ClearCreatedCrafts();
        saveManager.LoadAllSaves();

        selectedShip = null;
        CraftUIButton selectedCraftButton = null;
        SaveShip refreshedFocusedShip = null;
        IReadOnlyList<SaveShip> savedShips = saveManager.SavedShips;
        for (int i = 0; i < savedShips.Count; i++)
        {
            SaveShip savedShip = savedShips[i];
            if (savedShip == null)
                continue;

            CraftUIButton craftButton = Instantiate(craftPrefab, contentRoot);
            craftButton.SetShip(savedShip.shipName, prefabFactory.GetShipIcon(savedShip.shipId));
            SaveShip shipToSelect = savedShip;
            bool isSelected = savedShip.shipName == selectedShipName;
            craftButton.SetSelected(isSelected);
            craftButton.SetClickAction(() => SelectCraft(shipToSelect, craftButton));
            createdCrafts.Add(craftButton);

            if (isSelected)
            {
                selectedShip = savedShip;
                selectedCraftButton = craftButton;
            }

            if (savedShip.shipName == focusedShipName)
                refreshedFocusedShip = savedShip;
        }

        if (selectedShipName != null && selectedShip == null)
            selectedShipName = null;
        SetSelectedCraft(selectedCraftButton);

        if (focusedShipName != null)
            SetFocusedShip(refreshedFocusedShip);

        CreateNewShipButton();
    }

    public void ClearSelection()
    {
        selectedShipName = null;
        selectedShip = null;
        SetSelectedCraft(null);
        SetFocusedShip(null);
    }

    public void EditSelectedCraft()
    {
        EditCraft(selectedShip ?? focusedShip);
    }

    public void EditCraft(SaveShip ship)
    {
        if (ship == null)
        {
            Debug.LogWarning("Select a saved craft before opening the editor.", this);
            return;
        }

        if (craftCreationFlow == null)
        {
            Debug.LogError("Saved craft list requires a craft creation flow to edit a craft.", this);
            return;
        }

        craftCreationFlow.BeginEditingCraft(ship);
    }

    private void SelectCraft(SaveShip ship, CraftUIButton selectedButton)
    {
        if (ship == null)
            return;

        if (selectedShipName == ship.shipName)
        {
            ClearSelection();
            ShipSelected?.Invoke(null);
            return;
        }

        selectedShipName = ship.shipName;
        selectedShip = ship;
        SetSelectedCraft(selectedButton);
        SetFocusedShip(ship);
        ShipSelected?.Invoke(ship);
    }

    private void SetSelectedCraft(CraftUIButton selectedButton)
    {
        for (int i = 0; i < createdCrafts.Count; i++)
        {
            CraftUIButton craftButton = createdCrafts[i];
            if (craftButton != null)
                craftButton.SetSelected(craftButton == selectedButton);
        }
    }

    private void ClearCreatedCrafts()
    {
        if (createdNewShipButton != null)
            Destroy(createdNewShipButton.gameObject);

        createdNewShipButton = null;

        for (int i = 0; i < createdCrafts.Count; i++)
        {
            CraftUIButton craftButton = createdCrafts[i];
            if (craftButton != null)
                Destroy(craftButton.gameObject);
        }

        createdCrafts.Clear();
    }

    private void CreateNewShipButton()
    {
        createdNewShipButton = Instantiate(craftNewShipPrefab, contentRoot);
        createdNewShipButton.SetSelected(false);
        createdNewShipButton.SetClickAction(craftCreationFlow.BeginNewCraft);
        createdNewShipButton.transform.SetAsFirstSibling();
    }

    private void RefreshFocusedShipNameText()
    {
        if (focusedShipNameText != null)
            focusedShipNameText.text = GetFocusedShipName();
    }

    private bool ValidateConfiguration()
    {
        ResolveProjectDependencies();

        if (craftPrefab == null)
        {
            Debug.LogError("Saved craft list requires a Craft prefab.", this);
            return false;
        }

        if (contentRoot == null)
        {
            Debug.LogError("Saved craft list requires a content root.", this);
            return false;
        }

        if (craftNewShipPrefab == null)
        {
            Debug.LogError("Saved craft list requires a CraftNewShip prefab.", this);
            return false;
        }

        if (craftCreationFlow == null)
        {
            Debug.LogError("Saved craft list requires a craft creation flow.", this);
            return false;
        }

        if (saveManager == null)
        {
            Debug.LogError("Saved craft list could not resolve SaveManager from ProjectContext.", this);
            return false;
        }

        if (prefabFactory == null)
        {
            Debug.LogError("Saved craft list could not resolve PrefabFactory from ProjectContext.", this);
            return false;
        }

        return true;
    }
}
