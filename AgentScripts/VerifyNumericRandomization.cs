using System;
using System.Collections.Generic;
using System.Reflection;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class VerifyNumericRandomization
{
    private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    private static int checks;
    private static void Check(bool value, string message) { checks++; if (!value) throw new Exception(message); }
    private static void Near(float actual, float expected, string message) => Check(Mathf.Abs(actual - expected) < .0002f, message + $" {actual} != {expected}");
    public static object Run()
    {
        checks = 0; NumericRandomPreviewControls.SetSeedOffset(0);
        Pure();
        foreach (var mode in (EnemySpeedRandomizeOn[])Enum.GetValues(typeof(EnemySpeedRandomizeOn)))
            CompareRuntimePreview(mode, EnemyEmissionOriginCaptureMode.OnShot, false);
        foreach (var origin in new[] { EnemyEmissionOriginCaptureMode.OnAttackStart, EnemyEmissionOriginCaptureMode.OnBurstStart })
            CompareRuntimePreview(EnemySpeedRandomizeOn.PerShot, origin, false);
        CompareRuntimePreview(EnemySpeedRandomizeOn.PerAttack, EnemyEmissionOriginCaptureMode.OnShot, true);
        CompareRuntimePreview(EnemySpeedRandomizeOn.PerBurst, EnemyEmissionOriginCaptureMode.OnBurstStart, false, false);
        CompareRuntimePreview(EnemySpeedRandomizeOn.PerProjectile, EnemyEmissionOriginCaptureMode.OnShot, false, true, true);
        return new { passed = true, checks, exactRuntimePreviewSamples = true, speedModes = 4, originModes = 3,
            consistent = true, cooldown = true, presetsAndSerialization = true };
    }
    private static void Pure()
    {
        var range = new RandomFloatRange { enabled = true, min = 5, max = 2 };
        var a = new NumericRandomStream(77); var b = new NumericRandomStream(77);
        double total = 0;
        for (int i = 0; i < 1000; i++)
        { float value = range.Sample(900, ref a); Check(value >= 2 && value <= 5, "Reversed range bounded"); Near(value, range.Sample(900, ref b), "Same seed repeats"); total += value; }
        Check(Math.Abs(total / 1000 - 3.5) < .1, "Uniform range mean");
        range.min = range.max = 3; Near(range.Sample(900, ref a), 3, "Degenerate range");
        range.enabled = false; Near(range.Sample(900, ref a), 900, "Fixed value retained");
        var ints = new RandomIntRange { enabled = true, min = 3, max = -1 };
        var seen = new HashSet<int>(); for (int i = 0; i < 100; i++) { int value = ints.Sample(0, ref a); Check(value >= -1 && value <= 3, "Int bounds"); seen.Add(value); }
        Check(seen.Count == 5, "Inclusive uniform Int support");
        var settings = new DirectedWaveAttackSettings();
        settings.WaveBurstSettings.AttackCooldownRandom.CopyFrom(new RandomFloatRange { enabled = true, min = 2, max = 4 });
        var shooting = settings.WaveBurstSettings.Shooting;
        shooting.SetProjectileBaseSpeedOverride(true, 7);
        shooting.ProjectileBaseSpeedRandom.CopyFrom(new RandomFloatRange { enabled = true, min = 3, max = 6 });
        shooting.EmissionOrigin.X.Configure(EnemyEmissionOriginSource.World, 8, EnemyEmissionOriginReferenceMode.Boundary, true);
        shooting.EmissionOrigin.X.OffsetRandom.CopyFrom(new RandomFloatRange { enabled = true, min = -3, max = -1 });
        var copy = new DirectedWaveAttackSettings(); copy.CopyFrom(settings);
        Check(copy.WaveBurstSettings.AttackCooldownRandom.enabled && copy.WaveBurstSettings.Shooting.ProjectileBaseSpeedRandom.enabled,
            "Copies preserve ranges");
        copy.WaveBurstSettings.Shooting.ProjectileBaseSpeedRandom.min = 100;
        Near(shooting.ProjectileBaseSpeedRandom.min, 3, "Private range copies");
        var restored = JsonUtility.FromJson<DirectedWaveAttackSettings>(JsonUtility.ToJson(settings));
        Check(restored.WaveBurstSettings.Shooting.EmissionOrigin.X.OffsetRandom.enabled, "Serialized origin range");
        Near(restored.WaveBurstSettings.Shooting.EmissionOrigin.X.Offset, 8, "Serialized original float remains unchanged");
        var origin = shooting.EmissionOrigin; Near(origin.Resolve(Vector3.zero, Vector3.zero, new Rect(0, 0, 10, 20), new Vector2(-2, 0)).x, 12, "Boundary offset points outward");
        origin.X.Configure(EnemyEmissionOriginSource.World, 8); Near(origin.Resolve(Vector3.zero, Vector3.zero, default, new Vector2(-2, 0)).x, -2, "Absolute random world coordinate");
        var old = JsonUtility.FromJson<EnemyBurstAttackSettings>("{\"attackCooldown\":7,\"shooting\":{\"projectileBaseSpeed\":9}}");
        Near(old.AttackCooldown, 7, "Old serialized cooldown preserved"); Check(!old.AttackCooldownRandom.enabled && !old.Shooting.ProjectileBaseSpeedRandom.enabled, "Old ranges disabled");
        var preset = ScriptableObject.CreateInstance<DirectedWaveAttackPreset>();
        try
        {
            var speedRange = new RandomFloatRange { enabled = true, min = 11, max = 13 };
            preset.SetAttackSettings(settings, true, 12, speedRange, EnemySpeedRandomizeOn.PerBurst);
            var pattern = new DirectedWaveAttackPattern(); Set(pattern, "attackPreset", preset);
            var resolved = new DirectedWaveAttackSettings(); pattern.CopyResolvedSettingsTo(resolved);
            Check(resolved.RuntimeProjectileBaseSpeedRandom.enabled && resolved.RuntimeProjectileBaseSpeedRandomizeOn == EnemySpeedRandomizeOn.PerBurst, "Preset speed range resolves");
            pattern.UsePresetAsLocalSettings(); pattern.CopyResolvedSettingsTo(resolved);
            Check(resolved.RuntimeProjectileBaseSpeedRandom.enabled, "Convert preset to inline keeps range");
        }
        finally { UnityEngine.Object.DestroyImmediate(preset); }
        var registry = new EnemyAttackNumericRegistry(); registry.Configure(0);
        var sampled = EnemyAttackCooldownSampler.Sample(settings.WaveBurstSettings, registry);
        Check(sampled >= 2 && sampled <= 4, "Cooldown sampled from range");
        Set(settings, "burstSettingsSource", DirectedWaveBurstSettingsSource.EnemySettings);
        Set(settings, "overrideEnemyAttackCooldown", true); Set(settings, "enemyAttackCooldown", 17f);
        Near(EnemyAttackCooldownSampler.Sample(settings.WaveBurstSettings, registry, settings), 17, "Fixed wave cooldown override retains priority");
        settings.EnemyAttackCooldownRandom.CopyFrom(new RandomFloatRange { enabled = true, min = 21, max = 21 });
        Near(EnemyAttackCooldownSampler.Sample(settings.WaveBurstSettings, registry, settings), 21, "Random wave cooldown override priority");
    }
    private static void CompareRuntimePreview(EnemySpeedRandomizeOn mode, EnemyEmissionOriginCaptureMode originMode, bool consistent, bool burst = true, bool sector = false)
    {
        var scene = EditorSceneManager.NewPreviewScene(); var root = new GameObject("Numeric random verification");
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, scene);
        var actor = new GameObject("Numeric runtime enemy"); actor.SetActive(false);
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(actor, scene);
        using var runtime = new EnemyProjectilePreviewWorld();
        try
        {
            var wave = root.AddComponent<DirectedEnemySubWave>(); var attack = root.GetComponent<DirectedWaveAttackBehaviour>() ?? root.AddComponent<DirectedWaveAttackBehaviour>();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefub/Enemies Prefab/Enemy.prefab").GetComponent<Enemy>();
            var bullet = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefub/Enemy Projectiles/Bullet.prefab").GetComponent<EnemyBullet>();
            Set(wave, "enemyPrefab", prefab); Set(wave, "enemyCount", 1);
            var settings = (DirectedWaveAttackSettings)Get(attack, "attackSettings");
            Set(settings, "fireMode", DirectedWaveAttackFireMode.Stationary); settings.StationaryFireSettings.ConfigureAngle(180);
            Set(settings, "burstSettingsSource", DirectedWaveBurstSettingsSource.WaveOverride);
            Set(settings, "waitForPreviousAttack", true); Set(settings, "delayAfterAttack", 1000f);
            var profile = settings.WaveBurstSettings; profile.ConfigureLegacyRepeatedBurst(2, 1000, .15f);
            Set(profile, "attackShotCount", 2); Set(profile, "attackShotInterval", .25f);
            profile.AttackCooldownRandom.CopyFrom(new RandomFloatRange { enabled = true, min = 10, max = 20 });
            if (!burst) { Set(profile, "burstAttack", EnemyBurstAttackMode.None); Set(profile, "attackShotCount", 4); Set(profile, "attackShotInterval", .15f); }
            var shooting = profile.Shooting; shooting.SetProjectileBaseSpeedOverride(true, 4);
            shooting.ConfigureLegacyFan(3, 0, 0);
            if (sector)
            {
                var layout = shooting.EmissionLayout; layout.Configure(EnemyEmissionLayoutType.Sector, 3);
                Set(layout, "launchMode", EnemySectorLaunchMode.Sequential); Set(layout, "launchInterval", .2f);
                Set(layout, "startDelay", .1f); Set(layout, "radius", 0f); Set(layout, "startAngle", 0f); Set(layout, "endAngle", 0f);
            }
            shooting.ProjectileBaseSpeedRandom.CopyFrom(new RandomFloatRange { enabled = true, min = 2, max = 6 });
            Set(shooting, "speedRandomizeOn", mode); Set(shooting, "numericRandomSeed", 2468);
            if (consistent) { Set(shooting, "initialSpeedMode", EnemyProjectileInitialSpeedMode.Consistent); Set(shooting, "consistentInitialSpeedChangePerVolley", -.1f); }
            shooting.EmissionOrigin.X.Configure(EnemyEmissionOriginSource.World);
            shooting.EmissionOrigin.Y.Configure(EnemyEmissionOriginSource.World, 3);
            shooting.EmissionOrigin.X.OffsetRandom.CopyFrom(new RandomFloatRange { enabled = true, min = -3, max = 3 });
            Set(shooting.EmissionOrigin.X, "randomizeOn", originMode);
            Set(shooting, "overrideProjectileLifetime", true); Set(shooting, "projectileLifetime", 40f); settings.Validate();
            var enemy = actor.AddComponent<Enemy>(); var executor = actor.AddComponent<SimpleEnemyAttackExecutor>();
            Set(executor, "defaultProjectilePrefab", bullet); Set(executor, "projectileSpawner", Get(runtime, "spawner"));
            Invoke(executor, "Awake"); actor.SetActive(true); executor.SetNumericOwner(0);
            executor.SetWaveAttackSettings(profile); executor.BeginEmissionOriginAttack(profile); executor.BeginAttackSequence(profile);
            for (int i = 0; i < 4; i++)
            {
                if (sector) Clock(enemy, new[] { 0f, .15f, .4f, .55f }[i]);
                Check(executor.PrepareEmissionOriginShot(profile, i % 2 == 0), "Runtime origin ready");
                Check(executor.TryFireStationary(Vector3.down, false, profile), "Runtime volley spawned");
            }
            if (sector) { Clock(enemy, 2f); Invoke(executor, "TickPendingSectors", 2f); }
            float cooldown = EnemyAttackCooldownSampler.Sample(profile, executor.NumericRegistry, settings);
            var actual = Capture(runtime);
            using var preview = new WaveShootingPreviewSession(wave, true, false, 0, Vector3.down * 5);
            while (!preview.AdvanceTo(sector ? 2f : 1f, 1000d)) { }
            var expected = Capture(preview.ProjectileWorld);
            int totalCount = sector ? 36 : 12;
            Check(actual.Count == totalCount && expected.Count == totalCount, "Four Fan/Sector volleys created: " + actual.Count + "/" + expected.Count);
            if (sector) { actual.Sort(Compare); expected.Sort(Compare); }
            for (int i = 0; i < actual.Count; i++)
            { Near(actual[i].x, expected[i].x, "Runtime/Preview offset " + i); Near(actual[i].y, expected[i].y, "Runtime/Preview speed " + i); }
            var groups = (System.Collections.IList)Get(preview, "groups"); var slots = (System.Collections.IList)Get(groups[0], "Slots");
            float ready = (float)Get(slots[0], "ReadyTime");
            Near(ready - (float)Get(slots[0], "CompletionTime"), cooldown, "Runtime/Preview chosen cooldown");
            for (int i = 0; i < 5; i++) Near((float)Get(slots[0], "ReadyTime"), ready, "Cooldown reads do not resample");
            int uniqueX = Unique(actual, true); Check(uniqueX == (originMode == EnemyEmissionOriginCaptureMode.OnAttackStart ? 1 : originMode == EnemyEmissionOriginCaptureMode.OnBurstStart ? (burst ? 2 : 1) : 4), "Offset capture frequency");
            if (!consistent) Check(Unique(actual, false) == (mode == EnemySpeedRandomizeOn.PerAttack ? 1 : mode == EnemySpeedRandomizeOn.PerBurst ? (burst ? 2 : 1) : mode == EnemySpeedRandomizeOn.PerShot ? 4 : totalCount), "Speed sampling frequency " + mode);
            else Near(actual[3].y, actual[0].y - .1f, "Consistent step applies after entire volley");
            using (var repeat = new WaveShootingPreviewSession(wave, true, false, 0, Vector3.down * 5))
            {
                while (!repeat.AdvanceTo(sector ? 2f : 1f, 1000d)) { }
                var repeated = Capture(repeat.ProjectileWorld); if (sector) repeated.Sort(Compare);
                for (int i = 0; i < repeated.Count; i++) { Near(repeated[i].x, expected[i].x, "Repeat Seed origin"); Near(repeated[i].y, expected[i].y, "Repeat Seed speed"); }
            }
            NumericRandomPreviewControls.SetSeedOffset(99);
            using (var reroll = new WaveShootingPreviewSession(wave, true, false, 0, Vector3.down * 5))
            {
                while (!reroll.AdvanceTo(.1f, 1000d)) { }
                var changed = Capture(reroll.ProjectileWorld);
                if (!sector) Check(changed[0] != expected[0], "New Seed changes generation");
            }
            NumericRandomPreviewControls.SetSeedOffset(0);
            NumericRandomPreviewControls.Repeat(); Check(!preview.IsConfigurationCurrent, "Repeat Seed invalidates existing Preview");
            executor.EndAttackSequence(profile);
        }
        finally { UnityEngine.Object.DestroyImmediate(actor); UnityEngine.Object.DestroyImmediate(root); EditorSceneManager.ClosePreviewScene(scene); }
    }
    private static int Compare(Vector2 a, Vector2 b) { int x = Mathf.RoundToInt(a.x * 10000).CompareTo(Mathf.RoundToInt(b.x * 10000)); return x != 0 ? x : a.y.CompareTo(b.y); }
    private static void Clock(Enemy enemy, float time)
    {
        object clock = new EnemyAttackClock(); Set(clock, "initialized", true); Set(clock, "previousTime", Time.time);
        Set(clock, "attackTime", time); Set(enemy, "attackClock", clock);
    }
    private static List<Vector2> Capture(EnemyProjectilePreviewWorld world)
    {
        using var q = world.SimulationWorld.EntityManager.CreateEntityQuery(typeof(LocalTransform), typeof(EnemyProjectileVelocity));
        using var positions = q.ToComponentDataArray<LocalTransform>(Allocator.Temp); using var velocities = q.ToComponentDataArray<EnemyProjectileVelocity>(Allocator.Temp);
        var result = new List<Vector2>(); for (int i = 0; i < positions.Length; i++) result.Add(new Vector2(positions[i].Position.x, Unity.Mathematics.math.length(velocities[i].Value.xy))); return result;
    }
    private static int Unique(List<Vector2> samples, bool x) { var values = new HashSet<int>(); foreach (var sample in samples) values.Add(Mathf.RoundToInt((x ? sample.x : sample.y) * 100000)); return values.Count; }
    private static object Get(object value, string name) => value.GetType().GetField(name, Fields).GetValue(value);
    private static void Set(object value, string name, object field)
    {
        for (var type = value?.GetType(); type != null; type = type.BaseType)
        {
            var info = type.GetField(name, Fields); if (info != null) { info.SetValue(value, field); return; }
            var property = type.GetProperty(name, Fields); if (property != null) { property.SetValue(value, field); return; }
        }
        throw new Exception("Missing field " + name + " on " + value?.GetType().Name);
    }
    private static object Invoke(object value, string name, params object[] args) => value.GetType().GetMethod(name, Fields).Invoke(value, args);
}
