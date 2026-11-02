using UnityEditor;
using UnityEngine;

// Undo, inactive objects and prefab imports do not always run component OnEnable.
// Observe composition edits instead of invalidating on every frame/hierarchy tick.
[InitializeOnLoad]
internal static class DirectedWaveBehaviourComponentCacheEditor
{
    static DirectedWaveBehaviourComponentCacheEditor()
    {
        ObjectChangeEvents.changesPublished += Changed;
        Undo.undoRedoPerformed += InvalidateLoaded;
        PrefabUtility.prefabInstanceUpdated += InvalidateHierarchy;
    }

    private static void Changed(ref ObjectChangeEventStream stream)
    {
        if (Application.isPlaying) return;
        for (int i = 0; i < stream.length; i++)
        {
            switch (stream.GetEventType(i))
            {
                case ObjectChangeKind.ChangeGameObjectStructure:
                    stream.GetChangeGameObjectStructureEvent(i, out var structure);
                    InvalidateObject(EditorUtility.EntityIdToObject(structure.instanceId));
                    break;
                case ObjectChangeKind.ChangeGameObjectStructureHierarchy:
                    stream.GetChangeGameObjectStructureHierarchyEvent(i, out var hierarchy);
                    InvalidateHierarchy(EditorUtility.EntityIdToObject(hierarchy.instanceId) as GameObject);
                    break;
                case ObjectChangeKind.ChangeGameObjectOrComponentProperties:
                    stream.GetChangeGameObjectOrComponentPropertiesEvent(i, out var properties);
                    InvalidateObject(EditorUtility.EntityIdToObject(properties.instanceId));
                    break;
                case ObjectChangeKind.UpdatePrefabInstances:
                    stream.GetUpdatePrefabInstancesEvent(i, out var prefabs);
                    for (int p = 0; p < prefabs.instanceIds.Length; p++)
                        InvalidateHierarchy(EditorUtility.EntityIdToObject(prefabs.instanceIds[p]) as GameObject);
                    break;
            }
        }
    }

    private static void InvalidateObject(Object value)
    {
        if (value == null || (value.hideFlags & HideFlags.DontSave) != 0) return;
        if (value is DirectedEnemySubWave wave) wave.InvalidateBehaviourComponentCache();
        else if (value is GameObject go)
        {
            if (go.TryGetComponent(out DirectedEnemySubWave owner)) owner.InvalidateBehaviourComponentCache();
        }
        else if (value is DirectedWaveFormationBehaviour || value is DirectedWavePhaseEntryBehaviour
            || value is DirectedWavePostBehaviour || value is DirectedWaveCompletionBehaviour || value is DirectedWaveAttackBehaviour)
        {
            DirectedWaveBehaviourCacheInvalidationForEditor((Component)value);
        }
    }

    private static void DirectedWaveBehaviourCacheInvalidationForEditor(Component component)
    {
        if (component.TryGetComponent(out DirectedEnemySubWave wave)) wave.InvalidateBehaviourComponentCache();
    }

    private static void InvalidateHierarchy(GameObject root)
    {
        if (root == null || (root.hideFlags & HideFlags.DontSave) != 0) return;
        foreach (var wave in root.GetComponentsInChildren<DirectedEnemySubWave>(true)) wave.InvalidateBehaviourComponentCache();
    }

    internal static void InvalidateLoaded()
    {
        foreach (var wave in Resources.FindObjectsOfTypeAll<DirectedEnemySubWave>()) wave.InvalidateBehaviourComponentCache();
    }
}

internal sealed class DirectedWaveBehaviourCachePrefabImport : AssetPostprocessor
{
    private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
    {
        if (ContainsPrefab(imported) || ContainsPrefab(deleted) || ContainsPrefab(moved) || ContainsPrefab(movedFrom))
            DirectedWaveBehaviourComponentCacheEditor.InvalidateLoaded();
    }
    private static bool ContainsPrefab(string[] paths)
    {
        foreach (string path in paths)
            if (path.EndsWith(".prefab", System.StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }
}
