using System;
using System.Collections.Generic;
using System.Reflection;
using Unity.Collections;
using Unity.Entities;
using UnityEditor;
using UnityEngine;

public static class VerifyNumericChildren
{
    private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private static int checks;
    private static void Check(bool value, string message) { checks++; if (!value) throw new Exception(message); }
    public static object Run()
    {
        checks = 0;
        foreach (bool nested in new[] { false, true })
            foreach (EnemySpeedRandomizeOn mode in new[] { EnemySpeedRandomizeOn.PerAttack, EnemySpeedRandomizeOn.PerProjectile })
            {
                var a = Simulate(100, mode, nested); var b = Simulate(100, mode, nested); var c = Simulate(101, mode, nested);
                Check(a.Count == (nested ? 9 : 3), "Expected child/grandchild count");
                for (int i = 0; i < a.Count; i++) Check(Mathf.Abs(a[i] - b[i]) < .00001f, "Child lineage replays with same Seed");
                Check(Mathf.Abs(a[0] - c[0]) > .00001f, "Parent Seed changes child random stream");
                if (!nested) Check(mode == EnemySpeedRandomizeOn.PerAttack ? a[0] == a[2] : a[0] != a[2], "Child sampling frequency");
                for (int i = 0; i < a.Count; i++) Check(a[i] >= 2 && a[i] <= 9, "Child speed within selected ranges");
            }
        LocalAndLegacy();
        return new { passed = true, checks, nestedChildSeeds = true, localOverridePriority = true, legacyRandomOptIn = true };
    }
    private static List<float> Simulate(int seed, EnemySpeedRandomizeOn mode, bool nested)
    {
        using var world = new EnemyProjectilePreviewWorld();
        var bullet = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefub/Enemy Projectiles/Bullet.prefab").GetComponent<EnemyBullet>();
        var child = Child(3, 2, 5, mode);
        if (nested) child.Pattern.Shooting.SetChildAttack(Child(2, 8, 9, mode));
        var profile = new EnemyBurstAttackSettings(); profile.Shooting.SetChildAttack(child);
        Set(profile.Shooting, "numericRandomSeed", seed);
        var registry = new EnemyAttackNumericRegistry(); registry.Configure(0);
        var numeric = registry.Get(profile); numeric.BeginSpeed();
        var sample = numeric.PrepareSpeed(profile.Shooting, true, 0f);
        var requests = new List<EnemyProjectileSpawnRequest>();
        EnemyAttackProjectileBuilder.AppendEmitter(requests, bullet, profile.Shooting, Vector3.zero, Vector3.zero, Vector3.down,
            1f, .01f, 0f, new EnemyProjectilePrefabSet(bullet), randomSpeed: sample);
        world.Spawn(requests);
        using var query = world.SimulationWorld.EntityManager.CreateEntityQuery(typeof(EnemyProjectileStaticData), typeof(EnemyProjectileVelocity));
        Entity parent; using (var entities = query.ToEntityArray(Allocator.Temp)) parent = entities[0];
        world.Step(.1f, .1f, Vector3.up * 10);
        if (nested) world.Step(.2f, .1f, Vector3.up * 10);
        var result = new List<float>(); using var all = query.ToEntityArray(Allocator.Temp);
        foreach (var entity in all) if (entity != parent)
            result.Add(Unity.Mathematics.math.length(world.SimulationWorld.EntityManager.GetComponentData<EnemyProjectileVelocity>(entity).Value));
        result.Sort(); return result;
    }
    private static EnemyChildAttackSettings Child(int fan, float min, float max, EnemySpeedRandomizeOn mode)
    {
        var child = new EnemyChildAttackSettings(); Set(child, "triggerValue", 0f);
        Set(child, "fireMode", DirectedWaveAttackFireMode.Stationary); child.Stationary.ConfigureAngle(180);
        child.Pattern.Shooting.ConfigureLegacyFan(fan, 0, 0); child.Pattern.Shooting.SetProjectileBaseSpeedOverride(true, min);
        child.Pattern.Shooting.ProjectileBaseSpeedRandom.CopyFrom(new RandomFloatRange { enabled = true, min = min, max = max });
        Set(child.Pattern.Shooting, "speedRandomizeOn", mode); child.Pattern.Validate(); return child;
    }
    private static void LocalAndLegacy()
    {
        var source = new EnemyBurstAttackSettings(); source.Shooting.SetProjectileBaseSpeedOverride(true, 4);
        source.Shooting.ProjectileBaseSpeedRandom.CopyFrom(new RandomFloatRange { enabled = true, min = 3, max = 6 });
        var wave = new DirectedWaveAttackSettings(); wave.SetRuntimeProjectileBaseSpeedOverride(12,
            new RandomFloatRange { enabled = true, min = 11, max = 13 }, EnemySpeedRandomizeOn.PerBurst);
        var resolved = EnemyAttackSettingsResolver.CreateRuntimeCopy(source, null, 12);
        EnemyAttackSettingsResolver.ApplyRuntimeSpeedRandomization(resolved, null, wave);
        Check(resolved.Shooting.TryGetRandomSpeedRange(out float low, out float high) && low == 11 && high == 13, "Shared override wins without a local slot override");
        var localObject = new GameObject("Numeric local priority verification");
        try
        {
            var local = localObject.AddComponent<DirectedWaveEnemyOverride>();
            local.InitializeAttackPatternOverride(new DirectedWaveAttackSettings(), source);
            resolved = EnemyAttackSettingsResolver.CreateRuntimeCopy(source, local, 12);
            EnemyAttackSettingsResolver.ApplyRuntimeSpeedRandomization(resolved, local, wave);
            Check(resolved.Shooting.TryGetRandomSpeedRange(out low, out high) && low == 3 && high == 6, "Explicit local range wins over shared speed override");
            local.InitializeAttackPatternOverride(wave, new EnemyBurstAttackSettings());
            resolved = EnemyAttackSettingsResolver.CreateRuntimeCopy(source, local, 12);
            Check(resolved.Shooting.TryGetRandomSpeedRange(out low, out high) && low == 11 && high == 13, "Initializing local settings retains resolved shared range");
        }
        finally { UnityEngine.Object.DestroyImmediate(localObject); }
        Set(source.Shooting, "initialSpeedMode", EnemyProjectileInitialSpeedMode.Random);
        Check(!source.Shooting.TryGetRandomSpeedRange(out _, out _), "Old Random mode retains its existing sampler by default");
        Set(source.Shooting, "seededInitialSpeedRandom", true);
        Set(source.Shooting, "randomInitialSpeedMinimum", 20f); Set(source.Shooting, "randomInitialSpeedMaximum", 30f);
        Check(source.Shooting.TryGetRandomSpeedRange(out low, out high) && low == 20 && high == 30,
            "Seeded Random mode keeps its legacy range priority above Base Speed");
        var numeric = new EnemyAttackNumericState(1, 0, 1);
        Check(numeric.PrepareSpeed(source.Shooting, true, 0f).Evaluate(0) >= 20, "Seeded legacy Random uses common numeric sampler");
    }
    private static void Set(object value, string name, object field) => value.GetType().GetField(name, Fields).SetValue(value, field);
}
