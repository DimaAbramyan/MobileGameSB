using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class InspectLaserBouncePrefab
{
    const string Path = "Assets/Waves/3/3_2_extrime.prefab";
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    static object Field(object o, string n) => o.GetType().GetField(n, Flags).GetValue(o);
    static void Tick(World world, EnemyLaserSystem system, float time)
    {
        world.SetTime(new Unity.Core.TimeData(time, .02f));
        system.Update();
    }
    public static object Fix()
    {
        var stage = PrefabStageUtility.GetCurrentPrefabStage();
        if (stage == null || stage.assetPath != Path || stage.scene.isDirty)
            throw new InvalidOperationException("Expected the unchanged 3_2_extrime Prefab Stage.");
        var changes = new List<string>();
        foreach (var wave in stage.prefabContentsRoot.GetComponentsInChildren<DirectedEnemySubWave>(true))
        {
            var behaviour = wave.AttackPatternBehaviour; if (behaviour == null) continue;
            using var so = new SerializedObject(behaviour);
            using var p = so.GetIterator();
            while (p.Next(true))
            {
                if (p.type != nameof(EnemyShootingSettings)
                    || p.FindPropertyRelative("projectileType").intValue != (int)EnemyProjectileType.Laser
                    || !p.FindPropertyRelative("screenBoundary.bounceOnScreenBounds").boolValue) continue;
                var geometry = p.FindPropertyRelative("laser.geometry");
                if (geometry.FindPropertyRelative("lengthMode").intValue != (int)EnemyLaserLengthMode.ToScreenEdge) continue;
                var offset = geometry.FindPropertyRelative("lengthOffset");
                if (offset.floatValue != 0f) continue;
                Undo.RecordObject(behaviour, "Extend laser path after screen bounce");
                offset.floatValue = 10f; changes.Add(wave.name + "/" + offset.propertyPath);
            }
            so.ApplyModifiedProperties(); EditorUtility.SetDirty(behaviour);
        }
        if (changes.Count != 1) throw new InvalidOperationException("Expected one authored Laser profile, got " + changes.Count);
        PrefabUtility.SaveAsPrefabAsset(stage.prefabContentsRoot, Path, out bool saved);
        if (!saved) throw new InvalidOperationException("Prefab save failed");
        AssetDatabase.SaveAssets();
        return new { saved, changes, lengthOffset = 10 };
    }
    public static object Report()
    {
        var stage = PrefabStageUtility.GetCurrentPrefabStage();
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(Path);
        var rows = new List<object>();
        foreach (var wave in asset.GetComponentsInChildren<DirectedEnemySubWave>(true))
        {
            var b = wave.AttackPatternBehaviour;
            for (int slot = 0; b != null && slot < wave.GetConfiguredEnemySlotCount(); slot++)
            {
                var resolved = new DirectedWaveAttackSettings();
                if (!b.TryCopyResolvedAttackSettingsTo(slot, false, resolved)) continue;
                var s = resolved.WaveBurstSettings.Shooting;
                rows.Add(new { wave = wave.name, slot, type = s.ProjectileType.ToString(),
                    mode = s.Laser.Geometry.LengthMode.ToString(), offset = s.Laser.Geometry.LengthOffset,
                    length = s.Laser.Geometry.LaserLength, bounce = s.ScreenBoundary.BounceOnScreenBounds,
                    edges = s.ScreenBoundary.BounceEdges.ToString(), limited = s.ScreenBoundary.OverrideBaseBounceTime,
                    count = s.ScreenBoundary.MaximumBounces, telegraphOverride = !ReferenceEquals(s.Laser.Geometry, s.Laser.TelegraphGeometry) });
            }
        }
        return new { asset = Path, stage = stage?.assetPath, stageDirty = stage != null && stage.scene.isDirty, rows };
    }
    public static object Preview()
    {
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(Path);
        var wave = asset.GetComponentInChildren<DirectedEnemySubWave>(true);
        using var session = new WaveShootingPreviewSession(wave, true, false, 0, new Vector3(0, -3));
        var rows = new List<object>(); bool warning = false, active = false;
        for (int frame = 1; frame <= 600 && !active; frame++)
        {
            while (!session.AdvanceTo(frame * WaveShootingPreviewSession.StepInterval, 1000)) { }
            var spawner = Field(session.ProjectileWorld, "spawner"); var runtime = Field(spawner, "lasers");
            if (runtime == null) continue;
            foreach (var beam in (IList)Field(runtime, "beams"))
            {
                var entity = (Entity)Field(beam, "Entity"); var m = session.ProjectileWorld.SimulationWorld.EntityManager;
                if (!m.Exists(entity)) continue;
                var state = m.GetComponentData<EnemyLaserState>(entity);
                if (state.Telegraph == 0 && state.Active == 0 || state.Telegraph != 0 && warning || state.Active != 0 && active) continue;
                warning |= state.Telegraph != 0; active |= state.Active != 0;
                var segments = (List<EnemyLaserState>)Field(beam, "Segments"); var data = new List<object>();
                foreach (var s in segments) data.Add(new { x = s.Start.x, y = s.Start.y, dx = s.Direction.x, dy = s.Direction.y, s.Length });
                rows.Add(new { time = frame * WaveShootingPreviewSession.StepInterval, phase = state.Telegraph != 0 ? "Telegraph" : "Active", segments = data });
            }
        }
        return new { path = Path, warning, active, rows };
    }
    public static object Runtime()
    {
        if (!Application.isPlaying) throw new InvalidOperationException("This check must run in Play Mode");
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(Path);
        var wave = asset.GetComponentInChildren<DirectedEnemySubWave>(true);
        var resolved = new DirectedWaveAttackSettings();
        if (!wave.AttackPatternBehaviour.TryCopyResolvedAttackSettingsTo(0, false, resolved)) throw new Exception("No resolved slot");
        var shooting = resolved.WaveBurstSettings.Shooting;
        var cameraObject = new GameObject("Laser prefab runtime verification") { hideFlags = HideFlags.HideAndDontSave };
        var camera = cameraObject.AddComponent<Camera>(); camera.enabled = false; camera.orthographic = true;
        camera.orthographicSize = 5; camera.aspect = .6f; camera.transform.position = new Vector3(0, 0, -10);
        try
        {
            using var world = new World("3_2_extrime runtime laser check", WorldFlags.Game);
            var manager = world.EntityManager;
            using var runtime = new EnemyLaserRuntime(manager);
            var owner = manager.CreateEntity(); manager.AddComponentObject(owner, new EnemyLaserSimulation { Runtime = runtime });
            var system = world.GetOrCreateSystemManaged<EnemyLaserSystem>();
            var bullet = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefub/Enemy Projectiles/Bullet.prefab").GetComponent<EnemyBullet>();
            var requests = new List<EnemyProjectileSpawnRequest>();
            var start = new Vector3(-2, 4.12f); var heading = new Vector3(.58013463f, -.8145206f);
            EnemyAttackProjectileBuilder.AppendEmitter(requests, bullet, shooting, start, start, heading, 1, 1, 0,
                new EnemyProjectilePrefabSet(bullet), laserOrigin: new EnemyLaserOrigin(null, start, shooting.EmissionOrigin, camera));
            var entity = manager.Instantiate(runtime.Template); runtime.Register(entity, requests[0], 0);
            Tick(world, system, .1f);
            var beam = ((IList)Field(runtime, "beams"))[0];
            var segments = (List<EnemyLaserState>)Field(beam, "Segments");
            if (segments.Count != 2) throw new Exception("Telegraph has no reflected leg");
            string warning = JsonUtility.ToJson(segments[1]);
            Tick(world, system, shooting.Laser.ActiveStart + .01f);
            if (segments.Count != 2 || JsonUtility.ToJson(segments[1]) != warning) throw new Exception("Active and Telegraph paths differ");
            var point = segments[1].Start + segments[1].Direction * .5f;
            var target = new EnemyProjectileCollisionTarget { Kind = EnemyProjectileCollisionTargetKind.PlayerShip,
                TargetId = 123, IsCircle = 1, Center = point, PreviousCenter = point, Radius = .02f };
            if (EnemyLaserGeometryUtility.Hits(segments[0], target)) throw new Exception("Expected reflected-only contact");
            var registry = manager.CreateEntity(typeof(EnemyProjectileCollisionRegistryTag));
            manager.AddBuffer<EnemyProjectileCollisionTarget>(registry).Add(target);
            manager.AddBuffer<EnemyProjectileResolutionEvent>(registry);
            Tick(world, system, shooting.Laser.ActiveStart + .02f);
            int hits = manager.GetBuffer<EnemyProjectileResolutionEvent>(registry).Length;
            if (hits != 1) throw new Exception("Reflected segment damage missing or duplicated: " + hits);
            var visuals = ((List<Entity>)Field(beam, "VisualSegments")).ToArray();
            Tick(world, system, shooting.Laser.TotalDuration + .01f);
            foreach (var visual in visuals) if (manager.Exists(visual)) throw new Exception("System did not clean reflected visual");
            // Exercise creation inside Tick too: silent delay has no legs at registration.
            var delayed = new EnemyShootingSettings(); delayed.CopyFrom(shooting);
            JsonUtility.FromJsonOverwrite("{\"activationDelay\":0.2}", delayed.Laser);
            requests.Clear();
            EnemyAttackProjectileBuilder.AppendEmitter(requests, bullet, delayed, start, start, heading, 1, 1, 0,
                new EnemyProjectilePrefabSet(bullet), laserOrigin: new EnemyLaserOrigin(null, start, delayed.EmissionOrigin, camera));
            var second = manager.Instantiate(runtime.Template); runtime.Register(second, requests[0], 10);
            var next = ((IList)Field(runtime, "beams"))[0];
            if (((List<Entity>)Field(next, "VisualSegments")).Count != 0) throw new Exception("Delay unexpectedly rendered legs");
            Tick(world, system, 10.3f);
            var nextVisuals = ((List<Entity>)Field(next, "VisualSegments")).ToArray();
            if (nextVisuals.Length == 0) throw new Exception("System did not create reflected legs");
            Tick(world, system, 10 + delayed.Laser.TotalDuration + .01f);
            foreach (var visual in nextVisuals) if (manager.Exists(visual)) throw new Exception("System leaked delayed reflected visual");
            return new { playing = Application.isPlaying, prefab = Path, segments = segments.Count,
                telegraphMatchesActive = true, reflectedDamageEvents = hits, ecsSystemCreation = true, ecsSystemCleanup = true };
        }
        finally { UnityEngine.Object.Destroy(cameraObject); }
    }
}
