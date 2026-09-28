using UnityEngine;

public sealed class CraftColourPresetCollectionController : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField] private ShipColorPaletteSelectionController colorPicker;
    [SerializeField] private CraftCreationFlowController craftCreationFlow;

    [Header("Arkanoid Presets")]
    [SerializeField] private CraftColourPreset[] presets = System.Array.Empty<CraftColourPreset>();

    private bool isSubscribed;

    private void Awake()
    {
        Subscribe();
        Refresh();
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

    public void RefreshBindings()
    {
        Unsubscribe();
        Subscribe();
        Refresh();
    }

    public void Refresh()
    {
        HullContentDefinition selectedHull = craftCreationFlow != null
            ? craftCreationFlow.SelectedHull
            : null;

        for (int i = 0; i < presets.Length; i++)
        {
            CraftColourPreset preset = presets[i];
            if (preset == null)
                continue;

            bool isAvailable = selectedHull != null && preset.Supports(selectedHull);
            preset.SetAvailable(isAvailable);
        }

        RefreshSelection(colorPicker != null ? colorPicker.Palette : null);
    }

    private void Subscribe()
    {
        if (isSubscribed)
            return;

        for (int i = 0; i < presets.Length; i++)
        {
            if (presets[i] != null)
                presets[i].Clicked += SelectPreset;
        }

        if (craftCreationFlow != null)
            craftCreationFlow.HullSelectionChanged += HandleHullSelectionChanged;

        if (colorPicker != null)
            colorPicker.PaletteChanged += RefreshSelection;

        isSubscribed = true;
    }

    private void Unsubscribe()
    {
        if (!isSubscribed)
            return;

        for (int i = 0; i < presets.Length; i++)
        {
            if (presets[i] != null)
                presets[i].Clicked -= SelectPreset;
        }

        if (craftCreationFlow != null)
            craftCreationFlow.HullSelectionChanged -= HandleHullSelectionChanged;

        if (colorPicker != null)
            colorPicker.PaletteChanged -= RefreshSelection;

        isSubscribed = false;
    }

    private void SelectPreset(CraftColourPreset preset)
    {
        if (preset == null || colorPicker == null || craftCreationFlow == null)
            return;

        if (!preset.Supports(craftCreationFlow.SelectedHull))
        {
            Debug.LogWarning(
                $"Colour preset '{preset.PresetName}' is only available for its configured hull.",
                preset);
            return;
        }

        colorPicker.ApplyPalette(preset.Palette);
    }

    private void HandleHullSelectionChanged(HullContentDefinition _)
    {
        Refresh();
    }

    private void RefreshSelection(ShipColorPalette palette)
    {
        for (int i = 0; i < presets.Length; i++)
        {
            CraftColourPreset preset = presets[i];
            if (preset == null)
                continue;

            preset.SetSelected(preset.Supports(craftCreationFlow != null
                ? craftCreationFlow.SelectedHull
                : null) && preset.Matches(palette));
        }
    }
}
