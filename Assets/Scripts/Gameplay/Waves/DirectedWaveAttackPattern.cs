using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public sealed class DirectedWaveAttackPattern
{
    [SerializeField] private List<int> selectedEnemySlots = new();

    [SerializeField] private DirectedWaveAttackSettings attackSettings =
        new DirectedWaveAttackSettings();

    [SerializeField] private DirectedWaveAttackPreset attackPreset;
    [SerializeField] private bool overridePresetAttacksPerSecond;
    [SerializeField, Min(0.01f)] private float presetAttacksPerSecond = 1f;
    [SerializeField] private bool overrideBaseProjectileSpeed;
    [SerializeField, Randomizable(nameof(baseProjectileSpeedRandom), .01f)] private float baseProjectileSpeed = 1f;
    [SerializeField, HideInInspector] private RandomFloatRange baseProjectileSpeedRandom = new();
    [SerializeField] private EnemySpeedRandomizeOn baseProjectileSpeedRandomizeOn = EnemySpeedRandomizeOn.PerProjectile;
    public RandomFloatRange BaseProjectileSpeedRandom => baseProjectileSpeedRandom ??= new();
    public EnemySpeedRandomizeOn BaseProjectileSpeedRandomizeOn => baseProjectileSpeedRandomizeOn;

    public IReadOnlyList<int> SelectedEnemySlots => selectedEnemySlots;
    public DirectedWaveAttackPreset Preset => attackPreset;

    public void CopyResolvedSettingsTo(DirectedWaveAttackSettings destination)
    {
        if (destination == null)
            return;

        attackSettings ??= new DirectedWaveAttackSettings();
        attackSettings.Validate();
        DirectedWaveAttackSettings source = attackPreset != null
            ? attackPreset.AttackSettings
            : attackSettings;
        destination.CopyFrom(source);
        EnemySectorLaunchOrderRegistry.BindSource(destination.WaveBurstSettings, this);

        if (attackPreset != null && overridePresetAttacksPerSecond)
            destination.SetAttacksPerSecond(presetAttacksPerSecond);

        if (attackPreset != null && attackPreset.OverridesBaseProjectileSpeed)
            destination.SetRuntimeProjectileBaseSpeedOverride(
                attackPreset.BaseProjectileSpeed, attackPreset.BaseProjectileSpeedRandom, attackPreset.BaseProjectileSpeedRandomizeOn);

        if (overrideBaseProjectileSpeed)
            destination.SetRuntimeProjectileBaseSpeedOverride(baseProjectileSpeed, BaseProjectileSpeedRandom, baseProjectileSpeedRandomizeOn);
    }

    public void CopyResolvedSettingsTo(DirectedWaveAttackPreset preset)
    {
        if (preset == null)
            return;

        DirectedWaveAttackSettings resolved = new DirectedWaveAttackSettings();
        CopyResolvedSettingsTo(resolved);
        preset.SetAttackSettings(
            resolved,
            resolved.HasRuntimeProjectileBaseSpeedOverride,
            resolved.RuntimeProjectileBaseSpeedOverride, resolved.RuntimeProjectileBaseSpeedRandom, resolved.RuntimeProjectileBaseSpeedRandomizeOn);
    }

    public void UsePresetAsLocalSettings()
    {
        DirectedWaveAttackSettings resolved = new DirectedWaveAttackSettings();
        CopyResolvedSettingsTo(resolved);
        attackSettings ??= new DirectedWaveAttackSettings();
        attackSettings.CopyFrom(resolved);
        overrideBaseProjectileSpeed =
            resolved.HasRuntimeProjectileBaseSpeedOverride;
        baseProjectileSpeed = resolved.HasRuntimeProjectileBaseSpeedOverride
            ? resolved.RuntimeProjectileBaseSpeedOverride
            : baseProjectileSpeed;
        BaseProjectileSpeedRandom.CopyFrom(resolved.RuntimeProjectileBaseSpeedRandom);
        baseProjectileSpeedRandomizeOn = resolved.RuntimeProjectileBaseSpeedRandomizeOn;
        attackPreset = null;
        overridePresetAttacksPerSecond = false;
    }

    public void Validate()
    {
        attackSettings ??= new DirectedWaveAttackSettings();
        attackSettings.Validate();
        presetAttacksPerSecond = Mathf.Max(0.01f, presetAttacksPerSecond);
        baseProjectileSpeed = Mathf.Max(0.01f, baseProjectileSpeed);
        selectedEnemySlots ??= new List<int>();

        HashSet<int> uniqueSlots = new();
        for (int i = selectedEnemySlots.Count - 1; i >= 0; i--)
        {
            int slotIndex = selectedEnemySlots[i];
            if (slotIndex < 0 || !uniqueSlots.Add(slotIndex))
                selectedEnemySlots.RemoveAt(i);
        }
    }

    public void RemoveSlotsAlreadyAssigned(HashSet<int> assignedSlots)
    {
        if (assignedSlots == null)
            return;

        selectedEnemySlots ??= new List<int>();
        for (int i = selectedEnemySlots.Count - 1; i >= 0; i--)
        {
            int slotIndex = selectedEnemySlots[i];
            if (slotIndex < 0 || !assignedSlots.Add(slotIndex))
                selectedEnemySlots.RemoveAt(i);
        }
    }
}
