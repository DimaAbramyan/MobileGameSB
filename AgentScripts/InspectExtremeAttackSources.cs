using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class InspectExtremeAttackSources
{
    public static object Details()
    {
        var root = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Waves/1_Extreme/1_4_extrime.prefab");
        Transform fourWay = null;
        foreach (var child in root.GetComponentsInChildren<Transform>(true))
            if (child.name == "FourWay") fourWay = child;
        var result = new List<object>();
        foreach (var component in fourWay.GetComponentsInChildren<Component>(true))
        {
            if (component == null) continue;
            result.Add(new { name = component.name, parent = component.transform.parent?.name, type = component.GetType().Name });
        }
        var wave = fourWay.GetComponent<DirectedEnemySubWave>();
        var prefab = wave.GetConfiguredEnemyPrefabForSlot(0);
        var pointOverride = wave.GetConfiguredEnemyOverrideForSlot(0);
        var values = new Dictionary<string, object>();
        var so = new SerializedObject(pointOverride);
        foreach (string field in new[] { "overrideAttackPattern", "fireMode", "useLegacyFullAttackSettings", "overrideFourWayRotation" })
        {
            var p = so.FindProperty(field);
            values[field] = p.propertyType == SerializedPropertyType.Boolean ? (object)p.boolValue : p.enumNames[p.enumValueIndex];
        }
        var enemyComponents = new List<object>();
        foreach (var component in prefab.GetComponentsInChildren<Component>(true))
            if (component != null) enemyComponents.Add(new { name = component.name, type = component.GetType().Name });
        return new { waveComponents = result, resolvedPrefab = AssetDatabase.GetAssetPath(prefab), slot = values, enemyComponents };
    }
    public static object Run()
    {
        var paths = new[] { "Assets/Waves/1_Extreme/1_4_extrime.prefab",
            "Assets/Prefub/Enemies Prefab/FourWayEnemy.prefab", "Assets/Prefub/Enemies Prefab/TrippleShooter.prefab",
            "Assets/Prefub/EnemyShipsLegasy/FourWayEnemy.prefab" };
        var result = new List<object>();
        foreach (var path in paths)
        {
            var root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (root == null) continue;
            foreach (var component in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (component == null) continue;
                string type = component.GetType().Name;
                if (!(component is Enemy) && !type.Contains("Attack") && !type.Contains("Override")
                    && !type.Contains("SubWave")) continue;
                var fields = new Dictionary<string, object>();
                var so = new SerializedObject(component);
                var property = so.GetIterator();
                while (property.Next(true))
                {
                    if (property.propertyType == SerializedPropertyType.Boolean) fields[property.propertyPath] = property.boolValue;
                    else if (property.propertyType == SerializedPropertyType.Enum) fields[property.propertyPath] = property.enumNames[property.enumValueIndex];
                    else if (property.propertyType == SerializedPropertyType.ObjectReference && property.objectReferenceValue != null)
                        fields[property.propertyPath] = AssetDatabase.GetAssetPath(property.objectReferenceValue) + ":" + property.objectReferenceValue.name;
                    else if (property.propertyPath.Contains("selectedEnemy") && property.propertyType == SerializedPropertyType.Integer)
                        fields[property.propertyPath] = property.intValue;
                }
                result.Add(new { asset = path, name = component.name, type, fields });
            }
        }
        return result;
    }
}
