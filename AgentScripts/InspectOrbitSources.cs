using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class InspectOrbitSources
{
    public static string Main()
    {
        var result = new List<object>();
        foreach (string type in new[] { "HullContentDefinition", "HullCatalog", "ShipData" })
        foreach (string guid in AssetDatabase.FindAssets("t:" + type, new[] { "Assets" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var asset = AssetDatabase.LoadMainAssetAtPath(path);
            if (type == "ShipData")
            {
                var data = (ShipData)asset;
                result.Add(new { path, id = data.shipId, meta = AssetDatabase.GetAssetPath(data.ShipMetaConfig) });
                continue;
            }
            var so = new SerializedObject(asset);
            var fields = new Dictionary<string, object>();
            var it = so.GetIterator();
            while (it.NextVisible(true))
            {
                if (it.propertyType == SerializedPropertyType.ObjectReference)
                    fields[it.propertyPath] = AssetDatabase.GetAssetPath(it.objectReferenceValue);
                else if (it.propertyType == SerializedPropertyType.String)
                    fields[it.propertyPath] = it.stringValue;
                else if (it.propertyType == SerializedPropertyType.Enum)
                    fields[it.propertyPath] = it.enumDisplayNames[it.enumValueIndex];
            }
            result.Add(new { path, fields });
        }
        foreach (string guid in AssetDatabase.FindAssets("Arkanoid t:Prefab"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var components = new List<object>();
            foreach (var c in prefab.GetComponentsInChildren<Component>(true))
            {
                if (c == null) continue;
                var fields = new Dictionary<string, string>();
                var so = new SerializedObject(c);
                var it = so.GetIterator();
                while(it.NextVisible(true))
                    if(it.propertyType == SerializedPropertyType.ObjectReference)
                        if (it.propertyPath != "m_Script")
                            fields[it.propertyPath] = AssetDatabase.GetAssetPath(it.objectReferenceValue) + " :: " + (it.objectReferenceValue != null ? it.objectReferenceValue.name : "null");
                components.Add(new { go = c.gameObject.name, type = c.GetType().Name, fields });
            }
            result.Add(new { path, components });
        }
        foreach (string guid in AssetDatabase.FindAssets("Orbit", new[] { "Assets" }))
            result.Add(new { orbitAsset = AssetDatabase.GUIDToAssetPath(guid) });
        return Newtonsoft.Json.JsonConvert.SerializeObject(result);
    }
}
