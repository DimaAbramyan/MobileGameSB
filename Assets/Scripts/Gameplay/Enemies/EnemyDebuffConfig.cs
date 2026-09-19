using System;
using UnityEngine;

public enum EnemyDebuffThresholdMode
{
    Disabled,
    AtMaximum,
    AtSpecifiedPercent
}

public enum EnemyDebuffThresholdAction
{
    NotifyOnly,
    BeginBurning,
    DestroyEnemy
}

[Serializable]
public sealed class EnemyDebuffApplication
{
    [SerializeField] private EnemyDebuffConfig debuff;
    [SerializeField, Min(0f)] private float amountPerHit = 1f;

    public EnemyDebuffApplication()
    {
    }

    public EnemyDebuffApplication(EnemyDebuffConfig debuff, float amountPerHit)
    {
        this.debuff = debuff;
        this.amountPerHit = Mathf.Max(0f, amountPerHit);
    }

    public EnemyDebuffConfig Debuff => debuff;
    public float AmountPerHit => Mathf.Max(0f, amountPerHit);
    public bool IsValid => debuff != null && AmountPerHit > 0f;
}

public readonly struct EnemyDebuffProgress
{
    public static readonly EnemyDebuffProgress None = new(false, 0f, 0f, 0f);

    public EnemyDebuffProgress(
        bool isValid,
        float previousValue,
        float currentValue,
        float maximumValue)
    {
        IsValid = isValid;
        PreviousValue = Mathf.Max(0f, previousValue);
        CurrentValue = Mathf.Max(0f, currentValue);
        MaximumValue = Mathf.Max(0f, maximumValue);
    }

    public bool IsValid { get; }
    public float PreviousValue { get; }
    public float CurrentValue { get; }
    public float MaximumValue { get; }
}

public readonly struct EnemyDebuffThresholdEvent
{
    public EnemyDebuffThresholdEvent(
        Enemy enemy,
        EnemyDebuffConfig debuff,
        float value,
        float threshold)
    {
        Enemy = enemy;
        Debuff = debuff;
        Value = value;
        Threshold = threshold;
    }

    public Enemy Enemy { get; }
    public EnemyDebuffConfig Debuff { get; }
    public float Value { get; }
    public float Threshold { get; }
}

public abstract class EnemyDebuffConfig : ScriptableObject
{
    [Header("Threshold Event")]
    [SerializeField] private EnemyDebuffThresholdMode thresholdMode =
        EnemyDebuffThresholdMode.Disabled;
    [SerializeField, Range(0f, 100f)] private float thresholdPercent = 100f;
    [SerializeField] private EnemyDebuffThresholdAction thresholdAction =
        EnemyDebuffThresholdAction.NotifyOnly;
    [SerializeField] private bool triggerOncePerEnemy = true;

    public EnemyDebuffThresholdMode ThresholdMode => thresholdMode;
    public EnemyDebuffThresholdAction ThresholdAction => thresholdAction;
    public bool TriggerOncePerEnemy => triggerOncePerEnemy;

    public bool TryGetThresholdValue(
        float maximumValue,
        out float thresholdValue)
    {
        thresholdValue = 0f;
        if (thresholdMode == EnemyDebuffThresholdMode.Disabled
            || maximumValue <= 0f)
        {
            return false;
        }

        thresholdValue = thresholdMode == EnemyDebuffThresholdMode.AtMaximum
            ? maximumValue
            : maximumValue * Mathf.Clamp01(thresholdPercent / 100f);
        return thresholdValue > 0f;
    }
}
