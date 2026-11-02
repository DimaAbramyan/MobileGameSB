using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class StatCatalogEditorCache
{
    private sealed class Entry
    {
        public StatValueCatalog Catalog;
        public readonly Dictionary<Object, int> Revisions = new();
    }
    private static readonly Dictionary<ScriptableObject, Entry> entries = new();
    public static StatValueCatalog Get(ScriptableObject meta)
    {
        if (meta == null) return new StatValueCatalog();
        if (entries.TryGetValue(meta, out var entry))
        {
            bool valid = true;
            foreach (var pair in entry.Revisions)
                if (pair.Key == null || EditorUtility.GetDirtyCount(pair.Key) != pair.Value) { valid = false; break; }
            if (valid) return entry.Catalog;
        }
        entry = new Entry { Catalog = StatCatalogBuilder.Build(meta) };
        foreach (var dependency in entry.Catalog.Dependencies)
            entry.Revisions[dependency] = EditorUtility.GetDirtyCount(dependency);
        if (entries.Count > 64) entries.Clear();
        entries[meta] = entry;
        return entry.Catalog;
    }
}
