using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.AddressableAssets;

public enum StatDescriptionKind { Weapon, ActiveAbility, PassiveAbility }

public sealed class StatDescriptionView : MonoBehaviour
{
    [SerializeField] private StatBlocksView source;
    [SerializeField] private TMP_Text target;
    [SerializeField] private StatDescriptionKind kind;
    private StatDescriptionSettings settings;
    private StatValueCatalog catalog;
    private string localized, compiledText, fallback = "";
    private readonly StatDescriptionTemplate template = new();
    private readonly StatSmartDescription smartTemplate = new();
    private bool isSmart, localeSubscribed;
    private int localizationVersion;
    public string Warning { get; private set; }
    private void OnEnable()
    {
        UnsubscribeLocalization(); settings = null; catalog = null;
        if (source != null) source.ValuesRefreshed += Refresh;
        if (Application.isPlaying && LocalizationSettings.HasSettings)
        { LocalizationSettings.SelectedLocaleChanged += OnLocaleChanged; localeSubscribed = true; }
        Refresh();
    }
    private void OnDisable()
    {
        if (source != null) source.ValuesRefreshed -= Refresh;
        if (localeSubscribed) LocalizationSettings.SelectedLocaleChanged -= OnLocaleChanged;
        localeSubscribed = false;
        UnsubscribeLocalization(); settings = null; catalog = null; compiledText = null;
    }
    private void UnsubscribeLocalization()
    {
        localizationVersion++;
    }
    private void OnLocaleChanged(Locale _) { LoadLocalized(); }
    private async void LoadLocalized()
    {
        int version = ++localizationVersion;
        localized = null; isSmart = false; compiledText = null; Warning = null;
        if (settings?.localizedText == null || settings.localizedText.IsEmpty) { Refresh(); return; }
        var reference = settings.localizedText;
        try
        {
            var operation = LocalizationSettings.StringDatabase.GetTableEntryAsync(reference.TableReference, reference.TableEntryReference, reference.LocaleOverride, reference.FallbackState);
            Addressables.ResourceManager.Acquire(operation);
            try
            {
                var entry = await operation.Task;
                if (version != localizationVersion || this == null || !isActiveAndEnabled) return;
                if (entry.Entry == null) throw new System.InvalidOperationException("Localized description entry not found");
                localized = entry.Entry.LocalizedValue; isSmart = entry.Entry.IsSmart; Refresh();
            }
            finally { if (operation.IsValid()) Addressables.Release(operation); }
        }
        catch (System.Exception e)
        {
            if (version != localizationVersion || this == null) return;
            Warning = e.Message; localized = "— [localization]"; Refresh();
        }
    }
    public void SetFallback(string value) { fallback = value ?? ""; Refresh(); }
    public void Refresh()
    {
        if (target == null || source == null) return;
        var next = source.Meta is WeaponMetaConfig weapon ? weapon.Description
            : source.Meta is ShipMetaConfig ship ? kind == StatDescriptionKind.ActiveAbility ? ship.ActiveAbilityDescription : ship.PassiveAbilityDescription : null;
        if (!ReferenceEquals(next, settings))
        {
            UnsubscribeLocalization(); settings = next; localized = null; compiledText = null;
            isSmart = false;
            if (Application.isPlaying && isActiveAndEnabled && settings?.localizedText != null && !settings.localizedText.IsEmpty) LoadLocalized();
        }
        string text = localized ?? settings?.text ?? "";
        if (settings == null || string.IsNullOrEmpty(text)) { SetText(fallback); return; }
        if (catalog != source.Catalog || compiledText != text)
        {
            catalog = source.Catalog; compiledText = text;
            template.Compile(text, settings, catalog);
            smartTemplate.Compile(text, settings, catalog);
        }
        if (isSmart)
        {
            var locale = settings.localizedText.LocaleOverride ?? LocalizationSettings.SelectedLocale;
            SetText(smartTemplate.Render(source.ValueResolver, locale != null ? locale : StatValueFormatter.Culture));
            Warning = smartTemplate.Warning;
        }
        else
        {
            if (StatSmartDescription.RequiresSmart(text, settings)) { Warning = "Enable Smart on this String Table entry to use Smart Formatting."; SetText("— [format: enable Smart]"); }
            else SetText(template.Render(source.ValueResolver, settings, (System.IFormatProvider)settings.localizedText?.LocaleOverride ?? StatValueFormatter.Culture));
        }
    }
    private void SetText(string text) { if (target.text != text) target.text = text; }
}
