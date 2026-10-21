using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

// Managed scheduling only for projectiles that opt in. Flight/rendering stay in the existing ECS systems.
public sealed class EnemyChildAttackSimulation : IComponentData
{
    public EnemyChildAttackRunner Runner;
}

[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
[UpdateAfter(typeof(ProjectileLifetimeSystem))]
[UpdateBefore(typeof(ProjectileCleanupSystem))]
public partial class EnemyChildAttackSystem : SystemBase
{
    private EntityQuery owners;
    protected override void OnCreate() => owners = GetEntityQuery(ComponentType.ReadOnly<EnemyChildAttackSimulation>());
    protected override void OnUpdate()
    {
        using var entities = owners.ToEntityArray(Allocator.Temp);
        for (int i = 0; i < entities.Length; i++)
            EntityManager.GetComponentObject<EnemyChildAttackSimulation>(entities[i]).Runner.Tick(
                (float)SystemAPI.Time.ElapsedTime, SystemAPI.Time.DeltaTime);
    }
}

public sealed partial class EnemyChildAttackRunner
{
    private const int MaximumVolleys = 2048;
    private const int MaximumShotEvents = 128;
    private readonly EntityManager manager;
    private readonly EnemyProjectileEcsSpawner spawner;
    private readonly EntityQuery allProjectiles;
    private readonly EntityQuery targets;
    private readonly EntityQuery burstRegistries;
    private readonly Dictionary<Entity, Lineage> lineages = new();
    private readonly List<Entity> deadLineages = new();
    private readonly HashSet<Entity> pendingBurstParents = new();
    private readonly struct Lineage
    {
        public readonly EnemyChildAttackBudget Budget;
        public readonly EnemyProjectilePrefabSet Prefabs;
        public readonly int Generation;
        public readonly float DamageMultiplier;
        public readonly EnemyAttackCompletionTracker Completion;
        public Lineage(EnemyChildAttackBudget budget, EnemyProjectilePrefabSet prefabs, int generation, float damageMultiplier, EnemyAttackCompletionTracker completion)
        { Budget = budget; Prefabs = prefabs; Generation = generation; DamageMultiplier = damageMultiplier; Completion = completion; }
    }
    private readonly List<ParentState> parents = new();
    private readonly List<EnemyProjectileSpawnRequest> requests = new(EnemyChildAttackSettings.MaximumPerStep);
    private readonly System.Random random = new(937);
    private readonly System.Func<EnemyShootingSettings, float> spreadSampler;
    private readonly System.Func<EnemyShootingSettings, float> speedSampler;
    private int frameBudget, pendingCount;
    private uint fallbackNumericSeed;
    private Vector3 targetPosition;
    private bool hasTarget, warned;
    public long Omitted { get; private set; }
    public int ActiveParents => parents.Count;

    private sealed class ParentState
    {
        public Entity Parent;
        public EnemyChildAttackSettings Settings;
        public EnemyProjectilePrefabSet Prefabs;
        public EnemyChildAttackBudget Budget;
        public EnemyAttackCompletionTracker Completion;
        public int Generation, Repeat, Shots;
        public int NumericKey, BoundaryEvents, ActiveBoundaryAttacks;
        public float Birth, Distance, AttackStart, NextRepeat, DamageMultiplier;
        public Vector3 Position, Previous, Forward, CapturedOrigin, CapturedForward;
        public bool Started, Detached, Complete;
        public ParentState BoundaryOwner;
        public EnemyAttackPatternSchedule Schedule;
        public EnemyProjectileInitialSpeedState Speed;
        public EnemyAttackNumericState Numeric;
        public EnemySectorLaunchOrderRegistry SectorOrders;
        public Vector3[] LockedDirections;
        public Vector3 LockedTarget;
        public readonly List<Volley> Volleys = new(2);
    }
    private sealed class Volley
    {
        public EnemyShootingSettings Shooting;
        public float Start, Speed, Spread;
        public EnemyRandomSpeedSample NumericSpeed;
        public EnemySectorLaunchTicket LaunchOrder;
        public int Next, Count;
        public Vector3[] LockedDirections;
    }

    public EnemyChildAttackRunner(EntityManager manager, EnemyProjectileEcsSpawner spawner)
    {
        this.manager = manager; this.spawner = spawner;
        allProjectiles = manager.CreateEntityQuery(typeof(EnemyProjectileStaticData));
        targets = manager.CreateEntityQuery(typeof(EnemyProjectileCollisionRegistryTag), typeof(EnemyProjectileCollisionTarget));
        burstRegistries = manager.CreateEntityQuery(typeof(EnemyProjectileCollisionRegistryTag), typeof(EnemyProjectileBurstSpawnRequest));
        spreadSampler = SampleSpread;
        speedSampler = SampleSpeed;
    }

    public void Register(Entity entity, EnemyProjectileSpawnRequest request, float time)
    {
        if (request.ChildAttack == null && request.ChildBudget == null) return;
        var budget = request.ChildBudget ?? new EnemyChildAttackBudget();
        var prefabs = request.ChildPrefabs.Default != null ? request.ChildPrefabs : new EnemyProjectilePrefabSet(request.LegacyPrefab);
        lineages[entity] = new Lineage(budget, prefabs, request.ChildGeneration, request.DamageMultiplier, request.CompletionTracker);
        manager.SetComponentEnabled<EnemyChildAttackLineage>(entity, true);
        if (request.ChildAttack == null) return;
        if (request.ChildAttack.UsesScreenBoundary && request.ChildAttack.TriggerBoundaries == EnemyChildAttackBoundaries.None) return;
        if (request.ChildGeneration >= EnemyChildAttackSettings.MaximumDepth
            || parents.Count >= EnemyChildAttackSettings.MaximumTrackedParents)
        { Limit(1); return; }
        request.CompletionTracker?.BeginChild();
        int numericKey = unchecked((int)(request.NumericSeedKey != 0 ? request.NumericSeedKey : ++fallbackNumericSeed));
        Vector3 position = request.Position;
        if (request.ChildAttack.UsesScreenBoundary || request.Laser != null)
        {
            var pose = manager.GetComponentData<LocalTransform>(entity);
            position = new Vector3(pose.Position.x, pose.Position.y, pose.Position.z);
            if (request.ChildAttack.UsesScreenBoundary) boundaryObservers++;
        }
        parents.Add(new ParentState { Parent = entity, Settings = request.ChildAttack.SampleNumeric((uint)numericKey), Prefabs = prefabs, Completion = request.CompletionTracker,
            Budget = budget, Generation = request.ChildGeneration, DamageMultiplier = request.DamageMultiplier,
            NumericKey = numericKey,
            Numeric = new EnemyAttackNumericState(request.ChildAttack.Pattern.Shooting.NumericRandomSeed,
                numericKey, (uint)(request.ChildGeneration + 1)),
            Birth = time, Position = position, Previous = position,
            Forward = request.Direction.sqrMagnitude > .0001f ? request.Direction.normalized : Vector3.down });
    }

    public void Tick(float time, float delta, Vector3? previewTarget = null, int maximumLive = EnemyChildAttackSettings.MaximumLiveProjectiles, Camera gameplayCamera = null)
    {
#if UNITY_EDITOR
        using var profileScope = WavePreviewProfiler.Measure(WavePreviewProfiler.Area.Child);
#endif
        boundaryCamera = boundaryObservers > 0 ? (gameplayCamera != null ? gameplayCamera : Camera.main) : null;
        boundaryBoundsInitialized = false;
        RemoveDeadLineages();
        frameBudget = Mathf.Min(EnemyChildAttackSettings.MaximumPerStep, Mathf.Max(0, maximumLive - allProjectiles.CalculateEntityCount()));
        hasTarget = previewTarget.HasValue;
        targetPosition = previewTarget.GetValueOrDefault();
        if (!hasTarget && !targets.IsEmptyIgnoreFilter)
        {
            using var registries = targets.ToEntityArray(Allocator.Temp);
            for (int r = 0; r < registries.Length && !hasTarget; r++)
            {
                var buffer = manager.GetBuffer<EnemyProjectileCollisionTarget>(registries[r], true);
                for (int i = 0; i < buffer.Length; i++)
                    if (buffer[i].Kind == EnemyProjectileCollisionTargetKind.PlayerShip)
                    { targetPosition = new Vector3((buffer[i].Min.x + buffer[i].Max.x) * .5f, (buffer[i].Min.y + buffer[i].Max.y) * .5f, 0f); hasTarget = true; break; }
            }
        }
        int count = parents.Count; // New descendants never trigger recursively in this simulation step.
        for (int i = 0; i < count; i++) Update(parents[i], time, previewTarget.HasValue, delta);
        for (int i = parents.Count - 1; i >= 0; i--)
        {
            var state = parents[i];
            if (!state.Complete) continue;
            if (state.Settings.UsesScreenBoundary && state.BoundaryOwner == null) boundaryObservers--;
            if (state.BoundaryOwner != null)
            {
                state.BoundaryOwner.ActiveBoundaryAttacks--;
                FinishBoundaryObservation(state.BoundaryOwner);
            }
            state.Completion?.EndChild();
            parents.RemoveAt(i);
        }
    }

    // Burst At Point descendants keep the same lineage and safety budget, even after their source is cleaned up.
    public bool AppendBurstRequest(List<EnemyProjectileSpawnRequest> destination, EnemyProjectileBurstSpawnRequest request, EnemyBullet prefab)
    {
        Lineage lineage = default;
        if (request.HasChildLineage != 0)
        {
            if (!lineages.TryGetValue(request.SourceProjectile, out lineage)
                || lineage.Generation >= EnemyChildAttackSettings.MaximumDepth || lineage.Budget.Remaining <= 0
                || frameBudget <= 0 || allProjectiles.CalculateEntityCount() + destination.Count >= EnemyChildAttackSettings.MaximumLiveProjectiles)
            { Limit(1); return false; }
            frameBudget--; lineage.Budget.Remaining--;
        }
        destination.Add(new EnemyProjectileSpawnRequest(prefab,
            new Vector3(request.Position.x, request.Position.y), new Vector3(request.Direction.x, request.Direction.y), lineage.Budget != null ? lineage.DamageMultiplier : 1f, 0f,
            EnemyProjectileSpeedBehavior.None, 0f, null, EnemyProjectileSizeBehavior.Default, 0f, 1f, null, 1f,
            true, request.Characteristic, childPrefabs: lineage.Prefabs, childGeneration: lineage.Generation + 1, childBudget: lineage.Budget,
            completionTracker: lineage.Completion));
        return true;
    }

    private void RemoveDeadLineages()
    {
        if (lineages.Count == 0) return;
        pendingBurstParents.Clear(); deadLineages.Clear();
        using var registries = burstRegistries.ToEntityArray(Allocator.Temp);
        for (int i = 0; i < registries.Length; i++)
        {
            var requests = manager.GetBuffer<EnemyProjectileBurstSpawnRequest>(registries[i], true);
            for (int j = 0; j < requests.Length; j++)
                if (requests[j].HasChildLineage != 0) pendingBurstParents.Add(requests[j].SourceProjectile);
        }
        foreach (var entry in lineages)
            if (!manager.Exists(entry.Key) && !pendingBurstParents.Contains(entry.Key)) deadLineages.Add(entry.Key);
        for (int i = 0; i < deadLineages.Count; i++) lineages.Remove(deadLineages[i]);
    }

    private void Update(ParentState state, float time, bool preview, float delta)
    {
#if UNITY_EDITOR
        using var profileScope = WavePreviewProfiler.Measure(WavePreviewProfiler.Area.ChildTriggerUpdate);
#endif
        if (state.Settings.UsesScreenBoundary && state.BoundaryOwner == null)
        { UpdateBoundaryObserver(state, time, delta, preview); return; }
        bool exists = manager.Exists(state.Parent);
        if (exists && manager.HasComponent<EnemyLaserState>(state.Parent)
            && manager.GetComponentData<EnemyLaserState>(state.Parent).Cancelled != 0)
        { CancelVolleys(state); state.Complete = true; return; }
        var resolution = exists ? manager.GetComponentData<EnemyProjectileResolution>(state.Parent).Kind : EnemyProjectileResolutionKind.Expired;
        if (state.BoundaryOwner != null && (!exists || resolution != EnemyProjectileResolutionKind.None))
        { CancelVolleys(state); state.Complete = true; return; }
        if (exists && !state.Detached)
        {
            var pose = RefreshParentPose(state);
            if (preview && !manager.HasComponent<EnemyLaserState>(state.Parent) && state.Settings.TriggerMode == EnemyChildAttackTriggerMode.OnEvent
                && state.Settings.EventType == EnemyChildAttackEvent.HitPlayer && hasTarget)
            {
                float radius = manager.GetComponentData<EnemyProjectileStaticData>(state.Parent).Radius * pose.Scale + .05f;
                Vector3 segment = state.Position - state.Previous;
                float t = segment.sqrMagnitude > .000001f ? Mathf.Clamp01(Vector3.Dot(targetPosition - state.Previous, segment) / segment.sqrMagnitude) : 0f;
                if ((state.Previous + segment * t - targetPosition).sqrMagnitude <= radius * radius)
                {
                    resolution = EnemyProjectileResolutionKind.HitPlayer;
                    manager.SetComponentData(state.Parent, new EnemyProjectileResolution { Kind = resolution });
                }
            }
            state.Previous = state.Position;
        }
        bool trigger = ShouldTrigger(state, time, resolution, exists);
        if (!state.Started)
        {
            if (!trigger)
            { if (!exists || resolution != EnemyProjectileResolutionKind.None) state.Complete = true; return; }
            // Destroy Parent owns expiry after activation; hits/interception and expiry-triggered events still resolve normally.
            bool holdLifetime = state.Settings.DestroyParent && exists
                && (state.Settings.TriggerMode != EnemyChildAttackTriggerMode.OnEvent || state.Settings.EventType != EnemyChildAttackEvent.Expired);
            if (holdLifetime && resolution == EnemyProjectileResolutionKind.Expired
                && manager.GetComponentData<EnemyProjectileResolution>(state.Parent).LifetimeExpired != 0)
            {
                manager.SetComponentData(state.Parent, default(EnemyProjectileResolution));
                resolution = EnemyProjectileResolutionKind.None;
            }
            if (holdLifetime && resolution == EnemyProjectileResolutionKind.None)
                manager.SetComponentEnabled<EnemyChildAttackLifetimeHold>(state.Parent, true);
            state.Started = true;
            state.Detached = !exists || resolution != EnemyProjectileResolutionKind.None;
            state.CapturedOrigin = state.Settings.ResolveOrigin(state.Position, state.Forward);
            state.CapturedForward = state.Forward;
            BeginAttack(state, time);
        }
        else if (!state.Detached && (!exists || resolution != EnemyProjectileResolutionKind.None))
        {
            // External interception cancels an ordinary attack. Event attacks and lifetime expiry finish from the last pose.
            if (resolution != EnemyProjectileResolutionKind.Expired && state.Settings.TriggerMode != EnemyChildAttackTriggerMode.OnEvent)
            { CancelVolleys(state); state.Complete = true; return; }
            state.Detached = true;
        }
        if (state.Budget.Remaining <= 0)
        {
            CancelVolleys(state);
            Limit(1);
            if (state.BoundaryOwner == null && state.Settings.DestroyParent && exists && resolution == EnemyProjectileResolutionKind.None)
                manager.SetComponentData(state.Parent, new EnemyProjectileResolution { Kind = EnemyProjectileResolutionKind.Expired });
            state.Complete = true;
            return;
        }
        if (time < state.NextRepeat) return;
        var profile = state.Settings.Pattern.RuntimeSettings;
        int shotEvents = 0;
        while (state.Schedule.IsDue(time) && state.Shots < MaximumShotEvents && shotEvents++ < 32)
        {
            QueueVolley(state, profile, time, state.Schedule.IsBurstStart);
            state.Shots++;
            state.Schedule.Advance(profile, time, 1f, state.Schedule.IsLastShot ? state.Numeric.SampleCooldown(profile.AttackCooldown, profile.AttackCooldownRandom) : (float?)null);
        }
        for (int i = state.Volleys.Count - 1; i >= 0; i--)
        {
            var volley = state.Volleys[i];
            var layout = volley.Shooting.EmissionLayout;
            while (volley.Next < volley.Count && time + .00001f >= volley.Start + (layout.IsSequential ? layout.StartDelay + volley.Next * layout.LaunchInterval : 0f))
                Emit(state, volley.Shooting, volley, volley.Next++, time);
            if (volley.Next >= volley.Count) { volley.LaunchOrder.Complete(); state.Volleys.RemoveAt(i); pendingCount--; }
        }
        if ((!state.Schedule.IsComplete && state.Shots < MaximumShotEvents) || state.Volleys.Count > 0) return;
        if (!state.Schedule.IsComplete) Limit(1);
        if (++state.Repeat < state.Settings.RepeatCount)
        {
            float next = time + state.Settings.RepeatInterval;
            BeginAttack(state, next);
            return;
        }
        if (state.BoundaryOwner == null && state.Settings.DestroyParent && exists && resolution == EnemyProjectileResolutionKind.None)
            manager.SetComponentData(state.Parent, new EnemyProjectileResolution { Kind = EnemyProjectileResolutionKind.Expired });
        state.Complete = true;
    }

    private LocalTransform RefreshParentPose(ParentState state)
    {
        var pose = manager.GetComponentData<LocalTransform>(state.Parent);
        state.Position = new Vector3(pose.Position.x, pose.Position.y, pose.Position.z);
        Vector3 movement = state.Position - state.Previous;
        state.Distance += movement.magnitude;
        var velocity = manager.GetComponentData<EnemyProjectileVelocity>(state.Parent).Value;
        if (manager.HasComponent<EnemyLaserState>(state.Parent))
        {
            var direction = manager.GetComponentData<EnemyLaserState>(state.Parent).Direction;
            state.Forward = new Vector3(direction.x, direction.y);
        }
        else if (movement.sqrMagnitude > .000001f) state.Forward = movement.normalized;
        else if (math.lengthsq(velocity) > .0001f) state.Forward = new Vector3(velocity.x, velocity.y).normalized;
        return pose;
    }

    private bool ShouldTrigger(ParentState state, float time, EnemyProjectileResolutionKind resolution, bool exists)
    {
        switch (state.Settings.TriggerMode)
        {
            case EnemyChildAttackTriggerMode.AfterTime: return time + .00001f >= state.Birth + state.Settings.TriggerValue;
            case EnemyChildAttackTriggerMode.AfterDistance: return state.Distance + .00001f >= state.Settings.TriggerValue;
            default:
                switch (state.Settings.EventType)
                {
                    case EnemyChildAttackEvent.OnSpawn: return exists;
                    case EnemyChildAttackEvent.HitPlayer: return resolution == EnemyProjectileResolutionKind.HitPlayer
                        || exists && manager.HasComponent<EnemyLaserState>(state.Parent)
                            && manager.GetComponentData<EnemyLaserState>(state.Parent).HitPlayer != 0;
                    case EnemyChildAttackEvent.Intercepted: return resolution == EnemyProjectileResolutionKind.Intercepted;
                    case EnemyChildAttackEvent.Expired: return resolution == EnemyProjectileResolutionKind.Expired;
                    case EnemyChildAttackEvent.BurstAtPoint: return exists && manager.HasComponent<EnemyProjectileBurstAtPoint>(state.Parent)
                        && manager.GetComponentData<EnemyProjectileBurstAtPoint>(state.Parent).HasDetonated != 0;
                    default: return false;
                }
        }
    }

    private static void BeginAttack(ParentState state, float time)
    {
        var layout = state.Settings.Pattern.Shooting.EmissionLayout;
        if (layout.UsesPingPong) (state.SectorOrders ??= new EnemySectorLaunchOrderRegistry()).BeginAttack(layout);
        state.AttackStart = time; state.NextRepeat = time; state.Shots = 0;
        state.Speed = default; state.Numeric.BeginSpeed(); state.LockedDirections = null;
        state.Schedule.Begin(state.Settings.Pattern.RuntimeSettings, time, 1f, true, state.Numeric);
    }

    private Vector3 Origin(ParentState state) => state.Settings.OriginMode == EnemyChildAttackOriginMode.CaptureOnStart
        ? state.CapturedOrigin : state.Settings.ResolveOrigin(state.Position, state.Forward);
    private Quaternion Basis(ParentState state) => Quaternion.FromToRotation(Vector3.up,
        state.Settings.OriginMode == EnemyChildAttackOriginMode.CaptureOnStart ? state.CapturedForward : state.Forward);
    private Vector3 Position(ParentState state, EnemyShootingSettings shooting, int arm, float time)
    {
        var origin = Origin(state); var basis = Basis(state);
        // Emission Origin on the stored pattern is intentionally ignored: parent + offset is authoritative.
        return shooting.GetEmissionPositionFromOrigin(arm, origin, origin, origin, basis, basis, basis, Vector3.one, time - state.AttackStart);
    }

    private void QueueVolley(ParentState state, EnemyBurstAttackSettings profile, float time, bool burstStart)
    {
#if UNITY_EDITOR
        using var profileScope = WavePreviewProfiler.Measure(WavePreviewProfiler.Area.ChildVolley);
#endif
        if (pendingCount >= MaximumVolleys || state.Volleys.Count >= 32) { Limit(1); return; }
        var shooting = profile.Shooting;
        var numeric = state.Numeric.PrepareSpeed(shooting, burstStart && profile.RepeatBurst,
            shooting.InitialSpeedMode == EnemyProjectileInitialSpeedMode.Consistent ? state.Speed.ConsistentDelta : 0f);
        shooting = shooting.SampleGeometry(numeric.Numeric);
        var prefab = shooting.ResolveProjectilePrefab(state.Prefabs.Get(shooting.ProjectileType));
        if (prefab == null && shooting.ProjectileType != EnemyProjectileType.Laser) { Limit(1); return; }
        int count = Mathf.Min(256, shooting.DirectionCount);
        bool aimed = state.Settings.FireMode == DirectedWaveAttackFireMode.Aimed;
        if (aimed && !hasTarget) { Limit(1); return; }
        var launchOrder = state.SectorOrders != null ? state.SectorOrders.Reserve(shooting.EmissionLayout)
            : new EnemySectorLaunchTicket(shooting.EmissionLayout.ReverseOrder);
        if (aimed && state.Settings.AimMode != DirectedWaveAttackAimMode.AimContinuous
            && (state.LockedDirections == null || state.LockedDirections.Length != count || shooting.UsesGeometryRandomization || state.Settings.AimMode == DirectedWaveAttackAimMode.AimOnBurst && profile.RepeatBurst && burstStart))
        {
            if (state.LockedDirections == null
                || !shooting.UsesGeometryRandomization && state.LockedDirections.Length != count
                || state.Settings.AimMode == DirectedWaveAttackAimMode.AimOnBurst && profile.RepeatBurst && burstStart)
                state.LockedTarget = targetPosition;
            state.LockedDirections = new Vector3[count];
            for (int i = 0; i < count; i++)
            {
                int arm = shooting.EmissionLayout.IsSequential ? launchOrder.GetLaunchIndex(shooting.EmissionLayout, i) : i;
                var directionShooting = shooting.SampleGeometry(numeric.Numeric.ForDirection(arm), true);
                state.LockedDirections[i] = (state.LockedTarget - Position(state, directionShooting, arm, time)).normalized;
            }
        }
        var authoring = prefab != null ? prefab.GetComponent<EnemyProjectileAuthoring>() : null;
        float speed = state.Speed.GetVolleySpeed(shooting, authoring != null ? authoring.BaseSpeed : 0f);
        state.Volleys.Add(new Volley { Start = time, Speed = speed, LaunchOrder = launchOrder, Shooting = shooting,
            NumericSpeed = numeric, Spread = shooting.UsesShotGun ? 0f : SampleSpread(shooting),
            Count = count, LockedDirections = state.LockedDirections });
        pendingCount++;
        state.Speed.AdvanceAfterVolley(shooting);
        if (shooting.DirectionCount > count) Limit(shooting.DirectionCount - count);
    }

    private void Emit(ParentState state, EnemyShootingSettings shooting, Volley volley, int order, float time)
    {
#if UNITY_EDITOR
        using var profileScope = WavePreviewProfiler.Measure(WavePreviewProfiler.Area.ChildEmit);
#endif
        int arm = shooting.EmissionLayout.IsSequential ? volley.LaunchOrder.GetLaunchIndex(shooting.EmissionLayout, order) : order;
        shooting = shooting.SampleGeometry(volley.NumericSpeed.Numeric.ForDirection(arm), true);
        int available = Mathf.Min(frameBudget, state.Budget.Remaining);
        int requestedCount = shooting.ProjectilesPerDirection;
        if (available <= 0) { Limit(requestedCount); return; }
        Vector3 position = Position(state, shooting, arm, time);
        bool aimed = state.Settings.FireMode == DirectedWaveAttackFireMode.Aimed;
        bool stationary = state.Settings.FireMode == DirectedWaveAttackFireMode.Stationary;
        Vector3 requested = Basis(state) * Vector3.up;
        bool target = aimed;
        if (aimed) requested = volley.LockedDirections != null ? volley.LockedDirections[order] : targetPosition - position;
        if (stationary)
        {
            target = state.Settings.Stationary.DirectionMode == EnemyStationaryDirectionMode.WorldPoint;
            requested = target ? (Vector3)state.Settings.Stationary.WorldPoint - position : state.Settings.Stationary.Direction;
        }
        if (state.Settings.FireMode == DirectedWaveAttackFireMode.None) return;
        Vector3 direction = shooting.GetEmissionDirection(arm, Basis(state), time - state.AttackStart, requested, target, stationary: stationary);
        requests.Clear();
        EnemyAttackProjectileBuilder.AppendEmitter(requests, state.Prefabs.Get(shooting.ProjectileType), shooting,
            position, Origin(state), direction, state.DamageMultiplier, volley.Speed, volley.Spread, state.Prefabs, spreadSampler,
            available, state.Generation + 1, state.Budget, speedSampler, state.Completion, volley.NumericSpeed, arm * shooting.ProjectilesPerDirection,
            directionIndex: arm,
            laserOrigin: new EnemyLaserOrigin(null, state.Position, null, boundaryCamera,
                entity: state.Parent, freezeTranslation: state.Settings.OriginMode == EnemyChildAttackOriginMode.CaptureOnStart));
        if (spawner.TrySpawnBatch(requests))
        { frameBudget -= requests.Count; state.Budget.Remaining -= requests.Count; }
        else Limit(requests.Count);
        if (requestedCount > requests.Count) Limit(requestedCount - requests.Count);
    }

    private float SampleSpeed(EnemyShootingSettings shooting) => Mathf.Lerp(shooting.RandomInitialSpeedMinimum, shooting.RandomInitialSpeedMaximum, (float)random.NextDouble());
    private float SampleSpread(EnemyShootingSettings shooting) => shooting.ShootingMode == EnemyProjectileFireMode.Random ? shooting.GenerateRandomSpreadAngle(random) : 0f;
    private void Limit(int count)
    {
        Omitted += count;
        if (warned) return;
        warned = true;
        Debug.LogWarning("Child Attack Pattern: safety limits reached or a required projectile/player reference is missing. Some child launches were omitted.");
    }

    public void Dispose()
    {
        for (int i = 0; i < parents.Count; i++) { CancelVolleys(parents[i]); parents[i].Completion?.EndChild(); }
        parents.Clear(); lineages.Clear(); allProjectiles.Dispose(); targets.Dispose(); burstRegistries.Dispose();
    }

    private void CancelVolleys(ParentState state)
    {
        for (int i = 0; i < state.Volleys.Count; i++) state.Volleys[i].LaunchOrder.Cancel();
        pendingCount -= state.Volleys.Count;
        state.Volleys.Clear();
    }
}
