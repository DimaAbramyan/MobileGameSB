using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

// Enabled only on opted-in moving bodies; old waves pay no per-bullet bounce pass.
public struct EnemyProjectileBounceEnabled : IComponentData, IEnableableComponent { }

// The same sweep and reflected motion frame are used by runtime and Preview.
public static class EnemyProjectileScreenBounce
{
    public static float2 Resolve(float2 from, float2 to, float2 min, float2 max,
        EnemyChildAttackBoundaries edges, out float2 sign, out float4 contacts)
    {
        sign = new float2(1f); contacts = new float4(float.PositiveInfinity);
        to.x = Axis(from.x, to.x, min.x, max.x, (edges & EnemyChildAttackBoundaries.Left) != 0,
            (edges & EnemyChildAttackBoundaries.Right) != 0, out sign.x, out contacts.x, out contacts.y);
        to.y = Axis(from.y, to.y, min.y, max.y, (edges & EnemyChildAttackBoundaries.Bottom) != 0,
            (edges & EnemyChildAttackBoundaries.Top) != 0, out sign.y, out contacts.z, out contacts.w);
        return to;
    }

    // Finite mode follows each contact in time order. After the last allowed
    // reflection, consume the remaining movement without reflecting again.
    public static float2 ResolveLimited(float2 from, float2 to, float2 min, float2 max,
        EnemyChildAttackBoundaries edges, int remainingBounces, out float2 sign, out float4 contacts, out int completed)
    {
        sign = new float2(1f); contacts = new float4(float.PositiveInfinity); completed = 0;
        float2 position = from, travel = to - from;
        float elapsed = 0f, remainingTime = 1f;
        while (completed < remainingBounces)
        {
            if (!TryNextContact(position, travel, min, max, edges, out float first, out float2 reflection)) break;
            bool hitX = reflection.x < 0f, hitY = reflection.y < 0f;
            position += travel * first;
            elapsed += remainingTime * first;
            if (hitX)
            {
                int index = travel.x < 0f ? 0 : 1;
                contacts[index] = math.min(contacts[index], elapsed);
                position.x = travel.x < 0f ? min.x : max.x;
            }
            if (hitY)
            {
                int index = travel.y < 0f ? 2 : 3;
                contacts[index] = math.min(contacts[index], elapsed);
                position.y = travel.y < 0f ? min.y : max.y;
            }
            travel *= (1f - first) * reflection;
            sign *= reflection;
            remainingTime *= 1f - first;
            completed++;
        }
        return position + travel;
    }

    // Shared ordered contact query for moving projectiles and segmented lasers.
    // A simultaneous corner contact reflects both axes and consumes one bounce.
    public static bool TryNextContact(float2 from, float2 travel, float2 min, float2 max,
        EnemyChildAttackBoundaries edges, out float fraction, out float2 reflection)
    {
        float x = ContactFraction(from.x, travel.x, min.x, max.x,
            (edges & EnemyChildAttackBoundaries.Left) != 0, (edges & EnemyChildAttackBoundaries.Right) != 0);
        float y = ContactFraction(from.y, travel.y, min.y, max.y,
            (edges & EnemyChildAttackBoundaries.Bottom) != 0, (edges & EnemyChildAttackBoundaries.Top) != 0);
        fraction = math.min(x, y);
        reflection = new float2(x == fraction ? -1f : 1f, y == fraction ? -1f : 1f);
        return math.isfinite(fraction);
    }

    private static float ContactFraction(float from, float delta, float min, float max, bool low, bool high)
    {
        if (max - min <= .00001f || math.abs(delta) <= .0000001f) return float.PositiveInfinity;
        if (low && delta < 0f && from + delta <= min) return math.saturate((min - from) / delta);
        if (high && delta > 0f && from + delta >= max) return math.saturate((max - from) / delta);
        return float.PositiveInfinity;
    }

    private static float Axis(float from, float to, float min, float max, bool low, bool high,
        out float sign, out float firstLow, out float firstHigh)
    {
        sign = 1; firstLow = firstHigh = float.PositiveInfinity;
        float delta = to - from, span = max - min;
        if (span <= .00001f || math.abs(delta) <= .0000001f) return to;
        bool hitLow = low && delta < 0f && to <= min;
        bool hitHigh = high && delta > 0f && to >= max;
        if (!hitLow && !hitHigh) return to;
        float first = math.saturate(((hitLow ? min : max) - from) / delta);
        if (hitLow) firstLow = first; else firstHigh = first;
        if (low && high)
        {
            float second = first + span / math.abs(delta);
            if (second <= 1f) { if (hitLow) firstHigh = second; else firstLow = second; }
            // Fold arbitrarily long steps without an iteration limit or loss of
            // overshoot distance. At an exact wall, use the outgoing side.
            float coordinate = (to - min) / span;
            float segment = math.floor(coordinate + (delta > 0f ? .000001f : -.000001f));
            sign = math.abs(math.fmod(segment, 2f)) >= 1f ? -1f : 1f;
            float phase = math.fmod(math.fmod(to - min, 2f * span) + 2f * span, 2f * span);
            return min + span - math.abs(phase - span);
        }
        sign = -1;
        return 2f * (hitLow ? min : max) - to;
    }

    public static bool TryContact(in EnemyProjectileScreenState state, EnemyChildAttackBoundaries edges,
        out float fraction, out Vector3 position)
    {
        float4 times = state.BounceContacts;
        if ((edges & EnemyChildAttackBoundaries.Left) == 0) times.x = float.PositiveInfinity;
        if ((edges & EnemyChildAttackBoundaries.Right) == 0) times.y = float.PositiveInfinity;
        if ((edges & EnemyChildAttackBoundaries.Bottom) == 0) times.z = float.PositiveInfinity;
        if ((edges & EnemyChildAttackBoundaries.Top) == 0) times.w = float.PositiveInfinity;
        fraction = math.cmin(times); position = default;
        if (!math.isfinite(fraction)) return false;
        float2 point = math.lerp(state.BounceFrom, state.BounceTo, fraction);
        point = Resolve(state.BounceFrom, point, state.BounceMin, state.BounceMax, state.Settings.BounceEdges, out _, out _);
        position = new Vector3(point.x, point.y, state.BounceZ);
        return true;
    }

    public static void ReflectMotion(ref EnemyProjectileMovement movement, float2 sign, float2 oldPosition, float2 newPosition)
    {
        float handedness = sign.x * sign.y;
        movement.ForwardDirection *= sign;
        movement.LateralSpeed *= handedness;
        movement.AngularSpeedDegreesPerSecond *= handedness;
        movement.OrbitCenter = newPosition + (movement.OrbitCenter - oldPosition) * sign;
        movement.DnaMedianDirection *= sign;
        if (movement.OrbitCenterMode == EnemyProjectileOrbitCenterMode.DnaLike)
            movement.OrbitAngleRadians *= handedness;
        else
            movement.OrbitAngleRadians = math.atan2(math.sin(movement.OrbitAngleRadians) * sign.y,
                math.cos(movement.OrbitAngleRadians) * sign.x);
    }
}

[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
[UpdateAfter(typeof(ProjectileMovementSystem))]
[UpdateAfter(typeof(EnemyCompositePoseSystem))]
[UpdateAfter(typeof(EnemyProjectileSizeCleanupSystem))]
[UpdateBefore(typeof(ProjectileInterceptorSystem))]
[UpdateBefore(typeof(ProjectilePlayerCollisionSystem))]
[UpdateBefore(typeof(EnemyChildAttackSystem))]
public partial class EnemyProjectileScreenBounceSystem : SystemBase
{
    private Camera previewCamera;
    public void UpdatePreview(Camera camera)
    { previewCamera = camera; try { Update(); } finally { previewCamera = null; } }

    protected override void OnUpdate()
    {
        Camera camera = previewCamera != null ? previewCamera : Camera.main;
        var speeds = SystemAPI.GetComponentLookup<EnemyProjectileCustomSpeed>(false);
        var bodies = SystemAPI.GetComponentLookup<EnemyCompositeBounds>(false);
        var bursts = SystemAPI.GetComponentLookup<EnemyProjectileBurstAtPoint>(false);
        bool cached = false, available = false; float cachedZ = 0f; Rect bounds = default;
        foreach (var (screen, pose, previous, data, velocity, movement, entity) in SystemAPI.Query<RefRW<EnemyProjectileScreenState>,
            RefRW<LocalTransform>, RefRO<EnemyProjectilePreviousPosition>, RefRO<EnemyProjectileStaticData>,
            RefRW<EnemyProjectileVelocity>, RefRW<EnemyProjectileMovement>>()
            .WithAll<EnemyProjectileBounceEnabled>().WithNone<EnemyLaserState,EnemyProjectileHiddenAnchor>().WithEntityAccess())
        {
            screen.ValueRW.BounceContacts = new float4(float.PositiveInfinity);
            var settings = screen.ValueRO.Settings;
            if (movement.ValueRO.Pattern == EnemyProjectileMovementPattern.TargetPoint
                || !settings.BounceOnScreenBounds || settings.BounceEdges == EnemyChildAttackBoundaries.None
                || !settings.CanBounce(screen.ValueRO.CompletedBounces)
                || EntityManager.GetComponentData<EnemyProjectileResolution>(entity).Kind != EnemyProjectileResolutionKind.None) continue;
            float z = pose.ValueRO.Position.z;
            if (!cached || cachedZ != z)
            { cached = true; cachedZ = z; available = EnemyEmissionOriginSettings.TryGetGameplayBounds(camera, z, out bounds); }
            if (!available) continue;
            float2 from = previous.ValueRO.Value, to = pose.ValueRO.Position.xy;
            float2 min = new(bounds.xMin, bounds.yMin), max = new(bounds.xMax, bounds.yMax);
            if (screen.ValueRO.Entered == 0 && ProjectileInterceptorSystem.IntersectsExpandedBounds(from, to, min, max, 0f)) screen.ValueRW.Entered = 1;
            if (screen.ValueRO.Entered == 0) continue; // Outside births must enter first.
            float radius = math.abs(data.ValueRO.Radius * pose.ValueRO.Scale);
            if (bodies.HasComponent(entity))
            { var body = bodies[entity]; min += to - body.Min; max -= body.Max - to; }
            else { min += radius; max -= radius; }
            float2 sign; float4 contacts; float2 result;
            if (settings.OverrideBaseBounceTime)
            {
                result = EnemyProjectileScreenBounce.ResolveLimited(from, to, min, max, settings.BounceEdges,
                    settings.MaximumBounces - screen.ValueRO.CompletedBounces, out sign, out contacts, out int completed);
                screen.ValueRW.CompletedBounces += completed;
            }
            else
                result = EnemyProjectileScreenBounce.Resolve(from, to, min, max, settings.BounceEdges, out sign, out contacts);
            screen.ValueRW.BounceContacts = contacts;
            screen.ValueRW.BounceFrom = from; screen.ValueRW.BounceTo = to;
            screen.ValueRW.BounceMin = min; screen.ValueRW.BounceMax = max; screen.ValueRW.BounceZ = z;
            if (!math.any(math.isfinite(contacts))) continue;
            pose.ValueRW.Position = new float3(result, z);
            if (bodies.HasComponent(entity))
            {
                // Components stay owned by their controller; translate the whole
                // body now so collision/boundary queries see the bounced pose.
                float2 translation = result - to;
                var body = bodies[entity]; body.Min += translation; body.Max += translation;
                body.CenterMin += translation; body.CenterMax += translation; bodies[entity] = body;
                var members = EntityManager.GetBuffer<EnemyCompositeMember>(entity, true);
                for (int i = 0; i < members.Length; i++)
                {
                    Entity member = members[i].Entity;
                    if (!EntityManager.HasComponent<LocalTransform>(member)) continue;
                    var part = EntityManager.GetComponentData<LocalTransform>(member);
                    part.Position += new float3(translation, 0f); EntityManager.SetComponentData(member, part);
                }
            }
            velocity.ValueRW.Value *= sign;
            EnemyProjectileScreenBounce.ReflectMotion(ref movement.ValueRW, sign, to, result);
            if (bursts.HasComponent(entity))
            { var burst = bursts[entity]; burst.TargetPosition = result + (burst.TargetPosition - to) * sign; bursts[entity] = burst; }
            if (speeds.HasComponent(entity)) { var speed = speeds[entity]; speed.Direction *= sign; speeds[entity] = speed; }
            float2 facing = movement.ValueRO.Pattern == EnemyProjectileMovementPattern.Lateral
                ? velocity.ValueRO.Value + new float2(movement.ValueRO.ForwardDirection.y, -movement.ValueRO.ForwardDirection.x) * movement.ValueRO.LateralSpeed
                : velocity.ValueRO.Value;
            if (math.lengthsq(facing) > .0000001f) pose.ValueRW.Rotation = quaternion.RotateZ(math.atan2(facing.y, facing.x) + math.PI * .5f);
            // Keep Destroy Outside Screen's swept entry state on the reflected path.
            screen.ValueRW.Previous = result;
        }
    }
}
