using UnityEngine;

public enum EnemyEmissionLayoutType : byte
{
    Point = 0,
    Radial = 1,
    Circle = 2,
    Sector = 3
}

public enum EnemySectorLaunchMode : byte { Simultaneous = 0, Sequential = 1 }
public enum EnemySectorLaunchOrder : byte { Forward, Reverse, PingPong }
public enum EnemySectorInitialDirection : byte { Forward, Reverse }

[System.Serializable]
public sealed partial class EnemyEmissionLayoutSettings
{
    [SerializeField] private EnemyEmissionLayoutType type;
    [SerializeField] [Randomizable(nameof(positionOffsetXRandom), nameof(positionOffsetYRandom), EnemyNumericScopes.Direction, EnemyNumericRandomizeOn.PerShot)]
    private Vector3 positionOffset;
    [SerializeField] [Randomizable(nameof(directionsCountRandom), 1f, EnemyNumericScopes.Volley, EnemyNumericRandomizeOn.PerShot)]
    private int directionsCount = 4;
    [SerializeField] [Randomizable(nameof(emitterCountRandom), 1f, EnemyNumericScopes.Volley, EnemyNumericRandomizeOn.PerShot)]
    private int emitterCount = 4;
    [SerializeField] [Randomizable(nameof(radiusRandom), 0f, EnemyNumericScopes.Direction, EnemyNumericRandomizeOn.PerShot)]
    private float radius = 1f;
    [SerializeField] [Randomizable(nameof(startAngleRandom), float.MinValue, EnemyNumericScopes.Volley, EnemyNumericRandomizeOn.PerShot)]
    private float startAngle;
    [SerializeField] [Randomizable(nameof(endAngleRandom), float.MinValue, EnemyNumericScopes.Volley, EnemyNumericRandomizeOn.PerShot)]
    private float endAngle = 90f;
    [SerializeField] private EnemySectorLaunchMode launchMode;
    [SerializeField] [Randomizable(nameof(startDelayRandom), 0f, EnemyNumericScopes.Volley, EnemyNumericRandomizeOn.PerShot)]
    private float startDelay;
    [SerializeField] [Randomizable(nameof(launchIntervalRandom), 0f, EnemyNumericScopes.Volley, EnemyNumericRandomizeOn.PerShot)]
    private float launchInterval = .1f;
    [SerializeField] private bool waitUntilSectorReady;
    [SerializeField] private bool reverseOrder;
    [SerializeField] private EnemySectorLaunchOrder launchOrder;
    [SerializeField] private EnemySectorInitialDirection initialDirection;
    [SerializeField, Tooltip("Start each attack from Initial Direction. Off continues after the last completed Sector; cancelled sectors do not advance the order.")]
    private bool resetOrderOnAttackStart = true;
    [System.NonSerialized] private object orderSource;
    [System.NonSerialized] private int orderPatternIndex;
    [SerializeField] [Randomizable(nameof(centerOffsetXRandom), nameof(centerOffsetYRandom), EnemyNumericScopes.Direction, EnemyNumericRandomizeOn.PerShot)]
    private Vector3 centerOffset;
    [SerializeField, Tooltip("Compatibility with legacy Single Rotation: rotate the shooting point offset around Layout Basis. This never rotates the enemy Transform.")]
    private bool rotateShootingPointOffset;

    public EnemyEmissionLayoutType Type => type;
    public int EmissionCount => type == EnemyEmissionLayoutType.Radial || type == EnemyEmissionLayoutType.Sector ? Mathf.Max(1, directionsCount)
        : type == EnemyEmissionLayoutType.Circle ? Mathf.Max(1, emitterCount) : 1;
    public Vector3 PositionOffset => positionOffset;
    public float Radius => Mathf.Max(0f, radius);
    public float StartAngle => startAngle;
    public float EndAngle => endAngle;
    public EnemySectorLaunchMode LaunchMode => launchMode;
    public bool IsSequential => type == EnemyEmissionLayoutType.Sector && launchMode == EnemySectorLaunchMode.Sequential;
    public float StartDelay => Mathf.Max(0f, startDelay);
    public float LaunchInterval => Mathf.Max(0f, launchInterval);
    public bool WaitUntilSectorReady => IsSequential && waitUntilSectorReady;
    // The old bool stays serialized: existing prefabs/overrides need no asset migration.
    public EnemySectorLaunchOrder LaunchOrder => launchOrder == EnemySectorLaunchOrder.PingPong ? launchOrder
        : reverseOrder ? EnemySectorLaunchOrder.Reverse : EnemySectorLaunchOrder.Forward;
    public EnemySectorInitialDirection InitialDirection => initialDirection;
    public bool ResetOrderOnAttackStart => resetOrderOnAttackStart;
    public bool UsesPingPong => IsSequential && LaunchOrder == EnemySectorLaunchOrder.PingPong;
    public bool ReverseOrder => LaunchOrder == EnemySectorLaunchOrder.Reverse
        || LaunchOrder == EnemySectorLaunchOrder.PingPong && initialDirection == EnemySectorInitialDirection.Reverse;
    internal (object Source, int Index) OrderKey => (orderSource ?? this, orderPatternIndex);
    internal void BindOrderSource(object source, int index) { orderSource = source; orderPatternIndex = index; }
    public float LaunchDuration => IsSequential ? StartDelay + (EmissionCount - 1) * LaunchInterval : 0f;
    // A positive sweep includes both ends. 350 -> 10 crosses zero; 0 -> 360 is a full turn.
    public float SectorSweep => Mathf.Abs(endAngle - startAngle) >= 360f ? 360f : Mathf.Repeat(endAngle - startAngle, 360f);
    public int GetLaunchIndex(int order) => GetLaunchIndex(order, ReverseOrder);
    public int GetLaunchIndex(int order, bool reversed) => reversed ? EmissionCount - 1 - order : order;
    public int GetLaunchOrder(int index) => GetLaunchIndex(index, ReverseOrder);
    public float GetLaunchDelayForOrder(int order) => IsSequential ? StartDelay + order * LaunchInterval : 0f;
    public float GetLaunchDelay(int index) => GetLaunchDelayForOrder(GetLaunchOrder(index));
    public Vector3 CenterOffset => centerOffset;
    public bool RotateShootingPointOffset => rotateShootingPointOffset;
    public void SetRotateShootingPointOffset(bool enabled) => rotateShootingPointOffset = enabled;
    public void ScaleAttackTiming(float multiplier)
    {
        multiplier = Mathf.Max(.01f, multiplier);
        startDelay /= multiplier; launchInterval /= multiplier;
        startDelayRandom?.Scale(1f / multiplier); launchIntervalRandom?.Scale(1f / multiplier);
    }

    public float GetAngle(int index)
    {
        if (type == EnemyEmissionLayoutType.Point) return 0f;
        if (type == EnemyEmissionLayoutType.Sector)
            return startAngle + SectorSweep * (EmissionCount == 1 ? .5f : (float)index / (EmissionCount - 1));
        return startAngle + index * (360f / EmissionCount);
    }

    // The same local-to-world placement is used by runtime and Scene preview.
    public Vector3 GetPosition(int index, Vector3 shootingPoint, Vector3 enemyPosition,
        Quaternion layoutBasis, Quaternion pointBasis, Quaternion enemyBasis, Vector3 scale)
    {
        if (type == EnemyEmissionLayoutType.Point)
            return shootingPoint + pointBasis * Vector3.Scale(positionOffset, scale);
        if (type == EnemyEmissionLayoutType.Radial || type == EnemyEmissionLayoutType.Sector)
            return shootingPoint;
        Vector3 radial = Quaternion.Euler(0f, 0f, GetAngle(index)) * Vector3.up * Radius;
        return enemyPosition + enemyBasis * Vector3.Scale(centerOffset, scale)
            + layoutBasis * radial;
    }

    public void Configure(EnemyEmissionLayoutType layoutType, int count = 4, float layoutRadius = 1f,
        float angle = 0f, Vector3 pointOffset = default, Vector3 circleCenterOffset = default)
    {
        type = layoutType;
        directionsCount = emitterCount = Mathf.Max(1, count);
        radius = Mathf.Max(0f, layoutRadius);
        startAngle = angle;
        positionOffset = pointOffset;
        centerOffset = circleCenterOffset;
        rotateShootingPointOffset = false;
    }

    public void CopyFrom(EnemyEmissionLayoutSettings source)
    {
        CopyNumericFrom(source);
        type = source.type;
        positionOffset = source.positionOffset;
        directionsCount = source.directionsCount;
        emitterCount = source.emitterCount;
        radius = source.radius;
        startAngle = source.startAngle;
        endAngle = source.endAngle;
        launchMode = source.launchMode;
        startDelay = source.startDelay;
        launchInterval = source.launchInterval;
        waitUntilSectorReady = source.waitUntilSectorReady;
        reverseOrder = source.reverseOrder;
        launchOrder = source.launchOrder;
        initialDirection = source.initialDirection;
        resetOrderOnAttackStart = source.resetOrderOnAttackStart;
        (orderSource, orderPatternIndex) = source.OrderKey;
        centerOffset = source.centerOffset;
        rotateShootingPointOffset = source.rotateShootingPointOffset;
    }

    public void Validate()
    {
        directionsCount = Mathf.Max(1, directionsCount);
        emitterCount = Mathf.Max(1, emitterCount);
        radius = Mathf.Max(0f, radius);
        startDelay = Mathf.Max(0f, startDelay);
        launchInterval = Mathf.Max(0f, launchInterval);
    }

    public void ConfigureSector(float start, float end, int count, EnemySectorLaunchMode mode = EnemySectorLaunchMode.Simultaneous,
        float delay = 0f, float interval = .1f, bool waitUntilReady = false, bool reverse = false)
    {
        Configure(EnemyEmissionLayoutType.Sector, count, angle: start);
        endAngle = end;
        launchMode = mode;
        startDelay = Mathf.Max(0f, delay);
        launchInterval = Mathf.Max(0f, interval);
        waitUntilSectorReady = waitUntilReady;
        reverseOrder = reverse;
        launchOrder = reverse ? EnemySectorLaunchOrder.Reverse : EnemySectorLaunchOrder.Forward;
    }

    public void ConfigureLaunchOrder(EnemySectorLaunchOrder order, EnemySectorInitialDirection initial = EnemySectorInitialDirection.Forward, bool resetOnAttackStart = true)
    {
        launchOrder = order; reverseOrder = order == EnemySectorLaunchOrder.Reverse;
        initialDirection = initial; resetOrderOnAttackStart = resetOnAttackStart;
    }
}
