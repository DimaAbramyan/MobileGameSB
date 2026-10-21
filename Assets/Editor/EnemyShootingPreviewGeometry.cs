using UnityEngine;

// Scene drawing and verification use the runtime emission/direction methods.
// Stack members may share identical positions and directions: never spread them just for display.
public struct EnemyPreviewEmitter
{
    public int Index, LaunchOrder;
    public float LaunchDelay;
    public Vector3 Position, BaseDirection;
}

public struct EnemyPreviewProjectile
{
    public int StackIndex, FanIndex;
    public Vector3 Position, Direction, DnaMedian;
    public float SpreadAngle;
}

public struct EnemyFacingPreviewGuide
{
    public Vector3 Direction;
    public float TurnAngle;
    public bool PingPong;
}

public static class EnemyShootingPreviewGeometry
{
    public static EnemyFacingPreviewGuide GetFacingGuide(EnemyBurstAttackSettings patterns, Quaternion initial,
        Quaternion basis, float time, EnemyRotationSettings rotationOverride = null,
        Vector3? lockedAim = null, bool resolvedDirection = false)
    {
        using var profileScope = WavePreviewProfiler.Measure(WavePreviewProfiler.Area.Facing);
        Quaternion pose = FacingPose(patterns, initial, basis, time, rotationOverride, lockedAim, resolvedDirection);
        float turn;
        bool rotation = patterns.FacingMode == EnemyFacingMode.Rotation;
        if (rotation)
            turn = patterns.FacingRotation.EvaluateAngle(time + .05f) - patterns.FacingRotation.EvaluateAngle(time);
        else
        {
            Quaternion next = FacingPose(patterns, initial, basis, time + .05f, rotationOverride, lockedAim, resolvedDirection);
            turn = Vector3.SignedAngle(pose * Vector3.up, next * Vector3.up, Vector3.forward);
        }
        return new EnemyFacingPreviewGuide { Direction = pose * Vector3.up, TurnAngle = turn,
            PingPong = rotation && patterns.FacingRotation.RotationMode == FourWayEnemyRotationMode.PingPongByAngle };
    }

    private static Quaternion FacingPose(EnemyBurstAttackSettings patterns, Quaternion initial, Quaternion basis,
        float time, EnemyRotationSettings rotationOverride, Vector3? lockedAim, bool resolvedDirection)
    {
        int index = patterns.FacingPatternIndex;
        if (patterns.FacingMode == EnemyFacingMode.FollowPattern && index >= 0 && lockedAim.HasValue)
        {
            EnemyShootingSettings shooting = patterns.GetPattern(index).Shooting;
            if (resolvedDirection || shooting.EmissionLayout.Type == EnemyEmissionLayoutType.Point)
                return EnemyAttackFacing.Step(initial, shooting.GetFacingDirection(lockedAim.Value),
                    patterns.FacingSpeed, patterns.FacingAngleOffset, Mathf.Max(0f, time));
        }
        return EnemyAttackFacing.Preview(patterns, initial, basis, time, rotationOverride);
    }

    public static Vector3 ResolveOrigin(EnemyShootingSettings shooting, EnemyEmissionPreviewFrame frame)
    {
        if (frame.Origin.HasValue) return frame.Origin.Value;
        Rect bounds = frame.GameplayBounds ?? default;
        if (!frame.GameplayBounds.HasValue && shooting.EmissionOrigin.RequiresBounds)
            EnemyEmissionOriginSettings.TryGetGameplayBounds(Camera.main, frame.EnemyPosition.z, out bounds);
        return shooting.EmissionOrigin.Resolve(frame.EnemyPosition,
            frame.PlayerPosition ?? frame.Target ?? frame.EnemyPosition, bounds);
    }

    public static EnemyPreviewEmitter GetEmitter(EnemyShootingSettings shooting, int index,
        EnemyEmissionPreviewFrame frame, Quaternion basis, float time,
        EnemyRotationSettings rotationOverride = null, Vector3? requestedDirection = null, bool aimed = false, bool stationary = false)
    {
        using var profileScope = WavePreviewProfiler.Measure(WavePreviewProfiler.Area.EmitterGeometry);
        Vector3 origin = shooting.GetEmissionPositionFromOrigin(index, frame.ShootingPoint, frame.EnemyPosition,
            ResolveOrigin(shooting, frame), basis, frame.PointBasis, frame.EnemyBasis, frame.Scale, time, rotationOverride);
        Vector3 requested = requestedDirection ?? basis * Vector3.up;
        if (aimed && frame.Target.HasValue)
        {
            Vector3 aimOrigin = frame.HasCapturedAim
                ? shooting.GetEmissionPositionFromOrigin(index, frame.CapturedShootingPoint, frame.CapturedEnemyPosition,
                    frame.CapturedOrigin ?? ResolveOrigin(shooting, frame),
                    basis, frame.PointBasis, frame.EnemyBasis, frame.Scale, frame.CapturedRotationTime, rotationOverride)
                : origin;
            requested = frame.Target.Value - aimOrigin;
        }
        return new EnemyPreviewEmitter { Index = index, Position = origin,
            BaseDirection = shooting.GetEmissionDirection(index, basis, time, requested, aimed, rotationOverride, stationary),
            LaunchOrder = shooting.EmissionLayout.GetLaunchOrder(index), LaunchDelay = shooting.EmissionLayout.GetLaunchDelay(index) };
    }

    public static EnemyPreviewProjectile GetProjectile(EnemyShootingSettings shooting, EnemyPreviewEmitter emitter, int index, EnemyNumericSample numeric = default)
    {
        int fanCount = shooting.UsesFan ? shooting.FanProjectileCount : 1;
        int stackIndex = index / fanCount;
        float spread = shooting.ShootingMode == EnemyProjectileFireMode.Random
            ? shooting.UsesShotGun ? shooting.GetShotGunPreviewRandomAngle(stackIndex) : shooting.RandomPreviewAngle : 0f;
        if (shooting.ShootingMode == EnemyProjectileFireMode.Random && shooting.UsesExtendedNumericRandomization && numeric.Active)
            spread = shooting.NumericSpread(numeric.ForProjectile(shooting.UsesShotGun ? stackIndex * fanCount : 0));
        return new EnemyPreviewProjectile { Position = emitter.Position, StackIndex = stackIndex, FanIndex = index % fanCount,
            SpreadAngle = spread, Direction = shooting.GetProjectileDirection(emitter.BaseDirection, 0, index, spread, numeric),
            DnaMedian = shooting.UsesDnaOrbit ? shooting.GetDnaMedianDirection(emitter.BaseDirection, 0, spread, numeric) : Vector3.zero };
    }

    public static Vector3 SectorStartDirection(EnemyShootingSettings shooting, EnemyPreviewEmitter first)
        => Quaternion.Euler(0f, 0f, shooting.EmissionLayout.StartAngle - shooting.EmissionLayout.GetAngle(0)) * first.BaseDirection;

    public static Vector3 SectorEndDirection(EnemyShootingSettings shooting, EnemyPreviewEmitter first)
        => Quaternion.Euler(0f, 0f, shooting.EmissionLayout.SectorSweep) * SectorStartDirection(shooting, first);

    public static int CoincidentStackCount(EnemyShootingSettings shooting)
        => shooting.UsesShotGun && shooting.ShootingMode != EnemyProjectileFireMode.Random
            ? shooting.ProjectilesPerDirection / (shooting.UsesFan ? shooting.FanProjectileCount : 1) : 1;
}
