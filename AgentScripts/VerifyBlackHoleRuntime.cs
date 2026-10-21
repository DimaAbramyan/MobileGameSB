using System;
using System.Collections.Generic;
using System.Reflection;
using Unity.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEditor;
using UnityEngine;

// Run real ECS systems in an isolated World; no player saves or battle-scene changes.
public static class VerifyBlackHoleRuntime
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    public static object Run()
    {
        if (EditorApplication.isPlaying || EditorApplication.isCompiling || EditorApplication.isUpdating)
            throw new Exception("Editor must be idle in Edit Mode.");
        var checks = new List<string>();
        VerifyDipoleMath(checks);
        var blackPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/GameContent/Hulls/Gameplay/BlackHoleCraft.prefab");
        var activeFieldPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefub/AbilitiesObjects/BlackHole.prefab");
        var arkanoidPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefub/Ships/Arkanoid.prefab");
        var referenceMagnet = arkanoidPrefab.GetComponentInChildren<Magnite>(true);
        foreach (var path in new[] { "Assets/GameContent/Hulls/Gameplay/BlackHoleCraft.prefab", "Assets/Prefub/Ships/BlackHole.prefab" })
        {
            var magnet = AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponentInChildren<Magnite>(true);
            Check(Mathf.Approximately(magnet.MagnetRadius, referenceMagnet.MagnetRadius)
                && Mathf.Approximately(magnet.AttractionRadius, referenceMagnet.AttractionRadius),
                "Magnet radii match Arkanoid: " + path, checks);
            Check(magnet.GetComponent<CircleCollider2D>().offset == referenceMagnet.GetComponent<CircleCollider2D>().offset
                && Mathf.Approximately(magnet.GetComponent<CircleCollider2D>().radius, referenceMagnet.MagnetRadius),
                "Magnet collider matches Arkanoid: " + path, checks);
        }

        var clone = UnityEngine.Object.Instantiate(blackPrefab);
        var other = UnityEngine.Object.Instantiate(arkanoidPrefab);
        BlackHolePrefab activeField = null;
        GameObject intangibleRoot = null;
        var helper = new GameObject("Black Hole runtime verification");
        var world = new World("Black Hole slow verification");
        try
        {
            var ship = clone.GetComponent<ParentShip>();
            var passive = (BlackHolePassive)ship.PassiveAbility;
            // Follow the production lifecycle, without manually assigning stats.
            ship.Awake();
            ship.Start();
            ship.ShowShip();
            Check(passive.TryGetSlowField(out EnemyProjectileSlowField startupField)
                && startupField.EvaluateMultiplier(startupField.Center
                    - startupField.Forward * startupField.Radius * 0.5f) > 1f,
                "Real ship Awake/Start applies rear acceleration without fixture stat injection", checks);
            passive.Init(ship);
            passive.ApplyShipMetaStats(ship.ShipData.GetMetaRuntimeStats(0));
            ship.ShowShip();
            var controller = helper.AddComponent<PlayerController>();
            Set(controller, "currentShip", ship);
            var registry = helper.AddComponent<EnemyProjectileCollisionRegistry>();
            var em = world.EntityManager;
            var registryEntity = em.CreateEntity(typeof(EnemyProjectileCollisionRegistryTag));
            em.AddBuffer<EnemyProjectileCollisionTarget>(registryEntity);
            em.AddBuffer<EnemyProjectileSlowField>(registryEntity);
            em.AddBuffer<EnemyProjectilePurgeField>(registryEntity);
            em.AddBuffer<EnemyProjectileResolutionEvent>(registryEntity);
            em.AddBuffer<EnemyProjectileBurstSpawnRequest>(registryEntity);
            Set(registry, "playerController", controller);
            Set(registry, "entityWorld", world);
            Set(registry, "entityManager", em);
            Set(registry, "registryEntity", registryEntity);
            registry.RegisterShip(ship);
            registry.RegisterShip(ship);
            Invoke(registry, "UpdateTargetSnapshot");
            Check(em.GetBuffer<EnemyProjectileSlowField>(registryEntity).Length == 1,
                "Active Black Hole emits one aura, including duplicate registration", checks);
            Check(em.GetBuffer<EnemyProjectileCollisionTarget>(registryEntity).Length == 1,
                "Magnet and passive collider do not enlarge the damage hitbox", checks);
            var field = em.GetBuffer<EnemyProjectileSlowField>(registryEntity)[0];
            Check(math.abs(field.ShipRadius - blackPrefab.GetComponent<CircleCollider2D>().radius) < 0.00001f,
                "Ship radius constant matches real hitbox", checks);
            Check(field.MaximumSpeedMultiplier > 1f && field.DistanceFalloff > 0f,
                "Existing serialized ship config supplies new dipole defaults", checks);
            Check(field.Radius > referenceMagnet.AttractionRadius,
                "Slow aura remains independent of smaller Magnet", checks);
            var movement = world.GetOrCreateSystem<ProjectileMovementSystem>();
            world.SetTime(new TimeData(1, 0.1f));
            VerifyEdgeDistances(em, world, movement, field, checks);
            float2 center = field.Center;
            float2 frontPosition = center + field.Forward * field.Radius * 0.5f;
            VerifyLegacy(passive, field, checks);
            Movement(em, world, movement, center, field.MinimumSpeedMultiplier, "Slow at center", checks);
            Movement(em, world, movement, center + new float2(field.Radius * 0.5f, 0),
                1f, "Neutral lateral corridor", checks);
            Movement(em, world, movement, frontPosition,
                field.EvaluateMultiplier(frontPosition),
                "Distance-based front slow", checks);
            Movement(em, world, movement, center - field.Forward * field.Radius * 0.5f,
                field.EvaluateMultiplier(center - field.Forward * field.Radius * 0.5f),
                "Rear acceleration is applied by real ECS movement", checks);
            Movement(em, world, movement, center + new float2(field.Radius, 0), 1, "Full speed at boundary", checks);
            Movement(em, world, movement, center + new float2(field.Radius * 2, 0), 1, "Full speed outside aura", checks);

            var moving = Projectile(em, frontPosition, new float2(1, 0));
            for (int tick = 0; tick < 3; tick++)
            {
                em.SetComponentData(moving, LocalTransform.FromPosition(new float3(frontPosition, 0)));
                movement.Update(world.Unmanaged);
                Check(math.abs(em.GetComponentData<EnemyProjectileVelocity>(moving).Value.x - 1f) < 0.00001f,
                    "Slow does not compound or overwrite velocity, tick " + tick, checks);
            }
            em.SetComponentData(moving, LocalTransform.FromPosition(new float3(center.x + field.Radius * 2, center.y, 0)));
            float before = em.GetComponentData<LocalTransform>(moving).Position.x;
            movement.Update(world.Unmanaged);
            Check(math.abs(em.GetComponentData<LocalTransform>(moving).Position.x - before - 0.1f) < 0.0001f,
                "Same projectile recovers full speed after leaving aura", checks);
            em.DestroyEntity(moving);

            var customized = Projectile(em, frontPosition, new float2(2, 0));
            em.AddComponentData(customized, new EnemyProjectileCustomSpeed { InitialSpeed = 2,
                Direction = new float2(1, 0), HasReachedSpeedChangeY = 1 });
            em.AddBuffer<EnemyProjectileSpeedCurveSample>(customized).Add(new EnemyProjectileSpeedCurveSample { SpeedMultiplier = 2 });
            var customSpeed = world.GetOrCreateSystem<ProjectileCustomSpeedSystem>();
            customSpeed.Update(world.Unmanaged);
            movement.Update(world.Unmanaged);
            Check(math.abs(em.GetComponentData<EnemyProjectileVelocity>(customized).Value.x - 4) < 0.0001f
                && math.abs(em.GetComponentData<LocalTransform>(customized).Position.x - frontPosition.x
                    - 0.4f * field.EvaluateMultiplier(frontPosition)) < 0.0001f,
                "Custom speed curve combines with aura", checks);
            em.AddComponentData(customized, new EnemyProjectileCustomScale { HasReachedScaleChangeY = 1 });
            em.AddBuffer<EnemyProjectileScaleCurveSample>(customized).Add(new EnemyProjectileScaleCurveSample { ScaleMultiplier = 1 });
            var customScale = world.GetOrCreateSystem<ProjectileCustomScaleSystem>();
            customScale.Update(world.Unmanaged);
            Check(em.GetComponentData<EnemyProjectileCustomScale>(customized).ProgressSinceScaleChange < 0.4f,
                "Scale profile progresses by slowed travel", checks);
            em.DestroyEntity(customized);

            var homing = Projectile(em, frontPosition, new float2(0, -2));
            em.AddComponentData(homing, new EnemyProjectileHoming { TurnSpeedDegreesPerSecond = 360 });
            var homingSystem = world.GetOrCreateSystem<ProjectileHomingSystem>();
            homingSystem.Update(world.Unmanaged);
            float2 steeredVelocity = em.GetComponentData<EnemyProjectileVelocity>(homing).Value;
            float2 beforeHoming = em.GetComponentData<LocalTransform>(homing).Position.xy;
            movement.Update(world.Unmanaged);
            float traveled = math.distance(beforeHoming, em.GetComponentData<LocalTransform>(homing).Position.xy);
            Check(math.abs(math.length(steeredVelocity) - 2) < 0.0001f
                && math.dot(steeredVelocity, center - frontPosition) > 0f
                && traveled < 0.2f, "Homing keeps steering while travel is slowed", checks);
            em.DestroyEntity(homing);

            var burst = Projectile(em, center, new float2(2, 0));
            em.AddComponentData(burst, new EnemyProjectileBurstAtPoint { TargetPosition = center + new float2(0.1f, 0),
                DetonationRadius = 0.001f, ProjectilesPerWave = 4, WaveCount = 1 });
            var burstSystem = world.GetOrCreateSystem<ProjectileBurstAtPointSystem>();
            burstSystem.Update(world.Unmanaged);
            Check(em.GetComponentData<EnemyProjectileBurstAtPoint>(burst).HasDetonated == 0,
                "Burst projectile cannot detonate early using unslowed travel", checks);
            movement.Update(world.Unmanaged);
            Check(em.GetComponentData<LocalTransform>(burst).Position.x < center.x + 0.1f,
                "Burst projectile approaches target at slowed speed", checks);
            em.DestroyEntity(burst);

            ship.HideShip();
            Invoke(registry, "UpdateTargetSnapshot");
            Check(em.GetBuffer<EnemyProjectileSlowField>(registryEntity).Length == 0,
                "Hide/Off removes aura", checks);
            Movement(em, world, movement, center, 1, "Full speed after ability Off", checks);
            ship.ShowShip();
            passive.enabled = false;
            Invoke(registry, "UpdateTargetSnapshot");
            Check(em.GetBuffer<EnemyProjectileSlowField>(registryEntity).Length == 0,
                "Disabling passive removes aura", checks);
            passive.enabled = true;
            ship.ShowShip();
            Set(controller, "currentShip", other.GetComponent<ParentShip>());
            Invoke(registry, "UpdateTargetSnapshot");
            Check(em.GetBuffer<EnemyProjectileSlowField>(registryEntity).Length == 0,
                "Switching ships removes old aura even if Off was missed", checks);
            Set(controller, "currentShip", ship);
            clone.transform.position += Vector3.right;
            clone.transform.rotation = Quaternion.Euler(0f, 0f, 90f);
            Invoke(registry, "UpdateTargetSnapshot");
            Check(math.distance(em.GetBuffer<EnemyProjectileSlowField>(registryEntity)[0].Center, center + new float2(1, 0)) < 0.0001f,
                "Aura follows ship position", checks);
            Check(math.distance(em.GetBuffer<EnemyProjectileSlowField>(registryEntity)[0].Forward,
                new float2(-1f, 0f)) < 0.0001f, "Field lobes follow ship local Forward rotation", checks);

            activeField = UnityEngine.Object.Instantiate(activeFieldPrefab)
                .GetComponent<BlackHolePrefab>();
            Invoke(activeField, "Awake");
            Set(activeField, "projectileCollisionRegistry", registry);
            registry.RegisterProjectilePurgeField(activeField);
            Invoke(registry, "UpdateTargetSnapshot");
            DynamicBuffer<EnemyProjectilePurgeField> purgeFields =
                em.GetBuffer<EnemyProjectilePurgeField>(registryEntity);
            Check(purgeFields.Length == 1,
                "Active Black Hole emits one ECS purge field", checks);
            var purgeSystem = world.GetOrCreateSystem<ProjectilePurgeFieldSystem>();
            Entity purged = Projectile(em, purgeFields[0].Center, new float2(1, 0));
            purgeSystem.Update(world.Unmanaged);
            Check(em.GetComponentData<EnemyProjectileResolution>(purged).Kind
                    == EnemyProjectileResolutionKind.Expired,
                "Active Black Hole marks ECS projectile for cleanup", checks);
            Entity preserved = Projectile(
                em,
                purgeFields[0].Center + new float2(purgeFields[0].Radius + 0.1f, 0),
                new float2(1, 0));
            purgeSystem.Update(world.Unmanaged);
            Check(em.GetComponentData<EnemyProjectileResolution>(preserved).Kind
                    == EnemyProjectileResolutionKind.None,
                "Active Black Hole preserves ECS projectile outside its radius", checks);
            em.DestroyEntity(purged);
            em.DestroyEntity(preserved);
            registry.UnregisterProjectilePurgeField(activeField);
            UnityEngine.Object.DestroyImmediate(activeField.gameObject);
            activeField = null;
            Invoke(registry, "UpdateTargetSnapshot");
            Check(em.GetBuffer<EnemyProjectilePurgeField>(registryEntity).Length == 0,
                "Destroying active Black Hole removes ECS purge field", checks);

            intangibleRoot = new GameObject("Intangible magnet verification");
            CircleCollider2D shipHitbox = intangibleRoot.AddComponent<CircleCollider2D>();
            var magnetObject = new GameObject("Magnet");
            magnetObject.transform.SetParent(intangibleRoot.transform, false);
            CircleCollider2D magnetCollider = magnetObject.AddComponent<CircleCollider2D>();
            ShipIntangibleState intangible =
                intangibleRoot.AddComponent<ShipIntangibleState>();
            intangible.ConfigureDefaultHitbox(shipHitbox);
            intangible.Enter();
            Check(!shipHitbox.enabled && magnetCollider.enabled,
                "Invulnerability disables only ship hitbox and keeps Magnet active", checks);
            intangible.Exit();
            Check(shipHitbox.enabled && magnetCollider.enabled,
                "Ending invulnerability restores hitbox without changing Magnet", checks);

            UnityEngine.Object.DestroyImmediate(clone);
            clone = null;
            Invoke(registry, "UpdateTargetSnapshot");
            Check(em.GetBuffer<EnemyProjectileSlowField>(registryEntity).Length == 0,
                "Destroying ship removes aura", checks);
            return new { passed = checks.Count, auraRadius = field.Radius,
                minimumSpeedMultiplier = field.MinimumSpeedMultiplier, checks };
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(helper);
            if (clone != null) UnityEngine.Object.DestroyImmediate(clone);
            UnityEngine.Object.DestroyImmediate(other);
            if (activeField != null) UnityEngine.Object.DestroyImmediate(activeField.gameObject);
            if (intangibleRoot != null) UnityEngine.Object.DestroyImmediate(intangibleRoot);
            world.Dispose();
        }
    }

    private static Entity Projectile(EntityManager em, float2 position, float2 velocity)
    {
        var entity = em.CreateEntity(typeof(LocalTransform), typeof(EnemyProjectilePreviousPosition),
            typeof(EnemyProjectileVelocity), typeof(EnemyProjectileMovement), typeof(EnemyProjectileResolution),
            typeof(EnemyProjectileStaticData));
        em.SetComponentData(entity, LocalTransform.FromPosition(new float3(position, 0)));
        em.SetComponentData(entity, new EnemyProjectileVelocity { Value = velocity });
        return entity;
    }

    private static void VerifyDipoleMath(List<string> checks)
    {
        var contract = new BlackHoleShipMetaContract();
        contract.Configure(3f, 50f, 10f, 0.5f);
        EnemyProjectileSlowField field = contract.CreateProjectileField(float2.zero, new float2(0f, 1f));
        Check(math.abs(field.EvaluateMultiplier(new float2(0f, 5f)) - 0.75f) < 0.00001f
            && math.abs(field.EvaluateMultiplier(new float2(0f, -5f)) - 1.175f) < 0.00001f,
            "Front/back distance envelope has independent slow/fast extrema", checks);
        float previous = field.EvaluateMultiplier(new float2(0f, 2f));
        for (int angle = 1; angle <= 90; angle++)
        {
            float a = math.radians(angle);
            float value = field.EvaluateMultiplier(new float2(math.sin(a), math.cos(a)) * 2f);
            Check(value >= previous - 0.00001f && value <= 1f,
                "Front angular monotonicity " + angle, checks);
            previous = value;
        }
        for (int angle = 90; angle <= 180; angle++)
        {
            float a = math.radians(angle);
            float value = field.EvaluateMultiplier(new float2(math.sin(a), math.cos(a)) * 2f);
            Check(value >= previous - 0.00001f && value <= 1.35f,
                "Back angular monotonicity " + angle, checks);
            previous = value;
        }
        for (int angle = 45; angle <= 135; angle++)
        {
            float a = math.radians(angle);
            float2 p = new float2(math.sin(a), math.cos(a)) * 2f;
            Check(field.EvaluateMultiplier(p) == 1f
                && field.EvaluateMultiplier(new float2(-p.x, p.y)) == 1f,
                "Wide neutral side region " + angle, checks);
        }
        float priorFront = 0.5f;
        float priorBack = 1.35f;
        for (int step = 1; step <= 100; step++)
        {
            float distance = step * 0.1f;
            float front = field.EvaluateMultiplier(new float2(0f, distance));
            float back = field.EvaluateMultiplier(new float2(0f, -distance));
            Check(front >= priorFront && back <= priorBack
                && math.isfinite(front) && math.isfinite(back), "Distance continuity " + step, checks);
            priorFront = front;
            priorBack = back;
        }
        Check(priorFront == 1f && priorBack == 1f
            && field.EvaluateMultiplier(new float2(0f, 20f)) == 1f,
            "Boundary/outside are neutral without editing base speed", checks);
        field.Forward = new float2(1f, 0f);
        Check(math.abs(field.EvaluateMultiplier(new float2(5f, 0f)) - 0.75f) < 0.00001f
            && field.EvaluateMultiplier(new float2(0f, 5f)) == 1f,
            "Rotated forward changes lobes without changing projectile direction", checks);
        contract.ConfigureFieldShape(20f, 10f, 40f, 15f, 1.8f, 2.5f);
        var copy = (BlackHoleShipMetaContract)contract.Clone();
        Check(copy.FrontFullEffectAngle == 20f && copy.FrontTransitionAngle == 10f
            && copy.BackFullEffectAngle == 40f && copy.BackTransitionAngle == 15f
            && copy.MaximumProjectileSpeedMultiplier == 1.8f && copy.DistanceFalloff == 2.5f
            && copy.ProjectileSlowRadius == 10f && copy.MinimumProjectileSpeedMultiplier == 0.5f,
            "Runtime contract clone preserves all serialized field settings", checks);
        using (var world = new World("Dipole stacking verification"))
        {
            var entity = world.EntityManager.CreateEntity();
            var fields = world.EntityManager.AddBuffer<EnemyProjectileSlowField>(entity);
            fields.Add(field);
            fields.Add(field);
            Check(math.abs(EnemyProjectileSlowField.EvaluateMultiplier(new float2(-5f, 0f), fields) - 1.175f) < 0.00001f,
                "Acceleration is not clamped to 1 or compounded across duplicate fields", checks);
        }
    }

    private static void VerifyEdgeDistances(EntityManager em, World world, SystemHandle movement,
        EnemyProjectileSlowField field, List<string> checks)
    {
        const float radius = 0.2f;
        float2 near = field.Center + field.Forward * (field.ShipRadius + radius);
        Check(math.abs(field.EvaluateMultiplier(near, radius) - field.MinimumSpeedMultiplier) < 0.00001f,
            "Touching hitbox edges gives full effect", checks);
        Check(math.abs(field.EvaluateMultiplier(field.Center + field.Forward * 0.01f, radius)
                - field.MinimumSpeedMultiplier) < 0.00001f,
            "Overlapping circles clamp distance to zero", checks);
        var pointField = field;
        pointField.ShipRadius = 0f;
        foreach (float sign in new[] { 1f, -1f })
        {
            float2 position = field.Center + field.Forward * sign * (field.Radius * 0.5f + field.ShipRadius + radius);
            Check(math.abs(field.EvaluateMultiplier(position, radius)
                    - pointField.EvaluateMultiplier(field.Center + field.Forward * sign * field.Radius * 0.5f)) < 0.00001f,
                "Front/back falloff uses edge gap", checks);
            var entity = Projectile(em, position, new float2(1f, 0f));
            em.SetComponentData(entity, new EnemyProjectileStaticData { Radius = radius });
            movement.Update(world.Unmanaged);
            Check(math.abs(em.GetComponentData<LocalTransform>(entity).Position.x - position.x
                    - 0.1f * field.EvaluateMultiplier(position, radius)) < 0.00001f,
                "Actual ECS movement includes hitbox radius", checks);
            em.SetComponentData(entity, LocalTransform.FromPositionRotationScale(new float3(position, 0), quaternion.identity, 2f));
            em.AddComponentData(entity, new EnemyProjectileCustomScale());
            movement.Update(world.Unmanaged);
            Check(math.abs(em.GetComponentData<LocalTransform>(entity).Position.x - position.x
                    - 0.1f * field.EvaluateMultiplier(position, radius * 2f)) < 0.00001f,
                "Scaled ECS hitbox matches collision radius", checks);
            em.DestroyEntity(entity);
        }
        float2 boundary = field.Center + field.Forward * (field.Radius + field.ShipRadius + radius);
        Check(math.abs(field.EvaluateMultiplier(boundary, radius) - 1f) < 0.00001f,
            "Edge-gap boundary returns normal speed", checks);
        Check(field.EvaluateMultiplier(field.Center + new float2(field.ShipRadius + radius, 0f), radius) == 1f,
            "Hitbox radii preserve neutral side corridor", checks);
    }

    private static void VerifyLegacy(BlackHolePassive passive, EnemyProjectileSlowField field, List<string> checks)
    {
        var root = new GameObject("Legacy dipole verification");
        try
        {
            var bullet = root.AddComponent<EnemyBullet>();
            var collider = root.AddComponent<CircleCollider2D>();
            typeof(EnemyBullet).GetField("_speed", Private).SetValue(bullet, 2f);
            typeof(EnemyBullet).GetField("_MaxRange", Private).SetValue(bullet, 100f);
            var multiplier = typeof(EnemyProjectile).GetField("SpeedMultiplier", Private);
            float2 front = field.Center + field.Forward * field.Radius * 0.5f;
            root.transform.position = new Vector3(front.x, front.y, 0f);
            bullet.Launch(Vector3.right);
            var stay = typeof(BlackHolePassive).GetMethod("OnTriggerStay2D", Private);
            stay.Invoke(passive, new object[] { collider });
            float expected = field.EvaluateMultiplier(front, collider.radius);
            Check(math.abs((float)multiplier.GetValue(bullet) - expected) < 0.00001f,
                "Legacy and ECS use the same front speed multiplier", checks);
            float x = root.transform.position.x;
            bullet.TransformPosition();
            Check(math.abs(root.transform.position.x - x - 2f * expected * Time.deltaTime) < 0.0001f
                && bullet._position == Vector3.right
                && (float)typeof(EnemyBullet).GetField("_speed", Private).GetValue(bullet) == 2f,
                "Legacy displacement uses multiplier without changing base speed or direction", checks);
            float2 back = field.Center - field.Forward * field.Radius * 0.5f;
            root.transform.position = new Vector3(back.x, back.y, 0f);
            Invoke(passive, "FixedUpdate");
            Check((float)multiplier.GetValue(bullet) > 1f, "Legacy rear lobe accelerates", checks);
            typeof(BlackHolePassive).GetMethod("OnTriggerExit2D", Private).Invoke(passive, new object[] { collider });
            Check((float)multiplier.GetValue(bullet) == 1f, "Legacy exit resets multiplier", checks);
            stay.Invoke(passive, new object[] { collider });
            passive.Off();
            Check((float)multiplier.GetValue(bullet) == 1f, "Legacy Off resets tracked projectile", checks);
            passive.On();
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    private static void Movement(EntityManager em, World world, SystemHandle system,
        float2 position, float multiplier, string name, List<string> checks)
    {
        var entity = Projectile(em, position, new float2(1, 0));
        system.Update(world.Unmanaged);
        Check(math.abs(em.GetComponentData<LocalTransform>(entity).Position.x - position.x - 0.1f * multiplier) < 0.0001f,
            name, checks);
        em.DestroyEntity(entity);
    }
    private static void Check(bool value, string name, List<string> checks)
    { if (!value) throw new Exception(name); checks.Add(name); }
    private static void Set(object target, string field, object value) => target.GetType().GetField(field, Private).SetValue(target, value);
    private static void Invoke(object target, string method) => target.GetType().GetMethod(method, Private).Invoke(target, null);
}
