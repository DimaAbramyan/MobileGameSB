using System;
using System.Collections.Generic;
using System.Reflection;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEditor;
using UnityEngine;

public static class VerifyBlackHoleRegions
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static int checks;
    public static object Run()
    {
        if (EditorApplication.isPlaying || EditorApplication.isCompiling || EditorApplication.isUpdating) throw new Exception("Idle Editor required.");
        checks = 0;
        Clock(); Defaults(); LegacyAndBoss();
        using (var fixture = new Fixture())
        {
            fixture.Configure(true, true);
            fixture.Active.TryActivate(fixture.Ship);
            Check(fixture.Active.IsActive && Mathf.Approximately(fixture.Active.CooldownRemaining, 5f), "Standard ability cooldown starts");
            Check(fixture.Passive.TryGetSlowField(out var passive), "Passive remains enabled");
            Check(fixture.Active.TryGetRegionSpeedField(out var field), "Active field exists");
            foreach (var p in new[] { new float2(0f, .5f), new float2(0f, -.5f), new float2(7f, 1f), new float2(-7f, -1f) })
            {
                var active = fixture.Preview.SimulationWorld.EntityManager.GetBuffer<EnemyProjectileRegionSpeedField>(fixture.RegistryEntity);
                var original = fixture.Preview.SimulationWorld.EntityManager.GetBuffer<EnemyProjectileSlowField>(fixture.RegistryEntity);
                Near(EnemyProjectileSpeedModifiers.EvaluateMultiplier(p, original, active), passive.EvaluateMultiplier(p) * field.EvaluateMultiplier(p), "Active and passive multiply independently");
            }
            Near(field.EvaluateMultiplier(new float2(0f, 0f)), .2f, "Line belongs to front; no neutral strip");
            Near(field.EvaluateMultiplier(new float2(7f, 7f)), .2f, "Effect reaches remote front");
            Near(field.EvaluateMultiplier(new float2(-7f, -7f)), 2.5f, "Effect reaches remote rear");
            Near(field.EvaluateMultiplier(field.Maximum + 1f), 1f, "Non-gameplay area excluded");
            fixture.Stop();
            Check(fixture.Passive.TryGetSlowField(out _), "Unchanged passive after active ends");
            Near(fixture.Registry.EvaluateActiveSpeedMultiplier(Vector2.up), 1f, "Active modifier removed");
        }
        using (var fixture = new Fixture())
        {
            fixture.Configure(true, false); Check(fixture.Active.Activate(fixture.Ship), "Activate with passive suspended");
            Check(!fixture.Passive.TryGetSlowField(out _), "EnablePassiveAbility false suspends passive");
            var front = fixture.Spawn(new Vector3(0, 5), EnemyProjectileMovementPattern.Default);
            var rear = fixture.Spawn(new Vector3(0, -5), EnemyProjectileMovementPattern.Default);
            fixture.Preview.Step(.1f, .1f, new Vector3(100, 100));
            Near(fixture.Position(front).y, 4.96f, "Front moves at .2 without changing velocity");
            Near(fixture.Position(rear).y, -5.5f, "Rear moves at 2.5");
            Near(math.length(fixture.Preview.SimulationWorld.EntityManager.GetComponentData<EnemyProjectileVelocity>(front).Value), 2f, "Authored velocity preserved");
            fixture.Ship.transform.position = new Vector3(0, 6);
            fixture.Snapshot(); fixture.Preview.Step(.2f, .1f, new Vector3(100, 100));
            Near(fixture.Position(front).y, 4.46f, "Moving divider immediately changes existing projectile region");
            for (int i = 3; i <= 8; i++) fixture.Preview.Step(i * .1f, .1f, new Vector3(100, 100));
            Near(fixture.Position(front).y, 1.46f, "Repeated updates do not compound speed");
            var later = fixture.Spawn(new Vector3(0, 8), EnemyProjectileMovementPattern.Default);
            fixture.Preview.Step(.9f, .1f, new Vector3(100, 100));
            Near(fixture.Position(later).y, 7.96f, "New projectile receives current field");
            fixture.Stop();
            Check(fixture.Passive.TryGetSlowField(out _), "Suspended passive restores on completion");
            Check(fixture.Preview.SimulationWorld.EntityManager.GetBuffer<EnemyProjectileRegionSpeedField>(fixture.RegistryEntity).Length == 0, "ECS active buffer clears immediately");
            Near(fixture.Registry.EvaluateActiveSpeedMultiplier(new Vector2(0, 7)), 1f, "No active multiplier remains");
        }
        using (var fixture = new Fixture())
        {
            fixture.Configure(false, true); fixture.Active.Activate(fixture.Ship);
            Near(fixture.Registry.EvaluateActiveSpeedMultiplier(Vector2.up, true), 1f, "RewriteAttackSpeed false preserves attack timing");
            Near(fixture.Registry.EvaluateActiveSpeedMultiplier(Vector2.up), .2f, "RewriteAttackSpeed false still changes projectiles");
            fixture.Stop(); fixture.Configure(true, true); fixture.Active.Activate(fixture.Ship);
            var enemyObject = new GameObject("regional enemy");
            try
            {
                var enemy = enemyObject.AddComponent<Enemy>(); Set(enemy, "projectileCollisionRegistry", fixture.Registry);
                enemy.transform.position = Vector3.up * 5; Near(enemy.AttackSpeedMultiplier, .2f, "Enemy front fire-rate factor");
                PatternRotation(enemy, .2f);
                enemy.MultiplyFireRate(1.7f); Near(enemy.FireRateMultiplier, 1.7f, "Authored difficulty bonus remains independent");
                enemy.transform.position = Vector3.down * 5; Near(enemy.AttackSpeedMultiplier, 2.5f, "Enemy crossing line receives rear factor");
                PatternRotation(enemy, 1.7f * 2.5f);
                Set(fixture.Active, "activeUntil", Time.time - 1f); Call(fixture.Active, "Update"); fixture.Snapshot();
                Near(enemy.AttackSpeedMultiplier, 1f, "Expiry restores normal attack timing");
                PatternRotation(enemy, 1.7f);
                fixture.Configure(false, true); fixture.Active.Activate(fixture.Ship);
                PatternRotation(enemy, 1.7f);
                fixture.Stop();
            }
            finally { UnityEngine.Object.DestroyImmediate(enemyObject); }
            Check(!fixture.Active.IsActive && fixture.Passive.TryGetSlowField(out _), "Expiry leaves passive active");
            fixture.Active.Activate(fixture.Ship); fixture.Ship.HideShip(); Call(fixture.Active, "Update");
            Check(!fixture.Active.IsActive && !fixture.Passive.TryGetSlowField(out _), "Ship switch removes active and does not reactivate hidden passive");
            Near(fixture.Registry.EvaluateActiveSpeedMultiplier(Vector2.up), 1f, "No modifier follows hidden ship");
        }
        foreach (var mode in new[] { EnemyProjectileMovementPattern.Default, EnemyProjectileMovementPattern.AngularTurn, EnemyProjectileMovementPattern.Lateral, EnemyProjectileMovementPattern.Orbit })
        {
            using var modified = new Fixture(); modified.Configure(false, false); modified.Active.Activate(modified.Ship);
            using var normal = new EnemyProjectilePreviewWorld();
            var slow = modified.Spawn(new Vector3(0, 5), mode);
            var ordinary = modified.CreateRequest(new Vector3(0, 5), mode); normal.Spawn(new List<EnemyProjectileSpawnRequest> { ordinary });
            modified.Preview.Step(.1f, .1f, new Vector3(100, 100)); normal.Step(.02f, .02f, new Vector3(100, 100));
            using var query = normal.SimulationWorld.EntityManager.CreateEntityQuery(typeof(EnemyProjectileStaticData));
            using var entities = query.ToEntityArray(Allocator.Temp);
            var pose = normal.SimulationWorld.EntityManager.GetComponentData<LocalTransform>(entities[0]).Position;
            var expected = new Vector3(pose.x, pose.y, pose.z);
            Check(Vector3.Distance(modified.Position(slow), expected) < .002f, "Individual movement keeps path under time scaling: " + mode);
        }
        return new { passed = checks, tested = "regions, simultaneous passive, boundary, distance independence, new projectiles, cleanup/expiry/switch, recovery, both flags, authored movement, independent enemy attack clock" };
    }
    private static void PatternRotation(Enemy enemy, float expectedRate)
    {
        var executor = enemy.GetComponent<SimpleEnemyAttackExecutor>() ?? enemy.gameObject.AddComponent<SimpleEnemyAttackExecutor>();
        executor.BurstAttackSettings.SetFacing(EnemyFacingMode.Default);
        Call(executor, "Awake");
        Set(executor, "autonomousAttack", false);
        Set(executor, "patternRotationTime", 0f);
        Set(executor, "facingRotationTime", 0f);
        Quaternion physicalPose = enemy.transform.rotation;
        float dt = (float)executor.GetType().GetProperty("PatternRotationDeltaTime", Private).GetValue(executor);
        Near(dt, Time.deltaTime * expectedRate, "Pattern rotation follows effective fire rate");
        Call(executor, "Update");
        Near((float)executor.GetType().GetField("patternRotationTime", Private).GetValue(executor), dt, "Layout rotation advances by attack-scaled time");
        Near((float)executor.GetType().GetField("facingRotationTime", Private).GetValue(executor), dt, "Authored facing rotation advances by attack-scaled time");
        Call(executor, "LateUpdate");
        Near(Quaternion.Angle(physicalPose, enemy.transform.rotation), 0f, "Default physical facing is unchanged");
        executor.PauseRotation(); Call(executor, "Update");
        Near((float)executor.GetType().GetField("patternRotationTime", Private).GetValue(executor), dt, "Paused pattern rotation remains paused");
        var rotation = new EnemyRotationSettings();
        rotation.Configure(FourWayEnemyRotationDirection.CounterClockwise, FourWayEnemyRotationMode.ByAngle,
            90f, AnimationCurve.Linear(0, 0, 1, 1), 90f, 0f, true);
        executor.BurstAttackSettings.Shooting.ConfigureRotation(rotation, true);
        executor.BurstAttackSettings.Shooting.EmissionLayout.Configure(EnemyEmissionLayoutType.Radial, 1);
        executor.BurstAttackSettings.SetFacing(EnemyFacingMode.FollowPattern, 0, 90f);
        executor.GetType().GetMethod("RefreshRotation", Private).Invoke(executor, new object[] { true });
        Set(executor, "patternRotationTime", .5f);
        Call(executor, "LateUpdate");
        Near(Mathf.DeltaAngle(physicalPose.eulerAngles.z, enemy.transform.eulerAngles.z), Mathf.Min(45f, 90f * dt),
            "Follow Pattern visual turn uses attack-scaled angular speed");
        Call(executor, "OnDisable");
    }

    private static void Clock()
    {
        var clock = new EnemyAttackClock();
        Near(clock.Tick(10f, 1f), 10f, "Clock starts at ordinary battle time");
        Near(clock.Tick(11f, .2f), 10.2f, "Front slows pending attack time");
        Near(clock.Tick(12f, 2.5f), 12.7f, "Rear accelerates pending time immediately");
        Near(clock.Tick(13f, 1f), 13.7f, "Effect ends without lingering multiplier");
        Near(clock.Tick(13f, 2.5f), 13.7f, "Repeated reads do not compound clock");
    }
    private static void Defaults()
    {
        var value = new BlackHoleShipMetaContract();
        Near(value.ActiveDuration, 5f, "Default duration"); Near(value.FrontSpeedMultiplier, .2f, "Default front");
        Near(value.RearSpeedMultiplier, 2.5f, "Default rear"); Check(value.RewriteAttackSpeed && value.EnablePassiveAbility, "Default flags true");
        var copy = (BlackHoleShipMetaContract)value.Clone(); Near(copy.ActiveCooldown, value.ActiveCooldown, "Contract cloning includes cooldown");
        var restored = JsonUtility.FromJson<BlackHoleShipMetaContract>(JsonUtility.ToJson(value));
        Check(restored.RewriteAttackSpeed && restored.EnablePassiveAbility && restored.ActiveDuration == 5f, "Meta serialization preserves active parameters");
    }
    private static void LegacyAndBoss()
    {
        using var fixture = new Fixture(); fixture.Configure(false, true); fixture.Active.Activate(fixture.Ship);
        var legacyRoot = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefub/Enemy Projectiles/Bullet.prefab"));
        var bossRoot = new GameObject("Regional boss projectile verification");
        try
        {
            var legacy = legacyRoot.GetComponent<EnemyBullet>(); legacy.transform.position = new Vector3(0, .5f);
            typeof(EnemyProjectile).GetField("projectileCollisionRegistry", Private).SetValue(legacy, fixture.Registry);
            fixture.Passive.TryGetSlowField(out var field); float passive = field.EvaluateMultiplier(new float2(0, .5f));
            legacy.SetMultiplier(passive);
            var effective = typeof(EnemyProjectile).GetProperty("EffectiveSpeedMultiplier", Private);
            Near((float)effective.GetValue(legacy), passive * .2f, "Legacy projectile composes independent passive and active modifiers");
            fixture.Stop(); Near((float)effective.GetValue(legacy), passive, "Legacy passive multiplier survives active cleanup");
            fixture.Configure(false, false); fixture.Active.Activate(fixture.Ship);
            var boss = bossRoot.AddComponent<BossProjectile>(); Call(boss, "Awake");
            Set(boss, "projectileCollisionRegistry", fixture.Registry);
            boss.transform.position = new Vector3(0, 5);
            boss.Launch(null, new BossProjectileLaunchData(Vector2.down, null, default, 2f, 0f, 0f, 0f, 30f, 1f, Vector2.one, null));
            Call(boss, "FixedUpdate");
            Near(boss.transform.position.y, 5f - 2f * Time.fixedDeltaTime * .2f, "Pooled boss projectile front speed");
            boss.transform.position = new Vector3(0, -5); Call(boss, "FixedUpdate");
            Near(boss.transform.position.y, -5f - 2f * Time.fixedDeltaTime * 2.5f, "Pooled boss projectile rear speed after crossing");
            fixture.Stop(); boss.transform.position = new Vector3(0, 5); Call(boss, "FixedUpdate");
            Near(boss.transform.position.y, 5f - 2f * Time.fixedDeltaTime, "Pooled boss projectile restores ordinary speed");
        }
        finally { UnityEngine.Object.DestroyImmediate(legacyRoot); UnityEngine.Object.DestroyImmediate(bossRoot); }
    }
    private sealed class Fixture : IDisposable
    {
        public readonly EnemyProjectilePreviewWorld Preview = new();
        public readonly GameObject Root, Helper, CameraRoot;
        public readonly ParentShip Ship;
        public readonly BlackHoleActive Active;
        public readonly BlackHolePassive Passive;
        public readonly EnemyProjectileCollisionRegistry Registry;
        public readonly Entity RegistryEntity;
        private readonly EnemyBullet bullet;
        public Fixture()
        {
            Root = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/GameContent/Hulls/Gameplay/BlackHoleCraft.prefab"));
            Root.hideFlags = HideFlags.HideAndDontSave; Ship = Root.GetComponent<ParentShip>();
            Active = (BlackHoleActive)Ship.ActiveAbility; Passive = (BlackHolePassive)Ship.PassiveAbility;
            Passive.Init(Ship); Passive.ApplyShipMetaStats(Ship.ShipData.GetMetaRuntimeStats(0)); Ship.ShowShip();
            Helper = new GameObject("BlackHole regions verification"); Helper.hideFlags = HideFlags.HideAndDontSave;
            var controller = Helper.AddComponent<PlayerController>(); Set(controller, "currentShip", Ship);
            CameraRoot = new GameObject("BlackHole region test camera"); CameraRoot.hideFlags = HideFlags.HideAndDontSave;
            var camera = CameraRoot.AddComponent<Camera>(); camera.orthographic = true; camera.orthographicSize = 10f; camera.aspect = 1f; camera.transform.position = new Vector3(0, 0, -10);
            Set(controller, "gameplayCamera", camera);
            Registry = Helper.AddComponent<EnemyProjectileCollisionRegistry>(); Set(Registry, "playerController", controller);
            RegistryEntity = (Entity)typeof(EnemyProjectilePreviewWorld).GetField("registry", Private).GetValue(Preview);
            var manager = Preview.SimulationWorld.EntityManager;
            manager.AddBuffer<EnemyProjectileSlowField>(RegistryEntity); manager.AddBuffer<EnemyProjectilePurgeField>(RegistryEntity); manager.AddBuffer<EnemyProjectileResolutionEvent>(RegistryEntity);
            Set(Registry, "entityWorld", Preview.SimulationWorld); Set(Registry, "entityManager", manager); Set(Registry, "registryEntity", RegistryEntity);
            Set(Active, "projectileCollisionRegistry", Registry); Set(Active, "playerController", controller); Registry.RegisterShip(Ship);
            bullet = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefub/Enemy Projectiles/Bullet.prefab").GetComponent<EnemyBullet>();
        }
        public void Configure(bool rewrite, bool passive)
        {
            var contract = new BlackHoleShipMetaContract(); contract.ConfigureActive(5f, 5f, .2f, 2.5f, rewrite, passive);
            Active.ApplyShipMetaStats(new ShipMetaRuntimeStats(new ShipMetaBasicStats(), new ShipMetaContract[] { contract }));
        }
        public void Snapshot() => Call(Registry, "UpdateTargetSnapshot");
        public void Stop() => Call(Active, "StopActive");
        public EnemyProjectileSpawnRequest CreateRequest(Vector3 position, EnemyProjectileMovementPattern movement)
        {
            var shooting = new EnemyShootingSettings(); shooting.SetProjectileBaseSpeedOverride(true, 2f);
            Set(shooting, "overrideProjectileLifetime", true); Set(shooting, "projectileLifetime", 30f);
            Set(shooting, "projectileMovementPattern", movement); Set(shooting, "projectileAngularSpeed", 30f); Set(shooting, "projectileLateralSpeed", 1f);
            Set(shooting, "projectileOrbitRadius", 1f); shooting.Validate();
            var requests = new List<EnemyProjectileSpawnRequest>();
            EnemyAttackProjectileBuilder.AppendEmitter(requests, bullet, shooting, position, position, Vector3.down, 1f, 2f, 0f, new EnemyProjectilePrefabSet(bullet));
            return requests[0];
        }
        public Entity Spawn(Vector3 position, EnemyProjectileMovementPattern movement)
        {
            using var query = Preview.SimulationWorld.EntityManager.CreateEntityQuery(typeof(EnemyProjectileStaticData));
            Preview.Spawn(new List<EnemyProjectileSpawnRequest> { CreateRequest(position, movement) });
            using var entities = query.ToEntityArray(Allocator.Temp); return entities[entities.Length - 1];
        }
        public Vector3 Position(Entity entity)
        {
            var p = Preview.SimulationWorld.EntityManager.GetComponentData<LocalTransform>(entity).Position; return new Vector3(p.x, p.y, p.z);
        }
        public void Dispose()
        {
            UnityEngine.Object.DestroyImmediate(Root); UnityEngine.Object.DestroyImmediate(Helper); UnityEngine.Object.DestroyImmediate(CameraRoot); Preview.Dispose();
        }
    }
    private static void Set(object target, string name, object value) => target.GetType().GetField(name, Private).SetValue(target, value);
    private static void Call(object target, string name) => target.GetType().GetMethod(name, Private).Invoke(target, null);
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); checks++; }
    private static void Near(float value, float expected, string message) => Check(Mathf.Abs(value - expected) < .002f, message + ": " + value + " != " + expected);
}
