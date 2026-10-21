using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class InspectLocalSpeedOverrides
{
    public static object Run()
    {
        var records = new List<object>();
        int slots = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Waves" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            foreach (var wave in root.GetComponentsInChildren<DirectedEnemySubWave>(true))
            {
                var behaviour = wave.GetComponent<DirectedWaveAttackBehaviour>();
                for (int i = 0; i < wave.GetConfiguredEnemySlotCount(); i++)
                {
                    var local = wave.GetConfiguredEnemyOverrideForSlot(i);
                    if (local == null) continue;
                    slots++;
                    var so = new SerializedObject(local);
                    var profile = (EnemyBurstAttackSettings)so.FindProperty("attackPattern").boxedValue;
                    var resolved = new DirectedWaveAttackSettings();
                    bool shared = behaviour != null && behaviour.TryCopyResolvedAttackSettingsTo(i, false, resolved) && resolved.HasRuntimeProjectileBaseSpeedOverride;
                    var speeds = new List<object>();
                    for (int p = 0; p < profile.PatternCount; p++)
                    {
                        var shooting = profile.GetPattern(p).Shooting;
                        if (shooting.OverridesProjectileBaseSpeed) speeds.Add(new { pattern = p, speed = shooting.InitialSpeed });
                    }
                    if (so.FindProperty("overrideProjectileBaseSpeed").boolValue || speeds.Count > 0)
                        records.Add(new { path, wave = wave.name, slot = i, active = local.HasAttackPatternOverride,
                            common = so.FindProperty("overrideProjectileBaseSpeed").boolValue,
                            commonSpeed = so.FindProperty("projectileBaseSpeed").floatValue,
                            shared, sharedSpeed = resolved.RuntimeProjectileBaseSpeedOverride, speeds });
                }
            }
        }
        return new { slots, records };
    }
}
