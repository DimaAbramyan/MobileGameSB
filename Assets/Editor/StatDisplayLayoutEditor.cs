using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class StatDisplayLayoutEditor
{
    private static readonly Dictionary<string, Vector2> scrolls = new();
    public static void Draw(SerializedProperty settings)
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Stat Display", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Choose only the stats to display. Manual text remains available. Preview shows Meta values; live binding reads supported final values. [base] marks configuration-only values.", MessageType.None);
        var catalog = StatCatalogEditorCache.Get(settings.serializedObject.targetObject as ScriptableObject);
        foreach (string warning in catalog.Warnings) EditorGUILayout.HelpBox(warning, MessageType.Warning);
        Section(settings.FindPropertyRelative("baseStats"), "Base Stats Layout", catalog);
        Section(settings.FindPropertyRelative("secondaryStats"), "Secondary Stats Layout", catalog);
        if (GUILayout.Button("Open Layout Preview"))
        {
            settings.serializedObject.ApplyModifiedProperties();
            StatBlocksPreviewWindow.Open(settings.serializedObject.targetObject as ScriptableObject);
        }
        EditorGUILayout.Space();
    }

    private static void Section(SerializedProperty layout, string title, StatValueCatalog catalog)
    {
        var rows = layout.FindPropertyRelative("rows");
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            if (rows.arraySize == 0) EditorGUILayout.LabelField("Empty section", EditorStyles.miniLabel);
            for (int i = 0; i < rows.arraySize; i++) DrawRow(rows, i, catalog);
            if (GUILayout.Button("+ Add Row"))
            {
                rows.InsertArrayElementAtIndex(rows.arraySize);
                rows.GetArrayElementAtIndex(rows.arraySize - 1).FindPropertyRelative("cells").ClearArray();
            }
        }
    }

    private static void DrawRow(SerializedProperty rows, int index, StatValueCatalog catalog)
    {
        var row = rows.GetArrayElementAtIndex(index);
        var cells = row.FindPropertyRelative("cells");
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField($"Row {index + 1} · {cells.arraySize} cells", EditorStyles.boldLabel);
                Move(rows, index, -1, "↑"); Move(rows, index, 1, "↓");
                if (GUILayout.Button("Delete Row", GUILayout.Width(85))) { rows.DeleteArrayElementAtIndex(index); CommitAndExit(rows); }
            }
            string key = row.serializedObject.targetObject.GetInstanceID() + row.propertyPath;
            scrolls.TryGetValue(key, out var scroll);
            using (var view = new EditorGUILayout.ScrollViewScope(scroll, false, false, GUILayout.Height(cells.arraySize == 0 ? 20 : 310)))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    for (int c = 0; c < cells.arraySize; c++)
                    {
                        var cell = cells.GetArrayElementAtIndex(c);
                        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.Width(230)))
                        {
                            using (new EditorGUILayout.HorizontalScope())
                            {
                                GUILayout.Label($"Cell {c + 1}", EditorStyles.miniBoldLabel);
                                Move(cells, c, -1, "←"); Move(cells, c, 1, "→");
                                if (GUILayout.Button("×", GUILayout.Width(22))) { cells.DeleteArrayElementAtIndex(c); CommitAndExit(cells); }
                            }
                            DrawSource(cell, catalog);
                            bool bound = cell.FindPropertyRelative("useBinding").boolValue;
                            if (bound) EditorGUILayout.PropertyField(cell.FindPropertyRelative("overrideLabel"), new GUIContent("Override Label"));
                            using (new EditorGUI.DisabledScope(bound && !cell.FindPropertyRelative("overrideLabel").boolValue))
                            {
                                EditorGUILayout.LabelField("Label", EditorStyles.miniLabel);
                                EditorGUILayout.PropertyField(cell.FindPropertyRelative("label"), GUIContent.none);
                            }
                            EditorGUILayout.LabelField(bound ? "Value Format (optional)" : "Value (manual)", EditorStyles.miniBoldLabel);
                            EditorGUILayout.PropertyField(cell.FindPropertyRelative(bound ? "valueFormat" : "value"), GUIContent.none);
                            cell.isExpanded = EditorGUILayout.Foldout(cell.isExpanded, "Localization", true);
                            if (cell.isExpanded)
                            {
                                using (new EditorGUI.DisabledScope(bound && !cell.FindPropertyRelative("overrideLabel").boolValue))
                                {
                                    EditorGUILayout.LabelField("Label Key (optional)", EditorStyles.miniLabel);
                                    EditorGUILayout.PropertyField(cell.FindPropertyRelative("labelLocalizationKey"), GUIContent.none);
                                }
                            }
                        }
                    }
                }
                if (scrolls.Count > 128) scrolls.Clear();
                scrolls[key] = view.scrollPosition;
            }
            if (GUILayout.Button("+ Add Cell to Row"))
            {
                cells.InsertArrayElementAtIndex(cells.arraySize);
                var cell = cells.GetArrayElementAtIndex(cells.arraySize - 1);
                cell.FindPropertyRelative("label").stringValue = "Stat";
                cell.FindPropertyRelative("value").stringValue = "—";
                cell.FindPropertyRelative("labelLocalizationKey").stringValue = "";
                cell.FindPropertyRelative("useBinding").boolValue = false;
                cell.FindPropertyRelative("overrideLabel").boolValue = false;
                cell.FindPropertyRelative("sourceAsset").objectReferenceValue = null;
                foreach (string field in new[] { "sourcePath", "statId", "valueFormat" })
                    cell.FindPropertyRelative(field).stringValue = "";
            }
        }
    }
    private static void DrawSource(SerializedProperty cell, StatValueCatalog catalog)
    {
        var meta = cell.serializedObject.targetObject as ScriptableObject;
        var selection = new StatDisplayCell {
            sourcePath = cell.FindPropertyRelative("sourcePath").stringValue,
            statId = cell.FindPropertyRelative("statId").stringValue,
            sourceAsset = cell.FindPropertyRelative("sourceAsset").objectReferenceValue };
        bool bound = cell.FindPropertyRelative("useBinding").boolValue;
        catalog.TryFind(selection, out var definition);
        string caption = !bound ? "Manual Text" : definition != null ? definition.Name : "Missing Source / Stat";
        if (GUILayout.Button(new GUIContent(caption + " ▾", definition?.SourceLabel), EditorStyles.popup))
        {
            cell.serializedObject.ApplyModifiedProperties();
            string propertyPath = cell.propertyPath;
            StatBindingPicker.Show(meta, selection.sourcePath, selection.statId, item => Select(meta, propertyPath, item), true);
        }
        if (!bound) return;
        if (definition == null) { EditorGUILayout.HelpBox("Selected source or stat was removed. Select explicitly to repair.", MessageType.Warning); return; }
        EditorGUILayout.LabelField(definition.SourceLabel, EditorStyles.wordWrappedMiniLabel);
        EditorGUILayout.LabelField(definition.Kind + " · Preview: " + definition.FormatValue(definition.Read(null).Value, null), EditorStyles.miniLabel);
        try { definition.FormatValue(definition.Read(null).Value, cell.FindPropertyRelative("valueFormat").stringValue); }
        catch (System.FormatException) { EditorGUILayout.HelpBox("Invalid value format", MessageType.Warning); }
    }
    private static void Select(ScriptableObject meta, string path, StatValueDefinition definition)
    {
        if (meta == null) return;
        var serialized = new SerializedObject(meta);
        var cell = serialized.FindProperty(path);
        if (cell == null) return;
        cell.FindPropertyRelative("useBinding").boolValue = definition != null;
        if (definition != null)
        {
            cell.FindPropertyRelative("sourcePath").stringValue = definition.SourcePath;
            cell.FindPropertyRelative("statId").stringValue = definition.Id;
            cell.FindPropertyRelative("sourceAsset").objectReferenceValue = definition.SourceAsset;
        }
        serialized.ApplyModifiedProperties();
        // Persist newly introduced nested identity metadata together with its selected reference.
        StatBindingPicker.SaveIdentities(meta);
    }
    private static void Move(SerializedProperty list, int index, int step, string text)
    {
        using (new EditorGUI.DisabledScope(index + step < 0 || index + step >= list.arraySize))
            if (GUILayout.Button(text, GUILayout.Width(24))) { list.MoveArrayElement(index, index + step); CommitAndExit(list); }
    }
    private static void CommitAndExit(SerializedProperty p) { p.serializedObject.ApplyModifiedProperties(); GUIUtility.ExitGUI(); }
}
