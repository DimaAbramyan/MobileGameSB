using UnityEngine;

[CreateAssetMenu(
    fileName = "DirectedWaveAttackPreset",
    menuName = "Game/Waves/Directed Wave Attack Preset")]
public sealed class DirectedWaveAttackPreset : ScriptableObject
{
    public int ConfigurationRevision { get; private set; }
    [SerializeField] private DirectedWaveAttackSettings attackSettings =
        new DirectedWaveAttackSettings();
    [Header("Projectile Speed")]
    [SerializeField, Tooltip(
        "Uses this speed in the runtime copy of each enemy attack pattern. Enemy and projectile prefabs are unchanged.")]
    private bool overrideBaseProjectileSpeed;
    [SerializeField, Randomizable(nameof(baseProjectileSpeedRandom), .01f)] private float baseProjectileSpeed = 1f;
    [SerializeField, HideInInspector] private RandomFloatRange baseProjectileSpeedRandom = new();
    [SerializeField] private EnemySpeedRandomizeOn baseProjectileSpeedRandomizeOn = EnemySpeedRandomizeOn.PerProjectile;
    public RandomFloatRange BaseProjectileSpeedRandom => baseProjectileSpeedRandom ??= new();
    public EnemySpeedRandomizeOn BaseProjectileSpeedRandomizeOn => baseProjectileSpeedRandomizeOn;

    public DirectedWaveAttackSettings AttackSettings
    {
        get
        {
            attackSettings ??= new DirectedWaveAttackSettings();
            return attackSettings;
        }
    }

    public bool OverridesBaseProjectileSpeed => overrideBaseProjectileSpeed;
    public float BaseProjectileSpeed => Mathf.Max(0.01f, baseProjectileSpeed);

    public void SetAttackSettings(
        DirectedWaveAttackSettings source,
        bool overrideSpeed = false,
        float speed = 1f, RandomFloatRange random = null,
        EnemySpeedRandomizeOn randomizeOn = EnemySpeedRandomizeOn.PerProjectile)
    {
        ConfigurationRevision++;
        attackSettings ??= new DirectedWaveAttackSettings();
        attackSettings.CopyFrom(source);
        overrideBaseProjectileSpeed = overrideSpeed;
        baseProjectileSpeed = Mathf.Max(0.01f, speed);
        BaseProjectileSpeedRandom.CopyFrom(random); baseProjectileSpeedRandomizeOn = randomizeOn;
    }

    private void OnValidate()
    {
        ConfigurationRevision++;
        attackSettings ??= new DirectedWaveAttackSettings();
        attackSettings.Validate();
        baseProjectileSpeed = Mathf.Max(0.01f, baseProjectileSpeed);
    }
}
