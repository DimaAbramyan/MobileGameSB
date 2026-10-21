using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class ProbeRotationMismatch
{
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    public static object Run()
    {
        var root = PrefabStageUtility.GetCurrentPrefabStage().prefabContentsRoot;
        var result = new List<object>();
        foreach (var sub in root.GetComponentsInChildren<DirectedEnemySubWave>(true))
        {
            var settings = new DirectedWaveAttackSettings();
            if (sub.AttackPatternBehaviour == null || !sub.AttackPatternBehaviour.TryCopyResolvedAttackSettingsTo(0, false, settings, out _)) continue;
            var local = sub.GetConfiguredEnemyOverrideForSlot(0); var source = sub.GetConfiguredEnemyPrefabForSlot(0);
            var executor = source.GetComponent<SimpleEnemyAttackExecutor>();
            var profile = EnemyAttackSettingsResolver.CreateRuntimeCopy(EnemyAttackSettingsResolver.Resolve(executor, settings.UsesEnemyBurstSettings, settings.WaveBurstSettings), local, null);
            result.Add(new { name = sub.name, source = AssetDatabase.GetAssetPath(source), waveSpeed = settings.WaveBurstSettings.Shooting.Rotation.RotationSpeedDegreesPerSecond,
                waveMode = settings.WaveBurstSettings.Shooting.Rotation.RotationMode.ToString(), hasLocal = local != null,
                localRotation = local?.RotationOverride?.RotationSpeedDegreesPerSecond,
                profileSpeed = profile.Shooting.Rotation.RotationSpeedDegreesPerSecond, profileMode = profile.Shooting.Rotation.RotationMode.ToString(),
                faceMode = profile.FacingMode.ToString(), facingSpeed = profile.FacingRotation.RotationSpeedDegreesPerSecond,
                fireRate = source.FireRateMultiplier, attackSpeed = source.AttackSpeedMultiplier, rotationSettings = JsonUtility.ToJson(profile.Shooting.Rotation) });
        }
        return result;
    }
}
