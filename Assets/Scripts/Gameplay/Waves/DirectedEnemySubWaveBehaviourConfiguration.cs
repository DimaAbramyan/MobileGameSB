using UnityEngine;
using UnityEngine.Serialization;

public sealed partial class DirectedEnemySubWave
{
    private const int StructuredConfigurationVersion = 1;

    [Header("Behaviour Components")]
    [SerializeField] private DirectedWaveFormationBehaviour formationBehaviour;
    [SerializeField] private DirectedWavePhaseEntryBehaviour phaseEntryBehaviour;
    [SerializeField] private DirectedWavePostBehaviour postBehaviourBlock;
    [SerializeField] private DirectedWaveCompletionBehaviour completionBehaviour;
    [SerializeField] private DirectedWaveAttackBehaviour attackBehaviourBlock;

    [Header("Behaviour Blocks")]
    [SerializeField] private DirectedWaveFormationConfiguration formation = new();
    [SerializeField] private DirectedWavePhaseEntryConfiguration phaseEntry = new();
    [SerializeField] private DirectedWavePostBehaviourConfiguration postBehaviour = new();
    [SerializeField] private DirectedWaveAttackPatternConfiguration attackPattern;
    [SerializeField] private DirectedWaveCompletionConfiguration completion;
    [SerializeField, HideInInspector] private int structuredConfigurationVersion;

    private DirectedWaveFormationBehaviour cachedFormationBehaviour;
    private DirectedWavePhaseEntryBehaviour cachedPhaseEntryBehaviour;
    private DirectedWavePostBehaviour cachedPostBehaviour;
    private DirectedWaveCompletionBehaviour cachedCompletionBehaviour;
    private DirectedWaveAttackBehaviour cachedAttackBehaviour;

    // Old prefabs are read through these transient snapshots until the user
    // explicitly runs a migration. They are never serialized back into assets.
    private DirectedWaveFormationConfiguration legacyCompatibilityFormation;
    private DirectedWavePhaseEntryConfiguration legacyCompatibilityPhaseEntry;
    private DirectedWavePostBehaviourConfiguration legacyCompatibilityPostBehaviour;
    private bool legacyCompatibilityConfigurationInitialized;

    // Legacy data remains only as a migration source. It is hidden and never
    // read by runtime code after TryMigrateLegacyBehaviourConfiguration runs.
    [FormerlySerializedAs("enemyPrefab")]
    [SerializeField, HideInInspector] private Enemy legacyEnemyPrefab;
    [FormerlySerializedAs("enemyCount")]
    [SerializeField, HideInInspector] private int legacyEnemyCount;
    [FormerlySerializedAs("spawnInterval")]
    [SerializeField, HideInInspector] private float legacySpawnInterval;
    [FormerlySerializedAs("spawnOrderMode")]
    [SerializeField, HideInInspector] private DirectedWaveSpawnOrderMode
        legacySpawnOrderMode;
    [FormerlySerializedAs("spawnOrderAngle")]
    [SerializeField, HideInInspector] private float legacySpawnOrderAngle;
    [FormerlySerializedAs("spawnOrderStartAngle")]
    [SerializeField, HideInInspector] private float legacySpawnOrderStartAngle;
    [FormerlySerializedAs("spawnPoint")]
    [SerializeField, HideInInspector] private Transform legacySpawnPoint;
    [FormerlySerializedAs("parentEnemiesToSubWave")]
    [SerializeField, HideInInspector] private bool legacyParentEnemiesToSubWave;

    [FormerlySerializedAs("entranceMode")]
    [SerializeField, HideInInspector] private DirectedWaveEntranceMode
        legacyEntranceMode;
    [FormerlySerializedAs("pathCoordinateSpace")]
    [SerializeField, HideInInspector] private DirectedWaveCoordinateSpace
        legacyPathCoordinateSpace;
    [FormerlySerializedAs("rotateEnemiesAlongEntrancePath")]
    [SerializeField, HideInInspector] private bool
        legacyRotateEnemiesAlongEntrancePath;
    [FormerlySerializedAs("pathCheckpoints")]
    [SerializeField, HideInInspector] private DirectedWavePathCheckpoint[]
        legacyPathCheckpoints;
    [FormerlySerializedAs("individualEntrancePoints")]
    [SerializeField, HideInInspector] private DirectedWaveIndividualEntrancePoint[]
        legacyIndividualEntrancePoints;
    [FormerlySerializedAs("individualPointMovementStartDelay")]
    [SerializeField, HideInInspector] private float
        legacyIndividualPointMovementStartDelay;
    [FormerlySerializedAs("individualPointMovementDuration")]
    [SerializeField, HideInInspector] private float
        legacyIndividualPointMovementDuration;
    [FormerlySerializedAs("individualPointMovementCurve")]
    [SerializeField, HideInInspector] private AnimationCurve
        legacyIndividualPointMovementCurve;
    [FormerlySerializedAs("individualEntranceShapeCenter")]
    [SerializeField, HideInInspector] private Vector3
        legacyIndividualEntranceShapeCenter;
    [FormerlySerializedAs("individualEntranceShapeRadius")]
    [SerializeField, HideInInspector] private float
        legacyIndividualEntranceShapeRadius;
    [FormerlySerializedAs("individualEntranceShapeFlattening")]
    [SerializeField, HideInInspector] private Vector2
        legacyIndividualEntranceShapeFlattening;
    [FormerlySerializedAs("individualEntranceShapeRotationDegrees")]
    [SerializeField, HideInInspector] private float
        legacyIndividualEntranceShapeRotationDegrees;
    [FormerlySerializedAs("entranceCompletionMode")]
    [SerializeField, HideInInspector] private DirectedWaveEntranceCompletionMode
        legacyEntranceCompletionMode;
    [FormerlySerializedAs("entranceLoopStartCheckpointIndex")]
    [SerializeField, HideInInspector] private int
        legacyEntranceLoopStartCheckpointIndex;
    [FormerlySerializedAs("entranceLoopTeleportToStart")]
    [SerializeField, HideInInspector] private bool
        legacyEntranceLoopTeleportToStart;
    [FormerlySerializedAs("entranceLoopTeleportDelay")]
    [SerializeField, HideInInspector] private float legacyEntranceLoopTeleportDelay;

    [FormerlySerializedAs("formationFrozen")]
    [SerializeField, HideInInspector] private bool legacyFormationFrozen;
    [FormerlySerializedAs("formationLayout")]
    [SerializeField, HideInInspector] private DirectedWaveFormationLayout
        legacyFormationLayout;
    [FormerlySerializedAs("formationCoordinateSpace")]
    [SerializeField, HideInInspector] private DirectedWaveCoordinateSpace
        legacyFormationCoordinateSpace;
    [FormerlySerializedAs("formationCenter")]
    [SerializeField, HideInInspector] private Vector3 legacyFormationCenter;
    [FormerlySerializedAs("spacing")]
    [SerializeField, HideInInspector] private Vector2 legacySpacing;
    [FormerlySerializedAs("columns")]
    [SerializeField, HideInInspector] private int legacyColumns;
    [FormerlySerializedAs("rows")]
    [SerializeField, HideInInspector] private int legacyRows;
    [FormerlySerializedAs("gridMatrixCells")]
    [SerializeField, HideInInspector] private bool[] legacyGridMatrixCells;
    [FormerlySerializedAs("arcRadius")]
    [SerializeField, HideInInspector] private float legacyArcRadius;
    [FormerlySerializedAs("arcDegrees")]
    [SerializeField, HideInInspector] private float legacyArcDegrees;
    [FormerlySerializedAs("shapePointCount")]
    [SerializeField, HideInInspector] private int legacyShapePointCount;
    [FormerlySerializedAs("shapeRadius")]
    [SerializeField, HideInInspector] private float legacyShapeRadius;
    [FormerlySerializedAs("shapeFlattening")]
    [SerializeField, HideInInspector] private Vector2 legacyShapeFlattening;
    [FormerlySerializedAs("customFormationPoints")]
    [SerializeField, HideInInspector] private Vector3[] legacyCustomFormationPoints;
    [FormerlySerializedAs("customFormationEnemyOverrides")]
    [SerializeField, HideInInspector] private Enemy[]
        legacyCustomFormationEnemyOverrides;
    [FormerlySerializedAs("proceduralFormationEnemyOverrides")]
    [SerializeField, HideInInspector] private Enemy[]
        legacyProceduralFormationEnemyOverrides;
    [FormerlySerializedAs("formationPointsRoot")]
    [SerializeField, HideInInspector] private Transform legacyFormationPointsRoot;
    [FormerlySerializedAs("settleDuration")]
    [SerializeField, HideInInspector] private float legacySettleDuration;
    [FormerlySerializedAs("settleCurve")]
    [SerializeField, HideInInspector] private AnimationCurve legacySettleCurve;

    [FormerlySerializedAs("postCommands")]
    [SerializeField, HideInInspector] private DirectedWavePostCommand[]
        legacyPostCommands;
    [FormerlySerializedAs("postStartDelay")]
    [SerializeField, HideInInspector] private float legacyPostStartDelay;
    [FormerlySerializedAs("postCommandPipelineFixedCount")]
    [SerializeField, HideInInspector] private int
        legacyPostCommandPipelineFixedCount;
    [FormerlySerializedAs("postCommandPipelineLoop")]
    [SerializeField, HideInInspector] private bool legacyPostCommandPipelineLoop;
    [FormerlySerializedAs("localMovementOffset")]
    [SerializeField, HideInInspector] private Vector3 legacyLocalMovementOffset;
    [FormerlySerializedAs("localMovementDuration")]
    [SerializeField, HideInInspector] private float legacyLocalMovementDuration;
    [FormerlySerializedAs("localMovementLoop")]
    [SerializeField, HideInInspector] private bool legacyLocalMovementLoop;
    [FormerlySerializedAs("localMovementPingPong")]
    [SerializeField, HideInInspector] private bool legacyLocalMovementPingPong;
    [FormerlySerializedAs("localMovementCurve")]
    [SerializeField, HideInInspector] private AnimationCurve
        legacyLocalMovementCurve;
    [FormerlySerializedAs("wobbleAmplitude")]
    [SerializeField, HideInInspector] private Vector2 legacyWobbleAmplitude;
    [FormerlySerializedAs("wobbleFrequency")]
    [SerializeField, HideInInspector] private float legacyWobbleFrequency;
    [FormerlySerializedAs("wobblePhaseMode")]
    [SerializeField, HideInInspector] private DirectedWaveWobblePhaseMode
        legacyWobblePhaseMode;
    [FormerlySerializedAs("wobblePhaseOffset")]
    [SerializeField, HideInInspector] private float legacyWobblePhaseOffset;
    [FormerlySerializedAs("wobbleDirectionAngle")]
    [SerializeField, HideInInspector] private float legacyWobbleDirectionAngle;
    [FormerlySerializedAs("wobbleDirectionStep")]
    [SerializeField, HideInInspector] private float legacyWobbleDirectionStep;
    [FormerlySerializedAs("patrolLoop")]
    [SerializeField, HideInInspector] private bool legacyPatrolLoop;
    [FormerlySerializedAs("patrolCoordinateSpace")]
    [SerializeField, HideInInspector] private DirectedWaveCoordinateSpace
        legacyPatrolCoordinateSpace;
    [FormerlySerializedAs("patrolPoints")]
    [SerializeField, HideInInspector] private DirectedWavePatrolPoint[]
        legacyPatrolPoints;
    [FormerlySerializedAs("selfOrbitRadius")]
    [SerializeField, HideInInspector] private Vector2 legacySelfOrbitRadius;
    [FormerlySerializedAs("selfOrbitPhaseOffset")]
    [SerializeField, HideInInspector] private float legacySelfOrbitPhaseOffset;
    [FormerlySerializedAs("selfRotationDegreesPerSecond")]
    [SerializeField, HideInInspector] private float
        legacySelfRotationDegreesPerSecond;
    [FormerlySerializedAs("formationRotationDegreesPerSecond")]
    [SerializeField, HideInInspector] private float
        legacyFormationRotationDegreesPerSecond;
    [FormerlySerializedAs("formationMorphLoop")]
    [SerializeField, HideInInspector] private bool legacyFormationMorphLoop;
    [FormerlySerializedAs("formationMorphReturnDuration")]
    [SerializeField, HideInInspector] private float
        legacyFormationMorphReturnDuration;
    [FormerlySerializedAs("formationMorphReturnCurve")]
    [SerializeField, HideInInspector] private AnimationCurve
        legacyFormationMorphReturnCurve;
    [FormerlySerializedAs("formationMorphSteps")]
    [SerializeField, HideInInspector] private DirectedWaveFormationMorphStep[]
        legacyFormationMorphSteps;

    public DirectedWaveFormationConfiguration Formation => FormationConfig;
    public DirectedWavePhaseEntryConfiguration PhaseEntry => PhaseEntryConfig;
    public DirectedWavePostBehaviourConfiguration PostBehaviour => PostBehaviourConfig;
    public DirectedWaveAttackPatternConfiguration AttackPattern => attackPattern;
    public DirectedWaveCompletionConfiguration Completion => CompletionConfig;

    public DirectedWaveFormationBehaviour FormationBehaviour =>
        ResolveFormationBehaviour();
    public DirectedWavePhaseEntryBehaviour PhaseEntryBehaviour =>
        ResolvePhaseEntryBehaviour();
    public DirectedWavePostBehaviour PostBehaviourComponent =>
        ResolvePostBehaviour();
    public DirectedWaveCompletionBehaviour CompletionBehaviour =>
        ResolveCompletionBehaviour();
    public DirectedWaveAttackBehaviour AttackPatternBehaviour =>
        ResolveAttackBehaviour();

    public Transform PhaseEntrySpawnPoint => PhaseEntryConfig.spawnPoint;

    public bool UsesSeparateBehaviourComponents =>
        FormationBehaviour != null
        || PhaseEntryBehaviour != null
        || PostBehaviourComponent != null
        || CompletionBehaviour != null;

    public bool UsesLegacyConfigurationCompatibility =>
        !UsesSeparateBehaviourComponents
        && structuredConfigurationVersion < StructuredConfigurationVersion
        && HasLegacyBehaviourConfiguration();

    public bool HasPhaseEntry
    {
        get
        {
            DirectedWavePhaseEntryBehaviour component = PhaseEntryBehaviour;
            return component != null
                ? component.isActiveAndEnabled
                : UsesLegacyConfigurationCompatibility
                    || phaseEntry != null && phaseEntry.enabled;
        }
    }

    public bool HasPostBehaviour
    {
        get
        {
            DirectedWavePostBehaviour component = PostBehaviourComponent;
            return component != null
                ? component.isActiveAndEnabled
                : UsesLegacyConfigurationCompatibility
                    || postBehaviour != null && postBehaviour.enabled;
        }
    }

    private DirectedWaveFormationConfiguration FormationConfig =>
        FormationBehaviour != null
            ? FormationBehaviour.Configuration
            : UsesLegacyConfigurationCompatibility
                ? GetLegacyCompatibilityFormation()
            : formation ??= new DirectedWaveFormationConfiguration();

    private DirectedWavePhaseEntryConfiguration PhaseEntryConfig =>
        PhaseEntryBehaviour != null
            ? PhaseEntryBehaviour.Configuration
            : UsesLegacyConfigurationCompatibility
                ? GetLegacyCompatibilityPhaseEntry()
            : phaseEntry ??= new DirectedWavePhaseEntryConfiguration();

    private DirectedWavePostBehaviourConfiguration PostBehaviourConfig =>
        PostBehaviourComponent != null
            ? PostBehaviourComponent.Configuration
            : UsesLegacyConfigurationCompatibility
                ? GetLegacyCompatibilityPostBehaviour()
            : postBehaviour ??= new DirectedWavePostBehaviourConfiguration();

    private DirectedWaveCompletionConfiguration CompletionConfig =>
        CompletionBehaviour != null
            ? CompletionBehaviour.Configuration
            : completion ??= new DirectedWaveCompletionConfiguration();

    public void SetAttackPatternBehaviour(DirectedWaveAttackBehaviour behaviour)
    {
        if (behaviour != null && behaviour.gameObject != gameObject)
        {
            Debug.LogError(
                "Directed Wave Attack Pattern must be attached to the same GameObject as its Directed Enemy Sub Wave.",
                behaviour);
            return;
        }

        DirectedWaveAttackBehaviour previousBehaviour = AttackPatternBehaviour;
        if (behaviour == null)
        {
            previousBehaviour?.UnregisterFromWave();
            attackBehaviourBlock = null;
            cachedAttackBehaviour = null;
            attackPattern = null;
            attackBehaviour = null;
            return;
        }

        if (previousBehaviour != null && previousBehaviour != behaviour)
            previousBehaviour.UnregisterFromWave();

        attackPattern ??= new DirectedWaveAttackPatternConfiguration();
        attackPattern.behaviour = behaviour;
        attackBehaviourBlock = behaviour;
        cachedAttackBehaviour = behaviour;
        attackBehaviour = behaviour;
        behaviour.RegisterWithWave();
    }

    public void SetFormationBehaviour(DirectedWaveFormationBehaviour behaviour)
    {
        if (!IsValidBehaviourComponent(behaviour))
            return;

        formationBehaviour = behaviour;
        cachedFormationBehaviour = behaviour;
    }

    public void SetPhaseEntryBehaviour(DirectedWavePhaseEntryBehaviour behaviour)
    {
        if (!IsValidBehaviourComponent(behaviour))
            return;

        phaseEntryBehaviour = behaviour;
        cachedPhaseEntryBehaviour = behaviour;
    }

    public void SetPostBehaviourComponent(DirectedWavePostBehaviour behaviour)
    {
        if (!IsValidBehaviourComponent(behaviour))
            return;

        postBehaviourBlock = behaviour;
        cachedPostBehaviour = behaviour;
    }

    public void SetCompletionBehaviour(DirectedWaveCompletionBehaviour behaviour)
    {
        if (!IsValidBehaviourComponent(behaviour))
            return;

        completionBehaviour = behaviour;
        cachedCompletionBehaviour = behaviour;
    }

    public void ClearFormationBehaviour(DirectedWaveFormationBehaviour behaviour)
    {
        if (formationBehaviour == behaviour)
            formationBehaviour = null;

        if (cachedFormationBehaviour == behaviour)
            cachedFormationBehaviour = null;
    }

    public void ClearPhaseEntryBehaviour(DirectedWavePhaseEntryBehaviour behaviour)
    {
        if (phaseEntryBehaviour == behaviour)
            phaseEntryBehaviour = null;

        if (cachedPhaseEntryBehaviour == behaviour)
            cachedPhaseEntryBehaviour = null;
    }

    public void ClearPostBehaviourComponent(DirectedWavePostBehaviour behaviour)
    {
        if (postBehaviourBlock == behaviour)
            postBehaviourBlock = null;

        if (cachedPostBehaviour == behaviour)
            cachedPostBehaviour = null;
    }

    public void ClearCompletionBehaviour(DirectedWaveCompletionBehaviour behaviour)
    {
        if (completionBehaviour == behaviour)
            completionBehaviour = null;

        if (cachedCompletionBehaviour == behaviour)
            cachedCompletionBehaviour = null;
    }

    private bool IsValidBehaviourComponent(Component behaviour)
    {
        if (behaviour == null)
            return true;

        if (behaviour.gameObject == gameObject)
            return true;

        Debug.LogError(
            "Directed wave behaviour components must be attached to the same GameObject as their Directed Enemy Sub Wave.",
            behaviour);
        return false;
    }

    private void ResolveBehaviourComponents()
    {
        ResolveFormationBehaviour();
        ResolvePhaseEntryBehaviour();
        ResolvePostBehaviour();
        ResolveCompletionBehaviour();
        ResolveAttackBehaviour();
    }

    private void InvalidateBehaviourComponentCache()
    {
        cachedFormationBehaviour = null;
        cachedPhaseEntryBehaviour = null;
        cachedPostBehaviour = null;
        cachedCompletionBehaviour = null;
        cachedAttackBehaviour = null;
    }

    private DirectedWaveFormationBehaviour ResolveFormationBehaviour()
    {
        if (formationBehaviour != null)
            return formationBehaviour;

        cachedFormationBehaviour ??=
            GetComponent<DirectedWaveFormationBehaviour>();
        return cachedFormationBehaviour;
    }

    private DirectedWavePhaseEntryBehaviour ResolvePhaseEntryBehaviour()
    {
        if (phaseEntryBehaviour != null)
            return phaseEntryBehaviour;

        cachedPhaseEntryBehaviour ??=
            GetComponent<DirectedWavePhaseEntryBehaviour>();
        return cachedPhaseEntryBehaviour;
    }

    private DirectedWavePostBehaviour ResolvePostBehaviour()
    {
        if (postBehaviourBlock != null)
            return postBehaviourBlock;

        cachedPostBehaviour ??= GetComponent<DirectedWavePostBehaviour>();
        return cachedPostBehaviour;
    }

    private DirectedWaveCompletionBehaviour ResolveCompletionBehaviour()
    {
        if (completionBehaviour != null)
            return completionBehaviour;

        cachedCompletionBehaviour ??=
            GetComponent<DirectedWaveCompletionBehaviour>();
        return cachedCompletionBehaviour;
    }

    private DirectedWaveAttackBehaviour ResolveAttackBehaviour()
    {
        if (attackBehaviourBlock != null)
            return attackBehaviourBlock;

        if (attackPattern != null && attackPattern.behaviour != null)
            return attackPattern.behaviour;

        cachedAttackBehaviour ??= GetComponent<DirectedWaveAttackBehaviour>();
        return cachedAttackBehaviour;
    }

    private Enemy enemyPrefab
    {
        get => FormationConfig.enemyPrefab;
        set => FormationConfig.enemyPrefab = value;
    }

    private int enemyCount
    {
        get => FormationConfig.enemyCount;
        set => FormationConfig.enemyCount = value;
    }

    private float spawnInterval
    {
        get => FormationConfig.spawnInterval;
        set => FormationConfig.spawnInterval = value;
    }

    private DirectedWaveSpawnOrderMode spawnOrderMode
    {
        get => FormationConfig.spawnOrderMode;
        set => FormationConfig.spawnOrderMode = value;
    }

    private float spawnOrderAngle
    {
        get => FormationConfig.spawnOrderAngle;
        set => FormationConfig.spawnOrderAngle = value;
    }

    private float spawnOrderStartAngle
    {
        get => FormationConfig.spawnOrderStartAngle;
        set => FormationConfig.spawnOrderStartAngle = value;
    }

    private Transform spawnPoint
    {
        get => PhaseEntryConfig.spawnPoint;
        set => PhaseEntryConfig.spawnPoint = value;
    }

    private bool parentEnemiesToSubWave
    {
        get => FormationConfig.parentEnemiesToSubWave;
        set => FormationConfig.parentEnemiesToSubWave = value;
    }

    private DirectedWaveEntranceMode entranceMode
    {
        get => PhaseEntryConfig.entranceMode;
        set => PhaseEntryConfig.entranceMode = value;
    }

    private DirectedWaveCoordinateSpace pathCoordinateSpace
    {
        get => PhaseEntryConfig.pathCoordinateSpace;
        set => PhaseEntryConfig.pathCoordinateSpace = value;
    }

    private bool rotateEnemiesAlongEntrancePath
    {
        get => PhaseEntryConfig.rotateEnemiesAlongEntrancePath;
        set => PhaseEntryConfig.rotateEnemiesAlongEntrancePath = value;
    }

    private DirectedWavePathCheckpoint[] pathCheckpoints
    {
        get => PhaseEntryConfig.pathCheckpoints;
        set => PhaseEntryConfig.pathCheckpoints = value;
    }

    private DirectedWaveIndividualEntrancePoint[] individualEntrancePoints
    {
        get => PhaseEntryConfig.individualEntrancePoints;
        set => PhaseEntryConfig.individualEntrancePoints = value;
    }

    private float individualPointMovementStartDelay
    {
        get => PhaseEntryConfig.individualPointMovementStartDelay;
        set => PhaseEntryConfig.individualPointMovementStartDelay = value;
    }

    private float individualPointMovementDuration
    {
        get => PhaseEntryConfig.individualPointMovementDuration;
        set => PhaseEntryConfig.individualPointMovementDuration = value;
    }

    private AnimationCurve individualPointMovementCurve
    {
        get => PhaseEntryConfig.individualPointMovementCurve;
        set => PhaseEntryConfig.individualPointMovementCurve = value;
    }

    private Vector3 individualEntranceShapeCenter
    {
        get => PhaseEntryConfig.individualEntranceShapeCenter;
        set => PhaseEntryConfig.individualEntranceShapeCenter = value;
    }

    private float individualEntranceShapeRadius
    {
        get => PhaseEntryConfig.individualEntranceShapeRadius;
        set => PhaseEntryConfig.individualEntranceShapeRadius = value;
    }

    private Vector2 individualEntranceShapeFlattening
    {
        get => PhaseEntryConfig.individualEntranceShapeFlattening;
        set => PhaseEntryConfig.individualEntranceShapeFlattening = value;
    }

    private float individualEntranceShapeRotationDegrees
    {
        get => PhaseEntryConfig.individualEntranceShapeRotationDegrees;
        set => PhaseEntryConfig.individualEntranceShapeRotationDegrees = value;
    }

    private DirectedWaveEntranceCompletionMode entranceCompletionMode
    {
        get => PhaseEntryConfig.entranceCompletionMode;
        set => PhaseEntryConfig.entranceCompletionMode = value;
    }

    private int entranceLoopStartCheckpointIndex
    {
        get => PhaseEntryConfig.entranceLoopStartCheckpointIndex;
        set => PhaseEntryConfig.entranceLoopStartCheckpointIndex = value;
    }

    private bool entranceLoopTeleportToStart
    {
        get => PhaseEntryConfig.entranceLoopTeleportToStart;
        set => PhaseEntryConfig.entranceLoopTeleportToStart = value;
    }

    private float entranceLoopTeleportDelay
    {
        get => PhaseEntryConfig.entranceLoopTeleportDelay;
        set => PhaseEntryConfig.entranceLoopTeleportDelay = value;
    }

    private bool formationFrozen
    {
        get => FormationConfig.formationFrozen;
        set => FormationConfig.formationFrozen = value;
    }

    private DirectedWaveFormationLayout formationLayout
    {
        get => FormationConfig.formationLayout;
        set => FormationConfig.formationLayout = value;
    }

    private DirectedWaveCoordinateSpace formationCoordinateSpace
    {
        get => FormationConfig.formationCoordinateSpace;
        set => FormationConfig.formationCoordinateSpace = value;
    }

    private Vector3 formationCenter
    {
        get => FormationConfig.formationCenter;
        set => FormationConfig.formationCenter = value;
    }

    private Vector2 spacing
    {
        get => FormationConfig.spacing;
        set => FormationConfig.spacing = value;
    }

    private int columns
    {
        get => FormationConfig.columns;
        set => FormationConfig.columns = value;
    }

    private int rows
    {
        get => FormationConfig.rows;
        set => FormationConfig.rows = value;
    }

    private bool[] gridMatrixCells
    {
        get => FormationConfig.gridMatrixCells;
        set => FormationConfig.gridMatrixCells = value;
    }

    private float arcRadius
    {
        get => FormationConfig.arcRadius;
        set => FormationConfig.arcRadius = value;
    }

    private float arcDegrees
    {
        get => FormationConfig.arcDegrees;
        set => FormationConfig.arcDegrees = value;
    }

    private int shapePointCount
    {
        get => FormationConfig.shapePointCount;
        set => FormationConfig.shapePointCount = value;
    }

    private float shapeRadius
    {
        get => FormationConfig.shapeRadius;
        set => FormationConfig.shapeRadius = value;
    }

    private Vector2 shapeFlattening
    {
        get => FormationConfig.shapeFlattening;
        set => FormationConfig.shapeFlattening = value;
    }

    private Vector3[] customFormationPoints
    {
        get => FormationConfig.customFormationPoints;
        set => FormationConfig.customFormationPoints = value;
    }

    private Enemy[] customFormationEnemyOverrides
    {
        get => FormationConfig.customFormationEnemyOverrides;
        set => FormationConfig.customFormationEnemyOverrides = value;
    }

    private Enemy[] proceduralFormationEnemyOverrides
    {
        get => FormationConfig.proceduralFormationEnemyOverrides;
        set => FormationConfig.proceduralFormationEnemyOverrides = value;
    }

    private Transform formationPointsRoot
    {
        get => FormationConfig.formationPointsRoot;
        set => FormationConfig.formationPointsRoot = value;
    }

    private float settleDuration
    {
        get => PhaseEntryConfig.settleDuration;
        set => PhaseEntryConfig.settleDuration = value;
    }

    private AnimationCurve settleCurve
    {
        get => PhaseEntryConfig.settleCurve;
        set => PhaseEntryConfig.settleCurve = value;
    }

    private DirectedWavePostCommand[] postCommands
    {
        get => PostBehaviourConfig.postCommands;
        set => PostBehaviourConfig.postCommands = value;
    }

    private float postStartDelay
    {
        get => PostBehaviourConfig.postStartDelay;
        set => PostBehaviourConfig.postStartDelay = value;
    }

    private int postCommandPipelineFixedCount
    {
        get => PostBehaviourConfig.postCommandPipelineFixedCount;
        set => PostBehaviourConfig.postCommandPipelineFixedCount = value;
    }

    private bool postCommandPipelineLoop
    {
        get => PostBehaviourConfig.postCommandPipelineLoop;
        set => PostBehaviourConfig.postCommandPipelineLoop = value;
    }

    private Vector3 localMovementOffset
    {
        get => PostBehaviourConfig.localMovementOffset;
        set => PostBehaviourConfig.localMovementOffset = value;
    }

    private float localMovementDuration
    {
        get => PostBehaviourConfig.localMovementDuration;
        set => PostBehaviourConfig.localMovementDuration = value;
    }

    private bool localMovementLoop
    {
        get => PostBehaviourConfig.localMovementLoop;
        set => PostBehaviourConfig.localMovementLoop = value;
    }

    private bool localMovementPingPong
    {
        get => PostBehaviourConfig.localMovementPingPong;
        set => PostBehaviourConfig.localMovementPingPong = value;
    }

    private AnimationCurve localMovementCurve
    {
        get => PostBehaviourConfig.localMovementCurve;
        set => PostBehaviourConfig.localMovementCurve = value;
    }

    private Vector2 wobbleAmplitude
    {
        get => PostBehaviourConfig.wobbleAmplitude;
        set => PostBehaviourConfig.wobbleAmplitude = value;
    }

    private float wobbleFrequency
    {
        get => PostBehaviourConfig.wobbleFrequency;
        set => PostBehaviourConfig.wobbleFrequency = value;
    }

    private DirectedWaveWobblePhaseMode wobblePhaseMode
    {
        get => PostBehaviourConfig.wobblePhaseMode;
        set => PostBehaviourConfig.wobblePhaseMode = value;
    }

    private float wobblePhaseOffset
    {
        get => PostBehaviourConfig.wobblePhaseOffset;
        set => PostBehaviourConfig.wobblePhaseOffset = value;
    }

    private float wobbleDirectionAngle
    {
        get => PostBehaviourConfig.wobbleDirectionAngle;
        set => PostBehaviourConfig.wobbleDirectionAngle = value;
    }

    private float wobbleDirectionStep
    {
        get => PostBehaviourConfig.wobbleDirectionStep;
        set => PostBehaviourConfig.wobbleDirectionStep = value;
    }

    private bool patrolLoop
    {
        get => PostBehaviourConfig.patrolLoop;
        set => PostBehaviourConfig.patrolLoop = value;
    }

    private DirectedWaveCoordinateSpace patrolCoordinateSpace
    {
        get => PostBehaviourConfig.patrolCoordinateSpace;
        set => PostBehaviourConfig.patrolCoordinateSpace = value;
    }

    private DirectedWavePatrolPoint[] patrolPoints
    {
        get => PostBehaviourConfig.patrolPoints;
        set => PostBehaviourConfig.patrolPoints = value;
    }

    private Vector2 selfOrbitRadius
    {
        get => PostBehaviourConfig.selfOrbitRadius;
        set => PostBehaviourConfig.selfOrbitRadius = value;
    }

    private float selfOrbitPhaseOffset
    {
        get => PostBehaviourConfig.selfOrbitPhaseOffset;
        set => PostBehaviourConfig.selfOrbitPhaseOffset = value;
    }

    private float selfRotationDegreesPerSecond
    {
        get => PostBehaviourConfig.selfRotationDegreesPerSecond;
        set => PostBehaviourConfig.selfRotationDegreesPerSecond = value;
    }

    private float formationRotationDegreesPerSecond
    {
        get => PostBehaviourConfig.formationRotationDegreesPerSecond;
        set => PostBehaviourConfig.formationRotationDegreesPerSecond = value;
    }

    private bool formationMorphLoop
    {
        get => PostBehaviourConfig.formationMorphLoop;
        set => PostBehaviourConfig.formationMorphLoop = value;
    }

    private float formationMorphReturnDuration
    {
        get => PostBehaviourConfig.formationMorphReturnDuration;
        set => PostBehaviourConfig.formationMorphReturnDuration = value;
    }

    private AnimationCurve formationMorphReturnCurve
    {
        get => PostBehaviourConfig.formationMorphReturnCurve;
        set => PostBehaviourConfig.formationMorphReturnCurve = value;
    }

    private DirectedWaveFormationMorphStep[] formationMorphSteps
    {
        get => PostBehaviourConfig.formationMorphSteps;
        set => PostBehaviourConfig.formationMorphSteps = value;
    }

    private DirectedWaveFormationConfiguration GetLegacyCompatibilityFormation()
    {
        EnsureLegacyCompatibilityConfiguration();
        return legacyCompatibilityFormation;
    }

    private DirectedWavePhaseEntryConfiguration GetLegacyCompatibilityPhaseEntry()
    {
        EnsureLegacyCompatibilityConfiguration();
        return legacyCompatibilityPhaseEntry;
    }

    private DirectedWavePostBehaviourConfiguration GetLegacyCompatibilityPostBehaviour()
    {
        EnsureLegacyCompatibilityConfiguration();
        return legacyCompatibilityPostBehaviour;
    }

    private void EnsureLegacyCompatibilityConfiguration()
    {
        if (legacyCompatibilityConfigurationInitialized)
            return;

        legacyCompatibilityFormation = new DirectedWaveFormationConfiguration
        {
            enemyPrefab = legacyEnemyPrefab,
            enemyCount = legacyEnemyCount,
            spawnInterval = legacySpawnInterval,
            spawnOrderMode = legacySpawnOrderMode,
            spawnOrderAngle = legacySpawnOrderAngle,
            spawnOrderStartAngle = legacySpawnOrderStartAngle,
            parentEnemiesToSubWave = legacyParentEnemiesToSubWave,
            formationFrozen = legacyFormationFrozen,
            formationLayout = legacyFormationLayout,
            formationCoordinateSpace = legacyFormationCoordinateSpace,
            formationCenter = legacyFormationCenter,
            spacing = legacySpacing,
            columns = legacyColumns,
            rows = legacyRows,
            gridMatrixCells = legacyGridMatrixCells,
            arcRadius = legacyArcRadius,
            arcDegrees = legacyArcDegrees,
            shapePointCount = legacyShapePointCount,
            shapeRadius = legacyShapeRadius,
            shapeFlattening = legacyShapeFlattening,
            customFormationPoints = legacyCustomFormationPoints,
            customFormationEnemyOverrides = legacyCustomFormationEnemyOverrides,
            proceduralFormationEnemyOverrides =
                legacyProceduralFormationEnemyOverrides,
            formationPointsRoot = legacyFormationPointsRoot
        };

        legacyCompatibilityPhaseEntry = new DirectedWavePhaseEntryConfiguration
        {
            enabled = true,
            spawnPoint = legacySpawnPoint,
            entranceMode = legacyEntranceMode,
            pathCoordinateSpace = legacyPathCoordinateSpace,
            rotateEnemiesAlongEntrancePath =
                legacyRotateEnemiesAlongEntrancePath,
            pathCheckpoints = legacyPathCheckpoints,
            individualEntrancePoints = legacyIndividualEntrancePoints,
            individualPointMovementStartDelay =
                legacyIndividualPointMovementStartDelay,
            individualPointMovementDuration = legacyIndividualPointMovementDuration,
            individualPointMovementCurve = legacyIndividualPointMovementCurve,
            individualEntranceShapeCenter = legacyIndividualEntranceShapeCenter,
            individualEntranceShapeRadius = legacyIndividualEntranceShapeRadius,
            individualEntranceShapeFlattening =
                legacyIndividualEntranceShapeFlattening,
            individualEntranceShapeRotationDegrees =
                legacyIndividualEntranceShapeRotationDegrees,
            entranceCompletionMode = legacyEntranceCompletionMode,
            entranceLoopStartCheckpointIndex =
                legacyEntranceLoopStartCheckpointIndex,
            entranceLoopTeleportToStart = legacyEntranceLoopTeleportToStart,
            entranceLoopTeleportDelay = legacyEntranceLoopTeleportDelay,
            settleDuration = legacySettleDuration,
            settleCurve = legacySettleCurve
        };

        legacyCompatibilityPostBehaviour =
            new DirectedWavePostBehaviourConfiguration
            {
                enabled = true,
                postCommands = legacyPostCommands,
                postStartDelay = legacyPostStartDelay,
                postCommandPipelineFixedCount =
                    legacyPostCommandPipelineFixedCount,
                postCommandPipelineLoop = legacyPostCommandPipelineLoop,
                localMovementOffset = legacyLocalMovementOffset,
                localMovementDuration = legacyLocalMovementDuration,
                localMovementLoop = legacyLocalMovementLoop,
                localMovementPingPong = legacyLocalMovementPingPong,
                localMovementCurve = legacyLocalMovementCurve,
                wobbleAmplitude = legacyWobbleAmplitude,
                wobbleFrequency = legacyWobbleFrequency,
                wobblePhaseMode = legacyWobblePhaseMode,
                wobblePhaseOffset = legacyWobblePhaseOffset,
                wobbleDirectionAngle = legacyWobbleDirectionAngle,
                wobbleDirectionStep = legacyWobbleDirectionStep,
                patrolLoop = legacyPatrolLoop,
                patrolCoordinateSpace = legacyPatrolCoordinateSpace,
                patrolPoints = legacyPatrolPoints,
                selfOrbitRadius = legacySelfOrbitRadius,
                selfOrbitPhaseOffset = legacySelfOrbitPhaseOffset,
                selfRotationDegreesPerSecond =
                    legacySelfRotationDegreesPerSecond,
                formationRotationDegreesPerSecond =
                    legacyFormationRotationDegreesPerSecond,
                formationMorphLoop = legacyFormationMorphLoop,
                formationMorphReturnDuration =
                    legacyFormationMorphReturnDuration,
                formationMorphReturnCurve = legacyFormationMorphReturnCurve,
                formationMorphSteps = legacyFormationMorphSteps
            };

        legacyCompatibilityConfigurationInitialized = true;
    }

    private void InvalidateLegacyCompatibilityConfiguration()
    {
        legacyCompatibilityFormation = null;
        legacyCompatibilityPhaseEntry = null;
        legacyCompatibilityPostBehaviour = null;
        legacyCompatibilityConfigurationInitialized = false;
    }

    public bool TryMigrateLegacyBehaviourConfiguration()
    {
        EnsureBehaviourBlocks();
        if (structuredConfigurationVersion >= StructuredConfigurationVersion)
            return false;

        if (!HasLegacyBehaviourConfiguration())
        {
            structuredConfigurationVersion = StructuredConfigurationVersion;
            return false;
        }

        formation.enemyPrefab = legacyEnemyPrefab;
        formation.enemyCount = legacyEnemyCount;
        formation.spawnInterval = legacySpawnInterval;
        formation.spawnOrderMode = legacySpawnOrderMode;
        formation.spawnOrderAngle = legacySpawnOrderAngle;
        formation.spawnOrderStartAngle = legacySpawnOrderStartAngle;
        formation.parentEnemiesToSubWave = legacyParentEnemiesToSubWave;

        phaseEntry.enabled = true;
        phaseEntry.spawnPoint = legacySpawnPoint;
        phaseEntry.entranceMode = legacyEntranceMode;
        phaseEntry.pathCoordinateSpace = legacyPathCoordinateSpace;
        phaseEntry.rotateEnemiesAlongEntrancePath =
            legacyRotateEnemiesAlongEntrancePath;
        phaseEntry.pathCheckpoints = legacyPathCheckpoints;
        phaseEntry.individualEntrancePoints = legacyIndividualEntrancePoints;
        phaseEntry.individualPointMovementStartDelay =
            legacyIndividualPointMovementStartDelay;
        phaseEntry.individualPointMovementDuration =
            legacyIndividualPointMovementDuration;
        phaseEntry.individualPointMovementCurve =
            legacyIndividualPointMovementCurve;
        phaseEntry.individualEntranceShapeCenter =
            legacyIndividualEntranceShapeCenter;
        phaseEntry.individualEntranceShapeRadius =
            legacyIndividualEntranceShapeRadius;
        phaseEntry.individualEntranceShapeFlattening =
            legacyIndividualEntranceShapeFlattening;
        phaseEntry.individualEntranceShapeRotationDegrees =
            legacyIndividualEntranceShapeRotationDegrees;
        phaseEntry.entranceCompletionMode = legacyEntranceCompletionMode;
        phaseEntry.entranceLoopStartCheckpointIndex =
            legacyEntranceLoopStartCheckpointIndex;
        phaseEntry.entranceLoopTeleportToStart =
            legacyEntranceLoopTeleportToStart;
        phaseEntry.entranceLoopTeleportDelay = legacyEntranceLoopTeleportDelay;
        phaseEntry.settleDuration = legacySettleDuration;
        phaseEntry.settleCurve = legacySettleCurve;

        formation.formationFrozen = legacyFormationFrozen;
        formation.formationLayout = legacyFormationLayout;
        formation.formationCoordinateSpace = legacyFormationCoordinateSpace;
        formation.formationCenter = legacyFormationCenter;
        formation.spacing = legacySpacing;
        formation.columns = legacyColumns;
        formation.rows = legacyRows;
        formation.gridMatrixCells = legacyGridMatrixCells;
        formation.arcRadius = legacyArcRadius;
        formation.arcDegrees = legacyArcDegrees;
        formation.shapePointCount = legacyShapePointCount;
        formation.shapeRadius = legacyShapeRadius;
        formation.shapeFlattening = legacyShapeFlattening;
        formation.customFormationPoints = legacyCustomFormationPoints;
        formation.customFormationEnemyOverrides =
            legacyCustomFormationEnemyOverrides;
        formation.proceduralFormationEnemyOverrides =
            legacyProceduralFormationEnemyOverrides;
        formation.formationPointsRoot = legacyFormationPointsRoot;

        postBehaviour.enabled = true;
        postBehaviour.postCommands = legacyPostCommands;
        postBehaviour.postStartDelay = legacyPostStartDelay;
        postBehaviour.postCommandPipelineFixedCount =
            legacyPostCommandPipelineFixedCount;
        postBehaviour.postCommandPipelineLoop = legacyPostCommandPipelineLoop;
        postBehaviour.localMovementOffset = legacyLocalMovementOffset;
        postBehaviour.localMovementDuration = legacyLocalMovementDuration;
        postBehaviour.localMovementLoop = legacyLocalMovementLoop;
        postBehaviour.localMovementPingPong = legacyLocalMovementPingPong;
        postBehaviour.localMovementCurve = legacyLocalMovementCurve;
        postBehaviour.wobbleAmplitude = legacyWobbleAmplitude;
        postBehaviour.wobbleFrequency = legacyWobbleFrequency;
        postBehaviour.wobblePhaseMode = legacyWobblePhaseMode;
        postBehaviour.wobblePhaseOffset = legacyWobblePhaseOffset;
        postBehaviour.wobbleDirectionAngle = legacyWobbleDirectionAngle;
        postBehaviour.wobbleDirectionStep = legacyWobbleDirectionStep;
        postBehaviour.patrolLoop = legacyPatrolLoop;
        postBehaviour.patrolCoordinateSpace = legacyPatrolCoordinateSpace;
        postBehaviour.patrolPoints = legacyPatrolPoints;
        postBehaviour.selfOrbitRadius = legacySelfOrbitRadius;
        postBehaviour.selfOrbitPhaseOffset = legacySelfOrbitPhaseOffset;
        postBehaviour.selfRotationDegreesPerSecond =
            legacySelfRotationDegreesPerSecond;
        postBehaviour.formationRotationDegreesPerSecond =
            legacyFormationRotationDegreesPerSecond;
        postBehaviour.formationMorphLoop = legacyFormationMorphLoop;
        postBehaviour.formationMorphReturnDuration =
            legacyFormationMorphReturnDuration;
        postBehaviour.formationMorphReturnCurve =
            legacyFormationMorphReturnCurve;
        postBehaviour.formationMorphSteps = legacyFormationMorphSteps;

        DirectedWaveAttackBehaviour legacyAttack =
            GetComponent<DirectedWaveAttackBehaviour>();
        if (legacyAttack != null)
        {
            attackPattern ??= new DirectedWaveAttackPatternConfiguration();
            attackPattern.behaviour = legacyAttack;
        }

        structuredConfigurationVersion = StructuredConfigurationVersion;
        return true;
    }

    private void EnsureBehaviourBlocks()
    {
        formation ??= new DirectedWaveFormationConfiguration();
        phaseEntry ??= new DirectedWavePhaseEntryConfiguration();
        postBehaviour ??= new DirectedWavePostBehaviourConfiguration();
    }

    private bool HasLegacyBehaviourConfiguration()
    {
        return legacyEnemyPrefab != null
            || legacyEnemyCount > 0
            || legacySpawnPoint != null
            || legacyPathCheckpoints?.Length > 0
            || legacyIndividualEntrancePoints?.Length > 0
            || legacyCustomFormationPoints?.Length > 0
            || legacyCustomFormationEnemyOverrides?.Length > 0
            || legacyProceduralFormationEnemyOverrides?.Length > 0
            || legacyFormationPointsRoot != null
            || legacyPostCommands?.Length > 0
            || legacyPatrolPoints?.Length > 0
            || legacyFormationMorphSteps?.Length > 0;
    }
}
