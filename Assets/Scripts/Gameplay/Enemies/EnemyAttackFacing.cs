using UnityEngine;

public enum EnemyFacingMode : byte
{
    Default = 0,
    Fixed = 1,
    FollowPattern = 2
}

// Shared direction-to-pose rules for runtime and Inspector previews.
public static class EnemyAttackFacing
{
    public static Quaternion TargetRotation(Vector3 direction, float angleOffset)
    {
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;
        return Quaternion.Euler(0f, 0f, angle + angleOffset);
    }

    public static Quaternion Step(Quaternion current, Vector3 direction, float speed,
        float angleOffset, float deltaTime)
    {
        if (direction.sqrMagnitude < 0.0001f)
            return current;
        Quaternion target = TargetRotation(direction, angleOffset);
        return speed <= 0f ? target : Quaternion.RotateTowards(current, target,
            speed * Mathf.Max(0f, deltaTime));
    }

    public static Quaternion Preview(EnemyBurstAttackSettings patterns, Quaternion initial,
        Quaternion firingBasis, float elapsed, EnemyRotationSettings rotationOverride = null)
    {
        int index = patterns.FacingPatternIndex;
        if (patterns.FacingMode != EnemyFacingMode.FollowPattern || index < 0)
            return initial;
        EnemyShootingSettings shooting = patterns.GetPattern(index).Shooting;
        elapsed = Mathf.Max(0f, elapsed);
        if (patterns.FacingSpeed <= 0f)
            return Step(initial, Direction(shooting, firingBasis, elapsed, rotationOverride),
                0f, patterns.FacingAngleOffset, 0f);
        // A bounded Editor-only caller uses the same angular step as the runtime.
        int steps = Mathf.Clamp(Mathf.CeilToInt(elapsed * 60f), 1, 1200);
        float dt = elapsed / steps;
        Quaternion result = initial;
        for (int i = 1; i <= steps; i++)
        {
            float time = i * dt;
            result = Step(result, Direction(shooting, firingBasis, time, rotationOverride),
                patterns.FacingSpeed, patterns.FacingAngleOffset, dt);
        }
        return result;
    }

    private static Vector3 Direction(EnemyShootingSettings shooting, Quaternion basis,
        float elapsed, EnemyRotationSettings rotationOverride)
    {
        Quaternion evaluated = shooting.EvaluateDirectionBasis(basis, elapsed, rotationOverride);
        return shooting.GetFacingDirection(shooting.EvaluateShotDirection(evaluated, basis * Vector3.up));
    }
}
