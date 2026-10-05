using UnityEngine;
using UnityEngine.Serialization;
using Zenject;
using System.Collections.Generic;

[DefaultExecutionOrder(-100)]
[DisallowMultipleComponent]
[RequireComponent(typeof(Enemy))]
public sealed class SimpleEnemyAttackExecutor : MonoBehaviour,
    IEnemyBurstAttackExecutor, IEnemyBurstAttackSettingsOverrideReceiver,
    IEnemyProjectileTypeOverrideReceiver, IEnemyShootingSettingsReceiver, IFormationAttackActivation,
    IEnemyAttackSettingsOverrideState, IEnemyAttackPatternSettingsReceiver, IEnemyAttackDirectionProvider,
    IEnemyAttackAimReceiver, IEnemyAttackSequenceExecutor, IEnemyLockedAimAttackExecutor
{
    private const int CurrentAttackModeVersion = 2;
    [Inject] private EnemyProjectileEcsSpawner projectileSpawner;

    [SerializeField, HideInInspector] private EnemyProjectileType projectileType;
    [SerializeField, FormerlySerializedAs("projectilePrefab")] private EnemyBullet defaultProjectilePrefab;
    [SerializeField] private EnemyBullet homingProjectilePrefab;
    [SerializeField] private EnemyBullet burstAtPointProjectilePrefab;
    [SerializeField] private Transform projectileSpawnPoint;
    [SerializeField] private Vector3 projectileSpawnOffset = new Vector3(0f, 0.25f, 0f);
    [SerializeField, Tooltip("Single: rotate this pivot or the whole enemy when empty. Multiple: use its fixed orientation as the firing basis; neither ship nor pivot is rotated.")]
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

    private Enemy enemy;
    private CircleShip circleShip;
    private EnemyShootingSettings waveShootingSettings;
    private EnemyBurstAttackSettings waveAttackSettings;
    private EnemyAttackPatternSchedule[] patternSchedules;
    private float patternRotationTime;
    private bool patternRotationRunning;
    private Quaternion firingEnemyLocalRotation;
    private Quaternion firingPivotRelativeRotation;
    private Vector3 firingDefaultSpawnOffset;
    private Vector3 firingRotationSpawnOffset;
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

    private struct PatternAim
    {
        public bool HasValue;
        public bool UsesTargetPosition;
        public bool UsesAimedDirection;
        public Vector3 Value;

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
    private readonly EnemyAttackRotation rotation = new EnemyAttackRotation();
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
    private bool UsesVirtualRotation => ActivePatterns.HasMultiplePatterns
        || ActivePatterns.FacingMode != EnemyFacingMode.Default;
    private Quaternion FiringEnemyRotation => transform.parent != null
        ? transform.parent.rotation * firingEnemyLocalRotation : firingEnemyLocalRotation;
    public Vector3 AttackForward => UsesVirtualRotation ? FiringEnemyRotation * Vector3.up : transform.up;
    public bool CanPerformWaveAttack
    {
        get
        {
            if (!isActiveAndEnabled || enemy == null || enemy.isDead || projectileSpawner == null || ActivePatterns.PatternCount == 0)
                return false;
            if (!ActivePatterns.HasMultiplePatterns)
                return GetSelectedProjectilePrefab(ActiveShooting.ProjectileType) != null;
            for (int i = 0; i < ActivePatterns.PatternCount; i++)
                if (GetSelectedProjectilePrefab(ActivePatterns.GetPattern(i).Shooting.ProjectileType) == null)
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

        return TryGetProjectileBaseSpeed(
            settings.GetPattern(0).Shooting.ProjectileType,
            out baseSpeed);
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
            if (!UsesVirtualRotation)
                return rotation.IsComplete;
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
        rotation.Pause();
        patternRotationRunning = false;
        RestoreFacingPose();
        waveAimTarget = null;
        waveShootingSettings = null;
        waveAttackSettings = null;
        isWaveAttackControlled = false;
        ClearInitialSpeedSequences();
        if (circleShip != null)
            circleShip.SetWaveAttackControl(false);
    }

    private void OnValidate()
    {
        MigrateLegacyAttackMode();
        if (Application.isPlaying && enemy != null)
        {
            RefreshRotation(false);
            ResetAttackSchedule();
            UpdateLegacyAttackControl();
        }
    }

    private void Update()
    {
        if (enemy == null || enemy.isDead)
            return;
        if (UsesVirtualRotation)
        {
            if (patternRotationRunning)
                patternRotationTime += Time.deltaTime;
        }
        else
            rotation.Tick(Time.deltaTime);
        if (autonomousAttack && !isWaveAttackControlled && isFormationAttackReady && CanPerformWaveAttack)
            TickAutonomousAttack(Time.time);
    }

    private void LateUpdate()
    {
        if (enemy != null && !enemy.isDead)
            TickFacing(Time.deltaTime);
    }

    private void TickFacing(float deltaTime)
    {
        EnemyBurstAttackSettings patterns = ActivePatterns;
        if (patterns.FacingMode != EnemyFacingMode.FollowPattern || boundFacingTarget == null)
            return;
        int index = patterns.FacingPatternIndex;
        if (index < 0 || patternAims == null || index >= patternAims.Length)
            return;
        EnemyBurstAttackSettings pattern = patterns.GetPattern(index);
        EnemyShootingSettings shooting = pattern.Shooting;
        Vector3 position = GetProjectileSpawnPosition(pattern);
        PatternAim aim = patternAims[index];
        Vector3 requested = aim.GetRequestedDirection(position);
        bool usesAimedShotGunDirection = aim.UsesTargetPosition || aim.UsesAimedDirection;
        if (waveAimTarget != null && (!(waveAimTarget is Object source) || source != null))
        {
            requested = waveAimTarget.AttackTargetPosition - position;
            usesAimedShotGunDirection = true;
        }
        Vector3 direction = shooting.GetFacingDirection(
            ResolveShotDirection(
                shooting,
                requested,
                usesAimedShotGunDirection,
                true));
        boundFacingTarget.rotation = EnemyAttackFacing.Step(boundFacingTarget.rotation, direction,
            patterns.FacingSpeed, patterns.FacingAngleOffset, deltaTime);
        facingPoseApplied = true;
    }

    private void StorePatternAim(EnemyBurstAttackSettings pattern, PatternAim aim)
    {
        if (!UsesVirtualRotation || patternAims == null)
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
        if (shooting.ShootingType == EnemyShootingType.Rotation)
            basis *= firingPivotRelativeRotation;
        return shooting.EvaluateDirectionBasis(basis, patternRotationTime);
    }

    private Quaternion GetShotDirectionBasis(
        EnemyShootingSettings shooting,
        bool virtualRotation)
    {
        if (virtualRotation)
            return GetVirtualDirectionBasis(shooting);

        Transform pivot = shooting.ShootingType == EnemyShootingType.Rotation
            ? DirectionTransform
            : transform;
        return pivot.rotation;
    }

    private Vector3 ResolveShotDirection(
        EnemyShootingSettings shooting,
        Vector3 requestedDirection,
        bool useAimedShotGunDirection,
        bool virtualRotation)
    {
        Quaternion basis = GetShotDirectionBasis(shooting, virtualRotation);
        return shooting.EvaluateShotDirection(
            basis,
            requestedDirection,
            useAimedShotGunDirection);
    }

    public void ApplyBurstAttackSettingsOverride(EnemyBurstAttackSettings settings)
    {
        if (settings == null)
            return;
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
        waveAttackSettings = settings;
        PrepareRandomSpreadSequences(waveAttackSettings);
        waveShootingSettings = settings != null && !settings.HasMultiplePatterns ? settings.Shooting : null;
        RefreshRotation(true);
    }

    public void SetWaveShootingSettings(EnemyShootingSettings settings)
    {
        if (ReferenceEquals(waveShootingSettings, settings) && waveAttackSettings == null)
            return;
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

        PrepareRandomSpreadSequences(settings);
        for (int index = 0; index < settings.PatternCount; index++)
        {
            EnemyBurstAttackSettings pattern = settings.GetPattern(index);
            initialSpeedSequences[pattern] = default;
            activeInitialSpeedSequences.Add(pattern);
        }
    }

    public void EndAttackSequence(EnemyBurstAttackSettings settings)
    {
        if (settings == null)
            return;

        for (int index = 0; index < settings.PatternCount; index++)
            activeInitialSpeedSequences.Remove(settings.GetPattern(index));
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

    public Vector3 GetWaveAimOrigin(EnemyBurstAttackSettings settings)
    {
        return GetProjectileSpawnPosition((settings ?? burstAttackSettings).GetPattern(0));
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

    private bool TryFire(PatternAim aim, EnemyBurstAttackSettings attackSettings)
    {
        EnemyBurstAttackSettings settings = attackSettings ?? burstAttackSettings;
        bool fired = false;
        for (int i = 0; i < settings.PatternCount; i++)
        {
            EnemyBurstAttackSettings pattern = settings.GetPattern(i);
            Vector3 position = GetProjectileSpawnPosition(pattern);
            StorePatternAim(pattern, aim);
            bool success = TryLaunchProjectiles(
                position,
                aim.GetRequestedDirection(position),
                pattern,
                settings.HasMultiplePatterns || UsesVirtualRotation,
                aim.UsesTargetPosition || aim.UsesAimedDirection);
            fired |= success;
            if (!success)
                return false;
        }
        return fired;
    }

    private Vector3 GetProjectileSpawnPosition(EnemyBurstAttackSettings settings)
    {
        if (UsesVirtualRotation)
        {
            if (projectileSpawnPoint != null && !ownedSpawnPoint)
                return projectileSpawnPoint.position;
            Vector3 offset = settings.Shooting.ShootingType == EnemyShootingType.Rotation
                ? firingRotationSpawnOffset : firingDefaultSpawnOffset;
            Vector3 localPoint = transform.localPosition
                + firingEnemyLocalRotation * Vector3.Scale(offset, transform.localScale);
            return transform.parent != null ? transform.parent.TransformPoint(localPoint) : localPoint;
        }
        if (projectileSpawnPoint != null)
            return projectileSpawnPoint.position;
        Transform pivot = settings.Shooting.ShootingType == EnemyShootingType.Rotation ? DirectionTransform : transform;
        return pivot.TransformPoint(projectileSpawnOffset);
    }

    private bool TryLaunchProjectiles(Vector3 position, Vector3 direction, EnemyBurstAttackSettings settings,
        bool virtualRotation, bool useAimedShotGunDirection)
    {
        EnemyShootingSettings shooting = settings.Shooting;
        EnemyBullet prefab = GetSelectedProjectilePrefab(shooting.ProjectileType);
        if (!isActiveAndEnabled || enemy == null || enemy.isDead || prefab == null || projectileSpawner == null)
            return false;
        direction = ResolveShotDirection(
            shooting,
            direction,
            useAimedShotGunDirection,
            virtualRotation);
        EnemyProjectileAuthoring authoring = prefab.GetComponent<EnemyProjectileAuthoring>();
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
        int fanProjectileCount = shooting.UsesFan
            ? shooting.FanProjectileCount
            : 1;
        projectileSpawnRequests.Clear();
        for (int arm = 0; arm < shooting.DirectionCount; arm++)
        {
            float shotGunRandomSpreadAngle = 0f;
            for (int projectile = 0; projectile < shooting.ProjectilesPerDirection; projectile++)
            {
                if (shooting.UsesShotGun
                    && shooting.ShootingMode == EnemyProjectileFireMode.Random
                    && projectile % fanProjectileCount == 0)
                {
                    shotGunRandomSpreadAngle =
                        GetPrecomputedRandomSpreadAngle(shooting);
                }

                float projectileRandomSpreadAngle = shooting.UsesShotGun
                    ? shotGunRandomSpreadAngle
                    : randomSpreadAngle;
                float initialSpeed = shooting.InitialSpeedMode
                    == EnemyProjectileInitialSpeedMode.Random
                    ? shooting.GetRandomInitialSpeed()
                    : volleySpeed;
                projectileSpawnRequests.Add(new EnemyProjectileSpawnRequest(
                    prefab,
                    position,
                    shooting.GetProjectileDirection(
                        direction,
                        arm,
                        projectile,
                        projectileRandomSpreadAngle),
                    enemy.DamageMultiplier,
                    initialSpeed,
                    shooting.SpeedBehavior,
                    shooting.SpeedChangeY,
                    shooting.SpeedByDistance,
                    shooting.SizeBehavior,
                    shooting.SizeChangeY,
                    shooting.SizeChangeSpeed,
                    shooting.SizeChangeCurve,
                    shooting.FinalSize,
                    transform.position,
                    shooting.MovementPattern,
                    shooting.AngularSpeed,
                    shooting.LateralSpeed,
                    shooting.OrbitCenterMode,
                    shooting.OrbitFixedWorldCenter,
                    shooting.OrbitRadius,
                    shooting.OrbitRadialSpeed,
                    shooting.OverridesProjectileLifetime,
                    shooting.ProjectileLifetime));
            }
        }

        if (!projectileSpawner.TrySpawnBatch(projectileSpawnRequests))
            return false;

        speedState.AdvanceAfterVolley(shooting);
        if (hasActiveSpeedSequence)
            initialSpeedSequences[settings] = speedState;
        return true;
    }

    public void ApplyRotationOverride(EnemyRotationSettings settings)
    {
        burstAttackSettings.SetRotationForAllPatterns(settings);
        if (waveAttackSettings == null && waveShootingSettings == null)
            RefreshRotation(true);
    }

    public void RestartRotation() { RefreshRotation(true); }
    public void PauseRotation()
    {
        rotation.Pause();
        patternRotationRunning = false;
    }
    public void ResumeRotation()
    {
        if (UsesVirtualRotation)
        {
            patternRotationRunning = true;
            return;
        }
        if (ActiveShooting.Rotates)
            rotation.Resume();
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
        RestoreFacingPose();
        rotation.Bind(DirectionTransform, ActiveShooting.Rotation);
        if (UsesVirtualRotation)
        {
            rotation.RestoreInitialPose();
            CaptureVirtualBasis();
            if (restart)
                patternRotationTime = 0f;
            patternRotationRunning = true;
            if (ActivePatterns.FacingMode == EnemyFacingMode.FollowPattern && ActivePatterns.FacingPatternIndex < 0)
                Debug.LogWarning("Facing Pattern is missing. Select an existing pattern in the attack profile.", this);
            return;
        }
        CaptureVirtualBasis();
        if (!ActiveShooting.Rotates)
        {
            rotation.Pause();
            return;
        }
        if (restart)
            rotation.Restart();
        else
            rotation.Resume();
    }

    private void CaptureVirtualBasis()
    {
        firingEnemyLocalRotation = transform.localRotation;
        firingPivotRelativeRotation = Quaternion.Inverse(transform.rotation) * DirectionTransform.rotation;
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

    private void ResetAttackSchedule()
    {
        ClearInitialSpeedSequences();
        if (burstAttackSettings.HasMultiplePatterns)
        {
            if (patternSchedules == null || patternSchedules.Length != burstAttackSettings.PatternCount)
                patternSchedules = new EnemyAttackPatternSchedule[burstAttackSettings.PatternCount];
            float fireRate = enemy != null ? enemy.FireRateMultiplier : 1f;
            for (int i = 0; i < patternSchedules.Length; i++)
            {
                patternSchedules[i].Begin(burstAttackSettings.GetPattern(i), Time.time, fireRate, true);
                BeginAttackSequence(burstAttackSettings.GetPattern(i));
            }
        }
        remainingAttackShots = 0;
        remainingBurstShots = 0;
        float rate = enemy != null ? enemy.FireRateMultiplier : 1f;
        nextAttackTime = Time.time + burstAttackSettings.AttackStartDelay / Mathf.Max(0.01f, rate);
        nextShotTime = nextAttackTime;
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
            if (currentTime < nextAttackTime)
                return;
            remainingAttackShots = burstAttackSettings.GetAttackShotCountForFireRate(rate);
            remainingBurstShots = 0;
            nextShotTime = currentTime;
            BeginAttackSequence(burstAttackSettings);
        }
        if (currentTime < nextShotTime)
            return;
        if (burstAttackSettings.RepeatBurst && remainingBurstShots <= 0)
            remainingBurstShots = burstAttackSettings.BurstShotCount;
        TryFireInDirection(AttackForward, burstAttackSettings);
        if (burstAttackSettings.RepeatBurst && --remainingBurstShots > 0)
        {
            nextShotTime = currentTime + burstAttackSettings.BurstShotInterval;
            return;
        }
        if (--remainingAttackShots > 0)
        {
            nextShotTime = currentTime + burstAttackSettings.AttackShotInterval
                / (burstAttackSettings.RepeatBurst ? 1f : rate);
            return;
        }
        EndAttackSequence(burstAttackSettings);
        nextAttackTime = currentTime + burstAttackSettings.AttackCooldown / rate;
    }

    private void TickMultipleAutonomousAttacks(float currentTime)
    {
        float rate = Mathf.Max(0.01f, enemy.FireRateMultiplier);
        for (int i = 0; i < patternSchedules.Length; i++)
        {
            EnemyBurstAttackSettings pattern = burstAttackSettings.GetPattern(i);
            ref EnemyAttackPatternSchedule schedule = ref patternSchedules[i];
            if (schedule.IsComplete && currentTime >= schedule.NextAttackTime)
            {
                schedule.Begin(pattern, currentTime, rate, false);
                BeginAttackSequence(pattern);
            }
            if (!schedule.IsDue(currentTime))
                continue;
            TryFireInDirection(AttackForward, pattern);
            schedule.Advance(pattern, currentTime, rate);
            if (schedule.IsComplete)
                EndAttackSequence(pattern);
        }
    }

    private void ClearInitialSpeedSequences()
    {
        initialSpeedSequences.Clear();
        activeInitialSpeedSequences.Clear();
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
    BurstAtPoint = 2
}
