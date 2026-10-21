using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

// Runs in the connected Editor without saving or modifying source prefabs.
public static class VerifyWavePreviewCoordinatorActivation
{
    private const string Path = "Assets/Waves/2/2_2_extrime.prefab";
    private static int checks;
    private static void Check(bool condition, string message)
    { checks++; if (!condition) throw new Exception(message); }

    private static async Task Seek(WavePreviewPlayer player, float time)
    {
        player.Seek(time);
        for (int guard = 0; player.IsSeeking; guard++)
        {
            if (guard > 1000) throw new Exception("Seek stalled");
            player.Update(0, 1000);
            await Task.Yield();
        }
        Check(Mathf.Abs(player.CurrentTime - time) < WavePreviewPlayer.StepInterval * .51f,
            "Requested frame reached at " + time);
    }

    private static string Capture(WavePreviewPlayer player)
    {
        var state = new List<string>();
        var simulation = player.Simulation;
        for (int cue = 0; cue < simulation.CueCount; cue++)
        {
            var session = simulation.GetSession(cue);
            if (session == null) continue;
            var positions = new Dictionary<int, Vector3>();
            session.CopyPreviewPositionsTo(positions);
            foreach (var position in positions)
                state.Add("E" + cue + "/" + position.Key + ":" + position.Value.ToString("R"));
        }
        var manager = simulation.ProjectileWorld.SimulationWorld.EntityManager;
        using var query = manager.CreateEntityQuery(typeof(EnemyProjectileMovement), typeof(LocalTransform));
        using var entities = query.ToEntityArray(Allocator.Temp);
        foreach (var entity in entities)
        {
            string value = JsonUtility.ToJson(manager.GetComponentData<LocalTransform>(entity))
                + JsonUtility.ToJson(manager.GetComponentData<EnemyProjectileMovement>(entity));
            if (manager.HasComponent<EnemyLaserState>(entity))
                value += JsonUtility.ToJson(manager.GetComponentData<EnemyLaserState>(entity));
            state.Add(value);
        }
        state.Sort(StringComparer.Ordinal);
        var result = new StringBuilder(string.Join("\n", state));
        foreach (var e in player.Events)
            result.Append($"\n{e.Time:R}|{e.Kind}|{e.Cue}|{e.Slot}|{e.Count}|{e.Detail}");
        return result.ToString();
    }

    private static void CheckDisabled(Wave wave, string reason)
    {
        using var player = new WavePreviewPlayer(wave, 30, new Vector3(0, -3));
        player.Play(); player.Pause();
        Check(player.Simulation.CoordinatorPlayer == null, reason);
        Check(player.Simulation.IsConfigurationCurrent, reason + " does not invalidate the preview");
    }

    public static async Task<object> Run()
    {
        checks = 0;
        int worlds = World.All.Count;
        int seed = NumericRandomPreviewControls.SeedOffset;
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        bool dirty = scene.isDirty;
        byte[] bytes = File.ReadAllBytes(Path);
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(Path);
        string serializedAsset = EditorJsonUtility.ToJson(asset.GetComponent<WaveAttackCoordinator>());
        var root = PrefabUtility.LoadPrefabContents(Path);
        var timeline = new List<string>();
        try
        {
            NumericRandomPreviewControls.SetSeedOffset(0);
            var assetWave = asset.GetComponent<Wave>();
            var instanceWave = root.GetComponent<Wave>();
            Check(!asset.scene.IsValid() && !asset.activeInHierarchy, "Regression uses an inactive Project prefab asset");
            Check(root.activeInHierarchy, "Comparison uses an active loaded prefab instance");
            using (var fromAsset = new WavePreviewPlayer(assetWave, 30, new Vector3(0, -3)))
            using (var fromInstance = new WavePreviewPlayer(instanceWave, 30, new Vector3(0, -3)))
            {
                fromAsset.Play(); fromAsset.Pause();
                fromInstance.Play(); fromInstance.Pause();
                Check(fromAsset.Simulation.CoordinatorPlayer != null, "Enabled prefab asset coordinator must run in Preview");
                Check(fromInstance.Simulation.CoordinatorPlayer != null, "Enabled instance coordinator runs in Preview");
                foreach (float time in new[] { .5f, 1f, 3f, 7f, 10f, 14f, 20f })
                {
                    await Seek(fromAsset, time); await Seek(fromInstance, time);
                    Check(fromAsset.Simulation.IsConfigurationCurrent, "Asset preview remains current at " + time);
                    Check(fromInstance.Simulation.IsConfigurationCurrent, "Instance preview remains current at " + time);
                    Check(Capture(fromAsset) == Capture(fromInstance), "Asset and instance movement/projectile/event state matches at " + time);
                }
                bool first = false, second = false;
                foreach (var e in fromAsset.Events)
                {
                    if (e.Kind != WavePreviewEventKind.CoordinatorGroupChange) continue;
                    timeline.Add(e.Time.ToString("F3", System.Globalization.CultureInfo.InvariantCulture) + " " + e.Detail);
                    first |= e.Detail.StartsWith("1."); second |= e.Detail.StartsWith("2.");
                }
                Check(first && second, "Both coordinator groups participate in the replay");
                string end = Capture(fromAsset);
                await Seek(fromAsset, 5); await Seek(fromAsset, 20);
                Check(Capture(fromAsset) == end, "Prefab asset rewind preserves coordinator timing and movement");
            }

            var coordinator = root.GetComponent<WaveAttackCoordinator>();
            coordinator.enabled = false;
            CheckDisabled(instanceWave, "Disabled component is respected");
            coordinator.enabled = true;
            coordinator.Settings.enabled = false;
            CheckDisabled(instanceWave, "Disabled settings are respected");
            coordinator.Settings.enabled = true;
            root.SetActive(false);
            CheckDisabled(instanceWave, "Inactive root is respected");
            root.SetActive(true);
            var parent = new GameObject("Inactive preview regression parent");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(parent, root.scene);
            try
            {
                root.transform.SetParent(parent.transform, false); parent.SetActive(false);
                CheckDisabled(instanceWave, "Inactive ancestor is respected");
            }
            finally { root.transform.SetParent(null, false); Object.DestroyImmediate(parent); }
            Check(serializedAsset == EditorJsonUtility.ToJson(asset.GetComponent<WaveAttackCoordinator>()), "Source coordinator serialization unchanged");
            Check(Convert.ToBase64String(bytes) == Convert.ToBase64String(File.ReadAllBytes(Path)), "Source prefab bytes unchanged");
            Check(scene.isDirty == dirty, "Real scene dirty state unchanged");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
            NumericRandomPreviewControls.SetSeedOffset(seed);
        }
        Check(World.All.Count == worlds, "No leaked Preview worlds");
        var result = new { passed = true, checks, prefabAssetInstanceParity = true, timeline };
        File.WriteAllText("AgentArtifacts/wave-player-coordinator-activation-proof.json",
            Newtonsoft.Json.JsonConvert.SerializeObject(result, Newtonsoft.Json.Formatting.Indented));
        return result;
    }
}
