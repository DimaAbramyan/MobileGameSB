using UnityEditor;
using UnityEngine;

public static class StatDescriptionEditor
{
    private static readonly System.Collections.Generic.Dictionary<string, StatSmartDescription> smartPreviews = new();
    public static void Draw(SerializedProperty property, string title)
    {
        var meta = (ScriptableObject)property.serializedObject.targetObject;
        var catalog = StatCatalogEditorCache.Get(meta);
        using var panel = new EditorGUILayout.VerticalScope(EditorStyles.helpBox);
        EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
        var text = property.FindPropertyRelative("text");
        string control = meta.GetInstanceID() + property.propertyPath;
        GUI.SetNextControlName(control);
        text.stringValue = EditorGUILayout.TextArea(text.stringValue, GUILayout.MinHeight(80));
        int cursor = text.stringValue.Length;
        if (GUI.GetNameOfFocusedControl() == control)
            cursor = ((TextEditor)GUIUtility.GetStateObject(typeof(TextEditor), GUIUtility.keyboardControl)).cursorIndex;
        if (GUILayout.Button("Insert Stat"))
        {
            property.serializedObject.ApplyModifiedProperties();
            string path = property.propertyPath; int position = cursor;
            StatBindingPicker.Show(meta, "", "", d => Insert(meta, path, position, d));
        }
        var entries = property.FindPropertyRelative("insertions");
        for (int i = 0; i < entries.arraySize; i++)
        {
            var entry = entries.GetArrayElementAtIndex(i); var binding = entry.FindPropertyRelative("binding");
            string token = entry.FindPropertyRelative("token").stringValue;
            var cell = new StatDisplayCell { sourcePath = binding.FindPropertyRelative("sourcePath").stringValue,
                statId = binding.FindPropertyRelative("statId").stringValue, sourceAsset = binding.FindPropertyRelative("sourceAsset").objectReferenceValue };
            catalog.TryFind(cell, out var definition);
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("{" + token + "}", GUILayout.Width(65));
                if (GUILayout.Button(definition?.Name ?? "Missing Stat", EditorStyles.popup))
                {
                    property.serializedObject.ApplyModifiedProperties(); string path = binding.propertyPath;
                    StatBindingPicker.Show(meta, cell.sourcePath, cell.statId, d => Replace(meta, path, d));
                }
                if (GUILayout.Button("Insert Again", GUILayout.Width(85))) text.stringValue += "{" + token + "}";
                if (GUILayout.Button("×", GUILayout.Width(25)))
                {
                    text.stringValue = text.stringValue.Replace("{" + token + "}", ""); entries.DeleteArrayElementAtIndex(i);
                    property.serializedObject.ApplyModifiedProperties(); GUIUtility.ExitGUI();
                }
            }
            if (definition == null) EditorGUILayout.HelpBox("Selected source or stat no longer exists.", MessageType.Warning);
            else EditorGUILayout.LabelField(definition.SourceLabel, EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.PropertyField(binding.FindPropertyRelative("valueFormat"), new GUIContent("Value Format"));
            StatValueFormatter.Format(definition, null, binding.FindPropertyRelative("valueFormat").stringValue, out string warning);
            if (warning != null && definition != null) EditorGUILayout.HelpBox(warning, MessageType.Warning);
        }
        EditorGUILayout.PropertyField(property.FindPropertyRelative("boldValues"));
        EditorGUILayout.PropertyField(property.FindPropertyRelative("colorValues"));
        if (property.FindPropertyRelative("colorValues").boolValue) EditorGUILayout.PropertyField(property.FindPropertyRelative("valueColor"));
        EditorGUILayout.PropertyField(property.FindPropertyRelative("localizedText"), new GUIContent("Localized Sentence"), true);
        var settings = (StatDescriptionSettings)property.boxedValue;
        string sentence = text.stringValue;
        bool smart = false;
        string localeKey = "StatDescription.Locale." + control;
        string localeCode = SessionState.GetString(localeKey, "ru");
        localeCode = EditorGUILayout.TextField("Preview Locale", localeCode);
        SessionState.SetString(localeKey, localeCode);
        System.Globalization.CultureInfo culture;
        try { culture = System.Globalization.CultureInfo.GetCultureInfo(localeCode); }
        catch (System.Globalization.CultureNotFoundException) { culture = StatValueFormatter.Culture; EditorGUILayout.HelpBox("Unknown locale code", MessageType.Warning); }
        if (settings.localizedText != null && !settings.localizedText.IsEmpty)
        {
            var collection = UnityEditor.Localization.LocalizationEditorSettings.GetStringTableCollection(settings.localizedText.TableReference);
            var table = collection?.GetTable(new UnityEngine.Localization.LocaleIdentifier(culture.Name)) as UnityEngine.Localization.Tables.StringTable;
            var entry = table?.GetEntryFromReference(settings.localizedText.TableEntryReference);
            if (entry != null) { sentence = entry.LocalizedValue; smart = entry.IsSmart; }
            else EditorGUILayout.HelpBox("No entry for Preview Locale; preview uses Meta text.", MessageType.Info);
        }
        EditorGUILayout.HelpBox("In the String Table, enable Smart for this entry. Forms belong in each translation: {stat1:plural:заряд|заряда|зарядов} (ru), {stat1:plural:charge|charges} (en). Explicit formats such as {stat1:N1} use the raw number; bare {stat1} uses Value Format.", MessageType.Info);
        var preview = new StatDescriptionTemplate(); preview.Compile(sentence, settings, catalog);
        EditorGUILayout.LabelField("Preview (base Meta values)", EditorStyles.miniBoldLabel);
        string rendered;
        if (smart)
        {
            if (!smartPreviews.TryGetValue(control, out var smartPreview))
            {
                if (smartPreviews.Count > 64) smartPreviews.Clear();
                smartPreview = new StatSmartDescription(); smartPreviews[control] = smartPreview;
            }
            smartPreview.Compile(sentence, settings, catalog); rendered = smartPreview.Render(null, culture);
            if (smartPreview.Warning != null) EditorGUILayout.HelpBox(smartPreview.Warning, MessageType.Warning);
        }
        else
        {
            rendered = preview.Render(null, settings, culture);
            if (StatSmartDescription.RequiresSmart(sentence, settings)) EditorGUILayout.HelpBox("This entry is not Smart. Enable Smart in the String Table to use formatters.", MessageType.Warning);
        }
        EditorGUILayout.LabelField(rendered, new GUIStyle(EditorStyles.wordWrappedLabel) { richText = true });
        if (rendered.Contains("[missing")) EditorGUILayout.HelpBox("Description contains an unavailable insertion.", MessageType.Warning);
    }
    public static void Insert(ScriptableObject meta, string path, int position, StatValueDefinition definition)
    {
        var so = new SerializedObject(meta); var description = so.FindProperty(path);
        var list = description.FindPropertyRelative("insertions"); string token = "stat" + (list.arraySize + 1);
        bool Exists(string name) { for (int i = 0; i < list.arraySize; i++) if (list.GetArrayElementAtIndex(i).FindPropertyRelative("token").stringValue == name) return true; return false; }
        int next = list.arraySize + 1; while (Exists(token)) token = "stat" + (++next);
        list.arraySize++; var entry = list.GetArrayElementAtIndex(list.arraySize - 1);
        entry.FindPropertyRelative("token").stringValue = token; Write(entry.FindPropertyRelative("binding"), definition);
        entry.FindPropertyRelative("binding").FindPropertyRelative("valueFormat").stringValue = "";
        var text = description.FindPropertyRelative("text"); text.stringValue = text.stringValue.Insert(Mathf.Clamp(position, 0, text.stringValue.Length), "{" + token + "}");
        so.ApplyModifiedProperties(); StatBindingPicker.SaveIdentities(meta);
    }
    private static void Replace(ScriptableObject meta, string path, StatValueDefinition definition)
    { var so = new SerializedObject(meta); var p = so.FindProperty(path); if (p == null) return; Write(p, definition); so.ApplyModifiedProperties(); StatBindingPicker.SaveIdentities(meta); }
    private static void Write(SerializedProperty binding, StatValueDefinition d)
    {
        binding.FindPropertyRelative("useBinding").boolValue = true;
        binding.FindPropertyRelative("sourcePath").stringValue = d.SourcePath;
        binding.FindPropertyRelative("statId").stringValue = d.Id;
        binding.FindPropertyRelative("sourceAsset").objectReferenceValue = d.SourceAsset;
    }
}
