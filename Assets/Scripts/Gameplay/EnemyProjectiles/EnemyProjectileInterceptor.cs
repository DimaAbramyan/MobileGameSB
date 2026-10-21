using UnityEngine;

public enum EnemyProjectileDestructionPolicy : byte
{
    FragileOnly = 0,
    NormalAndFragile = 1
}

/// <summary>Declares which enemy projectiles an ability can destroy.</summary>
public interface IEnemyProjectileDestructionAbility
{
    EnemyProjectileDestructionPolicy ProjectileDestructionPolicy { get; }
}

public static class EnemyProjectileDestructionAbilityExtensions
{
    public static bool CanDestroyProjectile(this IEnemyProjectileDestructionAbility ability,
        EnemyProjectileCharacteristic characteristic)
        => ability != null && EnemyProjectileCharacteristics.CanBeDestroyed(characteristic,
            ability.ProjectileDestructionPolicy == EnemyProjectileDestructionPolicy.NormalAndFragile);
}

/// <summary>Registered physical interceptor shared by legacy and ECS projectiles.</summary>
public abstract class EnemyProjectileInterceptor : MonoBehaviour, IEnemyProjectileDestructionAbility
{
    public virtual EnemyProjectileDestructionPolicy ProjectileDestructionPolicy =>
        EnemyProjectileDestructionPolicy.FragileOnly;
    public virtual bool DestroysNormalProjectiles =>
        ProjectileDestructionPolicy == EnemyProjectileDestructionPolicy.NormalAndFragile;
    public virtual bool TryDestroyEnemyProjectile(EnemyProjectile projectile)
    {
        if (projectile == null || !projectile.TryDestroyByAbility((IEnemyProjectileDestructionAbility)this))
            return false;
        NotifyEnemyProjectileIntercepted();
        return true;
    }

    public virtual void NotifyEnemyProjectileIntercepted() { }

    public virtual bool TryGetBoxSweep(out Vector2 previousCenter, out Vector2 center,
        out Vector2 size, out Vector2 right)
    {
        previousCenter = center = size = right = default;
        return false;
    }

    // Existing interceptors retain their collider-bounds sweep. Circular moving
    // interceptors opt in to an exact relative-motion sweep instead.
    public virtual bool TryGetCircularSweep(
        out Vector2 previousCenter, out Vector2 center, out float radius)
    {
        previousCenter = center = default;
        radius = 0f;
        return false;
    }
}
