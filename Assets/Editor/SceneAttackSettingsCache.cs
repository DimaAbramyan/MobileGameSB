using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Unity.Profiling;

// Materialized configuration only. Never store aim, positions or execution state here.
// Consumers borrow these settings read-only; slot-specific mutations must use their own copy.
[InitializeOnLoad]
internal static class SceneAttackSettingsCache
{
    private sealed class Entry
    {
        public readonly DirectedWaveAttackSettings Settings = new();
        public bool Found;
        public int PatternIndex;
    }

    private sealed class Source
    {
        public int ConfigurationRevision;
        public int DirtyCount;
        public int PresetRevision;
        public readonly Dictionary<int, Entry> Post = new();
        public readonly Dictionary<int, Entry> Entrance = new();

        public void Reset(DirectedWaveAttackBehaviour behaviour)
        {
            // Editor dirty/preset notifications may precede OnValidate. Keep a
            // live controller's materialization stable during selection changes.
            if (!Application.isPlaying) behaviour.InvalidateResolvedGroupSettings();
            Post.Clear();
            Entrance.Clear();
            ConfigurationRevision = behaviour.ConfigurationRevision;
            DirtyCount = EditorUtility.GetDirtyCount(behaviour);
            PresetRevision = EnemyPresetRevision.Value;
        }
    }

    private static readonly Dictionary<DirectedWaveAttackBehaviour, Source> sources = new();
    private static readonly DirectedWaveAttackSettings defaults = new();
    internal static DirectedWaveAttackSettings Defaults => defaults;
    private static readonly ProfilerMarker LookupMarker = new("WaveEditor.SceneAttackSettings.Lookup");
    private static readonly ProfilerMarker ResolveMarker = new("WaveEditor.SceneAttackSettings.Resolve");

    static SceneAttackSettingsCache()
    {
        Selection.selectionChanged += Clear;
        Undo.undoRedoPerformed += Clear;
        EditorApplication.projectChanged += Clear;
        EditorApplication.hierarchyChanged += Clear;
        PrefabUtility.prefabInstanceUpdated += OnPrefabUpdated;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
        AssemblyReloadEvents.beforeAssemblyReload += Clear;
    }

    private static void OnPrefabUpdated(GameObject instance) => Clear();
    private static void OnPlayModeChanged(PlayModeStateChange state) => Clear();
    internal static void Clear() => sources.Clear();

    internal static bool TryGet(DirectedWaveAttackBehaviour behaviour, int slot, bool entrance,
        out DirectedWaveAttackSettings settings, out int patternIndex)
    {
        using var sample = LookupMarker.Auto();
        settings = defaults;
        patternIndex = -1;
        if (behaviour == null) return false;

        if (!sources.TryGetValue(behaviour, out var source))
        {
            source = new Source();
            source.Reset(behaviour);
            sources.Add(behaviour, source);
        }
        else if (source.ConfigurationRevision != behaviour.ConfigurationRevision
            || source.DirtyCount != EditorUtility.GetDirtyCount(behaviour)
            || source.PresetRevision != EnemyPresetRevision.Value)
        {
            source.Reset(behaviour);
        }

        // The resolver's entrance branch and Single branch use one shared configuration.
        // Keep their eligibility checks, and keep Sequence/Multiple slots separate: their
        // first matching pattern may differ even when the reported pattern index is -1.
        bool grouped = !entrance && behaviour.UseAttackGroups;
        if (grouped && behaviour.GetAttackGroupForSlot(slot) == null) return false;
        bool shared = entrance || !grouped && !behaviour.UsesAttackSequence && behaviour.AttackPatternCount == 0;
        if (shared && (!behaviour.AllowsSequenceSlot(slot)
            || entrance && !behaviour.EditorPreviewEntranceSettings.IsEnabled)) return false;

        var entries = entrance ? source.Entrance : source.Post;
        int key = shared ? -1 : grouped ? behaviour.GroupPreviewCacheKey(slot) : slot;
        if (!entries.TryGetValue(key, out var entry))
        {
            entry = new Entry();
            using (ResolveMarker.Auto())
                entry.Found = behaviour.TryCopyResolvedAttackSettingsTo(slot, entrance,
                    entry.Settings, out entry.PatternIndex);
            entries.Add(key, entry);
        }
        settings = entry.Settings;
        patternIndex = entry.PatternIndex;
        return entry.Found;
    }
}
