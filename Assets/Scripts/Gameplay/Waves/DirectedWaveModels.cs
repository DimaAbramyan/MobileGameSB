using UnityEngine;

public enum DirectedWaveSegmentMotion
{
    Linear,
    Bezier,
    CatmullRom
}

public enum DirectedWaveCoordinateSpace
{
    World,
    LocalToSubWave,
    LocalToSpawnPoint
}

public enum DirectedWaveEntranceMode
{
    Checkpoints,
    IndividualPoints
}

public enum DirectedWaveEntranceCompletionMode
{
    MoveToFormation,
    LoopEntrancePath
}

public enum DirectedWaveFormationLayout
{
    HorizontalLine,
    VerticalLine,
    Grid,
    VShape,
    Arc,
    Circle,
    Triangle,
    Square,
    Diamond,
    CustomPoints,
    TransformPoints
}

public enum DirectedWaveWobblePhaseMode
{
    SpawnOrder,
    Directional
}

public enum DirectedWavePostCommandType
{
    Patrol = 0,
    LocalMovement = 1,
    Wobble = 2,
    // Keeps the numeric value of the removed Attack command so existing wave data stays valid.
    LegacyAttack = 3,
    CircularMovement = 4,
    FormationRotation = 5,
    FormationMorph = 6,
    Wait = 7,
    Parallel = 8,
    Loop = 9,
    FormationReorder = 10
}

public enum DirectedWaveFormationReorderMode
{
    Mirror = 0,
    [InspectorName("Default")]
    Identity = 1,
    Random = 2
}

public enum DirectedWaveParallelExecutionMode
{
    Blocking,
    Background
}

public enum DirectedWavePostCommandCompletionMode
{
    Timed,
    CompleteRoute,
    Infinite
}

public enum DirectedWaveSpawnOrderMode
{
    Manual,
    DirectionAngle,
    CenterToOutside,
    OutsideToCenter,
    Clockwise,
    CounterClockwise
}

[System.Serializable]
public sealed class DirectedWavePathCheckpoint
{
    public Vector3 position;
    [Min(0.01f)] public float durationToNext = 0.5f;
    [Min(0.01f)] public float speedToNext = 1f;
    public DirectedWaveSegmentMotion motionToNext =
        DirectedWaveSegmentMotion.CatmullRom;
    public AnimationCurve easeToNext =
        AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
}

[System.Serializable]
public sealed class DirectedWaveIndividualEntrancePoint
{
    public Vector3 position;
}

[System.Serializable]
public sealed class DirectedWavePatrolPoint
{
    public Vector3 offset;
    [Min(0f)] public float wait;
    [Min(0.01f)] public float durationToNext = 0.5f;
    [Min(0.01f)] public float speedToNext = 1f;
    public DirectedWaveSegmentMotion motionToNext =
        DirectedWaveSegmentMotion.Linear;
    public AnimationCurve easeToNext =
        AnimationCurve.Linear(0f, 0f, 1f, 1f);
}

[System.Serializable]
public sealed class DirectedWavePostCommand
{
    public DirectedWavePostCommandType type = DirectedWavePostCommandType.Wobble;
    public bool enabled = true;
    public DirectedWavePostCommandCompletionMode completionMode =
        DirectedWavePostCommandCompletionMode.Timed;
    [Min(0.01f)] public float duration = 1f;
    [Min(0f)] public float holdDuration;
    public DirectedWaveParallelExecutionMode parallelExecutionMode =
        DirectedWaveParallelExecutionMode.Blocking;
    public bool infiniteParallel;
    [Min(1)] public int loopCount = 1;
    public bool infiniteLoop;
    public Vector3 targetOffset;
    public DirectedWaveCoordinateSpace targetOffsetCoordinateSpace =
        DirectedWaveCoordinateSpace.World;
    public float rotationDegrees = 45f;
    public bool continuousFormationRotation;
    public AnimationCurve curve =
        AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    public DirectedWaveFormationReorderMode formationReorderMode =
        DirectedWaveFormationReorderMode.Mirror;
    public bool formationReorderUseTargetCenter;
    public Vector3 formationReorderTargetCenter;
    [Min(0.01f)] public float formationReorderSpeed = 5f;
    [Min(0f)] public float formationReorderStartInterval = 0.1f;
    [Min(1)] public int formationReorderShipsPerBatch = 1;
    public int formationReorderRandomSeed = 12345;
    public DirectedWaveFormationMorphStep morphTarget =
        new DirectedWaveFormationMorphStep();
    public DirectedWavePostCommand[] parallelCommands;
    public DirectedWavePostCommand[] loopCommands;
}

[System.Serializable]
public sealed class DirectedWaveFormationMorphStep
{
    public DirectedWaveFormationLayout layout = DirectedWaveFormationLayout.Circle;
    public Vector3 centerOffset;
    [Min(1)] public int columns = 5;
    [Min(1)] public int rows = 3;
    [Min(0f)] public float arcRadius = 2f;
    public float arcDegrees = 120f;
    [Min(0f)] public float shapeRadius = 2f;
    public Vector2 shapeFlattening = Vector2.one;
    public Vector3[] customPoints = System.Array.Empty<Vector3>();
    [Min(0.01f)] public float durationToShape = 1f;
    [Min(0f)] public float holdDuration;
    public AnimationCurve easeToShape =
        AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
}
