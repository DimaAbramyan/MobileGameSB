using UnityEditor;
using UnityEngine;

public static class EnemyShootingPreview
{
    private const int TrajectorySteps = 24;

    public static void Draw(EnemyShootingSettings shooting, Vector3 position, Quaternion basis,
        float rotationTime, EnemyRotationSettings rotationOverride = null,
        Vector3? sourcePosition = null, Vector3? requestedDirection = null,
        bool useAimedShotGunDirection = false)
    {
        basis = shooting.EvaluateDirectionBasis(basis, rotationTime, rotationOverride);
        Vector3 direction = shooting.EvaluateShotDirection(
            basis,
            requestedDirection ?? basis * Vector3.up,
            useAimedShotGunDirection);
        Handles.color = new Color(1f, 0.6f, 0.15f, 0.9f);
        for (int arm = 0; arm < shooting.DirectionCount; arm++)
        {
            float shotGunRandomSpreadAngle = 0f;
            int fanProjectileCount = shooting.UsesFan
                ? shooting.FanProjectileCount
                : 1;
            for (int projectile = 0; projectile < shooting.ProjectilesPerDirection; projectile++)
            {
                if (shooting.UsesShotGun
                    && shooting.ShootingMode == EnemyProjectileFireMode.Random
                    && projectile % fanProjectileCount == 0)
                {
                    shotGunRandomSpreadAngle = shooting.GetShotGunPreviewRandomAngle(
                        projectile / fanProjectileCount);
                }

                float randomSpreadAngle = shooting.ShootingMode == EnemyProjectileFireMode.Random
                    ? shooting.UsesShotGun
                        ? shotGunRandomSpreadAngle
                        : shooting.RandomPreviewAngle
                    : 0f;
                Vector3 ray = shooting.GetProjectileDirection(direction, arm, projectile, randomSpreadAngle);
                DrawTrajectory(shooting, position, sourcePosition ?? position, ray);
            }
        }
    }

    private static void DrawTrajectory(
        EnemyShootingSettings shooting,
        Vector3 spawnPosition,
        Vector3 sourcePosition,
        Vector3 direction)
    {
        direction = direction.sqrMagnitude > 0.0001f
            ? direction.normalized
            : Vector3.down;
        float previewSpeed = shooting.InitialSpeed > 0f
            ? shooting.InitialSpeed
            : 1f;
        float duration = 0.8f / Mathf.Max(0.01f, previewSpeed);
        float deltaTime = duration / TrajectorySteps;
        var points = new Vector3[TrajectorySteps + 1];
        Vector3 position = spawnPosition;
        float orbitRadius = 0f;
        float orbitAngle = 0f;
        Vector3 orbitCenter = sourcePosition;

        if (shooting.MovementPattern == EnemyProjectileMovementPattern.Orbit)
        {
            if (shooting.OrbitCenterMode == EnemyProjectileOrbitCenterMode.FixedWorldPoint)
                orbitCenter = shooting.OrbitFixedWorldCenter;
            Vector3 offset = spawnPosition - orbitCenter;
            Vector3 radialDirection = offset.sqrMagnitude > 0.0001f
                ? offset.normalized
                : direction;
            orbitRadius = shooting.OrbitRadius > 0f
                ? shooting.OrbitRadius
                : offset.magnitude;
            position = orbitCenter + radialDirection * orbitRadius;
            orbitAngle = Mathf.Atan2(radialDirection.y, radialDirection.x) * Mathf.Rad2Deg;
            Handles.DrawSolidDisc(orbitCenter, Vector3.forward, 0.025f);
        }

        points[0] = position;
        for (int index = 1; index <= TrajectorySteps; index++)
        {
            switch (shooting.MovementPattern)
            {
                case EnemyProjectileMovementPattern.AngularTurn:
                    direction = Quaternion.Euler(
                        0f,
                        0f,
                        -shooting.AngularSpeed * deltaTime) * direction;
                    position += direction * (previewSpeed * deltaTime);
                    break;
                case EnemyProjectileMovementPattern.Lateral:
                    Vector3 right = new(direction.y, -direction.x, 0f);
                    position += (direction * previewSpeed
                        + right * shooting.LateralSpeed) * deltaTime;
                    break;
                case EnemyProjectileMovementPattern.Orbit:
                    orbitRadius = Mathf.Max(
                        0f,
                        orbitRadius + shooting.OrbitRadialSpeed * deltaTime);
                    orbitAngle -= shooting.AngularSpeed * deltaTime;
                    float angleRadians = orbitAngle * Mathf.Deg2Rad;
                    position = orbitCenter + new Vector3(
                        Mathf.Cos(angleRadians),
                        Mathf.Sin(angleRadians),
                        0f) * orbitRadius;
                    break;
                default:
                    position += direction * (previewSpeed * deltaTime);
                    break;
            }

            points[index] = position;
        }

        Handles.DrawAAPolyLine(2f, points);
    }

    public static void DrawFacing(EnemyBurstAttackSettings patterns, Vector3 position, Quaternion initial,
        Quaternion firingBasis, float rotationTime, EnemyRotationSettings rotationOverride = null,
        Vector3? lockedAim = null)
    {
        if (patterns.FacingMode != EnemyFacingMode.FollowPattern || patterns.FacingPatternIndex < 0)
            return;
        Quaternion pose = EnemyAttackFacing.Preview(patterns, initial, firingBasis, rotationTime, rotationOverride);
        EnemyShootingSettings shooting = patterns.GetPattern(patterns.FacingPatternIndex).Shooting;
        if (lockedAim.HasValue && shooting.ShootingType != EnemyShootingType.Rotation)
            pose = EnemyAttackFacing.Step(initial, shooting.GetFacingDirection(lockedAim.Value),
                patterns.FacingSpeed, patterns.FacingAngleOffset, Mathf.Max(0f, rotationTime));
        Handles.color = new Color(0.2f, 0.95f, 1f, 0.9f);
        Handles.ArrowHandleCap(0, position, Quaternion.LookRotation(pose * Vector3.up, Vector3.forward),
            1.1f, EventType.Repaint);
    }

    public static void DrawPatterns(EnemyBurstAttackSettings patterns, Vector3 position, Quaternion basis,
        float rotationTime, EnemyRotationSettings rotationOverride = null)
    {
        for (int i = 0; i < patterns.PatternCount; i++)
            Draw(patterns.GetPattern(i).Shooting, position, basis, rotationTime, rotationOverride);
    }

    public static void DrawWaveSlot(DirectedEnemySubWave wave, int slot, Vector3 position,
        float enemyTime, float elapsed)
    {
        Enemy prefab = wave.GetConfiguredEnemyPrefabForSlot(slot);
        if (prefab == null || !prefab.TryGetComponent(out SimpleEnemyAttackExecutor executor))
            return;
        var pointOverride = wave.GetConfiguredEnemyOverrideForSlot(slot);
        EnemyBurstAttackSettings patterns = executor.BurstAttackSettings;
        EnemyRotationSettings rotationOverride = pointOverride != null ? pointOverride.RotationOverride : null;
        float rotationTime = enemyTime;
        var resolved = new DirectedWaveAttackSettings();
        bool hasResolvedSettings = false;
        float postStart = wave.GetSimulationPreviewPostStartTime();
        var behaviour = wave.AttackPatternBehaviour;
        if (behaviour != null && behaviour.enabled)
        {
            bool entrance = elapsed < postStart;
            hasResolvedSettings = behaviour.TryCopyResolvedAttackSettingsTo(
                slot,
                entrance,
                resolved);
            if (hasResolvedSettings && !resolved.UsesEnemyBurstSettings
                && (entrance || elapsed >= postStart + resolved.AttackStartDelay))
            {
                patterns = resolved.WaveBurstSettings;
                rotationOverride = null;
                rotationTime = entrance ? enemyTime : Mathf.Max(0f, elapsed - postStart - resolved.AttackStartDelay);
            }
        }
        Vector3? requestedDirection = null;
        bool useAimedShotGunDirection = false;
        if (hasResolvedSettings)
        {
            if (!TryGetWaveSlotFireDirection(
                    wave, slot, resolved, position, prefab.transform.up,
                    out Vector3 direction, out useAimedShotGunDirection))
                return;
            requestedDirection = direction;
        }

        if (pointOverride != null && pointOverride.HasAttackPatternOverride
            && (hasResolvedSettings || pointOverride.UsesLegacyFullAttackSettings))
        {
            var localPatterns = new EnemyBurstAttackSettings();
            if (!pointOverride.TryCopyLegacyFullAttackSettings(localPatterns))
                localPatterns.CopyFrom(patterns);
            pointOverride.ApplyAttackPatternOverride(
                localPatterns,
                hasResolvedSettings && resolved.HasRuntimeProjectileBaseSpeedOverride
                    ? resolved.RuntimeProjectileBaseSpeedOverride
                    : (float?)null);
            patterns = localPatterns;
        }
        else if (pointOverride != null && pointOverride.AttackSettingsOverride != null)
        {
            patterns = pointOverride.AttackSettingsOverride;
        }
        else if (hasResolvedSettings && resolved.HasRuntimeProjectileBaseSpeedOverride)
        {
            var speedOverridePatterns = new EnemyBurstAttackSettings();
            speedOverridePatterns.CopyFrom(patterns);
            speedOverridePatterns.SetRuntimeProjectileBaseSpeedOverrideForAllPatterns(
                resolved.RuntimeProjectileBaseSpeedOverride);
            patterns = speedOverridePatterns;
        }
        var serialized = new SerializedObject(executor);
        var pivot = serialized.FindProperty("rotationTarget").objectReferenceValue as Transform;
        Vector3? facingAim = null;
        for (int i = 0; i < patterns.PatternCount; i++)
        {
            EnemyShootingSettings shooting = patterns.GetPattern(i).Shooting;
            Quaternion basis = shooting.ShootingType == EnemyShootingType.Rotation && pivot != null
                ? pivot.rotation : prefab.transform.rotation;
            Vector3? patternDirection = requestedDirection;
            if (hasResolvedSettings && useAimedShotGunDirection)
            {
                DirectedWaveAttackAimMode aimMode = pointOverride != null
                    ? pointOverride.ResolveAimMode(resolved.AimMode) : resolved.AimMode;
                float phaseStart = elapsed < postStart ? elapsed - enemyTime
                    : postStart + resolved.AttackStartDelay;
                float captureTime = GetAimCaptureTime(patterns, i, aimMode,
                    Mathf.Max(0f, elapsed - phaseStart)) + phaseStart;
                patternDirection = GetCapturedAimDirection(wave, slot, position, elapsed, captureTime);
                if (i == patterns.FacingPatternIndex)
                    facingAim = patternDirection;
            }
            Draw(shooting, position, basis, rotationTime, rotationOverride,
                requestedDirection: patternDirection,
                useAimedShotGunDirection: useAimedShotGunDirection);
        }
        int facingIndex = patterns.FacingPatternIndex;
        if (patterns.FacingMode == EnemyFacingMode.FollowPattern && facingIndex >= 0)
        {
            EnemyShootingSettings shooting = patterns.GetPattern(facingIndex).Shooting;
            Quaternion basis = shooting.ShootingType == EnemyShootingType.Rotation && pivot != null
                ? pivot.rotation : prefab.transform.rotation;
            var facing = serialized.FindProperty("facingTarget").objectReferenceValue as Transform;
            DrawFacing(patterns, position, facing != null ? facing.rotation : prefab.transform.rotation,
                basis, rotationTime, rotationOverride, facingAim);
        }
    }

    private static bool TryGetWaveSlotFireDirection(
        DirectedEnemySubWave wave, int slot, DirectedWaveAttackSettings settings,
        Vector3 position, Vector3 forward, out Vector3 direction,
        out bool usesAim)
    {
        DirectedWaveEnemyOverride pointOverride = wave.GetConfiguredEnemyOverrideForSlot(slot);
        DirectedWaveAttackFireMode mode = pointOverride != null
            ? pointOverride.ResolveFireMode(settings.FireMode)
            : settings.FireMode;
        usesAim = mode == DirectedWaveAttackFireMode.Aimed;
        direction = forward;
        if (mode == DirectedWaveAttackFireMode.None)
            return false;

        Vector3 toPlayer = ((IEnemyAttackAimTarget)wave).AttackTargetPosition - position;
        if (usesAim)
            direction = toPlayer;
        if (mode != DirectedWaveAttackFireMode.ForwardWhenPlayerAhead)
            return true;

        float halfAngle = pointOverride != null
            ? pointOverride.ResolveForwardFireHalfAngle(settings.ForwardFireHalfAngle)
            : settings.ForwardFireHalfAngle;
        Vector2 planarForward = forward;
        Vector2 planarTarget = toPlayer;
        return planarTarget.sqrMagnitude < 0.0001f
            || planarForward.sqrMagnitude > 0.0001f
                && Vector2.Dot(planarForward.normalized, planarTarget.normalized)
                    >= Mathf.Cos(halfAngle * Mathf.Deg2Rad);
    }

    // Preview samples the route at the aim acquisition time, so a moving enemy
    // does not turn the shot direction between shots of the same burst.
    public static float GetAimCaptureTime(EnemyBurstAttackSettings patterns, int patternIndex,
        DirectedWaveAttackAimMode mode, float elapsed)
    {
        if (mode == DirectedWaveAttackAimMode.AimContinuous)
            return Mathf.Max(0f, elapsed);
        float duration = 0f;
        float firstShot = float.PositiveInfinity;
        for (int i = 0; i < patterns.PatternCount; i++)
        {
            EnemyBurstAttackSettings entry = patterns.GetPattern(i);
            float delay = patterns.HasMultiplePatterns ? entry.AttackStartDelay : 0f;
            firstShot = Mathf.Min(firstShot, delay);
            duration = Mathf.Max(duration, delay + entry.AttackDuration);
        }
        float cycleDuration = Mathf.Max(0.01f, duration + patterns.MaxAttackCooldown);
        float cycleStart = Mathf.Floor(Mathf.Max(0f, elapsed) / cycleDuration) * cycleDuration;
        EnemyBurstAttackSettings pattern = patterns.GetPattern(patternIndex);
        if (mode == DirectedWaveAttackAimMode.AimOnStart)
            return cycleStart + firstShot;
        float patternDelay = patterns.HasMultiplePatterns ? pattern.AttackStartDelay : 0f;
        if (!pattern.RepeatBurst)
            return cycleStart + patternDelay;
        float burstPeriod = (pattern.BurstShotCount - 1) * pattern.BurstShotInterval
            + pattern.AttackShotInterval;
        int burstIndex = burstPeriod > 0f
            ? Mathf.Clamp(Mathf.FloorToInt((elapsed - cycleStart - patternDelay) / burstPeriod),
                0, pattern.AttackShotCount - 1) : 0;
        return cycleStart + patternDelay + burstIndex * burstPeriod;
    }

    private static Vector3 GetCapturedAimDirection(DirectedEnemySubWave wave, int slot,
        Vector3 position, float elapsed, float captureTime)
    {
        var current = wave.EvaluateSimulationPreview(elapsed);
        var captured = wave.EvaluateSimulationPreview(captureTime);
        Vector3 origin = position;
        if (current.TryGetValue(slot, out Vector3 currentOrigin)
            && captured.TryGetValue(slot, out Vector3 capturedOrigin))
            origin += capturedOrigin - currentOrigin;
        DirectedWaveAttackAimState aim = default;
        return aim.ResolveDirection(origin, ((IEnemyAttackAimTarget)wave).AttackTargetPosition, false);
    }
}
