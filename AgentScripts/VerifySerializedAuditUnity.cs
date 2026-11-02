using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class VerifySerializedAuditUnity
{
    public static object Run(string[] paths, string[] missingGuids)
    {
        var checkedPaths = new HashSet<string>();
        var issues = new List<object>();
        foreach (var path in paths)
        {
            if (!checkedPaths.Add(path)) continue;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) { issues.Add(new { path, problem = "AssetDatabase cannot load prefab" }); continue; }
            foreach (var transform in prefab.GetComponentsInChildren<Transform>(true))
            {
                int missing = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject);
                if (missing > 0) issues.Add(new { path, gameObject = transform.name, missingScripts = missing });
                var animation = transform.GetComponent<Animation>();
                if (animation == null) continue;
                using var serialized = new SerializedObject(animation);
                var clip = serialized.FindProperty("m_Animation");
                if (clip != null && clip.objectReferenceValue == null)
                    issues.Add(new { path, gameObject = transform.name, missingDefaultAnimation = true });
            }
        }
        var guids = new HashSet<string>(); var unresolved = new List<string>();
        foreach (var guid in missingGuids)
            if (guids.Add(guid) && string.IsNullOrEmpty(AssetDatabase.GUIDToAssetPath(guid))) unresolved.Add(guid);
        return new { readOnly = true, prefabsChecked = checkedPaths.Count, unresolvedGuids = unresolved, issues, assetsModified = false };
    }
}
