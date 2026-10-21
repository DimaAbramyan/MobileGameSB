using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using UnityEditor;
using UnityEngine;

public static class InspectWavePreviewMotion
{
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    public static object Present()
    {
        const string path = "Assets/Waves/1/1_Extreme/1_4_extrime.prefab";
        var stage = UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage();
        var wave = stage != null && stage.assetPath == path ? stage.prefabContentsRoot.GetComponent<Wave>()
            : AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponent<Wave>();
        bool dirty = wave.gameObject.scene.isDirty;
        var settings = new List<object>();
        foreach (var sub in wave.GetComponentsInChildren<DirectedEnemySubWave>(true))
        {
            var resolved = new DirectedWaveAttackSettings();
            if (sub.AttackPatternBehaviour == null || !sub.AttackPatternBehaviour.TryCopyResolvedAttackSettingsTo(0, false, resolved, out _)) continue;
            var shooting = resolved.WaveBurstSettings.Shooting;
            settings.Add(new { subwave = sub.name, movement = shooting.MovementPattern.ToString(), angular = shooting.AngularSpeed,
                radial = shooting.OrbitRadialSpeed, layoutRotation = shooting.Rotates, layoutSpeed = shooting.Rotation.RotationSpeedDegreesPerSecond });
        }
        var window = WavePreviewPlayerWindow.Open(wave);
        typeof(WavePreviewPlayerWindow).GetField("followSelection", Flags).SetValue(window, false);
        var player = (WavePreviewPlayer)typeof(WavePreviewPlayerWindow).GetMethod("EnsurePlayer", Flags).Invoke(window, null);
        player.Seek(5); while (player.IsSeeking) player.Update(0, 1000); player.Pause(); window.Repaint();
        var view = SceneView.lastActiveSceneView;
        if (view != null) { view.in2DMode = true; view.LookAt(new Vector3(0, 0, 0), Quaternion.identity, 7, true); view.Repaint(); }
        if (wave.gameObject.scene.isDirty != dirty) throw new Exception("Preview changed source dirty flag");
        return new { presented = true, inPrefabStage = stage != null && stage.assetPath == path, time = player.CurrentTime, settings, sourceDirtyUnchanged = true };
    }
    public static async Task<object> Run()
    {
        var wave = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Waves/1/1_Extreme/1_4_extrime.prefab").GetComponent<Wave>();
        var details = new List<string>(); var roots = new HashSet<GameObject>(); roots.Add(wave.gameObject);
        foreach (var cue in wave.ScheduledSubWaves) if (cue?.SubWavePrefab != null) roots.Add(cue.SubWavePrefab);
        foreach (var root in roots)
        foreach (var sub in root.GetComponentsInChildren<DirectedEnemySubWave>(true))
        {
            details.Add(sub.name + " " + EditorJsonUtility.ToJson(sub));
            if (sub.AttackPatternBehaviour != null) details.Add("ATTACK " + EditorJsonUtility.ToJson(sub.AttackPatternBehaviour));
            var source = sub.GetConfiguredEnemyPrefabForSlot(0);
            if (source != null)
            {
                details.Add("PREFAB " + AssetDatabase.GetAssetPath(source) + " scale=" + source.transform.lossyScale.ToString("R"));
                foreach (var renderer in source.GetComponentsInChildren<SpriteRenderer>(true))
                    details.Add("SOURCE RENDERER " + renderer.name + " scale=" + renderer.transform.lossyScale.ToString("R") + " size=" + renderer.bounds.size.ToString("R"));
            }
        }
        using var player = new WavePreviewPlayer(wave, 30, new Vector3(0, -3));
        var summaries = new List<object>();
        foreach (float time in new[] { 2f, 5f, 10f, 15f, 20f, 30f })
        {
            player.Seek(time); while (player.IsSeeking) { player.Update(0, 100); await Task.Yield(); }
            var manager = player.Simulation.ProjectileWorld.SimulationWorld.EntityManager;
            using var query = manager.CreateEntityQuery(typeof(EnemyProjectileMovement), typeof(LocalTransform));
            var groups = new Dictionary<string, int>();
            using (var entities = query.ToEntityArray(Allocator.Temp))
            foreach (var entity in entities)
            {
                var movement = manager.GetComponentData<EnemyProjectileMovement>(entity);
                string key = $"{movement.Pattern}/{movement.OrbitCenterMode}/angular={movement.AngularSpeedDegreesPerSecond:R}/radial={movement.OrbitRadialSpeed:R}/reference={movement.ReferenceSpeed:R}";
                if (!groups.TryAdd(key, 1)) groups[key]++;
            }
            float largest = 0; string largestName = null;
            for (int cue = 0; cue < player.Simulation.CueCount; cue++)
            {
                var session = player.Simulation.GetSession(cue); if (session == null) continue;
                var visualRoot = (GameObject)typeof(WaveShootingPreviewSession).GetField("visualsRoot", Flags).GetValue(session);
                foreach (var renderer in visualRoot.GetComponentsInChildren<SpriteRenderer>(true))
                {
                    float size = renderer.bounds.size.magnitude;
                    if (size > largest) { largest = size; largestName = renderer.name + " " + renderer.transform.lossyScale.ToString("R"); }
                }
            }
            summaries.Add(new { time, groups, largest, largestName, warning = player.Simulation.Warning });
        }
        File.WriteAllLines("AgentArtifacts/wave-preview-motion-authoring.txt", details);
        return new { wave = wave.name, summaries };
    }
}
