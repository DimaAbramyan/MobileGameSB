using System.Collections.Generic;

internal sealed partial class DirectedWaveAttackController
{
    private EnemyAttackNumericState schedulingNumeric;
    public EnemyAttackNumericState SchedulingNumeric => schedulingNumeric;
    public void RestoreSchedulingNumeric(EnemyAttackNumericState previous)
    {
        if (ReferenceEquals(schedulingNumeric, previous)) return;
        schedulingNumeric = previous;
        nextAttackTime = UnityEngine.Time.time + settings.SampleStartDelay(schedulingNumeric);
    }
    private readonly Dictionary<Enemy, EnemyAttackNumericRegistry> numericFallback = new();
    private EnemyAttackNumericRegistry GetNumericRegistry(Enemy enemy)
    {
        EnemyAttackNumericRegistry registry;
        if (executors.TryGetValue(enemy, out var executor) && executor is IEnemyAttackNumericSource source)
            registry = source.NumericRegistry;
        else if (!numericFallback.TryGetValue(enemy, out registry))
        {
            registry = new EnemyAttackNumericRegistry();
            if (wave.TryGetFormationIndex(enemy, out int slot)) registry.Configure(slot);
            numericFallback.Add(enemy, registry);
        }
        return registry;
    }
    private float SampleNumericCooldown(Enemy enemy, EnemyBurstAttackSettings profile)
        => EnemyAttackCooldownSampler.Sample(profile, GetNumericRegistry(enemy), settings);
}
