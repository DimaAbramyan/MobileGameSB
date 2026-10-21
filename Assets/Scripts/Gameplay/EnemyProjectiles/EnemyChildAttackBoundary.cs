using UnityEngine;

// Segment/edge intersection shared by Runtime and Preview. Starting outside is not an event.
public static class EnemyChildAttackBoundaryIntersection
{
    public static bool TryCross(Vector3 from, Vector3 to, Rect bounds, EnemyChildAttackBoundary boundary,
        float offset, out float fraction, out EnemyChildAttackBoundary reached)
        => TryCross(from, to, bounds, EnemyChildAttackBoundarySelection.FromLegacy(boundary), offset, true, out fraction, out reached);

    public static bool TryCross(Vector3 from, Vector3 to, Rect bounds, EnemyChildAttackBoundaries boundaries,
        float offset, out float fraction, out EnemyChildAttackBoundary reached)
        => TryCross(from, to, bounds, boundaries, offset, false, out fraction, out reached);

    private static bool TryCross(Vector3 from, Vector3 to, Rect bounds, EnemyChildAttackBoundaries boundaries,
        float offset, bool legacyFiniteEdges, out float fraction, out EnemyChildAttackBoundary reached)
    {
        float first = float.PositiveInfinity;
        EnemyChildAttackBoundary firstBoundary = EnemyChildAttackBoundary.Any;
        fraction = first;
        reached = firstBoundary;
        boundaries &= EnemyChildAttackBoundaries.All;
        if (boundaries == EnemyChildAttackBoundaries.None) return false;
        float left = bounds.xMin, right = bounds.xMax, bottom = bounds.yMin, top = bounds.yMax;
        bool useLeft = (boundaries & EnemyChildAttackBoundaries.Left) != 0;
        bool useRight = (boundaries & EnemyChildAttackBoundaries.Right) != 0;
        bool useTop = (boundaries & EnemyChildAttackBoundaries.Top) != 0;
        bool useBottom = (boundaries & EnemyChildAttackBoundaries.Bottom) != 0;
        left = useLeft ? left + offset : legacyFiniteEdges ? left : float.NegativeInfinity;
        right = useRight ? right - offset : legacyFiniteEdges ? right : float.PositiveInfinity;
        bottom = useBottom ? bottom + offset : legacyFiniteEdges ? bottom : float.NegativeInfinity;
        top = useTop ? top - offset : legacyFiniteEdges ? top : float.PositiveInfinity;
        if (left > right || bottom > top) return false;
        // Unselected edges do not clip a selected edge. A bullet leaving through an
        // ignored edge can later reach a selected coordinate, even outside the viewport.
        Test(EnemyChildAttackBoundary.Top, useTop, from.y, to.y, top, from.x, to.x, left, right);
        Test(EnemyChildAttackBoundary.Bottom, useBottom, from.y, to.y, bottom, from.x, to.x, left, right);
        Test(EnemyChildAttackBoundary.Left, useLeft, from.x, to.x, left, from.y, to.y, bottom, top);
        Test(EnemyChildAttackBoundary.Right, useRight, from.x, to.x, right, from.y, to.y, bottom, top);
        fraction = first;
        reached = firstBoundary;
        return !float.IsPositiveInfinity(first);

        void Test(EnemyChildAttackBoundary edge, bool enabled, float start, float end, float coordinate,
            float tangentStart, float tangentEnd, float minimum, float maximum)
        {
            if (!enabled) return;
            float before = start - coordinate, after = end - coordinate;
            // Arrival counts; departing from or resting on the line does not. Both travel directions count.
            if (before == 0f || before > 0f && after > 0f || before < 0f && after < 0f) return;
            float t = before / (before - after);
            float tangent = Mathf.LerpUnclamped(tangentStart, tangentEnd, t);
            if (t < 0f || t > 1f || tangent < minimum || tangent > maximum || t >= first) return;
            first = t;
            firstBoundary = edge;
        }
    }
}
