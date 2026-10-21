using UnityEngine;

public enum EnemyEmissionOriginSource { Enemy, Player, World }
public enum EnemyEmissionOriginReferenceMode { Absolute, Boundary }
public enum EnemyEmissionOriginXBoundary { Left, Right }
public enum EnemyEmissionOriginYBoundary { Bottom, Top }
public enum EnemyEmissionOriginCaptureMode
{
    [InspectorName("On Attack Start")] OnAttackStart,
    [InspectorName("On Burst Start")] OnBurstStart,
    [InspectorName("On Shot")] OnShot
}

[System.Serializable]
public sealed class EnemyEmissionOriginAxis
{
    [SerializeField] private EnemyEmissionOriginSource source;
    [SerializeField, Randomizable(nameof(offsetRandom))] private float offset;
    [SerializeField, HideInInspector] private RandomFloatRange offsetRandom = new();
    [SerializeField] private EnemyEmissionOriginCaptureMode randomizeOn;
    public RandomFloatRange OffsetRandom => offsetRandom ??= new();
    public EnemyEmissionOriginCaptureMode RandomizeOn => randomizeOn;
    [SerializeField] private EnemyEmissionOriginReferenceMode referenceMode;
    // 0 is Left/Bottom; 1 is Right/Top. Inspector exposes an axis-specific enum.
    [SerializeField] private int boundary;
    [SerializeField] private EnemyEmissionOriginCaptureMode captureMode;
    public EnemyEmissionOriginSource Source => source;
    public float Offset => offset;
    public EnemyEmissionOriginReferenceMode ReferenceMode => referenceMode;
    public bool UsesUpperBoundary => boundary == 1;
    public EnemyEmissionOriginCaptureMode CaptureMode => captureMode;
    public void SetCaptureMode(EnemyEmissionOriginCaptureMode mode) => captureMode = mode;
    public bool RequiresBounds => source == EnemyEmissionOriginSource.World
        && referenceMode == EnemyEmissionOriginReferenceMode.Boundary;
    public bool IsDefault => source == EnemyEmissionOriginSource.Enemy && offset == 0f && !OffsetRandom.enabled;

    public float Resolve(float enemy, float player, float minimum, float maximum)
        => Resolve(enemy, player, minimum, maximum, offset);
    public float Resolve(float enemy, float player, float minimum, float maximum, float sampledOffset)
    {
        if (source == EnemyEmissionOriginSource.Enemy) return enemy + sampledOffset;
        if (source == EnemyEmissionOriginSource.Player) return player + sampledOffset;
        if (referenceMode == EnemyEmissionOriginReferenceMode.Absolute) return sampledOffset;
        return UsesUpperBoundary ? maximum - sampledOffset : minimum + sampledOffset;
    }

    public void Configure(EnemyEmissionOriginSource value, float axisOffset = 0f,
        EnemyEmissionOriginReferenceMode mode = EnemyEmissionOriginReferenceMode.Absolute, bool upperBoundary = false)
    {
        source = value; offset = axisOffset; referenceMode = mode; boundary = upperBoundary ? 1 : 0;
    }

    public void CopyFrom(EnemyEmissionOriginAxis other)
    {
        source = other.source; offset = other.offset; referenceMode = other.referenceMode; boundary = other.boundary;
        captureMode = other.captureMode;
        OffsetRandom.CopyFrom(other.OffsetRandom); randomizeOn = other.randomizeOn;
    }
}

[System.Serializable]
public sealed class EnemyEmissionOriginSettings
{
    [SerializeField] private EnemyEmissionOriginAxis x = new EnemyEmissionOriginAxis();
    [SerializeField] private EnemyEmissionOriginAxis y = new EnemyEmissionOriginAxis();
    public EnemyEmissionOriginAxis X => x ??= new EnemyEmissionOriginAxis();
    public EnemyEmissionOriginAxis Y => y ??= new EnemyEmissionOriginAxis();
    public bool UsesPlayer => X.Source == EnemyEmissionOriginSource.Player || Y.Source == EnemyEmissionOriginSource.Player;
    public bool RequiresBounds => X.RequiresBounds || Y.RequiresBounds;
    public bool IsDefault => X.IsDefault && Y.IsDefault;

    public Vector3 Resolve(Vector3 enemy, Vector3 capturedPlayer, Rect bounds)
        => new Vector3(X.Resolve(enemy.x, capturedPlayer.x, bounds.xMin, bounds.xMax),
            Y.Resolve(enemy.y, capturedPlayer.y, bounds.yMin, bounds.yMax), enemy.z);

    public Vector3 Resolve(Vector3 enemy, Vector3 capturedPlayer, Rect bounds, Vector2 offsets)
        => new(X.Resolve(enemy.x, capturedPlayer.x, bounds.xMin, bounds.xMax, X.OffsetRandom.enabled ? offsets.x : X.Offset),
            Y.Resolve(enemy.y, capturedPlayer.y, bounds.yMin, bounds.yMax, Y.OffsetRandom.enabled ? offsets.y : Y.Offset), enemy.z);
    public void SetCaptureMode(EnemyEmissionOriginCaptureMode mode)
    { X.SetCaptureMode(mode); Y.SetCaptureMode(mode); }
    public void CopyFrom(EnemyEmissionOriginSettings other)
    {
        X.CopyFrom(other.X); Y.CopyFrom(other.Y);
    }

    // Camera viewport already excludes HUD, safe-area margins and letterboxing.
    // Rays also handle perspective cameras and a viewport that is not full-screen.
    public static bool TryGetGameplayBounds(Camera camera, float worldZ, out Rect bounds)
    {
        bounds = default;
        if (camera == null || camera.pixelWidth <= 0 || camera.pixelHeight <= 0) return false;
        Plane plane = new Plane(Vector3.forward, new Vector3(0f, 0f, worldZ));
        float minX = float.PositiveInfinity, minY = float.PositiveInfinity;
        float maxX = float.NegativeInfinity, maxY = float.NegativeInfinity;
        for (int i = 0; i < 4; i++)
        {
            Ray ray = camera.ViewportPointToRay(new Vector3(i & 1, i >> 1, 0f));
            if (!plane.Raycast(ray, out float distance)) return false;
            Vector3 point = ray.GetPoint(distance);
            minX = Mathf.Min(minX, point.x); maxX = Mathf.Max(maxX, point.x);
            minY = Mathf.Min(minY, point.y); maxY = Mathf.Max(maxY, point.y);
        }
        bounds = Rect.MinMaxRect(minX, minY, maxX, maxY);
        return true;
    }
}

// Per executor and per pattern, never stored in shared presets or authored settings.
public struct EnemyEmissionOriginCaptureState
{
    private EnemyAttackNumericState numeric;
    public Vector2? RandomOffsets => numeric?.Offsets;
    public void BindNumeric(EnemyEmissionOriginSettings settings, EnemyAttackNumericState state)
    { if (ReferenceEquals(numeric, state)) return; numeric = state; numeric?.BeginOrigin(settings); }
    private bool hasPlayer;
    private Vector3 player;
    public void Begin(EnemyEmissionOriginSettings settings, Vector3 position, EnemyAttackNumericState numeric = null)
    {
        this.numeric = numeric; numeric?.BeginOrigin(settings);
        hasPlayer = settings.UsesPlayer;
        player = position;
    }
    public Vector3 Prepare(EnemyEmissionOriginSettings settings, Vector3 position, bool burstStart, bool? numericBurstStart = null)
    {
        numeric?.PrepareOrigin(settings, numericBurstStart ?? burstStart);
        if (!hasPlayer) player = position;
        if (Refreshes(settings.X, burstStart)) player.x = position.x;
        if (Refreshes(settings.Y, burstStart)) player.y = position.y;
        hasPlayer = true;
        return player;
    }
    private static bool Refreshes(EnemyEmissionOriginAxis axis, bool burstStart)
        => axis.Source == EnemyEmissionOriginSource.Player
            && (axis.CaptureMode == EnemyEmissionOriginCaptureMode.OnShot
                || axis.CaptureMode == EnemyEmissionOriginCaptureMode.OnBurstStart && burstStart);
}
// Entrance rules can overlap on one enemy. Each rule owns its captures, while
// the executor only receives the resolved player coordinates for the next volley.
public sealed class EnemyEmissionOriginAttackCapture
{
    private readonly Vector3 attackStartPlayer;
    private EnemyEmissionOriginCaptureState[] captures;
    private EnemyAttackNumericState[] numeric;
    public EnemyAttackNumericState Numeric(int index) => numeric != null ? numeric[index] : null;
    public Vector2? RandomOffsets(int index) => captures != null ? captures[index].RandomOffsets : null;
    public EnemyEmissionOriginAttackCapture(Vector3 player) => attackStartPlayer = player;
    public Vector3 Prepare(EnemyBurstAttackSettings settings, int index, Vector3 currentPlayer, bool burstStart,
        EnemyAttackNumericRegistry registry = null)
    {
        if (captures == null || captures.Length != settings.PatternCount)
        {
            captures = new EnemyEmissionOriginCaptureState[settings.PatternCount];
            numeric = new EnemyAttackNumericState[settings.PatternCount];
            for (int i = 0; i < captures.Length; i++)
            {
                numeric[i] = registry?.CreateDetached(settings.GetPattern(i));
                numeric[i]?.BeginSpeed();
                captures[i].Begin(settings.GetPattern(i).Shooting.EmissionOrigin, attackStartPlayer, numeric[i]);
            }
        }
        return captures[index].Prepare(settings.GetPattern(index).Shooting.EmissionOrigin, currentPlayer, burstStart,
            burstStart && settings.GetPattern(index).RepeatBurst);
    }
}
