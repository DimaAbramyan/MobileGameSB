using System;
using System.Collections.Generic;
using UnityEngine;

// Common Fan/Stack/Spread expansion and projectile authoring for enemy and child attacks.
public static class EnemyAttackProjectileBuilder
{
    public static float SampleLegacyRandomSpeed(EnemyShootingSettings shooting, EnemyNumericSample numeric)
        => Mathf.Lerp(shooting.RandomInitialSpeedMinimum, shooting.RandomInitialSpeedMaximum, numeric.Unit(3091u));

    public static int NumericProjectileIndex(EnemyShootingSettings shooting, int direction, int projectile, int legacyOffset)
        => NumericProjectileIndex(shooting.UsesExtendedNumericRandomization, direction, projectile, legacyOffset);
    private static int NumericProjectileIndex(bool extended, int direction, int projectile, int legacyOffset)
        => extended
            ? unchecked((int)NumericRandomStream.Hash((uint)direction ^ NumericRandomStream.Hash((uint)projectile + 0x8d41c3b7u)))
            : legacyOffset + projectile;
    public static void AppendEmitter(List<EnemyProjectileSpawnRequest> requests, EnemyBullet prefab,
        EnemyShootingSettings shooting, Vector3 position, Vector3 origin, Vector3 direction,
        float damageMultiplier, float volleySpeed, float randomSpreadAngle, EnemyProjectilePrefabSet prefabs,
        Func<EnemyShootingSettings, float> spreadSampler = null, int maximum = int.MaxValue,
        int generation = 0, EnemyChildAttackBudget budget = null,
        Func<EnemyShootingSettings, float> speedSampler = null,
        EnemyAttackCompletionTracker completionTracker = null, EnemyRandomSpeedSample randomSpeed = default, int projectileOffset = 0,
        EnemyLaserOrigin laserOrigin = default, int directionIndex = 0)
    {
        var directionNumeric = randomSpeed.Numeric.ForDirection(directionIndex);
        shooting = shooting.SampleGeometry(directionNumeric, true);
        bool extended = shooting.UsesExtendedNumericRandomization;
        prefab = shooting.ResolveProjectilePrefab(prefab);
        int fanCount = shooting.UsesFan ? shooting.FanProjectileCount : 1;
        float stackSpread = 0f, dnaFanSpeed = volleySpeed;
        int count = Mathf.Min(shooting.ProjectilesPerDirection, maximum);
        if (extended) count = Mathf.Min(count, EnemyChildAttackSettings.MaximumPerStep);
        for (int projectile = 0; projectile < count; projectile++)
        {
            var numeric = directionNumeric.ForProjectile(projectile);
            var values = shooting.SampleProjectile(numeric);
            if (shooting.UsesShotGun && shooting.ShootingMode == EnemyProjectileFireMode.Random && projectile % fanCount == 0)
                stackSpread = extended && numeric.Active ? shooting.NumericSpread(numeric)
                    : spreadSampler != null ? spreadSampler(shooting) : UnityEngine.Random.Range(shooting.RandomMinAngle, shooting.RandomMaxAngle);
            float spread = shooting.UsesShotGun ? stackSpread : randomSpreadAngle;
            if (!shooting.UsesShotGun && shooting.ShootingMode == EnemyProjectileFireMode.Random && directionNumeric.Active && extended)
                spread = shooting.NumericSpread(directionNumeric.ForProjectile(0));
            if (!randomSpeed.Active && shooting.UsesDnaOrbit && shooting.InitialSpeedMode == EnemyProjectileInitialSpeedMode.Random && projectile % fanCount == 0)
                dnaFanSpeed = numeric.Active && extended
                    ? SampleLegacyRandomSpeed(shooting, numeric)
                    : speedSampler != null ? speedSampler(shooting) : shooting.GetRandomInitialSpeed();
            float speed = volleySpeed;
            int numericIndex = NumericProjectileIndex(extended, directionIndex, projectile, projectileOffset);
            if (randomSpeed.Active) speed = randomSpeed.Evaluate(numericIndex);
            else if (shooting.UsesDnaOrbit) speed = dnaFanSpeed;
            else if (shooting.InitialSpeedMode == EnemyProjectileInitialSpeedMode.Random)
                speed = numeric.Active && extended
                    ? SampleLegacyRandomSpeed(shooting, numeric)
                    : speedSampler != null ? speedSampler(shooting) : shooting.GetRandomInitialSpeed();
            requests.Add(new EnemyProjectileSpawnRequest(prefab, position,
                shooting.GetProjectileDirection(direction, 0, projectile, spread, numeric), damageMultiplier, speed,
                shooting.SpeedBehavior, values.SpeedChangeY, shooting.SpeedByDistance,
                shooting.SizeBehavior, values.SizeChangeY, values.SizeChangeSpeed, shooting.SizeChangeCurve, values.FinalSize,
                origin, shooting.MovementPattern, values.AngularSpeed, values.LateralSpeed,
                shooting.OrbitCenterMode, values.OrbitFixedWorldCenter, values.OrbitRadius, values.OrbitRadialSpeed,
                shooting.OverridesProjectileLifetime, values.Lifetime,
                shooting.UsesDnaOrbit ? shooting.GetDnaMedianDirection(direction, 0, spread, numeric) : Vector3.zero,
                shooting.UsesDnaOrbit ? shooting.GetNumericDnaHalfCycleDuration(values, numeric) : 0f,
                shooting.OverwritesBaseColour, shooting.ProjectileColour,
                shooting.OverwritesProjectileCharacteristic, shooting.ProjectileCharacteristic,
                shooting.ChildAttack, prefabs, generation, budget, shooting.SpeedCurveTarget, completionTracker,
                NumericRandomStream.Hash(randomSpeed.SeedKey ^ (uint)numericIndex),
                shooting.ProjectileType == EnemyProjectileType.Laser ? shooting.Laser : null, laserOrigin));
        }
    }
}
