using System;
using UnityEditor;
using UnityEngine;

public static class StatBindingPicker
{
    public static void Show(ScriptableObject meta, string path, string id, Action<StatValueDefinition> select, bool manual = false)
    {
        var menu = new GenericMenu();
        if (manual) { menu.AddItem(new GUIContent("Manual Text"), false, () => select(null)); menu.AddSeparator(""); }
        foreach (var definition in StatCatalogEditorCache.Get(meta).Values)
        {
            var captured = definition;
            menu.AddItem(new GUIContent(definition.SourceLabel + "/" + definition.Name), definition.SourcePath == path && definition.Id == id, () => select(captured));
        }
        menu.ShowAsContext();
    }
    public static void SaveIdentities(ScriptableObject meta)
    {
        foreach (var dependency in StatCatalogEditorCache.Get(meta).Dependencies)
        {
            if (!EditorUtility.IsPersistent(dependency)) continue;
            EditorUtility.SetDirty(dependency); AssetDatabase.SaveAssetIfDirty(dependency);
        }
    }
}
