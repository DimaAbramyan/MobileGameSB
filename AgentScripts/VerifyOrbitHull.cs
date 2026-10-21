using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Unity.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEditor;
using UnityEngine;

public static class VerifyOrbitHull
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static int checks;
    public static object RunChargeUp()
    {
        if (EditorApplication.isPlaying || EditorApplication.isCompiling || EditorApplication.isUpdating)
            throw new Exception("Editor must be idle");
        checks = 0;
        var root = new GameObject("Orbit charge up verification"); root.SetActive(false);
        OrbitPassiveAbility passive = null;
        try
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/GameContent/Hulls/Gameplay/OrbitCraft.prefab");
            Check(prefab != null && prefab.GetComponent<OrbitPassiveAbility>() != null
                && prefab.GetComponent<OrbitExpansionActiveAbility>() != null, "Orbit prefab ability wiring exists");
            var clone = UnityEngine.Object.Instantiate(prefab, root.transform);
            var ship = clone.GetComponent<ParentShip>();
            passive = clone.GetComponent<OrbitPassiveAbility>();
            var active = clone.GetComponent<OrbitExpansionActiveAbility>();
            ship.Awake(); Invoke(active, "Awake"); passive.Init(ship); ship.ShowShip();
            var isolated = (OrbitShipMetaContract)((OrbitShipMetaContract)Get(passive, "settings")).Clone();
            Set(passive, "settings", isolated);
            float reserve = active.ToggleTimeRemaining;
            float maximum = active.ToggleMaximumTime;
            Set(isolated, "expansionSpeed", 0.01f);
            foreach (int frames in new[] { 20, 100 })
            {
                float duration = frames * Time.fixedDeltaTime;
                Set(isolated, "chargeUpDuration", duration);
                Check(((OrbitShipMetaContract)isolated.Clone()).ChargeUpDuration == duration, "Level clones retain charge duration");
                Set(passive, "intensity", 0f);
                Check(passive.BeginHold(), "Hold begins");
                for (int i = 0; i < frames / 2; i++) Invoke(passive, "FixedUpdate");
                Check(Mathf.Abs(passive.Intensity - 0.5f) < 0.0001f, "Half charge at half duration");
                for (int i = frames / 2; i < frames; i++) Invoke(passive, "FixedUpdate");
                Check(Mathf.Abs(passive.Intensity - 1f) < 0.0001f, "Full charge at configured duration regardless of reserve/old expansion speed");
                Check(Mathf.Abs(passive.CurrentAngularSpeed - isolated.MaximumAngularSpeed) < 0.001f
                    && Mathf.Abs(passive.CurrentOrbitRadius - isolated.MaximumOrbitRadius) < 0.001f,
                    "Maximum speed and orbit reached together");
                passive.EndHold();
                Invoke(passive, "FixedUpdate");
                float resumed = passive.Intensity;
                Check(Mathf.Abs(resumed - (1f - isolated.CollapseSpeed * Time.fixedDeltaTime)) < 0.0001f,
                    "Release retains configured collapse speed");
                passive.BeginHold();
                Check(passive.Intensity == resumed, "Resuming does not reset charge");
                Invoke(passive, "FixedUpdate");
                Check(passive.Intensity > resumed, "Resuming continues from current intensity");
            }
            Check(active.ToggleTimeRemaining == reserve && active.ToggleMaximumTime == maximum,
                "Charge duration does not change ability reserve");
            Set(isolated, "chargeUpDuration", 0f);
            Check(isolated.ChargeUpDuration == 0.01f, "Duration cannot divide by zero");
        }
        finally
        {
            if (passive != null && Get(passive, "satellites") is OrbitSatellite[] satellites)
            {
                foreach (var satellite in satellites) if (satellite != null) UnityEngine.Object.DestroyImmediate(satellite.gameObject);
                Set(passive, "satellites", null);
            }
            UnityEngine.Object.DestroyImmediate(root);
        }
        return new { passed = checks };
    }

    public static object SaveChargeUpDuration()
    {
        if (EditorApplication.isPlaying || EditorApplication.isCompiling || EditorApplication.isUpdating)
            throw new Exception("Editor must be idle");
        var asset = AssetDatabase.LoadAssetAtPath<ShipMetaConfig>("Assets/Prefub/Data/ShipMeta/OrbitMeta.asset");
        var serialized = new SerializedObject(asset);
        var field = serialized.GetIterator();
        int configured = 0;
        Undo.RecordObject(asset, "Configure Orbit charge up duration");
        while (field.Next(true))
            if (field.name == "chargeUpDuration" && field.propertyType == SerializedPropertyType.Float)
            {
                field.floatValue = 1f;
                configured++;
            }
        if (configured == 0) throw new Exception("Orbit charge duration field not found");
        serialized.ApplyModifiedProperties();
        EditorUtility.SetDirty(asset);
        AssetDatabase.SaveAssets();
        asset.GetLevel(0).TryGetContract(out OrbitShipMetaContract orbit);
        return new { configured, seconds = orbit.ChargeUpDuration };
    }

    public static object RunEcsCooldown()
    {
        if (EditorApplication.isPlaying || EditorApplication.isCompiling || EditorApplication.isUpdating)
            throw new Exception("Editor must be idle");
        checks = 0;
        var savedWorld = World.DefaultGameObjectInjectionWorld;
        var world = new World("Orbit cooldown ECS verification", WorldFlags.Game);
        var root = new GameObject("Orbit cooldown ECS verification"); root.SetActive(false);
        OrbitPassiveAbility passive = null;
        try
        {
            World.DefaultGameObjectInjectionWorld = world;
            root.AddComponent<Rigidbody2D>();
            var controller = root.AddComponent<PlayerController>();
            var registry = root.AddComponent<EnemyProjectileCollisionRegistry>();
            Set(registry, "playerController", controller);
            var clone = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/GameContent/Hulls/Gameplay/OrbitCraft.prefab"), root.transform);
            var ship = clone.GetComponent<ParentShip>();
            passive = clone.GetComponent<OrbitPassiveAbility>();
            var active = clone.GetComponent<OrbitExpansionActiveAbility>();
            Set(passive, "projectileRegistry", registry);
            ship.Awake(); Invoke(active, "Awake"); passive.Init(ship);
            Set(controller, "currentShip", ship); ship.ShowShip();
            var settings = (OrbitShipMetaContract)((OrbitShipMetaContract)Get(passive, "settings")).Clone();
            Set(settings, "cooldownReductionPerProjectile", 0.2f);
            Set(passive, "settings", settings);
            var satellites = (OrbitSatellite[])Get(passive, "satellites");
            foreach(var satellite in satellites) registry.RegisterInterceptor(satellite);
            Invoke(registry, "EnsureRegistryEntity");
            var registryEntity = (Entity)Get(registry, "registryEntity");
            var em = world.EntityManager;
            world.SetTime(new TimeData(1f, 0.02f));
            Invoke(registry, "UpdateTargetSnapshot");
            var target = em.GetBuffer<EnemyProjectileCollisionTarget>(registryEntity)[0];
            var interceptor = world.GetOrCreateSystem<ProjectileInterceptorSystem>();
            var cleanup = world.GetOrCreateSystem<ProjectileCleanupSystem>();
            var end = world.GetOrCreateSystemManaged<EndFixedStepSimulationEntityCommandBufferSystem>();
            Set(active, "cooldownTimer", 2f);
            for(int i = 0; i < 2; i++)
            {
                var shot = Shot(em, target.Center - new float2(1, 0), target.Center + new float2(1, 0));
                interceptor.Update(world.Unmanaged);
                Check(em.GetComponentData<EnemyProjectileResolution>(shot).Kind == EnemyProjectileResolutionKind.Intercepted,
                    "Actual ECS projectile intercepted");
                interceptor.Update(world.Unmanaged);
                cleanup.Update(world.Unmanaged); end.Update();
                Check(!em.Exists(shot), "Intercepted projectile actually destroyed");
                Invoke(registry, "ResolveEvents");
                float expected = 2f - (i + 1) * 0.2f;
                Check(Mathf.Abs(active.CooldownRemaining - expected) < 0.0001f, "Each destroyed projectile rewards once");
                Invoke(registry, "ResolveEvents");
                Check(Mathf.Abs(active.CooldownRemaining - expected) < 0.0001f, "Second event drain gives no duplicate reward");
            }
            ship.HideShip();
            satellites[0].NotifyEnemyProjectileIntercepted();
            Check(Mathf.Abs(active.CooldownRemaining - 1.6f) < 0.0001f, "Hidden Orbit cannot receive queued reward");
        }
        finally
        {
            if (passive != null && Get(passive, "satellites") is OrbitSatellite[] satellites)
            {
                foreach(var satellite in satellites) if(satellite != null) UnityEngine.Object.DestroyImmediate(satellite.gameObject);
                Set(passive, "satellites", null);
            }
            UnityEngine.Object.DestroyImmediate(root);
            World.DefaultGameObjectInjectionWorld = savedWorld;
            world.Dispose();
        }
        return new { passed = checks, actualEcsProjectiles = 2 };
    }

    public static object RunCooldown()
    {
        if (EditorApplication.isPlaying || EditorApplication.isCompiling || EditorApplication.isUpdating)
            throw new Exception("Editor must be idle");
        checks = 0;
        var root = new GameObject("Orbit cooldown verification"); root.SetActive(false);
        OrbitPassiveAbility passive = null;
        try
        {
            var clone = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/GameContent/Hulls/Gameplay/OrbitCraft.prefab"), root.transform);
            var ship = clone.GetComponent<ParentShip>();
            passive = clone.GetComponent<OrbitPassiveAbility>();
            var active = clone.GetComponent<OrbitExpansionActiveAbility>();
            ship.Awake(); Invoke(active, "Awake"); passive.Init(ship); ship.ShowShip();
            var isolated = (OrbitShipMetaContract)((OrbitShipMetaContract)Get(passive, "settings")).Clone();
            Set(passive, "settings", isolated);
            Check(Mathf.Approximately(new OrbitShipMetaContract().CooldownReductionPerProjectile, 0.2f), "Default reduction is 0.2 seconds");
            Set(isolated, "cooldownReductionPerProjectile", 0.2f);
            var satellites = (OrbitSatellite[])Get(passive, "satellites");
            Set(active, "cooldownTimer", 0f);
            Set(active, "toggleTimeRemaining", 1f);
            float resourcePerSecond = active.ToggleMaximumTime / active.ExportMetaRecoverySettings().ToggleRechargeDuration;
            satellites[0].NotifyEnemyProjectileIntercepted();
            Check(Mathf.Abs(active.ToggleTimeRemaining - (1f + 0.2f * resourcePerSecond)) < 0.0001f,
                "Interception advances Toggle reserve even when cooldown timer is zero");
            Set(active, "toggleIsActive", true);
            float beforeHeldReward = active.ToggleTimeRemaining;
            satellites[0].NotifyEnemyProjectileIntercepted();
            Check(Mathf.Abs(active.ToggleTimeRemaining - (beforeHeldReward + 0.2f * resourcePerSecond)) < 0.0001f,
                "Interception also restores reserve while ability is held");
            Set(active, "toggleIsActive", false);
            Set(active, "toggleTimeRemaining", active.ToggleMaximumTime - 0.01f);
            satellites[0].NotifyEnemyProjectileIntercepted();
            Check(active.ToggleTimeRemaining == active.ToggleMaximumTime, "Reserve clamps at maximum");
            Set(active, "cooldownTimer", 1f);
            satellites[0].NotifyEnemyProjectileIntercepted();
            Check(Mathf.Abs(active.CooldownRemaining - 0.8f) < 0.0001f, "One interception removes 0.2 seconds");
            satellites[1].NotifyEnemyProjectileIntercepted();
            Check(Mathf.Abs(active.CooldownRemaining - 0.6f) < 0.0001f, "Another satellite rewards its own interception");
            Set(isolated, "cooldownReductionPerProjectile", 0.35f);
            satellites[0].NotifyEnemyProjectileIntercepted();
            Check(Mathf.Abs(active.CooldownRemaining - 0.25f) < 0.0001f, "Configured m applies");
            satellites[0].NotifyEnemyProjectileIntercepted();
            Check(active.CooldownRemaining == 0f, "Reduction clamps at zero");
            satellites[0].NotifyEnemyProjectileIntercepted();
            Check(active.CooldownRemaining == 0f, "Ready ability cannot bank reduction");
            Set(active, "cooldownTimer", 1f);
            Set(isolated, "cooldownReductionPerProjectile", 0f);
            satellites[0].NotifyEnemyProjectileIntercepted();
            Check(active.CooldownRemaining == 1f, "Zero m disables reward");
            Set(isolated, "cooldownReductionPerProjectile", 0.2f);
            float duration = active.CooldownDuration;
            var shotRoot = new GameObject("Inactive legacy shot"); shotRoot.SetActive(false);
            shotRoot.transform.SetParent(root.transform);
            var shot = shotRoot.AddComponent<EnemyBullet>();
            Check(!satellites[0].TryDestroyEnemyProjectile(shot) && active.CooldownRemaining == 1f,
                "Already consumed/inactive physical shot cannot reward twice");
            ship.HideShip();
            satellites[0].NotifyEnemyProjectileIntercepted();
            Check(active.CooldownRemaining == 1f, "Hidden ship receives no reward");
            ship.ShowShip();
            satellites[0].NotifyEnemyProjectileIntercepted();
            Check(Mathf.Abs(active.CooldownRemaining - 0.8f) < 0.0001f, "Switching back restores own passive");
            Check(active.CooldownDuration == duration, "Configured cooldown duration stays unchanged");
        }
        finally
        {
            if (passive != null && Get(passive, "satellites") is OrbitSatellite[] satellites)
            {
                foreach(var satellite in satellites) if(satellite != null) UnityEngine.Object.DestroyImmediate(satellite.gameObject);
                Set(passive, "satellites", null);
            }
            UnityEngine.Object.DestroyImmediate(root);
        }
        return new { passed = checks };
    }

    public static object RunDirections()
    {
        if (EditorApplication.isPlaying || EditorApplication.isCompiling || EditorApplication.isUpdating)
            throw new Exception("Editor must be idle");
        checks = 0;
        var root = new GameObject("Orbit direction verification"); root.SetActive(false);
        OrbitPassiveAbility passive = null;
        try
        {
            var clone = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/GameContent/Hulls/Gameplay/OrbitCraft.prefab"), root.transform);
            var ship = clone.GetComponent<ParentShip>();
            passive = clone.GetComponent<OrbitPassiveAbility>();
            ship.Awake(); passive.Init(ship);
            var original = (OrbitShipMetaContract)Get(passive, "settings");
            var isolated = (OrbitShipMetaContract)original.Clone();
            bool originalDirection = original.Clockwise;
            Set(isolated, "baseAngularSpeed", 90f);
            Set(isolated, "maximumAngularSpeed", 270f);
            Set(passive, "settings", isolated);
            ship.ShowShip();
            var satellites = (OrbitSatellite[])Get(passive, "satellites");
            foreach(bool clockwise in new[] { false, true })
            {
                Set(isolated, "clockwise", clockwise);
                Check(((OrbitShipMetaContract)isolated.Clone()).Clockwise == clockwise,
                    "Meta level copies retain the direction");
                foreach(int phase in new[] { 0, 1, 2 })
                {
                    Set(passive, "angle", 0f);
                    if(phase == 1) passive.BeginHold(); else passive.EndHold();
                    Set(passive, "intensity", phase == 2 ? 1f : 0f);
                    Invoke(passive, "PositionSatellites");
                    Vector2 before = satellites[0].transform.position - ship.transform.position;
                    Invoke(passive, "FixedUpdate");
                    Vector2 after = satellites[0].transform.position - ship.transform.position;
                    float turn = Vector2.SignedAngle(before, after);
                    Check(clockwise ? turn < 0f : turn > 0f,
                        "Clockwise=" + clockwise + ", normal/hold/collapse phase=" + phase);
                    float expectedRadius = isolated.EvaluateObjectRadius(passive.Intensity);
                    var satellite = satellites[0];
                    Check(Mathf.Abs(satellite.GetComponent<CircleCollider2D>().radius - expectedRadius) < 0.00001f,
                        "Physical radius follows charge in phase " + phase);
                    var visual = satellite.GetComponentInChildren<SpriteRenderer>();
                    Check(Mathf.Abs(visual.sprite.bounds.size.x * visual.transform.localScale.x * 0.5f - expectedRadius) < 0.00001f,
                        "Visual radius agrees with hitbox in phase " + phase);
                    satellite.TryGetCircularSweep(out var previousCenter, out var center, out float snapshotRadius);
                    Check(Mathf.Abs(snapshotRadius - expectedRadius) < 0.00001f,
                        "ECS receives changing physical radius in phase " + phase);
                    if(phase == 1) Check(expectedRadius > isolated.ObjectRadius, "Objects grow when ability accelerates");
                    if(phase == 2) Check(expectedRadius > isolated.ObjectRadius && expectedRadius < isolated.MaximumChargedObjectRadius,
                        "Objects shrink gradually after release");
                }
            }
            Check(original.Clockwise == originalDirection, "Designer asset values preserved");
            VerifyGapLimit();
        }
        finally
        {
            if(passive != null && Get(passive, "satellites") is OrbitSatellite[] satellites)
            {
                foreach(var satellite in satellites) if(satellite != null) UnityEngine.Object.DestroyImmediate(satellite.gameObject);
                Set(passive, "satellites", null);
            }
            UnityEngine.Object.DestroyImmediate(root);
        }
        return new { passed = checks };
    }

    public static object Run()
    {
        if (EditorApplication.isPlaying || EditorApplication.isCompiling || EditorApplication.isUpdating)
            throw new Exception("Editor must be idle");
        checks = 0;
        VerifyAssets();
        var savedWorld = World.DefaultGameObjectInjectionWorld;
        var world = new World("Orbit verification", WorldFlags.Game);
        var root = new GameObject("Orbit verification");
        root.SetActive(false);
        OrbitPassiveAbility passive = null;
        try
        {
            World.DefaultGameObjectInjectionWorld = world;
            root.AddComponent<Rigidbody2D>();
            var controller = root.AddComponent<PlayerController>();
            var registry = root.AddComponent<EnemyProjectileCollisionRegistry>();
            Set(registry, "playerController", controller);
            var clone = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/GameContent/Hulls/Gameplay/OrbitCraft.prefab"), root.transform);
            clone.transform.position = new Vector3(1000f, 1000f, 0f);
            var ship = clone.GetComponent<ParentShip>();
            passive = clone.GetComponent<OrbitPassiveAbility>();
            var active = clone.GetComponent<OrbitExpansionActiveAbility>();
            Set(passive, "projectileRegistry", registry);
            ship.Awake(); Invoke(active, "Awake"); passive.Init(ship);
            Set(controller, "currentShip", ship);
            ship.ShowShip();
            var satellites = (OrbitSatellite[])Get(passive, "satellites");
            Check(satellites.Length == 3, "Three satellites in ordinary state");
            float baseRadius = passive.CurrentOrbitRadius;
            float baseSpeed = passive.CurrentAngularSpeed;
            var directionSettings = (OrbitShipMetaContract)((OrbitShipMetaContract)Get(passive, "settings")).Clone();
            Set(passive, "settings", directionSettings);
            foreach (bool clockwise in new[] { false, true })
            {
                Set(directionSettings, "clockwise", clockwise);
                foreach (bool held in new[] { false, true })
                {
                    Set(passive, "angle", 0f);
                    Invoke(passive, "PositionSatellites");
                    if (held) passive.BeginHold(); else passive.EndHold();
                    Vector2 before = satellites[0].transform.position - clone.transform.position;
                    Invoke(passive, "FixedUpdate");
                    Vector2 after = satellites[0].transform.position - clone.transform.position;
                    float turn = Vector2.SignedAngle(before, after);
                    Check(clockwise ? turn < 0f : turn > 0f,
                        "Actual satellite direction matches Clockwise=" + clockwise + ", held=" + held);
                }
            }
            Set(directionSettings, "clockwise", false);
            passive.EndHold(); Set(passive, "angle", 0f); Set(passive, "intensity", 0f);
            Invoke(passive, "PositionSatellites");
            foreach (var satellite in satellites)
            {
                Check(satellite.GetComponentInChildren<ParentShip>() == null, "No duplicated ParentShip on satellite");
                Check(Mathf.Abs(Vector2.Distance(satellite.transform.position, clone.transform.position) - baseRadius) < 0.0001f,
                    "Satellite positioned at base radius");
                Check(Mathf.Abs(satellite.GetComponent<CircleCollider2D>().radius - 0.18f) < 0.0001f
                    && satellite.transform.localScale == Vector3.one, "Exact physical object radius independent of visual scale");
                registry.RegisterInterceptor(satellite);
            }
            Invoke(registry, "EnsureRegistryEntity");
            Entity registryEntity = (Entity)Get(registry, "registryEntity");
            var em = world.EntityManager;
            world.SetTime(new TimeData(1f, 0.02f));
            Invoke(registry, "UpdateTargetSnapshot");
            var targets = em.GetBuffer<EnemyProjectileCollisionTarget>(registryEntity);
            Check(targets.Length == 3, "Registry publishes individual circular targets");
            for (int i = 0; i < targets.Length; i++)
                Check(targets[i].IsCircle == 1 && Mathf.Abs(targets[i].Radius - 0.18f) < 0.0001f,
                    "ECS collision snapshot uses physical circles");

            var firstTarget = targets[0];
            var interceptor = world.GetOrCreateSystem<ProjectileInterceptorSystem>();
            var collision = world.GetOrCreateSystem<ProjectilePlayerCollisionSystem>();
            var cleanup = world.GetOrCreateSystem<ProjectileCleanupSystem>();
            var end = world.GetOrCreateSystemManaged<EndFixedStepSimulationEntityCommandBufferSystem>();
            Entity hit = Shot(em, firstTarget.Center + new float2(-1f, 0f), firstTarget.Center + new float2(1f, 0f));
            interceptor.Update(world.Unmanaged);
            Check(em.GetComponentData<EnemyProjectileResolution>(hit).Kind == EnemyProjectileResolutionKind.Intercepted,
                "Real ECS interceptor catches projectile crossing satellite");
            cleanup.Update(world.Unmanaged); end.Update();
            Check(!em.Exists(hit), "Intercepted ECS entity destroyed by existing cleanup pipeline");
            Set(active, "cooldownTimer", 2f);
            Invoke(registry, "ResolveEvents");
            Check(Mathf.Abs(active.CooldownRemaining - 1.8f) < 0.0001f,
                "Actual ECS interception reduces Orbit cooldown by 0.2 seconds");
            Invoke(registry, "ResolveEvents");
            Check(Mathf.Abs(active.CooldownRemaining - 1.8f) < 0.0001f,
                "Consumed ECS resolution event cannot reward twice");
            Set(active, "cooldownTimer", 0f);

            Entity gap = Shot(em, new float2(1000f, 1000f), new float2(1000f, 1000.7f));
            interceptor.Update(world.Unmanaged);
            Check(em.GetComponentData<EnemyProjectileResolution>(gap).Kind == EnemyProjectileResolutionKind.None,
                "Projectile passes through angular gap between satellites");
            em.DestroyEntity(gap);
            float2 corner = firstTarget.Center + new float2(0.17f, 0.17f);
            Entity cornerShot = Shot(em, corner, corner);
            interceptor.Update(world.Unmanaged);
            Check(em.GetComponentData<EnemyProjectileResolution>(cornerShot).Kind == EnemyProjectileResolutionKind.None,
                "AABB corner outside circular hitbox does not intercept");
            em.DestroyEntity(cornerShot);

            var sameTarget = firstTarget;
            targets = em.GetBuffer<EnemyProjectileCollisionTarget>(registryEntity);
            targets.Add(new EnemyProjectileCollisionTarget { TargetId = 999,
                Kind = EnemyProjectileCollisionTargetKind.PlayerShip,
                Min = sameTarget.Center - 0.2f, Max = sameTarget.Center + 0.2f });
            Entity priority = Shot(em, sameTarget.Center, sameTarget.Center);
            interceptor.Update(world.Unmanaged); collision.Update(world.Unmanaged);
            Check(em.GetComponentData<EnemyProjectileResolution>(priority).Kind == EnemyProjectileResolutionKind.Intercepted,
                "Interception wins over player hit, as for Arkanoid");
            em.DestroyEntity(priority);

            active.TryActivate(ship);
            Check(active.IsToggleActive && passive.IsHeld, "Shared Toggle system starts Orbit hold");
            for (int i = 0; i < 75; i++) Invoke(passive, "FixedUpdate");
            Check(passive.CurrentOrbitRadius > baseRadius && passive.CurrentAngularSpeed > baseSpeed,
                "Holding progressively increases radius and rotation speed");
            Check(passive.Intensity > 0.45f && passive.Intensity < 0.55f,
                "Half of maximum hold time produces intermediate intensity");
            for (int i = 0; i < 100; i++) Invoke(passive, "FixedUpdate");
            Check(passive.Intensity > 0.999f, "Full hold reaches configured maxima");
            Invoke(registry, "UpdateTargetSnapshot");
            Check(em.GetBuffer<EnemyProjectileCollisionTarget>(registryEntity).Length == 3,
                "Satellites continue intercepting at expanded radius");
            active.TryRelease(ship);
            Check(!active.IsToggleActive && !passive.IsHeld && passive.Intensity > 0.99f,
                "Release ends hold without teleporting satellites to base orbit");
            Invoke(passive, "FixedUpdate");
            Check(passive.Intensity > 0f && passive.Intensity < 1f,
                "Collapse begins smoothly");
            Invoke(registry, "UpdateTargetSnapshot");
            Check(em.GetBuffer<EnemyProjectileCollisionTarget>(registryEntity).Length == 3,
                "Interception remains active during collapse");
            for (int i = 0; i < 70; i++) Invoke(passive, "FixedUpdate");
            Check(passive.Intensity == 0f && Mathf.Abs(passive.CurrentOrbitRadius - baseRadius) < 0.0001f
                && Mathf.Abs(passive.CurrentAngularSpeed - baseSpeed) < 0.0001f,
                "Release returns radius and angular speed to ordinary values");
            active.RestoreAllRecoveryResources(); active.TryActivate(ship);
            Set(active, "toggleTimeRemaining", 0f); Invoke(active, "Update");
            Check(!active.IsToggleActive && !passive.IsHeld, "Resource exhaustion invokes shared release/collapse");

            var enemyObject = new GameObject("Contact enemy"); enemyObject.transform.SetParent(root.transform);
            var enemy = enemyObject.AddComponent<Enemy>(); enemy._currentHealth = 100f;
            float hp = enemy._currentHealth;
            float notified = 0f; ship.OnDamageDealt += value => notified += value;
            Invoke(passive, "TryDamageEnemy", enemy);
            Check(Mathf.Abs(enemy._currentHealth - (hp - 2f)) < 0.0001f && notified == 2f,
                "Contact damage reaches enemy and owning hull damage notification");
            Invoke(passive, "TryDamageEnemy", enemy); Invoke(passive, "TryDamageEnemy", enemy);
            Check(enemy._currentHealth == hp - 2f, "Three satellite contacts cannot bypass shared damage interval");
            var timers = (IDictionary)Get(passive, "nextEnemyHitTimes");
            timers[enemy] = Time.time - 0.01f;
            Invoke(passive, "TryDamageEnemy", enemy);
            Check(enemy._currentHealth == hp - 4f, "Contact damages again only when interval expires");
            passive.ApplyBattleAbilityDamageMultiplier(2f); timers[enemy] = Time.time - 0.01f;
            Invoke(passive, "TryDamageEnemy", enemy);
            Check(enemy._currentHealth == hp - 8f, "Existing ability damage progression applies to Orbit");
            clone.transform.position += new Vector3(2f, -3f, 0f);
            Invoke(passive, "FixedUpdate");
            Check(Mathf.Abs(Vector2.Distance(satellites[0].transform.position, clone.transform.position) - baseRadius) < 0.0002f,
                "Orbit follows direct player movement");

            ship.HideShip();
            foreach (var satellite in satellites)
            {
                Check(!satellite.gameObject.activeSelf, "Hiding owning hull hides satellite");
                registry.UnregisterInterceptor(satellite);
            }
            Invoke(registry, "UpdateTargetSnapshot");
            Check(em.GetBuffer<EnemyProjectileCollisionTarget>(registryEntity).Length == 0,
                "Hidden Orbit cannot intercept for another selected hull");
            ship.ShowShip();
            Check(ReferenceEquals(satellites, Get(passive, "satellites")), "Switching reuses satellites instead of allocating replacements");
            Check(!passive.IsHeld && passive.Intensity == 0f, "Switching back starts normal orbit with no stuck hold");
            VerifySweepMath();
            VerifyGapLimit();
            VerifyArkanoid(world, em, registry, registryEntity);
        }
        finally
        {
            if (passive != null && Get(passive, "satellites") is OrbitSatellite[] satellites)
            {
                foreach(var satellite in satellites) if (satellite != null) UnityEngine.Object.DestroyImmediate(satellite.gameObject);
                Set(passive, "satellites", null);
            }
            UnityEngine.Object.DestroyImmediate(root);
            World.DefaultGameObjectInjectionWorld = savedWorld;
            world.Dispose();
        }
        return new { passed = checks };
    }

    private static void VerifyAssets()
    {
        var content = AssetDatabase.LoadAssetAtPath<HullContentDefinition>("Assets/GameContent/Hulls/Data/OrbitContent.asset");
        var ark = AssetDatabase.LoadAssetAtPath<ShipData>("Assets/Prefub/Data/Ship/Arkanoid.asset");
        Check(content != null && content.Rarity == ContentRarity.Rare && content.DisplayName == "Orbit", "Rare hull content configured");
        var data = content.Data;
        Check(data != null && data != ark && data.shipId != ark.shipId, "Separate ShipData and unique hull identity");
        var orbitStats = data.GetMetaRuntimeStats(0); var arkStats = ark.GetMetaRuntimeStats(0);
        Check(orbitStats.MaximumHealthPoints == arkStats.MaximumHealthPoints
            && orbitStats.MaximumShieldPoints == arkStats.MaximumShieldPoints
            && orbitStats.MaximumEnergy == arkStats.MaximumEnergy && orbitStats.Speed == arkStats.Speed,
            "Basic meta characteristics match Arkanoid");
        Check(data.mass == ark.mass && data.drag == ark.drag && data.maximumWeaponCount == ark.maximumWeaponCount
            && data.HealthRegenCooldown == ark.HealthRegenCooldown && data.ShieldRegenCooldown == ark.ShieldRegenCooldown,
            "Physics, weapon capacity and regeneration copied from Arkanoid");
        for(int i = 1; i <= 10; i++)
            Check(data.AbilityDamageProgressionConfig.GetDamageMultiplier(i) == ark.AbilityDamageProgressionConfig.GetDamageMultiplier(i),
                "Ability progression matches source at battle level " + i);
        Check(orbitStats.TryGetContract(out OrbitShipMetaContract orbit) && orbit.ObjectCount == 3, "Orbit contract in Meta level zero");
        Check(!orbitStats.TryGetContract(out ArkanoidShipMetaContract unused), "Copied Meta no longer contains Arkanoid abilities");
        Check(orbitStats.TryGetContract(out AbilityRecoveryShipMetaContract recovery) && recovery.AbilityMode == UltimateAbilityMode.Toggle,
            "Held ability uses existing Toggle recovery contract");
        var gameplay = content.GameplayPrefab;
        Check(gameplay.GetComponent<ParentShip>().ShipData == data
            && gameplay.GetComponent<ParentShip>().PassiveAbility is OrbitPassiveAbility
            && gameplay.GetComponent<ParentShip>().ActiveAbility is OrbitExpansionActiveAbility,
            "Battle prefab links correct data and abilities");
        Check(gameplay.GetComponent<ArkanoidPassiveAbility>() == null && gameplay.GetComponent<ArkanoidStasisActiveAbility>() == null,
            "Battle prefab has no copied Arkanoid active/passive");
        Check(gameplay.GetComponent<HullLoadoutDefinition>().IsConfigurationValid(out string issue), "Valid battle weapon platforms: " + issue);
        Check(content.Prefab.GetComponent<HullLoadoutDefinition>().IsConfigurationValid(out issue), "Valid crafting weapon platforms: " + issue);
        Check(content.Prefab.GetComponent<HullLoadoutDefinition>().ShipData == data, "Craft visual uses Orbit ShipData");
        var catalog = AssetDatabase.LoadAssetAtPath<HullCatalog>("Assets/GameContent/Catalogs/HullCatalog.asset");
        int count = 0; foreach(var hull in catalog.Hulls) if(hull == content) count++;
        Check(count == 1, "Orbit registered once in hull catalog");
        var weapons = AssetDatabase.LoadAssetAtPath<WeaponCatalog>("Assets/GameContent/Catalogs/WeaponCatalog.asset");
        var service = new ContentCatalogService(catalog, weapons);
        Check(service.TryGetHull(content.Id, out var resolved) && resolved == content,
            "Content service resolves Orbit by saved content ID");
        Check(service.TryGetHullByShipId(data.shipId, out resolved) && resolved == content,
            "Content service resolves unambiguous Orbit ShipData ID");
        var factoryObject = new GameObject("Orbit factory verification"); factoryObject.SetActive(false);
        try
        {
            var factory = factoryObject.AddComponent<CreatePlayerShips>();
            Set(factory, "contentCatalog", service);
            var save = new SaveData { hullContentId = content.Id, shipId = data.shipId };
            var args = new object[] { save, null };
            bool success = (bool)typeof(CreatePlayerShips).GetMethod("TryResolveHullPrefab", Private).Invoke(factory, args);
            Check(success && (GameObject)args[1] == content.GameplayPrefab,
                "Existing battle factory resolves Orbit blueprint to its gameplay prefab");
        }
        finally { UnityEngine.Object.DestroyImmediate(factoryObject); }
    }

    private static void VerifySweepMath()
    {
        Check(ProjectileInterceptorSystem.IntersectsMovingCircle(new float2(-1, 0), new float2(1, 0), float2.zero, float2.zero, 0.2f), "Fast projectile sweep");
        Check(ProjectileInterceptorSystem.IntersectsMovingCircle(float2.zero, float2.zero, new float2(-1, 0), new float2(1, 0), 0.2f), "Satellite sweeps over stationary projectile");
        Check(!ProjectileInterceptorSystem.IntersectsMovingCircle(new float2(-1, 1), new float2(1, 1), new float2(-1, 0), new float2(1, 0), 0.2f), "Relative parallel motion preserves gap");
        Check(ProjectileInterceptorSystem.IntersectsMovingCircle(new float2(0.19f, 0), new float2(0.19f, 0), float2.zero, float2.zero, 0.2f), "Stationary overlapping circles");
    }

    private static void VerifyGapLimit()
    {
        var contract = new OrbitShipMetaContract();
        Set(contract, "objectRadius", 100f);
        foreach(int count in new[] { 1, 2, 3, 4, 6, 12, 32 })
        {
            Set(contract, "objectCount", count);
            float spacing = count == 1 ? contract.BaseOrbitRadius * 2f
                : contract.BaseOrbitRadius * 2f * Mathf.Sin(Mathf.PI / count);
            Check(contract.ObjectRadius * 2f < spacing * 0.7f && contract.IsObjectRadiusLimited,
                "Dangerous gaps retained for count " + count);
            Set(contract, "maximumChargedObjectRadius", 100f);
            for(int step = 0; step <= 20; step++)
            {
                float progress = step / 20f;
                float orbitRadius = Mathf.Lerp(contract.BaseOrbitRadius, contract.MaximumOrbitRadius, progress);
                float separation = count == 1 ? orbitRadius * 2f : orbitRadius * 2f * Mathf.Sin(Mathf.PI / count);
                Check(contract.EvaluateObjectRadius(progress) * 2f < separation * 0.7f,
                    "Growth preserves gaps at charge " + progress + ", count " + count);
            }
        }
    }

    private static void VerifyArkanoid(World world, EntityManager em, EnemyProjectileCollisionRegistry registry, Entity registryEntity)
    {
        var go = new GameObject("Arkanoid regression");
        go.SetActive(false);
        try
        {
            go.AddComponent<Rigidbody2D>(); go.AddComponent<CircleCollider2D>();
            var ball = go.AddComponent<ArkanoidBall>();
            registry.RegisterInterceptor(ball);
            // Verify the original collider-bounds code path remains selected.
            var registered = (IList)Get(registry, "targets");
            Check(registered.Count > 0 && ball is EnemyProjectileInterceptor, "Arkanoid remains registered in shared interceptor architecture");
            Check(!ball.TryGetCircularSweep(out var previous, out var center, out float radius), "Arkanoid retains its existing collider-bounds behavior");
            registry.UnregisterInterceptor(ball);
        }
        finally { UnityEngine.Object.DestroyImmediate(go); }
    }

    private static Entity Shot(EntityManager em, float2 start, float2 end)
    {
        var entity = em.CreateEntity();
        em.AddComponentData(entity, new EnemyProjectileResolution());
        em.AddComponentData(entity, new EnemyProjectilePreviousPosition { Value = start });
        em.AddComponentData(entity, new EnemyProjectileStaticData { Radius = 0.01f, Characteristic = EnemyProjectileCharacteristic.Fragile });
        em.AddComponentData(entity, new EnemyProjectileDamage { Value = 1f });
        em.AddComponentData(entity, LocalTransform.FromPosition(new float3(end, 0f)));
        return entity;
    }
    private static FieldInfo Field(object target, string name)
    {
        for(Type type = target.GetType(); type != null; type = type.BaseType)
        { var field = type.GetField(name, Private); if(field != null) return field; }
        throw new Exception("Missing field " + name);
    }
    private static void Set(object target, string field, object value) => Field(target, field).SetValue(target, value);
    private static object Get(object target, string field) => Field(target, field).GetValue(target);
    private static object Invoke(object target, string name, params object[] args)
    {
        for(Type type = target.GetType(); type != null; type = type.BaseType)
        { var method = type.GetMethod(name, Private); if(method != null) return method.Invoke(target, args); }
        throw new Exception("Missing method " + name);
    }
    private static void Check(bool success, string message)
    { if(!success) throw new Exception(message); checks++; }
}
