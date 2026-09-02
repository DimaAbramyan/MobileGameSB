using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Zenject;

public sealed class CraftCreationFlowController : MonoBehaviour
{
    [SerializeField] private GameObject selectBody;
    [SerializeField] private GameObject selectWeapon;
    [SerializeField] private GameObject selectColour;
    [SerializeField] private NewMainMenuTabsController menuTabsController;
    [SerializeField] private HullPreviewController hullPreview;
    [SerializeField] private ShipColorPaletteSelectionController colorPicker;
    [SerializeField] private Button goToWeaponStageButton;
    [SerializeField] private Button goToBodyStageButton;
    [SerializeField] private Button goToColourStageButton;
    [SerializeField] private Button goToWeaponFromColourStageButton;

    private ContentProgressService contentProgressService;
    private ContentCatalogService contentCatalogService;
    private PrefabFactory prefabFactory;

    [Inject]
    private void Construct(
        ContentProgressService contentProgressService,
        ContentCatalogService contentCatalogService,
        PrefabFactory prefabFactory)
    {
        this.contentProgressService = contentProgressService;
        this.contentCatalogService = contentCatalogService;
        this.prefabFactory = prefabFactory;
    }

    private void Awake()
    {
        ResolveDependencies();
        RegisterNavigationButtons();
    }

    public HullContentDefinition SelectedHull { get; private set; }
    public IReadOnlyList<WeaponContentDefinition> SelectedWeapons => selectedWeapons;
    public int WeaponSlotCount => weaponSlotIds.Count;
    public string FocusedWeaponSlotId => focusedWeaponSlotId;
    public WeaponContentDefinition FocusedWeapon => focusedWeapon;
    public bool IsCreatingNewCraft { get; private set; }
    public bool IsEditingCraft => !string.IsNullOrEmpty(editingCraftName);
    public string EditingCraftName => editingCraftName;
    public ShipColorPalette SelectedColorPalette => selectedColorPalette != null
        ? selectedColorPalette.Clone()
        : null;

    public event Action<string> WeaponSlotFocusChanged;
    public event Action<WeaponContentDefinition> WeaponFocusChanged;
    public event Action<string, WeaponContentDefinition> WeaponAssignmentChanged;
    public event Action<HullContentDefinition> HullSelectionChanged;

    private readonly List<WeaponContentDefinition> selectedWeapons = new();
    private readonly Dictionary<string, WeaponContentDefinition> weaponsBySlot = new();
    private readonly Dictionary<string, Vector3> weaponSlotPositionsById = new();
    private readonly Dictionary<string, int> weaponSlotMaxTiersById = new();
    private readonly List<string> weaponSlotIds = new();
    private readonly List<string> slotsToRemove = new();
    private ShipColorPalette selectedColorPalette;
    private string editingCraftName;
    private string focusedWeaponSlotId;
    private WeaponContentDefinition focusedWeapon;

    private void OnDestroy()
    {
        UnregisterNavigationButtons();
        UnregisterColorPicker();
    }

    private void OnDisable()
    {
        IsCreatingNewCraft = false;
        ClearFocus();
    }

    public void BeginNewCraft()
    {
        IsCreatingNewCraft = true;
        editingCraftName = null;
        OpenCraftMenu();

        SelectedHull = null;
        ClearSelectedWeapons();
        ClearWeaponSlots();
        selectedColorPalette = null;
        hullPreview?.Clear();
        colorPicker?.SetPalette(new ShipColorPalette());
        HullSelectionChanged?.Invoke(null);
        ShowBodySelection();
    }

    public bool BeginEditingCraft(SaveShip ship)
    {
        ResolveDependencies();
        if (ship == null)
        {
            Debug.LogWarning("Cannot edit craft because no saved craft is selected.", this);
            return false;
        }

        if (!TryResolveSavedHull(ship, out HullContentDefinition hull))
        {
            Debug.LogWarning(
                $"Cannot edit craft '{ship.shipName}': its saved hull is not in the hull catalog.",
                this);
            return false;
        }

        if (!IsContentOwned(hull))
        {
            Debug.LogWarning(
                $"Cannot edit craft '{ship.shipName}': hull '{hull.DisplayName}' is not owned.",
                this);
            return false;
        }

        IsCreatingNewCraft = true;
        editingCraftName = ship.shipName;
        OpenCraftMenu();

        ShipColorPalette palette = ship.colors != null
            ? ship.colors.Clone()
            : hull.DefaultColorPalette;
        ApplyHullSelection(hull, palette);

        if (!TryRestoreSavedWeapons(ship, out string error))
        {
            Debug.LogWarning(
                $"Craft '{ship.shipName}' was opened with incomplete weapon loadout: {error}",
                this);
        }

        ShowBodySelection();
        return true;
    }

    public void CompleteNewCraft()
    {
        IsCreatingNewCraft = false;
        editingCraftName = null;
        ClearFocus();

        if (menuTabsController != null)
            menuTabsController.CloseCraftMenu();
        else
            gameObject.SetActive(false);
    }

    public void SetSelectedHull(HullContentDefinition hull)
    {
        if (hull != null && !IsContentOwned(hull))
        {
            Debug.LogWarning(
                $"Cannot select hull '{hull.DisplayName}': it is not owned.",
                this);
            return;
        }

        ApplyHullSelection(hull, hull != null ? hull.DefaultColorPalette : null);
    }

    private void ApplyHullSelection(HullContentDefinition hull, ShipColorPalette palette)
    {
        IsCreatingNewCraft = true;
        SelectedHull = hull;
        ClearSelectedWeapons();
        ClearWeaponSlots();
        selectedColorPalette = palette != null
            ? palette.Clone()
            : hull != null
                ? hull.DefaultColorPalette
                : new ShipColorPalette();
        hullPreview?.Show(hull, selectedColorPalette, this);
        colorPicker?.SetPalette(selectedColorPalette);
        HullSelectionChanged?.Invoke(SelectedHull);
    }

    private bool TryResolveSavedHull(SaveShip ship, out HullContentDefinition hull)
    {
        hull = null;
        if (contentCatalogService == null)
            return false;

        if (!string.IsNullOrWhiteSpace(ship.hullContentId))
            return contentCatalogService.TryGetHull(ship.hullContentId, out hull);

        return contentCatalogService.TryGetHullByShipId(ship.shipId, out hull);
    }

    private bool TryRestoreSavedWeapons(SaveShip ship, out string error)
    {
        if (ship.weaponData == null || ship.weaponData.Length == 0)
        {
            error = string.Empty;
            return true;
        }

        for (int i = 0; i < ship.weaponData.Length; i++)
        {
            WeaponDataSer savedWeapon = ship.weaponData[i];
            if (savedWeapon == null)
            {
                error = $"Weapon entry {i + 1} is empty.";
                return false;
            }

            if (!TryResolveSavedWeapon(savedWeapon, out WeaponContentDefinition weapon))
            {
                error = $"Weapon entry {i + 1} cannot be resolved from the content catalog.";
                return false;
            }

            string slotId = !string.IsNullOrWhiteSpace(savedWeapon.slotId)
                ? savedWeapon.slotId
                : i < weaponSlotIds.Count
                    ? weaponSlotIds[i]
                    : null;
            if (string.IsNullOrWhiteSpace(slotId) || !weaponSlotIds.Contains(slotId))
            {
                error = $"Weapon '{weapon.DisplayName}' references an unavailable slot.";
                return false;
            }

            if (weaponsBySlot.ContainsKey(slotId))
            {
                error = $"Several weapons reference slot '{slotId}'.";
                return false;
            }

            if (!AssignWeaponToSlot(slotId, weapon))
            {
                error = $"Weapon '{weapon.DisplayName}' cannot be assigned to slot '{slotId}'.";
                return false;
            }
        }

        error = string.Empty;
        return true;
    }

    private bool TryResolveSavedWeapon(
        WeaponDataSer savedWeapon,
        out WeaponContentDefinition weapon)
    {
        weapon = null;
        if (contentCatalogService == null)
            return false;

        if (!string.IsNullOrWhiteSpace(savedWeapon.contentId)
            && contentCatalogService.TryGetWeapon(savedWeapon.contentId, out weapon))
        {
            return true;
        }

        GameObject legacyPrefab = prefabFactory != null
            ? prefabFactory.GetWeapon(savedWeapon.ID)
            : null;
        return contentCatalogService.TryGetWeaponByPrefab(legacyPrefab, out weapon);
    }

    private void OpenCraftMenu()
    {
        if (menuTabsController != null)
            menuTabsController.OpenCraftMenu();
        else
            gameObject.SetActive(true);
    }

    public void SetSelectedWeapons(IReadOnlyList<WeaponContentDefinition> weapons)
    {
        ClearSelectedWeapons();

        if (weapons == null)
            return;

        for (int i = 0; i < weapons.Count; i++)
            AddSelectedWeapon(weapons[i]);
    }

    public void AddSelectedWeapon(WeaponContentDefinition weapon)
    {
        if (weapon == null)
            return;

        if (!IsContentOwned(weapon))
        {
            Debug.LogWarning(
                $"Cannot add weapon '{weapon.DisplayName}': it is not owned.",
                this);
            return;
        }

        IsCreatingNewCraft = true;
        bool hasFreeSlot = false;

        for (int i = 0; i < weaponSlotIds.Count; i++)
        {
            string slotId = weaponSlotIds[i];
            if (weaponsBySlot.ContainsKey(slotId))
                continue;

            hasFreeSlot = true;
            if (!CanAssignWeaponToSlot(slotId, weapon))
                continue;

            if (AssignWeaponToSlot(slotId, weapon))
                return;
        }

        string reason = hasFreeSlot
            ? $"Cannot add weapon '{weapon.DisplayName}': there is no compatible free weapon platform."
            : "Cannot add a weapon because the selected hull has no free weapon slot.";
        Debug.LogWarning(reason, this);
    }

    public bool ToggleSelectedWeapon(WeaponContentDefinition weapon)
    {
        if (weapon == null)
            return false;

        return SelectWeapon(weapon);
    }

    public bool IsWeaponSelected(WeaponContentDefinition weapon)
    {
        return weapon != null && selectedWeapons.Contains(weapon);
    }

    public void ClearSelectedWeapons()
    {
        slotsToRemove.Clear();
        foreach (KeyValuePair<string, WeaponContentDefinition> pair in weaponsBySlot)
            slotsToRemove.Add(pair.Key);

        for (int i = 0; i < slotsToRemove.Count; i++)
        {
            string slotId = slotsToRemove[i];
            weaponsBySlot.Remove(slotId);
            WeaponAssignmentChanged?.Invoke(slotId, null);
        }

        slotsToRemove.Clear();
        selectedWeapons.Clear();
        ClearFocus();
    }

    public void SetWeaponSlots(IReadOnlyList<string> slotIds)
    {
        weaponSlotIds.Clear();
        weaponSlotPositionsById.Clear();
        weaponSlotMaxTiersById.Clear();

        if (slotIds != null)
        {
            for (int i = 0; i < slotIds.Count; i++)
            {
                string slotId = slotIds[i];
                if (!string.IsNullOrWhiteSpace(slotId) && !weaponSlotIds.Contains(slotId))
                {
                    weaponSlotIds.Add(slotId);
                    weaponSlotPositionsById.Add(slotId, Vector3.zero);
                    weaponSlotMaxTiersById.Add(slotId, 1);
                }
            }
        }

        RemoveWeaponsOutsideConfiguredSlots();
    }

    public void SetWeaponSlotDefinitions(IReadOnlyList<CraftWeaponSlotDefinition> slotDefinitions)
    {
        weaponSlotIds.Clear();
        weaponSlotPositionsById.Clear();
        weaponSlotMaxTiersById.Clear();

        if (slotDefinitions != null)
        {
            for (int i = 0; i < slotDefinitions.Count; i++)
            {
                CraftWeaponSlotDefinition definition = slotDefinitions[i];
                if (string.IsNullOrWhiteSpace(definition.SlotId)
                    || weaponSlotPositionsById.ContainsKey(definition.SlotId))
                {
                    continue;
                }

                weaponSlotIds.Add(definition.SlotId);
                weaponSlotPositionsById.Add(definition.SlotId, definition.LocalPosition);
                weaponSlotMaxTiersById.Add(
                    definition.SlotId,
                    Mathf.Max(1, definition.MaxWeaponTier));
            }
        }

        RemoveWeaponsOutsideConfiguredSlots();
    }

    public void ClearWeaponSlots()
    {
        weaponSlotIds.Clear();
        weaponSlotPositionsById.Clear();
        weaponSlotMaxTiersById.Clear();
        RemoveWeaponsOutsideConfiguredSlots();
    }

    private void RemoveWeaponsOutsideConfiguredSlots()
    {
        slotsToRemove.Clear();
        foreach (KeyValuePair<string, WeaponContentDefinition> pair in weaponsBySlot)
        {
            if (!weaponSlotIds.Contains(pair.Key)
                || !CanAssignWeaponToSlot(pair.Key, pair.Value))
            slotsToRemove.Add(pair.Key);
        }

        for (int i = 0; i < slotsToRemove.Count; i++)
        {
            string slotId = slotsToRemove[i];
            weaponsBySlot.Remove(slotId);
            WeaponAssignmentChanged?.Invoke(slotId, null);
        }

        slotsToRemove.Clear();
        RefreshSelectedWeapons();

        if (!string.IsNullOrEmpty(focusedWeaponSlotId)
            && !weaponSlotIds.Contains(focusedWeaponSlotId))
        {
            ClearFocus();
        }
        else if (!string.IsNullOrEmpty(focusedWeaponSlotId))
        {
            WeaponSlotFocusChanged?.Invoke(focusedWeaponSlotId);
        }
    }

    public bool SelectWeaponSlot(string slotId)
    {
        if (string.IsNullOrWhiteSpace(slotId) || !weaponSlotIds.Contains(slotId))
        {
            Debug.LogWarning($"Weapon slot '{slotId}' is not configured for the selected hull.", this);
            return false;
        }

        if (focusedWeaponSlotId == slotId)
        {
            ClearFocus();
            return false;
        }

        if (focusedWeapon == null && !string.IsNullOrEmpty(focusedWeaponSlotId))
        {
            if (SwapWeaponsBetweenSlots(focusedWeaponSlotId, slotId))
            {
                ClearFocus();
                return true;
            }

            return false;
        }

        focusedWeaponSlotId = slotId;
        WeaponSlotFocusChanged?.Invoke(focusedWeaponSlotId);

        if (focusedWeapon != null)
            AssignFocusedWeaponToSlot();

        return true;
    }

    public bool SelectWeapon(WeaponContentDefinition weapon)
    {
        if (weapon == null)
        {
            SetFocusedWeapon(null);
            return false;
        }

        if (!IsContentOwned(weapon))
        {
            Debug.LogWarning(
                $"Cannot select weapon '{weapon.DisplayName}': it is not owned.",
                this);
            return false;
        }

        IsCreatingNewCraft = true;

        if (focusedWeapon == weapon && string.IsNullOrEmpty(focusedWeaponSlotId))
        {
            SetFocusedWeapon(null);
            return false;
        }

        SetFocusedWeapon(weapon);

        if (!string.IsNullOrEmpty(focusedWeaponSlotId))
            AssignFocusedWeaponToSlot();

        return true;
    }

    public WeaponContentDefinition GetWeaponForSlot(string slotId)
    {
        if (string.IsNullOrWhiteSpace(slotId))
            return null;

        return weaponsBySlot.TryGetValue(slotId, out WeaponContentDefinition weapon)
            ? weapon
            : null;
    }

    public int GetMaxWeaponTierForSlot(string slotId)
    {
        return !string.IsNullOrWhiteSpace(slotId)
            && weaponSlotMaxTiersById.TryGetValue(slotId, out int tier)
            ? Mathf.Max(1, tier)
            : 1;
    }

    public bool CanAssignWeaponToFocusedSlot(WeaponContentDefinition weapon)
    {
        return IsContentOwned(weapon)
            && (string.IsNullOrEmpty(focusedWeaponSlotId)
                || CanAssignWeaponToSlot(focusedWeaponSlotId, weapon));
    }

    public bool CanAssignWeaponToSlot(string slotId, WeaponContentDefinition weapon)
    {
        return TryGetWeaponAssignmentError(slotId, weapon, out _);
    }

    public bool TryGetWeaponAssignmentError(
        string slotId,
        WeaponContentDefinition weapon,
        out string error)
    {
        if (weapon == null)
        {
            error = "Не выбрано оружие.";
            return false;
        }

        if (!IsContentOwned(weapon))
        {
            error = $"Оружие '{weapon.DisplayName}' ещё не принадлежит игроку.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(slotId) || !weaponSlotIds.Contains(slotId))
        {
            error = $"Слот оружия '{slotId}' не настроен для выбранного корпуса.";
            return false;
        }

        return ShipBuildValidator.TryValidateWeaponPlatformTier(
            weapon,
            slotId,
            GetMaxWeaponTierForSlot(slotId),
            out error);
    }

    public bool TryValidateWeaponPlatformTiers(out string error)
    {
        for (int i = 0; i < weaponSlotIds.Count; i++)
        {
            string slotId = weaponSlotIds[i];
            if (!weaponsBySlot.TryGetValue(slotId, out WeaponContentDefinition weapon)
                || weapon == null)
            {
                continue;
            }

            if (!TryGetWeaponAssignmentError(slotId, weapon, out error))
                return false;
        }

        error = string.Empty;
        return true;
    }

    public bool TryCreateWeaponSaveData(out WeaponDataSer[] weapons, out string error)
    {
        if (weaponSlotIds.Count == 0)
        {
            weapons = System.Array.Empty<WeaponDataSer>();
            error = string.Empty;
            return true;
        }

        if (!TryValidateWeaponPlatformTiers(out error))
        {
            weapons = null;
            return false;
        }

        weapons = new WeaponDataSer[weaponSlotIds.Count];
        for (int i = 0; i < weaponSlotIds.Count; i++)
        {
            string slotId = weaponSlotIds[i];
            if (!weaponsBySlot.TryGetValue(slotId, out WeaponContentDefinition weapon)
                || weapon == null)
            {
                error = $"Не выбрано оружие для слота '{slotId}'.";
                weapons = null;
                return false;
            }

            if (!IsContentOwned(weapon))
            {
                error = $"Оружие '{weapon.DisplayName}' ещё не принадлежит игроку.";
                weapons = null;
                return false;
            }

            if (weapon.Data == null)
            {
                error = $"Для оружия '{weapon.DisplayName}' не назначен WeaponData.";
                weapons = null;
                return false;
            }

            if (string.IsNullOrWhiteSpace(weapon.Id))
            {
                error = $"Для оружия '{weapon.DisplayName}' не задан content id.";
                weapons = null;
                return false;
            }

            if (!weaponSlotPositionsById.TryGetValue(slotId, out Vector3 localPosition))
            {
                error = $"Для слота '{slotId}' не задана позиция оружия.";
                weapons = null;
                return false;
            }

            weapons[i] = new WeaponDataSer(
                -1,
                localPosition,
                weapon.Data.EnergyCost,
                weapon.Id,
                true,
                slotId);
        }

        error = string.Empty;
        return true;
    }

    private bool AssignFocusedWeaponToSlot()
    {
        if (string.IsNullOrEmpty(focusedWeaponSlotId) || focusedWeapon == null)
            return false;

        if (!AssignWeaponToSlot(focusedWeaponSlotId, focusedWeapon))
            return false;

        ClearFocus();
        return true;
    }

    private bool AssignWeaponToSlot(string slotId, WeaponContentDefinition weapon)
    {
        if (string.IsNullOrEmpty(slotId) || weapon == null)
            return false;

        if (!TryGetWeaponAssignmentError(slotId, weapon, out string error))
        {
            Debug.LogWarning(error, this);
            return false;
        }

        weaponsBySlot[slotId] = weapon;
        RefreshSelectedWeapons();
        WeaponAssignmentChanged?.Invoke(slotId, weapon);
        return true;
    }

    private bool SwapWeaponsBetweenSlots(string firstSlotId, string secondSlotId)
    {
        bool hasFirstWeapon = weaponsBySlot.TryGetValue(
            firstSlotId,
            out WeaponContentDefinition firstWeapon);
        bool hasSecondWeapon = weaponsBySlot.TryGetValue(
            secondSlotId,
            out WeaponContentDefinition secondWeapon);

        if (hasFirstWeapon
            && !TryGetWeaponAssignmentError(secondSlotId, firstWeapon, out string firstError))
        {
            Debug.LogWarning(firstError, this);
            return false;
        }

        if (hasSecondWeapon
            && !TryGetWeaponAssignmentError(firstSlotId, secondWeapon, out string secondError))
        {
            Debug.LogWarning(secondError, this);
            return false;
        }

        if (hasSecondWeapon)
            weaponsBySlot[firstSlotId] = secondWeapon;
        else
            weaponsBySlot.Remove(firstSlotId);

        if (hasFirstWeapon)
            weaponsBySlot[secondSlotId] = firstWeapon;
        else
            weaponsBySlot.Remove(secondSlotId);

        RefreshSelectedWeapons();
        WeaponAssignmentChanged?.Invoke(firstSlotId, secondWeapon);
        WeaponAssignmentChanged?.Invoke(secondSlotId, firstWeapon);
        return true;
    }

    private bool IsContentOwned(CraftContentDefinition content)
    {
        ResolveDependencies();
        return content != null
            && contentProgressService != null
            && contentProgressService.IsOwned(content);
    }

    private void ResolveDependencies()
    {
        if (contentProgressService != null
            && contentCatalogService != null
            && prefabFactory != null)
        {
            return;
        }

        ProjectContext projectContext = ProjectContext.Instance;
        if (projectContext == null)
            return;

        DiContainer container = projectContext.Container;
        if (contentProgressService == null && container.HasBinding<ContentProgressService>())
            contentProgressService = container.Resolve<ContentProgressService>();

        if (contentCatalogService == null && container.HasBinding<ContentCatalogService>())
            contentCatalogService = container.Resolve<ContentCatalogService>();

        if (prefabFactory == null && container.HasBinding<PrefabFactory>())
            prefabFactory = container.Resolve<PrefabFactory>();
    }

    private void RefreshSelectedWeapons()
    {
        selectedWeapons.Clear();

        for (int i = 0; i < weaponSlotIds.Count; i++)
        {
            string slotId = weaponSlotIds[i];
            if (weaponsBySlot.TryGetValue(slotId, out WeaponContentDefinition weapon)
                && weapon != null)
            {
                selectedWeapons.Add(weapon);
            }
        }
    }

    private void ClearFocus()
    {
        bool hadFocusedSlot = !string.IsNullOrEmpty(focusedWeaponSlotId);
        bool hadFocusedWeapon = focusedWeapon != null;

        focusedWeaponSlotId = null;
        focusedWeapon = null;

        if (hadFocusedSlot)
            WeaponSlotFocusChanged?.Invoke(null);

        if (hadFocusedWeapon)
            WeaponFocusChanged?.Invoke(null);
    }

    private void SetFocusedWeapon(WeaponContentDefinition weapon)
    {
        if (focusedWeapon == weapon)
            return;

        focusedWeapon = weapon;
        WeaponFocusChanged?.Invoke(focusedWeapon);
    }

    public void ShowBodySelection()
    {
        ShowStep(selectBody);
    }

    public void ShowWeaponSelection()
    {
        ShowStep(selectWeapon);
    }

    public void ShowColourSelection()
    {
        ShowStep(selectColour);
    }

    public void ShowNextStep()
    {
        if (selectBody != null && selectBody.activeSelf)
            ShowWeaponSelection();
        else if (selectWeapon != null && selectWeapon.activeSelf)
            ShowColourSelection();
    }

    public void ShowPreviousStep()
    {
        if (selectColour != null && selectColour.activeSelf)
            ShowWeaponSelection();
        else if (selectWeapon != null && selectWeapon.activeSelf)
            ShowBodySelection();
    }

    private void ShowStep(GameObject step)
    {
        if (step == null)
        {
            Debug.LogError("Craft creation flow has an unassigned step.", this);
            return;
        }

        SetStepActive(selectBody, step == selectBody);
        SetStepActive(selectWeapon, step == selectWeapon);
        SetStepActive(selectColour, step == selectColour);
    }

    private static void SetStepActive(GameObject step, bool isActive)
    {
        if (step != null)
            step.SetActive(isActive);
    }

    private void RegisterNavigationButtons()
    {
        if (goToWeaponStageButton != null)
            goToWeaponStageButton.onClick.AddListener(ShowWeaponSelection);

        if (goToBodyStageButton != null)
            goToBodyStageButton.onClick.AddListener(ShowBodySelection);

        if (goToColourStageButton != null)
            goToColourStageButton.onClick.AddListener(ShowColourSelection);

        if (goToWeaponFromColourStageButton != null)
            goToWeaponFromColourStageButton.onClick.AddListener(ShowWeaponSelection);

        if (colorPicker != null)
            colorPicker.PaletteChanged += ApplySelectedPalette;
    }

    private void UnregisterNavigationButtons()
    {
        if (goToWeaponStageButton != null)
            goToWeaponStageButton.onClick.RemoveListener(ShowWeaponSelection);

        if (goToBodyStageButton != null)
            goToBodyStageButton.onClick.RemoveListener(ShowBodySelection);

        if (goToColourStageButton != null)
            goToColourStageButton.onClick.RemoveListener(ShowColourSelection);

        if (goToWeaponFromColourStageButton != null)
            goToWeaponFromColourStageButton.onClick.RemoveListener(ShowWeaponSelection);
    }

    private void UnregisterColorPicker()
    {
        if (colorPicker != null)
            colorPicker.PaletteChanged -= ApplySelectedPalette;
    }

    private void ApplySelectedPalette(ShipColorPalette palette)
    {
        if (!IsCreatingNewCraft || SelectedHull == null || palette == null)
            return;

        selectedColorPalette = palette.Clone();
        hullPreview?.ApplyPalette(selectedColorPalette);
    }
}
