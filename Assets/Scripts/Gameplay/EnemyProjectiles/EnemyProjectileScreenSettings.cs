using System;
using UnityEngine;

public enum EnemyProjectileBoundaryOffsetMode { Automatic, Custom }

[Serializable]
public struct EnemyProjectileScreenSettings
{
    // Inverse storage makes missing fields in old Unity/JSON data enabled by default.
    [SerializeField] private bool disabled;
    [SerializeField] private EnemyProjectileBoundaryOffsetMode offsetMode;
    [SerializeField] private float boundaryOffset;
    [SerializeField] private bool bounceOnScreenBounds;
    // XOR with the default mask lets missing fields in old structs mean Left + Right.
    [SerializeField] private int bounceEdges;
    [SerializeField] private bool overrideBaseBounceTime;
    // Store count minus one so absent fields in old serialized structs mean one.
    [SerializeField] private int extraBounceCount;
    public const EnemyChildAttackBoundaries DefaultBounceEdges = EnemyChildAttackBoundaries.Left | EnemyChildAttackBoundaries.Right;
    public bool BounceOnScreenBounds => bounceOnScreenBounds;
    public bool OverrideBaseBounceTime => overrideBaseBounceTime;
    public int MaximumBounces => extraBounceCount >= int.MaxValue - 1 ? int.MaxValue : Mathf.Max(0, extraBounceCount) + 1;
    public bool CanBounce(int completed) => !overrideBaseBounceTime || completed < MaximumBounces;
    public EnemyChildAttackBoundaries BounceEdges => (EnemyChildAttackBoundaries)(bounceEdges ^ (int)DefaultBounceEdges) & EnemyChildAttackBoundaries.All;
    public bool DestroyOutsideScreen => !disabled;
    public EnemyProjectileBoundaryOffsetMode OffsetMode => offsetMode;
    public float BoundaryOffset => float.IsFinite(boundaryOffset) ? Mathf.Max(0f, boundaryOffset) : 0f;
    public EnemyProjectileScreenSettings(bool enabled, EnemyProjectileBoundaryOffsetMode mode = EnemyProjectileBoundaryOffsetMode.Automatic, float offset = 0f,
        bool bounce = false, EnemyChildAttackBoundaries edges = DefaultBounceEdges,
        bool overrideBounceTime = false, int maximumBounces = 1)
    {
        disabled = !enabled; offsetMode = mode; boundaryOffset = offset; bounceOnScreenBounds = bounce;
        bounceEdges = (int)(edges ^ DefaultBounceEdges); overrideBaseBounceTime = overrideBounceTime;
        extraBounceCount = Mathf.Max(1, maximumBounces) - 1;
    }
}
