using System.Collections.Generic;
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

    private readonly Dictionary<int, ActiveDamageNumber> activeNumbers = new();
    private readonly Dictionary<int, int> aggregateNumberIds = new();
    private readonly Stack<DamageNumberView> inactiveViews = new();
    private readonly List<int> activeNumberIds = new(64);
    private readonly List<int> numbersToRelease = new();

    private DealDamageManager dealDamageManager;
    private int nextActiveNumberId;
    private bool isSubscribed;
    private bool hasLoggedMissingPrefab;

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
        if (activeNumbers.Count == 0)
            return;

        float deltaTime = Time.deltaTime;
        activeNumberIds.Clear();
        numbersToRelease.Clear();
        foreach (int numberId in activeNumbers.Keys)
            activeNumberIds.Add(numberId);

        for (int index = 0; index < activeNumberIds.Count; index++)
        {
            int numberId = activeNumberIds[index];
            if (!activeNumbers.TryGetValue(numberId, out ActiveDamageNumber number))
                continue;

            DamageNumberView view = number.View;
            if (view == null)
            {
                numbersToRelease.Add(numberId);
                continue;
            }

            DamageNumberData data = number.Data;
            data.Age += deltaTime;
            if (data.Age >= data.Lifetime)
            {
                numbersToRelease.Add(numberId);
                continue;
            }

            TryUpdateFollowPosition(number.Target, ref data);
            number.Data = data;
            activeNumbers[numberId] = number;

            view.SetPresentation(
                new Vector3(data.Position.x, data.Position.y, data.Position.z),
                GetColor(data.ModifierPercent, GetAlpha(data)),
                GetScale(data.ModifierPercent));
        }

        for (int index = 0; index < numbersToRelease.Count; index++)
            Release(numbersToRelease[index]);
    }

    private void OnDestroy()
    {
        Unsubscribe();

        numbersToRelease.Clear();
        foreach (int numberId in activeNumbers.Keys)
            numbersToRelease.Add(numberId);

        for (int index = 0; index < numbersToRelease.Count; index++)
            Release(numbersToRelease[index]);
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

        if (TryUpdateAggregate(request))
            return;

        if (activeNumbers.Count >= maxActiveNumbers)
            return;

        DamageNumberView view = GetView();
        if (view == null)
            return;

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
        int numberId = nextActiveNumberId++;
        activeNumbers.Add(numberId, new ActiveDamageNumber(
            view,
            data,
            request.AggregationKey,
            request.Target));
        aggregateNumberIds[request.AggregationKey] = numberId;
        view.Show(
            new Vector3(data.Position.x, data.Position.y, data.Position.z),
            data.Damage,
            GetColor(data.ModifierPercent, 1f),
            GetScale(data.ModifierPercent));
    }

    private bool TryUpdateAggregate(DamageNumberRequest request)
    {
        if (!aggregateNumberIds.TryGetValue(
                request.AggregationKey,
                out int numberId))
        {
            return false;
        }

        if (!activeNumbers.TryGetValue(numberId, out ActiveDamageNumber number))
        {
            aggregateNumberIds.Remove(request.AggregationKey);
            return false;
        }

        if (number.View == null)
        {
            Release(numberId);
            return false;
        }

        DamageNumberData data = number.Data;
        if (data.Age >= data.Lifetime)
        {
            Release(numberId);
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
        number.Data = data;

        if (request.Target != null)
            number.Target = request.Target;

        activeNumbers[numberId] = number;

        number.View.SetAmount(data.Damage);
        number.View.SetPresentation(
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

    private void Release(int numberId)
    {
        if (!activeNumbers.Remove(numberId, out ActiveDamageNumber number))
            return;

        RemoveAggregateMapping(numberId, number.AggregationKey);

        DamageNumberView view = number.View;
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
        Enemy target,
        ref DamageNumberData data)
    {
        if (target == null)
            return false;

        data.Position = GetDisplayPosition(target.DamageNumberCenter);
        data.Position.y += data.UpwardSpeed * Mathf.Max(
            0f,
            Mathf.Min(data.Age, data.Lifetime) - data.FadeStartDelay);
        return true;
    }

    private void RemoveAggregateMapping(int numberId, int aggregationKey)
    {
        if (aggregateNumberIds.TryGetValue(aggregationKey, out int mappedNumberId)
            && mappedNumberId == numberId)
        {
            aggregateNumberIds.Remove(aggregationKey);
        }
    }

    private struct ActiveDamageNumber
    {
        public readonly DamageNumberView View;
        public readonly int AggregationKey;
        public DamageNumberData Data;
        public Enemy Target;

        public ActiveDamageNumber(
            DamageNumberView view,
            DamageNumberData data,
            int aggregationKey,
            Enemy target)
        {
            View = view;
            Data = data;
            AggregationKey = aggregationKey;
            Target = target;
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
