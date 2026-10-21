using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class ConfigureWaveWarningStencil
{
    private const int StencilReference = 1;

    public static object Execute()
    {
        var updatedPrefabs = new List<string>();
        string[] prefabGuids = AssetDatabase.FindAssets(
            "t:Prefab",
            new[] { "Assets/Waves" });

        for (int prefabIndex = 0;
             prefabIndex < prefabGuids.Length;
             prefabIndex++)
        {
            string path = AssetDatabase.GUIDToAssetPath(prefabGuids[prefabIndex]);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
                continue;

            WaveDangerWarningController[] controllers = prefab
                .GetComponentsInChildren<WaveDangerWarningController>(true);
            bool prefabChanged = false;
            for (int controllerIndex = 0;
                 controllerIndex < controllers.Length;
                 controllerIndex++)
            {
                var serialized = new SerializedObject(controllers[controllerIndex]);
                SerializedProperty stencilReference = serialized
                    .FindProperty("stencilReference");
                if (stencilReference == null
                    || stencilReference.intValue == StencilReference)
                {
                    continue;
                }

                stencilReference.intValue = StencilReference;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                prefabChanged = true;
            }

            if (!prefabChanged)
                continue;

            EditorUtility.SetDirty(prefab);
            updatedPrefabs.Add(path);
        }

        AssetDatabase.SaveAssets();
        return new
        {
            stencilReference = StencilReference,
            updatedPrefabs
        };
    }
}
