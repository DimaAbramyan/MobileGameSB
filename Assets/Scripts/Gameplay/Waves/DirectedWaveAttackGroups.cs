using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class DirectedWaveAttackGroup
{
    [SerializeField, HideInInspector] private string id;
    public string Id => id;
    [SerializeField] private DirectedWaveAttackPattern single = new();
    [SerializeField] private List<DirectedWaveAttackPattern> patterns = new();
    [SerializeField] private DirectedWaveAttackExecutionMode executionMode;
    [SerializeField] private DirectedWaveAttackSequenceSettings sequence = new();
    public DirectedWaveAttackPattern Single => single;
    public IReadOnlyList<DirectedWaveAttackPattern> Patterns => patterns;
    public DirectedWaveAttackExecutionMode ExecutionMode => executionMode;
    public DirectedWaveAttackSequenceSettings Sequence => sequence;
    public bool UsesSequence => executionMode == DirectedWaveAttackExecutionMode.Sequence;
    public DirectedWaveAttackGroup() { id = Guid.NewGuid().ToString("N"); }
    public void CopyFrom(DirectedWaveAttackGroup source)
    {
        if (source == null || ReferenceEquals(source, this)) return;
        string identity = id;
        JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(source), this);
        id = identity;
    }
    internal void Capture(DirectedWaveAttackPattern source, List<DirectedWaveAttackPattern> multiple,
        DirectedWaveAttackExecutionMode mode, DirectedWaveAttackSequenceSettings steps)
    {
        single = JsonUtility.FromJson<DirectedWaveAttackPattern>(JsonUtility.ToJson(source));
        executionMode = mode;
        patterns = new List<DirectedWaveAttackPattern>();
        if (multiple != null) foreach (var pattern in multiple)
            if (pattern != null) patterns.Add(JsonUtility.FromJson<DirectedWaveAttackPattern>(JsonUtility.ToJson(pattern)));
        sequence = JsonUtility.FromJson<DirectedWaveAttackSequenceSettings>(JsonUtility.ToJson(steps));
    }
    internal DirectedWaveAttackPattern PatternForSlot(int slot)
    {
        if (UsesSequence) return GuideForSlot(slot)?.pattern;
        if (patterns.Count == 0) return single;
        for (int i = 0; i < patterns.Count; i++)
            if (patterns[i] != null && patterns[i].SelectedEnemySlots.ContainsSlot(slot)) return patterns[i];
        return null;
    }
    internal DirectedWaveAttackSequenceStep GuideForSlot(int slot)
    {
        var cursor = new DirectedWaveAttackSequenceCursor(sequence);
        for (int i = 0; i < 256 && cursor.TryNext(out var step); i++)
            if (step.type == DirectedWaveAttackSequenceStepType.Attack
                && (!step.overrideTargetSlots || step.targetSlots.Contains(slot))) return step;
        return null;
    }
    internal int PresetRevision(int stamp)
    {
        stamp = stamp * 397 ^ (single.Preset != null ? single.Preset.ConfigurationRevision : 0);
        foreach (var pattern in patterns) stamp = stamp * 397 ^ (pattern?.Preset != null ? pattern.Preset.ConfigurationRevision : 0);
        return DirectedWaveAttackBehaviour.GroupSequencePresetRevision(sequence.steps, stamp);
    }
}

[Serializable]
public sealed class DirectedWaveAttackGroupSlot
{
    public int slot;
    public string groupId;
    public bool usesSlotReference;
    public Transform slotReference;
}

public sealed partial class DirectedWaveAttackBehaviour
{
    [SerializeField] private bool useAttackGroups;
    [SerializeField] private List<DirectedWaveAttackGroup> attackGroups = new();
    [SerializeField] private List<DirectedWaveAttackGroupSlot> attackGroupSlots = new();
    [NonSerialized] private readonly Dictionary<DirectedWaveAttackPattern, DirectedWaveAttackSettings> resolvedGroupPatterns = new();
    [NonSerialized] private readonly Dictionary<int, DirectedWaveAttackPattern> groupGuides = new();
    private int resolvedGroupsRevision = int.MinValue;
    public void InvalidateResolvedGroupSettings() { resolvedGroupPatterns.Clear(); groupGuides.Clear(); resolvedGroupsRevision = int.MinValue; }
    public bool UseAttackGroups => useAttackGroups;
    private readonly Dictionary<Enemy, IWaveAttackExecutor> groupControlledEnemies = new();
    private readonly List<Enemy> groupCandidates = new();
    private readonly List<MonoBehaviour> groupComponents = new();
    private readonly List<Enemy> groupDeadEnemies = new();
    private readonly Dictionary<Enemy, int> groupSpawnSlots = new();
    internal void RegisterAttackGroupSpawn(Enemy enemy, int slot)
    {
        if (!useAttackGroups || !isActiveAndEnabled || enemy == null) return;
        groupSpawnSlots[enemy] = slot;
        if (GetAttackGroupForSlot(slot) == null)
        {
            groupComponents.Clear(); enemy.GetComponents(groupComponents);
            for (int i = 0; i < groupComponents.Count; i++) if (groupComponents[i] is IWaveAttackExecutor executor)
            { groupControlledEnemies[enemy] = executor; executor.SetWaveAttackControl(true); break; }
        }
    }
    internal bool AllowsAttackGroupEnemy(Enemy enemy)
        => !useAttackGroups || !isActiveAndEnabled || enemy != null && ((groupSpawnSlots.TryGetValue(enemy, out int slot)
            || wave != null && wave.TryGetFormationIndex(enemy, out slot)) && GetAttackGroupForSlot(slot) != null);
    public IReadOnlyList<DirectedWaveAttackGroup> AttackGroups => attackGroups;
    public DirectedWaveAttackPattern GetAttackGroupPattern(string id, int index)
    {
        foreach (var group in attackGroups) if (group.Id == id)
            return index < 0 ? group.Single : index < group.Patterns.Count ? group.Patterns[index] : null;
        return null;
    }
    private void ValidateAttackGroups()
    {
        if (useAttackGroups && attackGroups.Count == 0) SetUseAttackGroups(true);
        // Removed slots are ignored at lookup. Do not mutate another component's
        // serialized assignments from a Formation OnValidate (breaks Undo).
    }
    private void ReserveAttackGroupControl()
    {
        if (!useAttackGroups || wave == null) return;
        groupDeadEnemies.Clear();
        foreach (var pair in groupControlledEnemies)
            if (pair.Key == null || pair.Key.isDead) groupDeadEnemies.Add(pair.Key);
            else pair.Value.SetWaveAttackControl(true);
        foreach (var enemy in groupDeadEnemies) groupControlledEnemies.Remove(enemy);
        wave.GetWaveAttackCandidates(groupCandidates);
        for (int i = 0; i < groupCandidates.Count; i++)
        {
            var enemy = groupCandidates[i];
            if (enemy == null || enemy.isDead || groupControlledEnemies.ContainsKey(enemy)) continue;
            groupComponents.Clear(); enemy.GetComponents(groupComponents);
            for (int j = 0; j < groupComponents.Count; j++) if (groupComponents[j] is IWaveAttackExecutor executor)
            { groupControlledEnemies.Add(enemy, executor); executor.SetWaveAttackControl(true); break; }
        }
    }
    private void ReleaseAttackGroupControl()
    {
        foreach (var pair in groupControlledEnemies) if (pair.Key != null) pair.Value.SetWaveAttackControl(false);
        groupControlledEnemies.Clear();
    }
    internal static int GroupSequencePresetRevision(List<DirectedWaveAttackSequenceStep> steps, int stamp) => SequencePresetRevision(steps, stamp, 0);
    private int GroupPresetRevision(int stamp)
    { for (int i = 0; i < attackGroups.Count; i++) stamp = attackGroups[i].PresetRevision(stamp); return stamp; }
    public DirectedWaveAttackGroup GetAttackGroupForSlot(int slot)
    {
        if (!useAttackGroups) return null;
        wave ??= GetComponent<DirectedEnemySubWave>();
        if (wave == null || slot < 0 || slot >= wave.GetConfiguredEnemySlotCount()) return null;
        for (int i = 0; i < attackGroupSlots.Count; i++)
            if (attackGroupSlots[i].usesSlotReference
                ? attackGroupSlots[i].slotReference != null && wave.GetConfiguredEnemyOverrideHostForSlot(slot) == attackGroupSlots[i].slotReference
                : attackGroupSlots[i].slot == slot)
                for (int g = 0; g < attackGroups.Count; g++)
                    if (attackGroups[g].Id == attackGroupSlots[i].groupId) return attackGroups[g];
        return null;
    }
    public void SetUseAttackGroups(bool enabled)
    {
        useAttackGroups = enabled;
        if (enabled && attackGroups.Count == 0)
        {
            var group = new DirectedWaveAttackGroup();
            var source = new DirectedWaveAttackPattern();
            source.Configure(attackSettings, attackPreset, overridePresetAttacksPerSecond, presetAttacksPerSecond,
                overrideBaseProjectileSpeed, baseProjectileSpeed, BaseProjectileSpeedRandom, baseProjectileSpeedRandomizeOn);
            group.Capture(source, attackPatterns, executionMode, AttackSequence);
            attackGroups.Add(group);
            wave ??= GetComponent<DirectedEnemySubWave>();
            int count = wave != null ? wave.GetConfiguredEnemySlotCount() : 0;
            for (int i = 0; i < count; i++) if (InitialAttackGroupSlot(i))
                AssignAttackGroup(i, group.Id);
        }
        InvalidateSequenceGuides();
    }
    private bool InitialAttackGroupSlot(int slot)
    {
        if (executionMode == DirectedWaveAttackExecutionMode.Sequence) return ControlsSequenceSlot(slot);
        if (attackPatterns == null || attackPatterns.Count == 0) return AllowsSequenceSlot(slot);
        for (int i = 0; i < attackPatterns.Count; i++)
            if (attackPatterns[i]?.SelectedEnemySlots.ContainsSlot(slot) == true) return true;
        return false;
    }
    public DirectedWaveAttackGroup CreateAttackGroup(string sourceId)
    {
        var group = new DirectedWaveAttackGroup();
        for (int i = 0; i < attackGroups.Count; i++) if (attackGroups[i].Id == sourceId) { group.CopyFrom(attackGroups[i]); break; }
        attackGroups.Add(group); InvalidateSequenceGuides(); return group;
    }
    public bool CopyAttackGroup(string destinationId, string sourceId)
    {
        DirectedWaveAttackGroup destination = null, source = null;
        foreach (var group in attackGroups) { if (group.Id == destinationId) destination = group; if (group.Id == sourceId) source = group; }
        if (destination == null || source == null || destination == source) return false;
        destination.CopyFrom(source); InvalidateSequenceGuides(); return true;
    }
    public bool RemoveAttackGroup(string id)
    {
        if (attackGroups.Count <= 1) return false;
        int index = attackGroups.FindIndex(g => g.Id == id);
        if (index < 0) return false;
        attackGroups.RemoveAt(index); attackGroupSlots.RemoveAll(s => s.groupId == id); InvalidateSequenceGuides(); return true;
    }
    public void AssignAttackGroup(int slot, string id)
    {
        bool valid = false; foreach (var group in attackGroups) if (group.Id == id) { valid = true; break; }
        wave ??= GetComponent<DirectedEnemySubWave>();
        var host = wave != null && slot >= 0 && slot < wave.GetConfiguredEnemySlotCount()
            ? wave.GetConfiguredEnemyOverrideHostForSlot(slot) : null;
        attackGroupSlots.RemoveAll(s => s.usesSlotReference ? host != null && s.slotReference == host : s.slot == slot);
        if (valid && slot >= 0) attackGroupSlots.Add(new DirectedWaveAttackGroupSlot {
            slot = slot, groupId = id, slotReference = host, usesSlotReference = host != null });
        InvalidateSequenceGuides();
    }
    public bool TryGetGroupAttackSettings(int slot, out DirectedWaveAttackSettings settings)
    {
        settings = null;
        var group = GetAttackGroupForSlot(slot);
        if (group == null) return false;
        int revision = ConfigurationRevision;
        if (resolvedGroupsRevision != revision) { resolvedGroupPatterns.Clear(); groupGuides.Clear(); resolvedGroupsRevision = revision; }
        if (!groupGuides.TryGetValue(slot, out var pattern))
        { pattern = group.PatternForSlot(slot); groupGuides.Add(slot, pattern); }
        if (pattern == null) return group.UsesSequence; // An empty/pause-only sequence still owns its assigned slot.
        if (!resolvedGroupPatterns.TryGetValue(pattern, out settings))
        {
            settings = new DirectedWaveAttackSettings();
            if (group.UsesSequence) group.GuideForSlot(slot).CopySettingsTo(settings);
            else pattern.CopyResolvedSettingsTo(settings);
            resolvedGroupPatterns.Add(pattern, settings);
        }
        return true;
    }
    public void CopyCommonAttackSettingsTo(DirectedWaveAttackSettings destination)
    { RefreshResolvedAttackSettings(); destination.CopyFrom(resolvedAttackSettings); }
    public int GroupPreviewCacheKey(int slot)
    {
        var group = GetAttackGroupForSlot(slot);
        if (group == null) return slot;
        if (group.UsesSequence) return slot;
        int pattern = 0;
        if (group.Patterns.Count > 0)
        {
            var selected = group.PatternForSlot(slot);
            if (selected == null) return slot;
            for (int i = 0; i < group.Patterns.Count; i++) if (group.Patterns[i] == selected) { pattern = i; break; }
        }
        // Negative keys are group+pattern configurations, positive keys remain
        // slot-dependent Sequence guides. Changes to either list reset the cache.
        for (int i = 0; i < attackGroups.Count; i++) if (attackGroups[i] == group)
            return -2 - i - pattern * attackGroups.Count;
        return slot;
    }
}

internal static class DirectedWaveAttackSlotList
{
    internal static bool ContainsSlot(this IReadOnlyList<int> slots, int slot)
    { for (int i = 0; i < slots.Count; i++) if (slots[i] == slot) return true; return false; }
}
