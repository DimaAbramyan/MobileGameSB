using System;
using System.Collections.Generic;
using System.Reflection;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEditor;
using UnityEngine;

public static class VerifyPrismEnergyWave
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static int checks;
    public static object Run()
    {
        if (EditorApplication.isPlaying || EditorApplication.isCompiling || EditorApplication.isUpdating)
            throw new Exception("Idle Editor required.");
        checks = 0;
        var visual = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/GameContent/Abilities/PrismEnergyWave.prefab");
        Check(visual != null, "Default visual prefab exists");
        var ownerObject = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefub/Ships/Prism.prefab"));
        var enemies = new List<GameObject>();
        var waveObjects = new List<GameObject>();
        var manager = new DealDamageManager();
        try
        {
            var owner = ownerObject.GetComponent<ParentShip>(); owner.Awake();
            owner.transform.position = new Vector3(1000, 1000);
            var ability = ownerObject.GetComponent<PrismBeamActiveAbility>();
            Set(ability, "dealDamageManager", manager);
            var config = new PrismShipMetaContract(); config.Configure(20f, 15f, 0f);
            config.ConfigureWave(visual, 1f, 4f, .3f, 2f, 6f, EnemyDamageType.Default, false);
            Set(ability, "metaContract", config);
            var first = EnemyAt(new Vector2(1000, 1002), enemies);
            var second = EnemyAt(new Vector2(1001.4f, 1004), enemies);
            var outside = EnemyAt(new Vector2(1003, 1004), enemies);
            int events = 0; manager.DamageApplied += _ => events++;
            Physics2D.SyncTransforms(); ability.TryActivate(owner);
            var wave = Waves(ability)[0]; waveObjects.Add(wave.gameObject);
            Near(ability.CooldownRemaining, 12f, "Standard cooldown starts immediately");
            Near(wave.transform.position.y, 1001f, "SpawnOffset is measured from ship center");
            Check(wave.transform.parent == null, "Wave is independent of ship transform");
            var box = wave.GetComponent<BoxCollider2D>();
            Near(box.size.x, 4f, "WaveWidth configures physical width");
            Near(box.size.y, .3f, "WaveThickness configures physical thickness");
            var renderer = wave.GetComponentInChildren<LineRenderer>();
            Check(renderer != null && renderer.sharedMaterial != null, "Visible wave renderer configured");
            Near(renderer.bounds.size.x, 4f, "Visual width matches wave");
            Near(renderer.bounds.size.y, .3f, "Visual thickness matches wave");
            ability.TryActivate(owner); Check(Waves(ability).Count == 1, "Cooldown blocks duplicate activation");
            wave.Step(1f);
            Near(wave.transform.position.y, 1003f, "Constant forward speed");
            Near(first._currentHealth, 980f, "Swept contact uses DealDamageManager");
            Check(events == 1, "Multiple enemy colliders produce one hit");
            owner.transform.position = new Vector3(1020, 1010); owner.transform.rotation = Quaternion.Euler(0, 0, 90);
            wave.Step(1f);
            Near(wave.transform.position.x, 1000f, "Owner movement does not move wave sideways");
            Near(wave.transform.position.y, 1005f, "Owner rotation does not redirect wave");
            Near(first._currentHealth, 980f, "Enemy is not damaged twice by one activation");
            Near(second._currentHealth, 980f, "Wave pierces and hits the next enemy");
            Near(outside._currentHealth, 1000f, "Enemy outside width is untouched");
            wave.Step(100f);
            Near(wave.TraveledDistance, 6f, "Final movement clamps to MaxDistance");
            Check(wave == null && wave.IsFinished, "Wave is destroyed at MaxDistance");
            owner.transform.position = new Vector3(1000, 1000); owner.transform.rotation = Quaternion.identity;
            ability.RestoreAllRecoveryResources(); ability.TryActivate(owner);
            var next = Waves(ability)[0]; waveObjects.Add(next.gameObject); next.Step(1f);
            Near(first._currentHealth, 960f, "A new activation has a fresh enemy hit set");
            Call(ability, "OnDisable"); Check(next == null, "Ability disable cleans up waves");
            var serializedMeta = owner.ShipData.GetMetaRuntimeStats(0);
            Check(serializedMeta.TryGetContract(out PrismShipMetaContract saved) && saved.WavePrefab == visual,
                "Existing PrismMeta references visual prefab");
            var copy = (PrismShipMetaContract)saved.Clone();
            Near(copy.PlasmaDamageBonusPercent, saved.PlasmaDamageBonusPercent, "Passive contract values preserved");
            Check(copy.DamageType == saved.DamageType && copy.WavePrefab == saved.WavePrefab, "Wave contract clone preserves settings");
            owner.GetComponent<PrismDamagePassiveAbility>().On();
            foreach (EnemyDamageType type in new[] { EnemyDamageType.Kinetick, EnemyDamageType.Plazma, EnemyDamageType.Default, EnemyDamageType.Chemical })
            {
                var target = EnemyAt(new Vector2(3000, 3000), enemies);
                Physics2D.SyncTransforms();
                var typedConfig = new PrismShipMetaContract(); typedConfig.Configure(20f, 15f, 0f);
                typedConfig.ConfigureWave(visual, 0f, 2f, .3f, 2f, 5f, type, false);
                var typedRoot = new GameObject("Typed wave"); typedRoot.transform.position = target.transform.position;
                waveObjects.Add(typedRoot);
                var typedWave = typedRoot.AddComponent<PrismEnergyWave>();
                typedWave.Initialize(owner, typedConfig, 2f, manager, null);
                float expectedDamage = 40f * EnemyDamageProfiles.Get(type).HullMultiplier
                    * (type == EnemyDamageType.Plazma ? 1.15f : 1f);
                Near(target._currentHealth, 1000f - expectedDamage, "DamageType, ability level and Prism passive compose: " + type);
                typedWave.Step(.01f);
                Near(target._currentHealth, 1000f - expectedDamage, "Spawn overlap is not damaged again: " + type);
                UnityEngine.Object.DestroyImmediate(typedRoot);
            }
            ProjectileRules(owner, visual, manager, waveObjects);
            return new { passed = checks, tested = "spawn/geometry/visual, constant independent movement, swept 2D damage, one hit per enemy, cooldown, distance cleanup, rotated ECS interception, policy and disabled interception" };
        }
        finally
        {
            foreach (var go in waveObjects) if (go != null) UnityEngine.Object.DestroyImmediate(go);
            foreach (var go in enemies) UnityEngine.Object.DestroyImmediate(go);
            UnityEngine.Object.DestroyImmediate(ownerObject);
        }
    }
    private static void ProjectileRules(ParentShip owner, GameObject visual, DealDamageManager manager, List<GameObject> objects)
    {
        using var world = new World("Prism wave interception verification");
        var helper = new GameObject("Prism registry verification");
        try
        {
            var registry = helper.AddComponent<EnemyProjectileCollisionRegistry>(); var em = world.EntityManager;
            var registryEntity = em.CreateEntity(typeof(EnemyProjectileCollisionRegistryTag));
            em.AddBuffer<EnemyProjectileCollisionTarget>(registryEntity); em.AddBuffer<EnemyProjectileSlowField>(registryEntity);
            em.AddBuffer<EnemyProjectilePurgeField>(registryEntity); em.AddBuffer<EnemyProjectileRegionSpeedField>(registryEntity);
            em.AddBuffer<EnemyProjectileResolutionEvent>(registryEntity); em.AddBuffer<EnemyProjectileBurstSpawnRequest>(registryEntity);
            Set(registry, "entityWorld", world); Set(registry, "entityManager", em); Set(registry, "registryEntity", registryEntity);
            var config = new PrismShipMetaContract(); config.Configure(0f, 15f, 0f);
            config.ConfigureWave(visual, 0f, 2f, .2f, 2f, 10f, EnemyDamageType.Default, true);
            var root = new GameObject("Rotated wave"); objects.Add(root);
            root.transform.position = new Vector3(-1000, -1000); root.transform.rotation = Quaternion.Euler(0, 0, 45);
            var wave = root.AddComponent<PrismEnergyWave>(); wave.Initialize(owner, config, 1f, manager, registry);
            Snapshot(registry);
            var targets = em.GetBuffer<EnemyProjectileCollisionTarget>(registryEntity);
            Check(targets.Length == 1 && targets[0].IsBox == 1, "Registry uses oriented moving box");
            var system = world.GetOrCreateSystem<ProjectileInterceptorSystem>();
            float2 center = new float2(-1000, -1000);
            float2 right = new float2(Mathf.Sqrt(.5f), Mathf.Sqrt(.5f));
            float2 up = new float2(-right.y, right.x);
            Projectile(em, world, system, center, center, EnemyProjectileCharacteristic.Fragile, true, "Fragile intercepted");
            Projectile(em, world, system, center, center, EnemyProjectileCharacteristic.Normal, false, "Normal survives default policy");
            Projectile(em, world, system, center + new float2(.75f, -.75f), center + new float2(.75f, -.75f), EnemyProjectileCharacteristic.Fragile, false, "Rotated AABB corner is not intercepted");
            Projectile(em, world, system, center + right * .65f, center + right * .65f, EnemyProjectileCharacteristic.Fragile, true, "Rotated width is respected");
            wave.Step(1f); Snapshot(registry);
            float2 between = center + up;
            Projectile(em, world, system, between, between, EnemyProjectileCharacteristic.Fragile, true, "Wave sweeps through stationary projectile");
            UnityEngine.Object.DestroyImmediate(root); Snapshot(registry);
            Check(em.GetBuffer<EnemyProjectileCollisionTarget>(registryEntity).Length == 0, "Destroyed wave unregisters interceptor");
            config.ConfigureWave(visual, 0f, 2f, .2f, 2f, 10f, EnemyDamageType.Default, true, EnemyProjectileDestructionPolicy.NormalAndFragile);
            root = new GameObject("Normal policy wave"); root.transform.position = new Vector3(-1000, -1000); objects.Add(root);
            wave = root.AddComponent<PrismEnergyWave>(); wave.Initialize(owner, config, 1f, manager, registry); Snapshot(registry);
            Projectile(em, world, system, center, center, EnemyProjectileCharacteristic.Normal, true, "Configured policy permits Normal");
            UnityEngine.Object.DestroyImmediate(root); Snapshot(registry);
            config.ConfigureWave(visual, 0f, 2f, .2f, 2f, 10f, EnemyDamageType.Default, false);
            root = new GameObject("Non-intercepting wave"); root.transform.position = new Vector3(-1000, -1000); objects.Add(root);
            wave = root.AddComponent<PrismEnergyWave>(); wave.Initialize(owner, config, 1f, manager, registry); Snapshot(registry);
            Check(em.GetBuffer<EnemyProjectileCollisionTarget>(registryEntity).Length == 0, "DestroyProjectiles false does not register");
            var legacyRoot = new GameObject("Legacy projectile probe"); objects.Add(legacyRoot);
            var legacyProjectile = legacyRoot.AddComponent<EnemyBullet>();
            Check(!wave.TryDestroyEnemyProjectile(legacyProjectile) && legacyRoot.activeSelf,
                "DestroyProjectiles false also rejects native projectile interception");
        }
        finally { UnityEngine.Object.DestroyImmediate(helper); }
    }
    private static void Projectile(EntityManager em, World world, SystemHandle system, float2 from, float2 to, EnemyProjectileCharacteristic type, bool hit, string name)
    {
        var entity = em.CreateEntity(typeof(EnemyProjectileResolution), typeof(EnemyProjectilePreviousPosition), typeof(EnemyProjectileStaticData), typeof(LocalTransform));
        em.SetComponentData(entity, new EnemyProjectilePreviousPosition { Value = from });
        em.SetComponentData(entity, new EnemyProjectileStaticData { Radius = .01f, Characteristic = type });
        em.SetComponentData(entity, LocalTransform.FromPosition(new float3(to, 0f))); system.Update(world.Unmanaged);
        Check((em.GetComponentData<EnemyProjectileResolution>(entity).Kind == EnemyProjectileResolutionKind.Intercepted) == hit, name);
        em.DestroyEntity(entity);
    }
    private static Enemy EnemyAt(Vector2 position, List<GameObject> objects)
    {
        var go = new GameObject("Wave target"); objects.Add(go); go.transform.position = position;
        var enemy = go.AddComponent<Enemy>(); Set(enemy, "_maxHealth", 1000f); enemy._currentHealth = 1000f;
        var box = go.AddComponent<BoxCollider2D>(); box.size = Vector2.one * .2f;
        var child = new GameObject("Second enemy collider"); child.transform.SetParent(go.transform, false);
        child.AddComponent<CircleCollider2D>().radius = .1f; return enemy;
    }
    private static List<PrismEnergyWave> Waves(PrismBeamActiveAbility ability) => (List<PrismEnergyWave>)Get(ability, "waves");
    private static void Snapshot(EnemyProjectileCollisionRegistry registry) => Call(registry, "UpdateTargetSnapshot");
    private static object Get(object target, string name) => target.GetType().GetField(name, Private).GetValue(target);
    private static void Set(object target, string name, object value) => target.GetType().GetField(name, Private).SetValue(target, value);
    private static void Call(object target, string name) => target.GetType().GetMethod(name, Private).Invoke(target, null);
    private static void Near(float a, float b, string name) => Check(Mathf.Abs(a - b) < .005f, name + ": " + a + " != " + b);
    private static void Check(bool ok, string name) { if (!ok) throw new Exception(name); checks++; }
}
