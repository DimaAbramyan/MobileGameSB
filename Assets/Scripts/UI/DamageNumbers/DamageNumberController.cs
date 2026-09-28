using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Serialization;
using Zenject;

[DisallowMultipleComponent]
public sealed class DamageNumberController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private DamageNumberView damageNumberPrefab;
    [SerializeField] private Transform viewParent;

    [Header("Pool")]
    [SerializeField, Min(0)] private int initialPoolSize = 24;
    [SerializeField, Min(1)] private int maxActiveNumbers = 64;

    [Header("Presentation")]
    [SerializeField, Min(0.01f)] private float baseSize = 1f;
    [SerializeField, Min(0f)] private float upwardSpeed = 1f;
    [Tooltip("World-space height above the enemy visual center.")]
    [SerializeField, Min(0f)] private float verticalOffsetFromTargetCenter = 0.5f;

    [Header("Damage Accumulation")]
    [Tooltip("Time without new damage before the fade animation begins.")]
    [FormerlySerializedAs("fadeStartDelay")]
    [SerializeField, Min(0f)] private float accumulationDuration = 1f;

    [Header("Fade Animation")]
    [SerializeField, Min(0.01f)] private float fadeDuration = 0.5f;

    [Header("Modifier Visual")]
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color maximumBonusColor = new(
        0.65490198f,
        0.07843138f,
        0.07843138f,
        1f);
    [SerializeField] private Color maximumPenaltyColor = new(
        0.08235294f,
        0.71372551f,
        0.7019608f,
        1f);
    [SerializeField, Min(0f)] private float maximumBonusPercent = 25f;
    [SerializeField, Min(0f)] private float maximumPenaltyPercent = 25f;
    [SerializeField, Min(0.01f)] private float minimumScale = 0.9f;
    [SerializeField, Min(0.01f)] private float maximumScale = 1.25f;

    private readonly Dictionary<Entity, DamageNumberView> activeViews = new();
    private readonly Dictionary<int, Entity> aggregateEntities = new();
    private readonly Dictionary<Entity, int> aggregateKeys = new();
    private readonly Dictionary<Entity, Enemy> targetEnemies = new();
    private readonly Stack<DamageNumberView> inactiveViews = new();
    private readonly List<Entity> entitiesToRelease = new();

    private EntityManager entityManager;
    private World entityWorld;
    private DealDamageManager dealDamageManager;
    private bool isSubscribed;
    private bool hasLoggedMissingPrefab;
    private bool hasLoggedMissingWorld;

    public DamageNumberView DamageNumberPrefab => damageNumberPrefab;
    public Transform ViewParent => viewParent;

    public void Configure(
        DamageNumberView prefab,
        Transform parent)
    {
        damageNumberPrefab = prefab;
        viewParent = parent != null ? parent : transform;
        hasLoggedMissingPrefab = false;
    }

    [Inject]
    public void Construct(DealDamageManager damageManager)
    {
        if (dealDamageManager == damageManager)
            return;

        Unsubscribe();
        dealDamageManager = damageManager;
        Subscribe();
    }

    private void Awake()
    {
        if (viewParent == null)
            viewParent = transform;

        PrewarmPool();
    }

    private void OnEnable()
    {
        Subscribe();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void LateUpdate()
    {
        if (activeViews.Count == 0 || !EnsureEntityManager())
            return;

        entitiesToRelease.Clear();
        foreach (KeyValuePair<Entity, DamageNumberView> activeView in activeViews)
        {
            Entity entity = activeView.Key;
            DamageNumberView view = activeView.Value;
            if (view == null
                || !entityManager.Exists(entity)
                || !entityManager.HasComponent<DamageNumberData>(entity))
            {
                entitiesToRelease.Add(entity);
                continue;
            }

            DamageNumberData data = entityManager.GetComponentData<DamageNumberData>(entity);
            if (data.Age >= data.Lifetime)
            {
                entitiesToRelease.Add(entity);
                continue;
            }

            if (TryUpdateFollowPosition(entity, ref data))
                entityManager.SetComponentData(entity, data);

            view.SetPresentation(
                new Vector3(data.Position.x, data.Position.y, data.Position.z),
                GetColor(data.ModifierPercent, GetAlpha(data)),
                GetScale(data.ModifierPercent));
        }

        for (int index = 0; index < entitiesToRelease.Count; index++)
            Release(entitiesToRelease[index]);
    }

    private void OnDestroy()
    {
        Unsubscribe();

        entitiesToRelease.Clear();
        foreach (Entity entity in activeViews.Keys)
            entitiesToRelease.Add(entity);

        for (int index = 0; index < entitiesToRelease.Count; index++)
            Release(entitiesToRelease[index]);
    }

    private void Subscribe()
    {
        if (isSubscribed || dealDamageManager == null)
            return;

        dealDamageManager.DamageApplied += CreateDamageNumber;
        isSubscribed = true;
    }

    private void Unsubscribe()
    {
        if (!isSubscribed || dealDamageManager == null)
            return;

        dealDamageManager.DamageApplied -= CreateDamageNumber;
        isSubscribed = false;
    }

    private void CreateDamageNumber(DamageNumberRequest request)
    {
        if (request.Damage <= 0f)
        {
            return;
        }

        if (!EnsureEntityManager())
        {
            if (!hasLoggedMissingWorld)
            {
                Debug.LogError(
                    $"{nameof(DamageNumberController)} needs an active default ECS World.",
                    this);
                hasLoggedMissingWorld = true;
            }

            return;
        }

        if (TryUpdateAggregate(request))
            return;

        if (activeViews.Count >= maxActiveNumbers)
            return;

        DamageNumberView view = GetView();
        if (view == null)
            return;

        Entity entity = entityManager.CreateEntity(typeof(DamageNumberData));
        var data = new DamageNumberData
        {
            Position = GetDisplayPosition(request.WorldPosition),
            Damage = request.Damage,
            ModifierPercent = request.ModifierPercent,
            Age = 0f,
            Lifetime = GetEffectiveLifetime(),
            FadeStartDelay = accumulationDuration,
            FadeDuration = fadeDuration,
            UpwardSpeed = upwardSpeed
        };
        entityManager.SetComponentData(entity, data);
        activeViews.Add(entity, view);
        aggregateEntities[request.AggregationKey] = entity;
        aggregateKeys[entity] = request.AggregationKey;
        if (request.Target != null)
            targetEnemies[entity] = request.Target;
        view.Show(
            new Vector3(data.Position.x, data.Position.y, data.Position.z),
            data.Damage,
            GetColor(data.ModifierPercent, 1f),
            GetScale(data.ModifierPercent));
    }

    private bool TryUpdateAggregate(DamageNumberRequest request)
    {
        if (!aggregateEntities.TryGetValue(
                request.AggregationKey,
                out Entity entity))
        {
            return false;
        }

        if (!activeViews.TryGetValue(entity, out DamageNumberView view)
            || view == null
            || !entityManager.Exists(entity)
            || !entityManager.HasComponent<DamageNumberData>(entity))
        {
            Release(entity);
            return false;
        }

        DamageNumberData data = entityManager.GetComponentData<DamageNumberData>(entity);
        if (data.Age >= data.Lifetime)
        {
            Release(entity);
            return false;
        }

        float previousDamage = data.Damage;
        data.Damage += request.Damage;
        data.ModifierPercent = GetCombinedModifierPercent(
            previousDamage,
            data.ModifierPercent,
            request.Damage,
            request.ModifierPercent);
        data.Position = GetDisplayPosition(request.WorldPosition);
        data.Age = 0f;
        data.Lifetime = GetEffectiveLifetime();
        data.FadeStartDelay = accumulationDuration;
        data.FadeDuration = fadeDuration;
        data.UpwardSpeed = upwardSpeed;
        entityManager.SetComponentData(entity, data);

        if (request.Target != null)
            targetEnemies[entity] = request.Target;

        view.SetAmount(data.Damage);
        view.SetPresentation(
            new Vector3(data.Position.x, data.Position.y, data.Position.z),
            GetColor(data.ModifierPercent, 1f),
            GetScale(data.ModifierPercent));
        return true;
    }

    private DamageNumberView GetView()
    {
        while (inactiveViews.Count > 0)
        {
            DamageNumberView view = inactiveViews.Pop();
            if (view != null)
                return view;
        }

        if (damageNumberPrefab == null)
        {
            if (!hasLoggedMissingPrefab)
            {
                Debug.LogError(
                    $"{nameof(DamageNumberController)} needs a {nameof(DamageNumberView)} prefab.",
                    this);
                hasLoggedMissingPrefab = true;
            }

            return null;
        }

        return Instantiate(damageNumberPrefab, viewParent);
    }

    private void Release(Entity entity)
    {
        if (!activeViews.Remove(entity, out DamageNumberView view))
        {
            RemoveAggregateMapping(entity);
            return;
        }

        RemoveAggregateMapping(entity);
        targetEnemies.Remove(entity);

        if (entityWorld != null
            && entityWorld.IsCreated
            && entityManager.Exists(entity))
            entityManager.DestroyEntity(entity);

        if (view == null)
            return;

        view.Hide();
        inactiveViews.Push(view);
    }

    private void PrewarmPool()
    {
        if (damageNumberPrefab == null)
            return;

        for (int index = 0; index < initialPoolSize; index++)
        {
            DamageNumberView view = Instantiate(damageNumberPrefab, viewParent);
            view.Hide();
            inactiveViews.Push(view);
        }
    }

    private bool EnsureEntityManager()
    {
        if (entityWorld != null && entityWorld.IsCreated)
            return true;

        World world = World.DefaultGameObjectInjectionWorld;
        if (world == null || !world.IsCreated)
            return false;

        entityWorld = world;
        entityManager = entityWorld.EntityManager;
        return true;
    }

    private float GetEffectiveLifetime()
    {
        return Mathf.Max(0.01f, accumulationDuration + fadeDuration);
    }

    private float3 GetDisplayPosition(Vector3 targetCenter)
    {
        return new float3(
            targetCenter.x,
            targetCenter.y + verticalOffsetFromTargetCenter,
            targetCenter.z);
    }

    private bool TryUpdateFollowPosition(
        Entity entity,
        ref DamageNumberData data)
    {
        if (!targetEnemies.TryGetValue(entity, out Enemy target)
            || target == null)
        {
            targetEnemies.Remove(entity);
            return false;
        }

        data.Position = GetDisplayPosition(target.DamageNumberCenter);
        data.Position.y += data.UpwardSpeed * Mathf.Max(
            0f,
            Mathf.Min(data.Age, data.Lifetime) - data.FadeStartDelay);
        return true;
    }

    private void RemoveAggregateMapping(Entity entity)
    {
        if (!aggregateKeys.Remove(entity, out int key))
            return;

        if (aggregateEntities.TryGetValue(key, out Entity mappedEntity)
            && mappedEntity == entity)
        {
            aggregateEntities.Remove(key);
        }
    }

    private static float GetCombinedModifierPercent(
        float firstDamage,
        float firstModifierPercent,
        float secondDamage,
        float secondModifierPercent)
    {
        float totalDamage = firstDamage + secondDamage;
        if (totalDamage <= 0f)
            return 0f;

        return (firstDamage * firstModifierPercent
            + secondDamage * secondModifierPercent)
            / totalDamage;
    }

    private float GetAlpha(DamageNumberData data)
    {
        if (data.Age <= data.FadeStartDelay)
            return 1f;

        return 1f - Mathf.Clamp01(
            (data.Age - data.FadeStartDelay)
            / Mathf.Max(0.01f, data.FadeDuration));
    }

    private Color GetColor(float modifierPercent, float alpha)
    {
        Color color = normalColor;
        if (modifierPercent > 0f && maximumBonusPercent > 0f)
        {
            color = Color.Lerp(
                normalColor,
                maximumBonusColor,
                Mathf.Clamp01(modifierPercent / maximumBonusPercent));
        }
        else if (modifierPercent < 0f && maximumPenaltyPercent > 0f)
        {
            color = Color.Lerp(
                normalColor,
                maximumPenaltyColor,
                Mathf.Clamp01(-modifierPercent / maximumPenaltyPercent));
        }

        color.a *= Mathf.Clamp01(alpha);
        return color;
    }

    private float GetScale(float modifierPercent)
    {
        if (modifierPercent > 0f && maximumBonusPercent > 0f)
        {
            return baseSize * Mathf.Lerp(
                1f,
                maximumScale,
                Mathf.Clamp01(modifierPercent / maximumBonusPercent));
        }

        if (modifierPercent < 0f && maximumPenaltyPercent > 0f)
        {
            return baseSize * Mathf.Lerp(
                1f,
                minimumScale,
                Mathf.Clamp01(-modifierPercent / maximumPenaltyPercent));
        }

        return baseSize;
    }

    private void OnValidate()
    {
        initialPoolSize = Mathf.Max(0, initialPoolSize);
        maxActiveNumbers = Mathf.Max(1, maxActiveNumbers);
        baseSize = Mathf.Max(0.01f, baseSize);
        upwardSpeed = Mathf.Max(0f, upwardSpeed);
        verticalOffsetFromTargetCenter = Mathf.Max(
            0f,
            verticalOffsetFromTargetCenter);
        accumulationDuration = Mathf.Max(0f, accumulationDuration);
        fadeDuration = Mathf.Max(0.01f, fadeDuration);
        maximumBonusPercent = Mathf.Max(0f, maximumBonusPercent);
        maximumPenaltyPercent = Mathf.Max(0f, maximumPenaltyPercent);
        minimumScale = Mathf.Max(0.01f, minimumScale);
        maximumScale = Mathf.Max(0.01f, maximumScale);
    }
}
