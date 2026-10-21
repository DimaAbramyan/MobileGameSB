using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class FixExtremeFourWayProjectileMovement
{
    private const string Path = "Assets/Waves/1_Extreme/1_4_extrime.prefab";
    public static object Inspect() => Process(false);
    public static object Run() => Process(true);
    private static object Process(bool apply)
    {
        if (EditorApplication.isPlaying || EditorApplication.isCompiling || EditorApplication.isUpdating)
            throw new Exception("Editor must be idle in Edit Mode.");
        var stage = PrefabStageUtility.GetCurrentPrefabStage();
        bool useStage = stage != null && stage.assetPath == Path;
        var root = useStage ? stage.prefabContentsRoot : AssetDatabase.LoadAssetAtPath<GameObject>(Path);
        DirectedEnemySubWave wave = null;
        foreach (var item in root.GetComponentsInChildren<DirectedEnemySubWave>(true))
            if (item.name == "FourWay") wave = item;
        var behaviour = wave.GetComponent<DirectedWaveAttackBehaviour>();
        var shared = new SerializedObject(behaviour).FindProperty("attackSettings.waveBurstSettings.shooting");
        var local = wave.GetConfiguredEnemyOverrideForSlot(0);
        var so = new SerializedObject(local);
        var movement = so.FindProperty("attackPattern.shooting.projectileMovementPattern");
        string before = movement.enumNames[movement.enumValueIndex];
        if (apply)
        {
            Undo.RecordObject(local, "Match FourWay local projectile movement to wave");
            movement.enumValueIndex = shared.FindPropertyRelative("projectileMovementPattern").enumValueIndex;
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(local);
            if (useStage)
            {
                EditorSceneManager.MarkSceneDirty(stage.scene);
                PrefabUtility.SaveAsPrefabAsset(root, Path);
            }
            else AssetDatabase.SaveAssetIfDirty(local);
        }
        return new { applied = apply, usedPrefabStage = useStage,
            sharedMovement = shared.FindPropertyRelative("projectileMovementPattern").enumNames[shared.FindPropertyRelative("projectileMovementPattern").enumValueIndex],
            localMovementBefore = before, localMovementAfter = movement.enumNames[movement.enumValueIndex],
            sharedRotate = shared.FindPropertyRelative("rotate").boolValue,
            localRotate = so.FindProperty("attackPattern.shooting.rotate").boolValue };
    }
}
