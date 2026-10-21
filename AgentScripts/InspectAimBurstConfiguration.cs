using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class InspectAimBurstConfiguration
{
    public static object Run()
    {
        const string path = "Assets/Waves/2/2_2_extrime.prefab";
        var stage = PrefabStageUtility.GetCurrentPrefabStage();
        var source = stage != null && stage.assetPath == path ? stage.prefabContentsRoot : AssetDatabase.LoadAssetAtPath<GameObject>(path);
        DirectedEnemySubWave wave = null;
        foreach (var candidate in source.GetComponentsInChildren<DirectedEnemySubWave>(true)) if (candidate.name == "SubWave_1 (2)") wave = candidate;
        if (wave == null) throw new Exception("SubWave_1 (2) missing");
        var attack = wave.AttackPatternBehaviour; var result = new List<object>();
        for (int i = 0; i < wave.GetConfiguredEnemySlotCount(); i++)
        {
            var settings = new DirectedWaveAttackSettings();
            if (!attack.TryCopyResolvedAttackSettingsTo(i, false, settings, out int patternIndex)) continue;
            var prefab = wave.GetConfiguredEnemyPrefabForSlot(i);
            var executor = prefab.GetComponent<SimpleEnemyAttackExecutor>();
            if (executor == null) continue;
            var local = wave.GetConfiguredEnemyOverrideForSlot(i);
            var profile = EnemyAttackSettingsResolver.CreateRuntimeCopy(EnemyAttackSettingsResolver.Resolve(executor, settings.UsesEnemyBurstSettings, settings.WaveBurstSettings), local, null);
            var patterns = new List<object>();
            for (int p = 0; p < profile.PatternCount; p++)
            {
                var pattern = profile.GetPattern(p); var s = pattern.Shooting;
                patterns.Add(new { p, repeatBurst = pattern.RepeatBurst, attacks = pattern.AttackShotCount, burstShots = pattern.BurstShotCount,
                    interval = pattern.AttackShotInterval, burstInterval = pattern.BurstShotInterval, layout = s.EmissionLayout.Type.ToString(),
                    sequential = s.EmissionLayout.IsSequential, originDefault = s.EmissionOrigin.IsDefault,
                    projectileType = s.ProjectileType.ToString(), facing = profile.FacingMode.ToString(), curve = s.Rotates });
            }
            result.Add(new { slot = i, patternIndex, enemy = prefab.name, aim = settings.AimMode.ToString(),
                effectiveAim = (local != null ? local.ResolveAimMode(settings.AimMode) : settings.AimMode).ToString(),
                source = settings.UsesEnemyBurstSettings ? "Enemy" : "Wave", patterns });
        }
        using var serialized = new SerializedObject(attack);
        return new { prefabStage = stage != null ? stage.assetPath : null, fromOpenStage = stage != null && stage.assetPath == path,
            attackMode = serialized.FindProperty("executionMode").enumValueIndex,
            rootAim = serialized.FindProperty("attackSettings.aimMode").enumValueIndex, patterns = result };
    }
}
