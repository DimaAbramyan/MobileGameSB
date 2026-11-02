using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// Focused functional checks; run via Pipeline run_script, without changing authored assets.
public static class VerifyEnemyLaserBounce
{
    private const BindingFlags F = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private static int checks;
    private static EnemyBullet bullet;
    private static readonly Vector3 Far = new(100, 100);
    private static object Get(object o, string n) => o.GetType().GetField(n, F).GetValue(o);
    private static void Set(object o, string n, object v)
    {
        var field = o.GetType().GetField(n, F);
        if (field != null) field.SetValue(o, v);
        else o.GetType().GetProperty(n, F).SetValue(o, v);
    }
    private static object Call(object o, string n, params object[] args) => o.GetType().GetMethod(n, F).Invoke(o, args);
    private static void Check(bool ok, string message) { checks++; if (!ok) throw new Exception(message); }
    private static void Near(float a, float b, string m) => Check(Mathf.Abs(a - b) < .003f, m + ": " + a + " / " + b);
    private static void Near(float2 a, float2 b, string m) => Check(math.distance(a, b) < .003f, m + ": " + a + " / " + b);
    private static EnemyProjectileScreenSettings Screen(int count = 2, bool limited = true,
        EnemyChildAttackBoundaries edges = EnemyChildAttackBoundaries.All)
        => new(false, bounce: true, edges: edges, overrideBounceTime: limited, maximumBounces: count);
    private static float2 End(EnemyLaserState s) => s.Start + s.Direction * s.Length;
    private static List<EnemyLaserState> Path(Vector2 start, Vector2 direction, float length, EnemyProjectileScreenSettings screen)
    {
        var path = new List<EnemyLaserState>();
        EnemyLaserGeometryUtility.BuildPath(start, direction, length, .2f, Rect.MinMaxRect(-3, -5, 3, 5), screen, path);
        return path;
    }
    private static void Geometry()
    {
        var plain = Path(Vector2.zero, Vector2.right, 20, default);
        Check(plain.Count == 1, "Bounce off is one original segment"); Near(End(plain[0]), new float2(20, 0), "Plain endpoint");
        var one = Path(Vector2.zero, Vector2.right, 20, Screen(1));
        Check(one.Count == 2, "One bounce"); Near(one[0].Length, 3, "First wall distance");
        Near(End(one[1]), new float2(-14, 0), "After bounce limit the remainder continues straight");
        var two = Path(Vector2.zero, Vector2.right, 20, Screen(2));
        Check(two.Count == 3, "Two bounces"); Near(End(two[2]), new float2(8, 0), "Second bounce and remaining distance");
        var unlimited = Path(Vector2.zero, Vector2.right, 20, Screen(limited: false));
        Check(unlimited.Count == 4, "Unlimited reflects within finite total length"); Near(End(unlimited[3]), new float2(-2, 0), "Unlimited endpoint");
        var rightOnly = Path(Vector2.zero, Vector2.right, 20, Screen(limited: false, edges: EnemyChildAttackBoundaries.Right));
        Check(rightOnly.Count == 2, "Only selected right edge reflects");
        var corner = Path(Vector2.zero, new Vector2(3, 5).normalized, 30, Screen(1));
        Check(corner.Count == 2 && corner[1].Direction.x < 0 && corner[1].Direction.y < 0, "Corner reflects both axes using one bounce");
        var outside = Path(new Vector2(-10, 0), Vector2.right, 20, Screen(limited: false));
        Near(End(outside[outside.Count - 1]), new float2(-2, 0), "Outside emitter enters before reflecting");
        var away = Path(new Vector2(-10, 0), Vector2.left, 20, Screen());
        Check(away.Count == 1, "Outside emitter facing away never bounces");
        var zero = Path(Vector2.zero, Vector2.right, 0, Screen()); Check(zero.Count == 1 && zero[0].Length == 0, "Zero length safe");
        var exact = Path(Vector2.zero, Vector2.right, 3, Screen()); Check(exact.Count == 1, "Exact endpoint has no phantom segment");
        // Independent reference: existing moving-projectile sweep, all masks and varied directions.
        for (int mask = 0; mask < 16; mask++) for (int i = 0; i < 12; i++)
        {
            var d = new float2(math.cos(i * .47f), math.sin(i * .47f));
            var path = Path(Vector2.zero, new Vector2(d.x, d.y), 40, Screen(3, edges: (EnemyChildAttackBoundaries)mask));
            float2 expected = EnemyProjectileScreenBounce.ResolveLimited(float2.zero, d * 40, new float2(-3, -5), new float2(3, 5),
                (EnemyChildAttackBoundaries)mask, 3, out _, out _, out _);
            Near(End(path[path.Count - 1]), expected, "Same reflection rules as ordinary projectile");
            float total = 0;
            for (int n = 0; n < path.Count; n++)
            {
                total += path[n].Length;
                if (n > 0) Near(path[n].Start, End(path[n - 1]), "Connected segments");
            }
            Near(total, 40, "Total length preserved");
        }
    }
    private static EnemyShootingSettings Shooting(bool warning = true)
    {
        var s = new EnemyShootingSettings(); s.SetProjectileType(EnemyProjectileType.Laser); s.SetScreenBoundary(Screen());
        Set(s.Laser.Geometry, "lengthMode", EnemyLaserLengthMode.Custom); Set(s.Laser.Geometry, "laserLength", 20f);
        Set(s.Laser.Geometry, "laserWidth", .2f); Set(s.Laser, "enableTelegraph", warning);
        Set(s.Laser, "telegraphDuration", .3f); Set(s.Laser, "laserDuration", 1f); Set(s.Laser, "fadeOutDuration", .1f);
        Set(s.Laser, "damage", 3f); return s;
    }
    private static Entity Spawn(EnemyProjectilePreviewWorld w, EnemyShootingSettings s, EnemyLaserOrigin origin = default)
    {
        var requests = new List<EnemyProjectileSpawnRequest>();
        EnemyAttackProjectileBuilder.AppendEmitter(requests, bullet, s, Vector3.zero, Vector3.zero, new Vector3(1, .3f).normalized,
            1, 1, 0, new EnemyProjectilePrefabSet(bullet), laserOrigin: origin);
        w.Spawn(requests);
        using var q = w.SimulationWorld.EntityManager.CreateEntityQuery(typeof(EnemyLaserState)); return q.GetSingletonEntity();
    }
    private static object Runtime(EnemyProjectilePreviewWorld w) => Get(Get(w, "spawner"), "lasers");
    private static object Beam(EnemyProjectilePreviewWorld w) => ((IList)Get(Runtime(w), "beams"))[0];
    private static List<EnemyLaserState> Segments(object beam) => (List<EnemyLaserState>)Get(beam, "Segments");
    private static int Events(EnemyProjectilePreviewWorld w, out float damage)
    {
        var m = w.SimulationWorld.EntityManager; using var q = m.CreateEntityQuery(typeof(EnemyProjectileCollisionRegistryTag));
        var events = m.GetBuffer<EnemyProjectileResolutionEvent>(q.GetSingletonEntity());
        damage = events.Length == 0 ? 0 : events[0].Damage; return events.Length;
    }
    private static string Fingerprint(EnemyProjectilePreviewWorld w)
    {
        var rows = new List<string>(); var m = w.SimulationWorld.EntityManager;
        foreach (var beam in (IList)Get(Runtime(w), "beams"))
        {
            string row = "";
            foreach (var s in Segments(beam)) row += JsonUtility.ToJson(s);
            foreach (var entity in (List<Entity>)Get(beam, "VisualSegments"))
                row += JsonUtility.ToJson(m.GetComponentData<LocalTransform>(entity)) + JsonUtility.ToJson(m.GetComponentData<PostTransformMatrix>(entity));
            rows.Add(row);
        }
        rows.Sort(StringComparer.Ordinal); return string.Join("\n", rows);
    }
    private static void Visuals(EnemyProjectilePreviewWorld w)
    {
        var m = w.SimulationWorld.EntityManager;
        foreach (var beam in (IList)Get(Runtime(w), "beams"))
        {
            var path = Segments(beam); var extra = (List<Entity>)Get(beam, "VisualSegments");
            Check(extra.Count >= path.Count - 1, "All reflected segments have visuals");
            for (int i = 0; i < path.Count; i++)
            {
                var e = i == 0 ? (Entity)Get(beam, "Entity") : extra[i - 1];
                var pose = m.GetComponentData<LocalTransform>(e); var scale = m.GetComponentData<PostTransformMatrix>(e).Value;
                Near(pose.Position.xy, path[i].Start, "Visual starts at path vertex");
                Near(scale.c1.y, path[i].Length, "Visual length matches collision"); Near(scale.c0.x, path[i].Width, "Visual width matches collision");
                float2 forward = math.mul(pose.Rotation, new float3(0, 1, 0)).xy;
                Near(forward, path[i].Direction, "Visual heading matches collision");
                if (i > 0) Check(!m.HasComponent<EnemyProjectileMovement>(e), "Presentation legs cannot emit children or deal duplicate damage");
            }
        }
    }
    private static void Lifecycle(Camera camera)
    {
        using var w = new EnemyProjectilePreviewWorld(); var s = Shooting();
        var e = Spawn(w, s, new EnemyLaserOrigin(null, Vector3.zero, s.EmissionOrigin, camera));
        w.Step(.1f, .1f, Far, camera); var beam = Beam(w);
        Check(Segments(beam).Count == 3, "Telegraph includes two reflected legs");
        string warning = JsonUtility.ToJson(Segments(beam)[1]); Visuals(w);
        var targetPoint = Segments(beam)[1].Start + Segments(beam)[1].Direction * Segments(beam)[1].Length * .5f;
        var target = new EnemyProjectileCollisionTarget { IsCircle = 1, Radius = .01f, Center = targetPoint, PreviousCenter = targetPoint };
        Check(!EnemyLaserGeometryUtility.Hits(Segments(beam)[0], target) && EnemyLaserGeometryUtility.Hits(Segments(beam), target), "Target only touches reflected leg");
        w.Step(.2f, .1f, new Vector3(targetPoint.x, targetPoint.y), camera); Check(Events(w, out _) == 0, "Reflected warning cannot damage");
        w.Step(.301f, .101f, new Vector3(targetPoint.x, targetPoint.y), camera);
        Check(JsonUtility.ToJson(Segments(beam)[1]) == warning, "Telegraph and activated path identical");
        Check(Events(w, out float damage) == 1, "Reflected leg damages once"); Near(damage, 3, "Configured damage retained");
        w.Step(.4f, .099f, new Vector3(targetPoint.x, targetPoint.y), camera); Check(Events(w, out _) == 0, "Shared contact latch prevents duplicate damage");
        Visuals(w);
        // Move a target onto a joint where two segments overlap; still one event per beam.
        w.Step(.5f, .1f, Far, camera);
        var joint = End(Segments(beam)[0]); w.Step(.6f, .1f, new Vector3(joint.x, joint.y), camera);
        Check(Events(w, out _) == 1, "Joint contact never doubles damage");
        var visualEntities = ((List<Entity>)Get(beam, "VisualSegments")).ToArray();
        w.Step(1.31f, .71f, new Vector3(targetPoint.x, targetPoint.y), camera); Check(Events(w, out _) == 0, "Reflected fading tail harmless");
        w.Step(1.41f, .1f, Far, camera); w.Step(1.42f, .01f, Far, camera);
        Check(!w.SimulationWorld.EntityManager.Exists(e), "Laser cleaned up");
        foreach (var v in visualEntities) Check(!w.SimulationWorld.EntityManager.Exists(v), "Reflected visual cleaned up");
    }
    private static void FollowAndSnapshot(Camera camera)
    {
        var source = new GameObject("Bounce follow verification") { hideFlags = HideFlags.HideAndDontSave };
        try
        {
            using var w = new EnemyProjectilePreviewWorld(); var s = Shooting(); Set(s.Laser, "followOrigin", true);
            Set(s.Laser.Geometry, "constantLength", false); Set(s.Laser.Geometry, "lengthCurve", AnimationCurve.Linear(0, .5f, 1, 1.5f));
            Spawn(w, s, new EnemyLaserOrigin(source.transform, Vector3.zero, s.EmissionOrigin, camera));
            w.Step(.1f, .1f, Far, camera); source.transform.position = new Vector3(1, -.5f); source.transform.rotation = Quaternion.Euler(0, 0, 90);
            w.Step(.2f, .1f, Far, camera); var path = Segments(Beam(w));
            Near(path[0].Start, new float2(1, -.5f), "Warning follows source translation");
            Near(path[0].Direction, math.normalize(new float2(1, .3f)), "Released direction remains locked despite source rotation");
            Visuals(w);
            var graphType = typeof(WavePreviewPlayer).Assembly.GetType("WavePreviewStateGraph");
            var root = typeof(EnemyProjectilePreviewWorld).GetProperty("SnapshotManagedRoot", F).GetValue(w);
            var graph = graphType.GetMethod("Capture", BindingFlags.Public | BindingFlags.Static).Invoke(null, new object[] { new[] { root } });
            var nativeType = typeof(EnemyProjectilePreviewWorld).GetNestedType("Snapshot", BindingFlags.NonPublic);
            using var snapshot = (IDisposable)Activator.CreateInstance(nativeType, F, null, new object[] { w }, null);
            string expected = Fingerprint(w);
            w.Step(.7f, .5f, Far, camera); Visuals(w); string future = Fingerprint(w);
            var map = Call(snapshot, "Restore", w); Call(graph, "Restore", map); Call(w, "RestoreClock", .2f);
            Check(Fingerprint(w) == expected, "Snapshot restores vertices, visuals and remapped entity owners");
            w.Step(.7f, .5f, Far, camera); Check(Fingerprint(w) == future, "Continuation after snapshot is deterministic"); Visuals(w);
        }
        finally { Object.DestroyImmediate(source); }
    }
    private static DirectedWaveAttackSettings Attack()
    {
        var s = new DirectedWaveAttackSettings(); Set(s, "fireMode", DirectedWaveAttackFireMode.Stationary);
        s.StationaryFireSettings.ConfigureAngle(-75); Set(s, "burstSettingsSource", DirectedWaveBurstSettingsSource.WaveOverride);
        s.WaveBurstSettings.ConfigureLegacySingleAttack(1, .2f, 10); s.WaveBurstSettings.AuthoredShooting.CopyFrom(Shooting());
        Set(s.WaveBurstSettings.AuthoredShooting.Laser.Geometry, "laserLength", 60f); return s;
    }
    private static DirectedWaveAttackPattern Pattern()
    {
        var p = new DirectedWaveAttackPattern(); p.Configure(Attack(), null, false, 1, false, 1, null, EnemySpeedRandomizeOn.PerProjectile); return p;
    }
    private static void Seek(WavePreviewPlayer player, float t)
    {
        player.Seek(t); int guard = 0;
        while (player.IsSeeking) { player.Update(0, 100); if (++guard > 500) throw new Exception("Seek stalled"); }
    }
    private static void WaveModes()
    {
        var root = PrefabUtility.LoadPrefabContents("Assets/Waves/2/2_3_extrime.prefab");
        try
        {
            var wave = root.transform.Find("Main").GetComponent<DirectedEnemySubWave>(); var b = wave.AttackPatternBehaviour;
            Set(wave.Formation, "customFormationPoints", new[] { Vector3.left, Vector3.zero, Vector3.right });
            foreach (string mode in new[] { "Single", "Multiple", "Sequence", "Groups" })
            {
                Set(b, "executionMode", DirectedWaveAttackExecutionMode.Patterns); Set(b, "attackPatterns", new List<DirectedWaveAttackPattern>());
                Set(b, "attackPreset", null); Set(b, "attackSettings", Attack()); Set(b, "useSelectedEnemySlots", false);
                Set(b, "useAttackGroups", false); Set(b, "attackGroups", new List<DirectedWaveAttackGroup>()); Set(b, "attackGroupSlots", new List<DirectedWaveAttackGroupSlot>());
                if (mode == "Multiple") { var p = Pattern(); Set(p, "selectedEnemySlots", new List<int> { 0, 1, 2 }); Set(b, "attackPatterns", new List<DirectedWaveAttackPattern> { p }); }
                if (mode == "Sequence") { Set(b, "executionMode", DirectedWaveAttackExecutionMode.Sequence); b.AttackSequence.steps = new() { new() { pattern = Pattern() } }; }
                if (mode == "Groups")
                {
                    b.SetUseAttackGroups(true); var a = b.AttackGroups[0]; var multi = b.CreateAttackGroup(a.Id); var sequence = b.CreateAttackGroup(a.Id);
                    var p = Pattern(); Set(p, "selectedEnemySlots", new List<int> { 1 }); Set(multi, "patterns", new List<DirectedWaveAttackPattern> { p });
                    Set(sequence, "executionMode", DirectedWaveAttackExecutionMode.Sequence); sequence.Sequence.steps = new() { new() { pattern = Pattern() } };
                    b.AssignAttackGroup(1, multi.Id); b.AssignAttackGroup(2, sequence.Id);
                }
                b.InvalidateSequenceGuides(); EditorUtility.SetDirty(b);
                using (var session = new WaveShootingPreviewSession(wave, true, true, 0, Far))
                {
                    bool warning = false, active = false, bounced = false;
                    for (int frame = 1; frame <= 100; frame++)
                    {
                        while (!session.AdvanceTo(frame * WaveShootingPreviewSession.StepInterval, 1000)) { }
                        var runtime = Runtime(session.ProjectileWorld); if (runtime == null) continue;
                        foreach (var beam in (IList)Get(runtime, "beams"))
                        {
                            var e = (Entity)Get(beam, "Entity"); var m = session.ProjectileWorld.SimulationWorld.EntityManager;
                            if (!m.Exists(e)) continue; var state = m.GetComponentData<EnemyLaserState>(e);
                            warning |= state.Telegraph != 0; active |= state.Active != 0; bounced |= Segments(beam).Count > 1;
                        }
                    }
                    Check(warning && active && bounced, mode + " preview uses reflected Telegraph and active Laser"); Visuals(session.ProjectileWorld);
                }
            }
            var conductor = root.GetComponent<Wave>(); Set(conductor, "scheduledSubWaves", new List<WaveSubWaveCue> { new(wave.gameObject, 0) });
            Set(wave, "spawnInterval", 0f); Set(wave, "settleDuration", 0f);
            using var player = new WavePreviewPlayer(conductor, 123, Far); player.SnapshotInterval = .1f;
            Seek(player, .8f); string expected = Fingerprint(player.Simulation.ProjectileWorld);
            Check(expected.Length > 0, "Fast Seek contains reflected group lasers");
            Seek(player, .2f); Seek(player, .8f);
            Check(Fingerprint(player.Simulation.ProjectileWorld) == expected, "Fast Seek restores all reflected group paths");
            Check(player.RestoredSnapshots > 0, "Fast Seek actually restored snapshots"); Visuals(player.Simulation.ProjectileWorld);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
    private static void Child(Camera camera)
    {
        using var w = new EnemyProjectilePreviewWorld(); var parent = new EnemyShootingSettings();
        var child = new EnemyChildAttackSettings(); Set(child, "triggerValue", 0f); child.Pattern.AuthoredShooting.CopyFrom(Shooting(false));
        parent.SetChildAttack(child); var requests = new List<EnemyProjectileSpawnRequest>();
        EnemyAttackProjectileBuilder.AppendEmitter(requests, bullet, parent, Vector3.zero, Vector3.zero, Vector3.right, 1, 1, 0, new EnemyProjectilePrefabSet(bullet));
        w.Spawn(requests); w.Step(.1f, .1f, Far, camera); w.Step(.2f, .1f, Far, camera);
        using var q = w.SimulationWorld.EntityManager.CreateEntityQuery(typeof(EnemyLaserState));
        Check(q.CalculateEntityCount() == 1, "Child Attack spawns Laser through shared path");
        Check(Segments(Beam(w)).Count > 1, "Child Laser inherits Bounce"); Visuals(w);
    }
    private static void PeriodicAndProtection(Camera camera)
    {
        using var w = new EnemyProjectilePreviewWorld(); var s = Shooting(false);
        Set(s.Laser, "damageMode", EnemyLaserDamageMode.Periodic); Set(s.Laser, "damageInterval", .25f);
        var e = Spawn(w, s, new EnemyLaserOrigin(null, Vector3.zero, s.EmissionOrigin, camera));
        w.Step(.01f, .01f, Far, camera); var path = Segments(Beam(w));
        var p = path[1].Start + path[1].Direction * path[1].Length * .5f;
        var position = new Vector3(p.x, p.y);
        w.Step(.02f, .01f, position, camera); Check(Events(w, out _) == 1, "Periodic reflected contact");
        w.Step(.1f, .08f, position, camera); Check(Events(w, out _) == 0, "Periodic reflected interval respected");
        w.Step(.28f, .18f, position, camera); Check(Events(w, out _) == 1, "Periodic reflected next interval");
        var m = w.SimulationWorld.EntityManager;
        using var q = m.CreateEntityQuery(typeof(EnemyProjectileCollisionRegistryTag));
        var registry = q.GetSingletonEntity();
        var targets = m.GetBuffer<EnemyProjectileCollisionTarget>(registry); targets.Clear();
        targets.Add(new EnemyProjectileCollisionTarget { Kind = EnemyProjectileCollisionTargetKind.Interceptor, DestroysNormal = 1,
            IsCircle = 1, Center = p, PreviousCenter = p, Radius = .03f, TargetId = 900 });
        ((EnemyLaserRuntime)Runtime(w)).Tick(.3f, camera);
        Check(m.GetComponentData<EnemyProjectileResolution>(e).Kind == EnemyProjectileResolutionKind.Intercepted,
            "Interceptor on reflected segment resolves whole laser");
    }

    public static object Capture()
    {
        bullet = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefub/Enemy Projectiles/Bullet.prefab").GetComponent<EnemyBullet>();
        var scene = EditorSceneManager.NewPreviewScene(); var go = new GameObject("Laser Bounce render verification");
        SceneManager.MoveGameObjectToScene(go, scene);
        var camera = go.AddComponent<Camera>(); camera.scene = scene; camera.orthographic = true; camera.orthographicSize = 5;
        camera.transform.position = new Vector3(0, 0, -10); camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
        var texture = new RenderTexture(600, 1000, 24); camera.targetTexture = texture;
        var pixels = new Texture2D(600, 1000, TextureFormat.RGB24, false); var previous = RenderTexture.active;
        var paths = new[] { "AgentArtifacts/laser-bounce-telegraph.png", "AgentArtifacts/laser-bounce-active.png" };
        try
        {
            using var w = new EnemyProjectilePreviewWorld(); var s = Shooting();
            Set(s, "overwriteBaseColour", true); Set(s, "projectileColour", new Color(.1f, .9f, 1));
            Set(s.Laser, "telegraphOpacity", .6f);
            Spawn(w, s, new EnemyLaserOrigin(null, Vector3.zero, s.EmissionOrigin, camera));
            for (int stage = 0; stage < 2; stage++)
            {
                w.Step(stage == 0 ? .15f : .5f, stage == 0 ? .15f : .35f, Far, camera);
                Action<ScriptableRenderContext, Camera> draw = (_, c) => { if (c == camera) w.Draw(c); };
                RenderPipelineManager.beginCameraRendering += draw;
                try { camera.Render(); } finally { RenderPipelineManager.beginCameraRendering -= draw; }
                RenderTexture.active = texture; pixels.ReadPixels(new Rect(0, 0, 600, 1000), 0, 0); pixels.Apply();
                var path = Segments(Beam(w));
                for (int i = 0; i < path.Count; i++)
                {
                    var midpoint = path[i].Start + path[i].Direction * path[i].Length * .5f;
                    var point = camera.WorldToViewportPoint(new Vector3(midpoint.x, midpoint.y));
                    if (point.x <= 0 || point.x >= 1 || point.y <= 0 || point.y >= 1) continue;
                    var colour = pixels.GetPixel((int)(point.x * 600), (int)(point.y * 1000));
                    Check(stage == 0 ? colour.r > .15f : colour.g > .15f, "Rendered segment midpoint matches path: " + stage + "/" + i);
                }
                File.WriteAllBytes(paths[stage], pixels.EncodeToPNG());
            }
            return new { passed = true, paths, segments = Segments(Beam(w)).Count };
        }
        finally
        {
            RenderTexture.active = previous; camera.targetTexture = null; texture.Release();
            Object.DestroyImmediate(texture); Object.DestroyImmediate(pixels); Object.DestroyImmediate(go); EditorSceneManager.ClosePreviewScene(scene);
        }
    }
    public static object Run()
    {
        checks = 0; int worlds = World.All.Count;
        bullet = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefub/Enemy Projectiles/Bullet.prefab").GetComponent<EnemyBullet>();
        var go = new GameObject("Laser bounce verification camera") { hideFlags = HideFlags.HideAndDontSave };
        var camera = go.AddComponent<Camera>(); camera.enabled = false; camera.orthographic = true; camera.orthographicSize = 5;
        camera.aspect = .6f; camera.transform.position = new Vector3(0, 0, -10);
        try { Geometry(); Lifecycle(camera); FollowAndSnapshot(camera); Child(camera); PeriodicAndProtection(camera); WaveModes(); }
        finally { Object.DestroyImmediate(go); }
        Check(World.All.Count == worlds, "No leaked preview/snapshot worlds");
        return new { passed = true, checks, geometry = true, telegraph = true, reflectedDamage = true, followOrigin = true,
            snapshots = true, fastSeek = true, attackGroups = true, singleMultipleSequence = true, childAttack = true, cleanup = true };
    }
}
