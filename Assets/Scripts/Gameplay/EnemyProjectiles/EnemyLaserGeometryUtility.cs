using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

public static class EnemyLaserGeometryUtility
{
    // Length is the total travelled distance, including reflected legs. In automatic
    // mode Length Offset extends that distance beyond the first screen edge.
    // Settings are borrowed read-only; callers retain/reuse their segment buffer.
    public static void BuildPath(Vector2 start, Vector2 direction, float length, float width,
        Rect bounds, EnemyProjectileScreenSettings screen, List<EnemyLaserState> segments)
    {
        segments.Clear();
        float2 position = new(start.x, start.y);
        float2 heading = math.normalizesafe(new float2(direction.x, direction.y), new float2(0f, -1f));
        float remaining = float.IsFinite(length) ? Mathf.Max(0f, length) : 0f;
        bool bounce = screen.BounceOnScreenBounds && screen.BounceEdges != EnemyChildAttackBoundaries.None
            && bounds.width > .00001f && bounds.height > .00001f;
        int completed = 0;
        // Outside births must enter first, just like ordinary projectiles. Do not
        // mistake the near edge behind an outward-facing emitter for a contact.
        if (bounce && !bounds.Contains(start))
        {
            float enter = 0f, exit = float.PositiveInfinity;
            bounce = Clip(start.x, heading.x, bounds.xMin, bounds.xMax, ref enter, ref exit)
                && Clip(start.y, heading.y, bounds.yMin, bounds.yMax, ref enter, ref exit) && enter <= remaining;
            if (bounce && enter > 0f)
            {
                AddSegment(segments, position, heading, enter, width);
                position += heading * enter;
                remaining -= enter;
            }
        }
        float2 min = new(bounds.xMin, bounds.yMin), max = new(bounds.xMax, bounds.yMax);
        while (bounce && remaining > 0f && screen.CanBounce(completed)
            && EnemyProjectileScreenBounce.TryNextContact(position, heading * remaining, min, max,
                screen.BounceEdges, out float fraction, out float2 reflection))
        {
            float distance = remaining * fraction;
            if (distance > 0f) AddSegment(segments, position, heading, distance, width);
            position += heading * distance;
            // Snap the contact to prevent round-off creating repeated zero-length bounces.
            if (reflection.x < 0f) position.x = heading.x < 0f ? min.x : max.x;
            if (reflection.y < 0f) position.y = heading.y < 0f ? min.y : max.y;
            heading *= reflection;
            remaining -= distance;
            completed++;
        }
        if (remaining > 0f || segments.Count == 0) AddSegment(segments, position, heading, remaining, width);
    }

    private static void AddSegment(List<EnemyLaserState> segments, float2 start, float2 direction, float length, float width)
        => segments.Add(new EnemyLaserState { Start = start, Direction = direction, Length = length, Width = width });

    public static bool Hits(IReadOnlyList<EnemyLaserState> segments, EnemyProjectileCollisionTarget target)
    {
        for (int i = 0; i < segments.Count; i++) if (Hits(segments[i], target)) return true;
        return false;
    }

    // Ray/slab intersection. Outside emitters extend to the far edge after crossing the viewport.
    // A ray pointing away from the screen has zero automatic length.
    public static float ToScreenEdge(Vector2 start, Vector2 direction, Rect bounds)
    {
        if (bounds.width <= 0f || bounds.height <= 0f || direction.sqrMagnitude < .00000001f) return 0f;
        direction.Normalize();
        float enter = 0f, exit = float.PositiveInfinity;
        if (!Clip(start.x, direction.x, bounds.xMin, bounds.xMax, ref enter, ref exit)
            || !Clip(start.y, direction.y, bounds.yMin, bounds.yMax, ref enter, ref exit)) return 0f;
        return exit >= enter && !float.IsInfinity(exit) ? Mathf.Max(0f, exit) : 0f;
    }
    private static bool Clip(float start, float direction, float min, float max, ref float enter, ref float exit)
    {
        if (Mathf.Abs(direction) < .0000001f) return start >= min && start <= max;
        float a = (min - start) / direction, b = (max - start) / direction;
        enter = Mathf.Max(enter, Mathf.Min(a, b)); exit = Mathf.Min(exit, Mathf.Max(a, b));
        return exit >= enter;
    }
    public static bool Hits(EnemyLaserState beam, EnemyProjectileCollisionTarget target)
    {
        if (beam.Length <= 0f || beam.Width <= 0f) return false;
        float2 center = beam.Start + beam.Direction * (beam.Length * .5f);
        float2 halfSize = new(beam.Length * .5f, beam.Width * .5f);
        if (target.IsSegment != 0)
            return ProjectileInterceptorSystem.IntersectsMovingBox(target.SegmentStart, target.SegmentEnd,
                center, center, halfSize, beam.Direction, target.Radius);
        if (target.IsCircle != 0)
            return ProjectileInterceptorSystem.IntersectsMovingBox(target.PreviousCenter, target.Center,
                center, center, halfSize, beam.Direction, target.Radius);
        if (target.IsBox != 0)
            return BoxesOverlap(center, beam.Direction, halfSize, target.Center, target.BoxRight, target.BoxHalfSize);
        // SAT for the beam rectangle and a world-space player bounds rectangle.
        float2 targetCenter = (target.Min + target.Max) * .5f, extents = (target.Max - target.Min) * .5f;
        float2 delta = targetCenter - center, side = new(-beam.Direction.y, beam.Direction.x);
        return math.abs(delta.x) <= extents.x + math.abs(beam.Direction.x) * halfSize.x + math.abs(side.x) * halfSize.y
            && math.abs(delta.y) <= extents.y + math.abs(beam.Direction.y) * halfSize.x + math.abs(side.y) * halfSize.y
            && math.abs(math.dot(delta, beam.Direction)) <= halfSize.x + math.dot(extents, math.abs(beam.Direction))
            && math.abs(math.dot(delta, side)) <= halfSize.y + math.dot(extents, math.abs(side));
    }
    private static bool BoxesOverlap(float2 a, float2 rightA, float2 sizeA, float2 b, float2 rightB, float2 sizeB)
    {
        float2 upA = new(-rightA.y, rightA.x), upB = new(-rightB.y, rightB.x), delta = b - a;
        return Axis(delta, rightA, rightA, upA, sizeA, rightB, upB, sizeB)
            && Axis(delta, upA, rightA, upA, sizeA, rightB, upB, sizeB)
            && Axis(delta, rightB, rightA, upA, sizeA, rightB, upB, sizeB)
            && Axis(delta, upB, rightA, upA, sizeA, rightB, upB, sizeB);
    }
    private static bool Axis(float2 delta, float2 axis, float2 ra, float2 ua, float2 sa, float2 rb, float2 ub, float2 sb)
        => math.abs(math.dot(delta, axis)) <= math.abs(math.dot(ra, axis)) * sa.x + math.abs(math.dot(ua, axis)) * sa.y
            + math.abs(math.dot(rb, axis)) * sb.x + math.abs(math.dot(ub, axis)) * sb.y;
}
