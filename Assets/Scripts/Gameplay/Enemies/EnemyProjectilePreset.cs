using UnityEngine;

[CreateAssetMenu(fileName = "ProjectilePreset", menuName = "Game/Waves/Projectile Preset")]
public sealed class EnemyProjectilePreset : EnemyReusablePreset
{
    [SerializeField] private EnemyShootingSettings projectile = new();
    public EnemyShootingSettings Settings => projectile;

    public void SetSettings(EnemyShootingSettings source)
    {
        var clean = new EnemyShootingSettings();
        clean.CopyProjectileConfigurationFrom(source);
        clean.UseIndependentProjectileSpeed(source);
        projectile = clean;
        NotifyChanged();
    }

    protected override void OnEnable() { projectile.UseIndependentProjectileSpeed(); base.OnEnable(); }
}
