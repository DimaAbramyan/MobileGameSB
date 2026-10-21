using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using UnityEditor;
using UnityEngine;

public static class VerifyConductorAttackPreview
{
    static int checks;
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    static void Assert(bool value, string message) { checks++; if (!value) throw new Exception(message); }
    static void Set(object obj, string name, object value)
    {
        for (var type = obj.GetType(); type != null; type = type.BaseType)
        {
            var field = type.GetField(name, Flags | BindingFlags.DeclaredOnly);
            if (field != null) { field.SetValue(obj, value); return; }
            var property = type.GetProperty(name, Flags | BindingFlags.DeclaredOnly);
            if (property != null) { property.SetValue(obj, value); return; }
        }
        throw new Exception("Field missing: " + name);
    }
    static void Seek(WaveConductorShootingPreview preview, float time)
    {
        var timer = Stopwatch.StartNew();
        for (int i = 0; !preview.AdvanceTo(time, 4); i++)
            if (i > 2000 || timer.Elapsed.TotalSeconds > 12) throw new Exception("Seek stalled at " + preview.Time + " target=" + time);
    }
    static async Task SeekYielding(WaveConductorShootingPreview preview, float time)
    {
        for (int i = 0; !preview.AdvanceTo(time, 4); i++)
        { if (i > 2000) throw new Exception("Seek stalled"); await Task.Delay(1); }
    }
    static DirectedEnemySubWave CreateSubwave(Transform parent, string name, Vector3 position)
    {
        var root = new GameObject(name); root.transform.SetParent(parent, false); root.transform.localPosition = position;
        var wave = root.AddComponent<DirectedEnemySubWave>();
        Set(wave, "enemyPrefab", AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefub/Enemies Prefab/Enemy.prefab").GetComponent<Enemy>());
        Set(wave, "enemyCount", 1); Set(wave, "settleDuration", 0f); Set(wave, "spawnInterval", 0f);
        Set(wave, "formationCoordinateSpace", DirectedWaveCoordinateSpace.LocalToSubWave);
        var attack = root.AddComponent<DirectedWaveAttackBehaviour>();
        var settings = (DirectedWaveAttackSettings)typeof(DirectedWaveAttackBehaviour).GetField("attackSettings", Flags).GetValue(attack);
        Set(settings, "fireMode", DirectedWaveAttackFireMode.Aimed);
        Set(settings, "aimMode", DirectedWaveAttackAimMode.AimContinuous);
        Set(settings, "burstSettingsSource", DirectedWaveBurstSettingsSource.WaveOverride);
        Set(settings, "waitForPreviousAttack", true); Set(settings, "delayAfterAttack", 1000f);
        settings.WaveBurstSettings.ConfigureLegacySingleAttack(1, 0, 0);
        settings.WaveBurstSettings.Shooting.EmissionLayout.Configure(EnemyEmissionLayoutType.Point);
        settings.WaveBurstSettings.Shooting.ConfigureLegacyFan(1, 0, 0);
        settings.WaveBurstSettings.Shooting.SetProjectileBaseSpeedOverride(true, 3);
        Set(settings.WaveBurstSettings.Shooting, "overrideProjectileLifetime", true);
        Set(settings.WaveBurstSettings.Shooting, "projectileLifetime", 10f);
        settings.Validate();
        return wave;
    }
    public static async Task<object> Run()
    {
        foreach (var leftover in Resources.FindObjectsOfTypeAll<GameObject>())
            if (leftover.name == "Conductor attack verification" && UnityEditor.SceneManagement.EditorSceneManager.IsPreviewScene(leftover.scene))
            { var oldScene = leftover.scene; UnityEngine.Object.DestroyImmediate(leftover); UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(oldScene); }
        checks = 0;
        int initialWorlds = World.All.Count;
        var scene = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
        var root = new GameObject("Conductor attack verification");
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, scene);
        root.transform.position = new Vector3(7, 4);
        var conductor = root.AddComponent<Wave>();
        var first = CreateSubwave(root.transform, "First", new Vector3(-2, 0));
        var second = CreateSubwave(root.transform, "Second", new Vector3(2, 0));
        var schedule = new List<WaveSubWaveCue> { new(first.gameObject, 0), new(second.gameObject, .5f) };
        Set(conductor, "scheduledSubWaves", schedule);
        WaveConductorShootingPreview preview = null;
        WaveEditor editor = null;
        GameObject authoredRoot = null;
        Action<SceneView> draw = null;
        bool shown = EnemyShootingPreview.ShowFormationVolley;
        double denseStepMs = 0;
        var actual = new List<object>();
        try
        {
            EnemyShootingPreview.ShowFormationVolley = true;
            string before = EditorJsonUtility.ToJson(conductor);
            preview = new WaveConductorShootingPreview(conductor, new Vector3(7, -5));
            Assert(World.All.Count == initialWorlds + 1, "The whole conductor owns exactly one ECS world");
            Seek(preview, .4f);
            Assert(preview.ActiveCueCount == 1 && preview.GetSession(1) == null, "Delayed subwave has not started");
            Assert(preview.ProjectileCount == 1, "First subwave emitted its shot");
            var manager = preview.ProjectileWorld.SimulationWorld.EntityManager;
            Entity firstProjectile;
            Vector3 firstPosition;
            Vector3 velocity;
            using (var query = manager.CreateEntityQuery(typeof(EnemyProjectileMovement), typeof(LocalTransform), typeof(EnemyProjectileVelocity)))
            using (var entities = query.ToEntityArray(Allocator.Temp))
            {
                firstProjectile = entities[0];
                firstPosition = manager.GetComponentData<LocalTransform>(firstProjectile).Position;
                var v = manager.GetComponentData<EnemyProjectileVelocity>(firstProjectile).Value;
                velocity = new Vector3(v.x, v.y);
            }
            float firstTime = preview.Time;
            preview.TargetPosition = new Vector3(14, 4);
            Seek(preview, .8f);
            Assert(preview.ActiveCueCount == 2 && preview.ProjectileCount == 2, "Both scheduled subwaves emit after their delays: active=" + preview.ActiveCueCount + " shots=" + preview.ProjectileCount + " time=" + preview.Time + " post=" + second.GetSimulationPreviewPostStartTime() + " warning=" + preview.Warning);
            Assert(ReferenceEquals(preview.GetSession(0).ProjectileWorld, preview.GetSession(1).ProjectileWorld), "World is shared");
            Assert(preview.GetSession(0).TargetObject == preview.GetSession(1).TargetObject, "Player target is shared");
            Assert(preview.GetSession(1).TargetPosition == preview.TargetPosition, "Moved target is used by later subwaves");
            Vector3 moved = manager.GetComponentData<LocalTransform>(firstProjectile).Position;
            Assert((moved - firstPosition - velocity * (preview.Time - firstTime)).magnitude < .001f,
                "Projectile movement runs once per global step, regardless of active subwave count");
            var positions = (Dictionary<int, Vector3>)typeof(WaveShootingPreviewSession).GetField("positions", Flags).GetValue(preview.GetSession(0));
            var expected = new Dictionary<int, Vector3>();
            first.EvaluateSimulationPreviewNonAlloc(preview.GetSession(0).Time, conductor.transform, preview.GetSpawnOrder(0), expected, out _);
            Assert((positions[0] - expected[0]).sqrMagnitude < .00001f, "Shooting sprites and movement preview use the same parent transform");
            Assert(before == EditorJsonUtility.ToJson(conductor), "Preview never edits the authored schedule");
            preview.Dispose(); preview = null;
            Assert(World.All.Count == initialWorlds, "Disposing the conductor releases its world");

            Set(conductor, "scheduledSubWaves", new List<WaveSubWaveCue>());
            conductor.SubWavesToCreate = new List<GameObject> { first.gameObject, second.gameObject };
            using (var legacy = new WaveConductorShootingPreview(conductor, new Vector3(7, -5)))
            { Seek(legacy, .1f); Assert(legacy.ActiveCueCount == 2, "Legacy assigned list is previewed when no schedule is configured"); }
            Set(conductor, "scheduledSubWaves", schedule);

            foreach (var subwave in new[] { first, second })
            {
                Set(subwave, "enemyCount", 256);
                var settings = (DirectedWaveAttackSettings)typeof(DirectedWaveAttackBehaviour).GetField("attackSettings", Flags).GetValue(subwave.AttackPatternBehaviour);
                Set(settings, "fireMode", DirectedWaveAttackFireMode.Stationary);
                settings.WaveBurstSettings.Shooting.EmissionLayout.Configure(EnemyEmissionLayoutType.Radial, 1000);
                settings.WaveBurstSettings.Shooting.ConfigureLegacyFan(1000, -30, 30);
                settings.Validate();
            }
            preview = new WaveConductorShootingPreview(conductor, new Vector3(7, -5));
            Seek(preview, 2f);
            Assert(preview.EnemyCount == 64 && preview.ActiveCueCount == 2, "Dense wave samples enemies from every assigned subwave");
            Assert(preview.ProjectileCount <= 2048 && preview.Omitted > 0, "2048-projectile limit applies to the entire wave: active=" + preview.ProjectileCount + " omitted=" + preview.Omitted);
            var stopwatch = Stopwatch.StartNew();
            for (int i = 0; i < 10; i++) { stopwatch.Restart(); preview.AdvanceTo(preview.Time + 1f / 30, 4); denseStepMs = Math.Max(denseStepMs, stopwatch.Elapsed.TotalMilliseconds); }
            preview.Dispose(); preview = null;

            authoredRoot = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Waves/2/2_1_extrime.prefab"));
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(authoredRoot, scene);
            var authored = authoredRoot.GetComponent<Wave>();
            before = EditorJsonUtility.ToJson(authored);
            preview = new WaveConductorShootingPreview(authored, new Vector3(0, -3));
            Seek(preview, 15f);
            int participating = 0;
            var warning = authored.GetComponent<WaveDangerWarningController>();
            float warningDelay = warning != null && warning.ShouldPlayWarning ? warning.WarningDuration : 0f;
            for (int i = 0; i < authored.ScheduledSubWaves.Count; i++)
            {
                var cue = authored.ScheduledSubWaves[i];
                if (cue.SubWavePrefab == null || cue.SubWavePrefab.GetComponent<DirectedEnemySubWave>() == null) continue;
                participating++;
                var session = preview.GetSession(i);
                Assert(session != null, "Every directed subwave from actual 2_1_extrime participates: " + i);
                Assert(Math.Abs(session.Time - (preview.Time - cue.StartDelay - warningDelay)) < .001f, "Cue delay and danger warning share the movement clock");
                actual.Add(new { cue = i, name = cue.SubWavePrefab.name, localTime = session.Time, enemies = session.EnemyCount });
            }
            Assert(preview.CueCount == participating && preview.ProjectileCount > 0, "Actual wave shows combined shooting");
            Assert(before == EditorJsonUtility.ToJson(authored), "Actual prefab clone settings remain unchanged");
            preview.Dispose(); preview = null;

            editor = (WaveEditor)Editor.CreateEditor(authored);
            Set(editor, "showAttackPatterns", true); Set(editor, "previewDuration", 100f);
            typeof(WaveEditor).GetMethod("StartPreview", Flags).Invoke(editor, null);
            Set(editor, "previewStartTime", EditorApplication.timeSinceStartup - 2f);
            var view = SceneView.lastActiveSceneView;
            if (view == null) throw new Exception("Scene View required");
            var done = new TaskCompletionSource<bool>();
            draw = current =>
            {
                if (current != view || Event.current.type != EventType.Repaint || done.Task.IsCompleted) return;
                try { Assert((WaveConductorShootingPreview)typeof(WaveEditor).GetField("shootingPreview", Flags).GetValue(editor) != null, "Wave inspector creates combined preview"); done.SetResult(true); }
                catch (Exception error) { done.TrySetException(error); }
            };
            SceneView.duringSceneGui += draw; view.Repaint();
            if (await Task.WhenAny(done.Task, Task.Delay(10000)) != done.Task) throw new Exception("Scene View timeout");
            await done.Task;
            SceneView.duringSceneGui -= draw; draw = null;
            Set(editor, "showAttackPatterns", false);
            typeof(WaveEditor).GetMethod("DisposeShootingPreview", Flags).Invoke(editor, null);
            typeof(WaveEditor).GetMethod("UpdatePreview", Flags).Invoke(editor, null);
            Assert((bool)typeof(WaveEditor).GetField("previewPlaying", Flags).GetValue(editor), "Turning off shooting preserves movement preview");
            Assert(typeof(WaveEditor).GetField("shootingPreview", Flags).GetValue(editor) == null, "Turning off shooting releases its resources");
            typeof(WaveEditor).GetMethod("StopPreview", Flags).Invoke(editor, null);
            Assert(World.All.Count == initialWorlds, "Stop releases all combined preview worlds");
            return new { passed = true, checks, denseStepMs, actual };
        }
        finally
        {
            if (draw != null) SceneView.duringSceneGui -= draw;
            if (editor != null) UnityEngine.Object.DestroyImmediate(editor);
            preview?.Dispose();
            if (authoredRoot != null) UnityEngine.Object.DestroyImmediate(authoredRoot);
            UnityEngine.Object.DestroyImmediate(root);
            UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene);
            EnemyShootingPreview.ShowFormationVolley = shown;
        }
    }
}
