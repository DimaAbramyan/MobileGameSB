using System.Collections.Generic;
using UnityEngine;

public enum DirectedWaveEntranceAttackCountMode
{
    PerEnemy,
    TotalForGroup
}

public enum DirectedWaveEntranceAttackOrder
{
    Sequential,
    Random
}

public enum DirectedWaveContinuousEntranceAttackStartMode
{
    LoopRestart,
    Checkpoint
}

[System.Serializable]
public sealed class DirectedWaveEntranceAttackSettings
{
    [SerializeField] private bool isEnabled;
    [SerializeField] private List<DirectedWaveAtCheckpointAttackRule>
        atCheckpointRules = new();
    [SerializeField] private List<DirectedWaveAcrossCheckpointsAttackRule>
        acrossCheckpointRules = new();
    [SerializeField] private DirectedWaveContinuousEntranceAttackRule
        continuousAttackRule = new();

    public bool IsEnabled => isEnabled;
    public List<DirectedWaveAtCheckpointAttackRule> AtCheckpointRules =>
        atCheckpointRules;
    public List<DirectedWaveAcrossCheckpointsAttackRule> AcrossCheckpointRules =>
        acrossCheckpointRules;
    public DirectedWaveContinuousEntranceAttackRule ContinuousAttackRule =>
        continuousAttackRule;

    public void Validate()
    {
        atCheckpointRules ??= new List<DirectedWaveAtCheckpointAttackRule>();
        acrossCheckpointRules ??=
            new List<DirectedWaveAcrossCheckpointsAttackRule>();
        continuousAttackRule ??= new DirectedWaveContinuousEntranceAttackRule();

        for (int i = 0; i < atCheckpointRules.Count; i++)
            atCheckpointRules[i]?.Validate();

        for (int i = 0; i < acrossCheckpointRules.Count; i++)
            acrossCheckpointRules[i]?.Validate();

        continuousAttackRule.Validate();
    }
}

[System.Serializable]
public sealed class DirectedWaveContinuousEntranceAttackRule
{
    [SerializeField] private bool isEnabled;
    [SerializeField] private DirectedWaveContinuousEntranceAttackStartMode
        startMode = DirectedWaveContinuousEntranceAttackStartMode.LoopRestart;
    [SerializeField, Min(0)] private int checkpointIndex;

    public bool IsEnabled => isEnabled;
    public DirectedWaveContinuousEntranceAttackStartMode StartMode => startMode;
    public int CheckpointIndex => Mathf.Max(0, checkpointIndex);

    public bool Matches(int reachedCheckpointIndex, bool isLoopRestart)
    {
        if (!isEnabled)
            return false;

        return startMode == DirectedWaveContinuousEntranceAttackStartMode.LoopRestart
            ? isLoopRestart
            : reachedCheckpointIndex == CheckpointIndex;
    }

    public void Validate()
    {
        checkpointIndex = Mathf.Max(0, checkpointIndex);
    }
}

[System.Serializable]
public sealed class DirectedWaveAtCheckpointAttackRule
{
    [SerializeField] private bool isEnabled = true;
    [SerializeField, Min(0)] private int checkpointIndex;
    [SerializeField] private bool useSelectedEnemySlots;
    [SerializeField] private List<int> selectedEnemySlots = new();
    [SerializeField, Min(1)] private int shotCount = 1;

    public bool IsEnabled => isEnabled;
    public int CheckpointIndex => Mathf.Max(0, checkpointIndex);
    public bool UsesSelectedEnemySlots => useSelectedEnemySlots;
    public int ShotCount => Mathf.Max(1, shotCount);

    public bool AllowsEnemySlot(int slotIndex)
    {
        if (!useSelectedEnemySlots)
            return true;

        if (selectedEnemySlots == null)
            return false;

        for (int i = 0; i < selectedEnemySlots.Count; i++)
        {
            if (selectedEnemySlots[i] == slotIndex)
                return true;
        }

        return false;
    }

    public void Validate()
    {
        checkpointIndex = Mathf.Max(0, checkpointIndex);
        shotCount = Mathf.Max(1, shotCount);
        selectedEnemySlots ??= new List<int>();

        for (int i = 0; i < selectedEnemySlots.Count; i++)
            selectedEnemySlots[i] = Mathf.Max(0, selectedEnemySlots[i]);
    }
}

[System.Serializable]
public sealed class DirectedWaveAcrossCheckpointsAttackRule
{
    [SerializeField] private bool isEnabled = true;
    [SerializeField, Min(0)] private int startCheckpointIndex;
    [SerializeField, Min(0)] private int endCheckpointIndex = 1;
    [SerializeField] private bool useSelectedEnemySlots;
    [SerializeField] private List<int> selectedEnemySlots = new();
    [SerializeField] private DirectedWaveEntranceAttackCountMode attackCountMode =
        DirectedWaveEntranceAttackCountMode.PerEnemy;
    [SerializeField, Min(1)] private int attackCount = 1;
    [SerializeField] private DirectedWaveEntranceAttackOrder attackOrder =
        DirectedWaveEntranceAttackOrder.Sequential;

    public bool IsEnabled => isEnabled;
    public int StartCheckpointIndex => Mathf.Max(0, startCheckpointIndex);
    public int EndCheckpointIndex => Mathf.Max(0, endCheckpointIndex);
    public bool UsesSelectedEnemySlots => useSelectedEnemySlots;
    public DirectedWaveEntranceAttackCountMode AttackCountMode =>
        attackCountMode;
    public int AttackCount => Mathf.Max(1, attackCount);
    public DirectedWaveEntranceAttackOrder AttackOrder => attackOrder;

    public bool AllowsEnemySlot(int slotIndex)
    {
        if (!useSelectedEnemySlots)
            return true;

        if (selectedEnemySlots == null)
            return false;

        for (int i = 0; i < selectedEnemySlots.Count; i++)
        {
            if (selectedEnemySlots[i] == slotIndex)
                return true;
        }

        return false;
    }

    public void Validate()
    {
        startCheckpointIndex = Mathf.Max(0, startCheckpointIndex);
        endCheckpointIndex = Mathf.Max(0, endCheckpointIndex);
        attackCount = Mathf.Max(1, attackCount);
        selectedEnemySlots ??= new List<int>();

        for (int i = 0; i < selectedEnemySlots.Count; i++)
            selectedEnemySlots[i] = Mathf.Max(0, selectedEnemySlots[i]);
    }
}
