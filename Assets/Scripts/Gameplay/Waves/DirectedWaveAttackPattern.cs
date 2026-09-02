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

    public IReadOnlyList<int> SelectedEnemySlots => selectedEnemySlots;

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

        if (attackPreset != null && overridePresetAttacksPerSecond)
            destination.SetAttacksPerSecond(presetAttacksPerSecond);
    }

    public void CopyResolvedSettingsTo(DirectedWaveAttackPreset preset)
    {
        if (preset == null)
            return;

        DirectedWaveAttackSettings resolved = new DirectedWaveAttackSettings();
        CopyResolvedSettingsTo(resolved);
        preset.SetAttackSettings(resolved);
    }

    public void UsePresetAsLocalSettings()
    {
        DirectedWaveAttackSettings resolved = new DirectedWaveAttackSettings();
        CopyResolvedSettingsTo(resolved);
        attackSettings ??= new DirectedWaveAttackSettings();
        attackSettings.CopyFrom(resolved);
        attackPreset = null;
        overridePresetAttacksPerSecond = false;
    }

    public void Validate()
    {
        attackSettings ??= new DirectedWaveAttackSettings();
        attackSettings.Validate();
        presetAttacksPerSecond = Mathf.Max(0.01f, presetAttacksPerSecond);
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
