using UnityEngine;

[CreateAssetMenu(
    fileName = "DirectedWaveAttackPreset",
    menuName = "Game/Waves/Directed Wave Attack Preset")]
public sealed class DirectedWaveAttackPreset : ScriptableObject
{
    [SerializeField] private DirectedWaveAttackSettings attackSettings =
        new DirectedWaveAttackSettings();
    [Header("Projectile Speed")]
    [SerializeField, Tooltip(
        "Uses this speed in the runtime copy of each enemy attack pattern. Enemy and projectile prefabs are unchanged.")]
    private bool overrideBaseProjectileSpeed;
    [SerializeField, Min(0.01f)] private float baseProjectileSpeed = 1f;

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
        float speed = 1f)
    {
        attackSettings ??= new DirectedWaveAttackSettings();
        attackSettings.CopyFrom(source);
        overrideBaseProjectileSpeed = overrideSpeed;
        baseProjectileSpeed = Mathf.Max(0.01f, speed);
    }

    private void OnValidate()
    {
        attackSettings ??= new DirectedWaveAttackSettings();
        attackSettings.Validate();
        baseProjectileSpeed = Mathf.Max(0.01f, baseProjectileSpeed);
    }
}
