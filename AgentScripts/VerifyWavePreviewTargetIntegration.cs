using System;
using System.Collections.Generic;
using System.Reflection;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class VerifyWavePreviewTargetIntegration
{
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    static int checks;
    static void Check(bool condition, string message)
    { checks++; if (!condition) throw new Exception(message); }
    static void Set(object target, string name, object value)
    {
        for (var type = target.GetType(); type != null; type = type.BaseType)
        {
            var field = type.GetField(name, Flags);
            if (field != null) { field.SetValue(target, value); return; }
            var property = type.GetProperty(name, Flags);
            if (property != null) { property.SetValue(target, value); return; }
        }
        throw new Exception("Missing " + name);
    }
    static void Seek(WavePreviewPlayer player, float time)
    {
        player.Seek(time);
        for (int i = 0; player.IsSeeking; i++)
        { if (i > 500) throw new Exception("Seek stalled"); player.Update(0, 100); }
    }
    static string Capture(WavePreviewPlayer player)
    {
        var manager = player.Simulation.ProjectileWorld.SimulationWorld.EntityManager;
        using var query = manager.CreateEntityQuery(typeof(EnemyProjectileMovement), typeof(LocalTransform));
        using var entities = query.ToEntityArray(Allocator.Temp);
        var rows = new List<string>();
        foreach (var entity in entities)
            rows.Add(JsonUtility.ToJson(manager.GetComponentData<LocalTransform>(entity))
                + JsonUtility.ToJson(manager.GetComponentData<EnemyProjectileMovement>(entity)));
        rows.Sort(StringComparer.Ordinal);
        return string.Join("\n", rows);
    }
    public static object Run()
    {
        checks = 0;
        int worlds = World.All.Count;
        var scene = EditorSceneManager.NewPreviewScene();
        var root = new GameObject("Target integration verification");
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, scene);
        try
        {
            var wave = root.AddComponent<Wave>();
            var child = new GameObject("Aimed attack"); child.transform.SetParent(root.transform, false);
            child.transform.localPosition = new Vector3(0, 2);
            var subwave = child.AddComponent<DirectedEnemySubWave>();
            Set(subwave, "enemyPrefab", AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefub/Enemies Prefab/Enemy.prefab").GetComponent<Enemy>());
            Set(subwave, "enemyCount", 1); Set(subwave, "settleDuration", 0f); Set(subwave, "spawnInterval", 0f);
            Set(subwave, "formationCoordinateSpace", DirectedWaveCoordinateSpace.LocalToSubWave);
            var attack = child.GetComponent<DirectedWaveAttackBehaviour>() ?? child.AddComponent<DirectedWaveAttackBehaviour>();
            Set(attack, "allowAutonomousAttackDuringEntrance", false);
            var settings = (DirectedWaveAttackSettings)typeof(DirectedWaveAttackBehaviour).GetField("attackSettings", Flags).GetValue(attack);
            Set(settings, "fireMode", DirectedWaveAttackFireMode.Aimed);
            Set(settings, "aimMode", DirectedWaveAttackAimMode.AimContinuous);
            Set(settings, "burstSettingsSource", DirectedWaveBurstSettingsSource.WaveOverride);
            Set(settings, "waitForPreviousAttack", true); Set(settings, "delayAfterAttack", .1f);
            settings.WaveBurstSettings.ConfigureLegacySingleAttack(1, .1f, .15f);
            var shooting = settings.WaveBurstSettings.Shooting;
            shooting.ConfigureLegacyFan(1, 0, 0); shooting.SetProjectileBaseSpeedOverride(true, 1);
            Set(shooting, "overrideProjectileLifetime", true); Set(shooting, "projectileLifetime", 4f);
            settings.Validate();
            Set(wave, "scheduledSubWaves", new List<WaveSubWaveCue> { new(child, 0) });
            using (var player = new WavePreviewPlayer(wave, 6, new Vector3(0, -3)))
            {
                player.SnapshotInterval = .25f;
                Seek(player, 3f); player.Pause();
                var simulation = player.Simulation; int frame = player.CurrentFrame;
                var target = new Vector3(1.2f, -1.7f, 0);
                player.TargetPosition = target;
                Check(player.CurrentFrame == frame && !player.IsPlaying, "Drag preserves paused timeline");
                Check(ReferenceEquals(simulation, player.Simulation), "Drag does not restart simulation");
                Check(player.Simulation.TargetPosition == target && player.TargetHistory.Evaluate(frame) == target, "Both target coordinates propagate and are recorded");
                Seek(player, 4f);
                var manager = player.Simulation.ProjectileWorld.SimulationWorld.EntityManager;
                using (var query = manager.CreateEntityQuery(typeof(EnemyProjectileVelocity)))
                using (var velocities = query.ToComponentDataArray<EnemyProjectileVelocity>(Allocator.Temp))
                {
                    bool aimed = false;
                    foreach (var velocity in velocities) aimed |= velocity.Value.x > .01f;
                    Check(aimed, "Aimed projectiles use changed target X");
                }
                string state = Capture(player);
                Check(state.Length > 0, "Actual projectile state captured");
                Seek(player, 3f); Seek(player, 4f);
                Check(Capture(player) == state, "Rewind and fast seek restore identical projectiles");
                Check(player.TargetPosition == target && !player.IsPlaying, "Recorded XY target and pause survive seek");
                Check(player.RestoredSnapshots > 0, "Snapshot restore exercised");
            }
            Check(World.All.Count == worlds, "Owned ECS worlds disposed");
            return new { passed = true, checks, aiming = true, pausedDrag = true, history = true, snapshots = true, deterministicSeek = true };
        }
        finally { UnityEngine.Object.DestroyImmediate(root); EditorSceneManager.ClosePreviewScene(scene); }
    }
}
