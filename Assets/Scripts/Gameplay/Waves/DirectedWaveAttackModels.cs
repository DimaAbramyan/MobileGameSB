using UnityEngine;
using UnityEngine.Serialization;

// Kept only to migrate serialized attack settings from the earlier combined mode.
public enum DirectedWaveAttackType
{
    ShootFromFormation,
    DiveAndShoot,
    ShootForward
}

public enum DirectedWaveAttackFireMode
{
    Aimed,
    Forward,
    None
}

public enum DirectedWaveAttackMovementMode
{
    None,
    MoveToPlayer,
    FlyThroughDive
}

public enum DirectedWaveDiveSchedulingMode
{
    WaitForReturn,
    StartNextWhileReturning
}

public enum DirectedWaveFlyThroughReturnMode
{
    EntrancePath,
    ReverseDivePath,
    TeleportPosition
}

public enum DirectedWaveDiveTargetMode
{
    FlyPastPlayer,
    StopAtPlayerRadius
}

public enum DirectedWaveBurstSettingsSource
{
    EnemySettings,
    WaveOverride
}

[System.Serializable]
public sealed class DirectedWaveAttackSettings
{
    private const int CurrentSerializedVersion = 10;

    // Kept to preserve already serialized wave prefabs. The component's enabled
    // state now determines whether post-formation attacks run.
    [SerializeField, HideInInspector] private bool isEnabled;
    [SerializeField, HideInInspector, FormerlySerializedAs("attackType")]
    private DirectedWaveAttackType legacyAttackType =
        DirectedWaveAttackType.ShootFromFormation;

    [Header("Attack Start Delay")]
    [SerializeField, InspectorName("Delay Attack Start")]
    private bool useAttackStartDelay;
    [SerializeField, Min(0f), InspectorName("Attack Start Delay")]
    private float attackStartDelay = 1f;

    [Header("Attack")]
    [SerializeField] private DirectedWaveAttackFireMode fireMode =
        DirectedWaveAttackFireMode.Aimed;

    [Header("Movement")]
    [SerializeField] private DirectedWaveAttackMovementMode movementMode =
        DirectedWaveAttackMovementMode.None;

    [SerializeField, HideInInspector] private int serializedVersion =
        CurrentSerializedVersion;

    [Header("Scheduling")]
    [SerializeField, Min(1)] private int attacksPerEnemyPerCycle = 1;
    [SerializeField, Min(0.01f)] private float attacksPerSecond = 1f;

    [Header("Sequential Scheduling")]
    [SerializeField, InspectorName("Wait For Previous Attack")]
    private bool waitForPreviousAttack;
    [SerializeField, Min(0f), InspectorName("Delay After Attack")]
    private float delayAfterAttack = 1f;

    [Header("Attack Pattern")]
    [SerializeField, InspectorName("Attack Settings Source"), Tooltip(
        "Enemy Settings uses the attack pattern configured on each enemy. "
        + "Wave Override uses the shared values below.")]
    private DirectedWaveBurstSettingsSource burstSettingsSource =
        DirectedWaveBurstSettingsSource.EnemySettings;
    [SerializeField, Tooltip(
        "When Enemy Settings is selected, replaces only each enemy's Attack Cooldown. All other attack pattern values remain on the enemy.")]
    private bool overrideEnemyAttackCooldown;
    [SerializeField, Min(0f), Tooltip(
        "Used only when Enemy Settings is selected and Override Enemy Attack Cooldown is enabled.")]
    private float enemyAttackCooldown = 1f;
    [SerializeField, Tooltip(
        "Used only when Burst Settings Source is Wave Override.")]
    [InspectorName("Wave Attack Pattern")]
    private EnemyBurstAttackSettings waveBurstSettings =
        new EnemyBurstAttackSettings();
    [SerializeField, HideInInspector, FormerlySerializedAs("burstShotCount")]
    private int legacyBurstShotCount = 1;
    [SerializeField, HideInInspector, FormerlySerializedAs("burstShotInterval")]
    private float legacyBurstShotInterval = 0.15f;

    [Header("Dive")]
    [SerializeField, Min(0f)] private float minDiveDepth = 1.5f;
    [SerializeField, Min(0f)] private float maxDiveDepth = 2.5f;
    [SerializeField, Min(0.01f)] private float minDiveSpeed = 4f;
    [SerializeField, Min(0.01f)] private float maxDiveSpeed = 6f;
    [SerializeField] private AnimationCurve diveSpeedCurve =
        AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Fly Through Dive")]
    [SerializeField, Min(0.01f)] private float flyThroughApproachSpeed = 6f;
    [SerializeField, Min(0f)] private float flyThroughExitPadding = 0.5f;
    [SerializeField] private DirectedWaveFlyThroughReturnMode
        flyThroughReturnMode = DirectedWaveFlyThroughReturnMode.EntrancePath;
    [SerializeField, HideInInspector]
    private bool useFlyThroughReturnTeleport;
    [SerializeField, Tooltip(
        "World coordinate where the enemy appears immediately after leaving the camera view before returning along the reverse dive path.")]
    private Vector2 flyThroughReturnTeleportPosition = new(0f, 7f);
    [SerializeField, Min(0f), Tooltip(
        "Per-enemy cooldown that starts after the complete fly-through, teleport and return have finished.")]
    private float flyThroughDiveCooldown = 1f;

    [Header("Dive Preparation")]
    [SerializeField, InspectorName("Use Preparation")]
    private bool useDivePreparation;
    [SerializeField, Min(0f), InspectorName("Preparation Distance")]
    private float divePreparationDistance = 0.75f;
    [SerializeField, Min(0.01f), InspectorName("Preparation Duration")]
    private float divePreparationDuration = 0.2f;
    [SerializeField, InspectorName("Preparation Speed Curve")]
    private AnimationCurve divePreparationSpeedCurve =
        AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Dive Target")]
    [SerializeField] private DirectedWaveDiveTargetMode diveTargetMode =
        DirectedWaveDiveTargetMode.FlyPastPlayer;
    [SerializeField, Min(0f)] private float playerStandoffRadius = 2f;

    [Header("Dive Scheduling")]
    [SerializeField] private DirectedWaveDiveSchedulingMode diveSchedulingMode =
        DirectedWaveDiveSchedulingMode.WaitForReturn;

    [Header("Return")]
    [SerializeField, Min(0.01f)] private float returnSpeedMultiplier = 1f;
    [SerializeField] private AnimationCurve returnSpeedCurve =
        AnimationCurve.Linear(0f, 0f, 1f, 1f);

    public bool IsEnabled => true;
    public float AttackStartDelay => useAttackStartDelay
        ? Mathf.Max(0f, attackStartDelay)
        : 0f;
    public DirectedWaveAttackFireMode FireMode => fireMode;
    public bool HasFireMode => fireMode != DirectedWaveAttackFireMode.None;
    public DirectedWaveAttackMovementMode MovementMode => movementMode;
    public bool RequiresPlayerTarget =>
        fireMode == DirectedWaveAttackFireMode.Aimed
        || UsesDiveMovement;
    public bool MovesToPlayer =>
        movementMode == DirectedWaveAttackMovementMode.MoveToPlayer;
    public bool UsesFlyThroughDive =>
        movementMode == DirectedWaveAttackMovementMode.FlyThroughDive;
    public bool UsesDiveMovement =>
        movementMode != DirectedWaveAttackMovementMode.None;
    public int AttacksPerEnemyPerCycle => Mathf.Max(1, attacksPerEnemyPerCycle);
    public float AttacksPerSecond => Mathf.Max(0.01f, attacksPerSecond);
    public bool WaitsForPreviousAttack => waitForPreviousAttack;
    public float DelayAfterAttack => Mathf.Max(0f, delayAfterAttack);
    public bool UsesEnemyBurstSettings =>
        burstSettingsSource == DirectedWaveBurstSettingsSource.EnemySettings;
    public float ResolveAttackCooldown(float sourceCooldown)
    {
        return UsesEnemyBurstSettings && overrideEnemyAttackCooldown
            ? Mathf.Max(0f, enemyAttackCooldown)
            : Mathf.Max(0f, sourceCooldown);
    }
    public EnemyBurstAttackSettings WaveBurstSettings => waveBurstSettings;
    public float ReturnSpeedMultiplier => Mathf.Max(0.01f, returnSpeedMultiplier);
    public float FlyThroughApproachSpeed => Mathf.Max(
        0.01f,
        flyThroughApproachSpeed);
    public float FlyThroughExitPadding => Mathf.Max(0f, flyThroughExitPadding);
    public DirectedWaveFlyThroughReturnMode FlyThroughReturnMode =>
        flyThroughReturnMode;
    public Vector3 GetFlyThroughReturnTeleportPosition(float worldZ)
    {
        return new Vector3(
            flyThroughReturnTeleportPosition.x,
            flyThroughReturnTeleportPosition.y,
            worldZ);
    }
    public float FlyThroughDiveCooldown => Mathf.Max(0f, flyThroughDiveCooldown);
    public bool UsesDivePreparation => useDivePreparation
        && DivePreparationDistance > 0.0001f;
    public float DivePreparationDistance => Mathf.Max(0f, divePreparationDistance);
    public float DivePreparationDuration => Mathf.Max(
        0.01f,
        divePreparationDuration);
    public AnimationCurve DivePreparationSpeedCurve => divePreparationSpeedCurve;
    public AnimationCurve DiveSpeedCurve => diveSpeedCurve;
    public AnimationCurve ReturnSpeedCurve => returnSpeedCurve;
    public DirectedWaveDiveTargetMode DiveTargetMode => diveTargetMode;
    public float PlayerStandoffRadius => Mathf.Max(0f, playerStandoffRadius);
    public bool AllowsConcurrentMovements =>
        diveSchedulingMode == DirectedWaveDiveSchedulingMode.StartNextWhileReturning;

    public float GetRandomDiveDepth()
    {
        return Random.Range(
            Mathf.Min(minDiveDepth, maxDiveDepth),
            Mathf.Max(minDiveDepth, maxDiveDepth));
    }

    public float GetRandomDiveSpeed()
    {
        return Random.Range(
            Mathf.Min(minDiveSpeed, maxDiveSpeed),
            Mathf.Max(minDiveSpeed, maxDiveSpeed));
    }

    public void CopyFrom(DirectedWaveAttackSettings source)
    {
        if (source == null || ReferenceEquals(source, this))
            return;

        source.Validate();
        isEnabled = source.isEnabled;
        legacyAttackType = source.legacyAttackType;
        useAttackStartDelay = source.useAttackStartDelay;
        attackStartDelay = source.attackStartDelay;
        fireMode = source.fireMode;
        movementMode = source.movementMode;
        attacksPerEnemyPerCycle = source.attacksPerEnemyPerCycle;
        attacksPerSecond = source.attacksPerSecond;
        waitForPreviousAttack = source.waitForPreviousAttack;
        delayAfterAttack = source.delayAfterAttack;
        burstSettingsSource = source.burstSettingsSource;
        overrideEnemyAttackCooldown = source.overrideEnemyAttackCooldown;
        enemyAttackCooldown = source.enemyAttackCooldown;
        waveBurstSettings ??= new EnemyBurstAttackSettings();
        if (source.waveBurstSettings != null)
            waveBurstSettings.CopyFrom(source.waveBurstSettings);
        else
            waveBurstSettings = new EnemyBurstAttackSettings();

        legacyBurstShotCount = source.legacyBurstShotCount;
        legacyBurstShotInterval = source.legacyBurstShotInterval;
        minDiveDepth = source.minDiveDepth;
        maxDiveDepth = source.maxDiveDepth;
        minDiveSpeed = source.minDiveSpeed;
        maxDiveSpeed = source.maxDiveSpeed;
        diveSpeedCurve = CloneCurve(source.diveSpeedCurve);
        flyThroughApproachSpeed = source.flyThroughApproachSpeed;
        flyThroughExitPadding = source.flyThroughExitPadding;
        flyThroughReturnMode = source.flyThroughReturnMode;
        useFlyThroughReturnTeleport = source.useFlyThroughReturnTeleport;
        flyThroughReturnTeleportPosition = source.flyThroughReturnTeleportPosition;
        flyThroughDiveCooldown = source.flyThroughDiveCooldown;
        useDivePreparation = source.useDivePreparation;
        divePreparationDistance = source.divePreparationDistance;
        divePreparationDuration = source.divePreparationDuration;
        divePreparationSpeedCurve = CloneCurve(source.divePreparationSpeedCurve);
        diveTargetMode = source.diveTargetMode;
        playerStandoffRadius = source.playerStandoffRadius;
        diveSchedulingMode = source.diveSchedulingMode;
        returnSpeedMultiplier = source.returnSpeedMultiplier;
        returnSpeedCurve = CloneCurve(source.returnSpeedCurve);
        serializedVersion = CurrentSerializedVersion;
        Validate();
    }

    public void SetAttacksPerSecond(float value)
    {
        attacksPerSecond = Mathf.Max(0.01f, value);
    }

    public void Validate()
    {
        MigrateLegacySettings();

        attacksPerEnemyPerCycle = Mathf.Max(1, attacksPerEnemyPerCycle);
        attacksPerSecond = Mathf.Max(0.01f, attacksPerSecond);
        attackStartDelay = Mathf.Max(0f, attackStartDelay);
        delayAfterAttack = Mathf.Max(0f, delayAfterAttack);
        enemyAttackCooldown = Mathf.Max(0f, enemyAttackCooldown);
        waveBurstSettings ??= new EnemyBurstAttackSettings();
        waveBurstSettings.Validate();
        minDiveDepth = Mathf.Max(0f, minDiveDepth);
        maxDiveDepth = Mathf.Max(minDiveDepth, maxDiveDepth);
        minDiveSpeed = Mathf.Max(0.01f, minDiveSpeed);
        maxDiveSpeed = Mathf.Max(minDiveSpeed, maxDiveSpeed);
        flyThroughApproachSpeed = Mathf.Max(0.01f, flyThroughApproachSpeed);
        flyThroughExitPadding = Mathf.Max(0f, flyThroughExitPadding);
        flyThroughDiveCooldown = Mathf.Max(0f, flyThroughDiveCooldown);
        divePreparationDistance = Mathf.Max(0f, divePreparationDistance);
        divePreparationDuration = Mathf.Max(0.01f, divePreparationDuration);
        playerStandoffRadius = Mathf.Max(0f, playerStandoffRadius);
        returnSpeedMultiplier = Mathf.Max(0.01f, returnSpeedMultiplier);

        if (diveSpeedCurve == null || diveSpeedCurve.length == 0)
            diveSpeedCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        if (divePreparationSpeedCurve == null
            || divePreparationSpeedCurve.length == 0)
        {
            divePreparationSpeedCurve = AnimationCurve.EaseInOut(
                0f,
                0f,
                1f,
                1f);
        }

        if (returnSpeedCurve == null || returnSpeedCurve.length == 0)
            returnSpeedCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
    }

    private void MigrateLegacySettings()
    {
        if (serializedVersion < 1)
        {
            switch (legacyAttackType)
            {
                case DirectedWaveAttackType.DiveAndShoot:
                    fireMode = DirectedWaveAttackFireMode.Aimed;
                    movementMode = DirectedWaveAttackMovementMode.MoveToPlayer;
                    break;

                case DirectedWaveAttackType.ShootForward:
                    fireMode = DirectedWaveAttackFireMode.Forward;
                    movementMode = DirectedWaveAttackMovementMode.None;
                    break;

                default:
                    fireMode = DirectedWaveAttackFireMode.Aimed;
                    movementMode = DirectedWaveAttackMovementMode.None;
                    break;
            }

            serializedVersion = 1;
        }

        waveBurstSettings ??= new EnemyBurstAttackSettings();
        if (serializedVersion < 2)
        {
            waveBurstSettings.ConfigureLegacySingleAttack(
                legacyBurstShotCount,
                0f,
                legacyBurstShotInterval);
            burstSettingsSource = DirectedWaveBurstSettingsSource.WaveOverride;
            serializedVersion = 2;
        }

        if (serializedVersion < 3)
        {
            waveBurstSettings.Validate();
            serializedVersion = 3;
        }

        if (serializedVersion < CurrentSerializedVersion)
        {
            waveBurstSettings.Validate();
            useAttackStartDelay = waveBurstSettings.UsesAttackStartDelay;
            attackStartDelay = waveBurstSettings.AttackStartDelay;
            if (serializedVersion < 5)
            {
                flyThroughApproachSpeed = Mathf.Max(0.01f, minDiveSpeed);
                flyThroughExitPadding = 0.5f;
            }
            if (serializedVersion < 6)
            {
                useDivePreparation = false;
                divePreparationDistance = 0.75f;
                divePreparationDuration = 0.2f;
                divePreparationSpeedCurve = AnimationCurve.EaseInOut(
                    0f,
                    0f,
                    1f,
                    1f);
            }
            if (serializedVersion < 7)
            {
                flyThroughReturnTeleportPosition = new Vector2(0f, 7f);
                flyThroughDiveCooldown = 1f;
            }
            if (serializedVersion < 8)
                useFlyThroughReturnTeleport = false;
            if (serializedVersion < 9)
            {
                flyThroughReturnMode = useFlyThroughReturnTeleport
                    ? DirectedWaveFlyThroughReturnMode.TeleportPosition
                    : DirectedWaveFlyThroughReturnMode.EntrancePath;
            }
            if (serializedVersion < 10)
            {
                overrideEnemyAttackCooldown = false;
                enemyAttackCooldown = 1f;
            }
            serializedVersion = CurrentSerializedVersion;
        }
    }

    private static AnimationCurve CloneCurve(AnimationCurve source)
    {
        if (source == null || source.length == 0)
            return null;

        AnimationCurve copy = new AnimationCurve(source.keys)
        {
            preWrapMode = source.preWrapMode,
            postWrapMode = source.postWrapMode
        };
        return copy;
    }
}
