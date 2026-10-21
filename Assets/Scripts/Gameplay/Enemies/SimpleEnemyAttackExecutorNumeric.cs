using System.Collections.Generic;

public sealed partial class SimpleEnemyAttackExecutor : IEnemyAttackNumericSource
{
    public EnemyAttackNumericRegistry NumericRegistry { get; } = new();
    private readonly Dictionary<EnemyBurstAttackSettings, EnemyRandomSpeedSample> numericShots = new();
    private readonly Dictionary<EnemyBurstAttackSettings, float> numericCooldowns = new();
    private readonly Dictionary<EnemyBurstAttackSettings, EnemyShootingSettings> numericGeometry = new();
    private EnemyShootingSettings GetShotGeometry(EnemyBurstAttackSettings profile)
        => numericGeometry.TryGetValue(profile, out var shooting) ? shooting : profile.Shooting;
    private bool resettingNumericSchedule;
    private int numericBurstCount;
    public void SetNumericOwner(int slot) => NumericRegistry.Configure(slot);
    private EnemyRandomSpeedSample GetNumericShot(EnemyBurstAttackSettings profile)
        => numericShots.TryGetValue(profile, out var sample) ? sample : default;
    private float SampleNumericCooldown(EnemyBurstAttackSettings profile)
    {
        float value = EnemyAttackCooldownSampler.Sample(profile, NumericRegistry);
        numericCooldowns[profile] = value;
        return value;
    }
}
