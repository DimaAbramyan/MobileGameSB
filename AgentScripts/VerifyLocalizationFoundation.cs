using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.Localization;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Metadata;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;
using UnityEngine.ResourceManagement.AsyncOperations;
using Zenject;

public static class VerifyLocalizationFoundation
{
    private static readonly string[] Codes = { "en", "ru", "zh-Hans", "de", "fr", "es", "pt-BR", "ja" };

    public static Task<object> Run() => RunCore(false);

    // Runs in an empty Play scene without instantiating the project's save/audio/analytics hosts.
    public static Task<object> RunIsolated() => RunCore(true);

    private static async Task<object> RunCore(bool isolated)
    {
        var checks = new List<string>();
        var settings = LocalizationEditorSettings.ActiveLocalizationSettings;
        Check(settings != null, "Settings registered in build config", checks);
        Check(!LocalizationSettings.InitializeSynchronously, "Nonblocking initialization", checks);
        var locales = new List<Locale>(LocalizationEditorSettings.GetLocales());
        Check(locales.Count == Codes.Length, "Exactly eight locales", checks);
        var aa = AddressableAssetSettingsDefaultObject.Settings;
        Check(aa != null, "Addressables settings exist", checks);
        Check(aa.BuildAddressablesWithPlayerBuild == AddressableAssetSettings.PlayerBuildOption.BuildWithPlayer,
            "Addressables content builds with Android player", checks);
        foreach (var name in new[] { "UI", "Content" })
        {
            var collection = LocalizationEditorSettings.GetStringTableCollection(name);
            Check(collection != null && collection.Tables.Count == Codes.Length, "Eight persistent tables: " + name, checks);
            Check(collection.IsPreloadTableFlagSet() == (name == "UI"), "Preload policy: " + name, checks);
            foreach (var table in collection.Tables)
            {
                var entry = aa.FindAssetEntry(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(table.asset)));
                Check(entry != null && entry.parentGroup.GetSchema<BundledAssetGroupSchema>().IncludeInBuild,
                    "Table included in build: " + table.asset.name, checks);
                Check(LocalizationEditorSettings.GetPreloadTableFlag(table.asset) == (name == "UI"),
                    "Per-table preload policy: " + table.asset.name, checks);
                Check(table.asset.SharedData == collection.SharedData, "Shared data retained: " + table.asset.name, checks);
                if (Array.IndexOf(Codes, table.asset.LocaleIdentifier.Code) >= 3)
                    Check(((StringTable)table.asset).Count == 0, "No invented translations: " + table.asset.name, checks);
            }
        }
        foreach (var code in Codes)
        {
            var locale = locales.Find(l => l.Identifier.Code == code);
            Check(locale != null, "Locale " + code, checks);
            Check(aa.FindAssetEntry(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(locale))) != null,
                "Locale included in Addressables: " + code, checks);
            if (code != "en")
                Check(locale.Metadata.GetMetadata<FallbackLocale>()?.Locale.Identifier.Code == "en", "Fallback metadata " + code, checks);
        }

        DiContainer container;
        if (isolated)
        {
            Check(Application.isPlaying, "Isolated runtime check is in Play Mode", checks);
            Check(!ProjectContext.HasInstance, "No gameplay ProjectContext was created", checks);
            container = new DiContainer();
            container.BindInterfacesTo<UnityLocalizationService>().AsSingle();
        }
        else if (Application.isPlaying)
        {
            container = ProjectContext.Instance.Container;
            Check(container != null, "Live ProjectContext container", checks);
        }
        else
        {
            container = new DiContainer();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/ProjectContext.prefab");
            var installer = prefab.GetComponent<ProjectManagerInstaller>();
            Check(installer != null, "Global installer on ProjectContext", checks);
            container.Inject(installer);
            installer.InstallBindings();
        }
        var service = container.Resolve<ILocalizationService>();
        Check(ReferenceEquals(service, container.Resolve<ILocalizationService>()), "Project singleton binding", checks);
        var concrete = (UnityLocalizationService)service;
        Locale previous = settings.GetSelectedLocale();
        // Edit Mode deliberately uses the Editor's preview locale, not startup selectors.
        if (!Application.isPlaying)
            settings.SetSelectedLocale(locales.Find(l => l.Identifier.Code == "en"));
        int events = 0;
        bool watchEvents = false;
        var eventErrors = new List<string>();
        Action<Locale> onChange = locale =>
        {
            events++;
            if (!watchEvents) return;
            if (locale == null || service.SelectedLocale != locale || !settings.GetInitializationOperation().IsDone)
                eventErrors.Add("LocaleChanged fired before selected locale was initialized");
            else if (Application.isPlaying && (!TryCachedTable(settings, "UI", locale, out var preloaded) || !preloaded.IsDone))
                eventErrors.Add("LocaleChanged fired before UI preload: " + locale.Identifier.Code);
        };
        service.LocaleChanged += onChange;
        try
        {
            var earlyRequest = service.GetStringAsync("UI", "foundation.ready");
            var first = service.InitializeAsync();
            Check(ReferenceEquals(first, service.InitializeAsync()), "Shared initialization task", checks);
            await first;
            Check(await earlyRequest == "Localization ready", "String request before initialization and English startup", checks);
            Check(service.IsInitialized, "Initialization completed", checks);
            Check(LocalizationSettings.ProjectLocale.Identifier.Code == "en", "Project locale en", checks);
            await SettleEvents();
            watchEvents = true;
            var expected = new[] { "Localization ready", "Локализация готова", "本地化已就绪", "Localization ready", "Localization ready", "Localization ready", "Localization ready", "Localization ready" };
            var smart = new[] { "Items: 7", "Предметы: 7", "物品：7", "Items: 7", "Items: 7", "Items: 7", "Items: 7", "Items: 7" };
            var content = new[] { "Content localization ready", "Локализация контента готова", "内容本地化已就绪", "Content localization ready", "Content localization ready", "Content localization ready", "Content localization ready", "Content localization ready" };
            for (int i = 0; i < Codes.Length; i++)
            {
                string code = Codes[i];
                string previousCode = service.SelectedLocale?.Identifier.Code;
                int before = events;
                await service.SetLocaleAsync(code);
                await SettleEvents();
                Check(service.SelectedLocale?.Identifier.Code == code, "SelectedLocale " + code, checks);
                Check(events == before + (previousCode == code ? 0 : 1), "Exactly one event per change: " + code, checks);
                AsyncOperationHandle<StringTable> uiHandle = default;
                // Localization 1.5 deliberately disables preloading in Edit Mode preview.
                if (Application.isPlaying)
                    Check(TryCachedTable(settings, "UI", service.SelectedLocale, out uiHandle)
                        && uiHandle.IsDone && uiHandle.Status == AsyncOperationStatus.Succeeded, "UI actually preloaded: " + code + "; " + CacheSummary(settings), checks);
                Check(!TryCachedTable(settings, "Content", service.SelectedLocale, out _), "Content not preloaded: " + code, checks);
                var initHandle = settings.GetInitializationOperation();
                Check(ReferenceEquals(first, service.InitializeAsync()), "Shared service initialization: " + code, checks);
                Check(await service.GetStringAsync("UI", "foundation.ready") == expected[i], "Static string " + code, checks);
                Check(await service.GetStringAsync("UI", "foundation.items", 7) == smart[i], "Smart parameter " + code, checks);
                Check(await service.GetStringAsync("UI", "foundation.fallback") == "English fallback", "Fallback result " + code, checks);
                if (!Application.isPlaying)
                    Check(TryCachedTable(settings, "UI", service.SelectedLocale, out uiHandle), "UI loaded by Edit Mode request: " + code, checks);
                Check(await service.GetStringAsync("Content", "foundation.ready") == content[i], "Lazy Content and fallback " + code, checks);
                Check(TryCachedTable(settings, "Content", service.SelectedLocale, out var contentHandle)
                    && contentHandle.IsDone, "Content loaded on request: " + code, checks);
                var database = settings.GetStringDatabase();
                var uiCollection = LocalizationEditorSettings.GetStringTableCollection("UI");
                Check(database.GetTableAsync("UI", service.SelectedLocale).Equals(uiHandle)
                    && database.GetTableAsync(uiCollection.SharedData.TableCollectionNameGuid, service.SelectedLocale).Equals(uiHandle),
                    "Name/GUID requests reuse UI operation: " + code, checks);
                Check(database.GetTableAsync("Content", service.SelectedLocale).Equals(contentHandle), "Content operation reused: " + code, checks);
                before = events;
                await service.SetLocaleAsync(code);
                await SettleEvents();
                Check(events == before && settings.GetInitializationOperation().Equals(initHandle), "Same-locale selection does not reinitialize: " + code, checks);
            }
            // A missing key is visible, rather than returning an unexplained empty string.
            Check(await service.GetStringAsync("UI", "foundation.missing") == "[Missing translation: UI/foundation.missing]",
                "Missing key diagnostic", checks);
            var collection = LocalizationEditorSettings.GetStringTableCollection("UI");
            TableReference guid = collection.SharedData.TableCollectionNameGuid;
            TableEntryReference id = collection.SharedData.GetEntry("foundation.ready").Id;
            Check(await service.GetStringAsync(guid, id) == expected[expected.Length - 1], "Stable GUID and entry ID references", checks);
            var reference = service.CreateString(guid, id);
            Check(reference.TableReference.Equals(guid), "Native LocalizedString reference", checks);
            bool invalidLocale = false;
            try { await service.SetLocaleAsync("invalid-locale"); }
            catch (ArgumentException) { invalidLocale = true; }
            Check(invalidLocale, "Unsupported locale rejected explicitly", checks);
            bool emptyReference = false;
            try { await service.GetStringAsync(default(TableReference), "key"); }
            catch (ArgumentException) { emptyReference = true; }
            Check(emptyReference, "Empty reference rejected explicitly", checks);
            // Let the official locale-changed coroutine dispatch after table preloading.
            for (int i = 0; i < 4; i++) await Task.Yield();
            Check(events >= 2, "Locale change forwarded", checks);
            Check(eventErrors.Count == 0, "Events follow selection and UI preload: " + string.Join("; ", eventErrors), checks);
        }
        finally
        {
            if (!Application.isPlaying) settings.SetSelectedLocale(previous);
            else if (previous != null) await service.SetLocaleAsync(previous.Identifier.Code);
            service.LocaleChanged -= onChange;
            if (!Application.isPlaying || isolated) concrete.Dispose();
        }
        if (isolated) Check(!ProjectContext.HasInstance, "Runtime checks did not initialize gameplay systems", checks);
        var disposable = Application.isPlaying && !isolated ? new UnityLocalizationService() : concrete;
        disposable.Dispose();
        bool disposed = false;
        try { await disposable.InitializeAsync(); } catch (ObjectDisposedException) { disposed = true; }
        Check(disposed, "Disposed service rejects requests", checks);
        return new { success = true, count = checks.Count, checks, playMode = Application.isPlaying,
            locales = locales.ConvertAll(l => l.Identifier.Code) };
    }

    // Diagnostic-only observation of the package cache: never creates a second cache or loads a table.
    private static bool TryCachedTable(LocalizationSettings settings, string name, Locale locale,
        out AsyncOperationHandle<StringTable> operation)
    {
        var property = typeof(LocalizedDatabase<StringTable, StringTableEntry>).GetProperty("TableOperations", BindingFlags.Instance | BindingFlags.NonPublic);
        if (property == null) throw new InvalidOperationException("Localization 1.5 cache diagnostic API changed.");
        var cache = (Dictionary<(LocaleIdentifier, string), AsyncOperationHandle<StringTable>>)property.GetValue(settings.GetStringDatabase());
        return cache.TryGetValue((locale.Identifier, name), out operation) && operation.IsValid();
    }

    private static string CacheSummary(LocalizationSettings settings)
    {
        var property = typeof(LocalizedDatabase<StringTable, StringTableEntry>).GetProperty("TableOperations", BindingFlags.Instance | BindingFlags.NonPublic);
        var cache = (Dictionary<(LocaleIdentifier, string), AsyncOperationHandle<StringTable>>)property.GetValue(settings.GetStringDatabase());
        var keys = new List<string>();
        foreach (var entry in cache) keys.Add(entry.Key.Item1.Code + "/" + entry.Key.Item2 + ":" + (entry.Value.IsValid() ? entry.Value.Status.ToString() : "invalid"));
        return string.Join(", ", keys);
    }

    private static async Task SettleEvents()
    {
        // The official LocaleChanged notification runs on its coroutine after table preloading.
        for (int i = 0; i < 4; i++)
        {
            if (!Application.isPlaying) EditorApplication.QueuePlayerLoopUpdate();
            await Task.Delay(10);
        }
    }

    private static void Check(bool condition, string message, List<string> checks)
    {
        if (!condition) throw new InvalidOperationException("Localization verification failed: " + message);
        checks.Add(message);
    }
}
