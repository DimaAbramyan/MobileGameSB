using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;

public static class VerifyDiveWaveDefaults
{
    public static object Run()
    {
        string[] paths = { "Assets/Waves/1/1_Extreme/1_1_extrime.prefab", "Assets/Waves/1/1_Extreme/1_2_extrime.prefab",
            "Assets/Waves/1/1_Extreme/1_3_extrime.prefab", "Assets/Waves/1/1_Extreme/1_4_extrime.prefab",
            "Assets/Waves/2/2_1_extrime.prefab", "Assets/Waves/2/2_2_extrime.prefab", "Assets/Waves/2/2_3_extrime.prefab" };
        int subwaves = 0, slots = 0; var result = new List<object>();
        foreach (var path in paths)
        {
            byte[] before = File.ReadAllBytes(path); var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                foreach (var transform in root.GetComponentsInChildren<Transform>(true))
                    if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject) > 0) throw new Exception("Missing scripts: " + path);
                foreach (var wave in root.GetComponentsInChildren<DirectedEnemySubWave>(true))
                {
                    subwaves++; int count = wave.GetConfiguredEnemySlotCount(); slots += count;
                    for (int i = 0; i < count; i++)
                    {
                        var settings = new DirectedWaveAttackSettings();
                        if (wave.AttackPatternBehaviour != null && wave.AttackPatternBehaviour.TryCopyResolvedAttackSettingsTo(i, false, settings)
                            && (settings.MinimumDiveDuration != 0 || settings.MaximumDiveSpeed != 0)) throw new Exception("Existing wave unexpectedly enabled limits: " + path);
                    }
                }
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            using var sha = SHA256.Create(); string hash = Convert.ToBase64String(sha.ComputeHash(before));
            if (Convert.ToBase64String(sha.ComputeHash(File.ReadAllBytes(path))) != hash) throw new Exception("Prefab changed: " + path);
            result.Add(new { path, hash });
        }
        return new { passed = true, prefabs = paths.Length, subwaves, slots, allLimitsDisabled = true, missingScripts = 0, noPrefabWrites = true, hashes = result };
    }
}
