using System;
using System.Collections.Generic;
using System.Reflection;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class VerifyProjectileBaseSize
{
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    static int checks;
    static readonly Vector3 Target = new(1000, 1000);
    static void Check(bool value, string message) { checks++; if (!value) throw new Exception(message); }
    static void Set(object value, string name, object data)
    {
        for (var type = value.GetType(); type != null; type = type.BaseType)
        {
            var f = type.GetField(name, Flags); if (f != null) { f.SetValue(value, data); return; }
            var p = type.GetProperty(name, Flags); if (p != null) { p.SetValue(value, data); return; }
        }
        throw new Exception("Missing " + name);
    }
    sealed class Context : IDisposable
    {
        public readonly EnemyProjectilePreviewWorld Preview = new();
        public EntityManager Manager => Preview.SimulationWorld.EntityManager;
        readonly UnityEngine.SceneManagement.Scene scene = EditorSceneManager.NewPreviewScene();
        public readonly Camera Camera;
        public Context()
        {
            var go = new GameObject("Bounce test camera") { hideFlags = HideFlags.HideAndDontSave };
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, scene);
            Camera = go.AddComponent<Camera>(); Camera.enabled = false; Camera.orthographic = true;
            Camera.orthographicSize = 5; Camera.aspect = 1; Camera.transform.position = new Vector3(0, 0, -10);
        }
        public Entity Spawn(Vector3 position, Vector3 direction, EnemyShootingSettings settings = null, float speed = 2)
        {
            var bullet = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefub/Enemy Projectiles/Bullet.prefab").GetComponent<EnemyBullet>();
            settings ??= Shooting(); settings.SetProjectileBaseSpeedOverride(true, speed); settings.Validate();
            var requests = new List<EnemyProjectileSpawnRequest>();
            EnemyAttackProjectileBuilder.AppendEmitter(requests, bullet, settings, position, position, direction, 1, speed, 0, new EnemyProjectilePrefabSet(bullet));
            Preview.Spawn(requests);
            using var query = Manager.CreateEntityQuery(typeof(EnemyProjectileStaticData)); using var entities = query.ToEntityArray(Allocator.Temp);
            var entity = entities[entities.Length - 1];
            var data = Manager.GetComponentData<EnemyProjectileStaticData>(entity); data.Radius = .2f; Manager.SetComponentData(entity, data);
            return entity;
        }
        public float2 Position(Entity entity) => Manager.GetComponentData<LocalTransform>(entity).Position.xy;
        public float2 Velocity(Entity entity) => Manager.GetComponentData<EnemyProjectileVelocity>(entity).Value;
        public void Step(float time, float delta) => Preview.Step(time, delta, Target, Camera);
        public void Dispose() { Preview.Dispose(); UnityEngine.Object.DestroyImmediate(Camera.gameObject); EditorSceneManager.ClosePreviewScene(scene); }
    }
    static EnemyShootingSettings Shooting(EnemyChildAttackBoundaries edges = EnemyChildAttackBoundaries.Left | EnemyChildAttackBoundaries.Right, bool destroy = true, bool bounce = true)
    {
        var settings = new EnemyShootingSettings(); settings.SetScreenBoundary(new EnemyProjectileScreenSettings(destroy, bounce: bounce, edges: edges));
        Set(settings, "overrideProjectileLifetime", true); Set(settings, "projectileLifetime", 100f); return settings;
    }
    static EnemyChildAttackSettings Child()
    {
        var child = EnemyChildAttackSettings.CreateDefault(); Set(child, "triggerValue", 0f); Set(child, "repeatInterval", .1f);
        child.Pattern.Shooting.SetScreenBoundary(new EnemyProjectileScreenSettings(false));
        child.Pattern.Shooting.SetProjectileBaseSpeedOverride(true, .1f); return child;
    }
    public static object SeekProbe()
    {
        checks = 0;
        var scene = EditorSceneManager.NewPreviewScene(); var root = new GameObject("Bounce snapshot verification");
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, scene);
        try
        {
            var wave = root.AddComponent<Wave>(); var go = new GameObject("One moving projectile"); go.transform.SetParent(root.transform, false);
            var subwave = go.AddComponent<DirectedEnemySubWave>();
            Set(subwave, "enemyPrefab", AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefub/Enemies Prefab/Enemy.prefab").GetComponent<Enemy>());
            Set(subwave, "enemyCount", 1); Set(subwave, "spawnInterval", 0f); Set(subwave, "settleDuration", 0f);
            var attack = go.GetComponent<DirectedWaveAttackBehaviour>() ?? go.AddComponent<DirectedWaveAttackBehaviour>();
            var settings = (DirectedWaveAttackSettings)attack.GetType().GetField("attackSettings", Flags).GetValue(attack);
            Set(attack, "allowAutonomousAttackDuringEntrance", false);
            Set(settings, "fireMode", DirectedWaveAttackFireMode.Stationary); settings.StationaryFireSettings.ConfigureAngle(135);
            Set(settings, "burstSettingsSource", DirectedWaveBurstSettingsSource.WaveOverride);
            Set(settings, "waitForPreviousAttack", true); Set(settings, "delayAfterAttack", 20f);
            settings.WaveBurstSettings.ConfigureLegacySingleAttack(1, 1f, 20f);
            var shooting = settings.WaveBurstSettings.Shooting;
            Set(shooting, "overrideBaseSize", true); Set(shooting, "baseSize", 2f);
            shooting.SetScreenBoundary(new EnemyProjectileScreenSettings(true, bounce: true, edges: EnemyChildAttackBoundaries.All));
            shooting.SetProjectileBaseSpeedOverride(true, 8f);
            Set(shooting, "overrideProjectileLifetime", true); Set(shooting, "projectileLifetime", 20f);
            Set(shooting, "projectileSizeBehavior", EnemyProjectileSizeBehavior.Customized);
            Set(shooting, "projectileSizeChangeY", 0f); Set(shooting, "projectileFinalSize", 0f); Set(shooting, "projectileSizeChangeSpeed", .3f);
            settings.Validate(); Set(wave, "scheduledSubWaves", new List<WaveSubWaveCue> { new(go, 0) });
            using var player = new WavePreviewPlayer(wave, 8, Target); player.SnapshotInterval = .25f;
            void Seek(float time) { player.Seek(time); for (int i = 0; player.IsSeeking; i++) { if (i > 500) throw new Exception("Seek stalled"); player.Update(0, 100); } }
            string Capture()
            {
                var m = player.Simulation.ProjectileWorld.SimulationWorld.EntityManager;
                using var query = m.CreateEntityQuery(typeof(EnemyProjectileMovement), typeof(LocalTransform));
                using var entities = query.ToEntityArray(Allocator.Temp); var rows = new List<string>();
                foreach (var e in entities) rows.Add(JsonUtility.ToJson(m.GetComponentData<LocalTransform>(e))
                    + JsonUtility.ToJson(m.GetComponentData<EnemyProjectileVelocity>(e))
                    + JsonUtility.ToJson(m.GetComponentData<EnemyProjectileMovement>(e))
                    + JsonUtility.ToJson(m.GetComponentData<EnemyProjectileScreenState>(e)));
                rows.Sort(StringComparer.Ordinal); return string.Join("\n", rows);
            }
            Seek(3f); string expected = Capture();
            Check(expected.Length > 0, "Live bouncing/shrinking projectile captured");
            Seek(1f); Seek(3f); Check(Capture() == expected, "Snapshots restore exact position, direction, scale and boundary state");
            Seek(6f); Check(player.Simulation.ProjectileWorld.Count == 0, "Fast Seek removes zero-size projectile before Lifetime");
            Seek(2f); Seek(6f); Check(player.Simulation.ProjectileWorld.Count == 0, "No zero-size projectile resurrected after rewind");
            Check(player.RestoredSnapshots > 0 && !player.IsPlaying, "Actual snapshot restores preserve pause");
            return new { passed = true, checks, snapshotRestores = player.RestoredSnapshots, deterministicSeek = true };
        }
        finally { UnityEngine.Object.DestroyImmediate(root); EditorSceneManager.ClosePreviewScene(scene); }
    }
    static EnemyShootingSettings Sized(float size, bool enabled = true)
    {
        var s = Shooting(bounce: false, destroy: false);
        Set(s, "overrideBaseSize", enabled); Set(s, "baseSize", size); return s;
    }
    static float Scale(Context c, Entity e) => c.Manager.GetComponentData<LocalTransform>(e).Scale;
    static void Near(float actual, float expected, string message) => Check(Mathf.Abs(actual - expected) < .002f, message + ": " + actual);
    public static object Run()
    {
        checks = 0; int worlds = World.All.Count;
        using (var c = new Context())
        {
            var normal = c.Spawn(Vector3.zero, Vector3.down, Sized(3, false));
            Near(Scale(c, normal), 1, "Override disabled preserves prefab scale");
            var large = c.Spawn(Vector3.zero, Vector3.down, Sized(2));
            Near(Scale(c, large), 2, "Override applies at spawn without curve");
            Check(c.Manager.HasComponent<EnemyProjectileCustomScale>(large), "Collider uses transform scale even without curve");
            Near(c.Manager.GetComponentData<EnemyProjectileStaticData>(large).Radius * Scale(c, large), .4f, "Effective hit radius follows size");
            var visual = c.Manager.GetComponentData<EnemyProjectileVisualStaticData>(large);
            var original = c.Manager.GetComponentData<EnemyProjectileVisualStaticData>(normal);
            Check(math.all(visual.Size == original.Size), "Authored visual dimensions preserved; uniform transform scales both axes");
            var tiny = c.Spawn(Vector3.zero, Vector3.down, Sized(0));
            c.Step(.01f, .01f); Check(!c.Manager.Exists(tiny), "Base zero uses normal zero-size cleanup");
            Near(Scale(c, large), 2, "No-curve size persists on update");
        }
        using (var c = new Context())
        {
            var s = Sized(2);
            Set(s, "projectileSizeBehavior", EnemyProjectileSizeBehavior.Customized);
            Set(s, "projectileSizeChangeY", 0f); Set(s, "projectileSizeChangeSpeed", 1f);
            Set(s, "projectileFinalSize", .5f); Set(s, "projectileSizeChangeCurve", AnimationCurve.Linear(0, 0, 1, 1));
            var e = c.Spawn(Vector3.zero, Vector3.down, s);
            c.Step(.01f, .01f); Near(Scale(c,e), 2, "Curve begins at overridden base");
            c.Step(.51f, .5f); Near(Scale(c,e), 1.5f, "Curve multiplies overridden base");
            c.Step(1.01f, .5f); Near(Scale(c,e), 1, "Curve final ratio applies to overridden base");
        }
        using (var c = new Context())
        {
            var s = Sized(2); var child = new EnemyChildAttackSettings(); Set(child, "triggerValue", 0f);
            child.Pattern.Shooting.SetScreenBoundary(new EnemyProjectileScreenSettings(false));
            Set(child.Pattern.Shooting, "overrideBaseSize", true); Set(child.Pattern.Shooting, "baseSize", 3f);
            s.SetChildAttack(child); var parent = c.Spawn(Vector3.zero, Vector3.down, s);
            c.Step(.01f,.01f);
            using var query = c.Manager.CreateEntityQuery(typeof(EnemyProjectileMovement), typeof(LocalTransform));
            using var entities = query.ToEntityArray(Allocator.Temp); bool found = false;
            foreach (var e in entities) if (e != parent) { Near(Scale(c,e), 3, "Child uses own base, without inheriting parent multiplier"); found=true; }
            Check(found,"Child emitted");
        }
        using (var c = new Context())
        {
            var s = Sized(2); Set(s, "projectileType", EnemyProjectileType.CompositeCircle);
            Set(s.Composite, "componentCount", 4); Set(s.Composite, "radiusMode", EnemyCompositeRadiusMode.Custom); Set(s.Composite, "radius", 1f);
            c.Spawn(Vector3.zero, Vector3.down, s);
            using var centers = c.Manager.CreateEntityQuery(typeof(EnemyCompositeCenter), typeof(LocalTransform));
            using var centerEntities = centers.ToEntityArray(Allocator.Temp); Check(centerEntities.Length==1,"One composite controller");
            Near(Scale(c,centerEntities[0]),2,"Composite center scales figure");
            var center = c.Manager.GetComponentData<LocalTransform>(centerEntities[0]).Position;
            using var members = c.Manager.CreateEntityQuery(typeof(EnemyCompositePart),typeof(LocalTransform));
            using var parts = members.ToEntityArray(Allocator.Temp); Check(parts.Length==4,"Composite topology unchanged: " + parts.Length);
            foreach(var e in parts) { Near(Scale(c,e),2,"Composite visual and collision scale"); Near(math.distance(c.Manager.GetComponentData<LocalTransform>(e).Position,center),2,"Composite spacing scales uniformly"); }
        }
        using (var c = new Context())
        {
            var s = Sized(2); Set(s,"projectileType",EnemyProjectileType.Laser);
            Set(s.Laser,"enableTelegraph",false); Set(s.Laser.Geometry,"lengthMode",EnemyLaserLengthMode.Custom);
            Set(s.Laser.Geometry,"laserLength",3f); Set(s.Laser.Geometry,"laserWidth",.25f);
            var e = c.Spawn(Vector3.zero,Vector3.down,s);
            var beam = c.Manager.GetComponentData<EnemyLaserState>(e);
            Near(beam.Length,6f,"Laser length scales"); Near(beam.Width,.5f,"Laser collision width scales");
            c.Step(.1f,.1f); Near(c.Manager.GetComponentData<EnemyLaserState>(e).Width,.5f,"Laser scale persists on update");
        }
        var source = Sized(2.5f); var full = new EnemyShootingSettings(); full.CopyFrom(source);
        Check(full.OverrideBaseSize && full.BaseSize == 2.5f,"Full settings copy");
        var preset = new EnemyShootingSettings(); preset.CopyProjectileConfigurationFrom(source);
        Check(preset.OverrideBaseSize && preset.BaseSize == 2.5f,"Projectile preset copy");
        var roundtrip = JsonUtility.FromJson<EnemyShootingSettings>(JsonUtility.ToJson(source));
        Check(roundtrip.OverrideBaseSize && roundtrip.BaseSize==2.5f,"JSON retains new fields");
        Set(roundtrip,"overrideBaseSize",false); Check(roundtrip.BaseSize==2.5f,"Toggle retains stored size");
        Check(!JsonUtility.FromJson<EnemyShootingSettings>("{}").OverrideBaseSize,"Old settings keep override disabled");
        Check(World.All.Count==worlds,"Isolated ECS worlds disposed");
        return new { passed=true, checks, baseSize=true, curves=true, composite=true, children=true, presets=true };
    }
    public static object InspectorProbe()
    {
        checks=0; var preset=ScriptableObject.CreateInstance<EnemyProjectilePreset>();
        try
        {
            preset.SetSettings(Sized(2.5f));
            Check(preset.Settings.OverrideBaseSize && preset.Settings.BaseSize==2.5f,"Actual preset stores override");
            using var serialized = new SerializedObject(preset);
            var property=serialized.FindProperty("projectile");
            var method=typeof(EnemyShootingSettingsDrawer).GetMethod("ProjectileFields",BindingFlags.Static|BindingFlags.NonPublic);
            bool Visible() { foreach(var f in (IEnumerable<string>)method.Invoke(null,new object[]{property})) if(f=="baseSize")return true; return false; }
            Check(Visible(),"Base Size visible when enabled");
            property.FindPropertyRelative("overrideBaseSize").boolValue=false; serialized.ApplyModifiedPropertiesWithoutUndo(); serialized.Update();
            Check(!Visible(),"Base Size hidden when disabled");
            Near(property.FindPropertyRelative("baseSize").floatValue,2.5f,"Inspector toggle preserves stored value");
            return new {passed=true, checks};
        }
        finally { UnityEngine.Object.DestroyImmediate(preset); }
    }
    public static object RunPlayProbe() { Check(Application.isPlaying,"Play Mode required"); return Run(); }
}


