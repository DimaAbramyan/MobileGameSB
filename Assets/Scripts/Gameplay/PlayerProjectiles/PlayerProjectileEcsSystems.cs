using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[BurstCompile]
[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
[UpdateBefore(typeof(PlayerProjectileMovementSystem))]
public partial struct PlayerProjectileHomingSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PlayerProjectileCollisionRegistryTag>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        DynamicBuffer<PlayerProjectileHomingTarget> targets = SystemAPI
            .GetSingletonBuffer<PlayerProjectileHomingTarget>(true);
        if (targets.Length == 0)
            return;

        float deltaTime = SystemAPI.Time.DeltaTime;
        foreach ((RefRW<PlayerProjectileVelocity> velocity,
                  RefRW<PlayerProjectileHoming> homing,
                  RefRO<LocalTransform> transform,
                  RefRO<PlayerProjectileResolution> resolution)
                 in SystemAPI.Query<RefRW<PlayerProjectileVelocity>,
                     RefRW<PlayerProjectileHoming>,
                     RefRO<LocalTransform>,
                     RefRO<PlayerProjectileResolution>>())
        {
            if (resolution.ValueRO.Kind != PlayerProjectileResolutionKind.None)
                continue;

            float2 currentVelocity = velocity.ValueRO.Value;
            float speed = math.length(currentVelocity);
            if (speed <= 0.0001f)
                continue;

            float2 position = transform.ValueRO.Position.xy;
            int targetIndex = FindTargetIndex(targets, homing.ValueRO.TargetId);
            if (targetIndex < 0)
            {
                targetIndex = FindNearestTargetIndex(targets, position);
                if (targetIndex < 0)
                    continue;

                homing.ValueRW.TargetId = targets[targetIndex].TargetId;
            }

            float2 targetDirection = targets[targetIndex].Position - position;
            if (math.lengthsq(targetDirection) <= 0.0001f)
                continue;

            float2 currentDirection = currentVelocity / speed;
            float2 desiredDirection = math.normalize(targetDirection);
            float signedAngle = math.atan2(
                currentDirection.x * desiredDirection.y
                - currentDirection.y * desiredDirection.x,
                math.dot(currentDirection, desiredDirection));
            float maximumTurn = math.radians(
                homing.ValueRO.RotationSpeedDegrees) * deltaTime;
            float turn = math.clamp(signedAngle, -maximumTurn, maximumTurn);
            math.sincos(turn, out float sine, out float cosine);
            velocity.ValueRW.Value = new float2(
                currentDirection.x * cosine - currentDirection.y * sine,
                currentDirection.x * sine + currentDirection.y * cosine)
                * speed;
        }
    }

    private static int FindTargetIndex(
        DynamicBuffer<PlayerProjectileHomingTarget> targets,
        int targetId)
    {
        if (targetId == 0)
            return -1;

        for (int index = 0; index < targets.Length; index++)
        {
            if (targets[index].TargetId == targetId)
                return index;
        }

        return -1;
    }

    private static int FindNearestTargetIndex(
        DynamicBuffer<PlayerProjectileHomingTarget> targets,
        float2 position)
    {
        int nearestIndex = -1;
        float nearestDistance = float.MaxValue;
        for (int index = 0; index < targets.Length; index++)
        {
            float distance = math.distancesq(position, targets[index].Position);
            if (distance >= nearestDistance)
                continue;

            nearestDistance = distance;
            nearestIndex = index;
        }

        return nearestIndex;
    }
}

[BurstCompile]
[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
public partial struct PlayerProjectileMovementSystem : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        float deltaTime = SystemAPI.Time.DeltaTime;
        foreach ((RefRW<LocalTransform> transform,
                  RefRW<PlayerProjectilePreviousPosition> previousPosition,
                  RefRO<PlayerProjectileVelocity> velocity,
                  RefRO<PlayerProjectileResolution> resolution,
                  RefRW<PostTransformMatrix> postTransformMatrix,
                  RefRW<PlayerProjectileScaleState> scaleState)
                 in SystemAPI.Query<RefRW<LocalTransform>,
                     RefRW<PlayerProjectilePreviousPosition>,
                     RefRO<PlayerProjectileVelocity>,
                     RefRO<PlayerProjectileResolution>,
                     RefRW<PostTransformMatrix>,
                     RefRW<PlayerProjectileScaleState>>())
        {
            if (resolution.ValueRO.Kind != PlayerProjectileResolutionKind.None)
                continue;

            float2 currentPosition = transform.ValueRO.Position.xy;
            previousPosition.ValueRW.Value = currentPosition;

            float2 currentVelocity = velocity.ValueRO.Value;
            transform.ValueRW.Position += new float3(
                currentVelocity.x * deltaTime,
                currentVelocity.y * deltaTime,
                0f);

            if (math.lengthsq(currentVelocity) > 0.0001f)
            {
                transform.ValueRW.Rotation = quaternion.RotateZ(
                    math.atan2(currentVelocity.y, currentVelocity.x)
                    + math.PI * 0.5f);
            }

            float2 rootScale = math.max(
                new float2(0.001f),
                scaleState.ValueRO.RootScale
                + scaleState.ValueRO.GrowthPerSecond * deltaTime);
            scaleState.ValueRW.RootScale = rootScale;
            float2 visualScale = scaleState.ValueRO.VisualScalePerRootUnit
                * rootScale;
            postTransformMatrix.ValueRW.Value = float4x4.Scale(new float3(
                visualScale.x,
                visualScale.y,
                1f));
        }
    }
}

[BurstCompile]
[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
[UpdateAfter(typeof(PlayerProjectileMovementSystem))]
  public partial struct PlayerProjectileEnemyCollisionSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PlayerProjectileCollisionRegistryTag>();
    }
}

/// <summary>
  /// Resolves the special Circular contract. It latches at the actual impact point,
  /// ticks one target, then launches toward an unvisited enemy in front of it.
  /// </summary>
  [BurstCompile]
  [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
  [UpdateAfter(typeof(PlayerProjectileEnemyCollisionSystem))]
  public partial struct PlayerProjectileCircularChainSystem : ISystem
  {
      public void OnCreate(ref SystemState state)
      {
          state.RequireForUpdate<PlayerProjectileCollisionRegistryTag>();
      }

      [BurstCompile]
      public void OnUpdate(ref SystemState state)
      {
          DynamicBuffer<PlayerProjectileHomingTarget> targets = SystemAPI
              .GetSingletonBuffer<PlayerProjectileHomingTarget>(true);
          DynamicBuffer<PlayerProjectileResolutionEvent> events = SystemAPI
              .GetSingletonBuffer<PlayerProjectileResolutionEvent>();
          ComponentLookup<PlayerProjectileStaticData> staticDataLookup = SystemAPI
              .GetComponentLookup<PlayerProjectileStaticData>(true);
          ComponentLookup<PlayerProjectileOwner> ownerLookup = SystemAPI
              .GetComponentLookup<PlayerProjectileOwner>(true);
          double currentTime = SystemAPI.Time.ElapsedTime;

          foreach ((RefRW<PlayerProjectileResolution> resolution,
                    RefRW<PlayerProjectileCircularChainState> chainState,
                    RefRO<PlayerProjectileCircularChainStaticData> chainData,
                    RefRW<PlayerProjectileVelocity> velocity,
                    RefRW<LocalTransform> transform,
                    DynamicBuffer<PlayerProjectileCircularChainVisitedTarget>
                        visitedTargets,
                    Entity entity)
                   in SystemAPI.Query<RefRW<PlayerProjectileResolution>,
                       RefRW<PlayerProjectileCircularChainState>,
                       RefRO<PlayerProjectileCircularChainStaticData>,
                       RefRW<PlayerProjectileVelocity>,
                       RefRW<LocalTransform>,
                       DynamicBuffer<PlayerProjectileCircularChainVisitedTarget>>()
                       .WithEntityAccess())
          {
              if (resolution.ValueRO.Kind != PlayerProjectileResolutionKind.None
                  || chainState.ValueRO.IsAttachedToTarget == 0)
              {
                  continue;
              }

              PlayerProjectileCircularChainState currentChain = chainState.ValueRO;
              if (!TryFindTarget(
                      targets,
                      currentChain.CurrentTargetId,
                      out PlayerProjectileHomingTarget currentTarget))
              {
                  ContinueChain(
                      ref resolution.ValueRW,
                      ref currentChain,
                      chainData.ValueRO,
                      ref velocity.ValueRW,
                      ref transform.ValueRW,
                      visitedTargets,
                      targets,
                      entity,
                      currentTime);
                  chainState.ValueRW = currentChain;
                  continue;
              }

              transform.ValueRW.Position = new float3(
                  GetAttachedPosition(currentTarget, currentChain.AttachmentLocalPosition),
                  transform.ValueRO.Position.z);
              if (currentTime < currentChain.NextHitTime)
              {
                  chainState.ValueRW = currentChain;
                  continue;
              }

              currentChain.NextHitTime = currentTime
                  + chainData.ValueRO.HitInterval;
              if (chainData.ValueRO.DamagePerHit > 0f)
              {
                  PlayerProjectileStaticData staticData = staticDataLookup[entity];
                  events.Add(new PlayerProjectileResolutionEvent
                  {
                      TargetId = currentTarget.TargetId,
                      TargetKind = PlayerProjectileCollisionTargetKind.Enemy,
                      OwnerId = ownerLookup[entity].Id,
                      DebuffSetId = staticData.DebuffSetId,
                      Damage = chainData.ValueRO.DamagePerHit,
                      DamageType = staticData.DamageType,
                      BypassesEnemyShield = staticData.BypassesEnemyShield,
                      ImpactPosition = transform.ValueRO.Position.xy
                  });
              }

              currentChain.HitsAppliedToCurrentTarget++;
              if (currentChain.HitsAppliedToCurrentTarget
                  >= chainData.ValueRO.HitsPerTarget)
              {
                  ContinueChain(
                      ref resolution.ValueRW,
                      ref currentChain,
                      chainData.ValueRO,
                      ref velocity.ValueRW,
                      ref transform.ValueRW,
                      visitedTargets,
                      targets,
                      entity,
                      currentTime);
              }

              chainState.ValueRW = currentChain;
          }
      }

      private static void ContinueChain(
          ref PlayerProjectileResolution resolution,
          ref PlayerProjectileCircularChainState chainState,
          PlayerProjectileCircularChainStaticData chainData,
          ref PlayerProjectileVelocity velocity,
          ref LocalTransform transform,
          DynamicBuffer<PlayerProjectileCircularChainVisitedTarget> visitedTargets,
          DynamicBuffer<PlayerProjectileHomingTarget> targets,
          Entity entity,
          double currentTime)
      {
          chainState.CurrentTargetId = 0;
          chainState.IsAttachedToTarget = 0;
          chainState.HitsAppliedToCurrentTarget = 0;
          chainState.NextHitTime = currentTime;
          if (visitedTargets.Length >= chainData.MaximumTargets)
          {
              resolution = new PlayerProjectileResolution
              {
                  Kind = PlayerProjectileResolutionKind.Expired,
                  TargetId = 0,
                  TargetKind = PlayerProjectileCollisionTargetKind.Enemy
              };
              return;
          }

          float2 fromPosition = transform.Position.xy;
          if (TryFindNearestTargetInCone(
                  targets,
                  visitedTargets,
                  fromPosition,
                  chainState.TravelDirection,
                  chainData.SearchRange,
                  chainData.SearchConeAngle,
                  out PlayerProjectileHomingTarget nextTarget))
          {
              LaunchTowards(
                  ref chainState,
                  ref velocity,
                  ref transform,
                  nextTarget.Position);
              return;
          }

          LaunchInRandomDirection(
              ref chainState,
              ref velocity,
              ref transform,
              entity,
              currentTime,
              chainData.RandomEscapeAngle);
      }

      private static void LaunchTowards(
          ref PlayerProjectileCircularChainState chainState,
          ref PlayerProjectileVelocity velocity,
          ref LocalTransform transform,
          float2 targetPosition)
      {
          float2 direction = targetPosition - transform.Position.xy;
          if (math.lengthsq(direction) < 0.0001f)
              return;

          SetDirection(
              ref chainState,
              ref velocity,
              ref transform,
              math.normalizesafe(direction, new float2(0f, 1f)));
      }

      private static void LaunchInRandomDirection(
          ref PlayerProjectileCircularChainState chainState,
          ref PlayerProjectileVelocity velocity,
          ref LocalTransform transform,
          Entity entity,
          double currentTime,
          float randomEscapeAngle)
      {
          float2 baseDirection = math.normalizesafe(
              chainState.TravelDirection,
              new float2(0f, 1f));
          uint seed = math.hash(new uint2(
              (uint)entity.Index,
              (uint)math.floor(currentTime * 1000d)));
          Unity.Mathematics.Random random = Unity.Mathematics.Random
              .CreateFromIndex(seed == 0u ? 1u : seed);
          float angle = random.NextFloat(
              -randomEscapeAngle * 0.5f,
              randomEscapeAngle * 0.5f);
          float2 direction = math.mul(
              quaternion.RotateZ(math.radians(angle)),
              new float3(baseDirection.x, baseDirection.y, 0f)).xy;
          SetDirection(ref chainState, ref velocity, ref transform, direction);
      }

      private static void SetDirection(
          ref PlayerProjectileCircularChainState chainState,
          ref PlayerProjectileVelocity velocity,
          ref LocalTransform transform,
          float2 direction)
      {
          float2 normalizedDirection = math.normalizesafe(
              direction,
              new float2(0f, 1f));
          chainState.TravelDirection = normalizedDirection;
          velocity.Value = normalizedDirection * chainState.TravelSpeed;
          transform.Rotation = quaternion.RotateZ(math.atan2(
              normalizedDirection.y,
              normalizedDirection.x) + math.PI * 0.5f);
      }

      private static bool TryFindNearestTargetInCone(
          DynamicBuffer<PlayerProjectileHomingTarget> targets,
          DynamicBuffer<PlayerProjectileCircularChainVisitedTarget> visitedTargets,
          float2 fromPosition,
          float2 forwardDirection,
          float searchRange,
          float searchConeAngle,
          out PlayerProjectileHomingTarget nearestTarget)
      {
          float2 forward = math.normalizesafe(forwardDirection, float2.zero);
          float maxDistanceSquared = searchRange * searchRange;
          float halfConeAngle = searchConeAngle * 0.5f;
          bool useCone = searchConeAngle < 360f && math.lengthsq(forward) > 0.0001f;
          bool foundTarget = false;
          nearestTarget = default;

          for (int index = 0; index < targets.Length; index++)
          {
              PlayerProjectileHomingTarget target = targets[index];
              if (HasVisitedTarget(visitedTargets, target.TargetId))
                  continue;

              float2 offset = target.Position - fromPosition;
              float distanceSquared = math.lengthsq(offset);
              if (distanceSquared <= 0.0001f || distanceSquared > maxDistanceSquared)
                  continue;

              if (useCone && math.degrees(math.acos(math.clamp(
                      math.dot(forward, math.normalize(offset)),
                      -1f,
                      1f))) > halfConeAngle)
              {
                  continue;
              }

              maxDistanceSquared = distanceSquared;
              nearestTarget = target;
              foundTarget = true;
          }

          return foundTarget;
      }

      private static bool HasVisitedTarget(
          DynamicBuffer<PlayerProjectileCircularChainVisitedTarget> visitedTargets,
          int targetId)
      {
          for (int index = 0; index < visitedTargets.Length; index++)
          {
              if (visitedTargets[index].TargetId == targetId)
                  return true;
          }

          return false;
      }

      private static bool TryFindTarget(
          DynamicBuffer<PlayerProjectileHomingTarget> targets,
          int targetId,
          out PlayerProjectileHomingTarget target)
      {
          for (int index = 0; index < targets.Length; index++)
          {
              if (targets[index].TargetId != targetId)
                  continue;

              target = targets[index];
              return true;
          }

          target = default;
          return false;
      }

      private static float2 GetAttachedPosition(
          PlayerProjectileHomingTarget target,
          float2 localPosition)
      {
          float2 scaledLocalPosition = localPosition * math.max(
              new float2(0.001f),
              math.abs(target.Scale));
          float2 rotatedLocalPosition = math.mul(
              quaternion.RotateZ(target.RotationRadians),
              new float3(
                  scaledLocalPosition.x,
                  scaledLocalPosition.y,
                  0f)).xy;
          return target.Position + rotatedLocalPosition;
      }
  }

public partial struct PlayerProjectileEnemyCollisionSystem : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        DynamicBuffer<PlayerProjectileCollisionTarget> targets = SystemAPI
            .GetSingletonBuffer<PlayerProjectileCollisionTarget>(true);
        DynamicBuffer<PlayerProjectileCollisionGridEntry> grid = SystemAPI
            .GetSingletonBuffer<PlayerProjectileCollisionGridEntry>(true);
        DynamicBuffer<PlayerProjectileResolutionEvent> events = SystemAPI
            .GetSingletonBuffer<PlayerProjectileResolutionEvent>();
        DynamicBuffer<PlayerProjectileEntityDamageReceiverTarget>
            entityDamageReceiverTargets = SystemAPI.GetSingletonBuffer<
                PlayerProjectileEntityDamageReceiverTarget>(true);
        DynamicBuffer<PlayerProjectileEntityDamageReceiverGridEntry>
            entityDamageReceiverGrid = SystemAPI.GetSingletonBuffer<
                PlayerProjectileEntityDamageReceiverGridEntry>(true);
        ComponentLookup<PlayerProjectileDamage> damageLookup = SystemAPI
            .GetComponentLookup<PlayerProjectileDamage>(true);
          ComponentLookup<PlayerProjectileOwner> ownerLookup = SystemAPI
              .GetComponentLookup<PlayerProjectileOwner>(true);
          ComponentLookup<PlayerProjectileVelocity> velocityLookup = SystemAPI
              .GetComponentLookup<PlayerProjectileVelocity>();
          ComponentLookup<PlayerProjectileCircularChainState> circularChainLookup = SystemAPI
              .GetComponentLookup<PlayerProjectileCircularChainState>();
          BufferLookup<PlayerProjectileCircularChainVisitedTarget> circularVisitedLookup =
              SystemAPI.GetBufferLookup<PlayerProjectileCircularChainVisitedTarget>();
          BufferLookup<PlayerProjectileHitEntityDamageReceiver>
              hitEntityDamageReceiverLookup = SystemAPI.GetBufferLookup<
                  PlayerProjectileHitEntityDamageReceiver>();
          BufferLookup<PlayerProjectileContinuousEntityDamageReceiver>
              continuousEntityDamageReceiverLookup = SystemAPI.GetBufferLookup<
                  PlayerProjectileContinuousEntityDamageReceiver>();
          BufferLookup<PlayerProjectileBallLightningDirectContact>
              ballLightningContactLookup = SystemAPI.GetBufferLookup<
                  PlayerProjectileBallLightningDirectContact>();
          BufferLookup<PlayerProjectileIgnoredCollisionTarget>
              ignoredTargetLookup = SystemAPI.GetBufferLookup<
                  PlayerProjectileIgnoredCollisionTarget>(true);
          ComponentLookup<PlayerProjectileResonanceSphereStaticData>
              resonanceSphereStaticLookup = SystemAPI.GetComponentLookup<
                  PlayerProjectileResonanceSphereStaticData>(true);
          ComponentLookup<PlayerProjectileResonanceSphereState>
              resonanceSphereStateLookup = SystemAPI.GetComponentLookup<
                  PlayerProjectileResonanceSphereState>();
          DynamicBuffer<PlayerProjectileHomingTarget> homingTargets = SystemAPI
              .GetSingletonBuffer<PlayerProjectileHomingTarget>(true);
        double currentTime = SystemAPI.Time.ElapsedTime;
        if ((targets.Length == 0 || grid.Length == 0)
            && (entityDamageReceiverTargets.Length == 0
                || entityDamageReceiverGrid.Length == 0))
            return;

        float cellSize = SystemAPI
            .GetSingleton<PlayerProjectileCollisionGridSettings>()
            .CellSize;
        if (cellSize <= 0f)
            return;

        foreach ((RefRW<PlayerProjectileResolution> resolution,
                  RefRO<PlayerProjectilePreviousPosition> previousPosition,
                  RefRO<PlayerProjectileStaticData> staticData,
                  RefRO<PlayerProjectileScaleState> scaleState,
                  RefRO<LocalTransform> transform,
                  DynamicBuffer<PlayerProjectileHitTarget> hitTargets,
                  DynamicBuffer<PlayerProjectileContinuousHitTarget>
                      continuousHitTargets,
                  Entity entity)
                 in SystemAPI.Query<RefRW<PlayerProjectileResolution>,
                     RefRO<PlayerProjectilePreviousPosition>,
                     RefRO<PlayerProjectileStaticData>,
                     RefRO<PlayerProjectileScaleState>,
                     RefRO<LocalTransform>,
                     DynamicBuffer<PlayerProjectileHitTarget>,
                     DynamicBuffer<PlayerProjectileContinuousHitTarget>>()
                     .WithEntityAccess())
        {
              if (resolution.ValueRO.Kind != PlayerProjectileResolutionKind.None)
                  continue;

              if (staticData.ValueRO.ContactMode == ProjectileContactMode.Ignore)
                  continue;

            float2 start = previousPosition.ValueRO.Value;
            float2 end = transform.ValueRO.Position.xy;
            float radius = staticData.ValueRO.Radius
                * GetCollisionScale(scaleState.ValueRO);
            float2 minimum = math.min(start, end) - radius;
            float2 maximum = math.max(start, end) + radius;
            int minimumX = (int)math.floor(minimum.x / cellSize);
            int maximumX = (int)math.floor(maximum.x / cellSize);
            int minimumY = (int)math.floor(minimum.y / cellSize);
            int maximumY = (int)math.floor(maximum.y / cellSize);
            bool didHit = false;

            for (int cellX = minimumX; cellX <= maximumX && !didHit; cellX++)
            {
                for (int cellY = minimumY; cellY <= maximumY && !didHit; cellY++)
                {
                    int gridIndex = FindFirstGridEntry(grid, cellX, cellY);
                    if (gridIndex < 0)
                        continue;

                    for (; gridIndex < grid.Length && !didHit; gridIndex++)
                    {
                        PlayerProjectileCollisionGridEntry gridEntry = grid[gridIndex];
                        if (gridEntry.CellX != cellX || gridEntry.CellY != cellY)
                            break;

                        int targetIndex = gridEntry.TargetIndex;
                        if ((uint)targetIndex >= (uint)targets.Length)
                            continue;

                        PlayerProjectileCollisionTarget target = targets[targetIndex];
                        if (!PlayerProjectileCollisionMath.IntersectsExpandedBounds(
                            start,
                            end,
                            target.Min,
                            target.Max,
                            radius))
                    {
                        continue;
                    }

                    if (ignoredTargetLookup.HasBuffer(entity)
                        && HasIgnoredTarget(
                            ignoredTargetLookup[entity],
                            target.TargetId))
                    {
                        continue;
                    }

                    if (staticData.ValueRO.ContactMode
                        == ProjectileContactMode.PierceOnce)
                    {
                        if (HasHitTarget(hitTargets, target.TargetId))
                            continue;

                        hitTargets.Add(new PlayerProjectileHitTarget
                        {
                            TargetId = target.TargetId
                        });
                        AddDamageEvent(
                            events,
                            target,
                            ownerLookup[entity].Id,
                            staticData.ValueRO,
                            damageLookup[entity].Value,
                            end);
                        continue;
                    }

                      if (staticData.ValueRO.ContactMode
                          == ProjectileContactMode.PierceContinuous)
                    {
                        if (!CanDamageContinuously(
                                continuousHitTargets,
                                target.TargetId,
                                currentTime,
                                staticData.ValueRO.ContinuousDamageInterval))
                        {
                            continue;
                        }

                        AddDamageEvent(
                            events,
                            target,
                            ownerLookup[entity].Id,
                            staticData.ValueRO,
                            damageLookup[entity].Value,
                            end);
                          continue;
                      }

                      if (staticData.ValueRO.ContactMode
                          == ProjectileContactMode.BallLightning)
                      {
                          if (!ballLightningContactLookup.HasBuffer(entity)
                              || !TryRegisterBallLightningDirectContact(
                                  ballLightningContactLookup[entity],
                                  target.TargetId,
                                  currentTime))
                          {
                              continue;
                          }

                          AddDamageEvent(
                              events,
                              target,
                              ownerLookup[entity].Id,
                              staticData.ValueRO,
                              damageLookup[entity].Value,
                              end);
                          continue;
                      }

                      if (staticData.ValueRO.ContactMode
                          == ProjectileContactMode.CircularChain)
                      {
                          if (target.Kind != PlayerProjectileCollisionTargetKind.Enemy
                              || !circularChainLookup.HasComponent(entity)
                              || !circularVisitedLookup.HasBuffer(entity))
                          {
                              continue;
                          }

                          PlayerProjectileCircularChainState chainState =
                              circularChainLookup[entity];
                          DynamicBuffer<PlayerProjectileCircularChainVisitedTarget>
                              visitedTargets = circularVisitedLookup[entity];
                          if (chainState.IsAttachedToTarget != 0
                              || HasVisitedTarget(visitedTargets, target.TargetId)
                              || !TryFindHomingTarget(
                                  homingTargets,
                                  target.TargetId,
                                  out PlayerProjectileHomingTarget homingTarget))
                          {
                              continue;
                          }

                          float2 targetScale = math.max(
                              new float2(0.001f),
                              math.abs(homingTarget.Scale));
                          float2 relativePosition = end - homingTarget.Position;
                          float2 unrotatedPosition = math.mul(
                              quaternion.RotateZ(-homingTarget.RotationRadians),
                              new float3(
                                  relativePosition.x,
                                  relativePosition.y,
                                  0f)).xy;
                          chainState.CurrentTargetId = target.TargetId;
                          chainState.AttachmentLocalPosition =
                              unrotatedPosition / targetScale;
                          chainState.HitsAppliedToCurrentTarget = 0;
                          chainState.NextHitTime = currentTime;
                          chainState.IsAttachedToTarget = 1;
                          circularChainLookup[entity] = chainState;
                          visitedTargets.Add(
                              new PlayerProjectileCircularChainVisitedTarget
                              {
                                  TargetId = target.TargetId
                              });
                          velocityLookup[entity] = new PlayerProjectileVelocity
                          {
                              Value = float2.zero
                          };
                          didHit = true;
                          break;
                      }

                      resolution.ValueRW = new PlayerProjectileResolution
                    {
                        Kind = PlayerProjectileResolutionKind.HitEnemy,
                        TargetId = target.TargetId,
                        TargetKind = target.Kind
                    };
                      didHit = true;
                      break;
                   }
               }
            }

            if (resolution.ValueRO.Kind != PlayerProjectileResolutionKind.None)
                  continue;

              for (int gridIndex = 0;
                   gridIndex < entityDamageReceiverGrid.Length && !didHit;
                   gridIndex++)
              {
                  PlayerProjectileEntityDamageReceiverGridEntry gridEntry =
                      entityDamageReceiverGrid[gridIndex];
                  if (gridEntry.CellX < minimumX || gridEntry.CellX > maximumX
                      || gridEntry.CellY < minimumY || gridEntry.CellY > maximumY
                      || gridEntry.Entity == entity)
                  {
                      continue;
                  }

                  for (int targetIndex = 0;
                       targetIndex < entityDamageReceiverTargets.Length;
                       targetIndex++)
                  {
                      PlayerProjectileEntityDamageReceiverTarget target =
                          entityDamageReceiverTargets[targetIndex];
                      if (target.Entity != gridEntry.Entity
                          || !PlayerProjectileCollisionMath.IntersectsExpandedBounds(
                              start,
                              end,
                              target.Min,
                              target.Max,
                              radius)
                          || !resonanceSphereStaticLookup.HasComponent(target.Entity)
                          || !resonanceSphereStateLookup.HasComponent(target.Entity))
                      {
                          continue;
                      }

                      if (TryHandleResonanceSphereContact(
                              entity,
                              target.Entity,
                              staticData.ValueRO,
                              damageLookup[entity].Value,
                              currentTime,
                              hitEntityDamageReceiverLookup,
                              continuousEntityDamageReceiverLookup,
                              resonanceSphereStaticLookup,
                              resonanceSphereStateLookup,
                              ref resolution.ValueRW))
                      {
                          didHit = resolution.ValueRO.Kind
                              != PlayerProjectileResolutionKind.None;
                          if (didHit)
                              break;
                      }
                  }
              }
          }
      }

       private static int FindFirstGridEntry(
           DynamicBuffer<PlayerProjectileCollisionGridEntry> grid,
           int cellX,
           int cellY)
       {
           int low = 0;
           int high = grid.Length - 1;
           while (low <= high)
           {
               int middle = low + (high - low) / 2;
               PlayerProjectileCollisionGridEntry entry = grid[middle];
               if (entry.CellX < cellX
                   || entry.CellX == cellX && entry.CellY < cellY)
               {
                   low = middle + 1;
                   continue;
               }

               high = middle - 1;
           }

           if (low >= grid.Length)
               return -1;

           PlayerProjectileCollisionGridEntry firstEntry = grid[low];
           return firstEntry.CellX == cellX && firstEntry.CellY == cellY
               ? low
               : -1;
       }

       private static bool TryRegisterBallLightningDirectContact(
          DynamicBuffer<PlayerProjectileBallLightningDirectContact> contacts,
          int targetId,
          double currentTime)
      {
          for (int index = 0; index < contacts.Length; index++)
          {
              PlayerProjectileBallLightningDirectContact contact = contacts[index];
              if (contact.TargetId != targetId)
                  continue;

              contact.LastContactTime = currentTime;
              if (contact.LastDamageTime == currentTime)
              {
                  contacts[index] = contact;
                  return false;
              }

              contact.LastDamageTime = currentTime;
              contacts[index] = contact;
              return true;
          }

          contacts.Add(new PlayerProjectileBallLightningDirectContact
          {
              TargetId = targetId,
              LastContactTime = currentTime,
              LastDamageTime = currentTime
          });
          return true;
      }

      private static bool TryHandleResonanceSphereContact(
          Entity projectile,
          Entity sphere,
          PlayerProjectileStaticData staticData,
          float damage,
          double currentTime,
          BufferLookup<PlayerProjectileHitEntityDamageReceiver> hitReceivers,
          BufferLookup<PlayerProjectileContinuousEntityDamageReceiver> continuousReceivers,
          ComponentLookup<PlayerProjectileResonanceSphereStaticData> sphereStaticData,
          ComponentLookup<PlayerProjectileResonanceSphereState> sphereStates,
          ref PlayerProjectileResolution resolution)
      {
          if (staticData.DamageType == EnemyDamageType.Resonance
              || staticData.ContactMode == ProjectileContactMode.CircularChain
              || staticData.ContactMode == ProjectileContactMode.BallLightning
              || damage <= 0f)
          {
              return false;
          }

          PlayerProjectileResonanceSphereState sphereState = sphereStates[sphere];
            if (sphereState.IsDetonating != 0
                || sphereState.IsFullChargeDetonationPending != 0)
              return false;

          switch (staticData.ContactMode)
          {
              case ProjectileContactMode.PierceOnce:
                  if (!hitReceivers.HasBuffer(projectile)
                      || HasHitEntityReceiver(hitReceivers[projectile], sphere))
                  {
                      return false;
                  }

                  hitReceivers[projectile].Add(
                      new PlayerProjectileHitEntityDamageReceiver { Entity = sphere });
                  break;

              case ProjectileContactMode.PierceContinuous:
                  if (!continuousReceivers.HasBuffer(projectile)
                      || !CanDamageEntityReceiverContinuously(
                          continuousReceivers[projectile],
                          sphere,
                          currentTime,
                          staticData.ContinuousDamageInterval))
                  {
                      return false;
                  }
                  break;

              case ProjectileContactMode.DamageAndDestroy:
              case ProjectileContactMode.ExplodeAndSpawn:
                  break;

              default:
                  return false;
          }

          PlayerProjectileResonanceSphereStaticData sphereData =
              sphereStaticData[sphere];
          sphereState.StoredDamage = math.min(
              sphereData.MaximumStoredDamage,
              sphereState.StoredDamage + damage);
            if (sphereData.MaximumStoredDamage > 0f
                && sphereState.StoredDamage >= sphereData.MaximumStoredDamage)
            {
                sphereState.IsFullChargeDetonationPending = 1;
                sphereState.FullChargeDetonationElapsed = 0f;
            }
          sphereStates[sphere] = sphereState;

          if (staticData.ContactMode == ProjectileContactMode.DamageAndDestroy)
          {
              resolution = new PlayerProjectileResolution
              {
                  Kind = PlayerProjectileResolutionKind.Expired,
                  TargetId = 0,
                  TargetKind = PlayerProjectileCollisionTargetKind.Enemy
              };
          }
          else if (staticData.ContactMode == ProjectileContactMode.ExplodeAndSpawn)
          {
              resolution = new PlayerProjectileResolution
              {
                  Kind = PlayerProjectileResolutionKind.HitEnemy,
                  TargetId = 0,
                  TargetKind = PlayerProjectileCollisionTargetKind.Enemy
              };
          }

          return true;
      }

      private static bool HasHitEntityReceiver(
          DynamicBuffer<PlayerProjectileHitEntityDamageReceiver> receivers,
          Entity receiver)
      {
          for (int index = 0; index < receivers.Length; index++)
          {
              if (receivers[index].Entity == receiver)
                  return true;
          }

          return false;
      }

      private static bool CanDamageEntityReceiverContinuously(
          DynamicBuffer<PlayerProjectileContinuousEntityDamageReceiver> receivers,
          Entity receiver,
          double currentTime,
          float interval)
      {
          for (int index = 0; index < receivers.Length; index++)
          {
              PlayerProjectileContinuousEntityDamageReceiver current = receivers[index];
              if (current.Entity != receiver)
                  continue;

              if (currentTime < current.NextDamageTime)
                  return false;

              current.NextDamageTime = currentTime + interval;
              receivers[index] = current;
              return true;
          }

          receivers.Add(new PlayerProjectileContinuousEntityDamageReceiver
          {
              Entity = receiver,
              NextDamageTime = currentTime + interval
          });
          return true;
      }

    private static bool HasHitTarget(
        DynamicBuffer<PlayerProjectileHitTarget> hitTargets,
        int targetId)
    {
        for (int index = 0; index < hitTargets.Length; index++)
        {
              if (hitTargets[index].TargetId == targetId)
                  return true;
          }

          return false;
      }

      private static bool HasVisitedTarget(
          DynamicBuffer<PlayerProjectileCircularChainVisitedTarget> visitedTargets,
          int targetId)
      {
          for (int index = 0; index < visitedTargets.Length; index++)
          {
              if (visitedTargets[index].TargetId == targetId)
                  return true;
          }

          return false;
      }

      private static bool TryFindHomingTarget(
          DynamicBuffer<PlayerProjectileHomingTarget> targets,
          int targetId,
          out PlayerProjectileHomingTarget target)
      {
          for (int index = 0; index < targets.Length; index++)
          {
              if (targets[index].TargetId != targetId)
                  continue;

              target = targets[index];
              return true;
          }

          target = default;
          return false;
      }

    private static bool CanDamageContinuously(
        DynamicBuffer<PlayerProjectileContinuousHitTarget> hitTargets,
        int targetId,
        double currentTime,
        float interval)
    {
        for (int index = 0; index < hitTargets.Length; index++)
        {
            PlayerProjectileContinuousHitTarget target = hitTargets[index];
            if (target.TargetId != targetId)
                continue;

            if (currentTime < target.NextDamageTime)
                return false;

            target.NextDamageTime = currentTime + interval;
            hitTargets[index] = target;
            return true;
        }

        hitTargets.Add(new PlayerProjectileContinuousHitTarget
        {
            TargetId = targetId,
            NextDamageTime = currentTime + interval
        });
        return true;
    }

    private static bool HasIgnoredTarget(
        DynamicBuffer<PlayerProjectileIgnoredCollisionTarget> ignoredTargets,
        int targetId)
    {
        for (int index = 0; index < ignoredTargets.Length; index++)
        {
            if (ignoredTargets[index].TargetId == targetId)
                return true;
        }

        return false;
    }

    private static void AddDamageEvent(
        DynamicBuffer<PlayerProjectileResolutionEvent> events,
        PlayerProjectileCollisionTarget target,
        int ownerId,
        PlayerProjectileStaticData staticData,
        float damage,
        float2 impactPosition)
    {
        events.Add(new PlayerProjectileResolutionEvent
        {
            TargetId = target.TargetId,
            TargetKind = target.Kind,
            OwnerId = ownerId,
            DebuffSetId = staticData.DebuffSetId,
            Damage = damage,
            DamageType = staticData.DamageType,
            BypassesEnemyShield = staticData.BypassesEnemyShield,
            ImpactPosition = impactPosition
        });
    }

    private static float GetCollisionScale(PlayerProjectileScaleState scaleState)
    {
        float2 ratio = scaleState.RootScale / math.max(
            new float2(0.001f),
            scaleState.InitialRootScale);
        return math.max(0.001f, math.max(ratio.x, ratio.y));
    }
}

[BurstCompile]
[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
[UpdateAfter(typeof(PlayerProjectileEnemyCollisionSystem))]
[UpdateBefore(typeof(PlayerContactAttackSystem))]
public partial struct PlayerProjectileExplosionSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PlayerProjectileCollisionRegistryTag>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        DynamicBuffer<PlayerProjectileCollisionTarget> targets = SystemAPI
            .GetSingletonBuffer<PlayerProjectileCollisionTarget>(true);
        DynamicBuffer<PlayerProjectileCollisionGridEntry> grid = SystemAPI
            .GetSingletonBuffer<PlayerProjectileCollisionGridEntry>(true);
        DynamicBuffer<PlayerProjectileResolutionEvent> events = SystemAPI
            .GetSingletonBuffer<PlayerProjectileResolutionEvent>();
        float cellSize = SystemAPI
            .GetSingleton<PlayerProjectileCollisionGridSettings>()
            .CellSize;
        EntityCommandBuffer commandBuffer = SystemAPI
            .GetSingleton<EndFixedStepSimulationEntityCommandBufferSystem.Singleton>()
            .CreateCommandBuffer(state.WorldUnmanaged);
        float deltaTime = SystemAPI.Time.DeltaTime;

        foreach ((RefRO<PlayerProjectileExplosionData> explosion,
                  RefRW<PlayerProjectileExplosionRemainingLifetime> lifetime,
                  RefRO<LocalTransform> transform,
                  DynamicBuffer<PlayerProjectileExplosionHitTarget> hitTargets,
                  Entity entity)
                 in SystemAPI.Query<RefRO<PlayerProjectileExplosionData>,
                     RefRW<PlayerProjectileExplosionRemainingLifetime>,
                     RefRO<LocalTransform>,
                     DynamicBuffer<PlayerProjectileExplosionHitTarget>>()
                     .WithEntityAccess())
        {
            float2 position = transform.ValueRO.Position.xy;
            float radius = explosion.ValueRO.Radius;
            if (radius > 0f && cellSize > 0f)
            {
                int minimumX = (int)math.floor((position.x - radius) / cellSize);
                int maximumX = (int)math.floor((position.x + radius) / cellSize);
                int minimumY = (int)math.floor((position.y - radius) / cellSize);
                int maximumY = (int)math.floor((position.y + radius) / cellSize);

                for (int cellX = minimumX; cellX <= maximumX; cellX++)
                {
                    for (int cellY = minimumY; cellY <= maximumY; cellY++)
                    {
                        int gridIndex = FindFirstGridEntry(grid, cellX, cellY);
                        if (gridIndex < 0)
                            continue;

                        for (; gridIndex < grid.Length; gridIndex++)
                        {
                            PlayerProjectileCollisionGridEntry gridEntry = grid[gridIndex];
                            if (gridEntry.CellX != cellX || gridEntry.CellY != cellY)
                                break;

                            if ((uint)gridEntry.TargetIndex >= (uint)targets.Length)
                                continue;

                            PlayerProjectileCollisionTarget target =
                                targets[gridEntry.TargetIndex];
                            if (HasHitTarget(hitTargets, target.TargetId)
                                || !IntersectsCircleBounds(
                                position,
                                radius,
                                target.Min,
                                target.Max))
                            {
                                continue;
                            }

                            hitTargets.Add(new PlayerProjectileExplosionHitTarget
                            {
                                TargetId = target.TargetId
                            });
                            events.Add(new PlayerProjectileResolutionEvent
                            {
                                TargetId = target.TargetId,
                                TargetKind = target.Kind,
                                OwnerId = explosion.ValueRO.OwnerId,
                                DebuffSetId = 0,
                                Damage = explosion.ValueRO.Damage,
                                DamageType = explosion.ValueRO.DamageType,
                                BypassesEnemyShield = explosion.ValueRO.BypassesEnemyShield,
                                ImpactPosition = position
                            });
                        }
                    }
                }
            }

            lifetime.ValueRW.Value -= deltaTime;
            if (lifetime.ValueRO.Value <= 0f)
                commandBuffer.DestroyEntity(entity);
        }
    }

    private static bool HasHitTarget(
        DynamicBuffer<PlayerProjectileExplosionHitTarget> hitTargets,
        int targetId)
    {
        for (int index = 0; index < hitTargets.Length; index++)
        {
            if (hitTargets[index].TargetId == targetId)
                return true;
        }

        return false;
    }

    private static int FindFirstGridEntry(
        DynamicBuffer<PlayerProjectileCollisionGridEntry> grid,
        int cellX,
        int cellY)
    {
        int low = 0;
        int high = grid.Length - 1;
        while (low <= high)
        {
            int middle = low + (high - low) / 2;
            PlayerProjectileCollisionGridEntry entry = grid[middle];
            if (entry.CellX < cellX
                || entry.CellX == cellX && entry.CellY < cellY)
            {
                low = middle + 1;
                continue;
            }

            high = middle - 1;
        }

        if (low >= grid.Length)
            return -1;

        PlayerProjectileCollisionGridEntry firstEntry = grid[low];
        return firstEntry.CellX == cellX && firstEntry.CellY == cellY
            ? low
            : -1;
    }

    private static bool IntersectsCircleBounds(
        float2 center,
        float radius,
        float2 minimum,
        float2 maximum)
    {
        float2 closestPoint = math.clamp(center, minimum, maximum);
        return math.lengthsq(center - closestPoint) <= radius * radius;
    }
}

[BurstCompile]
[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
[UpdateAfter(typeof(PlayerProjectileEnemyCollisionSystem))]
public partial struct PlayerContactAttackSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PlayerProjectileCollisionRegistryTag>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        DynamicBuffer<PlayerProjectileCollisionTarget> targets = SystemAPI
            .GetSingletonBuffer<PlayerProjectileCollisionTarget>(true);
        DynamicBuffer<PlayerProjectileCollisionGridEntry> grid = SystemAPI
            .GetSingletonBuffer<PlayerProjectileCollisionGridEntry>(true);
        DynamicBuffer<PlayerProjectileResolutionEvent> events = SystemAPI
            .GetSingletonBuffer<PlayerProjectileResolutionEvent>();
        float cellSize = SystemAPI
            .GetSingleton<PlayerProjectileCollisionGridSettings>()
            .CellSize;
        EntityCommandBuffer commandBuffer = SystemAPI
            .GetSingleton<EndFixedStepSimulationEntityCommandBufferSystem.Singleton>()
            .CreateCommandBuffer(state.WorldUnmanaged);

        foreach ((RefRO<PlayerContactAttackData> attack,
                  DynamicBuffer<PlayerContactPolygonVertex> polygonVertices,
                  DynamicBuffer<PlayerContactHitTarget> hitTargets,
                  Entity entity)
                 in SystemAPI.Query<RefRO<PlayerContactAttackData>,
                     DynamicBuffer<PlayerContactPolygonVertex>,
                     DynamicBuffer<PlayerContactHitTarget>>()
                     .WithEntityAccess())
        {
            float2 position = attack.ValueRO.Position;
            float rotationRadians = attack.ValueRO.RotationRadians;
            float cosine = math.cos(rotationRadians);
            float sine = math.sin(rotationRadians);
            if (cellSize > 0f
                && TryGetPolygonBounds(
                    polygonVertices,
                    position,
                    cosine,
                    sine,
                    out float2 minimum,
                    out float2 maximum))
            {
                int minimumX = (int)math.floor(minimum.x / cellSize);
                int maximumX = (int)math.floor(maximum.x / cellSize);
                int minimumY = (int)math.floor(minimum.y / cellSize);
                int maximumY = (int)math.floor(maximum.y / cellSize);

                for (int cellX = minimumX; cellX <= maximumX; cellX++)
                {
                    for (int cellY = minimumY; cellY <= maximumY; cellY++)
                    {
                        int gridIndex = FindFirstGridEntry(grid, cellX, cellY);
                        if (gridIndex < 0)
                            continue;

                        for (; gridIndex < grid.Length; gridIndex++)
                        {
                            PlayerProjectileCollisionGridEntry gridEntry = grid[gridIndex];
                            if (gridEntry.CellX != cellX || gridEntry.CellY != cellY)
                                break;

                            if ((uint)gridEntry.TargetIndex >= (uint)targets.Length)
                                continue;

                            PlayerProjectileCollisionTarget target =
                                targets[gridEntry.TargetIndex];
                            if (target.Kind != PlayerProjectileCollisionTargetKind.Enemy
                                || HasHitTarget(hitTargets, target.TargetId)
                                || !IntersectsPolygonBounds(
                                    polygonVertices,
                                    position,
                                    cosine,
                                    sine,
                                    target.Min,
                                    target.Max))
                            {
                                continue;
                            }

                            hitTargets.Add(new PlayerContactHitTarget
                            {
                                TargetId = target.TargetId
                            });
                            events.Add(new PlayerProjectileResolutionEvent
                            {
                                TargetId = target.TargetId,
                                TargetKind = target.Kind,
                                OwnerId = attack.ValueRO.OwnerId,
                                DebuffSetId = attack.ValueRO.DebuffSetId,
                                Damage = attack.ValueRO.Damage,
                                DamageType = attack.ValueRO.DamageType,
                                BypassesEnemyShield = attack.ValueRO.BypassesEnemyShield,
                                ImpactPosition = position
                            });
                        }
                    }
                }
            }

            commandBuffer.DestroyEntity(entity);
        }
    }

    private static bool HasHitTarget(
        DynamicBuffer<PlayerContactHitTarget> hitTargets,
        int targetId)
    {
        for (int index = 0; index < hitTargets.Length; index++)
        {
            if (hitTargets[index].TargetId == targetId)
                return true;
        }

        return false;
    }

    private static int FindFirstGridEntry(
        DynamicBuffer<PlayerProjectileCollisionGridEntry> grid,
        int cellX,
        int cellY)
    {
        int low = 0;
        int high = grid.Length - 1;
        while (low <= high)
        {
            int middle = low + (high - low) / 2;
            PlayerProjectileCollisionGridEntry entry = grid[middle];
            if (entry.CellX < cellX
                || entry.CellX == cellX && entry.CellY < cellY)
            {
                low = middle + 1;
                continue;
            }

            high = middle - 1;
        }

        if (low >= grid.Length)
            return -1;

        PlayerProjectileCollisionGridEntry firstEntry = grid[low];
        return firstEntry.CellX == cellX && firstEntry.CellY == cellY
            ? low
            : -1;
    }

    private static bool TryGetPolygonBounds(
        DynamicBuffer<PlayerContactPolygonVertex> vertices,
        float2 position,
        float cosine,
        float sine,
        out float2 minimum,
        out float2 maximum)
    {
        minimum = default;
        maximum = default;
        if (vertices.Length < 3)
            return false;

        float2 firstVertex = GetWorldVertex(vertices, 0, position, cosine, sine);
        minimum = firstVertex;
        maximum = firstVertex;
        for (int index = 1; index < vertices.Length; index++)
        {
            float2 vertex = GetWorldVertex(vertices, index, position, cosine, sine);
            minimum = math.min(minimum, vertex);
            maximum = math.max(maximum, vertex);
        }

        return true;
    }

    private static bool IntersectsPolygonBounds(
        DynamicBuffer<PlayerContactPolygonVertex> vertices,
        float2 position,
        float cosine,
        float sine,
        float2 minimum,
        float2 maximum)
    {
        if (vertices.Length < 3)
            return false;

        for (int index = 0; index < vertices.Length; index++)
        {
            float2 vertex = GetWorldVertex(vertices, index, position, cosine, sine);
            if (IsPointInsideBounds(vertex, minimum, maximum))
                return true;
        }

        float2 bottomLeft = minimum;
        float2 bottomRight = new(maximum.x, minimum.y);
        float2 topRight = maximum;
        float2 topLeft = new(minimum.x, maximum.y);
        if (IsPointInsidePolygon(
                bottomLeft,
                vertices,
                position,
                cosine,
                sine)
            || IsPointInsidePolygon(
                bottomRight,
                vertices,
                position,
                cosine,
                sine)
            || IsPointInsidePolygon(
                topRight,
                vertices,
                position,
                cosine,
                sine)
            || IsPointInsidePolygon(
                topLeft,
                vertices,
                position,
                cosine,
                sine))
        {
            return true;
        }

        for (int index = 0; index < vertices.Length; index++)
        {
            float2 edgeStart = GetWorldVertex(
                vertices,
                index,
                position,
                cosine,
                sine);
            float2 edgeEnd = GetWorldVertex(
                vertices,
                (index + 1) % vertices.Length,
                position,
                cosine,
                sine);
            if (SegmentsIntersect(edgeStart, edgeEnd, bottomLeft, bottomRight)
                || SegmentsIntersect(edgeStart, edgeEnd, bottomRight, topRight)
                || SegmentsIntersect(edgeStart, edgeEnd, topRight, topLeft)
                || SegmentsIntersect(edgeStart, edgeEnd, topLeft, bottomLeft))
            {
                return true;
            }
        }

        return false;
    }

    private static float2 GetWorldVertex(
        DynamicBuffer<PlayerContactPolygonVertex> vertices,
        int index,
        float2 position,
        float cosine,
        float sine)
    {
        float2 localPosition = vertices[index].LocalPosition;
        return position + new float2(
            localPosition.x * cosine - localPosition.y * sine,
            localPosition.x * sine + localPosition.y * cosine);
    }

    private static bool IsPointInsideBounds(
        float2 point,
        float2 minimum,
        float2 maximum)
    {
        return point.x >= minimum.x && point.x <= maximum.x
            && point.y >= minimum.y && point.y <= maximum.y;
    }

    private static bool IsPointInsidePolygon(
        float2 point,
        DynamicBuffer<PlayerContactPolygonVertex> vertices,
        float2 position,
        float cosine,
        float sine)
    {
        bool isInside = false;
        int previousIndex = vertices.Length - 1;
        for (int index = 0; index < vertices.Length; index++)
        {
            float2 current = GetWorldVertex(
                vertices,
                index,
                position,
                cosine,
                sine);
            float2 previous = GetWorldVertex(
                vertices,
                previousIndex,
                position,
                cosine,
                sine);
            if (IsPointOnSegment(point, previous, current))
                return true;

            bool spansPointY = (current.y > point.y) != (previous.y > point.y);
            if (spansPointY
                && point.x < (previous.x - current.x) * (point.y - current.y)
                    / (previous.y - current.y) + current.x)
            {
                isInside = !isInside;
            }

            previousIndex = index;
        }

        return isInside;
    }

    private static bool SegmentsIntersect(
        float2 firstStart,
        float2 firstEnd,
        float2 secondStart,
        float2 secondEnd)
    {
        float firstOrientation = Cross(
            firstEnd - firstStart,
            secondStart - firstStart);
        float secondOrientation = Cross(
            firstEnd - firstStart,
            secondEnd - firstStart);
        float thirdOrientation = Cross(
            secondEnd - secondStart,
            firstStart - secondStart);
        float fourthOrientation = Cross(
            secondEnd - secondStart,
            firstEnd - secondStart);
        const float epsilon = 0.00001f;

        if (((firstOrientation > epsilon && secondOrientation < -epsilon)
                || (firstOrientation < -epsilon && secondOrientation > epsilon))
            && ((thirdOrientation > epsilon && fourthOrientation < -epsilon)
                || (thirdOrientation < -epsilon && fourthOrientation > epsilon)))
        {
            return true;
        }

        return (math.abs(firstOrientation) <= epsilon
                && IsPointOnSegment(secondStart, firstStart, firstEnd))
            || (math.abs(secondOrientation) <= epsilon
                && IsPointOnSegment(secondEnd, firstStart, firstEnd))
            || (math.abs(thirdOrientation) <= epsilon
                && IsPointOnSegment(firstStart, secondStart, secondEnd))
            || (math.abs(fourthOrientation) <= epsilon
                && IsPointOnSegment(firstEnd, secondStart, secondEnd));
    }

    private static bool IsPointOnSegment(float2 point, float2 start, float2 end)
    {
        const float epsilon = 0.00001f;
        return math.abs(Cross(end - start, point - start)) <= epsilon
            && point.x >= math.min(start.x, end.x) - epsilon
            && point.x <= math.max(start.x, end.x) + epsilon
            && point.y >= math.min(start.y, end.y) - epsilon
            && point.y <= math.max(start.y, end.y) + epsilon;
    }

    private static float Cross(float2 first, float2 second)
    {
        return first.x * second.y - first.y * second.x;
    }
}

[BurstCompile]
[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
[UpdateAfter(typeof(PlayerProjectileEnemyCollisionSystem))]
public partial struct PlayerProjectileRangeSystem : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        float deltaTime = SystemAPI.Time.DeltaTime;
        ComponentLookup<PlayerProjectileSecondarySpawnData> secondarySpawnLookup =
            SystemAPI.GetComponentLookup<PlayerProjectileSecondarySpawnData>();
        foreach ((RefRW<PlayerProjectileRemainingRange> remainingRange,
                  RefRO<PlayerProjectileVelocity> velocity,
                  RefRW<PlayerProjectileResolution> resolution,
                  Entity entity)
                  in SystemAPI.Query<RefRW<PlayerProjectileRemainingRange>,
                      RefRO<PlayerProjectileVelocity>,
                     RefRW<PlayerProjectileResolution>>()
                     .WithEntityAccess())
        {
            if (resolution.ValueRO.Kind != PlayerProjectileResolutionKind.None)
                continue;

            float traveledDistance = math.length(velocity.ValueRO.Value) * deltaTime;
            remainingRange.ValueRW.Value -= traveledDistance;
            if (secondarySpawnLookup.HasComponent(entity))
            {
                PlayerProjectileSecondarySpawnData secondarySpawn =
                    secondarySpawnLookup[entity];
                secondarySpawn.TraveledDistance += traveledDistance;
                secondarySpawnLookup[entity] = secondarySpawn;
                if (secondarySpawn.SpawnTrigger
                        == SecondaryProjectileSpawnTrigger.AfterTravelDistance
                    && secondarySpawn.TraveledDistance
                        >= secondarySpawn.TravelDistance)
                {
                    resolution.ValueRW = new PlayerProjectileResolution
                    {
                        Kind = PlayerProjectileResolutionKind.Expired,
                        TargetId = 0,
                        TargetKind = PlayerProjectileCollisionTargetKind.Enemy
                    };
                    continue;
                }
            }

            if (remainingRange.ValueRW.Value <= 0f)
            {
                resolution.ValueRW = new PlayerProjectileResolution
                {
                    Kind = PlayerProjectileResolutionKind.Expired,
                    TargetId = 0,
                    TargetKind = PlayerProjectileCollisionTargetKind.Enemy
                };
            }
        }
    }
}

[BurstCompile]
[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
[UpdateAfter(typeof(PlayerProjectileRangeSystem))]
public partial struct PlayerProjectileLifetimeSystem : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        float deltaTime = SystemAPI.Time.DeltaTime;
        foreach ((RefRW<PlayerProjectileRemainingLifetime> lifetime,
                  RefRW<PlayerProjectileResolution> resolution)
                 in SystemAPI.Query<RefRW<PlayerProjectileRemainingLifetime>,
                       RefRW<PlayerProjectileResolution>>()
                       .WithNone<PlayerProjectileResonanceSphereState>())
        {
            if (resolution.ValueRO.Kind != PlayerProjectileResolutionKind.None)
                continue;

            lifetime.ValueRW.Value -= deltaTime;
            if (lifetime.ValueRW.Value <= 0f)
            {
                resolution.ValueRW = new PlayerProjectileResolution
                {
                    Kind = PlayerProjectileResolutionKind.Expired,
                    TargetId = 0,
                    TargetKind = PlayerProjectileCollisionTargetKind.Enemy
                };
            }
        }
    }
}

[BurstCompile]
[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
[UpdateAfter(typeof(PlayerProjectileLifetimeSystem))]
public partial struct PlayerProjectileCleanupSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PlayerProjectileCollisionRegistryTag>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        DynamicBuffer<PlayerProjectileResolutionEvent> events = SystemAPI
            .GetSingletonBuffer<PlayerProjectileResolutionEvent>();
        DynamicBuffer<PlayerProjectileExplosionRequest> explosionRequests =
            SystemAPI.GetSingletonBuffer<PlayerProjectileExplosionRequest>();
        DynamicBuffer<PlayerProjectileSecondarySpawnRequest> secondarySpawnRequests =
            SystemAPI.GetSingletonBuffer<PlayerProjectileSecondarySpawnRequest>();
        ComponentLookup<PlayerProjectileSecondarySpawnData> secondarySpawnLookup =
            SystemAPI.GetComponentLookup<PlayerProjectileSecondarySpawnData>(true);
        EntityCommandBuffer commandBuffer = SystemAPI
            .GetSingleton<EndFixedStepSimulationEntityCommandBufferSystem.Singleton>()
            .CreateCommandBuffer(state.WorldUnmanaged);

        foreach ((RefRO<PlayerProjectileResolution> resolution,
                  RefRO<PlayerProjectileStaticData> staticData,
                  RefRO<PlayerProjectileDamage> damage,
                  RefRO<PlayerProjectileOwner> owner,
                  RefRO<PlayerProjectileVelocity> velocity,
                  RefRO<LocalTransform> transform,
                  Entity entity)
                 in SystemAPI.Query<RefRO<PlayerProjectileResolution>,
                     RefRO<PlayerProjectileStaticData>,
                     RefRO<PlayerProjectileDamage>,
                     RefRO<PlayerProjectileOwner>,
                     RefRO<PlayerProjectileVelocity>,
                     RefRO<LocalTransform>>()
                     .WithEntityAccess())
        {
            if (resolution.ValueRO.Kind == PlayerProjectileResolutionKind.None)
                continue;

            if (resolution.ValueRO.Kind == PlayerProjectileResolutionKind.HitEnemy)
            {
                if (staticData.ValueRO.ContactMode
                    != ProjectileContactMode.ExplodeOnContact)
                {
                    events.Add(new PlayerProjectileResolutionEvent
                    {
                        TargetId = resolution.ValueRO.TargetId,
                        TargetKind = resolution.ValueRO.TargetKind,
                        OwnerId = owner.ValueRO.Id,
                        DebuffSetId = staticData.ValueRO.DebuffSetId,
                        Damage = damage.ValueRO.Value,
                        DamageType = staticData.ValueRO.DamageType,
                        BypassesEnemyShield = staticData.ValueRO.BypassesEnemyShield,
                        ImpactPosition = transform.ValueRO.Position.xy
                    });
                }

                if ((staticData.ValueRO.ContactMode
                        == ProjectileContactMode.ExplodeAndSpawn
                    || staticData.ValueRO.ContactMode
                        == ProjectileContactMode.ExplodeOnContact)
                    && staticData.ValueRO.ExplosionConfigId != 0)
                {
                    explosionRequests.Add(new PlayerProjectileExplosionRequest
                    {
                        ExplosionConfigId = staticData.ValueRO.ExplosionConfigId,
                        OwnerId = owner.ValueRO.Id,
                        Position = transform.ValueRO.Position.xy
                    });
                }

                if (resolution.ValueRO.TargetKind
                        == PlayerProjectileCollisionTargetKind.Enemy
                    && secondarySpawnLookup.HasComponent(entity))
                {
                    PlayerProjectileSecondarySpawnData secondarySpawn =
                        secondarySpawnLookup[entity];
                    if (secondarySpawn.SpawnTrigger
                        == SecondaryProjectileSpawnTrigger.OnEnemyContact)
                    {
                        AddSecondarySpawnRequest(
                            secondarySpawnRequests,
                            secondarySpawn,
                            owner.ValueRO.Id,
                            resolution.ValueRO.TargetId,
                            transform.ValueRO.Position.xy,
                            velocity.ValueRO.Value);
                    }
                }
            }
            else if (resolution.ValueRO.Kind == PlayerProjectileResolutionKind.Expired)
            {
                if ((staticData.ValueRO.ContactMode
                        == ProjectileContactMode.ExplodeAndSpawn
                    || staticData.ValueRO.ContactMode
                        == ProjectileContactMode.ExplodeOnContact)
                    && staticData.ValueRO.ExplodesAtMaximumRange != 0
                    && staticData.ValueRO.ExplosionConfigId != 0)
                {
                    explosionRequests.Add(new PlayerProjectileExplosionRequest
                    {
                        ExplosionConfigId = staticData.ValueRO.ExplosionConfigId,
                        OwnerId = owner.ValueRO.Id,
                        Position = transform.ValueRO.Position.xy
                    });
                }

                if (secondarySpawnLookup.HasComponent(entity))
                {
                    PlayerProjectileSecondarySpawnData secondarySpawn =
                        secondarySpawnLookup[entity];
                    if (secondarySpawn.SpawnTrigger
                        == SecondaryProjectileSpawnTrigger.AfterTravelDistance
                    )
                    {
                        AddSecondarySpawnRequest(
                            secondarySpawnRequests,
                            secondarySpawn,
                            owner.ValueRO.Id,
                            0,
                            transform.ValueRO.Position.xy,
                            velocity.ValueRO.Value);
                    }
                }
            }

            commandBuffer.DestroyEntity(entity);
        }
    }

    private static void AddSecondarySpawnRequest(
        DynamicBuffer<PlayerProjectileSecondarySpawnRequest> requests,
        PlayerProjectileSecondarySpawnData secondarySpawn,
        int ownerId,
        int triggeringTargetId,
        float2 position,
        float2 velocity)
    {
        if (secondarySpawn.SecondaryProjectileConfigId == 0)
            return;

        requests.Add(new PlayerProjectileSecondarySpawnRequest
        {
            SecondaryProjectileConfigId = secondarySpawn.SecondaryProjectileConfigId,
            OwnerId = ownerId,
            IgnoredTargetId = secondarySpawn.IgnoreTriggeringEnemy != 0
                ? triggeringTargetId
                : 0,
            Position = position,
            Direction = math.normalizesafe(velocity, new float2(0f, 1f)),
            Damage = secondarySpawn.Damage,
            Range = secondarySpawn.Range,
            Speed = secondarySpawn.Speed
        });
    }
}

public static class PlayerProjectileCollisionMath
{
    public static bool IntersectsExpandedBounds(
        float2 start,
        float2 end,
        float2 minimum,
        float2 maximum,
        float radius)
    {
        minimum -= radius;
        maximum += radius;

        float2 direction = end - start;
        float entry = 0f;
        float exit = 1f;
        if (!IntersectsAxis(start.x, direction.x, minimum.x, maximum.x, ref entry, ref exit)
            || !IntersectsAxis(
                start.y,
                direction.y,
                minimum.y,
                maximum.y,
                ref entry,
                ref exit))
        {
            return false;
        }

        return entry <= exit && exit >= 0f && entry <= 1f;
    }

    private static bool IntersectsAxis(
        float start,
        float direction,
        float minimum,
        float maximum,
        ref float entry,
        ref float exit)
    {
        if (math.abs(direction) < 0.00001f)
            return start >= minimum && start <= maximum;

        float inverseDirection = 1f / direction;
        float first = (minimum - start) * inverseDirection;
        float second = (maximum - start) * inverseDirection;
        if (first > second)
        {
            float swap = first;
            first = second;
            second = swap;
        }

        entry = math.max(entry, first);
        exit = math.min(exit, second);
        return entry <= exit;
    }
}
