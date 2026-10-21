using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

// Read-only snapshots of the seven waves authorized for this audit. Never saves loaded contents.
public static class AuditWaveCompatibility
{
    public static readonly string[] Paths = {
        "Assets/Waves/1/1_Extreme/1_1_extrime.prefab",
        "Assets/Waves/1/1_Extreme/1_2_extrime.prefab",
        "Assets/Waves/1/1_Extreme/1_3_extrime.prefab",
        "Assets/Waves/1/1_Extreme/1_4_extrime.prefab",
        "Assets/Waves/2/2_1_extrime.prefab",
        "Assets/Waves/2/2_2_extrime.prefab",
        "Assets/Waves/2/2_3_extrime.prefab" };

    public static object Capture(string name)
    {
        return CaptureRange(name, 0, Paths.Length);
    }

    public static object CaptureRange(string name, int first, int countToCapture)
    {
        var snapshots = new List<object>();
        int subwaves = 0, slots = 0, samples = 0;
        for (int pathIndex = first; pathIndex < first + countToCapture; pathIndex++)
        {
            string path = Paths[pathIndex];
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var waves = new List<object>();
                foreach (var wave in root.GetComponentsInChildren<DirectedEnemySubWave>(true))
                {
                    subwaves++;
                    int count = wave.GetConfiguredEnemySlotCount(); slots += count;
                    var profiles = new List<object>();
                    for (int i = 0; i < count; i++)
                    {
                        if (wave.GetConfiguredEnemyPrefabForSlot(i) == null)
                            throw new Exception(path + "/" + wave.name + ": missing enemy slot " + i);
                        var settings = new DirectedWaveAttackSettings();
                        bool resolved = wave.AttackPatternBehaviour != null
                            && wave.AttackPatternBehaviour.TryCopyResolvedAttackSettingsTo(i, false, settings);
                        profiles.Add(new { slot = i, resolved,
                            settings = resolved ? JsonUtility.ToJson(settings) : null,
                            speed = resolved && settings.HasRuntimeProjectileBaseSpeedOverride
                                ? settings.RuntimeProjectileBaseSpeedOverride : 0f });
                    }
                    var order = WaveShootingPreviewSession.BuildSpawnOrder(wave);
                    var positions = new Dictionary<int, Vector3>();
                    var frames = new List<object>();
                    float route = wave.GetSimulationPreviewPostStartTime();
                    var times = new List<float> { 0f, .1f, .5f, 1f, 2f, 4f, 8f, 16f, 30f, 60f, 120f,
                        Mathf.Max(0f, route - .001f), route, route + .001f, route + 1f, route + 5f };
                    times.Sort();
                    foreach (float time in times)
                    {
                        wave.EvaluateSimulationPreviewNonAlloc(time, root.transform, order, positions, out _);
                        var values = new List<float[]>();
                        for (int i = 0; i < count; i++)
                            if (positions.TryGetValue(i, out var p))
                            {
                                if (!float.IsFinite(p.x) || !float.IsFinite(p.y)) throw new Exception("Nonfinite route");
                                values.Add(new[] { (float)i, p.x, p.y, p.z }); samples++;
                            }
                        frames.Add(new { time, values });
                    }
                    waves.Add(new { wave.name, count, order, profiles, frames });
                }
                using var sha = SHA256.Create();
                snapshots.Add(new { path, fileHash = Convert.ToBase64String(sha.ComputeHash(File.ReadAllBytes(path))), waves });
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        string json = JsonConvert.SerializeObject(snapshots, Formatting.Indented);
        string output = "AgentArtifacts/wave-audit-" + name
            + (countToCapture == Paths.Length ? "" : "-" + first) + ".json";
        File.WriteAllText(output, json);
        string baseline = "AgentArtifacts/wave-audit-baseline.json";
        bool equal = true;
        if (name != "baseline")
        {
            var before = Newtonsoft.Json.Linq.JArray.Parse(File.ReadAllText(baseline));
            var after = Newtonsoft.Json.Linq.JArray.Parse(json);
            for (int i = 0; i < after.Count; i++)
                equal &= Newtonsoft.Json.Linq.JToken.DeepEquals(before[first + i], after[i]);
        }
        if (!equal) throw new Exception("Wave snapshots differ; compare " + baseline + " and " + output);
        return new { paths = countToCapture, subwaves, slots, positionSamples = samples, identical = equal, output };
    }
}
