using System.Collections.Generic;
using UnityEngine;
using Zenject;

public sealed class WeaponSelectionListController : MonoBehaviour
{
    [SerializeField] private CraftUIButton craftButtonPrefab;
    [SerializeField] private Transform contentRoot;
    [SerializeField] private CraftCreationFlowController craftCreationFlow;

    private readonly List<CraftUIButton> createdButtons = new();
    private readonly Dictionary<CraftUIButton, WeaponContentDefinition> buttonWeapons = new();
    private ContentCatalogService catalogService;
    private ContentProgressService contentProgressService;
    private bool hasStarted;

    [Inject]
    private void Construct(
        ContentCatalogService catalogService,
        ContentProgressService contentProgressService)
    {
        this.catalogService = catalogService;
        this.contentProgressService = contentProgressService;
    }

    private void Start()
    {
        hasStarted = true;
        RegisterCraftCreationFlow();
        RegisterProgressService();
        Refresh();
    }

    private void OnEnable()
    {
        if (hasStarted)
            Refresh();
    }

    private void OnDestroy()
    {
        UnregisterCraftCreationFlow();
        UnregisterProgressService();
        ClearButtons();
    }

    public void Refresh()
    {
        ResolveDependencies();
        if (!ValidateConfiguration())
            return;

        ClearButtons();

        IReadOnlyList<WeaponContentDefinition> weapons = catalogService.Weapons;
        for (int i = 0; i < weapons.Count; i++)
        {
            WeaponContentDefinition weapon = weapons[i];
            if (weapon == null)
                continue;

            bool isOwned = contentProgressService.IsOwned(weapon);
            CraftUIButton button = Instantiate(craftButtonPrefab, contentRoot);
            button.SetContent(weapon);
            button.SetSelected(weapon == craftCreationFlow.FocusedWeapon);
            button.SetAvailability(isOwned);
            button.SetInteractable(CanSelectWeapon(weapon));
            button.SetClickAction(() => SelectWeapon(weapon));
            createdButtons.Add(button);
            buttonWeapons.Add(button, weapon);
        }
    }

    private void SelectWeapon(WeaponContentDefinition weapon)
    {
        if (contentProgressService.IsOwned(weapon))
            craftCreationFlow.SelectWeapon(weapon);
    }

    private void RefreshFocusVisuals()
    {
        for (int i = 0; i < createdButtons.Count; i++)
        {
            CraftUIButton button = createdButtons[i];
            if (button != null && buttonWeapons.TryGetValue(button, out WeaponContentDefinition buttonWeapon))
            {
                bool isOwned = contentProgressService.IsOwned(buttonWeapon);
                button.SetSelected(buttonWeapon == craftCreationFlow.FocusedWeapon);
                button.SetAvailability(isOwned);
                button.SetInteractable(CanSelectWeapon(buttonWeapon));
            }
        }
    }

    private bool CanSelectWeapon(WeaponContentDefinition weapon)
    {
        return contentProgressService != null
            && contentProgressService.IsOwned(weapon)
            && craftCreationFlow != null
            && craftCreationFlow.CanAssignWeaponToFocusedSlot(weapon);
    }

    private void RegisterCraftCreationFlow()
    {
        if (craftCreationFlow != null)
        {
            craftCreationFlow.WeaponFocusChanged += HandleWeaponFocusChanged;
            craftCreationFlow.WeaponSlotFocusChanged += HandleWeaponSlotFocusChanged;
        }
    }

    private void UnregisterCraftCreationFlow()
    {
        if (craftCreationFlow != null)
        {
            craftCreationFlow.WeaponFocusChanged -= HandleWeaponFocusChanged;
            craftCreationFlow.WeaponSlotFocusChanged -= HandleWeaponSlotFocusChanged;
        }
    }

    private void RegisterProgressService()
    {
        if (contentProgressService != null)
            contentProgressService.ProgressChanged += Refresh;
    }

    private void UnregisterProgressService()
    {
        if (contentProgressService != null)
            contentProgressService.ProgressChanged -= Refresh;
    }

    private void HandleWeaponFocusChanged(WeaponContentDefinition _)
    {
        RefreshFocusVisuals();
    }

    private void HandleWeaponSlotFocusChanged(string _)
    {
        RefreshFocusVisuals();
    }

    private void ClearButtons()
    {
        for (int i = 0; i < createdButtons.Count; i++)
        {
            CraftUIButton button = createdButtons[i];
            if (button != null)
                Destroy(button.gameObject);
        }

        createdButtons.Clear();
        buttonWeapons.Clear();
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
            if (hasStarted)
                RegisterProgressService();
        }
    }

    private bool ValidateConfiguration()
    {
        if (catalogService == null || contentProgressService == null)
        {
            Debug.LogError("Weapon selection could not resolve content services.", this);
            return false;
        }

        if (craftButtonPrefab == null || contentRoot == null || craftCreationFlow == null)
        {
            Debug.LogError("Configure the weapon selection list references.", this);
            return false;
        }

        return true;
    }
}
