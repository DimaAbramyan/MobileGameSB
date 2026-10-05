public static class EnemyAttackSettingsResolver
{
    public static bool UsesEnemySettings(IWaveAttackExecutor executor, bool usesEnemySettings)
    {
        return usesEnemySettings
            || executor is IEnemyAttackSettingsOverrideState { HasAttackSettingsOverride: true };
    }

    public static EnemyBurstAttackSettings Resolve(
        IWaveAttackExecutor executor,
        bool usesEnemySettings,
        EnemyBurstAttackSettings waveSettings)
    {
        if (UsesEnemySettings(executor, usesEnemySettings)
            && executor is IEnemyBurstAttackExecutor burstExecutor
            && burstExecutor.BurstAttackSettings != null)
        {
            return burstExecutor.BurstAttackSettings;
        }

        return waveSettings;
    }
}
