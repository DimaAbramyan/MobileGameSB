using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class VerifyProjectileCharacteristics
{
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    static int checks;
    static void Check(bool condition, string name) { if (!condition) throw new Exception(name); checks++; }
    static void Set(object target, string field, object value)
    {
        for (var type = target.GetType(); type != null; type = type.BaseType)
        {
            var property = type.GetProperty(field, Flags);
            if (property != null) { property.SetValue(target, value); return; }
            var member = type.GetField(field, Flags);
            if (member != null) { member.SetValue(target, value); return; }
        }
        throw new Exception(field);
    }
    static object Get(object target, string name) => target.GetType().GetField(name, Flags).GetValue(target);
    static void Invoke(object target, string method) => target.GetType().GetMethod(method, Flags).Invoke(target, null);
    static EnemyProjectileSpawnRequest Request(EnemyBullet prefab, EnemyProjectileCharacteristic type)
        => new EnemyProjectileSpawnRequest(prefab, Vector3.zero, Vector3.down, 1f, 3f,
            EnemyProjectileSpeedBehavior.None, 0f, null, EnemyProjectileSizeBehavior.Default,
            0f, 1f, null, 1f, Vector3.zero, EnemyProjectileMovementPattern.Default, 0f, 0f,
            EnemyProjectileOrbitCenterMode.SourcePosition, Vector2.zero, 0f, 0f, false, 0f,
            overwritesBaseColour: true, projectileColour: Color.blue,
            overwritesCharacteristic: true, characteristic: type);

    public static object Run()
    {
        if (EditorApplication.isPlaying || EditorApplication.isCompiling || EditorApplication.isUpdating)
            throw new Exception("Editor must be idle");
        checks = 0;
        var scene = EditorSceneManager.NewPreviewScene();
        var root = new GameObject("Projectile characteristics verification");
        SceneManager.MoveGameObjectToScene(root, scene);
        var preset = ScriptableObject.CreateInstance<DirectedWaveAttackPreset>();
        try
        {
            var contractRoot = new GameObject("Ability contracts");
            contractRoot.transform.SetParent(root.transform);
            contractRoot.SetActive(false);
            IEnemyProjectileDestructionAbility[] contracts = {
                contractRoot.AddComponent<BlackHolePrefab>(),
                contractRoot.AddComponent<MaterializedBladeTrail>(),
                contractRoot.AddComponent<ArkanoidBall>(),
                contractRoot.AddComponent<OrbitSatellite>()
            };
            for (int i = 0; i < contracts.Length; i++)
            {
                Check(contracts[i].CanDestroyProjectile(EnemyProjectileCharacteristic.Fragile),
                    "All four ability contracts allow Fragile");
                Check(contracts[i].CanDestroyProjectile(EnemyProjectileCharacteristic.Normal) == (i < 2),
                    "Only Black Hole and Blade contracts allow Normal");
            }
            var enemy = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefub/Enemies Prefab/Enemy.prefab").GetComponent<Enemy>();
            var prefab = (EnemyBullet)new SerializedObject(enemy.GetComponent<SimpleEnemyAttackExecutor>())
                .FindProperty("defaultProjectilePrefab").objectReferenceValue;
            Check(prefab.GetComponent<EnemyProjectileAuthoring>().Characteristic == EnemyProjectileCharacteristic.Fragile,
                "Existing projectile prefab defaults Fragile");
            var wave = root.AddComponent<DirectedEnemySubWave>();
            var attack = root.AddComponent<DirectedWaveAttackBehaviour>();
            Set(wave, "enemyPrefab", enemy); Set(wave, "enemyCount", 1);
            Set(wave, "formationLayout", DirectedWaveFormationLayout.HorizontalLine);
            var settings = (DirectedWaveAttackSettings)Get(attack, "attackSettings");
            Set(settings, "fireMode", DirectedWaveAttackFireMode.Aimed);
            Set(settings, "burstSettingsSource", DirectedWaveBurstSettingsSource.WaveOverride);
            Set(settings, "waitForPreviousAttack", true); Set(settings, "delayAfterAttack", 1000f);
            settings.WaveBurstSettings.ConfigureLegacySingleAttack(1, 0, 0);
            var shooting = settings.WaveBurstSettings.Shooting;
            shooting.EmissionLayout.Configure(EnemyEmissionLayoutType.Point);
            Check(!shooting.OverwritesProjectileCharacteristic && shooting.ProjectileCharacteristic == EnemyProjectileCharacteristic.Fragile,
                "New attack profiles default Fragile without overwrite");
            Set(shooting, "overwriteProjectileCharacteristic", true);
            Set(shooting, "projectileCharacteristic", EnemyProjectileCharacteristic.Fragile);
            Set(shooting, "overwriteBaseColour", true); Set(shooting, "projectileColour", Color.blue);
            preset.SetAttackSettings(settings);
            Check(preset.AttackSettings.WaveBurstSettings.Shooting.UsesFragileCharacteristic, "Preset preserves Fragile override");
            var serialized = new SerializedObject(attack);
            var property = serialized.FindProperty("attackSettings.waveBurstSettings.shooting");
            var fields = (IEnumerable)typeof(EnemyShootingSettingsDrawer).GetMethod("ProjectileFields", BindingFlags.Static | BindingFlags.NonPublic)
                .Invoke(null, new object[] { property });
            bool colourVisible = false, colourToggleVisible = false;
            foreach (string field in fields) { colourVisible |= field == "projectileColour"; colourToggleVisible |= field == "overwriteBaseColour"; }
            Check(colourVisible && !colourToggleVisible, "Fragile drawer shows locked colour instead of colour override toggle");
            using (var session = new WaveShootingPreviewSession(wave, true, false, 0, new Vector3(0, -5)))
            {
                for (int i = 0; !session.AdvanceTo(.1f, 20); i++) if (i > 100) throw new Exception("Preview seek failed");
                var manager = session.ProjectileWorld.SimulationWorld.EntityManager;
                using var query = manager.CreateEntityQuery(typeof(EnemyProjectileMovement), typeof(EnemyProjectileStaticData), typeof(URPMaterialPropertyBaseColor));
                using var data = query.ToComponentDataArray<EnemyProjectileStaticData>(Allocator.Temp);
                using var colours = query.ToComponentDataArray<URPMaterialPropertyBaseColor>(Allocator.Temp);
                var pink = EnemyProjectileCharacteristics.FragileColour;
                Check(data.Length > 0 && data[0].Characteristic == EnemyProjectileCharacteristic.Fragile,
                    "Subwave override reaches actual preview projectile");
                Check(math.distance(colours[0].Value, EnemyProjectileCharacteristics.ToRenderColour(pink)) < .0001f,
                    "Fragile ignores authored blue override and uses fixed pink");
                session.ProjectileWorld.CaptureRenderState();
                var batches = (IList)Get(session.ProjectileWorld, "batchList");
                var material = (Material)batches[0].GetType().GetField("Material").GetValue(batches[0]);
                Check(Vector4.Distance(material.GetColor("_BaseColor"), pink) < .0001f, "Preview render uses locked pink colour");
            }
            using (var preview = new EnemyProjectilePreviewWorld())
            {
                var world = preview.SimulationWorld; var manager = world.EntityManager;
                using var registryQuery = manager.CreateEntityQuery(typeof(EnemyProjectileCollisionRegistryTag));
                var registry = registryQuery.GetSingletonEntity();
                manager.AddBuffer<EnemyProjectilePurgeField>(registry);
                var targets = manager.GetBuffer<EnemyProjectileCollisionTarget>(registry);
                targets.Add(new EnemyProjectileCollisionTarget { Kind = EnemyProjectileCollisionTargetKind.Interceptor,
                    TargetId = 42, IsCircle = 1, Radius = .5f });
                preview.Spawn(new List<EnemyProjectileSpawnRequest> { Request(prefab, EnemyProjectileCharacteristic.Normal), Request(prefab, EnemyProjectileCharacteristic.Fragile) });
                using var query = manager.CreateEntityQuery(typeof(EnemyProjectileMovement));
                using var entities = query.ToEntityArray(Allocator.Temp);
                var interceptor = world.GetOrCreateSystem<ProjectileInterceptorSystem>();
                interceptor.Update(world.Unmanaged);
                Check(manager.GetComponentData<EnemyProjectileResolution>(entities[0]).Kind == EnemyProjectileResolutionKind.None,
                    "Orbit/Arkanoid interceptors cannot destroy Normal");
                Check(manager.GetComponentData<EnemyProjectileResolution>(entities[1]).Kind == EnemyProjectileResolutionKind.Intercepted,
                    "Orbit/Arkanoid interceptors destroy Fragile");
                manager.SetComponentData(entities[1], new EnemyProjectileResolution());
                targets = manager.GetBuffer<EnemyProjectileCollisionTarget>(registry); targets.Clear();
                var bladeObject = new GameObject("Blade characteristic verification");
                bladeObject.transform.SetParent(root.transform);
                var blade = bladeObject.AddComponent<MaterializedBladeTrail>();
                blade.Init(new[] { new Vector2(-1, 0), new Vector2(1, 0) }, Vector2.up, .2f, 0f, 0f, 10f, null, 1f);
                blade.AppendProjectileCollisionTargets(targets, 43);
                Check(targets.Length == 1 && targets[0].DestroysNormal == 1 && targets[0].IsSegment == 1,
                    "Blade snapshots exact trail segments and permits Normal");
                interceptor.Update(world.Unmanaged);
                Check(manager.GetComponentData<EnemyProjectileResolution>(entities[0]).Kind == EnemyProjectileResolutionKind.Intercepted,
                    "Blade destroys Normal ECS projectile");
                Check(manager.GetComponentData<EnemyProjectileResolution>(entities[1]).Kind == EnemyProjectileResolutionKind.Intercepted,
                    "Blade destroys Fragile ECS projectile");
                foreach (var entity in entities) manager.SetComponentData(entity, new EnemyProjectileResolution());
                manager.GetBuffer<EnemyProjectilePurgeField>(registry).Add(new EnemyProjectilePurgeField { Radius = 1f, DestructionPolicy = EnemyProjectileDestructionPolicy.NormalAndFragile });
                world.GetOrCreateSystem<ProjectilePurgeFieldSystem>().Update(world.Unmanaged);
                foreach (var entity in entities)
                    Check(manager.GetComponentData<EnemyProjectileResolution>(entity).Kind == EnemyProjectileResolutionKind.Expired,
                        "Black Hole destroys both characteristics");
                foreach (var entity in entities) manager.SetComponentData(entity, new EnemyProjectileResolution());
                var policyFields = manager.GetBuffer<EnemyProjectilePurgeField>(registry);
                policyFields.Clear();
                policyFields.Add(new EnemyProjectilePurgeField { Radius = 1f,
                    DestructionPolicy = EnemyProjectileDestructionPolicy.FragileOnly });
                world.GetOrCreateSystem<ProjectilePurgeFieldSystem>().Update(world.Unmanaged);
                Check(manager.GetComponentData<EnemyProjectileResolution>(entities[0]).Kind == EnemyProjectileResolutionKind.None,
                    "ECS purge respects FragileOnly policy for Normal");
                Check(manager.GetComponentData<EnemyProjectileResolution>(entities[1]).Kind == EnemyProjectileResolutionKind.Expired,
                    "ECS purge respects FragileOnly policy for Fragile");
                Check(!ProjectileInterceptorSystem.IntersectsSegment(new float2(2, -1), new float2(2, 1), new float2(-1, 0), new float2(1, 0), .1f),
                    "Blade does not intercept beyond its trail");
                Check(ProjectileInterceptorSystem.IntersectsSegment(new float2(0, -1), new float2(0, 1), new float2(-1, 0), new float2(1, 0), .1f),
                    "Fast crossing projectile is intercepted by Blade");
            }
            foreach (bool destroysNormal in new[] { false, true })
            {
                Check(EnemyProjectileCharacteristics.CanBeDestroyed(EnemyProjectileCharacteristic.Normal, destroysNormal) == destroysNormal,
                    "Legacy Normal rule");
                Check(EnemyProjectileCharacteristics.CanBeDestroyed(EnemyProjectileCharacteristic.Fragile, destroysNormal), "Legacy Fragile rule");
            }
            var burstRoot = UnityEngine.Object.Instantiate(prefab.gameObject, root.transform);
            burstRoot.SetActive(false);
            var burstPrefab = burstRoot.GetComponent<EnemyBullet>();
            var burstAuthoring = burstRoot.GetComponent<EnemyProjectileAuthoring>();
            Set(burstAuthoring, "flightMode", EnemyProjectileFlightMode.BurstAtPoint);
            Set(burstAuthoring, "burstDetonationPoint", Vector2.zero);
            Set(burstAuthoring, "burstWaveCount", 1);
            Set(burstAuthoring, "burstProjectilesPerWave", 3);
            Set(burstAuthoring, "burstProjectilePrefab", prefab);
            foreach (var type in new[] { EnemyProjectileCharacteristic.Normal, EnemyProjectileCharacteristic.Fragile })
            {
                using var preview = new EnemyProjectilePreviewWorld();
                preview.Spawn(new List<EnemyProjectileSpawnRequest> { Request(burstPrefab, type) });
                preview.Step(1f / 30f, 1f / 30f, Vector3.down * 5);
                using var query = preview.SimulationWorld.EntityManager.CreateEntityQuery(typeof(EnemyProjectileMovement), typeof(EnemyProjectileStaticData), typeof(URPMaterialPropertyBaseColor));
                using var data = query.ToComponentDataArray<EnemyProjectileStaticData>(Allocator.Temp);
                using var colours = query.ToComponentDataArray<URPMaterialPropertyBaseColor>(Allocator.Temp);
                Check(data.Length == 3, "Burst creates expected child projectiles");
                for (int i = 0; i < data.Length; i++)
                {
                    Check(data[i].Characteristic == type, "Burst child inherits parent characteristic");
                    if (type == EnemyProjectileCharacteristic.Fragile)
                    {
                        var pink = EnemyProjectileCharacteristics.FragileColour;
                        Check(math.distance(colours[i].Value, EnemyProjectileCharacteristics.ToRenderColour(pink)) < .0001f,
                            "Fragile Burst child keeps locked pink");
                    }
                }
            }
            var legacy = new GameObject("Legacy Normal probe"); legacy.transform.SetParent(root.transform);
            var bullet = legacy.AddComponent<EnemyBullet>();
            var legacyAuthoring = legacy.AddComponent<EnemyProjectileAuthoring>();
            Set(legacyAuthoring, "characteristic", EnemyProjectileCharacteristic.Normal);
            Check(!bullet.TryDestroyByAbility() && legacy.activeSelf, "Legacy Normal remains active when other ability tries to destroy it");
            legacy.SetActive(false);
            var renderer = legacy.AddComponent<SpriteRenderer>();
            renderer.color = new Color(.2f, .7f, .4f, .8f);
            var original = renderer.color;
            var authoring = legacyAuthoring;
            Set(authoring, "visualRenderer", renderer);
            Set(authoring, "characteristic", EnemyProjectileCharacteristic.Fragile);
            legacy.SetActive(true); typeof(EnemyProjectile).GetMethod("OnEnable", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(bullet, null);
            Check(renderer.color == EnemyProjectileCharacteristics.FragileColour, "Legacy Fragile uses pink");
            legacy.SetActive(false);
            typeof(EnemyProjectile).GetMethod("OnDisable", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(bullet, null);
            Check(renderer.color == original, "Disabling Fragile restores original colour");
            Set(authoring, "characteristic", EnemyProjectileCharacteristic.Normal);
            legacy.SetActive(true); typeof(EnemyProjectile).GetMethod("OnEnable", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(bullet, null);
            Check(renderer.color == original, "Reused Normal retains original colour");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(preset);
            UnityEngine.Object.DestroyImmediate(root);
            EditorSceneManager.ClosePreviewScene(scene);
        }
        return new { passed = checks };
    }
}





