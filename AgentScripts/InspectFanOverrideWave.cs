using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class InspectFanOverrideWave
{
    public static object Run()
    {
        const string path = "Assets/Waves/1_Hard/1_4_hard.prefab";
        GameObject root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        var waves = new List<object>();
        foreach (var wave in root.GetComponentsInChildren<DirectedEnemySubWave>(true))
        {
            var slots = new List<object>();
            var behaviour = wave.AttackPatternBehaviour;
            var settings = new DirectedWaveAttackSettings();
            for (int i = 0; i < wave.GetConfiguredEnemySlotCount(); i++)
            {
                Enemy prefab = wave.GetConfiguredEnemyPrefabForSlot(i);
                var point = wave.GetConfiguredEnemyOverrideForSlot(i);
                bool hasSettings = behaviour != null && behaviour.CopyPreviewAttackSettingsTo(i, false, settings);
                slots.Add(new {
                    slot = i, prefab = prefab != null ? AssetDatabase.GetAssetPath(prefab) : null,
                    point = point != null ? Hierarchy(point.transform) : null,
                    pointMode = point?.AttackSettingsOverride?.Shooting.ShootingMode.ToString(),
                    pointProjectiles = point?.AttackSettingsOverride?.ProjectilesPerShot,
                    waveSettingsActive = hasSettings,
                    source = settings.UsesEnemyBurstSettings ? "Enemy Settings" : "Wave Override",
                    waveMode = settings.WaveBurstSettings.Shooting.ShootingMode.ToString(),
                    autonomousEntrance = behaviour != null && behaviour.AllowAutonomousAttackDuringEntrance
                });
            }
            waves.Add(new { name = Hierarchy(wave.transform), slots });
        }
        return waves;
    }
    private static string Hierarchy(Transform transform)
    {
        string path = transform.name;
        while (transform.parent != null) { transform = transform.parent; path = transform.name + "/" + path; }
        return path;
    }
}
