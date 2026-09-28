using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
[UpdateAfter(typeof(PlayerProjectileEnemyCollisionSystem))]
[UpdateBefore(typeof(PlayerProjectileRangeSystem))]
public partial struct PlayerProjectileArcNodesSystem : ISystem
{
    private NativeList<ArcNode> activeNodes;

    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PlayerProjectileCollisionRegistryTag>();
        activeNodes = new NativeList<ArcNode>(Allocator.Persistent);
    }

    public void OnDestroy(ref SystemState state)
    {
        if (activeNodes.IsCreated)
            activeNodes.Dispose();
    }

    public void OnUpdate(ref SystemState state)
    {
        activeNodes.Clear();
        foreach ((RefRO<PlayerProjectileArcNodesStaticData> nodeData,
                  RefRO<PlayerProjectileOwner> owner,
                  RefRO<PlayerProjectileResolution> resolution,
                  RefRO<LocalTransform> transform,
                  Entity entity)
                 in SystemAPI.Query<RefRO<PlayerProjectileArcNodesStaticData>,
                     RefRO<PlayerProjectileOwner>,
                     RefRO<PlayerProjectileResolution>,
                     RefRO<LocalTransform>>()
                     .WithEntityAccess())
        {
            if (resolution.ValueRO.Kind != PlayerProjectileResolutionKind.None)
                continue;

            activeNodes.Add(new ArcNode
            {
                Entity = entity,
                Position = transform.ValueRO.Position.xy,
                OwnerId = owner.ValueRO.Id,
                Data = nodeData.ValueRO
            });
        }

        if (activeNodes.Length < 2)
            return;

        DynamicBuffer<PlayerProjectileCollisionTarget> targets = SystemAPI
            .GetSingletonBuffer<PlayerProjectileCollisionTarget>(true);
        DynamicBuffer<PlayerProjectileResolutionEvent> damageEvents = SystemAPI
            .GetSingletonBuffer<PlayerProjectileResolutionEvent>();
        DynamicBuffer<PlayerProjectileElectricArcVisualRequest> visualRequests =
            SystemAPI.GetSingletonBuffer<PlayerProjectileElectricArcVisualRequest>();
        ComponentLookup<PlayerProjectileArcNodesState> stateLookup = SystemAPI
            .GetComponentLookup<PlayerProjectileArcNodesState>();
        double currentTime = SystemAPI.Time.ElapsedTime;

        for (int sourceIndex = 0; sourceIndex < activeNodes.Length; sourceIndex++)
        {
            ArcNode source = activeNodes[sourceIndex];
            if (!stateLookup.HasComponent(source.Entity))
                continue;

              PlayerProjectileArcNodesState nodeState = stateLookup[source.Entity];
              if (nodeState.NextPulseTime < 0d)
              {
                  // A new pair should visibly connect as soon as it exists.
                  // Waiting for the first interval made short-lived node pairs
                  // look as though Arc Nodes had no visual effect.
                  nodeState.NextPulseTime = currentTime;
                  stateLookup[source.Entity] = nodeState;
              }

            if (currentTime < nodeState.NextPulseTime)
                continue;

            nodeState.NextPulseTime = currentTime + source.Data.PulseInterval;
            stateLookup[source.Entity] = nodeState;

            FixedList512Bytes<ArcCandidate> candidates = default;
            float rangeSquared = source.Data.ConnectionRange
                * source.Data.ConnectionRange;
            for (int otherIndex = 0; otherIndex < activeNodes.Length; otherIndex++)
            {
                ArcNode other = activeNodes[otherIndex];
                if (other.Entity.Index <= source.Entity.Index
                    || other.OwnerId != source.OwnerId
                    || other.Data.NetworkId != source.Data.NetworkId)
                {
                    continue;
                }

                float distanceSquared = math.lengthsq(other.Position - source.Position);
                if (distanceSquared > rangeSquared)
                    continue;

                AddNearestCandidate(
                    ref candidates,
                    new ArcCandidate
                    {
                        NodeIndex = otherIndex,
                        DistanceSquared = distanceSquared
                    },
                    source.Data.MaximumConnections);
            }

            for (int candidateIndex = 0;
                 candidateIndex < candidates.Length;
                 candidateIndex++)
            {
                ArcNode targetNode = activeNodes[candidates[candidateIndex].NodeIndex];
                ApplyArc(
                    source,
                    targetNode,
                    targets,
                    damageEvents,
                    visualRequests);
            }
        }
    }

    private static void ApplyArc(
        ArcNode source,
        ArcNode targetNode,
        DynamicBuffer<PlayerProjectileCollisionTarget> targets,
        DynamicBuffer<PlayerProjectileResolutionEvent> damageEvents,
        DynamicBuffer<PlayerProjectileElectricArcVisualRequest> visualRequests)
    {
        if (source.Data.VisualDuration > 0f && source.Data.VisualWidth > 0f)
        {
            visualRequests.Add(new PlayerProjectileElectricArcVisualRequest
            {
                Start = source.Position,
                End = targetNode.Position,
                Duration = source.Data.VisualDuration,
                Width = source.Data.VisualWidth,
                Segments = source.Data.VisualSegments,
                Jitter = source.Data.VisualJitter,
                Color = source.Data.VisualColor
            });
        }

        if (source.Data.DamagePerArc <= 0f)
            return;

        FixedList512Bytes<int> damagedTargets = default;
        for (int targetIndex = 0; targetIndex < targets.Length; targetIndex++)
        {
            PlayerProjectileCollisionTarget target = targets[targetIndex];
            if (target.Kind != PlayerProjectileCollisionTargetKind.Enemy
                || ContainsTarget(damagedTargets, target.TargetId)
                || !PlayerProjectileCollisionMath.IntersectsExpandedBounds(
                    source.Position,
                    targetNode.Position,
                    target.Min,
                    target.Max,
                    source.Data.HitRadius))
            {
                continue;
            }

            damagedTargets.Add(target.TargetId);
            damageEvents.Add(new PlayerProjectileResolutionEvent
            {
                TargetId = target.TargetId,
                TargetKind = PlayerProjectileCollisionTargetKind.Enemy,
                OwnerId = source.OwnerId,
                DebuffSetId = 0,
                Damage = source.Data.DamagePerArc,
                DamageType = EnemyDamageType.Electric,
                BypassesEnemyShield = 0,
                ImpactPosition = (target.Min + target.Max) * 0.5f
            });
        }
    }

    private static void AddNearestCandidate(
        ref FixedList512Bytes<ArcCandidate> candidates,
        ArcCandidate candidate,
        int maximumConnections)
    {
        if (candidates.Length < maximumConnections)
        {
            candidates.Add(candidate);
            return;
        }

        int farthestIndex = 0;
        for (int index = 1; index < candidates.Length; index++)
        {
            if (candidates[index].DistanceSquared
                > candidates[farthestIndex].DistanceSquared)
            {
                farthestIndex = index;
            }
        }

        if (candidate.DistanceSquared < candidates[farthestIndex].DistanceSquared)
            candidates[farthestIndex] = candidate;
    }

    private static bool ContainsTarget(
        in FixedList512Bytes<int> targets,
        int targetId)
    {
        for (int index = 0; index < targets.Length; index++)
        {
            if (targets[index] == targetId)
                return true;
        }

        return false;
    }

    private struct ArcNode
    {
        public Entity Entity;
        public float2 Position;
        public int OwnerId;
        public PlayerProjectileArcNodesStaticData Data;
    }

    private struct ArcCandidate
    {
        public int NodeIndex;
        public float DistanceSquared;
    }
}
