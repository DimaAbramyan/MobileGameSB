using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEditor;
using UnityEngine;

public static class VerifyChildAttackIntegration
{
    private static int checks;
    private static EnemyBullet bullet;
    private static readonly Vector3 Target = new(0f, 10f);
    public static object Run()
    {
        checks = 0;
        bullet = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefub/Enemy Projectiles/Bullet.prefab").GetComponent<EnemyBullet>();
        TestAim(); TestReverseAimedSector(); TestStationaryAndOrigin(); TestMovement(); TestDamageInheritance(); TestRuntimeSystem(); TestBurstLineage(); TestSafety(); TestInspectorAndPresets();
        return new { passed = checks, tested = "Aimed capture modes, stationary world point, forced parent origin, all motion types + DNA, runtime ECS system, cyclic recursion/family/frame caps, Inspector nesting, presets, standalone Scene preview" };
    }
    private static EnemyChildAttackSettings Child()
    {
        var settings = new EnemyChildAttackSettings(); Set(settings, "triggerValue", 0f);
        settings.Pattern.Shooting.SetProjectileBaseSpeedOverride(true, 1f);
        Set(settings.Pattern.Shooting, "overrideProjectileLifetime", true); Set(settings.Pattern.Shooting, "projectileLifetime", 40f);
        settings.Pattern.Validate(); return settings;
    }
    private static Entity Spawn(EnemyProjectilePreviewWorld world, EnemyChildAttackSettings child, float damageMultiplier = 1f)
    {
        var parentSettings = new EnemyShootingSettings(); parentSettings.SetChildAttack(child); Set(parentSettings, "overrideProjectileLifetime", true); Set(parentSettings, "projectileLifetime", 40f);
        var requests = new List<EnemyProjectileSpawnRequest>();
        EnemyAttackProjectileBuilder.AppendEmitter(requests, bullet, parentSettings, Vector3.zero, Vector3.zero, Vector3.right, damageMultiplier, 1f, 0f, new EnemyProjectilePrefabSet(bullet));
        world.Spawn(requests);
        using var query = world.SimulationWorld.EntityManager.CreateEntityQuery(typeof(EnemyProjectileStaticData)); using var entities = query.ToEntityArray(Allocator.Temp);
        return entities[0];
    }
    private static List<Entity> Children(EnemyProjectilePreviewWorld world, Entity parent)
    {
        using var query = world.SimulationWorld.EntityManager.CreateEntityQuery(typeof(EnemyProjectileStaticData)); using var entities = query.ToEntityArray(Allocator.Temp);
        var result = new List<Entity>(); foreach (var entity in entities) if (entity != parent) result.Add(entity); return result;
    }
    private static Vector3 Direction(EnemyProjectilePreviewWorld world, Entity entity)
    {
        var v = world.SimulationWorld.EntityManager.GetComponentData<EnemyProjectileVelocity>(entity).Value; return new Vector3(v.x, v.y).normalized;
    }
    private static void TestAim()
    {
        foreach (var mode in new[] { DirectedWaveAttackAimMode.AimOnStart, DirectedWaveAttackAimMode.AimOnBurst, DirectedWaveAttackAimMode.AimContinuous })
        {
            using var world = new EnemyProjectilePreviewWorld(); var child = Child();
            Set(child, "fireMode", DirectedWaveAttackFireMode.Aimed); Set(child, "aimMode", mode);
            Set(child.Pattern, "burstAttack", EnemyBurstAttackMode.Enabled); Set(child.Pattern, "attackShotCount", 2); Set(child.Pattern, "attackShotInterval", .2f);
            Set(child.Pattern, "burstShotCount", 2); Set(child.Pattern, "burstShotInterval", .1f); child.Pattern.Validate();
            var parent = Spawn(world, child); world.Step(.1f, .1f, Target);
            var first = Children(world, parent)[0]; var firstDirection = Direction(world, first);
            var changed = new Vector3(-10f, 0f); world.Step(.21f, .11f, changed);
            var second = Children(world, parent)[1];
            Check(Vector3.Distance(Direction(world, second), mode == DirectedWaveAttackAimMode.AimContinuous ? Vector3.left : firstDirection) < .001f, "Aim retention within burst: " + mode);
            world.Step(.42f, .21f, changed); var third = Children(world, parent)[2];
            Check(Vector3.Distance(Direction(world, third), mode == DirectedWaveAttackAimMode.AimOnStart ? firstDirection : Vector3.left) < .001f, "Aim refresh at next burst: " + mode);
        }
    }
    private static void TestStationaryAndOrigin()
    {
        using var world = new EnemyProjectilePreviewWorld(); var child = Child(); Set(child, "spawnOffset", new Vector2(2f, 3f));
        Set(child, "fireMode", DirectedWaveAttackFireMode.Stationary); child.Stationary.ConfigureWorldPoint(new Vector2(10f, 3f));
        child.Pattern.Shooting.EmissionOrigin.X.Configure(EnemyEmissionOriginSource.World, 100f);
        child.Pattern.Shooting.EmissionOrigin.Y.Configure(EnemyEmissionOriginSource.World, 100f);
        var parent = Spawn(world, child); world.Step(.1f, .1f, Target); var entity = Children(world, parent)[0];
        var p = world.SimulationWorld.EntityManager.GetComponentData<LocalTransform>(entity).Position;
        Check(Vector3.Distance(new Vector3(p.x, p.y, p.z), new Vector3(2.1f, 3f)) < .001f, "Stored Emission Origin cannot override parent origin");
        Check(Vector3.Distance(Direction(world, entity), Vector3.right) < .001f, "Stationary aims from actual emitter point");
    }
    private static void TestReverseAimedSector()
    {
        foreach (int count in new[] { 3, 1000 })
        {
            using var world = new EnemyProjectilePreviewWorld(); var child = Child();
            Set(child, "fireMode", DirectedWaveAttackFireMode.Aimed); Set(child, "aimMode", DirectedWaveAttackAimMode.AimOnStart);
            var layout = child.Pattern.Shooting.EmissionLayout; layout.Configure(EnemyEmissionLayoutType.Sector, count);
            Set(layout, "radius", 2f); Set(layout, "launchMode", EnemySectorLaunchMode.Sequential); Set(layout, "reverseOrder", true);
            Set(layout, "launchInterval", 0f); child.Pattern.Validate();
            var parent = Spawn(world, child); world.Step(.1f, .1f, Target); var entities = Children(world, parent);
            Check(entities.Count == Mathf.Min(256, count), "Reverse aimed Sector obeys emitter cap");
            for (int order = 0; order < entities.Count; order++)
            {
                var entity = entities[order];
                var position = world.SimulationWorld.EntityManager.GetComponentData<LocalTransform>(entity).Position;
                var aim = (Target - new Vector3(position.x, position.y, position.z)).normalized;
                float sectorOffset = layout.GetAngle(layout.GetLaunchIndex(order)) - (layout.StartAngle + layout.SectorSweep * .5f);
                var expected = Quaternion.Euler(0f, 0f, sectorOffset) * aim;
                Check(Vector3.Distance(Direction(world, entity), expected) < .001f, "Reverse Sector aim corresponds to actual emitter and sector angle");
            }
        }
    }
    private static void TestMovement()
    {
        foreach (var movement in new[] { EnemyProjectileMovementPattern.Default, EnemyProjectileMovementPattern.AngularTurn, EnemyProjectileMovementPattern.Lateral, EnemyProjectileMovementPattern.Orbit })
        {
            using var world = new EnemyProjectilePreviewWorld(); var child = Child(); var shooting = child.Pattern.Shooting;
            Set(shooting, "projectileMovementPattern", movement); Set(shooting, "projectileOrbitRadius", 1f);
            child.Pattern.Validate(); var parent = Spawn(world, child); world.Step(.1f, .1f, Target); var entity = Children(world, parent)[0];
            var before = world.SimulationWorld.EntityManager.GetComponentData<LocalTransform>(entity).Position;
            world.Step(.2f, .1f, Target); var after = world.SimulationWorld.EntityManager.GetComponentData<LocalTransform>(entity).Position;
            Check(math.distance(before, after) > .001f, "Child moves: " + movement);
        }
        using var dna = new EnemyProjectilePreviewWorld(); var settings = Child();
        settings.Pattern.Shooting.ConfigureLegacyFan(3, -40f, 40f);
        Set(settings.Pattern.Shooting, "projectileMovementPattern", EnemyProjectileMovementPattern.Orbit);
        Set(settings.Pattern.Shooting, "projectileOrbitCenterMode", EnemyProjectileOrbitCenterMode.DnaLike); settings.Pattern.Validate();
        var owner = Spawn(dna, settings); dna.Step(.1f, .1f, Target); Check(Children(dna, owner).Count == 3, "DNA fan expands");
        using var q = dna.SimulationWorld.EntityManager.CreateEntityQuery(typeof(EnemyProjectileMovement));
        foreach (var entity in Children(dna, owner)) Check(dna.SimulationWorld.EntityManager.GetComponentData<EnemyProjectileMovement>(entity).Pattern == EnemyProjectileMovementPattern.Orbit, "DNA movement retained");
    }
    private static void TestRuntimeSystem()
    {
        using var world = new EnemyProjectilePreviewWorld(); Spawn(world, Child());
        world.SimulationWorld.GetOrCreateSystemManaged<EnemyChildAttackSystem>().Update();
        Check(world.Count == 2, "Runtime ECS scheduling system launches children");
    }
    private static void TestDamageInheritance()
    {
        using var world = new EnemyProjectilePreviewWorld(); var child = Child(); child.Pattern.Shooting.SetChildAttack(Child()); child.Pattern.Validate();
        var parent = Spawn(world, child, 2.5f); float expected = world.SimulationWorld.EntityManager.GetComponentData<EnemyProjectileDamage>(parent).Value;
        world.Step(.1f, .1f, Target); world.Step(.2f, .1f, Target);
        foreach (var entity in Children(world, parent))
            Check(Mathf.Abs(world.SimulationWorld.EntityManager.GetComponentData<EnemyProjectileDamage>(entity).Value - expected) < .0001f, "Children inherit wave damage multiplier");
    }
    private static void TestBurstLineage()
    {
        var actor = UnityEngine.Object.Instantiate(bullet.gameObject);
        actor.hideFlags = HideFlags.HideAndDontSave;
        try
        {
            var configuration = actor.GetComponent<EnemyProjectileAuthoring>();
            Set(configuration, "flightMode", EnemyProjectileFlightMode.BurstAtPoint);
            Set(configuration, "burstProjectilePrefab", bullet); Set(configuration, "burstWaveCount", 1);
            Set(configuration, "burstProjectilesPerWave", 4); Set(configuration, "burstDetonationPoint", Vector2.zero);
            using var world = new EnemyProjectilePreviewWorld();
            var child = Child(); Set(child, "triggerMode", EnemyChildAttackTriggerMode.OnEvent); Set(child, "eventType", EnemyChildAttackEvent.BurstAtPoint);
            var rootSettings = new EnemyShootingSettings(); rootSettings.SetChildAttack(child);
            var budget = new EnemyChildAttackBudget(); var requests = new List<EnemyProjectileSpawnRequest>();
            EnemyAttackProjectileBuilder.AppendEmitter(requests, actor.GetComponent<EnemyBullet>(), rootSettings, Vector3.zero, Vector3.zero,
                Vector3.right, 1f, 1f, 0f, new EnemyProjectilePrefabSet(bullet, null, actor.GetComponent<EnemyBullet>()), budget: budget);
            world.Spawn(requests); world.Step(.1f, .1f, Target);
            Check(world.Count == 5, "Burst event attack and authored secondary burst both launch");
            Check(budget.Remaining == EnemyChildAttackSettings.MaximumDescendants - 5, "Authored Burst At Point secondaries consume family budget");
            using var query = world.SimulationWorld.EntityManager.CreateEntityQuery(typeof(EnemyProjectileStaticData));
            using var entities = query.ToEntityArray(Allocator.Temp);
            foreach (var entity in entities)
                Check(world.SimulationWorld.EntityManager.IsComponentEnabled<EnemyChildAttackLineage>(entity), "Burst descendants retain lineage after source destruction");
        }
        finally { UnityEngine.Object.DestroyImmediate(actor); }
    }

    private static void TestSafety()
    {
        using (var cycle = new EnemyProjectilePreviewWorld())
        {
            var child = Child(); child.Pattern.Shooting.SetChildAttack(child); Spawn(cycle, child);
            for (int i = 1; i <= 16; i++) cycle.Step(i * .05f, .05f, Target);
            Check(cycle.Count <= EnemyChildAttackSettings.MaximumDepth + 1, "Cyclic data stops at generation limit");
        }
        using (var branch = new EnemyProjectilePreviewWorld())
        {
            var child = Child(); child.Pattern.Shooting.ConfigureLegacyFan(5, -40f, 40f); child.Pattern.Shooting.SetChildAttack(child); Spawn(branch, child);
            int previous = branch.Count;
            for (int i = 1; i <= 25; i++)
            {
                branch.Step(i * .05f, .05f, Target);
                Check(branch.Count - previous <= EnemyChildAttackSettings.MaximumPerStep, "Child frame cap"); previous = branch.Count;
            }
            Check(branch.Count <= EnemyChildAttackSettings.MaximumDescendants + 1 && branch.Omitted > 0, "Branch family/global preview caps");
        }
    }
    private static void TestInspectorAndPresets()
    {
        var root = PrefabUtility.LoadPrefabContents("Assets/Prefub/Enemies Prefab/Enemy.prefab");
        var preset = ScriptableObject.CreateInstance<DirectedWaveAttackPreset>();
        var restored = ScriptableObject.CreateInstance<DirectedWaveAttackPreset>();
        try
        {
            var executor = root.GetComponent<SimpleEnemyAttackExecutor>(); var serialized = new SerializedObject(executor);
            serialized.FindProperty("burstAttackSettings.attackPatterns").intValue = (int)EnemyAttackPatternsMode.Single;
            serialized.FindProperty("burstAttackSettings.shooting.useChildAttackPattern").boolValue = true;
            serialized.FindProperty("burstAttackSettings.shooting.childAttack").managedReferenceValue = Child(); serialized.ApplyModifiedPropertiesWithoutUndo();
            var child = serialized.FindProperty("burstAttackSettings.shooting.childAttack"); var nested = child.FindPropertyRelative("pattern.shooting");
            var fields = (IEnumerable)typeof(EnemyShootingSettingsDrawer).GetMethod("BehaviourFields", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { nested });
            foreach (string name in fields) Check(name != "emissionOrigin", "Child Inspector hides Emission Origin");
            Check(EditorGUI.GetPropertyHeight(child, true) > 250f, "Nested Inspector has full pattern height");
            var source = new DirectedWaveAttackSettings(); source.WaveBurstSettings.Shooting.SetChildAttack(Child()); preset.SetAttackSettings(source);
            Check(preset.AttackSettings.WaveBurstSettings.Shooting.ChildAttack != source.WaveBurstSettings.Shooting.ChildAttack, "Preset stores independent child settings");
            EditorJsonUtility.FromJsonOverwrite(EditorJsonUtility.ToJson(preset), restored);
            Check(restored.AttackSettings.WaveBurstSettings.Shooting.ChildAttack != null, "Preset Unity serialization retains managed child references");
            using var preview = new EnemyChildAttackScenePreview(); preview.Simulate(executor, .2f, Target);
            Check(preview.Count >= 2, "Standalone Scene View preview simulates children");
        }
        finally { PrefabUtility.UnloadPrefabContents(root); UnityEngine.Object.DestroyImmediate(preset); UnityEngine.Object.DestroyImmediate(restored); }
    }
    private static void Set(object target, string field, object value) => target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); checks++; }
}
