using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

public static class CaptureEmissionLayoutBaseline
{
    [Serializable] public class Document { public List<string> assets = new(); public List<Entry> entries = new(); public List<string> waves = new(); }
    [Serializable] public class Entry
    {
        public string asset, hierarchy, component, property, json;
        public int componentIndex, shootingType, directions, volley;
        public bool rotates;
        public List<Vector3> rays = new();
        public List<float> angles = new();
    }
    public static object Run()
    {
        var document = new Document();
        var paths = new HashSet<string>();
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Waves/1" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            document.waves.Add(path);
            paths.Add(path);
            foreach (string dependency in AssetDatabase.GetDependencies(path, true))
                if (dependency.EndsWith(".prefab") || dependency.EndsWith(".asset")) paths.Add(dependency);
        }
        foreach (string path in paths)
        {
            int before = document.entries.Count;
            var asset = AssetDatabase.LoadMainAssetAtPath(path);
            if (asset is GameObject root)
            {
                foreach (var component in root.GetComponentsInChildren<MonoBehaviour>(true))
                    if (component != null) Read(document, path, root.transform, component);
            }
            else if (asset is ScriptableObject) Read(document, path, null, asset);
            if (before != document.entries.Count) document.assets.Add(path);
        }
        document.assets.Sort(); document.waves.Sort();
        File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(), "AgentScripts/EmissionLayoutBaseline.json"),
            JsonConvert.SerializeObject(document, Formatting.Indented, new JsonSerializerSettings { ContractResolver = new FieldsResolver() }));
        int radial = 0; foreach (var entry in document.entries) if (entry.shootingType == 1) radial++;
        return new { waves = document.waves, assets = document.assets.Count, profiles = document.entries.Count, radial };
    }
    private static void Read(Document document, string path, Transform root, UnityEngine.Object target)
    {
        var serialized = new SerializedObject(target);
        var iterator = serialized.GetIterator();
        while (iterator.Next(true))
        {
            if (iterator.type != nameof(EnemyShootingSettings)) continue;
            var settings = (EnemyShootingSettings)iterator.boxedValue;
            var entry = new Entry { asset = path, component = target.GetType().FullName, property = iterator.propertyPath,
                hierarchy = target is Component c ? AnimationUtility.CalculateTransformPath(c.transform, root) : "",
                json = JsonUtility.ToJson(settings), shootingType = iterator.FindPropertyRelative("shootingType").enumValueIndex,
                directions = iterator.FindPropertyRelative("shootingType").enumValueIndex == 1
                    ? Mathf.Max(1, iterator.FindPropertyRelative("directionCount").intValue) : 1,
                rotates = iterator.FindPropertyRelative("shootingType").enumValueIndex == 1
                    && iterator.FindPropertyRelative("rotate").boolValue };
            entry.volley = entry.directions * settings.ProjectilesPerDirection;
            if (target is Component component)
            {
                var components = component.GetComponents(component.GetType());
                for (int i = 0; i < components.Length; i++) if (components[i] == component) entry.componentIndex = i;
            }
            foreach (float time in new[] { 0f, .37f, 1.4f, 8.5f })
            {
                entry.angles.Add(settings.Rotation.EvaluateAngle(time));
                foreach (bool aimed in new[] { false, true })
                {
                    // Reference model of the original Rotation implementation,
                    // using the retained serialized fields rather than the new layout.
                    var basis = Quaternion.Euler(0, 0, 27f)
                        * Quaternion.Euler(0, 0, entry.rotates ? settings.Rotation.EvaluateAngle(time) : 0f);
                    var direction = entry.shootingType == 1 ? basis * Vector3.up : new Vector3(.4f, -.9f, 0);
                    if (settings.UsesShotGun && !aimed) direction = basis * (Vector3)settings.ShotGunDirection;
                    direction = direction.sqrMagnitude < .0001f ? basis * Vector3.up : direction.normalized;
                    for (int arm = 0; arm < entry.directions; arm++)
                        for (int bullet = 0; bullet < settings.ProjectilesPerDirection; bullet++)
                        {
                            float angle = arm * (360f / entry.directions) + settings.RandomPreviewAngle;
                            if (settings.UsesFan && settings.FanProjectileCount > 1)
                            {
                                float minimum = iterator.FindPropertyRelative("fanMinAngle").floatValue;
                                float maximum = iterator.FindPropertyRelative("fanMaxAngle").floatValue;
                                angle += Mathf.Lerp(Mathf.Max(minimum, maximum), Mathf.Min(minimum, maximum),
                                    (bullet % settings.FanProjectileCount) / (float)(settings.FanProjectileCount - 1));
                            }
                            entry.rays.Add(Quaternion.Euler(0, 0, angle) * direction);
                        }
                }
            }
            document.entries.Add(entry);
        }
    }
    private class FieldsResolver : DefaultContractResolver
    {
        protected override IList<JsonProperty> CreateProperties(Type type, MemberSerialization serialization)
        {
            var properties = base.CreateProperties(type, MemberSerialization.Fields);
            if (type == typeof(Vector3))
                for (int i = properties.Count - 1; i >= 0; i--)
                    if (properties[i].PropertyName != "x" && properties[i].PropertyName != "y" && properties[i].PropertyName != "z")
                        properties.RemoveAt(i);
            return properties;
        }
    }
}
