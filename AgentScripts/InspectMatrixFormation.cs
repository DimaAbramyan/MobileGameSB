using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class InspectMatrixFormation
{
    public static object Run()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Waves/2/2_3_extrime.prefab");
        var stage = UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage();
        if (stage != null && stage.assetPath == "Assets/Waves/2/2_3_extrime.prefab") prefab = stage.prefabContentsRoot;
        var result = new List<object>();
        foreach (var wave in prefab.GetComponentsInChildren<DirectedEnemySubWave>(true))
        {
            using var data = new SerializedObject(wave);
            var iterator = data.GetIterator();
            var fields = new Dictionary<string, object>();
            while (iterator.Next(true))
            {
                if (iterator.name == "gridMatrixCells")
                {
                    int enabled = 0;
                    for (int i = 0; i < iterator.arraySize; i++) if (iterator.GetArrayElementAtIndex(i).boolValue) enabled++;
                    fields[iterator.propertyPath] = new { cells = iterator.arraySize, enabled };
                }
                if (iterator.name == "customFormationPoints")
                {
                    var positions = new List<float[]>();
                    for (int i = 0; i < iterator.arraySize; i++) { var p = iterator.GetArrayElementAtIndex(i).vector3Value; positions.Add(new[] { p.x, p.y, p.z }); }
                    fields[iterator.propertyPath] = positions;
                }
                if (iterator.name == "formationPointsRoot")
                {
                    var root = iterator.objectReferenceValue as Transform;
                    var points = new List<object>();
                    if (root != null) foreach (Transform child in root)
                        points.Add(new { child.name, position = new[] { child.localPosition.x, child.localPosition.y, child.localPosition.z }, active = child.gameObject.activeSelf });
                    fields[iterator.propertyPath] = points;
                }
            }
            var actual = new List<float[]>();
            for (int i = 0; i < wave.GetConfiguredEnemySlotCount(); i++) { var p = wave.GetSimulationFormationPosition(i); actual.Add(new[] { p.x, p.y, p.z }); }
            result.Add(new { wave.name, slots = wave.GetConfiguredEnemySlotCount(), formationComponent = wave.FormationBehaviour != null, fields, actual });
        }
        return Newtonsoft.Json.JsonConvert.SerializeObject(result);
    }
}
