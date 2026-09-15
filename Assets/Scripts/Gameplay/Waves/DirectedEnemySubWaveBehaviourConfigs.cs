using System;
using UnityEngine;

[Serializable]
public sealed class DirectedWaveFormationConfiguration
{
    [Header("Spawn")]
    [SerializeField] internal Enemy enemyPrefab;
    [SerializeField, Min(1)] internal int enemyCount = 1;
    [SerializeField, Min(0f)] internal float spawnInterval = 0.2f;
    [SerializeField] internal DirectedWaveSpawnOrderMode spawnOrderMode =
        DirectedWaveSpawnOrderMode.Manual;
    [SerializeField] internal float spawnOrderAngle;
    [SerializeField] internal float spawnOrderStartAngle = 90f;
    [SerializeField] internal bool parentEnemiesToSubWave = true;

    [Header("Layout")]
    [SerializeField] internal bool formationFrozen;
    [SerializeField] internal DirectedWaveFormationLayout formationLayout =
        DirectedWaveFormationLayout.HorizontalLine;
    [SerializeField] internal DirectedWaveCoordinateSpace formationCoordinateSpace =
        DirectedWaveCoordinateSpace.LocalToSubWave;
    [SerializeField] internal Vector3 formationCenter = new(0f, 2.5f, 0f);
    [SerializeField] internal Vector2 spacing = new(0.75f, 0.75f);
    [SerializeField, Min(1)] internal int columns = 6;
    [SerializeField, Min(1)] internal int rows = 2;
    [SerializeField] internal bool[] gridMatrixCells;
    [SerializeField, Min(0f)] internal float arcRadius = 2f;
    [SerializeField] internal float arcDegrees = 120f;
    [SerializeField, Min(1)] internal int shapePointCount = 8;
    [SerializeField, Min(0f)] internal float shapeRadius = 2f;
    [SerializeField] internal Vector2 shapeFlattening = Vector2.one;
    [SerializeField] internal Vector3[] customFormationPoints;
    [SerializeField] internal Enemy[] customFormationEnemyOverrides;
    [SerializeField] internal Enemy[] proceduralFormationEnemyOverrides =
        Array.Empty<Enemy>();
    [SerializeField] internal Transform formationPointsRoot;
}

[Serializable]
public sealed class DirectedWavePhaseEntryConfiguration
{
    [SerializeField] internal bool enabled;
    [SerializeField] internal Transform spawnPoint;
    [SerializeField] internal DirectedWaveEntranceMode entranceMode =
        DirectedWaveEntranceMode.Checkpoints;
    [SerializeField] internal DirectedWaveCoordinateSpace pathCoordinateSpace =
        DirectedWaveCoordinateSpace.LocalToSubWave;
    [SerializeField, Tooltip(
        "Rotates each enemy so transform up follows its movement direction during Phase Entry.")]
    internal bool rotateEnemiesAlongEntrancePath;
    [SerializeField] internal DirectedWavePathCheckpoint[] pathCheckpoints =
        Array.Empty<DirectedWavePathCheckpoint>();
    [SerializeField] internal DirectedWaveIndividualEntrancePoint[]
        individualEntrancePoints =
            Array.Empty<DirectedWaveIndividualEntrancePoint>();
    [SerializeField, Min(0f)] internal float individualPointMovementStartDelay =
        0.1f;
    [SerializeField, Min(0f)] internal float individualPointMovementDuration =
        0.35f;
    [SerializeField] internal AnimationCurve individualPointMovementCurve =
        AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [SerializeField, HideInInspector] internal Vector3 individualEntranceShapeCenter =
        new(0f, 5f, 0f);
    [SerializeField, HideInInspector, Min(0f)]
    internal float individualEntranceShapeRadius = 2f;
    [SerializeField, HideInInspector] internal Vector2
        individualEntranceShapeFlattening = Vector2.one;
    [SerializeField, HideInInspector] internal float
        individualEntranceShapeRotationDegrees;

    [Header("Completion")]
    [SerializeField] internal DirectedWaveEntranceCompletionMode
        entranceCompletionMode =
            DirectedWaveEntranceCompletionMode.MoveToFormation;
    [SerializeField, Min(0)] internal int entranceLoopStartCheckpointIndex;
    [SerializeField] internal bool entranceLoopTeleportToStart;
    [SerializeField, Min(0f)] internal float entranceLoopTeleportDelay;
    [SerializeField, Min(0f)] internal float settleDuration = 0.35f;
    [SerializeField] internal AnimationCurve settleCurve =
        AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
}

[Serializable]
public sealed class DirectedWavePostBehaviourConfiguration
{
    [SerializeField] internal bool enabled;
    [SerializeField] internal DirectedWavePostCommand[] postCommands =
        Array.Empty<DirectedWavePostCommand>();
    [SerializeField, Min(0f)] internal float postStartDelay = 0.25f;
    [SerializeField, Min(1)] internal int postCommandPipelineFixedCount = 1;
    [SerializeField] internal bool postCommandPipelineLoop;
    [SerializeField] internal Vector3 localMovementOffset = new(0.5f, 0f, 0f);
    [SerializeField, Min(0.01f)] internal float localMovementDuration = 1f;
    [SerializeField] internal bool localMovementLoop = true;
    [SerializeField] internal bool localMovementPingPong = true;
    [SerializeField] internal AnimationCurve localMovementCurve =
        AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [SerializeField] internal Vector2 wobbleAmplitude = new(0.25f, 0.1f);
    [SerializeField, Min(0f)] internal float wobbleFrequency = 1.5f;
    [SerializeField] internal DirectedWaveWobblePhaseMode wobblePhaseMode =
        DirectedWaveWobblePhaseMode.SpawnOrder;
    [SerializeField] internal float wobblePhaseOffset = 0.7f;
    [SerializeField] internal float wobbleDirectionAngle;
    [SerializeField, Min(0.01f)] internal float wobbleDirectionStep = 0.75f;
    [SerializeField] internal bool patrolLoop = true;
    [SerializeField] internal DirectedWaveCoordinateSpace patrolCoordinateSpace =
        DirectedWaveCoordinateSpace.World;
    [SerializeField] internal DirectedWavePatrolPoint[] patrolPoints =
        Array.Empty<DirectedWavePatrolPoint>();
    [SerializeField] internal Vector2 selfOrbitRadius = new(0.25f, 0.25f);
    [SerializeField] internal float selfOrbitPhaseOffset = 0.35f;
    [SerializeField] internal float selfRotationDegreesPerSecond = 90f;
    [SerializeField] internal float formationRotationDegreesPerSecond = 45f;
    [SerializeField] internal bool formationMorphLoop = true;
    [SerializeField, Min(0.01f)] internal float
        formationMorphReturnDuration = 1f;
    [SerializeField] internal AnimationCurve formationMorphReturnCurve =
        AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [SerializeField] internal DirectedWaveFormationMorphStep[] formationMorphSteps =
        Array.Empty<DirectedWaveFormationMorphStep>();
}

[Serializable]
public sealed class DirectedWaveAttackPatternConfiguration
{
    [SerializeField] internal DirectedWaveAttackBehaviour behaviour;
}

[Serializable]
public sealed class DirectedWaveCompletionConfiguration
{
    [SerializeField] internal bool enabled;

    // The current directed-wave completion contract is intentionally preserved:
    // a subwave finishes when all spawned enemies are gone.
    [SerializeField] internal bool completeWhenAllEnemiesAreDefeated = true;
}
