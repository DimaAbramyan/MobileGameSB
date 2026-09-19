using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A single damage pool shared by every living player ship. It consumes damage
/// before an individual ship's shield or hull and ends only when depleted.
/// </summary>
[DisallowMultipleComponent]
public sealed class TeamBarrierController : MonoBehaviour
{
    private PlayerController playerController;
    private readonly Dictionary<ParentShip, Func<float, float>> damageHandlers =
        new();
    private readonly List<ParentShip> subscribedShips = new();

    private PlayerEffect activeEffect;
    private float maximumHealth;
    private float currentHealth;

    public bool IsActive => currentHealth > 0f;
    public float MaximumHealth => maximumHealth;
    public float CurrentHealth => currentHealth;
    public float HealthNormalized => maximumHealth <= 0f
        ? 0f
        : Mathf.Clamp01(currentHealth / maximumHealth);

    private void Awake()
    {
        playerController = GetComponent<PlayerController>();
    }

    private void OnTransformChildrenChanged()
    {
        if (IsActive)
            SubscribeToTeamShips();
    }

    /// <summary>
    /// Recasting replaces the old shared pool with the newly generated one.
    /// </summary>
    public bool Activate(float health)
    {
        if (health <= 0f)
            return false;

        playerController ??= GetComponent<PlayerController>();
        if (playerController == null)
            return false;

        SubscribeToTeamShips();
        if (damageHandlers.Count == 0)
            return false;

        maximumHealth = health;
        currentHealth = health;
        activeEffect = playerController.Effects.ApplyOrRefresh(
            PlayerEffectType.TeamBarrier,
            PlayerEffectPolarity.Positive,
            PlayerEffectScope.Team,
            null,
            0f);
        activeEffect.SetProgressNormalized(1f);
        return true;
    }

    private void SubscribeToTeamShips()
    {
        for (int childIndex = 0; childIndex < transform.childCount; childIndex++)
        {
            ParentShip ship = transform.GetChild(childIndex).GetComponent<ParentShip>();
            if (ship == null || damageHandlers.ContainsKey(ship))
                continue;

            ParentShip subscribedShip = ship;
            Func<float, float> handler = damage =>
                AbsorbDamage(subscribedShip, damage);
            damageHandlers.Add(ship, handler);
            subscribedShips.Add(ship);
            ship.OnDamagePipeline += handler;
        }
    }

    private float AbsorbDamage(ParentShip ship, float damage)
    {
        if (damage <= 0f || currentHealth <= 0f)
            return damage;

        float absorbed = Mathf.Min(damage, currentHealth);
        currentHealth -= absorbed;
        UpdateEffectProgress();

        float remainingDamage = damage - absorbed;
        if (currentHealth <= 0f)
            Deactivate();

        return remainingDamage;
    }

    private void UpdateEffectProgress()
    {
        if (activeEffect != null)
            activeEffect.SetProgressNormalized(HealthNormalized);
    }

    private void Deactivate()
    {
        currentHealth = 0f;
        maximumHealth = 0f;

        if (playerController != null)
        {
            playerController.Effects.Remove(
                PlayerEffectType.TeamBarrier,
                PlayerEffectScope.Team);
        }

        activeEffect = null;
        UnsubscribeFromTeamShips();
    }

    private void UnsubscribeFromTeamShips()
    {
        for (int index = subscribedShips.Count - 1; index >= 0; index--)
        {
            ParentShip ship = subscribedShips[index];
            if (ship != null && damageHandlers.TryGetValue(ship, out Func<float, float> handler))
                ship.OnDamagePipeline -= handler;
        }

        subscribedShips.Clear();
        damageHandlers.Clear();
    }

    private void OnDestroy()
    {
        UnsubscribeFromTeamShips();
    }
}
