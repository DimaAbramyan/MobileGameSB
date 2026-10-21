using System;
using UnityEngine;

public enum EnemyLaserLengthMode { ToScreenEdge, Custom }
public enum EnemyLaserDamageMode { OnContact, Periodic }

[Serializable]
public sealed class EnemyLaserGeometry
{
    [SerializeField] private EnemyLaserLengthMode lengthMode;
    [SerializeField, Min(0f)] private float laserLength = 10f;
    [SerializeField] private float lengthOffset;
    [SerializeField, Min(0f)] private float laserWidth = .15f;
    [SerializeField, Tooltip("World-space offset from the resolved emission point. Fan/Stack keep their existing emission geometry.")]
    private Vector2 laserOffset;
    [SerializeField] private bool constantWidth = true;
    [SerializeField] private AnimationCurve widthCurve = AnimationCurve.Linear(0f, 1f, 1f, 1f);
    [SerializeField] private bool constantLength = true;
    [SerializeField] private AnimationCurve lengthCurve = AnimationCurve.Linear(0f, 1f, 1f, 1f);

    public EnemyLaserLengthMode LengthMode => lengthMode;
    public float LaserLength => Mathf.Max(0f, laserLength);
    public float LengthOffset => lengthOffset;
    public float LaserWidth => Mathf.Max(0f, laserWidth);
    public Vector2 LaserOffset => laserOffset;
    public bool ConstantWidth => constantWidth;
    public bool ConstantLength => constantLength;
    public float WidthAt(float normalizedTime) => LaserWidth * (constantWidth ? 1f : Curve(widthCurve, normalizedTime));
    public float LengthAt(float baseLength, float normalizedTime) => baseLength * (constantLength ? 1f : Curve(lengthCurve, normalizedTime));
    private static float Curve(AnimationCurve curve, float time)
        => Mathf.Max(0f, curve != null && curve.length > 0 ? curve.Evaluate(Mathf.Clamp01(time)) : 1f);
    public float ResolveLength(Vector3 start, Vector3 direction, Rect bounds)
        => lengthMode == EnemyLaserLengthMode.Custom ? LaserLength
            : Mathf.Max(0f, EnemyLaserGeometryUtility.ToScreenEdge(start, direction, bounds) + lengthOffset);

    public EnemyLaserGeometry Copy() => new EnemyLaserGeometry
    {
        lengthMode = lengthMode, laserLength = laserLength, lengthOffset = lengthOffset,
        laserWidth = laserWidth, laserOffset = laserOffset, constantWidth = constantWidth, constantLength = constantLength,
        widthCurve = Clone(widthCurve), lengthCurve = Clone(lengthCurve)
    };
    private static AnimationCurve Clone(AnimationCurve curve) => curve == null ? null
        : new AnimationCurve(curve.keys) { preWrapMode = curve.preWrapMode, postWrapMode = curve.postWrapMode };
}

[Serializable]
public sealed class EnemyLaserSettings
{
    [SerializeField] private EnemyLaserGeometry geometry = new();
    [SerializeField, Min(.01f)] private float laserDuration = 2f;
    [SerializeField, Min(0f), Tooltip("Silent delay before Telegraph (if enabled), then activation.")]
    private float activationDelay;
    [SerializeField, Min(0f)] private float fadeInDuration = .1f;
    [SerializeField, Min(0f), Tooltip("Visual tail after the active duration; the fading tail does not deal damage.")]
    private float fadeOutDuration = .2f;
    [SerializeField] private bool followOrigin;
    [SerializeField] private bool surviveSourceDestruction = true;
    [SerializeField, Min(0f)] private float damage = 1f;
    [SerializeField] private EnemyLaserDamageMode damageMode;
    [SerializeField, Min(.01f)] private float damageInterval = .5f;
    [SerializeField] private bool enableTelegraph = true;
    [SerializeField] private bool overrideTelegraphGeometry;
    [SerializeField] private EnemyLaserGeometry telegraphGeometry = new();
    [SerializeField, Min(0f)] private float telegraphDuration = 1f;
    [SerializeField] private Color telegraphColor = Color.red;
    [SerializeField, Range(0f, 1f)] private float telegraphOpacity = .3f;
    [SerializeField, Min(0f)] private float telegraphFadeIn = .1f;
    [SerializeField, Min(0f)] private float telegraphFadeOut = .1f;

    public EnemyLaserGeometry Geometry => geometry ??= new();
    public EnemyLaserGeometry TelegraphGeometry => overrideTelegraphGeometry ? telegraphGeometry ??= new() : Geometry;
    public float LaserDuration => Mathf.Max(.01f, laserDuration);
    public float ActivationDelay => Mathf.Max(0f, activationDelay);
    public float FadeInDuration => Mathf.Max(0f, fadeInDuration);
    public float FadeOutDuration => Mathf.Max(0f, fadeOutDuration);
    public bool FollowOrigin => followOrigin;
    public bool SurviveSourceDestruction => surviveSourceDestruction;
    public float Damage => Mathf.Max(0f, damage);
    public EnemyLaserDamageMode DamageMode => damageMode;
    public float DamageInterval => Mathf.Max(.01f, damageInterval);
    public bool EnableTelegraph => enableTelegraph;
    public float TelegraphDuration => enableTelegraph ? Mathf.Max(0f, telegraphDuration) : 0f;
    public Color TelegraphColor => telegraphColor;
    public float TelegraphOpacity => Mathf.Clamp01(telegraphOpacity);
    public float TelegraphFadeIn => Mathf.Max(0f, telegraphFadeIn);
    public float TelegraphFadeOut => Mathf.Max(0f, telegraphFadeOut);
    public float ActiveStart => ActivationDelay + TelegraphDuration;
    public float TotalDuration => ActiveStart + LaserDuration + FadeOutDuration;

    public EnemyLaserSettings Copy() => new EnemyLaserSettings
    {
        geometry = Geometry.Copy(), laserDuration = laserDuration, activationDelay = activationDelay,
        fadeInDuration = fadeInDuration, fadeOutDuration = fadeOutDuration, followOrigin = followOrigin,
        surviveSourceDestruction = surviveSourceDestruction, damage = damage, damageMode = damageMode,
        damageInterval = damageInterval, enableTelegraph = enableTelegraph, overrideTelegraphGeometry = overrideTelegraphGeometry,
        telegraphGeometry = telegraphGeometry?.Copy(), telegraphDuration = telegraphDuration,
        telegraphColor = telegraphColor, telegraphOpacity = telegraphOpacity,
        telegraphFadeIn = telegraphFadeIn, telegraphFadeOut = telegraphFadeOut
    };
}
