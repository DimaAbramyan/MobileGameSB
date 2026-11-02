using System;
using System.Collections.Generic;
using System.Reflection;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Isolated, production ECS systems; no gameplay scene, prefab or save is modified.
public static class VerifyProjectileCollisionSettings
{
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    static int checks;
    static readonly Vector3 Target = new(1000, 1000);
    static void Check(bool value, string message) { checks++; if (!value) throw new Exception(message); }
    static void Set(object value, string name, object data)
    {
        for (var type = value.GetType(); type != null; type = type.BaseType)
        {
            var field = type.GetField(name, Flags);
            if (field != null) { field.SetValue(value, data); return; }
            var property = type.GetProperty(name, Flags);
            if (property != null) { property.SetValue(value, data); return; }
        }
        throw new Exception("Missing " + name);
    }
    static EnemyShootingSettings Shooting(EnemyProjectileCollisionSettings collision = default)
    {
        var s = new EnemyShootingSettings(); Set(s, "collisionSettings", collision);
        s.SetScreenBoundary(new EnemyProjectileScreenSettings(false));
        Set(s, "overrideProjectileLifetime", true); Set(s, "projectileLifetime", 30f);
        return s;
    }
    static EnemyProjectileCollisionSettings Conditional(float threshold = .5f)
        => new(true, EnemyProjectileCollisionDisableCondition.ScaleBelowThreshold, threshold);

    sealed class Context : IDisposable
    {
        public readonly EnemyProjectilePreviewWorld Preview = new();
        public EntityManager Manager => Preview.SimulationWorld.EntityManager;
        public readonly Entity Registry;
        public Context()
        {
            using var query = Manager.CreateEntityQuery(typeof(EnemyProjectileCollisionRegistryTag));
            Registry = query.GetSingletonEntity();
            // Ordinary preview intentionally suppresses contacts; exercise actual Runtime contacts too.
            Manager.RemoveComponent<EnemyProjectilePreviewCollisionPolicy>(Registry);
        }
        public Entity Spawn(EnemyShootingSettings s)
        {
            var bullet = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefub/Enemy Projectiles/Bullet.prefab").GetComponent<EnemyBullet>();
            s.SetProjectileBaseSpeedOverride(true, 1f); s.Validate();
            using var query = Manager.CreateEntityQuery(typeof(EnemyProjectileStaticData));
            using var before = query.ToEntityArray(Allocator.Temp);
            var requests = new List<EnemyProjectileSpawnRequest>();
            EnemyAttackProjectileBuilder.AppendEmitter(requests, bullet, s, Vector3.zero, Vector3.zero,
                Vector3.down, 1f, 1f, 0, new EnemyProjectilePrefabSet(bullet));
            Check(requests.Count == 1 && requests[0].CollisionSettings.CollisionEnabled == s.CollisionSettings.CollisionEnabled, "Builder forwards collision settings");
            Preview.Spawn(requests);
            using var after = query.ToEntityArray(Allocator.Temp);
            foreach (var entity in after)
            {
                bool found = false; foreach (var prior in before) if (prior == entity) found = true;
                if (!found && !Manager.HasComponent<EnemyCompositePart>(entity)) return entity;
            }
            throw new Exception("No projectile spawned");
        }
        public void Step(float time, float delta) => Preview.Step(time, delta, Target);
        public bool Disabled(Entity e) => Manager.GetComponentData<EnemyProjectileCollisionState>(e).Disabled != 0;
        public void Scale(Entity e, float value)
        {
            var pose = Manager.GetComponentData<LocalTransform>(e); pose.Scale = value; Manager.SetComponentData(e, pose);
            Preview.SimulationWorld.GetOrCreateSystem<EnemyProjectileCollisionStateSystem>().Update(Preview.SimulationWorld.Unmanaged);
        }
        public void Contact(Entity e, EnemyProjectileCollisionTargetKind kind)
        {
            var position = Manager.GetComponentData<LocalTransform>(e).Position.xy;
            var targets = Manager.GetBuffer<EnemyProjectileCollisionTarget>(Registry); targets.Clear();
            targets.Add(new EnemyProjectileCollisionTarget { Kind = kind, TargetId = 7, IsCircle = 1,
                Center = position, PreviousCenter = position, Radius = .05f, DestroysNormal = 1 });
            var world = Preview.SimulationWorld;
            world.GetOrCreateSystem<ProjectileInterceptorSystem>().Update(world.Unmanaged);
            world.GetOrCreateSystem<ProjectilePlayerCollisionSystem>().Update(world.Unmanaged);
        }
        public EnemyProjectileResolutionKind Resolution(Entity e) => Manager.GetComponentData<EnemyProjectileResolution>(e).Kind;
        public void Dispose() => Preview.Dispose();
    }

    public static object Run()
    {
        checks = 0; int worlds = World.All.Count;
        Check(default(EnemyProjectileCollisionSettings).CollisionEnabled, "Legacy/default collisions enabled");
        Check(default(EnemyProjectileCollisionSettings).ScaleThreshold == .5f, "Default threshold .5");
        Check(JsonUtility.FromJson<EnemyShootingSettings>("{}").CollisionSettings.DisableCondition == EnemyProjectileCollisionDisableCondition.None, "Legacy JSON has no condition");
        using (var c = new Context())
        {
            var e = c.Spawn(Shooting()); c.Scale(e, .25f);
            Check(!c.Disabled(e), "None keeps contact at small scale");
            c.Contact(e, EnemyProjectileCollisionTargetKind.PlayerShip);
            Check(c.Resolution(e) == EnemyProjectileResolutionKind.HitPlayer, "Default player contact unchanged");
            e = c.Spawn(Shooting()); c.Contact(e, EnemyProjectileCollisionTargetKind.Interceptor);
            Check(c.Resolution(e) == EnemyProjectileResolutionKind.Intercepted, "Default interception unchanged");
        }
        using (var c = new Context())
        {
            var e = c.Spawn(Shooting(Conditional()));
            float radius = c.Manager.GetComponentData<EnemyProjectileStaticData>(e).Radius;
            c.Scale(e, .5001f); Check(!c.Disabled(e), "Above threshold collidable");
            c.Scale(e, .5f); Check(c.Disabled(e), "Equality disables immediately");
            c.Scale(e, 2f); Check(c.Disabled(e), "Growing does not restore collisions");
            c.Contact(e, EnemyProjectileCollisionTargetKind.PlayerShip);
            c.Contact(e, EnemyProjectileCollisionTargetKind.Interceptor);
            Check(c.Resolution(e) == EnemyProjectileResolutionKind.None, "Disabled body ignores player and interceptor");
            Check(c.Manager.GetComponentData<EnemyProjectileStaticData>(e).Radius == radius, "Radius remains intact");
            var position = c.Manager.GetComponentData<LocalTransform>(e).Position;
            c.Step(.1f, .1f); Check(c.Manager.Exists(e) && !math.all(position == c.Manager.GetComponentData<LocalTransform>(e).Position), "Visible body continues movement");
        }
        using (var c = new Context())
        {
            var s = Shooting(Conditional()); Set(s, "overrideBaseSize", true); Set(s, "baseSize", 2f);
            Set(s, "projectileSizeBehavior", EnemyProjectileSizeBehavior.Customized); Set(s, "projectileSizeChangeY", 0f);
            Set(s, "projectileSizeChangeSpeed", 1f); Set(s, "projectileFinalSize", 0f);
            Set(s, "projectileSizeChangeCurve", AnimationCurve.Linear(0, 0, 1, 1));
            var e = c.Spawn(s); Check(!c.Disabled(e), "Base override included at spawn");
            c.Step(.01f, .01f); c.Step(.61f, .6f); Check(!c.Disabled(e), "Curve * base .8 above threshold");
            c.Step(.91f, .3f); Check(c.Disabled(e) && c.Manager.Exists(e), "Curve * base .2 disables without removing");
            c.Step(1.11f, .2f); Check(!c.Manager.Exists(e), "Zero-size cleanup still removes disabled projectile");
        }
        using (var c = new Context())
        {
            var s = Shooting(new EnemyProjectileCollisionSettings(false));
            var child = new EnemyChildAttackSettings(); Set(child, "triggerValue", .05f);
            Set(child, "repeatCount", 2); Set(child, "repeatInterval", .1f);
            child.Pattern.Shooting.SetScreenBoundary(new EnemyProjectileScreenSettings(false));
            s.SetChildAttack(child);
            var e = c.Spawn(s); Check(c.Disabled(e), "Disabled from spawn");
            c.Contact(e, EnemyProjectileCollisionTargetKind.PlayerShip);
            c.Contact(e, EnemyProjectileCollisionTargetKind.Interceptor);
            Check(c.Resolution(e) == EnemyProjectileResolutionKind.None, "Disabled at spawn ignores all contacts");
            c.Step(.06f, .06f); int first = c.Preview.Count;
            c.Step(.26f, .2f); Check(first > 1 && c.Preview.Count > first, "Disabled parent continues Child Attack repeats");
            using var query = c.Manager.CreateEntityQuery(typeof(EnemyProjectileCollisionState));
            using var entities = query.ToEntityArray(Allocator.Temp);
            foreach (var entity in entities) if (entity != e) Check(!c.Disabled(entity), "Children use own collision configuration");
        }
        using (var c = new Context())
        {
            var s = Shooting(Conditional()); Set(s, "projectileType", EnemyProjectileType.CompositeCircle);
            Set(s.Composite, "componentCount", 4); Set(s.Composite, "radiusMode", EnemyCompositeRadiusMode.Custom); Set(s.Composite, "radius", 1f);
            var center = c.Spawn(s); Check(c.Disabled(center), "Composite controller has no active hitbox");
            using var query = c.Manager.CreateEntityQuery(typeof(EnemyCompositePart)); using var parts = query.ToEntityArray(Allocator.Temp);
            Check(parts.Length == 4, "Composite topology unchanged");
            c.Scale(parts[0], .5f); Check(c.Disabled(parts[0]) && !c.Disabled(parts[1]), "Each component latches independently");
            c.Step(.1f, .1f); Check(c.Disabled(parts[0]) && !c.Disabled(parts[1]), "Composite pose update does not re-enable collision");
            c.Contact(parts[0], EnemyProjectileCollisionTargetKind.Interceptor);
            Check(c.Resolution(parts[0]) == EnemyProjectileResolutionKind.None && c.Manager.Exists(center), "Disabled component cannot destroy composition");
        }
        var source = Shooting(Conditional(.75f));
        var copy = new EnemyShootingSettings(); copy.CopyFrom(source);
        Check(copy.CollisionSettings.ScaleThreshold == .75f, "Full copy retains collision settings");
        copy.CopyProjectileConfigurationFrom(source);
        Check(copy.CollisionSettings.ScaleThreshold == .75f, "Projectile presets retain collision settings");
        var json = JsonUtility.FromJson<EnemyShootingSettings>(JsonUtility.ToJson(source));
        Check(json.CollisionSettings.ScaleThreshold == .75f && json.CollisionSettings.CollisionEnabled, "JSON round trip");
        Check(World.All.Count == worlds, "All isolated worlds disposed");
        return new { passed = true, checks, productionContacts = true, preview = true, curves = true, composite = true, childAttack = true };
    }

    public static object InspectorProbe()
    {
        checks = 0; var preset = ScriptableObject.CreateInstance<EnemyProjectilePreset>();
        try
        {
            preset.SetSettings(Shooting(Conditional()));
            using var serialized = new SerializedObject(preset);
            var property = serialized.FindProperty("projectile.collisionSettings");
            Check(property != null, "Serialized collision block exists on actual preset");
            var drawer = new EnemyProjectileCollisionSettingsDrawer();
            float conditional = drawer.GetPropertyHeight(property, GUIContent.none);
            property.FindPropertyRelative("disableCondition").intValue = 0;
            float normal = drawer.GetPropertyHeight(property, GUIContent.none);
            property.FindPropertyRelative("disabled").boolValue = true;
            Check(conditional > normal && normal > drawer.GetPropertyHeight(property, GUIContent.none), "Conditional fields and block height match");
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Undo.RecordObject(preset, "Collision test");
            property.FindPropertyRelative("disabled").boolValue = false;
            serialized.ApplyModifiedProperties(); Undo.FlushUndoRecordObjects();
            Undo.PerformUndo(); Check(!preset.Settings.CollisionSettings.CollisionEnabled, "Undo retains settings");
            Undo.PerformRedo(); Check(preset.Settings.CollisionSettings.CollisionEnabled, "Redo retains settings");
            return new { passed = true, checks };
        }
        finally { Undo.ClearUndo(preset); UnityEngine.Object.DestroyImmediate(preset); }
    }

    public static object SeekProbe()
    {
        checks = 0; var scene = EditorSceneManager.NewPreviewScene(); var root = new GameObject("Collision seek check");
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, scene);
        try
        {
            var wave = root.AddComponent<Wave>(); var go = new GameObject("One projectile"); go.transform.SetParent(root.transform, false);
            var subwave = go.AddComponent<DirectedEnemySubWave>();
            Set(subwave, "enemyPrefab", AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefub/Enemies Prefab/Enemy.prefab").GetComponent<Enemy>());
            Set(subwave, "enemyCount", 1); Set(subwave, "spawnInterval", 0f); Set(subwave, "settleDuration", 0f);
            var attack = go.GetComponent<DirectedWaveAttackBehaviour>() ?? go.AddComponent<DirectedWaveAttackBehaviour>();
            var settings = (DirectedWaveAttackSettings)attack.GetType().GetField("attackSettings", Flags).GetValue(attack);
            Set(attack, "allowAutonomousAttackDuringEntrance", false);
            Set(settings, "fireMode", DirectedWaveAttackFireMode.Stationary); settings.StationaryFireSettings.ConfigureAngle(180);
            Set(settings, "burstSettingsSource", DirectedWaveBurstSettingsSource.WaveOverride);
            Set(settings, "waitForPreviousAttack", true); Set(settings, "delayAfterAttack", 20f);
            settings.WaveBurstSettings.ConfigureLegacySingleAttack(1, 1f, 20f);
            var shooting = settings.WaveBurstSettings.Shooting;
            Set(shooting, "collisionSettings", Conditional()); shooting.SetScreenBoundary(new EnemyProjectileScreenSettings(false));
            Set(shooting, "overrideProjectileLifetime", true); Set(shooting, "projectileLifetime", 20f);
            shooting.SetProjectileBaseSpeedOverride(true, .1f);
            Set(shooting, "projectileSizeBehavior", EnemyProjectileSizeBehavior.Customized);
            Set(shooting, "projectileSizeChangeY", 1000f); Set(shooting, "projectileFinalSize", .25f);
            Set(shooting, "projectileSizeChangeSpeed", .1f);
            // Shrinks below threshold, then grows above it before the curve ends.
            Set(shooting, "projectileSizeChangeCurve", new AnimationCurve(new Keyframe(0, 0), new Keyframe(.3f, 1), new Keyframe(.5f, 0), new Keyframe(1, 0)));
            settings.Validate(); Set(wave, "scheduledSubWaves", new List<WaveSubWaveCue> { new(go, 0) });
            using var player = new WavePreviewPlayer(wave, 8, Target); player.SnapshotInterval = .25f;
            void Seek(float time) { player.Seek(time); for (int i = 0; player.IsSeeking; i++) { if (i > 500) throw new Exception("Seek stalled"); player.Update(0, 100); } }
            string Capture(bool expectDisabled, bool requireLive = true)
            {
                var m = player.Simulation.ProjectileWorld.SimulationWorld.EntityManager;
                using var query = m.CreateEntityQuery(typeof(EnemyProjectileCollisionState), typeof(LocalTransform));
                using var entities = query.ToEntityArray(Allocator.Temp); var rows = new List<string>();
                foreach (var e in entities)
                {
                    Check((m.GetComponentData<EnemyProjectileCollisionState>(e).Disabled != 0) == expectDisabled, "Correct collision latch at seek time");
                    if (expectDisabled) Check(m.GetComponentData<LocalTransform>(e).Scale > .5f, "Real Size Curve has grown above threshold without restoring collision");
                    rows.Add(JsonUtility.ToJson(m.GetComponentData<LocalTransform>(e)) + JsonUtility.ToJson(m.GetComponentData<EnemyProjectileCollisionState>(e)));
                }
                if (requireLive) Check(rows.Count > 0, "Seek has live projectile"); rows.Sort(StringComparer.Ordinal); return string.Join("\n", rows);
            }
            Seek(.2f); Capture(false, false);
            Seek(9f); string expected = Capture(true);
            Seek(.2f); Capture(false, false); Seek(9f); Check(Capture(true) == expected, "Fast Seek restores exact transform and irreversible latch");
            Check(player.RestoredSnapshots > 0 && !player.IsPlaying, "Snapshots actually used; player remains paused");
            return new { passed = true, checks, restoredSnapshots = player.RestoredSnapshots };
        }
        finally { UnityEngine.Object.DestroyImmediate(root); EditorSceneManager.ClosePreviewScene(scene); }
    }

    public static object RunPlayProbe()
    {
        if (!Application.isPlaying) throw new Exception("Play Mode required");
        return Run();
    }
}
