using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Localization;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Metadata;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;

// One-off asset setup through the running Editor. No project Editor tooling or UI integration.
public static class SetupLocalizationFoundation
{
    private const string Root = "Assets/Localization";

    // Extends an existing foundation; never recreates shared data or writes translations.
    public static object AddReleaseLanguages()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
            throw new InvalidOperationException("Run setup in an idle Editor outside Play Mode.");

        var settings = LocalizationEditorSettings.ActiveLocalizationSettings;
        var english = LocalizationEditorSettings.GetLocale("en");
        var ui = LocalizationEditorSettings.GetStringTableCollection("UI");
        var content = LocalizationEditorSettings.GetStringTableCollection("Content");
        if (settings == null || english == null || ui == null || content == null)
            throw new InvalidOperationException("The existing Localization foundation is required.");

        var collections = new[] { ui, content };
        foreach (var collection in collections)
            foreach (string code in new[] { "en", "ru", "zh-Hans" })
                if (collection.GetTable(new LocaleIdentifier(code)) == null)
                    throw new InvalidOperationException($"Existing table {collection.TableCollectionName}/{code} is missing.");

        string[] codes = { "de", "fr", "es", "pt-BR", "ja" };
        string[] names = { "Deutsch", "Français", "Español", "Português (Brasil)", "日本語" };
        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Add release localization languages");
        int createdLocales = 0, createdTables = 0;
        try
        {
            for (int i = 0; i < codes.Length; i++)
            {
                var locale = LocalizationEditorSettings.GetLocale(codes[i]);
                if (locale == null)
                {
                    locale = LocaleAsset(codes[i], names[i], (ushort)(i + 3));
                    createdLocales++;
                }
                var fallback = locale.Metadata.GetMetadata<FallbackLocale>();
                if (fallback == null || fallback.Locale != english)
                {
                    Undo.RecordObject(locale, "Set English fallback");
                    Fallback(locale, english);
                }

                foreach (var collection in collections)
                {
                    if (collection.GetTable(locale.Identifier) != null)
                        continue;
                    string path = $"{Root}/Tables/{collection.TableCollectionName}/{collection.TableCollectionName}_{codes[i]}.asset";
                    var table = AssetDatabase.LoadAssetAtPath<StringTable>(path);
                    if (table == null)
                    {
                        if (AssetDatabase.LoadMainAssetAtPath(path) != null)
                            throw new InvalidOperationException("An incompatible asset already exists: " + path);
                        table = ScriptableObject.CreateInstance<StringTable>();
                        table.LocaleIdentifier = locale.Identifier;
                        table.SharedData = collection.SharedData;
                        AssetDatabase.CreateAsset(table, path);
                        Undo.RegisterCreatedObjectUndo(table, "Create release string table");
                        createdTables++;
                    }
                    if (table.SharedData != collection.SharedData || table.LocaleIdentifier != locale.Identifier)
                        throw new InvalidOperationException("An existing table has incompatible shared data or locale: " + path);
                    collection.AddTable(table, true);
                }
            }

            // Persist only localization/Addressables assets; unrelated dirty assets are not saved.
            foreach (string folder in new[] { Root, "Assets/AddressableAssetsData" })
                foreach (string guid in AssetDatabase.FindAssets("", new[] { folder }))
                {
                    var asset = AssetDatabase.LoadMainAssetAtPath(AssetDatabase.GUIDToAssetPath(guid));
                    if (asset != null && EditorUtility.IsDirty(asset))
                        AssetDatabase.SaveAssetIfDirty(asset);
                }
            return new { createdLocales, createdTables, supported = new[] { "en", "ru", "zh-Hans", "de", "fr", "es", "pt-BR", "ja" }, fallback = "en" };
        }
        finally { Undo.CollapseUndoOperations(undoGroup); }
    }

    public static object Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
            throw new InvalidOperationException("Run setup in an idle Editor outside Play Mode.");

        Folder(Root);
        Folder(Root + "/Locales");
        Folder(Root + "/Tables");
        var settings = LocalizationEditorSettings.ActiveLocalizationSettings;
        if (settings == null)
        {
            settings = ScriptableObject.CreateInstance<LocalizationSettings>();
            AssetDatabase.CreateAsset(settings, Root + "/LocalizationSettings.asset");
            Undo.RegisterCreatedObjectUndo(settings, "Create Localization Settings");
            LocalizationEditorSettings.ActiveLocalizationSettings = settings;
        }
        LocalizationSettings.Instance = settings;
        var en = LocaleAsset("en", "English", 0);
        var ru = LocaleAsset("ru", "Русский", 1);
        var zh = LocaleAsset("zh-Hans", "简体中文", 2);
        Fallback(ru, en);
        Fallback(zh, en);

        Undo.RecordObject(settings, "Configure localization foundation");
        LocalizationSettings.ProjectLocale = en;
        LocalizationSettings.InitializeSynchronously = false;
        LocalizationSettings.PreloadBehavior = PreloadBehavior.PreloadSelectedLocaleAndFallbacks;
        var selectors = settings.GetStartupLocaleSelectors();
        selectors.Clear();
        selectors.Add(new SpecificLocaleSelector { LocaleId = en.Identifier });
        var database = settings.GetStringDatabase();
        database.UseFallback = true;
        database.MissingTranslationState = MissingTranslationBehavior.ShowMissingTranslationMessage;
        database.NoTranslationFoundMessage = "[Missing translation: {table.TableCollectionName}/{key}]";

        var locales = new List<Locale> { en, ru, zh };
        var ui = Collection("UI", locales);
        var content = Collection("Content", locales);
        var addressables = AddressableAssetSettingsDefaultObject.Settings;
        Undo.RecordObject(addressables, "Include localization data in player builds");
        addressables.BuildAddressablesWithPlayerBuild = AddressableAssetSettings.PlayerBuildOption.BuildWithPlayer;
        EditorUtility.SetDirty(addressables);
        database.DefaultTable = ui.SharedData.TableCollectionNameGuid;
        ui.SetPreloadTableFlag(true, true);
        content.SetPreloadTableFlag(false, true);
        Entry(ui, "en", "foundation.ready", "Localization ready");
        Entry(ui, "ru", "foundation.ready", "Локализация готова");
        Entry(ui, "zh-Hans", "foundation.ready", "本地化已就绪");
        Entry(ui, "en", "foundation.items", "Items: {0}", true);
        Entry(ui, "ru", "foundation.items", "Предметы: {0}", true);
        Entry(ui, "zh-Hans", "foundation.items", "物品：{0}", true);
        Entry(ui, "en", "foundation.fallback", "English fallback");
        Entry(content, "en", "foundation.ready", "Content localization ready");
        Entry(content, "ru", "foundation.ready", "Локализация контента готова");
        Entry(content, "zh-Hans", "foundation.ready", "内容本地化已就绪");
        EditorUtility.SetDirty(settings);
        AssetDatabase.SaveAssets();
        AddReleaseLanguages();
        return new { settings = AssetDatabase.GetAssetPath(settings), locales = new[] { "en", "ru", "zh-Hans", "de", "fr", "es", "pt-BR", "ja" },
            tables = new[] { ui.TableCollectionName, content.TableCollectionName }, fallback = "en", startup = "en" };
    }

    private static Locale LocaleAsset(string code, string label, ushort order)
    {
        string path = Root + "/Locales/" + code + ".asset";
        var locale = AssetDatabase.LoadAssetAtPath<Locale>(path);
        if (locale == null)
        {
            locale = Locale.CreateLocale(code);
            AssetDatabase.CreateAsset(locale, path);
            Undo.RegisterCreatedObjectUndo(locale, "Create Locale");
        }
        Undo.RecordObject(locale, "Configure Locale");
        locale.LocaleName = label;
        locale.SortOrder = order;
        LocalizationEditorSettings.AddLocale(locale, true);
        EditorUtility.SetDirty(locale);
        return locale;
    }

    private static void Fallback(Locale locale, Locale english)
    {
        var fallback = locale.Metadata.GetMetadata<FallbackLocale>();
        if (fallback == null)
            locale.Metadata.AddMetadata(new FallbackLocale(english));
        else
            fallback.Locale = english;
        EditorUtility.SetDirty(locale);
    }

    private static StringTableCollection Collection(string name, IList<Locale> locales)
    {
        string directory = Root + "/Tables/" + name;
        Folder(directory);
        return AssetDatabase.LoadAssetAtPath<StringTableCollection>(directory + "/" + name + ".asset")
            ?? LocalizationEditorSettings.CreateStringTableCollection(name, directory, locales);
    }

    private static void Entry(StringTableCollection collection, string code, string key, string value, bool smart = false)
    {
        var table = (StringTable)collection.GetTable(new LocaleIdentifier(code));
        Undo.RecordObjects(new UnityEngine.Object[] { table, collection.SharedData }, "Add foundation test string");
        var entry = table.GetEntry(key) ?? table.AddEntry(key, value);
        entry.Value = value;
        entry.IsSmart = smart;
        EditorUtility.SetDirty(table);
        EditorUtility.SetDirty(collection.SharedData);
    }

    private static void Folder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
            return;
        int separator = path.LastIndexOf('/');
        string parent = path.Substring(0, separator);
        Folder(parent);
        AssetDatabase.CreateFolder(parent, path.Substring(separator + 1));
    }
}
