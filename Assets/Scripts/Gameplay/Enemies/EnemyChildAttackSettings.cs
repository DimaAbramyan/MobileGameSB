using System;
using UnityEngine;

public enum EnemyChildAttackTriggerMode { [InspectorName("After Time")] AfterTime, [InspectorName("After Distance")] AfterDistance, [InspectorName("On Event")] OnEvent }
public enum EnemyChildAttackEvent { [InspectorName("On Spawn")] OnSpawn, [InspectorName("Hit Player")] HitPlayer, Intercepted, Expired, [InspectorName("Burst At Point")] BurstAtPoint, [InspectorName("On Screen Boundary Reached")] OnScreenBoundaryReached }
public enum EnemyChildAttackBoundary { Any, Top, Bottom, Left, Right }
[Flags]
public enum EnemyChildAttackBoundaries { None = 0, Left = 1, Right = 2, Top = 4, Bottom = 8, All = Left | Right | Top | Bottom }

public static class EnemyChildAttackBoundarySelection
{
    public static EnemyChildAttackBoundaries FromLegacy(EnemyChildAttackBoundary boundary) => boundary switch
    {
        EnemyChildAttackBoundary.Top => EnemyChildAttackBoundaries.Top,
        EnemyChildAttackBoundary.Bottom => EnemyChildAttackBoundaries.Bottom,
        EnemyChildAttackBoundary.Left => EnemyChildAttackBoundaries.Left,
        EnemyChildAttackBoundary.Right => EnemyChildAttackBoundaries.Right,
        _ => EnemyChildAttackBoundaries.All
    };
    public static EnemyChildAttackBoundaries Resolve(int selection, EnemyChildAttackBoundary legacy)
        => selection < 0 ? FromLegacy(legacy) : (EnemyChildAttackBoundaries)selection & EnemyChildAttackBoundaries.All;
}
public enum EnemyChildAttackOffsetSpace { World, Local }
public enum EnemyChildAttackOriginMode { [InspectorName("Follow Parent")] FollowParent, [InspectorName("Capture On Start")] CaptureOnStart }

[Serializable]
public sealed partial class EnemyChildAttackSettings
{
    public const int MaximumDepth = 8;
    public const int MaximumDescendants = 2048;
    public const int MaximumPerStep = 512;
    public const int MaximumTrackedParents = 4096;
    public const int MaximumLiveProjectiles = 8192;
    [ThreadStatic] private static int copyDepth;
    [ThreadStatic] private static int validationDepth;

    [SerializeField] private EnemyChildAttackTriggerMode triggerMode;
    [SerializeField] [Randomizable(nameof(triggerValueRandom), 0f, EnemyNumericScopes.None, EnemyNumericRandomizeOn.PerProjectile)]
    private float triggerValue = 1f;
    [SerializeField] private EnemyChildAttackEvent eventType = EnemyChildAttackEvent.Expired;
    [SerializeField, HideInInspector] private EnemyChildAttackBoundary boundary;
    // Negative means inherit the old field. Do not materialize it during validation:
    // existing prefab variants may override only `boundary` and must keep doing so.
    [SerializeField, HideInInspector] private int triggerBoundaries = -1;
    [SerializeField, Tooltip("World units from the gameplay viewport edge. Positive points inward; negative points outward. Uses the projectile centre.")]
    [Randomizable(nameof(boundaryOffsetRandom), float.MinValue, EnemyNumericScopes.None, EnemyNumericRandomizeOn.PerProjectile)]
    private float boundaryOffset;
    [SerializeField, Tooltip("Fire on the first crossing only. When disabled, each new crossing starts an attack, including while a previous attack is running. Destroy Parent still waits for all scheduled attacks.")]
    private bool triggerOnce = true;
    [SerializeField, Tooltip("Delay in scaled game seconds after crossing. Capture On Start captures the crossing position; Follow Parent uses the moving parent's position.")]
    [Randomizable(nameof(timeOffsetRandom), 0f, EnemyNumericScopes.None, EnemyNumericRandomizeOn.PerProjectile)]
    private float timeOffset;
    [SerializeField, Tooltip("Total attack executions. 1 fires once. Repeat Interval starts after the previous attack finishes, including delayed Sector launches.")]
    [Randomizable(nameof(repeatCountRandom), 1f, EnemyNumericScopes.None, EnemyNumericRandomizeOn.PerProjectile)]
    private int repeatCount = 1;
    [SerializeField] [Randomizable(nameof(repeatIntervalRandom), 0f, EnemyNumericScopes.None, EnemyNumericRandomizeOn.PerProjectile)]
    private float repeatInterval = 1f;
    [SerializeField, Tooltip("Keeps the parent alive past its lifetime while the child attack is active, then expires it after all delayed launches. Hits/interception still resolve normally. Expiry-triggered attacks continue from the final parent pose.")]
    private bool destroyParent;
    [SerializeField] [Randomizable(nameof(spawnOffsetXRandom), nameof(spawnOffsetYRandom), EnemyNumericScopes.None, EnemyNumericRandomizeOn.PerProjectile)]
    private Vector2 spawnOffset;
    [SerializeField, Tooltip("Local X is right and Local Y is forward relative to the parent's current direction of travel.")]
    private EnemyChildAttackOffsetSpace offsetSpace;
    [SerializeField] private EnemyChildAttackOriginMode originMode;
    [SerializeField] private DirectedWaveAttackFireMode fireMode = DirectedWaveAttackFireMode.Default;
    [SerializeField] private DirectedWaveAttackAimMode aimMode;
    [SerializeField] private EnemyStationaryFireSettings stationaryFireSettings = new();
    [SerializeField] private EnemyAttackPatternSettings pattern = new();

    public EnemyChildAttackTriggerMode TriggerMode => triggerMode;
    public float TriggerValue => Mathf.Max(0f, triggerValue);
    public EnemyChildAttackEvent EventType => eventType;
    public bool UsesScreenBoundary => triggerMode == EnemyChildAttackTriggerMode.OnEvent && eventType == EnemyChildAttackEvent.OnScreenBoundaryReached;
    public EnemyChildAttackBoundary Boundary => boundary;
    public EnemyChildAttackBoundaries TriggerBoundaries => EnemyChildAttackBoundarySelection.Resolve(triggerBoundaries, boundary);
    public void SetTriggerBoundaries(EnemyChildAttackBoundaries selection)
        => triggerBoundaries = (int)(selection & EnemyChildAttackBoundaries.All);
    public float BoundaryOffset => float.IsFinite(boundaryOffset) ? boundaryOffset : 0f;
    public bool TriggerOnce => triggerOnce;
    public float TimeOffset => float.IsFinite(timeOffset) ? Mathf.Max(0f, timeOffset) : 0f;
    public int RepeatCount => Mathf.Clamp(repeatCount, 1, 32);
    public float RepeatInterval => Mathf.Max(0f, repeatInterval);
    public bool DestroyParent => destroyParent;
    public Vector2 SpawnOffset => spawnOffset;
    public EnemyChildAttackOffsetSpace OffsetSpace => offsetSpace;
    public EnemyChildAttackOriginMode OriginMode => originMode;
    public DirectedWaveAttackFireMode FireMode => Pattern.Shooting.PresetFiring.enabled ? Pattern.Shooting.PresetFiring.fireMode : fireMode;
    public DirectedWaveAttackAimMode AimMode => Pattern.Shooting.PresetFiring.enabled ? Pattern.Shooting.PresetFiring.aimMode : aimMode;
    public EnemyStationaryFireSettings Stationary => Pattern.Shooting.PresetFiring.enabled ? Pattern.Shooting.PresetFiring.stationaryFireSettings : stationaryFireSettings ??= new EnemyStationaryFireSettings();
    public EnemyAttackPatternSettings Pattern => pattern ??= new EnemyAttackPatternSettings();

    public void Validate()
    {
        if (validationDepth >= MaximumDepth) return;
        validationDepth++;
        try
        {
            triggerValue = float.IsNaN(triggerValue) || float.IsInfinity(triggerValue) ? 0f : Mathf.Max(0f, triggerValue);
            boundaryOffset = BoundaryOffset;
            timeOffset = TimeOffset;
            repeatCount = Mathf.Clamp(repeatCount, 1, 32);
            if (EnemyNumericSample.Enabled(repeatCountRandom))
            { repeatCountRandom.min = Mathf.Clamp(repeatCountRandom.min, 1, 32); repeatCountRandom.max = Mathf.Clamp(repeatCountRandom.max, 1, 32); }
            repeatInterval = float.IsNaN(repeatInterval) || float.IsInfinity(repeatInterval) ? 0f : Mathf.Max(0f, repeatInterval);
            Pattern.Validate();
        }
        finally { validationDepth--; }
    }

    public Vector3 ResolveOrigin(Vector3 position, Vector3 forward)
    {
        Vector3 offset = spawnOffset;
        if (offsetSpace == EnemyChildAttackOffsetSpace.Local)
            offset = Quaternion.FromToRotation(Vector3.up, forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector3.down) * offset;
        return position + offset;
    }

    public static EnemyChildAttackSettings Copy(EnemyChildAttackSettings source)
    {
        if (source == null || copyDepth >= MaximumDepth) return null;
        copyDepth++;
        try
        {
            var copy = new EnemyChildAttackSettings
            {
                triggerMode = source.triggerMode, triggerValue = source.triggerValue,
                eventType = source.eventType, repeatCount = source.repeatCount, repeatInterval = source.repeatInterval,
                boundary = source.boundary, boundaryOffset = source.boundaryOffset,
                triggerBoundaries = source.triggerBoundaries,
                triggerOnce = source.triggerOnce, timeOffset = source.timeOffset,
                destroyParent = source.destroyParent, spawnOffset = source.spawnOffset,
                offsetSpace = source.offsetSpace, originMode = source.originMode,
                fireMode = source.fireMode, aimMode = source.aimMode
            };
            copy.CopyNumericFrom(source);
            copy.stationaryFireSettings.CopyFrom(source.stationaryFireSettings);
            copy.pattern.CopyFrom(source.Pattern, true);
            return copy;
        }
        finally { copyDepth--; }
    }
}

public readonly struct EnemyProjectilePrefabSet
{
    public readonly EnemyBullet Default, Homing, BurstAtPoint;
    public EnemyProjectilePrefabSet(EnemyBullet normal, EnemyBullet homing = null, EnemyBullet burst = null)
    { Default = normal; Homing = homing; BurstAtPoint = burst; }
    public EnemyBullet Get(EnemyProjectileType type) => type switch
    {
        EnemyProjectileType.Homing => Homing != null ? Homing : Default,
        EnemyProjectileType.BurstAtPoint => BurstAtPoint != null ? BurstAtPoint : Default,
        _ => Default
    };
}

public sealed class EnemyChildAttackBudget
{
    public int Remaining = EnemyChildAttackSettings.MaximumDescendants;
}
