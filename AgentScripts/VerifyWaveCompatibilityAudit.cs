using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Unity.Entities;
using UnityEditor;
using UnityEngine;

public static class VerifyWaveCompatibilityAudit
{
    public static async Task<object> Run()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Run outside Play Mode.");
        int worlds = World.All.Count;
        int seed = NumericRandomPreviewControls.SeedOffset;
        var results = new List<object>();
        foreach (var path in new[] {
            "Assets/Waves/1/1_Extreme/1_1_extrime.prefab", "Assets/Waves/1/1_Extreme/1_2_extrime.prefab",
            "Assets/Waves/1/1_Extreme/1_3_extrime.prefab", "Assets/Waves/1/1_Extreme/1_4_extrime.prefab",
            "Assets/Waves/2/2_1_extrime.prefab", "Assets/Waves/2/2_2_extrime.prefab", "Assets/Waves/2/2_3_extrime.prefab" })
        {
            byte[] before = Digest(path);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null || !prefab.TryGetComponent<Wave>(out var wave))
                throw new InvalidOperationException("Wave asset missing: " + path);
            int cues, projectiles;
            using (var simulation = new WaveConductorShootingPreview(wave, Vector3.down * 3, spawnSeed: seed))
            {
                simulation.PrepareInitialState();
                while (!simulation.AdvanceTo(10f, 5)) await Task.Yield();
                cues = simulation.CueCount; projectiles = simulation.ProjectileCount;
                for (int cue = 0; cue < cues; cue++)
                {
                    var session = simulation.GetSession(cue);
                    if (session == null) continue;
                    var positions = new Dictionary<int, Vector3>(); session.CopyPreviewPositionsTo(positions);
                    foreach (var position in positions.Values)
                        if (!float.IsFinite(position.x) || !float.IsFinite(position.y) || !float.IsFinite(position.z))
                            throw new InvalidOperationException("Non-finite enemy pose: " + path);
                }
            }
            if (World.All.Count != worlds) throw new InvalidOperationException("World leaked: " + path);
            if (!Equal(before, Digest(path))) throw new InvalidOperationException("Serialized wave changed: " + path);
            results.Add(new { path, cues, projectilesAt10Seconds = projectiles, finitePoses = true, unchanged = true });
        }
        return new { passed = true, waves = results, leakedWorlds = 0, previewSmokeOnly = true };
    }
    private static byte[] Digest(string path)
    { using var algorithm = SHA256.Create(); return algorithm.ComputeHash(File.ReadAllBytes(path)); }
    private static bool Equal(byte[] first, byte[] second)
    { if (first.Length != second.Length) return false; for (int i = 0; i < first.Length; i++) if (first[i] != second[i]) return false; return true; }
}
