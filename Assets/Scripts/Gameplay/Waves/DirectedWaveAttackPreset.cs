using UnityEngine;

[CreateAssetMenu(
    fileName = "DirectedWaveAttackPreset",
    menuName = "Game/Waves/Directed Wave Attack Preset")]
public sealed class DirectedWaveAttackPreset : ScriptableObject
{
    [SerializeField] private DirectedWaveAttackSettings attackSettings =
        new DirectedWaveAttackSettings();

    public DirectedWaveAttackSettings AttackSettings
    {
        get
        {
            attackSettings ??= new DirectedWaveAttackSettings();
            return attackSettings;
        }
    }

    public void SetAttackSettings(DirectedWaveAttackSettings source)
    {
        attackSettings ??= new DirectedWaveAttackSettings();
        attackSettings.CopyFrom(source);
    }

    private void OnValidate()
    {
        attackSettings ??= new DirectedWaveAttackSettings();
        attackSettings.Validate();
    }
}
