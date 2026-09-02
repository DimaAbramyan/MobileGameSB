using UnityEngine;

public sealed class BallLightningWeapon : Weapon
{
    protected override bool Fire()
    {
        if (weaponData is not BallLightningData data)
            return false;

        bool firedAnyProjectile = false;
        int ballCount = data.BallsPerShot;
        float spreadAngle = data.BallSpreadAngle;
        ProjectileRuntimeConfig runtimeConfig = CreateProjectileRuntimeConfig();

        for (int ballIndex = 0; ballIndex < ballCount; ballIndex++)
        {
            ProjectileParams parameters = CreateProjectileParams();
            parameters.direction = GetBallDirection(
                parameters.direction,
                ballIndex,
                ballCount,
                spreadAngle);

            if (TrySpawnProjectile(parameters, runtimeConfig))
                firedAnyProjectile = true;
        }

        return firedAnyProjectile;
    }

    protected override ProjectileParams CreateProjectileParams()
    {
        if (weaponData is not BallLightningData data)
            return base.CreateProjectileParams();

        return new ProjectileParams
        {
            speed = data.ProjectileSpeed,
            damage = data.GetDirectDamage(Level),
            maxLength = data.MaxTravelDistance,
            direction = transform.up,
            maxAngle = 0f
        };
    }

    protected override ProjectileRuntimeConfig CreateProjectileRuntimeConfig()
    {
        ProjectileRuntimeConfig config = base.CreateProjectileRuntimeConfig();
        if (weaponData is not BallLightningData data)
            return config;

        config.contactMode = ProjectileContactMode.BallLightning;
        config.ballLightningAreaDamage = data.GetAreaDamage(Level);
        config.ballLightningAreaRadius = data.AreaRadius;
        config.ballLightningAreaTickInterval = data.GetAreaTickInterval(Level);
        config.ballLightningAreaDamageLayers = data.AreaDamageLayers;
        return config;
    }

    private static Vector3 GetBallDirection(
        Vector3 baseDirection,
        int ballIndex,
        int ballCount,
        float spreadAngle)
    {
        if (ballCount <= 1 || spreadAngle <= 0f)
            return baseDirection;

        float angleStep = spreadAngle / (ballCount - 1);
        float angle = -spreadAngle * 0.5f + angleStep * ballIndex;
        return Quaternion.Euler(0f, 0f, angle) * baseDirection;
    }
}
