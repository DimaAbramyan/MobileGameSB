using UnityEngine;

// Only firing policy. Enemy movement, facing, group order and wave scheduling stay with their owners.
[System.Serializable]
public sealed class EnemyPresetFiringSettings
{
    public bool enabled;
    public DirectedWaveAttackFireMode fireMode = DirectedWaveAttackFireMode.Aimed;
    public DirectedWaveAttackAimMode aimMode;
    public EnemyStationaryFireSettings stationaryFireSettings = new();
    [Range(0f, 180f)] public float forwardFireHalfAngle = 15f;
    public EnemyPresetFiringSettings Copy()
    {
        var copy = new EnemyPresetFiringSettings();
        JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(this), copy);
        return copy;
    }
}

[CreateAssetMenu(fileName = "AttackPatternPreset", menuName = "Game/Waves/Attack Pattern Preset")]
public sealed class EnemyAttackPatternPreset : EnemyReusablePreset
{
    [SerializeField] private EnemyAttackPatternSettings pattern = new();
    [SerializeField] private EnemyPresetFiringSettings firing = new() { enabled = true };
    [SerializeField] private EnemyProjectilePreset suggestedProjectile;
    public EnemyAttackPatternSettings Pattern => pattern;
    public EnemyPresetFiringSettings Firing => firing;
    public EnemyProjectilePreset SuggestedProjectile => suggestedProjectile;

    public void SetSettings(EnemyBurstAttackSettings source, EnemyPresetFiringSettings fire = null)
    {
        var clean = new EnemyAttackPatternSettings();
        clean.CopyFrom(source);
        clean.AuthoredShooting.ClearPresetLinks();
        clean.AuthoredShooting.ClearProjectileConfiguration();
        pattern = clean;
        if (fire != null) firing = fire.Copy();
        NotifyChanged();
    }
}
