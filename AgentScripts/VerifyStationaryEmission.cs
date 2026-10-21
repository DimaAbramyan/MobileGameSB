using System;
using System.Collections;
using System.Reflection;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using UnityEditor;
using UnityEngine;

public static class VerifyStationaryEmission
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static int checks;
    private struct Shot { public Vector3 Position, Direction; public EnemyProjectileMovement Movement; }
    private sealed class Target : IEnemyAttackAimTarget { public Vector3 AttackTargetPosition => new Vector3(-100, 100); }

    public static object Run()
    {
        checks = 0;
        if (EditorApplication.isPlaying || EditorApplication.isCompiling || EditorApplication.isUpdating) throw new Exception("Idle Editor required.");
        Migration();
        var scene = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
        var clone = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefub/Enemies Prefab/Enemy.prefab"));
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(clone, scene);
        var world = new World("Stationary verification");
        var spawner = new EnemyProjectileEcsSpawner(); Set(spawner, "entityWorld", world); Set(spawner, "entityManager", world.EntityManager);
        try
        {
            clone.transform.position = new Vector3(2, 3); clone.transform.rotation = Quaternion.Euler(0, 0, 27);
            var executor = clone.GetComponent<SimpleEnemyAttackExecutor>(); Set(executor, "projectileSpawner", spawner); Invoke(executor, "Awake"); Set(executor, "autonomousAttack", false);
            var profile = new EnemyBurstAttackSettings(); profile.Validate(); profile.SetFacing(EnemyFacingMode.Fixed);
            profile.Shooting.ConfigureLegacyFan(3, -15, 15); Set(profile.Shooting, "shotGunMode", EnemyProjectileShotGunMode.ShotGun);
            Set(profile.Shooting, "shotGunProjectileCount", 2); Set(profile.Shooting, "shotGunDirection", Vector2.down);
            Set(profile.Shooting, "projectileMovementPattern", EnemyProjectileMovementPattern.Orbit); Set(profile.Shooting, "projectileOrbitCenterMode", EnemyProjectileOrbitCenterMode.DnaLike);
            profile.Shooting.SetProjectileBaseSpeedOverride(true, 3);
            var stationary = new EnemyStationaryFireSettings();
            foreach (EnemyEmissionLayoutType layout in Enum.GetValues(typeof(EnemyEmissionLayoutType)))
            {
                if (layout == EnemyEmissionLayoutType.Sector) profile.Shooting.EmissionLayout.ConfigureSector(350, 10, 3);
                else profile.Shooting.EmissionLayout.Configure(layout, 4, .7f, 20, new Vector3(.2f, .3f), new Vector3(.1f, -.2f));
                foreach (bool point in new[] { false, true })
                {
                    if (point) stationary.ConfigureWorldPoint(new Vector2(5, -8)); else stationary.ConfigureAngle(45);
                    profile.Shooting.SetLayoutRotationEnabled(true);
                    profile.Shooting.Rotation.Configure(FourWayEnemyRotationDirection.CounterClockwise, FourWayEnemyRotationMode.Continuous, 90, AnimationCurve.Linear(0, 0, 1, 1), 360, 0, true);
                    executor.SetWaveAttackSettings(null); executor.SetWaveAttackSettings(profile); Set(executor, "patternRotationTime", .25f);
                    executor.SetWaveAimTarget(new Target());
                    Assert(stationary.TryFire(executor, profile, executor.AttackForward), "Stationary emits for " + layout);
                    var shots = Read(world); Assert(shots.Length == profile.Shooting.ProjectilesPerVolley, "Fan and Stack counts per emitter.");
                    var frame = EnemyShootingPreview.CreateFrame(executor, profile.Shooting, clone.transform.position, .25f, null, out var basis);
                    if (point) frame.Target = stationary.WorldPoint;
                    for (int arm = 0; arm < profile.Shooting.DirectionCount; arm++)
                    {
                        var emitter = EnemyShootingPreviewGeometry.GetEmitter(profile.Shooting, arm, frame, basis, .25f, requestedDirection: stationary.Direction, aimed: point, stationary: true);
                        Vector3 baseDirection = point ? ((Vector3)stationary.WorldPoint - shots[arm * 6].Position).normalized : Quaternion.Euler(0, 0, 45) * Vector3.up;
                        if (!(point && (layout == EnemyEmissionLayoutType.Point || layout == EnemyEmissionLayoutType.Circle)))
                            baseDirection = Quaternion.Euler(0, 0, 22.5f + (layout == EnemyEmissionLayoutType.Circle ? 0 : profile.Shooting.EmissionLayout.GetAngle(arm))) * baseDirection;
                        Near(emitter.BaseDirection, baseDirection, "World direction ignores enemy orientation and Stack direction.");
                        for (int j = 0; j < 6; j++)
                        {
                            var shot = shots[arm * 6 + j]; var preview = EnemyShootingPreviewGeometry.GetProjectile(profile.Shooting, emitter, j);
                            Near(shot.Position, preview.Position, "Preview emitter equals actual ECS origin.");
                            Near(shot.Direction, Quaternion.Euler(0, 0, 15 - (j % 3) * 15) * baseDirection, "Fan applies to Stationary base.");
                            Near(shot.Direction, preview.Direction, "Preview direction equals actual ECS projectile.");
                            Near(new Vector3(shot.Movement.DnaMedianDirection.x, shot.Movement.DnaMedianDirection.y), baseDirection, "DNA median follows Stationary direction.");
                        }
                    }
                    Clear(world);
                }
            }
            // A fixed world point must not turn into the player's position during a delayed Sector.
            stationary.ConfigureWorldPoint(new Vector2(5, -8)); profile.Shooting.SetLayoutRotationEnabled(false);
            profile.Shooting.EmissionLayout.ConfigureSector(350, 10, 3, EnemySectorLaunchMode.Sequential, .2f, .1f);
            executor.SetWaveAttackSettings(null); executor.SetWaveAttackSettings(profile); executor.SetWaveAimTarget(new Target());
            Assert(stationary.TryFire(executor, profile, executor.AttackForward), "Delayed Stationary Sector queued.");
            var pending = ((IList)Get(executor, "pendingSectors"))[0]; float start = (float)Get(pending, "StartTime");
            Assert(Read(world).Length == 0, "Start Delay respected."); clone.transform.position += Vector3.right;
            Invoke(executor, "TickPendingSectors", start + .4001f);
            var delayed = Read(world); Assert(delayed.Length == 18, "All delayed Fan/Stack directions launched.");
            for (int arm = 0; arm < 3; arm++)
            {
                Vector3 expected = Quaternion.Euler(0, 0, 350 + arm * 10 + 15) * ((Vector3)stationary.WorldPoint - delayed[arm * 6].Position).normalized;
                Near(delayed[arm * 6].Direction, expected, "Delayed Sector aims at fixed world point, never at player.");
            }
            Clear(world);
            profile.Shooting.EmissionLayout.Configure(EnemyEmissionLayoutType.Circle, 4, .7f);
            profile.SetFacing(EnemyFacingMode.FollowPattern, speed: 0f);
            executor.SetWaveAttackSettings(null); executor.SetWaveAttackSettings(profile); executor.SetWaveAimTarget(new Target());
            Assert(stationary.TryFire(executor, profile, executor.AttackForward), "Stationary stores its own facing direction.");
            var facingShots = Read(world);
            Vector3 firstEmitterAim = ((Vector3)stationary.WorldPoint - facingShots[0].Position).normalized;
            Invoke(executor, "TickFacing", 0f);
            Near(clone.transform.up, firstEmitterAim, "Follow Pattern faces the first emitter's Stationary aim, not the player.");
            Clear(world);
            Controllers(clone.GetComponent<Enemy>(), executor, profile, stationary, scene);
            return new { passed = true, checks, tested = "Stationary world angle/world point, every layout + Fan/Stack/DNA/rotation, actual ECS vs Preview, delayed Sector ignores player, post/entrance controllers, legacy migration/copy/idempotence" };
        }
        finally
        {
            Clear(world);
            var visuals = (IDictionary)Get(spawner, "visualsByConfiguration");
            foreach (object visual in visuals.Values) { UnityEngine.Object.DestroyImmediate((Material)visual.GetType().GetField("Material").GetValue(visual)); UnityEngine.Object.DestroyImmediate((Mesh)visual.GetType().GetField("Mesh").GetValue(visual)); }
            visuals.Clear(); spawner.Dispose(); UnityEngine.Object.DestroyImmediate(clone); world.Dispose(); UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene);
        }
    }

    private static void Migration()
    {
        var old = new DirectedWaveAttackSettings(); Set(old, "serializedVersion", 11); Set(old, "fireMode", DirectedWaveAttackFireMode.Stationary);
        Set(old, "useAttackStartDelay", true); Set(old, "attackStartDelay", .9f); old.Validate();
        Assert(old.StationaryFireSettings.PreservesLegacyForward, "Old enum value 1 preserves Forward.");
        Assert(Mathf.Approximately(old.AttackStartDelay, .9f), "Version 11 delay is not overwritten by the new migration.");
        string json = JsonUtility.ToJson(old); old.Validate(); Assert(JsonUtility.ToJson(old) == json, "Migration is idempotent.");
        var copied = new DirectedWaveAttackSettings(); copied.CopyFrom(old); Assert(copied.StationaryFireSettings.PreservesLegacyForward, "Preset/resolved copies preserve legacy direction.");
        old.StationaryFireSettings.ConfigureAngle(180); copied.CopyFrom(old); Near(copied.StationaryFireSettings.Direction, Vector3.down, "Configured angle survives copying.");
        Assert(!copied.RequiresPlayerTarget, "Stationary requires no player.");
        var fresh = new DirectedWaveAttackSettings(); fresh.Validate(); Assert(!fresh.StationaryFireSettings.PreservesLegacyForward, "New profiles use fixed direction.");
        var temp = new GameObject("Migration slot"); temp.SetActive(false);
        try
        {
            var local = temp.AddComponent<DirectedWaveEnemyOverride>(); Set(local, "fireMode", DirectedWaveAttackFireMode.Stationary); Set(local, "attackOverrideVersion", 3);
            Set(local, "overrideAttackPattern", true); Set(local, "inheritWaveFireMode", false); local.EnsureAttackOverrideMigration();
            Assert(local.ResolveStationaryFireSettings(fresh.StationaryFireSettings).PreservesLegacyForward, "Old local Forward retains direction.");
            local.InitializeAttackPatternOverride(copied, new EnemyBurstAttackSettings());
            Near(local.ResolveStationaryFireSettings(fresh.StationaryFireSettings).Direction, Vector3.down, "Local initialization copies Stationary settings.");
        }
        finally { UnityEngine.Object.DestroyImmediate(temp); }
    }

    private static void Controllers(Enemy enemy, SimpleEnemyAttackExecutor executor, EnemyBurstAttackSettings profile, EnemyStationaryFireSettings stationary, UnityEngine.SceneManagement.Scene scene)
    {
        var host = new GameObject("Stationary controller wave"); host.SetActive(false); UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(host, scene);
        try
        {
            var wave = host.AddComponent<DirectedEnemySubWave>(); var settings = new DirectedWaveAttackSettings(); Set(settings, "fireMode", DirectedWaveAttackFireMode.Stationary); settings.StationaryFireSettings.ConfigureAngle(90); settings.Validate();
            profile.Shooting.EmissionLayout.Configure(EnemyEmissionLayoutType.Point); executor.SetWaveAttackSettings(null); executor.SetWaveAttackSettings(profile);
            foreach (string name in new[] { "DirectedWaveAttackController", "DirectedWaveEntranceAttackController" })
            {
                Type type = typeof(DirectedEnemySubWave).Assembly.GetType(name); object controller = Activator.CreateInstance(type,
                    name == "DirectedWaveAttackController" ? new object[] { wave, settings, null } : new object[] { wave, settings, new DirectedWaveEntranceAttackSettings() });
                if (name == "DirectedWaveAttackController") Assert((bool)Invoke(controller, "TryFireShot", enemy, executor, profile), "Post controller fires without player.");
                else { var aim = new DirectedWaveAttackAimState(); Assert((bool)Invoke(controller, "TryFireSequence", enemy, aim, 0), "Entrance controller fires without player."); }
                type.GetMethod("Stop").Invoke(controller, null);
            }
        }
        finally { UnityEngine.Object.DestroyImmediate(host); }
    }

    private static Shot[] Read(World world)
    {
        using var query = world.EntityManager.CreateEntityQuery(typeof(EnemyProjectileVelocity), typeof(LocalTransform), typeof(EnemyProjectileMovement));
        using var poses = query.ToComponentDataArray<LocalTransform>(Allocator.Temp); using var movements = query.ToComponentDataArray<EnemyProjectileMovement>(Allocator.Temp);
        var shots = new Shot[poses.Length]; for (int i = 0; i < shots.Length; i++) shots[i] = new Shot { Position = poses[i].Position, Direction = new Vector3(movements[i].ForwardDirection.x, movements[i].ForwardDirection.y), Movement = movements[i] }; return shots;
    }
    private static void Clear(World world) { using var query = world.EntityManager.CreateEntityQuery(typeof(EnemyProjectileVelocity)); world.EntityManager.DestroyEntity(query); }
    private static void Set(object obj, string name, object value) => obj.GetType().GetField(name, Private).SetValue(obj, value);
    private static object Get(object obj, string name) => obj.GetType().GetField(name, Private | BindingFlags.Public).GetValue(obj);
    private static object Invoke(object obj, string name, params object[] args) => obj.GetType().GetMethod(name, Private).Invoke(obj, args);
    private static void Assert(bool passed, string message) { checks++; if (!passed) throw new Exception(message); }
    private static void Near(Vector3 actual, Vector3 expected, string message) => Assert((actual - expected).sqrMagnitude < .00001f, message + $" ({actual} != {expected})");
}
