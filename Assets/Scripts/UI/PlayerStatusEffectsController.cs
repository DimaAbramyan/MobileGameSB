using System;
using System.Collections.Generic;
using UnityEngine;
using Zenject;

[DisallowMultipleComponent]
public sealed class PlayerStatusEffectsController : MonoBehaviour
{
    [Serializable]
    private struct EffectIconBinding
    {
        [SerializeField] private PlayerEffectType effectType;
        [SerializeField] private Sprite icon;

        public PlayerEffectType EffectType => effectType;
        public Sprite Icon => icon;

        public EffectIconBinding(PlayerEffectType effectType, Sprite icon)
        {
            this.effectType = effectType;
            this.icon = icon;
        }
    }

    [Header("UI")]
    [SerializeField] private Transform statusBar;
    [SerializeField] private StatusEffectView statusEffectPrefab;

    [Header("Gameplay")]
    [SerializeField] private PlayerController playerController;

    [Header("Effect icons")]
    [SerializeField] private EffectIconBinding[] effectIcons = Array.Empty<EffectIconBinding>();

    private readonly Dictionary<PlayerEffect, StatusEffectView> effectViews = new();
    private readonly List<PlayerEffect> effectsToRemove = new();

    private PlayerEffectController effectController;
    private bool configurationWarningLogged;

    [Inject]
    private void Construct(PlayerController controller)
    {
        SetPlayerController(controller);
    }

    private void Awake()
    {
        if (statusBar == null)
            statusBar = transform;
    }

    private void OnEnable()
    {
        SubscribeToEffects();
    }

    private void OnDisable()
    {
        UnsubscribeFromEffects();
        ClearViews();
    }

    private void Update()
    {
        SynchronizeViews();
    }

    private void OnValidate()
    {
        if (statusBar == null)
            statusBar = transform;

        EnsureIconBindings();
    }

    private void SubscribeToEffects()
    {
        if (!isActiveAndEnabled || playerController == null || effectController != null)
            return;

        effectController = playerController.Effects;
        if (effectController == null)
            return;

        effectController.EffectApplied += HandleEffectApplied;
        effectController.EffectRefreshed += HandleEffectRefreshed;
        effectController.EffectRemoved += HandleEffectRemoved;

        SynchronizeViews();
    }

    private void SetPlayerController(PlayerController controller)
    {
        if (playerController == controller)
            return;

        UnsubscribeFromEffects();
        ClearViews();

        playerController = controller;
        SubscribeToEffects();
    }

    private void UnsubscribeFromEffects()
    {
        if (effectController == null)
            return;

        effectController.EffectApplied -= HandleEffectApplied;
        effectController.EffectRefreshed -= HandleEffectRefreshed;
        effectController.EffectRemoved -= HandleEffectRemoved;
        effectController = null;
    }

    private void HandleEffectApplied(PlayerEffect effect)
    {
        if (IsActiveEffect(effect))
            CreateOrRefreshView(effect);
    }

    private void HandleEffectRefreshed(PlayerEffect effect)
    {
        if (IsActiveEffect(effect))
            CreateOrRefreshView(effect);
    }

    private void HandleEffectRemoved(PlayerEffect effect, PlayerEffectRemovalReason _)
    {
        RemoveView(effect);
    }

    private void SynchronizeViews()
    {
        if (effectController == null)
            return;

        IReadOnlyList<PlayerEffect> activeEffects = effectController.ActiveEffects;
        for (int index = 0; index < activeEffects.Count; index++)
        {
            PlayerEffect effect = activeEffects[index];
            if (effect != null)
                CreateOrRefreshView(effect);
        }

        effectsToRemove.Clear();
        foreach (KeyValuePair<PlayerEffect, StatusEffectView> pair in effectViews)
        {
            if (!IsActiveEffect(pair.Key))
                effectsToRemove.Add(pair.Key);
        }

        for (int index = 0; index < effectsToRemove.Count; index++)
            RemoveView(effectsToRemove[index]);
    }

    private void CreateOrRefreshView(PlayerEffect effect)
    {
        if (effect == null)
            return;

        if (!effectViews.TryGetValue(effect, out StatusEffectView view) || view == null)
        {
            if (statusEffectPrefab == null || statusBar == null)
            {
                LogMissingConfiguration();
                return;
            }

            view = Instantiate(statusEffectPrefab, statusBar);
            view.name = $"{effect.Type} Status Effect";
            effectViews[effect] = view;
            PlaceViewByScope(effect, view);
        }

        view.Set(effect, GetIcon(effect.Type));
    }

    private void PlaceViewByScope(PlayerEffect effect, StatusEffectView view)
    {
        if (effect == null || view == null)
            return;

        if (effect.Scope == PlayerEffectScope.SingleShip)
        {
            view.transform.SetAsLastSibling();
            return;
        }

        int teamEffectCount = 0;
        IReadOnlyList<PlayerEffect> activeEffects = effectController?.ActiveEffects;
        if (activeEffects != null)
        {
            for (int index = 0; index < activeEffects.Count; index++)
            {
                PlayerEffect activeEffect = activeEffects[index];
                if (activeEffect == effect
                    || activeEffect == null
                    || activeEffect.Scope != PlayerEffectScope.Team)
                {
                    continue;
                }

                if (effectViews.TryGetValue(activeEffect, out StatusEffectView teamView)
                    && teamView != null)
                {
                    teamEffectCount++;
                }
            }
        }

        view.transform.SetSiblingIndex(teamEffectCount);
    }

    private void RemoveView(PlayerEffect effect)
    {
        if (effect == null || !effectViews.TryGetValue(effect, out StatusEffectView view))
            return;

        effectViews.Remove(effect);

        if (view != null)
            Destroy(view.gameObject);
    }

    private void ClearViews()
    {
        foreach (KeyValuePair<PlayerEffect, StatusEffectView> pair in effectViews)
        {
            if (pair.Value != null)
                Destroy(pair.Value.gameObject);
        }

        effectViews.Clear();
        effectsToRemove.Clear();
    }

    private bool IsActiveEffect(PlayerEffect effect)
    {
        if (effectController == null || effect == null)
            return false;

        IReadOnlyList<PlayerEffect> activeEffects = effectController.ActiveEffects;
        for (int index = 0; index < activeEffects.Count; index++)
        {
            if (activeEffects[index] == effect)
                return true;
        }

        return false;
    }

    private Sprite GetIcon(PlayerEffectType effectType)
    {
        for (int index = 0; index < effectIcons.Length; index++)
        {
            if (effectIcons[index].EffectType == effectType)
                return effectIcons[index].Icon;
        }

        return null;
    }

    private void LogMissingConfiguration()
    {
        if (configurationWarningLogged)
            return;

        configurationWarningLogged = true;
        Debug.LogError(
            $"{nameof(PlayerStatusEffectsController)} requires a StatusEffect prefab and StatusBar container.",
            this);
    }

    private void EnsureIconBindings()
    {
        PlayerEffectType[] effectTypes = (PlayerEffectType[])Enum.GetValues(typeof(PlayerEffectType));
        if (effectIcons.Length == effectTypes.Length && HasAllIconBindings(effectTypes))
            return;

        var iconsByType = new Dictionary<PlayerEffectType, Sprite>();
        for (int index = 0; index < effectIcons.Length; index++)
            iconsByType[effectIcons[index].EffectType] = effectIcons[index].Icon;

        effectIcons = new EffectIconBinding[effectTypes.Length];
        for (int index = 0; index < effectTypes.Length; index++)
        {
            PlayerEffectType effectType = effectTypes[index];
            iconsByType.TryGetValue(effectType, out Sprite icon);
            effectIcons[index] = new EffectIconBinding(effectType, icon);
        }
    }

    private bool HasAllIconBindings(PlayerEffectType[] effectTypes)
    {
        for (int index = 0; index < effectTypes.Length; index++)
        {
            bool found = false;
            for (int iconIndex = 0; iconIndex < effectIcons.Length; iconIndex++)
            {
                if (effectIcons[iconIndex].EffectType != effectTypes[index])
                    continue;

                found = true;
                break;
            }

            if (!found)
                return false;
        }

        return true;
    }
}
