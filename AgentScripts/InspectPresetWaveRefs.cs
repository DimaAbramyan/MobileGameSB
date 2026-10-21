using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
public static class InspectPresetWaveRefs
{
    public static object Run()
    {
        string path = "Assets/Waves/1/1_Extreme/1_2_extrime.prefab";
        var root = AssetDatabase.LoadAssetAtPath<GameObject>(path); var values = new List<object>();
        foreach (var wave in root.GetComponentsInChildren<DirectedEnemySubWave>(true))
        {
            using var serialized = new SerializedObject(wave); var enemy = serialized.FindProperty("enemyPrefab");
            values.Add(new { wave = wave.name, slots = wave.GetConfiguredEnemySlotCount(), enemyId = enemy.objectReferenceInstanceIDValue,
                enemy = enemy.objectReferenceValue != null ? AssetDatabase.GetAssetPath(enemy.objectReferenceValue) : null,
                resolved = wave.GetConfiguredEnemySlotCount() > 0 && wave.GetConfiguredEnemyPrefabForSlot(0) != null,
                dirty = EditorUtility.IsDirty(wave) });
        }
        return new { values, fileTime = System.IO.File.GetLastWriteTimeUtc(path).ToString("O") };
    }
}
