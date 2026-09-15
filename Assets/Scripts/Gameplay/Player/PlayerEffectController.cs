using System;
using System.Collections.Generic;
using UnityEngine;

public enum PlayerEffectPolarity
{
    Positive,
    Negative
}

public enum PlayerEffectScope
{
    Team,
    SingleShip
}

public enum PlayerEffectType
{
    TeamFireRate,
    TeamMagnetBoost,
    TeamInvulnerability,
    TeamMetalDropBoost,
    AbilityCooldownReset,
    BlackHoleActive,
    HuskarVampirism,
    ControlLoss
}

[Flags]
public enum PlayerEffectEndCondition
{
    Manual = 0,
    OwnerDamaged = 1 << 0,
    OwnerDied = 1 << 1,
    AbilityDeactivated = 1 << 2
}

public enum PlayerEffectRemovalReason
{
    Expired,
    Manual,
    EndCondition,
    Instant
}

/// <summary>
/// Runtime state of one active player effect. A zero duration means that the
/// effect is indefinite until removed manually or by an end condition.
/// </summary>
public sealed class PlayerEffect
{
    internal Action<float> OwnerDamageHandler;
    internal Action OwnerDeathHandler;
    internal Action<ActiveAbility> AbilityDeactivatedHandler;

    public PlayerEffectType Type { get; }
    public PlayerEffectPolarity Polarity { get; }
    public PlayerEffectScope Scope { get; }
    public ParentShip Owner { get; }
    public ActiveAbility SourceAbility { get; private set; }
    public PlayerEffectEndCondition EndConditions { get; private set; }
    public float TotalDuration { get; private set; }
    public float RemainingDuration { get; private set; }

    public bool IsTimed => TotalDuration > 0f;
    public bool IsIndefinite => !IsTimed;
    public float DurationNormalized => !IsTimed
        ? 1f
        : Mathf.Clamp01(RemainingDuration / TotalDuration);

    internal PlayerEffect(
        PlayerEffectType type,
        PlayerEffectPolarity polarity,
        PlayerEffectScope scope,
        ParentShip owner,
        float duration,
        PlayerEffectEndCondition endConditions,
        ActiveAbility sourceAbility)
    {
        Type = type;
        Polarity = polarity;
        Scope = scope;
        Owner = owner;
        Refresh(duration, endConditions, sourceAbility);
    }

    internal void Refresh(
        float duration,
        PlayerEffectEndCondition endConditions,
        ActiveAbility sourceAbility)
    {
        TotalDuration = Mathf.Max(0f, duration);
        RemainingDuration = TotalDuration;
        EndConditions = endConditions;
        SourceAbility = sourceAbility;
    }

    internal bool Tick(float deltaTime)
    {
        if (!IsTimed)
            return false;

        RemainingDuration = Mathf.Max(0f, RemainingDuration - deltaTime);
        return RemainingDuration <= 0f;
    }
}

/// <summary>
/// Runtime registry for team and individual ship buffs/debuffs. Reapplying an
/// equal effect refreshes its duration instead of creating a stack.
/// </summary>
public sealed class PlayerEffectController : MonoBehaviour
{
    private readonly List<PlayerEffect> activeEffects = new();

    public IReadOnlyList<PlayerEffect> ActiveEffects => activeEffects;

    public event Action<PlayerEffect> EffectApplied;
    public event Action<PlayerEffect> EffectRefreshed;
    public event Action<PlayerEffect, PlayerEffectRemovalReason> EffectRemoved;

    public PlayerEffect ApplyOrRefresh(
        PlayerEffectType type,
        PlayerEffectPolarity polarity,
        PlayerEffectScope scope,
        ParentShip owner,
        float duration,
        PlayerEffectEndCondition endConditions = PlayerEffectEndCondition.Manual,
        ActiveAbility sourceAbility = null)
    {
        if (scope == PlayerEffectScope.SingleShip && owner == null)
            return null;

        PlayerEffect effect = FindEffect(type, scope, owner);
        if (effect != null)
        {
            UnsubscribeFromEndConditions(effect);
            effect.Refresh(duration, endConditions, sourceAbility);
            SubscribeToEndConditions(effect);
            EffectRefreshed?.Invoke(effect);
            return effect;
        }

        effect = new PlayerEffect(
            type,
            polarity,
            scope,
            scope == PlayerEffectScope.Team ? null : owner,
            duration,
            endConditions,
            sourceAbility);
        activeEffects.Add(effect);
        SubscribeToEndConditions(effect);
        EffectApplied?.Invoke(effect);
        return effect;
    }

    public void RecordInstant(
        PlayerEffectType type,
        PlayerEffectPolarity polarity,
        PlayerEffectScope scope,
        ParentShip owner = null)
    {
        var effect = new PlayerEffect(
            type,
            polarity,
            scope,
            scope == PlayerEffectScope.Team ? null : owner,
            0f,
            PlayerEffectEndCondition.Manual,
            null);
        EffectApplied?.Invoke(effect);
        EffectRemoved?.Invoke(effect, PlayerEffectRemovalReason.Instant);
    }

    public bool Remove(
        PlayerEffectType type,
        PlayerEffectScope scope,
        ParentShip owner = null)
    {
        PlayerEffect effect = FindEffect(type, scope, owner);
        if (effect == null)
            return false;

        RemoveEffect(effect, PlayerEffectRemovalReason.Manual);
        return true;
    }

    public bool HasEffect(
        PlayerEffectType type,
        PlayerEffectScope scope,
        ParentShip owner = null)
    {
        return FindEffect(type, scope, owner) != null;
    }

    private void Update()
    {
        for (int index = activeEffects.Count - 1; index >= 0; index--)
        {
            PlayerEffect effect = activeEffects[index];
            if (effect != null && effect.Tick(Time.deltaTime))
                RemoveEffect(effect, PlayerEffectRemovalReason.Expired);
        }
    }

    private void SubscribeToEndConditions(PlayerEffect effect)
    {
        if (effect == null)
            return;

        if ((effect.EndConditions & PlayerEffectEndCondition.OwnerDamaged) != 0
            && effect.Owner != null)
        {
            effect.OwnerDamageHandler = _ => RemoveEffect(
                effect,
                PlayerEffectRemovalReason.EndCondition);
            effect.Owner.OnDamageTaken += effect.OwnerDamageHandler;
        }

        if ((effect.EndConditions & PlayerEffectEndCondition.OwnerDied) != 0
            && effect.Owner != null)
        {
            effect.OwnerDeathHandler = () => RemoveEffect(
                effect,
                PlayerEffectRemovalReason.EndCondition);
            effect.Owner.OnDied += effect.OwnerDeathHandler;
        }

        if ((effect.EndConditions
                & PlayerEffectEndCondition.AbilityDeactivated) != 0
            && effect.SourceAbility != null)
        {
            effect.AbilityDeactivatedHandler = ability =>
            {
                if (ability == effect.SourceAbility)
                {
                    RemoveEffect(
                        effect,
                        PlayerEffectRemovalReason.EndCondition);
                }
            };
            effect.SourceAbility.OnAbilityDeactivated +=
                effect.AbilityDeactivatedHandler;
        }
    }

    private void UnsubscribeFromEndConditions(PlayerEffect effect)
    {
        if (effect == null)
            return;

        if (effect.Owner != null && effect.OwnerDamageHandler != null)
            effect.Owner.OnDamageTaken -= effect.OwnerDamageHandler;
        if (effect.Owner != null && effect.OwnerDeathHandler != null)
            effect.Owner.OnDied -= effect.OwnerDeathHandler;
        if (effect.SourceAbility != null
            && effect.AbilityDeactivatedHandler != null)
        {
            effect.SourceAbility.OnAbilityDeactivated -=
                effect.AbilityDeactivatedHandler;
        }

        effect.OwnerDamageHandler = null;
        effect.OwnerDeathHandler = null;
        effect.AbilityDeactivatedHandler = null;
    }

    private PlayerEffect FindEffect(
        PlayerEffectType type,
        PlayerEffectScope scope,
        ParentShip owner)
    {
        ParentShip expectedOwner = scope == PlayerEffectScope.Team
            ? null
            : owner;

        for (int index = 0; index < activeEffects.Count; index++)
        {
            PlayerEffect effect = activeEffects[index];
            if (effect != null
                && effect.Type == type
                && effect.Scope == scope
                && effect.Owner == expectedOwner)
            {
                return effect;
            }
        }

        return null;
    }

    private void RemoveEffect(
        PlayerEffect effect,
        PlayerEffectRemovalReason reason)
    {
        if (effect == null || !activeEffects.Remove(effect))
            return;

        UnsubscribeFromEndConditions(effect);
        EffectRemoved?.Invoke(effect, reason);
    }

    private void OnDestroy()
    {
        for (int index = activeEffects.Count - 1; index >= 0; index--)
            UnsubscribeFromEndConditions(activeEffects[index]);

        activeEffects.Clear();
    }
}
