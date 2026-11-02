using UnityEngine;
using UnityEngine.Serialization;
using Zenject;
using System.Collections.Generic;

[DefaultExecutionOrder(-100)]
[DisallowMultipleComponent]
[RequireComponent(typeof(Enemy))]
public sealed partial class SimpleEnemyAttackExecutor : MonoBehaviour,
    IEnemyBurstAttackExecutor, IEnemyBurstAttackSettingsOverrideReceiver,
    IEnemyProjectileTypeOverrideReceiver, IEnemyShootingSettingsReceiver, IFormationAttackActivation,
    IEnemyAttackSettingsOverrideState, IEnemyAttackPatternSettingsReceiver, IEnemyAttackDirectionProvider,
    IEnemyAttackAimReceiver, IEnemyAttackSequenceExecutor, IEnemyLockedAimAttackExecutor,
    IEnemyEmissionAimAttackExecutor, IEnemyPendingEmissionExecutor, IEnemyStationaryAttackExecutor, IEnemyEmissionOriginReceiver,
    IEnemyAttackCompletionReceiver, IEnemyAttackActivity, IEnemySectorLaunchOrderSource
{
    private EnemyAttackCompletionTracker completionTracker;
    public void SetAttackCompletionTracker(EnemyAttackCompletionTracker tracker) => completionTracker = tracker;
    private const int CurrentAttackModeVersion = 2;
    [Inject] private EnemyProjectileEcsSpawner projectileSpawner;
    [InjectOptional] private PlayerController originPlayer;

    [SerializeField, HideInInspector] private EnemyProjectileType projectileType;
    [SerializeField, FormerlySerializedAs("projectilePrefab")] private EnemyBullet defaultProjectilePrefab;
    [SerializeField] private EnemyBullet homingProjectilePrefab;
    [SerializeField] private EnemyBullet burstAtPointProjectilePrefab;
    [SerializeField] private Transform projectileSpawnPoint;
    [SerializeField] private Vector3 projectileSpawnOffset = new Vector3(0f, 0.25f, 0f);
    [SerializeField, Tooltip("Fixed basis for Emission Layout. Empty uses the enemy's initial orientation. Layout Rotation never rotates this Transform.")]
    private Transform rotationTarget;
    [SerializeField, Tooltip("Optional visible child to turn in Follow Pattern. Empty turns the enemy itself. Firing directions and spawn positions remain independent.")]
    private Transform facingTarget;
    [SerializeField, Tooltip("Fire the local Attack Pattern without a wave controller. A wave temporarily takes over this schedule.")]
    private bool autonomousAttack;

    // Kept for migration of existing prefabs; the shared profile is authoritative.
    [SerializeField, HideInInspector] private int shootingMode;
    [SerializeField, HideInInspector] private int fanProjectileCount = 3;
    [SerializeField, HideInInspector] private float fanMinAngle = -45f;
    [SerializeField, HideInInspector] private float fanMaxAngle = 45f;
    [SerializeField, HideInInspector] private int shotGunProjectileCount = 3;
    [SerializeField, HideInInspector] private Vector2 shotGunDirection = Vector2.down;
    [SerializeField, HideInInspector] private float shotGunInitialSpeed = 5f;
    [SerializeField, HideInInspector, FormerlySerializedAs("shotGunSpeedBehavior")]
    private EnemyProjectileSpeedBehavior projectileSpeedBehavior;
    [SerializeField, HideInInspector, FormerlySerializedAs("shotGunSpeedChangeY")]
    private float projectileSpeedChangeY;
    [SerializeField, HideInInspector, FormerlySerializedAs("shotGunSpeedByDistance")]
    private AnimationCurve projectileSpeedByDistance = AnimationCurve.Linear(0f, 1f, 10f, 1f);
    [SerializeField, InspectorName("Attack Pattern")]
    private EnemyBurstAttackSettings burstAttackSettings = new EnemyBurstAttackSettings();
    [SerializeField, HideInInspector] private int attackModeVersion;
    [SerializeField, HideInInspector] private int emissionBindingVersion;

    private Enemy enemy;
    private CircleShip circleShip;
    private EnemyShootingSettings waveShootingSettings;
    private EnemyBurstAttackSettings waveAttackSettings;
    private EnemyAttackPatternSchedule[] patternSchedules;
    private bool[] autonomousPatternStarted;
    public bool HasActiveAttack
    {
        get
        {
            if (pendingSectors.Count > 0) return true;
            if (!autonomousAttack || isWaveAttackControlled) return false;
            if (!burstAttackSettings.HasMultiplePatterns) return remainingAttackShots > 0;
            if (autonomousPatternStarted != null)
                for (int i = 0; i < autonomousPatternStarted.Length; i++) if (autonomousPatternStarted[i]) return true;
            return false;
        }
    }
    private float patternRotationTime;
    private float facingRotationTime;
    private bool patternRotationRunning;
    private int layoutAttackCount, layoutProgramDepth;
    private bool layoutRotationHeld;
    private bool layoutProgramHasAttack;
    private EnemyLayoutRotationOnAttackFinished layoutFinishMode;
    private readonly List<EnemyBurstAttackSettings> layoutPendingEnds = new();

    public void BeginLayoutRotationProgram()
    { if (layoutProgramDepth++ == 0) layoutProgramHasAttack = false; }
    public void EndLayoutRotationProgram()
    {
        if (layoutProgramDepth > 0) layoutProgramDepth--;
        FinishLayoutRotationIfIdle();
    }
    private void FinishLayoutRotationIfIdle()
    {
        if (layoutAttackCount > 0 || layoutProgramDepth > 0 || layoutFinishMode == EnemyLayoutRotationOnAttackFinished.ContinueRotation) return;
        layoutRotationHeld = false;
        if (layoutFinishMode == EnemyLayoutRotationOnAttackFinished.ResetRotation) RestartRotation();
        PauseRotation(); layoutRotationHeld = true;
    }
    private void TickLayoutAttackEnds()
    {
        for (int i = layoutPendingEnds.Count - 1; i >= 0; i--)
        {
            if (HasPendingEmissions(layoutPendingEnds[i])) continue;
            layoutPendingEnds.RemoveAt(i); layoutAttackCount = Mathf.Max(0, layoutAttackCount - 1);
        }
        if (!layoutRotationHeld) FinishLayoutRotationIfIdle();
    }
    private Quaternion firingEnemyLocalRotation;
    private Quaternion firingPivotRelativeRotation;
    private Vector3 firingDefaultSpawnOffset;
    private Vector3 firingRotationSpawnOffset;
    private Vector3 firingPivotPositionOffset;
    private Quaternion firingSpawnPointRelativeRotation;
    private Vector3 firingSpawnPointRelativeScale = Vector3.one;
    private bool ownedSpawnPoint;
    private Transform boundFacingTarget;
    private Quaternion initialFacingLocalRotation;
    private bool facingPoseApplied;
    private PatternAim[] patternAims;
    private IEnemyAttackAimTarget waveAimTarget;
    private readonly Dictionary<EnemyBurstAttackSettings,
        EnemyProjectileInitialSpeedState> initialSpeedSequences = new();
    private readonly HashSet<EnemyBurstAttackSettings> activeInitialSpeedSequences = new();
    private readonly Dictionary<EnemyShootingSettings,
        EnemyProjectileRandomSpreadSequence> randomSpreadSequences = new();
    private readonly List<EnemyProjectileSpawnRequest> projectileSpawnRequests = new(32);
    private readonly Dictionary<EnemyBurstAttackSettings, Vector3[]> lockedEmissionDirections = new();
    private readonly Dictionary<EnemyBurstAttackSettings, EnemyEmissionOriginCaptureState> originCaptures = new();
    private readonly Dictionary<EnemyBurstAttackSettings, Vector3> shotOrigins = new();
    private readonly HashSet<EnemyBurstAttackSettings> preparedOriginShots = new();
    private bool warnedMissingOriginContext;
    private System.Func<EnemyShootingSettings, float> childSpreadSampler;
    private readonly List<PendingSectorVolley> pendingSectors = new(4);
    private readonly Stack<PendingSectorVolley> sectorPool = new(4);
    private readonly Dictionary<EnemyBurstAttackSettings, float> sectorCompletionTimes = new();
    private readonly EnemySectorLaunchOrderRegistry sectorOrders = new();

    private sealed class PendingSectorVolley
    {
        public EnemyRandomSpeedSample NumericSpeed;
        public EnemySectorLaunchTicket LaunchOrder;
        public EnemyBurstAttackSettings Settings;
        public EnemyShootingSettings Shooting;
        public PatternAim Aim;
        public float Speed, Spread, StartTime;
        public int NextOrder;
        public bool WaitingForReady;
        public bool Failed;
        public Vector3[] LockedDirections;
        public Vector3? Origin;
    }

    private struct PatternAim
    {
        public bool HasValue;
        public bool UsesTargetPosition;
        public bool UsesAimedDirection;
        public bool UsesStationaryDirection;
        public Vector3 Value;
        public Vector3[] LockedEmissionDirections;

        public Vector3 GetRequestedDirection(Vector3 position)
        {
            if (!HasValue)
                return Vector3.zero;
            return UsesTargetPosition ? Value - position : Value;
        }
    }

    private sealed class EnemyProjectileRandomSpreadSequence
    {
        private const int VariantCount = 5;

        private readonly float[] angles = new float[VariantCount];
        private int nextIndex;

        public EnemyProjectileRandomSpreadSequence(EnemyShootingSettings settings, System.Random generator)
        {
            for (int index = 0; index < angles.Length; index++)
                angles[index] = settings.GenerateRandomSpreadAngle(generator);
        }

        public float GetNextAngle()
        {
            float angle = angles[nextIndex];
            nextIndex = (nextIndex + 1) % angles.Length;
            return angle;
        }
    }
    private bool isWaveAttackControlled;
    private bool isFormationAttackReady = true;
    private int remainingAttackShots;
    private int remainingBurstShots;
    private float nextAttackTime;
    private float nextShotTime;

    private EnemyBurstAttackSettings ActivePatterns => waveAttackSettings ?? burstAttackSettings;
    private EnemyShootingSettings ActiveShooting => waveShootingSettings
        ?? (ActivePatterns.PatternCount > 0 ? ActivePatterns.GetPattern(0).Shooting : burstAttackSettings.Shooting);
    private Transform DirectionTransform => rotationTarget != null ? rotationTarget : transform;
    private Quaternion FiringEnemyRotation => transform.parent != null
        ? transform.parent.rotation * firingEnemyLocalRotation : firingEnemyLocalRotation;
    private Vector3 FiringEnemyScale => transform.parent != null
        ? (transform.parent.localToWorldMatrix * Matrix4x4.TRS(Vector3.zero, firingEnemyLocalRotation, transform.localScale)).lossyScale
        : transform.localScale;
    public Vector3 AttackForward => FiringEnemyRotation * Vector3.up;
    public bool CanPerformWaveAttack
    {
        get
        {
            if (!isActiveAndEnabled || enemy == null || enemy.isDead || projectileSpawner == null || ActivePatterns.PatternCount == 0)
                return false;
            if (!ActivePatterns.HasMultiplePatterns)
                return ActiveShooting.ProjectileType == EnemyProjectileType.Laser || ActiveShooting.ResolveProjectilePrefab(GetSelectedProjectilePrefab(ActiveShooting.ProjectileType)) != null;
            for (int i = 0; i < ActivePatterns.PatternCount; i++)
                if (ActivePatterns.GetPattern(i).Shooting.ProjectileType != EnemyProjectileType.Laser
                    && ActivePatterns.GetPattern(i).Shooting.ResolveProjectilePrefab(GetSelectedProjectilePrefab(ActivePatterns.GetPattern(i).Shooting.ProjectileType)) == null)
                    return false;
            return true;
        }
    }
    public EnemyBurstAttackSettings BurstAttackSettings => burstAttackSettings;
    public bool HasAttackSettingsOverride { get; private set; }

    public bool TryGetProjectileBaseSpeed(
        EnemyBurstAttackSettings settings,
        out float baseSpeed)
    {
        baseSpeed = 0f;
        if (settings == null || settings.PatternCount == 0)
            return false;

        var shooting = settings.GetPattern(0).Shooting;
        var prefab = shooting.ResolveProjectilePrefab(GetSelectedProjectilePrefab(shooting.ProjectileType));
        var authoring = prefab != null ? prefab.GetComponent<EnemyProjectileAuthoring>() : null;
        if (authoring == null) return false;
        baseSpeed = authoring.BaseSpeed;
        return true;
    }

    public bool TryGetProjectileBaseSpeed(
        EnemyProjectileType projectileType,
        out float baseSpeed)
    {
        baseSpeed = 0f;
        EnemyBullet prefab = GetSelectedProjectilePrefab(projectileType);
        EnemyProjectileAuthoring authoring = prefab != null
            ? prefab.GetComponent<EnemyProjectileAuthoring>()
            : null;
        if (authoring == null)
            return false;

        baseSpeed = authoring.BaseSpeed;
        return true;
    }

    public bool TryGetProjectileBaseLifetime(
        EnemyProjectileType projectileType,
        out float baseLifetime)
    {
        baseLifetime = 0f;
        EnemyBullet prefab = GetSelectedProjectilePrefab(projectileType);
        EnemyProjectileAuthoring authoring = prefab != null
            ? prefab.GetComponent<EnemyProjectileAuthoring>()
            : null;
        if (authoring == null)
            return false;

        baseLifetime = authoring.BaseLifetime;
        return true;
    }

    public bool IsRotationComplete
    {
        get
        {
            for (int i = 0; i < ActivePatterns.PatternCount; i++)
            {
                EnemyShootingSettings shooting = ActivePatterns.GetPattern(i).Shooting;
                if (shooting.Rotates && shooting.Rotation.HasMotion
                    && (shooting.Rotation.RotationMode != FourWayEnemyRotationMode.ByAngle
                        || patternRotationTime < shooting.Rotation.Duration))
                    return false;
            }
            return true;
        }
    }

    private void Reset() => attackModeVersion = CurrentAttackModeVersion;

    private void Awake()
    {
        MigrateLegacyAttackMode();
        enemy = GetComponent<Enemy>();
        circleShip = GetComponent<CircleShip>();
        GetComponent<FourWayEnemy>()?.EnsureExecutorConfigured();
        PrepareRandomSpreadSequences(burstAttackSettings);
        RefreshRotation(true);
        ResetAttackSchedule();
    }

    private void OnEnable()
    {
        if (enemy == null)
            return;
        RefreshRotation(ActiveShooting.Rotation.ResetRotationOnEnable);
        ResetAttackSchedule();
        UpdateLegacyAttackControl();
    }

    private void OnDisable()
    {
        layoutAttackCount = layoutProgramDepth = 0; layoutPendingEnds.Clear(); layoutRotationHeld = false;
        layoutFinishMode = EnemyLayoutRotationOnAttackFinished.ContinueRotation;
        CancelPendingEmissions();
        sectorOrders.Clear();
        patternRotationRunning = false;
        RestoreFacingPose();
        waveAimTarget = null;
        waveShootingSettings = null;
        waveAttackSettings = null;
        isWaveAttackControlled = false;
        ClearInitialSpeedSequences(); NumericRegistry.Clear();
        if (circleShip != null)
            circleShip.SetWaveAttackControl(false);
    }

    private void OnValidate()
    {
        MigrateLegacyAttackMode();
        if (Application.isPlaying && enemy != null)
        {
            CancelPendingEmissions();
            RefreshRotation(false);
            ResetAttackSchedule();
            UpdateLegacyAttackControl();
        }
    }

    private void Update()
    {
        if (enemy == null || enemy.isDead)
        {
            CancelPendingEmissions();
            return;
        }
        float rotationDeltaTime = PatternRotationDeltaTime;
        TickLayoutAttackEnds();
        if (patternRotationRunning)
            patternRotationTime += rotationDeltaTime;
        facingRotationTime += rotationDeltaTime;
        if (enemy.AttackPermissions != null && enemy.AttackPermissions.Interrupted) return;
        TickPendingSectors(AttackTime);
        if (autonomousAttack && !isWaveAttackControlled && isFormationAttackReady && CanPerformWaveAttack)
            TickAutonomousAttack(AttackTime);
    }

    private float AttackTime => enemy != null ? enemy.AttackTime : Time.time;

    // Only authored attack/facing rotations use this clock; physical movement keeps world time.
    private float PatternRotationDeltaTime => Time.deltaTime
        * (enemy != null ? enemy.AttackPermissions != null && enemy.AttackPermissions.Interrupted
            ? 0f : enemy.FireRateMultiplier * enemy.AttackSpeedMultiplier : 1f);

    private void LateUpdate()
    {
        if (enemy != null && !enemy.isDead)
            TickFacing(PatternRotationDeltaTime);
    }

    private void TickFacing(float deltaTime)
    {
        EnemyBurstAttackSettings patterns = ActivePatterns;
        if (patterns.FacingMode == EnemyFacingMode.Rotation && boundFacingTarget != null)
        {
            boundFacingTarget.localRotation = initialFacingLocalRotation
                * Quaternion.Euler(0f, 0f, patterns.FacingRotation.EvaluateAngle(facingRotationTime));
            facingPoseApplied = true;
            return;
        }
        if (patterns.FacingMode != EnemyFacingMode.FollowPattern || boundFacingTarget == null)
            return;
        int index = patterns.FacingPatternIndex;
        if (index < 0 || patternAims == null || index >= patternAims.Length)
            return;
        EnemyBurstAttackSettings pattern = patterns.GetPattern(index);
        EnemyShootingSettings shooting = pattern.Shooting;
        Vector3 position = GetProjectileSpawnPosition(pattern);
        PatternAim aim = patternAims[index];
        if (aim.UsesStationaryDirection) position = GetEmissionPosition(pattern, 0);
        Vector3 requested = aim.GetRequestedDirection(position);
        bool usesAimedShotGunDirection = aim.UsesTargetPosition || aim.UsesAimedDirection;
        if (!aim.UsesStationaryDirection && waveAimTarget != null && (!(waveAimTarget is Object source) || source != null))
        {
            requested = waveAimTarget.AttackTargetPosition - position;
            usesAimedShotGunDirection = true;
        }
        Vector3 direction = shooting.GetFacingDirection(shooting.GetEmissionDirection(0,
            GetVirtualDirectionBasis(shooting), patternRotationTime, requested, usesAimedShotGunDirection, stationary: aim.UsesStationaryDirection));
        boundFacingTarget.rotation = EnemyAttackFacing.Step(boundFacingTarget.rotation, direction,
            patterns.FacingSpeed, patterns.FacingAngleOffset, deltaTime);
        facingPoseApplied = true;
    }

    private void StorePatternAim(EnemyBurstAttackSettings pattern, PatternAim aim)
    {
        if (patternAims == null)
            return;
        for (int i = 0; i < ActivePatterns.PatternCount; i++)
        {
            if (!ReferenceEquals(ActivePatterns.GetPattern(i), pattern))
                continue;
            patternAims[i] = aim;
            return;
        }
    }

    private Quaternion GetVirtualDirectionBasis(EnemyShootingSettings shooting)
    {
        Quaternion basis = FiringEnemyRotation;
        if (shooting.EmissionLayout.Type != EnemyEmissionLayoutType.Point)
            basis *= firingPivotRelativeRotation;
        return basis;
    }

    public void ApplyBurstAttackSettingsOverride(EnemyBurstAttackSettings settings)
    {
        if (settings == null)
            return;
        CancelPendingEmissions();
        burstAttackSettings.CopyFrom(settings);
        PrepareRandomSpreadSequences(burstAttackSettings);
        HasAttackSettingsOverride = true;
        waveShootingSettings = null;
        waveAttackSettings = null;
        RefreshRotation(true);
        ResetAttackSchedule();
    }

    public void ApplyProjectileTypeOverride(EnemyProjectileType type)
    {
        burstAttackSettings.SetProjectileTypeForAllPatterns(type);
    }

    public void SetWaveAttackSettings(EnemyBurstAttackSettings settings)
    {
        if (ReferenceEquals(waveAttackSettings, settings))
            return;
        CancelPendingEmissions();
        waveAttackSettings = settings;
        PrepareRandomSpreadSequences(waveAttackSettings);
        waveShootingSettings = settings != null && !settings.HasMultiplePatterns ? settings.Shooting : null;
        RefreshRotation(true);
    }

    public void SetWaveShootingSettings(EnemyShootingSettings settings)
    {
        if (ReferenceEquals(waveShootingSettings, settings) && waveAttackSettings == null)
            return;
        CancelPendingEmissions();
        waveShootingSettings = settings;
        PrepareRandomSpreadSequence(waveShootingSettings);
        waveAttackSettings = null;
        RefreshRotation(true);
    }

    public void SetWaveAimTarget(IEnemyAttackAimTarget target) => waveAimTarget = target;

    public void SetWaveAttackControl(bool isControlled)
    {
        if (isWaveAttackControlled == isControlled)
            return;
        isWaveAttackControlled = isControlled;
        if (!isControlled)
        {
            CancelPendingEmissions();
            waveAimTarget = null;
            SetWaveAttackSettings(null);
            SetWaveShootingSettings(null);
            ResetAttackSchedule();
        }
        UpdateLegacyAttackControl();
    }

    public void SetFormationAttackReady(bool isReady)
    {
        if (isFormationAttackReady == isReady)
            return;
        isFormationAttackReady = isReady;
        ResetAttackSchedule();
    }

    public void BeginAttackSequence(EnemyBurstAttackSettings settings)
    {
        if (settings == null)
            return;

        if (!resettingNumericSchedule)
        {
            var mode = EnemyRotationSettings.AttackFinishedMode(settings);
            if (layoutAttackCount == 0 && (layoutProgramDepth == 0 || !layoutProgramHasAttack)) layoutFinishMode = mode;
            else if (mode > layoutFinishMode) layoutFinishMode = mode;
            layoutAttackCount++; layoutProgramHasAttack = true;
            if (layoutRotationHeld || mode != EnemyLayoutRotationOnAttackFinished.ContinueRotation) ResumeRotation();
            layoutRotationHeld = false;
        }

        PrepareRandomSpreadSequences(settings);
        if (!resettingNumericSchedule) BeginSectorLaunchOrder(settings);
        for (int index = 0; index < settings.PatternCount; index++)
        {
            EnemyBurstAttackSettings pattern = settings.GetPattern(index);
            initialSpeedSequences[pattern] = default;
            if (!isWaveAttackControlled) lockedEmissionDirections.Remove(pattern);
            if (!resettingNumericSchedule) NumericRegistry.Get(pattern)?.BeginSpeed();
            activeInitialSpeedSequences.Add(pattern);
        }
    }

    public void BeginSectorLaunchOrder(EnemyBurstAttackSettings settings)
    {
        for (int i = 0; i < settings.PatternCount; i++) sectorOrders.BeginAttack(settings.GetPattern(i).Shooting.EmissionLayout);
    }

    public void BeginEmissionOriginAttack(EnemyBurstAttackSettings settings)
    {
        for (int i = 0; i < settings.PatternCount; i++)
        {
            var pattern = settings.GetPattern(i);
            EnemyEmissionOriginCaptureState capture = default;
            capture.Begin(pattern.Shooting.EmissionOrigin, GetOriginPlayerPosition(), resettingNumericSchedule ? null : NumericRegistry.Get(pattern));
            originCaptures[pattern] = capture;
            shotOrigins.Remove(pattern);
            preparedOriginShots.Remove(pattern);
        }
    }

    private Vector3 GetOriginPlayerPosition() => waveAimTarget != null
        ? waveAimTarget.AttackTargetPosition : originPlayer != null ? originPlayer.transform.position : Vector3.zero;

    private Camera GetOriginCamera()
    {
        if (waveAimTarget is IEnemyEmissionOriginContext context) return context.EmissionOriginCamera;
        return originPlayer != null ? originPlayer.GameplayCamera : Camera.main;
    }

    public bool PrepareEmissionOriginShot(EnemyBurstAttackSettings settings, bool burstStart, Vector3? capturedPlayer = null, Vector2? capturedOffsets = null, EnemyAttackNumericState numericOverride = null)
    {
        for (int i = 0; i < settings.PatternCount; i++)
        {
            var pattern = settings.GetPattern(i);
            var origin = pattern.Shooting.EmissionOrigin;
            bool hasPlayer = waveAimTarget is IEnemyEmissionOriginContext context
                ? context.HasEmissionOriginPlayer : waveAimTarget != null || originPlayer != null;
            Camera camera = origin.RequiresBounds ? GetOriginCamera() : null;
            Rect bounds = default;
            if (origin.UsesPlayer && !hasPlayer
                || origin.RequiresBounds && !EnemyEmissionOriginSettings.TryGetGameplayBounds(camera, transform.position.z, out bounds))
            {
                if (!warnedMissingOriginContext)
                {
                    warnedMissingOriginContext = true;
                    Debug.LogWarning("Emission Origin requires a player reference or a valid gameplay camera for Boundary coordinates.", this);
                }
                return false;
            }
            originCaptures.TryGetValue(pattern, out var capture);
            if (!originCaptures.ContainsKey(pattern)) capture.Begin(origin, GetOriginPlayerPosition(), NumericRegistry.Get(pattern));
            capture.BindNumeric(origin, numericOverride ?? NumericRegistry.Get(pattern));
            Vector3 player = capture.Prepare(origin, GetOriginPlayerPosition(), burstStart, burstStart && pattern.RepeatBurst);
            player = capturedPlayer ?? player;
            initialSpeedSequences.TryGetValue(pattern, out var speedState);
            numericShots[pattern] = (numericOverride ?? NumericRegistry.Get(pattern))?.PrepareSpeed(pattern.Shooting, burstStart && pattern.RepeatBurst,
                pattern.Shooting.InitialSpeedMode == EnemyProjectileInitialSpeedMode.Consistent ? speedState.ConsistentDelta : 0f) ?? default;
            numericGeometry[pattern] = pattern.Shooting.SampleGeometry(numericShots[pattern].Numeric);
            originCaptures[pattern] = capture;
            var offsets = capturedOffsets ?? capture.RandomOffsets;
            shotOrigins[pattern] = offsets.HasValue ? origin.Resolve(transform.position, player, bounds, offsets.Value)
                : origin.Resolve(transform.position, player, bounds);
            preparedOriginShots.Add(pattern);
        }
        return true;
    }

    private Vector3 GetShotOrigin(EnemyBurstAttackSettings pattern)
        => !pattern.Shooting.EmissionOrigin.IsDefault && shotOrigins.TryGetValue(pattern, out var position)
            ? position : transform.position;

    public void EndAttackSequence(EnemyBurstAttackSettings settings)
    {
        if (settings == null)
            return;

        if (layoutAttackCount > 0)
        {
            if (HasPendingEmissions(settings)) layoutPendingEnds.Add(settings);
            else layoutAttackCount--;
            FinishLayoutRotationIfIdle();
        }

        for (int index = 0; index < settings.PatternCount; index++)
        {
            activeInitialSpeedSequences.Remove(settings.GetPattern(index));
            NumericRegistry.Release(settings.GetPattern(index));
            numericGeometry.Remove(settings.GetPattern(index));
        }
    }

    public bool TryFireAt(Vector3 targetPosition, EnemyBurstAttackSettings attackSettings)
    {
        return TryFire(
            new PatternAim
            {
                HasValue = true,
                UsesTargetPosition = true,
                Value = targetPosition
            },
            attackSettings);
    }

    public bool TryFireInDirection(Vector3 direction, EnemyBurstAttackSettings attackSettings)
    {
        return TryFire(
            new PatternAim
            {
                HasValue = true,
                UsesTargetPosition = false,
                Value = direction
            },
            attackSettings);
    }

    public bool TryFireStationary(Vector3 directionOrPoint, bool worldPoint, EnemyBurstAttackSettings settings)
    {
        return TryFire(new PatternAim { HasValue = true, UsesTargetPosition = worldPoint,
            UsesStationaryDirection = true, Value = directionOrPoint }, settings);
    }

    public Vector3 GetWaveAimOrigin(EnemyBurstAttackSettings settings)
    {
        return GetEmissionPosition((settings ?? burstAttackSettings).GetPattern(0), 0);
    }

    public bool TryFireInAimedDirection(Vector3 direction, EnemyBurstAttackSettings settings)
    {
        return TryFire(new PatternAim
        {
            HasValue = true,
            UsesAimedDirection = true,
            Value = direction
        }, settings);
    }

    public bool TryFireWithLockedEmissionAim(Vector3 targetPosition, EnemyBurstAttackSettings settings, bool refresh)
    {
        return TryFire(new PatternAim { HasValue = true, UsesTargetPosition = true,
            UsesAimedDirection = true, Value = targetPosition }, settings, refresh);
    }

    private bool TryFire(PatternAim aim, EnemyBurstAttackSettings attackSettings, bool refreshEmissionAim = false)
    {
        EnemyBurstAttackSettings settings = attackSettings ?? burstAttackSettings;
        bool fired = false;
        for (int i = 0; i < settings.PatternCount; i++)
        {
            EnemyBurstAttackSettings pattern = settings.GetPattern(i);
            if (!preparedOriginShots.Contains(pattern) && !PrepareEmissionOriginShot(pattern, false)) return false;
            PatternAim patternAim = aim;
            if (aim.UsesTargetPosition && aim.UsesAimedDirection)
                patternAim.LockedEmissionDirections = GetLockedEmissionDirections(pattern, aim.Value, refreshEmissionAim || pattern.Shooting.UsesGeometryRandomization);
            StorePatternAim(pattern, patternAim);
            bool success = TryLaunchProjectiles(
                pattern, patternAim);
            preparedOriginShots.Remove(pattern);
            fired |= success;
            if (!success)
                return false;
        }
        return fired;
    }

    private Vector3 GetProjectileSpawnPosition(EnemyBurstAttackSettings settings, EnemyShootingSettings geometry = null)
    {
        if (projectileSpawnPoint != null && !ownedSpawnPoint)
            return projectileSpawnPoint.position;
        EnemyShootingSettings shooting = geometry ?? GetShotGeometry(settings);
        Vector3 offset = shooting.EmissionLayout.Type == EnemyEmissionLayoutType.Radial
            || shooting.EmissionLayout.Type == EnemyEmissionLayoutType.Sector
            ? firingRotationSpawnOffset : firingDefaultSpawnOffset;
        Vector3 position = GetFiringPoint(offset);
        if (shooting.EmissionLayout.RotateShootingPointOffset && shooting.Rotates
            && (projectileSpawnPoint == null || projectileSpawnPoint == DirectionTransform
                || projectileSpawnPoint.IsChildOf(DirectionTransform)))
        {
            Vector3 pivot = GetFiringPoint(firingPivotPositionOffset);
            position = pivot + Quaternion.Euler(0f, 0f, shooting.EvaluateLayoutAngle(patternRotationTime)) * (position - pivot);
        }
        return position;
    }

    private Vector3 GetFiringPoint(Vector3 offset)
    {
        Vector3 localPoint = transform.localPosition
            + firingEnemyLocalRotation * Vector3.Scale(offset, transform.localScale);
        return transform.parent != null ? transform.parent.TransformPoint(localPoint) : localPoint;
    }

    public Vector3 GetEmissionPosition(EnemyBurstAttackSettings pattern, int index)
        => GetEmissionPositionAtOrigin(pattern, index, GetShotOrigin(pattern));

    private Vector3 GetEmissionPositionAtOrigin(EnemyBurstAttackSettings pattern, int index, Vector3 origin, EnemyShootingSettings geometry = null)
    {
        var shooting = geometry ?? GetShotGeometry(pattern);
        Quaternion basis = GetVirtualDirectionBasis(shooting);
        Quaternion pointBasis = projectileSpawnPoint != null
            ? ownedSpawnPoint ? FiringEnemyRotation * firingSpawnPointRelativeRotation : projectileSpawnPoint.rotation : basis;
        Vector3 scale = FiringEnemyScale;
        if (shooting.EmissionLayout.Type == EnemyEmissionLayoutType.Point && projectileSpawnPoint != null)
            scale = ownedSpawnPoint ? Vector3.Scale(scale, firingSpawnPointRelativeScale) : projectileSpawnPoint.lossyScale;
        return shooting.GetEmissionPositionFromOrigin(index, GetProjectileSpawnPosition(pattern, shooting), transform.position, origin,
            basis, pointBasis, FiringEnemyRotation, scale, patternRotationTime);
    }

    private Vector3[] GetLockedEmissionDirections(EnemyBurstAttackSettings pattern, Vector3 target, bool refresh)
    {
        int count = GetShotGeometry(pattern).UsesExtendedNumericRandomization
            ? Mathf.Min(EnemyChildAttackSettings.MaximumPerStep, GetShotGeometry(pattern).DirectionCount) : GetShotGeometry(pattern).DirectionCount;
        if (!lockedEmissionDirections.TryGetValue(pattern, out var directions) || directions.Length != count)
        {
            directions = new Vector3[count];
            lockedEmissionDirections[pattern] = directions;
            refresh = true;
        }
        if (refresh)
            for (int i = 0; i < count; i++)
            {
                Vector3 direction = target - GetEmissionPosition(pattern, i);
                directions[i] = direction.sqrMagnitude > .0001f ? direction.normalized : AttackForward;
            }
        return directions;
    }

    private bool TryLaunchProjectiles(EnemyBurstAttackSettings settings, PatternAim aim)
    {
        EnemyShootingSettings shooting = GetShotGeometry(settings);
        EnemyBullet prefab = shooting.ResolveProjectilePrefab(GetSelectedProjectilePrefab(shooting.ProjectileType));
        if (!isActiveAndEnabled || enemy == null || enemy.isDead
            || prefab == null && shooting.ProjectileType != EnemyProjectileType.Laser || projectileSpawner == null)
            return false;
        EnemyProjectileAuthoring authoring = prefab != null ? prefab.GetComponent<EnemyProjectileAuthoring>() : null;
        float projectileBaseSpeed = authoring != null ? authoring.BaseSpeed : 0f;
        bool hasActiveSpeedSequence = activeInitialSpeedSequences.Contains(settings);
        EnemyProjectileInitialSpeedState speedState = hasActiveSpeedSequence
            && initialSpeedSequences.TryGetValue(
                settings,
                out EnemyProjectileInitialSpeedState activeState)
            ? activeState
            : default;
        float volleySpeed = speedState.GetVolleySpeed(
            shooting,
            projectileBaseSpeed);
        float randomSpreadAngle = shooting.UsesShotGun
            ? 0f
            : GetPrecomputedRandomSpreadAngle(shooting);
        if (shooting.EmissionLayout.IsSequential)
        {
            if (!QueueSector(settings, aim, volleySpeed, randomSpreadAngle, AttackTime)) return false;
        }
        else if (!TryLaunchDirections(settings, aim, volleySpeed, randomSpreadAngle, 0,
            shooting.UsesExtendedNumericRandomization ? Mathf.Min(EnemyChildAttackSettings.MaximumPerStep, shooting.DirectionCount) : shooting.DirectionCount,
            randomSpeed: GetNumericShot(settings)))
            return false;

        speedState.AdvanceAfterVolley(shooting);
        if (hasActiveSpeedSequence)
            initialSpeedSequences[settings] = speedState;
        return true;
    }

    private bool TryLaunchDirections(EnemyBurstAttackSettings settings, PatternAim aim, float volleySpeed,
        float randomSpreadAngle, int firstOrder, int endOrder, Vector3? volleyOrigin = null, EnemyRandomSpeedSample randomSpeed = default, EnemySectorLaunchTicket? launchOrder = null, EnemyShootingSettings geometry = null)
    {
        EnemyShootingSettings shot = geometry ?? GetShotGeometry(settings);
        EnemyShootingSettings shooting = shot;
        EnemyBullet prefab = shooting.ResolveProjectilePrefab(GetSelectedProjectilePrefab(shooting.ProjectileType));
        if (!isActiveAndEnabled || enemy == null || enemy.isDead
            || prefab == null && shooting.ProjectileType != EnemyProjectileType.Laser || projectileSpawner == null)
            return false;
        projectileSpawnRequests.Clear();
        for (int order = firstOrder; order < endOrder; order++)
        {
            shooting = shot;
            int arm = shooting.EmissionLayout.IsSequential ? launchOrder.HasValue
                ? launchOrder.Value.GetLaunchIndex(shooting.EmissionLayout, order) : shooting.EmissionLayout.GetLaunchIndex(order) : order;
            shooting = shooting.SampleGeometry(randomSpeed.Numeric.ForDirection(arm), true);
            Vector3 origin = volleyOrigin ?? GetShotOrigin(settings);
            Vector3 position = GetEmissionPositionAtOrigin(settings, arm, origin, shooting);
            Vector3 requested = aim.LockedEmissionDirections != null
                ? arm < aim.LockedEmissionDirections.Length ? aim.LockedEmissionDirections[arm] : aim.Value - position : aim.GetRequestedDirection(position);
            Vector3 direction = shooting.GetEmissionDirection(arm, GetVirtualDirectionBasis(shooting),
                patternRotationTime, requested, aim.UsesTargetPosition || aim.UsesAimedDirection, stationary: aim.UsesStationaryDirection);
            childSpreadSampler ??= GetPrecomputedRandomSpreadAngle;
            EnemyAttackProjectileBuilder.AppendEmitter(projectileSpawnRequests, prefab, shooting, position, origin, direction,
                enemy.DamageMultiplier, volleySpeed, randomSpreadAngle,
                new EnemyProjectilePrefabSet(defaultProjectilePrefab, homingProjectilePrefab, burstAtPointProjectilePrefab), childSpreadSampler,
                completionTracker: completionTracker, randomSpeed: randomSpeed, projectileOffset: arm * shooting.ProjectilesPerDirection,
                maximum: shooting.UsesExtendedNumericRandomization ? Mathf.Max(0, EnemyChildAttackSettings.MaximumPerStep - projectileSpawnRequests.Count) : int.MaxValue,
                directionIndex: arm, targetPoint: shooting.UsesTargetPoint ? GetOriginPlayerPosition() : null,
                laserOrigin: shooting.ProjectileType == EnemyProjectileType.Laser
                    ? new EnemyLaserOrigin(transform, transform.position, shooting.EmissionOrigin, GetOriginCamera(), waveAimTarget,
                        originPlayer != null ? originPlayer.transform : null) : default);
        }

        return projectileSpawner.TrySpawnBatch(projectileSpawnRequests);
    }

    private bool QueueSector(EnemyBurstAttackSettings settings, PatternAim aim, float speed, float spread, float time)
    {
        PendingSectorVolley volley = sectorPool.Count > 0 ? sectorPool.Pop() : new PendingSectorVolley();
        volley.Settings = settings;
        volley.Shooting = GetShotGeometry(settings);
        volley.LaunchOrder = sectorOrders.Reserve(volley.Shooting.EmissionLayout);
        volley.Origin = volley.Shooting.EmissionOrigin.IsDefault ? (Vector3?)null : GetShotOrigin(settings);
        volley.Aim = aim;
        if (aim.LockedEmissionDirections != null)
        {
            int count = aim.LockedEmissionDirections.Length;
            if (volley.LockedDirections == null || volley.LockedDirections.Length != count)
                volley.LockedDirections = new Vector3[count];
            System.Array.Copy(aim.LockedEmissionDirections, volley.LockedDirections, count);
            volley.Aim.LockedEmissionDirections = volley.LockedDirections;
        }
        volley.Speed = speed; volley.NumericSpeed = GetNumericShot(settings);
        volley.Spread = spread;
        volley.NextOrder = 0;
        volley.Failed = false;
        volley.WaitingForReady = volley.Shooting.EmissionLayout.WaitUntilSectorReady && !isFormationAttackReady;
        volley.StartTime = time;
        pendingSectors.Add(volley);
        TickPendingSectors(time);
        return !volley.Failed;
    }

    private void TickPendingSectors(float time)
    {
        for (int i = 0; i < pendingSectors.Count;)
        {
            PendingSectorVolley volley = pendingSectors[i];
            EnemyEmissionLayoutSettings layout = volley.Shooting.EmissionLayout;
            if (volley.WaitingForReady)
            {
                if (!isFormationAttackReady) { i++; continue; }
                volley.WaitingForReady = false;
                volley.StartTime = time;
            }
            int end = volley.NextOrder;
            int total = volley.Shooting.UsesExtendedNumericRandomization ? Mathf.Min(EnemyChildAttackSettings.MaximumPerStep, layout.EmissionCount) : layout.EmissionCount;
            while (end < total && time >= volley.StartTime + layout.StartDelay + end * layout.LaunchInterval)
                end++;
            bool success = true;
            if (end > volley.NextOrder)
            {
                PatternAim aim = volley.Aim;
                // Continuous Aimed follows the current target at the actual launch time.
                if (aim.UsesTargetPosition && !aim.UsesAimedDirection && !aim.UsesStationaryDirection && waveAimTarget != null
                    && (!(waveAimTarget is Object target) || target != null))
                    aim.Value = waveAimTarget.AttackTargetPosition;
                success = TryLaunchDirections(volley.Settings, aim, volley.Speed, volley.Spread, volley.NextOrder, end,
                    volley.Origin ?? transform.position, volley.NumericSpeed, volley.LaunchOrder, volley.Shooting);
                volley.Failed = !success;
                if (!success) Debug.LogError("Sector launch failed; remaining directions were cancelled.", this);
                volley.NextOrder = end;
            }
            if (success && volley.NextOrder < total) { i++; continue; }
            if (success) volley.LaunchOrder.Complete(); else volley.LaunchOrder.Cancel();
            sectorCompletionTimes[volley.Settings] = time;
            pendingSectors.RemoveAt(i);
            volley.Settings = null;
            volley.Shooting = null;
            volley.Aim = default;
            sectorPool.Push(volley);
        }
    }

    public bool HasPendingEmissions(EnemyBurstAttackSettings settings)
    {
        if (settings == null) return false;
        for (int i = 0; i < pendingSectors.Count; i++)
            for (int pattern = 0; pattern < settings.PatternCount; pattern++)
                if (ReferenceEquals(pendingSectors[i].Settings, settings.GetPattern(pattern))) return true;
        return false;
    }

    private bool CanRestartSectorAttack(EnemyBurstAttackSettings settings, float time, float rate)
    {
        if (HasPendingEmissions(settings)) return false;
        return !sectorCompletionTimes.TryGetValue(settings, out float completion)
            || time >= completion + (numericCooldowns.TryGetValue(settings, out float value) ? value : settings.AttackCooldown) / rate;
    }

    private void CancelPendingEmissions()
    {
        for (int i = 0; i < pendingSectors.Count; i++)
        {
            PendingSectorVolley volley = pendingSectors[i];
            volley.LaunchOrder.Cancel();
            volley.Settings = null;
            volley.Shooting = null;
            volley.Aim = default;
            sectorPool.Push(volley);
        }
        pendingSectors.Clear();
        sectorCompletionTimes.Clear();
    }

    public void ApplyRotationOverride(EnemyRotationSettings settings)
    {
        burstAttackSettings.SetRotationForAllPatterns(settings);
        if (waveAttackSettings == null && waveShootingSettings == null)
            RefreshRotation(true);
    }

    public void RestartRotation()
    {
        float facingTime = facingRotationTime;
        RefreshRotation(true);
        patternRotationTime = 0f;
        facingRotationTime = facingTime;
    }
    public void PauseRotation()
    {
        patternRotationRunning = false;
    }
    public void ResumeRotation()
    {
        patternRotationRunning = true;
    }

    public void SetRotationTarget(Transform target)
    {
        if (rotationTarget == target)
            return;
        rotationTarget = target;
        RefreshRotation(true);
    }

    private void RefreshRotation(bool restart)
    {
        float previousLayoutTime = patternRotationTime;
        bool preserveLayout = layoutFinishMode != EnemyLayoutRotationOnAttackFinished.ContinueRotation
            && (layoutRotationHeld || layoutProgramDepth > 0 || layoutAttackCount > 0);
        RestoreFacingPose();
        CaptureVirtualBasis();
        if (restart)
        {
            patternRotationTime = 0f;
            facingRotationTime = 0f;
        }
        patternRotationRunning = true;
        if (preserveLayout) patternRotationTime = previousLayoutTime;
        if (layoutRotationHeld && layoutAttackCount == 0 && layoutProgramDepth == 0) patternRotationRunning = false;
        if (ActivePatterns.FacingMode == EnemyFacingMode.FollowPattern && ActivePatterns.FacingPatternIndex < 0)
            Debug.LogWarning("Facing Pattern is missing. Select an existing pattern in the attack profile.", this);
    }

    private void CaptureVirtualBasis()
    {
        firingEnemyLocalRotation = transform.localRotation;
        firingPivotRelativeRotation = Quaternion.Inverse(transform.rotation) * DirectionTransform.rotation;
        firingPivotPositionOffset = transform.InverseTransformPoint(DirectionTransform.position);
        firingSpawnPointRelativeRotation = projectileSpawnPoint != null
            ? Quaternion.Inverse(transform.rotation) * projectileSpawnPoint.rotation : Quaternion.identity;
        Vector3 rootScale = transform.lossyScale;
        Vector3 pointScale = projectileSpawnPoint != null ? projectileSpawnPoint.lossyScale : rootScale;
        firingSpawnPointRelativeScale = new Vector3(
            Mathf.Abs(rootScale.x) > .0001f ? pointScale.x / rootScale.x : 1f,
            Mathf.Abs(rootScale.y) > .0001f ? pointScale.y / rootScale.y : 1f,
            Mathf.Abs(rootScale.z) > .0001f ? pointScale.z / rootScale.z : 1f);
        ownedSpawnPoint = projectileSpawnPoint != null
            && (projectileSpawnPoint == transform || projectileSpawnPoint.IsChildOf(transform));
        firingDefaultSpawnOffset = ownedSpawnPoint
            ? transform.InverseTransformPoint(projectileSpawnPoint.position) : projectileSpawnOffset;
        firingRotationSpawnOffset = ownedSpawnPoint ? firingDefaultSpawnOffset
            : transform.InverseTransformPoint(DirectionTransform.TransformPoint(projectileSpawnOffset));
        boundFacingTarget = facingTarget != null ? facingTarget : transform;
        if (boundFacingTarget != transform && !boundFacingTarget.IsChildOf(transform))
        {
            if (ActivePatterns.FacingMode == EnemyFacingMode.FollowPattern)
                Debug.LogWarning("Facing Target must be the enemy or one of its children.", this);
            boundFacingTarget = null;
        }
        initialFacingLocalRotation = boundFacingTarget != null ? boundFacingTarget.localRotation : Quaternion.identity;
        int count = ActivePatterns.PatternCount;
        if (patternAims == null || patternAims.Length != count)
            patternAims = new PatternAim[count];
        else
            System.Array.Clear(patternAims, 0, patternAims.Length);
    }

    private void RestoreFacingPose()
    {
        if (facingPoseApplied && boundFacingTarget != null)
            boundFacingTarget.localRotation = initialFacingLocalRotation;
        facingPoseApplied = false;
    }

    private void UpdateLegacyAttackControl()
    {
        if (circleShip != null)
            circleShip.SetWaveAttackControl(isWaveAttackControlled || autonomousAttack);
    }

    private bool TryFireAutonomousPattern(EnemyBurstAttackSettings pattern, bool burstStart)
    {
        var firing = pattern.Shooting.PresetFiring;
        if (!firing.enabled) return TryFireInDirection(AttackForward, pattern);
        if (firing.fireMode == DirectedWaveAttackFireMode.None) return false;
        if (firing.fireMode == DirectedWaveAttackFireMode.Stationary)
            return firing.stationaryFireSettings.TryFire(this, pattern, AttackForward);
        if (firing.fireMode == DirectedWaveAttackFireMode.Default) return TryFireInDirection(AttackForward, pattern);
        if (originPlayer == null && waveAimTarget == null) return false;
        var target = GetOriginPlayerPosition();
        if (firing.fireMode == DirectedWaveAttackFireMode.ForwardWhenPlayerAhead)
        {
            Vector3 direction = target - transform.position;
            if (direction.sqrMagnitude > .0001f && Vector3.Dot(direction.normalized, AttackForward) < Mathf.Cos(firing.forwardFireHalfAngle * Mathf.Deg2Rad)) return false;
            return TryFireInDirection(AttackForward, pattern);
        }
        bool refresh = firing.aimMode == DirectedWaveAttackAimMode.AimContinuous
            || firing.aimMode == DirectedWaveAttackAimMode.AimOnBurst && pattern.RepeatBurst && burstStart;
        return TryFireWithLockedEmissionAim(target, pattern, refresh);
    }

    private void ResetAttackSchedule()
    {
        ClearInitialSpeedSequences(); resettingNumericSchedule = true;
        if (burstAttackSettings.HasMultiplePatterns)
        {
            if (autonomousPatternStarted == null || autonomousPatternStarted.Length != burstAttackSettings.PatternCount)
                autonomousPatternStarted = new bool[burstAttackSettings.PatternCount];
            else System.Array.Clear(autonomousPatternStarted, 0, autonomousPatternStarted.Length);
            if (patternSchedules == null || patternSchedules.Length != burstAttackSettings.PatternCount)
                patternSchedules = new EnemyAttackPatternSchedule[burstAttackSettings.PatternCount];
            float fireRate = enemy != null ? enemy.FireRateMultiplier : 1f;
            for (int i = 0; i < patternSchedules.Length; i++)
            {
                patternSchedules[i].Begin(burstAttackSettings.GetPattern(i), AttackTime, fireRate, true, NumericRegistry.Get(burstAttackSettings.GetPattern(i)));
                BeginAttackSequence(burstAttackSettings.GetPattern(i));
                BeginEmissionOriginAttack(burstAttackSettings.GetPattern(i));
            }
        }
        remainingAttackShots = 0;
        remainingBurstShots = 0;
        float rate = enemy != null ? enemy.FireRateMultiplier : 1f;
        nextAttackTime = AttackTime + (burstAttackSettings.AttackStartDelayRandom.enabled ? 0f : burstAttackSettings.AttackStartDelay / Mathf.Max(0.01f, rate));
        nextShotTime = nextAttackTime; resettingNumericSchedule = false;
    }

    private void TickAutonomousAttack(float currentTime)
    {
        if (burstAttackSettings.HasMultiplePatterns)
        {
            TickMultipleAutonomousAttacks(currentTime);
            return;
        }
        float rate = Mathf.Max(0.01f, enemy.FireRateMultiplier);
        if (remainingAttackShots <= 0)
        {
            if (enemy.AttackPermissions != null && !enemy.AttackPermissions.AllowNewAttacks) return;
            if (currentTime < nextAttackTime || !CanRestartSectorAttack(burstAttackSettings, currentTime, rate))
                return;
            remainingAttackShots = burstAttackSettings.GetAttackShotCountForFireRate(rate, NumericRegistry.Get(burstAttackSettings));
            numericBurstCount = burstAttackSettings.SampleBurstCount(NumericRegistry.Get(burstAttackSettings));
            remainingBurstShots = 0;
            nextShotTime = currentTime + (burstAttackSettings.AttackStartDelayRandom.enabled
                ? burstAttackSettings.SampleStartDelay(NumericRegistry.Get(burstAttackSettings)) / rate : 0f);
            BeginAttackSequence(burstAttackSettings);
            BeginEmissionOriginAttack(burstAttackSettings);
        }
        if (currentTime < nextShotTime)
            return;
        if (burstAttackSettings.RepeatBurst && remainingBurstShots <= 0)
            remainingBurstShots = numericBurstCount;
        if (!PrepareEmissionOriginShot(burstAttackSettings, !burstAttackSettings.RepeatBurst
            || remainingBurstShots == numericBurstCount)) return;
        TryFireAutonomousPattern(burstAttackSettings, !burstAttackSettings.RepeatBurst || remainingBurstShots == numericBurstCount);
        if (burstAttackSettings.RepeatBurst && --remainingBurstShots > 0)
        {
            nextShotTime = currentTime + burstAttackSettings.SampleBurstInterval(NumericRegistry.Get(burstAttackSettings));
            return;
        }
        if (--remainingAttackShots > 0)
        {
            nextShotTime = currentTime + burstAttackSettings.SampleShotInterval(NumericRegistry.Get(burstAttackSettings))
                / (burstAttackSettings.RepeatBurst ? 1f : rate);
            return;
        }
        float cooldown = SampleNumericCooldown(burstAttackSettings);
        EndAttackSequence(burstAttackSettings);
        nextAttackTime = currentTime + cooldown / rate;
    }

    private void TickMultipleAutonomousAttacks(float currentTime)
    {
        float rate = Mathf.Max(0.01f, enemy.FireRateMultiplier);
        for (int i = 0; i < patternSchedules.Length; i++)
        {
            EnemyBurstAttackSettings pattern = burstAttackSettings.GetPattern(i);
            ref EnemyAttackPatternSchedule schedule = ref patternSchedules[i];
            if (!autonomousPatternStarted[i] && enemy.AttackPermissions != null && !enemy.AttackPermissions.AllowNewAttacks) continue;
            if (schedule.IsComplete && currentTime >= schedule.NextAttackTime && CanRestartSectorAttack(pattern, currentTime, rate))
            {
                schedule.Begin(pattern, currentTime, rate, false, NumericRegistry.Get(pattern));
                BeginAttackSequence(pattern);
                BeginEmissionOriginAttack(pattern);
                autonomousPatternStarted[i] = true;
            }
            if (!schedule.IsDue(currentTime))
                continue;
            // Initial schedules are prepared during ResetAttackSchedule without
            // starting an attack lifecycle. Bind it when that pattern first runs.
            if (!autonomousPatternStarted[i])
            {
                BeginAttackSequence(pattern);
                BeginEmissionOriginAttack(pattern);
                autonomousPatternStarted[i] = true;
            }
            if (!PrepareEmissionOriginShot(pattern, schedule.IsBurstStart)) continue;
            TryFireAutonomousPattern(pattern, schedule.IsBurstStart);
            autonomousPatternStarted[i] = true;
            schedule.Advance(pattern, currentTime, rate, schedule.IsLastShot ? SampleNumericCooldown(pattern) : (float?)null);
            if (schedule.IsComplete)
            {
                autonomousPatternStarted[i] = false;
                EndAttackSequence(pattern);
            }
        }
    }

    private void ClearInitialSpeedSequences()
    {
        initialSpeedSequences.Clear();
        numericShots.Clear(); numericCooldowns.Clear(); numericGeometry.Clear(); NumericRegistry.ReleaseAll();
        activeInitialSpeedSequences.Clear();
        lockedEmissionDirections.Clear();
        originCaptures.Clear();
        shotOrigins.Clear();
        preparedOriginShots.Clear();
        warnedMissingOriginContext = false;
    }

    private void PrepareRandomSpreadSequences(EnemyBurstAttackSettings settings)
    {
        if (settings == null)
            return;

        for (int index = 0; index < settings.PatternCount; index++)
            PrepareRandomSpreadSequence(settings.GetPattern(index).Shooting);
    }

    private void PrepareRandomSpreadSequence(EnemyShootingSettings settings)
    {
        if (settings == null || settings.ShootingMode != EnemyProjectileFireMode.Random
            || randomSpreadSequences.ContainsKey(settings))
        {
            return;
        }

        randomSpreadSequences.Add(settings, new EnemyProjectileRandomSpreadSequence(
            settings,
            new System.Random(GetInstanceID() ^ randomSpreadSequences.Count)));
    }

    private float GetPrecomputedRandomSpreadAngle(EnemyShootingSettings settings)
    {
        if (settings.ShootingMode != EnemyProjectileFireMode.Random)
            return 0f;
        if (randomSpreadSequences.TryGetValue(settings, out EnemyProjectileRandomSpreadSequence sequence))
            return sequence.GetNextAngle();

        Debug.LogError("Random projectile pattern was not prepared for this enemy.", this);
        return 0f;
    }

    private EnemyBullet GetSelectedProjectilePrefab(EnemyProjectileType type) => type switch
    {
        EnemyProjectileType.Homing => homingProjectilePrefab,
        EnemyProjectileType.BurstAtPoint => burstAtPointProjectilePrefab,
        _ => defaultProjectilePrefab
    };

    public void ConfigureLegacyFourWay(EnemyBullet prefab, Transform spawnPoint, Vector3 offset,
        EnemyBurstAttackSettings settings, Transform pivot, EnemyRotationSettings rotationSettings, bool rotates)
    {
        defaultProjectilePrefab = prefab;
        projectileSpawnPoint = spawnPoint;
        projectileSpawnOffset = offset;
        rotationTarget = pivot;
        autonomousAttack = true;
        burstAttackSettings.CopyFrom(settings);
        burstAttackSettings.Shooting.ConfigureRotation(rotationSettings, rotates);
        if (rotates && burstAttackSettings.FacingMode == EnemyFacingMode.Default)
        {
            burstAttackSettings.SetFacing(EnemyFacingMode.Rotation);
            burstAttackSettings.FacingRotation.CopyFrom(burstAttackSettings.Shooting.Rotation);
            burstAttackSettings.Shooting.EmissionLayout.SetRotateShootingPointOffset(true);
        }
        PrepareRandomSpreadSequences(burstAttackSettings);
        attackModeVersion = CurrentAttackModeVersion;
        if (Application.isPlaying)
        {
            RefreshRotation(true);
            ResetAttackSchedule();
        }
    }

    internal void SetLegacyProjectileSpawner(EnemyProjectileEcsSpawner spawner)
    {
        if (projectileSpawner == null)
            projectileSpawner = spawner;
    }

    public void MigrateLegacyAttackMode()
    {
        burstAttackSettings ??= new EnemyBurstAttackSettings();
        burstAttackSettings.Validate();
        if (emissionBindingVersion < 1)
        {
            if (burstAttackSettings.Shooting.HadLegacyRotation && burstAttackSettings.FacingMode == EnemyFacingMode.Rotation
                && facingTarget == null && rotationTarget != null
                && (rotationTarget == transform || rotationTarget.IsChildOf(transform)))
                facingTarget = rotationTarget;
            emissionBindingVersion = 1;
        }
        if (attackModeVersion >= CurrentAttackModeVersion)
            return;
        if (attackModeVersion < 1 && burstAttackSettings.UsesAreaAttack)
        {
            shootingMode = 1;
            fanProjectileCount = burstAttackSettings.AreaAttackProjectileCount;
            fanMinAngle = burstAttackSettings.AreaAttackMinAngle;
            fanMaxAngle = burstAttackSettings.AreaAttackMaxAngle;
        }
        burstAttackSettings.Shooting.ConfigureLegacy(projectileType, shootingMode, fanProjectileCount,
            fanMinAngle, fanMaxAngle, shotGunProjectileCount, shotGunDirection, shotGunInitialSpeed,
            projectileSpeedBehavior, projectileSpeedChangeY, projectileSpeedByDistance);
        attackModeVersion = CurrentAttackModeVersion;
    }
}

public enum EnemyProjectileFireMode : byte
{
    Default = 0,
    Random = 2
}

public enum EnemyProjectileType : byte
{
    Default = 0,
    Homing = 1,
    BurstAtPoint = 2,
    Laser = 3,
    Composite = 4,
    // Retain the public aliases and numeric IDs for old serialized waves, presets and spawn specifications.
    CompositeCircle = 4,
    CompositePolygon = 5,
    CompositeSegmentedRing = 6
}
