using System;
using System.Threading.Tasks;
using UnityEngine.Localization;
using UnityEngine.Localization.Tables;

/// <summary>Use from Unity's main thread. Translation data belongs to Unity Localization.</summary>
public interface ILocalizationService
{
    bool IsInitialized { get; }
    Locale SelectedLocale { get; }
    event Action<Locale> LocaleChanged;

    Task InitializeAsync();
    Task SetLocaleAsync(string localeCode);
    Task<string> GetStringAsync(TableReference table, TableEntryReference key, params object[] arguments);

    // Native references support StringChanged, TMP localizers and serialized ScriptableObject fields.
    LocalizedString CreateString(TableReference table, TableEntryReference key, params object[] arguments);
}
