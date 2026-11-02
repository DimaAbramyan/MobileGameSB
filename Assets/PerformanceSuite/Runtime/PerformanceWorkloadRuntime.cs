#if UNITY_EDITOR || ANDROID_PERFORMANCE_SUITE
using System;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Profiling;
using UnityEngine;
using Zenject;
using Object = UnityEngine.Object;

namespace Game.Performance
{
    // Owns real runtime objects. No SceneContext, ProjectContext, SaveManager or Analytics is resolved here.
    public sealed class PerformanceWorkloadRuntime : IDisposable
    {
        private static readonly ProfilerMarker SpawnMarker = new("PerformanceSuite.Spawn");
        private static readonly ProfilerMarker CombatMarker = new("PerformanceSuite.CombatCommands");
        private static readonly ProfilerMarker WaveMarker = new("PerformanceSuite.WaveSetup");
        private readonly PerformanceSuiteConfig config;
        private readonly PerformanceCase scenario;
        private readonly System.Random random;
        private readonly List<EnemyProjectileSpawnRequest> requests = new(512);
        private readonly List<GameObject> templates = new();
        private readonly DiContainer container = new();
        private readonly List<IDisposable> services = new();
        private readonly List<ITickable> tickables = new();
        private readonly EnemyManager enemies = new();
        private readonly ParentShip[] ships = new ParentShip[2];
        private readonly List<Enemy> enemyScratch = new();
        private EntityQuery projectiles;
        private bool queryCreated;
        private EnemyProjectileEcsSpawner spawner;
        private EnemyProjectileCollisionRegistry registry;
        private PlayerController player;
        private ShipSelect shipSelect;
        private DealDamageManager damage;
        private GameObject root, waveObject;
        private EnemyChildAttackSettings child;
        private EnemyLaserSettings laser;
        private float nextCombat, nextWaveRestart;
        private float nextChildSeed;
        private bool disposed;
        public World World { get; private set; }
        public int ProjectileCount => World != null && World.IsCreated ? projectiles.CalculateEntityCount() : 0;
        public int EntityCount => World != null && World.IsCreated ? World.EntityManager.UniversalQuery.CalculateEntityCount() : 0;
        public int EnemyCount => enemies.enemyList?.Count ?? 0;
        public int Lasers => spawner?.ActiveLasers ?? 0;
        public int ChildParents => spawner?.ActiveChildAttackParents ?? 0;
        public long Spawned => spawner?.PerformanceSpawnedCount ?? 0;
        public long Omitted => spawner?.OmittedChildProjectiles ?? 0;
        public int RemainingOwnedEnemies => root == null ? 0 : root.GetComponentsInChildren<Enemy>(true).Length;
        public bool OwnedWorldAlive => World != null && World.IsCreated;
        public int CoordinatorTransitions { get; private set; }
        private int lastActiveHash;
        private int shipSwitches, extraHealthGrants;

        public PerformanceWorkloadRuntime(PerformanceSuiteConfig config, PerformanceCase scenario, Action<World> configureOwnedWorld = null)
        {
            this.config = config; this.scenario = scenario; random = new System.Random(scenario.seed);
            try { Create(configureOwnedWorld); }
            catch (Exception initializationError)
            {
                try { Dispose(); }
                catch (Exception cleanupError) { throw new AggregateException(initializationError, cleanupError); }
                throw;
            }
        }
        private void Create(Action<World> configureOwnedWorld)
        {
            if (config.bullet == null || config.enemy == null || config.ship == null)
                throw new InvalidOperationException("Generate/configure the suite fixtures first.");
            root = new GameObject("[Performance Workload]"); root.SetActive(false);
            var team = new GameObject("Performance Team"); team.transform.SetParent(root.transform);
            team.transform.position = new Vector3(0, -3.5f);
            team.AddComponent<Rigidbody2D>().gravityScale = 0;
            player = team.AddComponent<PlayerController>(); player.enabled = false;
            shipSelect = team.AddComponent<ShipSelect>();
            // Stable marker names across repetitions; avoids discovering counters from dead Worlds.
            World = new World("Android Performance Suite", WorldFlags.Game);
            configureOwnedWorld?.Invoke(World);
            var systems = DefaultWorldInitialization.GetAllSystems(WorldSystemFilterFlags.Default);
            DefaultWorldInitialization.AddSystemsToRootLevelSystemGroups(World, systems);
            if (!config.renderProjectiles) World.GetOrCreateSystemManaged<PresentationSystemGroup>().Enabled = false;
            registry = root.AddComponent<EnemyProjectileCollisionRegistry>();
            registry.BindPerformanceWorld(World, player);
            spawner = EnemyProjectileEcsSpawner.CreatePerformanceWorld(World, registry);
            container.Bind<EnemyManager>().FromInstance(enemies);
            container.Bind<PlayerController>().FromInstance(player);
            container.Bind<EnemyProjectileCollisionRegistry>().FromInstance(registry);
            container.Bind<EnemyProjectileEcsSpawner>().FromInstance(spawner);
            container.Bind<WaveManager>().FromMethod(_ => null).AsSingle();
            container.Bind<SoundManager>().FromInstance(new SoundManager(null));
            container.Bind<AudioDatabase>().FromMethod(_ => null).AsSingle();
            container.Bind<DealDamageManager>().AsSingle();
            container.Bind<MetalPickupController>().AsSingle();
            container.Bind<EnemyHeatSystem>().AsSingle();
            container.Bind<EnemyTemperatureController>().AsSingle();
            container.Bind<EnemyDisintegrationSystem>().AsSingle();
            container.Bind<EnemyPeriodicDamageSystem>().AsSingle();
            container.Bind<EnemyDebuffController>().AsSingle();
            foreach (var type in new[] { typeof(EnemyHeatSystem), typeof(EnemyTemperatureController), typeof(EnemyDisintegrationSystem), typeof(EnemyPeriodicDamageSystem), typeof(EnemyDebuffController) })
            {
                object service = container.Resolve(type);
                if (service is IInitializable initializable) initializable.Initialize();
                if (service is IDisposable disposable) services.Add(disposable);
                if (service is ITickable tickable) tickables.Add(tickable);
            }
            damage = container.Resolve<DealDamageManager>();
            for (int i = 0; i < ships.Length; i++)
            {
                var instance = container.InstantiatePrefab(config.ship.gameObject, team.transform);
                ships[i] = instance.GetComponent<ParentShip>();
            }
            projectiles = World.EntityManager.CreateEntityQuery(typeof(EnemyProjectileStaticData));
            queryCreated = true;
            root.SetActive(true);
            shipSelect.InitializeShips();
            if (scenario.workload != PerformanceWorkload.Combat && scenario.workload != PerformanceWorkload.Soak)
                foreach (var ship in ships) ship.SetHealthPoints(1000000);
            if (scenario.workload == PerformanceWorkload.Child || scenario.workload == PerformanceWorkload.Soak) child = ChildSettings(0);
            if (scenario.workload == PerformanceWorkload.Laser || scenario.workload == PerformanceWorkload.Soak)
            {
                laser = new EnemyLaserSettings();
                JsonUtility.FromJsonOverwrite("{\"enableTelegraph\":true,\"telegraphDuration\":0.4,\"laserDuration\":0.8,\"fadeOutDuration\":0.1,\"followOrigin\":false}", laser);
            }
            if (UsesWaves) CreateWave();
            ScriptBehaviourUpdateOrder.AppendWorldToCurrentPlayerLoop(World);
            nextWaveRestart = Time.time + 15f;
        }
        private bool UsesWaves => scenario.workload == PerformanceWorkload.Waves || scenario.workload == PerformanceWorkload.Combat || scenario.workload == PerformanceWorkload.Soak;
        private EnemyChildAttackSettings ChildSettings(int generation)
        {
            var settings = new EnemyChildAttackSettings();
            JsonUtility.FromJsonOverwrite("{\"triggerValue\":0.4,\"destroyParent\":false,\"repeatCount\":1}", settings);
            JsonUtility.FromJsonOverwrite("{\"patternId\":\"performance-child-" + generation + "\"}", settings.Pattern);
            var profile = settings.Pattern.AuthoredShooting;
            JsonUtility.FromJsonOverwrite("{\"emissionLayoutVersion\":1,\"overrideProjectileLifetime\":true,\"projectileLifetime\":1.5,\"overrideProjectileBaseSpeed\":true,\"projectileBaseSpeed\":1.5,\"emissionLayout\":{\"type\":1,\"directionsCount\":4}}", profile);
            if (generation + 1 < scenario.childGenerations)
            {
                // SerializeReference nested pattern is assigned by Unity's ordinary serialization.
                profile.SetPerformanceChild(ChildSettings(generation + 1));
            }
            return settings;
        }
        private void CreateWave()
        {
            using var marker = WaveMarker.Auto();
            if (scenario.wavePrefab != null)
            {
                waveObject = container.InstantiatePrefab(scenario.wavePrefab, root.transform);
                var wave = waveObject.GetComponent<Wave>();
                if (wave == null) throw new InvalidOperationException("Custom wave needs a Wave component.");
                wave.Init(null);
                return;
            }
            waveObject = new GameObject("Performance Wave"); waveObject.transform.SetParent(root.transform); waveObject.SetActive(false);
            var runtimeWave = waveObject.AddComponent<Wave>();
            JsonUtility.FromJsonOverwrite("{\"enableDebugLogs\":false}", runtimeWave);
            runtimeWave.SubWavesToCreate = new List<GameObject>();
            for (int i = 0; i < scenario.subwaves; i++)
            {
                var template = new GameObject("Performance Subwave Template " + i); template.SetActive(false);
                template.transform.SetParent(root.transform); templates.Add(template);
                var wave = template.AddComponent<DirectedEnemySubWave>();
                var formation = template.AddComponent<DirectedWaveFormationBehaviour>().Configuration;
                formation.enemyPrefab = config.enemy; formation.enemyCount = scenario.enemies;
                formation.spawnInterval = .01f; formation.parentEnemiesToSubWave = true;
                formation.formationLayout = i % 2 == 0 ? DirectedWaveFormationLayout.Grid : DirectedWaveFormationLayout.Circle;
                formation.spacing = new Vector2(.3f, .3f); formation.columns = 12; formation.shapeRadius = 1.6f;
                formation.shapePointCount = scenario.enemies;
                formation.formationCenter = new Vector3(0, 1.5f + i * .5f);
                var entry = template.AddComponent<DirectedWavePhaseEntryBehaviour>().Configuration;
                entry.enabled = true; entry.settleDuration = .1f;
                entry.pathCheckpoints = new[] {
                    new DirectedWavePathCheckpoint { position = new Vector3(-2, 5), durationToNext = .25f, motionToNext = DirectedWaveSegmentMotion.Bezier },
                    new DirectedWavePathCheckpoint { position = new Vector3(0, 3), durationToNext = .25f, motionToNext = DirectedWaveSegmentMotion.CatmullRom },
                    new DirectedWavePathCheckpoint { position = new Vector3(2, 2), durationToNext = .25f } };
                var post = template.AddComponent<DirectedWavePostBehaviour>().Configuration;
                post.enabled = true; post.postStartDelay = 0;
                // Use the existing composable movement command, no benchmark movement implementation.
                post.postCommands = new[] { new DirectedWavePostCommand { type = DirectedWavePostCommandType.LocalMovement, duration = 1f } };
                post.postCommandPipelineLoop = true;
                var attack = template.AddComponent<DirectedWaveAttackBehaviour>();
                JsonUtility.FromJsonOverwrite("{\"attackSettings\":{\"serializedVersion\":12,\"attacksPerSecond\":1,\"delayAfterAttack\":0.2}}", attack);
                wave.InvalidateBehaviourComponentCache();
                runtimeWave.SubWavesToCreate.Add(template);
            }
            if (scenario.coordinator)
            {
                var coordinator = waveObject.AddComponent<WaveAttackCoordinator>();
                coordinator.Settings.globalCooldown = .15f;
                for (int i = 0; i < scenario.subwaves; i++)
                    coordinator.Settings.groups.Add(new WaveAttackCoordinatorGroup { groupName = "Performance " + i,
                        completionMode = WaveAttackGroupCompletion.Timed, duration = 1f, delayAfterGroup = .1f,
                        controlledSubWaves = new List<WaveAttackSubWaveReference> { new() { scheduleIndex = i } } });
            }
            container.InjectGameObject(waveObject);
            waveObject.SetActive(true); runtimeWave.Init(null);
        }
        public void Tick(float delta)
        {
            if (disposed) return;
            enemies.enemyList?.RemoveAll(e => e == null);
            foreach (var service in tickables) service.Tick();
            int count = ProjectileCount;
            if (count > config.maximumLiveProjectiles) throw new InvalidOperationException("Safety live-projectile budget exceeded.");
            if (!UsesWaves || scenario.workload == PerformanceWorkload.Soak)
            {
                int missing = Math.Min(512, Math.Max(0, scenario.projectiles - count));
                if (scenario.workload == PerformanceWorkload.Child)
                {
                    float interval = 1f / scenario.childRootsPerSecond;
                    if (nextChildSeed <= 0) nextChildSeed = Time.time;
                    int due = Time.time < nextChildSeed ? 0 : Math.Min(4, (int)((Time.time - nextChildSeed) / interval) + 1);
                    missing = Math.Min(missing, due);
                    nextChildSeed += missing * interval;
                }
                if (scenario.workload == PerformanceWorkload.Soak) missing = Math.Min(missing, 8);
                if (missing > 0)
                {
                    using var marker = SpawnMarker.Auto();
                    requests.Clear();
                    for (int i = 0; i < missing; i++) requests.Add(Request());
                    if (!spawner.TrySpawnBatch(requests)) throw new InvalidOperationException("Runtime spawner rejected a valid fixture.");
                }
            }
            if (waveObject != null && waveObject.TryGetComponent<WaveAttackCoordinator>(out var coordinator) && coordinator.Player != null)
            {
                int hash = 17;
                foreach (var group in coordinator.Player.ActiveGroups) hash = unchecked(hash * 31 + group.Index + 1);
                if (hash != lastActiveHash) { CoordinatorTransitions++; lastActiveHash = hash; }
            }
            if (scenario.workload == PerformanceWorkload.Combat || scenario.workload == PerformanceWorkload.Soak)
            {
                if (Time.time >= nextCombat)
                {
                    using var marker = CombatMarker.Auto();
                    nextCombat = Time.time + .35f;
                    player.LockControls(0);
                    var previous = player.CurrentShip;
                    shipSelect.SwitchShip();
                    if (previous != player.CurrentShip) shipSwitches++;
                    var active = player.CurrentShip;
                    if (active != null)
                    {
                        float extraBefore = ExtraHealth();
                        active.RestoreAbilityRecoveryResources(); active.UseAbility();
                        if (ExtraHealth() > extraBefore) extraHealthGrants++;
                        active.TryTakeConfiguredDamage(.25f);
                        active.HealHealth(1f); active.HealShield(.5f);
                    }
                    enemyScratch.Clear();
                    if (enemies.enemyList != null) enemyScratch.AddRange(enemies.enemyList);
                    enemyScratch.RemoveAll(e => e == null || e.isDead || !e.isActiveAndEnabled);
                    if (enemyScratch.Count > 0) damage.DealDamage(enemyScratch[0], active, 100f, EnemyDamageType.Default);
                    player.ReviveDefeatedShips();
                }
            }
            if (UsesWaves && (waveObject == null || scenario.workload == PerformanceWorkload.Soak && Time.time >= nextWaveRestart))
            {
                DestroyWave(); CreateWave(); nextWaveRestart = Time.time + 15f;
            }
        }
        private float ExtraHealth()
        {
            float total = 0;
            foreach (var ship in ships) if (ship != null && ship.TryGetComponent<ExtraHealthPassive>(out var extra)) total += extra.ExtraHealth;
            return total;
        }
        private EnemyProjectileSpawnRequest Request()
        {
            float x = (float)random.NextDouble() * 4 - 2, y = (float)random.NextDouble() * 6 - 3;
            var position = new Vector3(x, y);
            var direction = new Vector3((float)random.NextDouble() - .5f, -1).normalized;
            if (scenario.workload == PerformanceWorkload.Collision) { position = new Vector3(x * .2f, 1); direction = Vector3.down; }
            var pattern = scenario.workload == PerformanceWorkload.Angular ? EnemyProjectileMovementPattern.AngularTurn
                : scenario.workload == PerformanceWorkload.Orbit ? EnemyProjectileMovementPattern.Orbit : EnemyProjectileMovementPattern.Default;
            var prefab = scenario.workload == PerformanceWorkload.Homing ? config.homingBullet : config.bullet;
            bool soakChild = scenario.workload == PerformanceWorkload.Soak && random.Next(8) == 0;
            bool soakLaser = scenario.workload == PerformanceWorkload.Soak && !soakChild && random.Next(16) == 0;
            return new EnemyProjectileSpawnRequest(prefab, position, direction, 1, scenario.baseSpeed,
                EnemyProjectileSpeedBehavior.None, 0, null, EnemyProjectileSizeBehavior.Default, 0, 1, null, 1,
                position, pattern, scenario.angularSpeed, scenario.lateralSpeed, EnemyProjectileOrbitCenterMode.SourcePosition, Vector2.zero, scenario.orbitRadius, scenario.orbitRadialSpeed,
                true, scenario.lifetime, childAttack: scenario.workload == PerformanceWorkload.Soak ? soakChild ? child : null : child,
                childPrefabs: new EnemyProjectilePrefabSet(config.bullet, config.homingBullet),
                numericSeedKey: (uint)random.Next(1, int.MaxValue), laser: scenario.workload == PerformanceWorkload.Soak ? soakLaser ? laser : null : laser);
        }
        private void DestroyWave()
        {
            if (waveObject != null)
            {
                // A forced fixture restart is a despawn, not a kill. Remove its local registry
                // entries before deferred Destroy; never pass destroyed Unity objects via iDamagable.
                var ownedEnemies = waveObject.GetComponentsInChildren<Enemy>(true);
                foreach (var enemy in ownedEnemies) enemies.enemyList?.Remove(enemy);
                waveObject.SetActive(false);
                Object.Destroy(waveObject);
            }
            foreach (var template in templates) if (template != null) Object.Destroy(template);
            templates.Clear(); waveObject = null;
            // Destroying a wave also removes parented enemies; drop stale managed registry entries.
            enemies.enemyList?.RemoveAll(e => e == null);
        }
        public void Observe(PerformanceRepeat result)
        {
            result.peakProjectiles = Math.Max(result.peakProjectiles, ProjectileCount);
            result.peakEntities = Math.Max(result.peakEntities, EntityCount);
            result.peakEnemies = Math.Max(result.peakEnemies, EnemyCount);
            result.peakLasers = Math.Max(result.peakLasers, Lasers);
            result.peakChildParents = Math.Max(result.peakChildParents, ChildParents);
            result.spawnedProjectiles = Spawned;
            result.destroyedProjectiles = Math.Max(0, Spawned - ProjectileCount);
            result.omittedChildProjectiles = Omitted;
            result.coordinatorTransitions = CoordinatorTransitions;
            result.highestChildGeneration = spawner?.PerformanceHighestChildGeneration ?? 0;
            result.shipSwitches = shipSwitches; result.extraHealthGrants = extraHealthGrants;
            result.enemyKills = enemies.EnemyKillCount;
        }
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            try
            {
                if (World != null) ScriptBehaviourUpdateOrder.RemoveWorldFromCurrentPlayerLoop(World);
                if (World != null && World.IsCreated)
                {
                    World.EntityManager.CompleteAllTrackedJobs();
                }
                spawner?.Dispose();
            }
            finally
            {
                try
                {
                    if (World != null && World.IsCreated)
                    { try { if (queryCreated) projectiles.Dispose(); } finally { World.Dispose(); } }
                }
                finally
                {
                    if (root != null)
                    { root.SetActive(false); if (Application.isPlaying) Object.Destroy(root); else Object.DestroyImmediate(root); }
                    for (int i = services.Count - 1; i >= 0; i--) services[i].Dispose();
                    services.Clear(); tickables.Clear();
                    enemies.enemyList?.Clear();
                }
            }
        }
    }
}
#endif
