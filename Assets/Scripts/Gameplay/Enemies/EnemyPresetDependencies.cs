using System.Collections.Generic;

// Walk configuration references only. The existing runtime depth/live/descendant budgets still apply.
public static class EnemyPresetDependencies
{
    public static bool HasCycle(EnemyReusablePreset preset)
        => Visit(preset, new HashSet<EnemyReusablePreset>(), new HashSet<EnemyReusablePreset>(), 0);

    private static bool Visit(EnemyReusablePreset preset, HashSet<EnemyReusablePreset> active,
        HashSet<EnemyReusablePreset> completed, int depth)
    {
        if (preset == null || completed.Contains(preset)) return false;
        if (depth > 64 || !active.Add(preset)) return true;
        var shooting = preset is EnemyProjectilePreset projectile ? projectile.Settings
            : ((EnemyAttackPatternPreset)preset).Pattern.AuthoredShooting;
        bool cycle = VisitShooting(shooting, active, completed, depth + 1);
        active.Remove(preset); completed.Add(preset);
        return cycle;
    }

    private static bool VisitShooting(EnemyShootingSettings shooting, HashSet<EnemyReusablePreset> active,
        HashSet<EnemyReusablePreset> completed, int depth)
    {
        if (shooting == null) return false;
        if (depth > 64) return true;
        return shooting.ProjectilePresetMode == EnemyPresetUseMode.Linked && Visit(shooting.ProjectilePreset, active, completed, depth)
            || shooting.AttackPatternPresetMode == EnemyPresetUseMode.Linked && Visit(shooting.AttackPatternPreset, active, completed, depth)
            || VisitShooting(shooting.AuthoredChild?.Pattern.AuthoredShooting, active, completed, depth + 1);
    }
}
