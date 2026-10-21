using UnityEngine;

public partial class EnemyRotationSettings
{
    [SerializeField, HideInInspector] private RandomFloatRange rotationSpeedDegreesPerSecondRandom = new() { randomizeOn = EnemyNumericRandomizeOn.PerAttack };
    [SerializeField, HideInInspector] private RandomFloatRange rotationAngleRandom = new() { randomizeOn = EnemyNumericRandomizeOn.PerAttack };
    [SerializeField, HideInInspector] private RandomFloatRange rotationFromAngleRandom = new() { randomizeOn = EnemyNumericRandomizeOn.PerAttack };
    public bool UsesNumericRandomization => EnemyNumericSample.Enabled(rotationSpeedDegreesPerSecondRandom) || EnemyNumericSample.Enabled(rotationAngleRandom) || EnemyNumericSample.Enabled(rotationFromAngleRandom);
    private void CopyNumericFrom(EnemyRotationSettings source)
    {
        rotationSpeedDegreesPerSecondRandom = source.rotationSpeedDegreesPerSecondRandom?.Copy() ?? new();
        rotationAngleRandom = source.rotationAngleRandom?.Copy() ?? new();
        rotationFromAngleRandom = source.rotationFromAngleRandom?.Copy() ?? new();
    }
    public EnemyRotationSettings SampleNumeric(EnemyNumericSample sample, bool perDirection = false)
    {
        if (!sample.Active || !UsesNumericRandomization) return this;
        var copy = (EnemyRotationSettings)MemberwiseClone();
        if (EnemyShootingSettings.AtScope(rotationSpeedDegreesPerSecondRandom?.randomizeOn ?? default, perDirection)) copy.rotationSpeedDegreesPerSecond = sample.Float(rotationSpeedDegreesPerSecondRandom, rotationSpeedDegreesPerSecond, 221u, .01f);
        if (EnemyShootingSettings.AtScope(rotationAngleRandom?.randomizeOn ?? default, perDirection)) copy.rotationAngle = sample.Float(rotationAngleRandom, rotationAngle, 222u, float.MinValue);
        if (EnemyShootingSettings.AtScope(rotationFromAngleRandom?.randomizeOn ?? default, perDirection)) copy.rotationFromAngle = sample.Float(rotationFromAngleRandom, rotationFromAngle, 223u, float.MinValue);
        return copy;
    }
}
