using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Zenject;

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
    Patrol,
    LocalMovement,
    Wobble,
    Attack,
    CircularMovement,
    FormationRotation,
    FormationMorph,
    Wait,
    Parallel,
    Loop
}

public enum DirectedWaveParallelExecutionMode
{
    Blocking,
    Background
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
public sealed class DirectedWavePatrolPoint
{
    public Vector3 offset;
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
    [Min(0.01f)] public float duration = 1f;
    [Min(0f)] public float holdDuration;
    public DirectedWaveParallelExecutionMode parallelExecutionMode =
        DirectedWaveParallelExecutionMode.Blocking;
    public bool infiniteParallel;
    [Min(1)] public int loopCount = 1;
    public bool infiniteLoop;
    public Vector3 targetOffset;
    public float rotationDegrees = 45f;
    public bool continuousFormationRotation;
    public AnimationCurve curve =
        AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
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

public sealed class DirectedEnemySubWave : InfoAboutSubWave
{
    [Inject] private DiContainer container;
    [Inject] private EnemyManager enemyManager;
    [Inject] private PlayerController playerController;

    [Header("Spawn")]
    [SerializeField] private Enemy enemyPrefab;
    [SerializeField, Min(1)] private int enemyCount = 1;
    [SerializeField, Min(0f)] private float spawnInterval = 0.2f;
    [SerializeField] private DirectedWaveSpawnOrderMode spawnOrderMode =
        DirectedWaveSpawnOrderMode.Manual;
    [SerializeField] private float spawnOrderAngle;
    [SerializeField] private float spawnOrderStartAngle = 90f;
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private bool parentEnemiesToSubWave = true;
    [SerializeField] private bool enableDebugLogs = true;

    [Header("Entrance path")]
    [SerializeField] private DirectedWaveCoordinateSpace pathCoordinateSpace =
        DirectedWaveCoordinateSpace.LocalToSubWave;
    [SerializeField] private DirectedWavePathCheckpoint[] pathCheckpoints =
        System.Array.Empty<DirectedWavePathCheckpoint>();

    [Header("Formation")]
    [SerializeField] private bool formationFrozen;
    [SerializeField] private DirectedWaveFormationLayout formationLayout =
        DirectedWaveFormationLayout.HorizontalLine;
    [SerializeField] private DirectedWaveCoordinateSpace formationCoordinateSpace =
        DirectedWaveCoordinateSpace.LocalToSubWave;
    [SerializeField] private Vector3 formationCenter = new Vector3(0f, 2.5f, 0f);
    [SerializeField] private Vector2 spacing = new Vector2(0.75f, 0.75f);
    [SerializeField, Min(1)] private int columns = 6;
    [SerializeField, Min(1)] private int rows = 2;
    [SerializeField, HideInInspector] private bool[] gridMatrixCells;
    [SerializeField, Min(0f)] private float arcRadius = 2f;
    [SerializeField] private float arcDegrees = 120f;
    [SerializeField, Min(1)] private int shapePointCount = 8;
    [SerializeField, Min(0f)] private float shapeRadius = 2f;
    [SerializeField] private Vector2 shapeFlattening = Vector2.one;
    [SerializeField] private Vector3[] customFormationPoints;
    [SerializeField] private Enemy[] customFormationEnemyOverrides;
    [SerializeField] private Transform formationPointsRoot;
    [SerializeField, Min(0f)] private float settleDuration = 0.35f;
    [SerializeField] private AnimationCurve settleCurve =
        AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Post behavior")]
    [SerializeField] private DirectedWavePostCommand[] postCommands =
        System.Array.Empty<DirectedWavePostCommand>();
    [SerializeField, Min(0f)] private float postStartDelay = 0.25f;
    [SerializeField] private bool postCommandPipelineLoop;
    [SerializeField] private Vector3 localMovementOffset = new Vector3(0.5f, 0f, 0f);
    [SerializeField, Min(0.01f)] private float localMovementDuration = 1f;
    [SerializeField] private bool localMovementLoop = true;
    [SerializeField] private bool localMovementPingPong = true;
    [SerializeField] private AnimationCurve localMovementCurve =
        AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [SerializeField] private Vector2 wobbleAmplitude = new Vector2(0.25f, 0.1f);
    [SerializeField, Min(0f)] private float wobbleFrequency = 1.5f;
    [SerializeField] private DirectedWaveWobblePhaseMode wobblePhaseMode =
        DirectedWaveWobblePhaseMode.SpawnOrder;
    [SerializeField] private float wobblePhaseOffset = 0.7f;
    [SerializeField] private float wobbleDirectionAngle;
    [SerializeField, Min(0.01f)] private float wobbleDirectionStep = 0.75f;
    [SerializeField, Min(0f)] private float diveInterval = 1.2f;
    [SerializeField, Min(0.01f)] private float diveDuration = 0.75f;
    [SerializeField, Min(0f)] private float diveReturnDuration = 0.65f;
    [SerializeField, Min(0f)] private float diveOvershootDistance = 1.5f;
    [SerializeField] private AnimationCurve diveCurve =
        AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [SerializeField] private AnimationCurve diveReturnCurve =
        AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [SerializeField] private bool patrolLoop = true;
    [SerializeField] private DirectedWavePatrolPoint[] patrolPoints =
        System.Array.Empty<DirectedWavePatrolPoint>();
    [SerializeField] private Vector2 selfOrbitRadius = new Vector2(0.25f, 0.25f);
    [SerializeField] private float selfOrbitPhaseOffset = 0.35f;
    [SerializeField] private float selfRotationDegreesPerSecond = 90f;
    [SerializeField] private float formationRotationDegreesPerSecond = 45f;
    [SerializeField] private bool formationMorphLoop = true;
    [SerializeField, Min(0.01f)] private float formationMorphReturnDuration = 1f;
    [SerializeField] private AnimationCurve formationMorphReturnCurve =
        AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [SerializeField] private DirectedWaveFormationMorphStep[] formationMorphSteps =
        System.Array.Empty<DirectedWaveFormationMorphStep>();

    private readonly HashSet<Enemy> aliveEnemies = new();
    private readonly Dictionary<Enemy, Vector3> formationPositions = new();
    private readonly Dictionary<Enemy, int> formationIndices = new();
    private readonly Dictionary<int, Vector3> formationPositionsByIndex = new();
    private readonly List<FormationMorphRuntimeSegment> formationMorphSegments = new();
    private readonly List<Coroutine> movementRoutines = new();
    private readonly List<ActiveBackgroundParallelCommand> activeBackgroundParallels =
        new();
    private Coroutine spawnRoutine;
    private Coroutine postBehaviorRoutine;
    private int lastBackgroundParallelFrame = -1;
    private int movingToFormationCount;
    private bool spawnFinished;
    private bool activated;
    private bool postBehaviorStarted;

    private sealed class ActiveBackgroundParallelCommand
    {
        public DirectedWavePostCommand command;
        public float elapsed;
    }

    protected override void Awake()
    {
    }

    protected override void OnDestroy()
    {
        if (enemyManager != null)
            enemyManager.OnEnemyDestroyed -= HandleEnemyDestroyed;

        if (spawnRoutine != null)
            StopCoroutine(spawnRoutine);

        if (postBehaviorRoutine != null)
            StopCoroutine(postBehaviorRoutine);

        for (int i = 0; i < movementRoutines.Count; i++)
        {
            if (movementRoutines[i] != null)
                StopCoroutine(movementRoutines[i]);
        }

        movementRoutines.Clear();
        formationPositions.Clear();
        formationIndices.Clear();
        formationPositionsByIndex.Clear();
        formationMorphSegments.Clear();
        activeBackgroundParallels.Clear();
    }

    public override void ActivateSubWave()
    {
        gameObject.SetActive(true);

        if (activated)
        {
            LogWarning("ActivateSubWave was called again, but this subwave is already activated.");
            return;
        }

        activated = true;
        spawnFinished = false;
        postBehaviorStarted = false;
        movingToFormationCount = 0;
        aliveEnemies.Clear();
        formationPositions.Clear();
        formationIndices.Clear();
        formationPositionsByIndex.Clear();
        formationMorphSegments.Clear();
        activeBackgroundParallels.Clear();
        lastBackgroundParallelFrame = -1;

        Log(
            $"Activated. EnemyPrefab={(enemyPrefab != null ? enemyPrefab.name : "NULL")}, " +
            $"PointOverrides={GetPointEnemyOverrideCount()}, " +
            $"Layout={formationLayout}, EffectiveEnemyCount={GetEffectiveEnemyCount()}, " +
            $"PostCommands={GetPostCommandSummary()}, " +
            $"Checkpoints={(pathCheckpoints != null ? pathCheckpoints.Length : 0)}, " +
            $"SpawnPoint={(spawnPoint != null ? spawnPoint.name : "NULL")}");

        if (container == null)
            LogWarning("DiContainer was not injected. Falling back to Unity Instantiate.");

        if (enemyManager == null)
            LogWarning("EnemyManager was not injected. Subwave can spawn enemies, but completion depends on null/dead cleanup only.");

        if (playerController == null
            && HasPostCommand(DirectedWavePostCommandType.Attack))
        {
            LogWarning("PlayerController was not injected. Attack post command will not know where the player is.");
        }

        if (enemyManager != null)
            enemyManager.OnEnemyDestroyed += HandleEnemyDestroyed;

        spawnRoutine = StartCoroutine(SpawnRoutine());
    }

    private IEnumerator SpawnRoutine()
    {
        if (!HasAnyEnemyPrefabConfigured())
        {
            LogError("No Enemy Prefab configured. Set global Enemy Prefab or point Enemy Override.");
            FinishSpawning();
            yield break;
        }

        int effectiveEnemyCount = GetEffectiveEnemyCount();
        if (effectiveEnemyCount <= 0)
        {
            LogWarning(
                "Effective enemy count is 0. " +
                "If Formation Layout is Free/TransformPoints, check Formation Points Root and Slot children.");
            FinishSpawning();
            yield break;
        }

        int[] spawnOrder = BuildSpawnOrder(effectiveEnemyCount);
        LogSpawnPlan(spawnOrder);
        Log($"Starting spawn routine. Count={effectiveEnemyCount}, Interval={spawnInterval:0.###}");
        for (int i = 0; i < effectiveEnemyCount; i++)
        {
            SpawnEnemy(spawnOrder[i], i);

            if (spawnInterval > 0f && i < effectiveEnemyCount - 1)
                yield return new WaitForSeconds(spawnInterval);
        }

        FinishSpawning();
    }

    private void SpawnEnemy(int formationIndex, int spawnStep)
    {
        Enemy prefabToSpawn = GetEnemyPrefabForIndex(formationIndex);
        if (prefabToSpawn == null)
        {
            LogError(
                $"No enemy prefab resolved for formation index {formationIndex}. " +
                "Set global Enemy Prefab or Enemy Override for this point.");
            return;
        }

        Vector3 spawnPosition = GetSpawnPosition();
        Transform parent = parentEnemiesToSubWave ? transform : null;

        Log(
            $"Spawning enemy {spawnStep + 1}/{GetEffectiveEnemyCount()} " +
            $"formationIndex={formationIndex} " +
            $"prefab={prefabToSpawn.name} at {spawnPosition}. " +
            $"Parent={(parent != null ? parent.name : "NULL")}");

        Enemy enemy = InstantiateEnemyPrefab(
            prefabToSpawn,
            spawnPosition,
            parent);

        if (enemy == null)
        {
            LogError(
                $"Instantiate returned null Enemy for prefab {prefabToSpawn.name}. " +
                "Check that Enemy Prefab has an Enemy component on its root.");
            return;
        }

        aliveEnemies.Add(enemy);
        Log($"Spawned enemy instance: {enemy.name}. AliveEnemies={aliveEnemies.Count}", enemy);

        movingToFormationCount++;
        Coroutine routine = StartCoroutine(
            MoveEnemyToFormation(enemy, formationIndex));
        movementRoutines.Add(routine);
    }

    private int[] BuildSpawnOrder(int count)
    {
        count = Mathf.Max(0, count);
        int[] order = new int[count];
        for (int i = 0; i < count; i++)
            order[i] = i;

        if (count <= 1 || spawnOrderMode == DirectedWaveSpawnOrderMode.Manual)
            return order;

        Vector3[] positions = new Vector3[count];
        Vector3 center = Vector3.zero;
        for (int i = 0; i < count; i++)
        {
            positions[i] = GetFormationPosition(i);
            center += positions[i];
        }

        center /= count;
        System.Array.Sort(
            order,
            (left, right) => CompareSpawnOrderIndices(
                left,
                right,
                positions,
                center));

        return order;
    }

    private int CompareSpawnOrderIndices(
        int left,
        int right,
        Vector3[] positions,
        Vector3 center)
    {
        int result = spawnOrderMode switch
        {
            DirectedWaveSpawnOrderMode.DirectionAngle =>
                CompareByDirectionProjection(
                    positions[left],
                    positions[right]),
            DirectedWaveSpawnOrderMode.CenterToOutside =>
                CompareByDistanceFromCenter(
                    positions[left],
                    positions[right],
                    center,
                    false),
            DirectedWaveSpawnOrderMode.OutsideToCenter =>
                CompareByDistanceFromCenter(
                    positions[left],
                    positions[right],
                    center,
                    true),
            DirectedWaveSpawnOrderMode.Clockwise =>
                CompareByAngleAroundCenter(
                    positions[left],
                    positions[right],
                    center,
                    true),
            DirectedWaveSpawnOrderMode.CounterClockwise =>
                CompareByAngleAroundCenter(
                    positions[left],
                    positions[right],
                    center,
                    false),
            _ => left.CompareTo(right)
        };

        return result != 0 ? result : left.CompareTo(right);
    }

    private int CompareByDirectionProjection(Vector3 left, Vector3 right)
    {
        Vector2 direction = GetSpawnOrderDirection(spawnOrderAngle);
        float leftProjection = Vector2.Dot(left, direction);
        float rightProjection = Vector2.Dot(right, direction);
        return leftProjection.CompareTo(rightProjection);
    }

    private int CompareByDistanceFromCenter(
        Vector3 left,
        Vector3 right,
        Vector3 center,
        bool outsideFirst)
    {
        float leftDistance = ((Vector2)(left - center)).sqrMagnitude;
        float rightDistance = ((Vector2)(right - center)).sqrMagnitude;
        int result = leftDistance.CompareTo(rightDistance);
        return outsideFirst ? -result : result;
    }

    private int CompareByAngleAroundCenter(
        Vector3 left,
        Vector3 right,
        Vector3 center,
        bool clockwise)
    {
        float leftAngle = GetNormalizedSpawnOrderAngle(left - center);
        float rightAngle = GetNormalizedSpawnOrderAngle(right - center);
        int result = leftAngle.CompareTo(rightAngle);
        return clockwise ? result : -result;
    }

    private float GetNormalizedSpawnOrderAngle(Vector3 offset)
    {
        float angle = Mathf.Atan2(offset.y, offset.x) * Mathf.Rad2Deg;
        float delta = Mathf.DeltaAngle(spawnOrderStartAngle, angle);
        return Mathf.Repeat(-delta, 360f);
    }

    private static Vector2 GetSpawnOrderDirection(float angleDegrees)
    {
        float radians = angleDegrees * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
    }

    private Enemy InstantiateEnemyPrefab(
        Enemy prefabToSpawn,
        Vector3 spawnPosition,
        Transform parent)
    {
        GameObject instance = container != null
            ? container.InstantiatePrefab(
                prefabToSpawn.gameObject,
                spawnPosition,
                prefabToSpawn.transform.rotation,
                parent)
            : Instantiate(
                prefabToSpawn.gameObject,
                spawnPosition,
                prefabToSpawn.transform.rotation,
                parent);

        if (instance == null)
            return null;

        return instance.GetComponent<Enemy>();
    }

    private IEnumerator MoveEnemyToFormation(Enemy enemy, int index)
    {
        if (enemy == null)
            yield break;

        Transform enemyTransform = enemy.transform;
        Rigidbody2D body = enemy.GetComponent<Rigidbody2D>();
        DirectedWaveRuntimeCheckpoint[] checkpoints =
            GetWorldPathCheckpoints();
        Vector3 formationPosition = GetFormationPosition(index);

        Log(
            $"Moving enemy {enemy.name}. Index={index}, " +
            $"PathCheckpoints={checkpoints.Length}, FormationPosition={formationPosition}",
            enemy);

        if (checkpoints.Length > 0)
        {
            SetEnemyPosition(enemyTransform, body, checkpoints[0].position);

            if (checkpoints.Length > 1)
            {
                yield return MoveAlongCheckpoints(
                    enemyTransform,
                    body,
                    checkpoints);
            }
        }

        if (enemy != null && settleDuration > 0f)
        {
            Vector3 from = enemyTransform.position;
            yield return MoveBetween(
                enemyTransform,
                body,
                from,
                formationPosition,
                settleDuration,
                settleCurve);
        }
        else if (enemy != null)
        {
            SetEnemyPosition(enemyTransform, body, formationPosition);
        }

        if (enemy != null && !enemy.isDead)
        {
            formationPositions[enemy] = formationPosition;
            formationIndices[enemy] = index;
            formationPositionsByIndex[index] = formationPosition;
        }

        movingToFormationCount = Mathf.Max(0, movingToFormationCount - 1);
        TryStartPostBehavior();
    }

    private IEnumerator MoveAlongCheckpoints(
        Transform target,
        Rigidbody2D body,
        DirectedWaveRuntimeCheckpoint[] checkpoints)
    {
        for (int i = 0; i < checkpoints.Length - 1; i++)
        {
            float duration = Mathf.Max(0.01f, checkpoints[i].durationToNext);
            float elapsed = 0f;

            while (elapsed < duration && target != null)
            {
                elapsed += Time.deltaTime;
                float time = Mathf.Clamp01(elapsed / duration);
                float curvedTime = EvaluateCurve(
                    checkpoints[i].easeToNext,
                    time);
                Vector3 position = EvaluateCheckpointSegment(
                    checkpoints,
                    i,
                    curvedTime);
                SetEnemyPosition(target, body, position);
                yield return null;
            }

            if (target != null)
                SetEnemyPosition(target, body, checkpoints[i + 1].position);
        }
    }

    private IEnumerator MoveBetween(
        Transform target,
        Rigidbody2D body,
        Vector3 from,
        Vector3 to,
        float duration,
        AnimationCurve curve)
    {
        float elapsed = 0f;

        while (elapsed < duration && target != null)
        {
            elapsed += Time.deltaTime;
            float time = Mathf.Clamp01(elapsed / duration);
            float curvedTime = EvaluateCurve(curve, time);
            SetEnemyPosition(target, body, Vector3.LerpUnclamped(from, to, curvedTime));
            yield return null;
        }

        if (target != null)
            SetEnemyPosition(target, body, to);
    }

    private Vector3 GetSpawnPosition()
    {
        DirectedWaveRuntimeCheckpoint[] checkpoints =
            GetWorldPathCheckpoints();
        if (checkpoints.Length > 0)
            return checkpoints[0].position;

        if (spawnPoint != null)
            return spawnPoint.position;

        return transform.position;
    }

    private DirectedWaveRuntimeCheckpoint[] GetWorldPathCheckpoints()
    {
        if (pathCheckpoints == null || pathCheckpoints.Length == 0)
            return System.Array.Empty<DirectedWaveRuntimeCheckpoint>();

        List<DirectedWaveRuntimeCheckpoint> result =
            new List<DirectedWaveRuntimeCheckpoint>(pathCheckpoints.Length);
        for (int i = 0; i < pathCheckpoints.Length; i++)
        {
            DirectedWavePathCheckpoint checkpoint = pathCheckpoints[i];
            if (checkpoint == null)
                continue;

            result.Add(new DirectedWaveRuntimeCheckpoint
            {
                position = ToWorld(checkpoint.position, pathCoordinateSpace),
                durationToNext = checkpoint.durationToNext,
                motionToNext = checkpoint.motionToNext,
                easeToNext = checkpoint.easeToNext
            });
        }

        return result.ToArray();
    }

    private static Vector3 EvaluateCheckpointSegment(
        DirectedWaveRuntimeCheckpoint[] checkpoints,
        int segmentIndex,
        float time)
    {
        DirectedWaveRuntimeCheckpoint current = checkpoints[segmentIndex];
        DirectedWaveRuntimeCheckpoint next = checkpoints[segmentIndex + 1];

        return current.motionToNext switch
        {
            DirectedWaveSegmentMotion.Bezier =>
                EvaluateBezierSegment(checkpoints, segmentIndex, time),
            DirectedWaveSegmentMotion.CatmullRom =>
                EvaluateCatmullRomSegment(checkpoints, segmentIndex, time),
            _ => Vector3.LerpUnclamped(current.position, next.position, time)
        };
    }

    private static Vector3 EvaluateBezierSegment(
        DirectedWaveRuntimeCheckpoint[] checkpoints,
        int segmentIndex,
        float time)
    {
        Vector3 p0 = checkpoints[segmentIndex].position;
        Vector3 p3 = checkpoints[segmentIndex + 1].position;

        Vector3 previous = segmentIndex > 0
            ? checkpoints[segmentIndex - 1].position
            : p0;
        Vector3 following = segmentIndex + 2 < checkpoints.Length
            ? checkpoints[segmentIndex + 2].position
            : p3;

        Vector3 p1 = p0 + (p3 - previous) / 6f;
        Vector3 p2 = p3 - (following - p0) / 6f;
        float t = Mathf.Clamp01(time);
        float oneMinusT = 1f - t;

        return oneMinusT * oneMinusT * oneMinusT * p0
            + 3f * oneMinusT * oneMinusT * t * p1
            + 3f * oneMinusT * t * t * p2
            + t * t * t * p3;
    }

    private static Vector3 EvaluateCatmullRomSegment(
        DirectedWaveRuntimeCheckpoint[] checkpoints,
        int segmentIndex,
        float time)
    {
        int p1 = segmentIndex;
        int p0 = Mathf.Max(p1 - 1, 0);
        int p2 = Mathf.Min(p1 + 1, checkpoints.Length - 1);
        int p3 = Mathf.Min(p1 + 2, checkpoints.Length - 1);
        float t = Mathf.Clamp01(time);

        return 0.5f * (
            2f * checkpoints[p1].position
            + (-checkpoints[p0].position + checkpoints[p2].position) * t
            + (2f * checkpoints[p0].position - 5f * checkpoints[p1].position
                + 4f * checkpoints[p2].position - checkpoints[p3].position)
            * t * t
            + (-checkpoints[p0].position + 3f * checkpoints[p1].position
                - 3f * checkpoints[p2].position + checkpoints[p3].position)
            * t * t * t);
    }

    private Vector3 GetFormationPosition(int index)
    {
        if (formationFrozen)
            return GetTransformFormationPosition(index);

        Vector3 localPosition = formationLayout switch
        {
            DirectedWaveFormationLayout.VerticalLine =>
                GetVerticalLinePosition(index),
            DirectedWaveFormationLayout.Grid =>
                GetGridPosition(index),
            DirectedWaveFormationLayout.VShape =>
                GetVShapePosition(index),
            DirectedWaveFormationLayout.Arc =>
                GetArcPosition(index),
            DirectedWaveFormationLayout.Circle =>
                GetCirclePosition(index),
            DirectedWaveFormationLayout.Triangle =>
                GetPolygonPerimeterPosition(index, GetTriangleVertices()),
            DirectedWaveFormationLayout.Square =>
                GetPolygonPerimeterPosition(index, GetSquareVertices()),
            DirectedWaveFormationLayout.Diamond =>
                GetPolygonPerimeterPosition(index, GetDiamondVertices()),
            DirectedWaveFormationLayout.CustomPoints =>
                GetCustomFormationPosition(index),
            DirectedWaveFormationLayout.TransformPoints =>
                GetTransformFormationPosition(index),
            _ => GetHorizontalLinePosition(index)
        };

        if (formationLayout == DirectedWaveFormationLayout.TransformPoints)
            return localPosition;

        return ToWorld(localPosition, formationCoordinateSpace);
    }

    private Vector3 GetHorizontalLinePosition(int index)
    {
        int count = GetEffectiveEnemyCount();
        float offset = (count - 1) * spacing.x * 0.5f;
        return formationCenter + new Vector3(index * spacing.x - offset, 0f, 0f);
    }

    private Vector3 GetVerticalLinePosition(int index)
    {
        int count = GetEffectiveEnemyCount();
        float offset = (count - 1) * spacing.y * 0.5f;
        return formationCenter + new Vector3(0f, offset - index * spacing.y, 0f);
    }

    private Vector3 GetGridPosition(int index)
    {
        int safeColumns = Mathf.Max(1, columns);
        int safeRows = Mathf.Max(1, rows);
        int column = index % safeColumns;
        int row = Mathf.Min(index / safeColumns, safeRows - 1);
        int count = GetEffectiveEnemyCount();
        int usedRows = Mathf.Min(safeRows, Mathf.CeilToInt(count / (float)safeColumns));

        float xOffset = (safeColumns - 1) * spacing.x * 0.5f;
        float yOffset = (usedRows - 1) * spacing.y * 0.5f;

        return formationCenter
            + new Vector3(
                column * spacing.x - xOffset,
                yOffset - row * spacing.y,
                0f);
    }

    private Vector3 GetVShapePosition(int index)
    {
        if (index == 0)
            return formationCenter;

        int pairIndex = (index + 1) / 2;
        float side = index % 2 == 0 ? 1f : -1f;

        return formationCenter
            + new Vector3(
                side * pairIndex * spacing.x,
                -pairIndex * spacing.y,
                0f);
    }

    private Vector3 GetArcPosition(int index)
    {
        int count = GetEffectiveEnemyCount();
        if (count <= 1)
            return formationCenter + Vector3.up * arcRadius;

        float halfArc = arcDegrees * 0.5f;
        float angle = Mathf.Lerp(-halfArc, halfArc, index / (count - 1f));
        float radians = (90f + angle) * Mathf.Deg2Rad;

        return formationCenter
            + new Vector3(
                Mathf.Cos(radians) * arcRadius,
                Mathf.Sin(radians) * arcRadius,
                0f);
    }

    private Vector3 GetCirclePosition(int index)
    {
        int count = Mathf.Max(1, GetEffectiveEnemyCount());
        if (count <= 1)
            return formationCenter;

        float angle = 90f - 360f * index / count;
        float radians = angle * Mathf.Deg2Rad;
        Vector2 flattening = GetSafeShapeFlattening();

        return formationCenter
            + new Vector3(
                Mathf.Cos(radians) * shapeRadius * flattening.x,
                Mathf.Sin(radians) * shapeRadius * flattening.y,
                0f);
    }

    private Vector3 GetPolygonPerimeterPosition(
        int index,
        Vector3[] vertices)
    {
        int count = Mathf.Max(1, GetEffectiveEnemyCount());
        if (count <= 1 || vertices == null || vertices.Length == 0)
            return formationCenter;

        float totalLength = 0f;
        for (int i = 0; i < vertices.Length; i++)
        {
            totalLength += Vector3.Distance(
                vertices[i],
                vertices[(i + 1) % vertices.Length]);
        }

        if (totalLength <= 0.0001f)
            return vertices[0];

        float remaining = totalLength * index / count;
        for (int i = 0; i < vertices.Length; i++)
        {
            Vector3 from = vertices[i];
            Vector3 to = vertices[(i + 1) % vertices.Length];
            float edgeLength = Vector3.Distance(from, to);

            if (remaining <= edgeLength)
            {
                float time = edgeLength <= 0.0001f
                    ? 0f
                    : remaining / edgeLength;
                return Vector3.LerpUnclamped(from, to, time);
            }

            remaining -= edgeLength;
        }

        return vertices[0];
    }

    private Vector3[] GetTriangleVertices()
    {
        Vector2 flattening = GetSafeShapeFlattening();
        return new[]
        {
            GetShapePoint(90f, flattening),
            GetShapePoint(210f, flattening),
            GetShapePoint(330f, flattening)
        };
    }

    private Vector3[] GetSquareVertices()
    {
        Vector2 flattening = GetSafeShapeFlattening();
        float x = shapeRadius * flattening.x;
        float y = shapeRadius * flattening.y;
        return new[]
        {
            formationCenter + new Vector3(-x, y, 0f),
            formationCenter + new Vector3(x, y, 0f),
            formationCenter + new Vector3(x, -y, 0f),
            formationCenter + new Vector3(-x, -y, 0f)
        };
    }

    private Vector3[] GetDiamondVertices()
    {
        Vector2 flattening = GetSafeShapeFlattening();
        return new[]
        {
            formationCenter + Vector3.up * shapeRadius * flattening.y,
            formationCenter + Vector3.right * shapeRadius * flattening.x,
            formationCenter + Vector3.down * shapeRadius * flattening.y,
            formationCenter + Vector3.left * shapeRadius * flattening.x
        };
    }

    private Vector3 GetShapePoint(float angleDegrees, Vector2 flattening)
    {
        float radians = angleDegrees * Mathf.Deg2Rad;
        return formationCenter
            + new Vector3(
                Mathf.Cos(radians) * shapeRadius * flattening.x,
                Mathf.Sin(radians) * shapeRadius * flattening.y,
                0f);
    }

    private Vector2 GetSafeShapeFlattening()
    {
        return new Vector2(
            Mathf.Max(0.01f, shapeFlattening.x),
            Mathf.Max(0.01f, shapeFlattening.y));
    }

    private Vector3 GetCustomFormationPosition(int index)
    {
        if (customFormationPoints == null || customFormationPoints.Length == 0)
            return GetHorizontalLinePosition(index);

        if (index < customFormationPoints.Length)
            return customFormationPoints[index];

        return customFormationPoints[customFormationPoints.Length - 1];
    }

    private Vector3 GetTransformFormationPosition(int index)
    {
        if (formationPointsRoot == null || formationPointsRoot.childCount == 0)
            return ToWorld(GetHorizontalLinePosition(index), formationCoordinateSpace);

        int safeIndex = Mathf.Clamp(index, 0, formationPointsRoot.childCount - 1);
        return formationPointsRoot.GetChild(safeIndex).position;
    }

    private int GetEffectiveEnemyCount()
    {
        if (formationFrozen)
            return formationPointsRoot != null ? formationPointsRoot.childCount : 0;

        if (formationLayout == DirectedWaveFormationLayout.TransformPoints)
            return formationPointsRoot != null ? formationPointsRoot.childCount : 0;

        if (formationLayout == DirectedWaveFormationLayout.CustomPoints
            && customFormationPoints != null
            && customFormationPoints.Length > 0)
        {
            return customFormationPoints.Length;
        }

        if (UsesShapeFormation())
            return Mathf.Max(1, shapePointCount);

        return Mathf.Max(1, enemyCount);
    }

    private bool UsesShapeFormation()
    {
        return formationLayout == DirectedWaveFormationLayout.Circle
            || formationLayout == DirectedWaveFormationLayout.Triangle
            || formationLayout == DirectedWaveFormationLayout.Square
            || formationLayout == DirectedWaveFormationLayout.Diamond;
    }

    private bool HasAnyEnemyPrefabConfigured()
    {
        if (enemyPrefab != null)
            return true;

        if (HasPointEnemyOverridesConfigured())
            return true;

        return false;
    }

    private Enemy GetEnemyPrefabForIndex(int index)
    {
        Enemy pointOverride = GetPointEnemyOverrideForIndex(index);
        return pointOverride != null ? pointOverride : enemyPrefab;
    }

    private Enemy GetPointEnemyOverrideForIndex(int index)
    {
        if (formationFrozen)
            return GetTransformPointEnemyOverride(index);

        return formationLayout switch
        {
            DirectedWaveFormationLayout.TransformPoints =>
                GetTransformPointEnemyOverride(index),
            DirectedWaveFormationLayout.CustomPoints =>
                GetCustomFormationEnemyOverride(index),
            _ => null
        };
    }

    private Enemy GetTransformPointEnemyOverride(int index)
    {
        if (formationPointsRoot == null
            || index < 0
            || index >= formationPointsRoot.childCount)
        {
            return null;
        }

        DirectedWaveEnemyOverride enemyOverride =
            formationPointsRoot
                .GetChild(index)
                .GetComponent<DirectedWaveEnemyOverride>();

        return enemyOverride != null
            ? enemyOverride.EnemyPrefabOverride
            : null;
    }

    private Enemy GetCustomFormationEnemyOverride(int index)
    {
        if (customFormationEnemyOverrides == null
            || index < 0
            || index >= customFormationEnemyOverrides.Length)
        {
            return null;
        }

        return customFormationEnemyOverrides[index];
    }

    private bool HasPointEnemyOverridesConfigured()
    {
        if (HasCustomFormationEnemyOverrides())
            return true;

        if (formationPointsRoot == null)
            return false;

        for (int i = 0; i < formationPointsRoot.childCount; i++)
        {
            DirectedWaveEnemyOverride enemyOverride =
                formationPointsRoot
                    .GetChild(i)
                    .GetComponent<DirectedWaveEnemyOverride>();

            if (enemyOverride != null && enemyOverride.EnemyPrefabOverride != null)
                return true;
        }

        return false;
    }

    private int GetPointEnemyOverrideCount()
    {
        int count = 0;

        if (customFormationEnemyOverrides != null)
        {
            for (int i = 0; i < customFormationEnemyOverrides.Length; i++)
            {
                if (customFormationEnemyOverrides[i] != null)
                    count++;
            }
        }

        if (formationPointsRoot != null)
        {
            for (int i = 0; i < formationPointsRoot.childCount; i++)
            {
                DirectedWaveEnemyOverride enemyOverride =
                    formationPointsRoot
                        .GetChild(i)
                        .GetComponent<DirectedWaveEnemyOverride>();

                if (enemyOverride != null && enemyOverride.EnemyPrefabOverride != null)
                    count++;
            }
        }

        return count;
    }

    private bool HasCustomFormationEnemyOverrides()
    {
        if (customFormationEnemyOverrides == null)
            return false;

        for (int i = 0; i < customFormationEnemyOverrides.Length; i++)
        {
            if (customFormationEnemyOverrides[i] != null)
                return true;
        }

        return false;
    }

    private void LogSpawnPlan(int[] spawnOrder)
    {
        if (!enableDebugLogs || spawnOrder == null || spawnOrder.Length <= 0)
            return;

        List<string> ranges = new();
        Enemy currentPrefab = null;
        int rangeStart = 0;

        for (int i = 0; i < spawnOrder.Length; i++)
        {
            Enemy prefab = GetEnemyPrefabForIndex(spawnOrder[i]);
            if (i == 0)
            {
                currentPrefab = prefab;
                rangeStart = 0;
                continue;
            }

            if (prefab == currentPrefab)
                continue;

            ranges.Add(FormatSpawnRange(rangeStart, i - 1, currentPrefab));
            currentPrefab = prefab;
            rangeStart = i;
        }

        ranges.Add(FormatSpawnRange(rangeStart, spawnOrder.Length - 1, currentPrefab));

        Log(
            $"Spawn plan: {string.Join(", ", ranges)}. " +
            $"OrderMode={spawnOrderMode}. " +
            $"FormationOrder=[{string.Join(", ", spawnOrder)}]. " +
            "Priority: point Enemy Override > global Enemy Prefab.");
    }

    private string FormatSpawnRange(int start, int end, Enemy prefab)
    {
        string range = start == end ? $"{start}" : $"{start}-{end}";
        string prefabName = prefab != null ? prefab.name : "NULL";
        return $"{range}: {prefabName}";
    }

    private Vector3 ToWorld(
        Vector3 position,
        DirectedWaveCoordinateSpace coordinateSpace)
    {
        return coordinateSpace switch
        {
            DirectedWaveCoordinateSpace.LocalToSpawnPoint when spawnPoint != null =>
                spawnPoint.TransformPoint(position),
            DirectedWaveCoordinateSpace.LocalToSubWave =>
                transform.TransformPoint(position),
            _ => position
        };
    }

    private static float EvaluateCurve(AnimationCurve curve, float time)
    {
        return curve != null ? curve.Evaluate(time) : time;
    }

    private static void SetEnemyPosition(
        Transform target,
        Rigidbody2D body,
        Vector3 position)
    {
        if (target == null)
            return;

        if (body != null && body.simulated)
        {
            body.MovePosition(position);
            return;
        }

        target.position = position;
    }

    private void FinishSpawning()
    {
        spawnFinished = true;
        spawnRoutine = null;
        Log($"Finished spawning. AliveEnemies={aliveEnemies.Count}");
        TryStartPostBehavior();
        TryComplete();
    }

    private void HandleEnemyDestroyed(Enemy enemy)
    {
        if (enemy == null || !aliveEnemies.Remove(enemy))
            return;

        Log($"Enemy destroyed: {enemy.name}. AliveEnemies={aliveEnemies.Count}", enemy);
        TryComplete();
    }

    private void TryStartPostBehavior()
    {
        if (postBehaviorStarted
            || !spawnFinished
            || movingToFormationCount > 0)
        {
            return;
        }

        if (!HasAnyPostCommand())
            return;

        aliveEnemies.RemoveWhere(enemy => enemy == null || enemy.isDead);
        if (aliveEnemies.Count <= 0)
            return;

        postBehaviorStarted = true;
        postBehaviorRoutine = StartCoroutine(PostBehaviorRoutine());
        Log($"Started post commands: {GetPostCommandSummary()}");
    }

    private IEnumerator PostBehaviorRoutine()
    {
        if (postStartDelay > 0f)
            yield return new WaitForSeconds(postStartDelay);

        Dictionary<int, Vector3> currentPositions =
            new Dictionary<int, Vector3>(formationPositionsByIndex);
        DirectedWavePostCommand[] enabledCommands = GetEnabledPostCommands();
        if (enabledCommands.Length == 0)
            yield break;

        do
        {
            for (int i = 0; i < enabledCommands.Length; i++)
            {
                DirectedWavePostCommand command = enabledCommands[i];
                if (IsBackgroundParallel(command))
                {
                    StartBackgroundParallel(command);
                    ApplyPipelinePositions(currentPositions);
                }
                else
                {
                    yield return ExecutePostPipelineCommand(
                        command,
                        currentPositions);
                }

                aliveEnemies.RemoveWhere(enemy => enemy == null || enemy.isDead);
                if (aliveEnemies.Count <= 0)
                    yield break;
            }
        }
        while (postCommandPipelineLoop);

        while (aliveEnemies.Count > 0)
        {
            aliveEnemies.RemoveWhere(enemy => enemy == null || enemy.isDead);
            if (aliveEnemies.Count <= 0)
                yield break;

            ApplyPipelinePositions(currentPositions);
            yield return null;
        }
    }

    private DirectedWavePostCommand[] GetEnabledPostCommands()
    {
        return GetEnabledCommands(postCommands);
    }

    private DirectedWavePostCommand[] GetEnabledCommands(
        DirectedWavePostCommand[] commands)
    {
        if (commands == null || commands.Length == 0)
            return System.Array.Empty<DirectedWavePostCommand>();

        List<DirectedWavePostCommand> result = new();
        for (int i = 0; i < commands.Length; i++)
        {
            DirectedWavePostCommand command = commands[i];
            if (command != null && command.enabled)
                result.Add(command);
        }

        return result.ToArray();
    }

    private IEnumerator ExecutePostPipelineCommand(
        DirectedWavePostCommand command,
        Dictionary<int, Vector3> currentPositions)
    {
        if (command == null)
            yield break;

        switch (command.type)
        {
            case DirectedWavePostCommandType.Patrol:
                yield return ExecutePipelinePatrol(command, currentPositions);
                break;
            case DirectedWavePostCommandType.LocalMovement:
                yield return ExecutePipelineMove(command, currentPositions);
                break;
            case DirectedWavePostCommandType.Wobble:
                yield return ExecutePipelineOverlay(command, currentPositions, true, false);
                break;
            case DirectedWavePostCommandType.Attack:
                yield return ExecutePipelineAttack(command, currentPositions);
                break;
            case DirectedWavePostCommandType.CircularMovement:
                yield return ExecutePipelineOverlay(command, currentPositions, false, true);
                break;
            case DirectedWavePostCommandType.FormationRotation:
                yield return ExecutePipelineFormationRotation(command, currentPositions);
                break;
            case DirectedWavePostCommandType.FormationMorph:
                yield return ExecutePipelineFormationMorph(command, currentPositions);
                break;
            case DirectedWavePostCommandType.Wait:
                yield return ExecutePipelineWait(command, currentPositions);
                break;
            case DirectedWavePostCommandType.Parallel:
                yield return ExecutePipelineParallel(command, currentPositions);
                break;
            case DirectedWavePostCommandType.Loop:
                yield return ExecutePipelineLoop(command, currentPositions);
                break;
        }
    }

    private IEnumerator ExecutePipelineLoop(
        DirectedWavePostCommand command,
        Dictionary<int, Vector3> currentPositions)
    {
        if (command == null)
            yield break;

        DirectedWavePostCommand[] commands =
            GetEnabledCommands(command.loopCommands);
        if (commands.Length == 0)
            yield break;

        int iterations = Mathf.Max(1, command.loopCount);
        int completedIterations = 0;
        while (command.infiniteLoop || completedIterations < iterations)
        {
            for (int i = 0; i < commands.Length; i++)
            {
                DirectedWavePostCommand child = commands[i];
                if (child.type == DirectedWavePostCommandType.Loop)
                {
                    LogWarning("Nested Loop command was skipped. Loop inside Loop is disabled.");
                    continue;
                }

                if (IsBackgroundParallel(child))
                {
                    StartBackgroundParallel(child);
                    ApplyPipelinePositions(currentPositions);
                }
                else
                {
                    yield return ExecutePostPipelineCommand(
                        child,
                        currentPositions);
                }

                aliveEnemies.RemoveWhere(enemy => enemy == null || enemy.isDead);
                if (aliveEnemies.Count <= 0)
                    yield break;
            }

            completedIterations++;
        }

        yield return HoldPipelinePositions(currentPositions, command.holdDuration);
    }

    private IEnumerator ExecutePipelineWait(
        DirectedWavePostCommand command,
        Dictionary<int, Vector3> currentPositions)
    {
        yield return HoldPipelinePositions(
            currentPositions,
            Mathf.Max(0.01f, command.duration));
        yield return HoldPipelinePositions(
            currentPositions,
            command.holdDuration);
    }

    private IEnumerator ExecutePipelineMove(
        DirectedWavePostCommand command,
        Dictionary<int, Vector3> currentPositions)
    {
        Dictionary<int, Vector3> from = new(currentPositions);
        Vector3 currentCenter = GetPositionsCenter(from);
        Vector3 targetCenter = GetStableFormationCenter() + command.targetOffset;
        Vector3 delta = targetCenter - currentCenter;
        Dictionary<int, Vector3> to = OffsetPositions(from, delta);

        yield return MovePipelinePositions(
            from,
            to,
            Mathf.Max(0.01f, command.duration),
            command.curve,
            currentPositions);
        yield return HoldPipelinePositions(
            currentPositions,
            command.holdDuration);
    }

    private IEnumerator ExecutePipelinePatrol(
        DirectedWavePostCommand command,
        Dictionary<int, Vector3> currentPositions)
    {
        Dictionary<int, Vector3> start = new(currentPositions);
        float duration = Mathf.Max(0.01f, command.duration);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float time = Mathf.Min(elapsed, duration);
            Dictionary<int, Vector3> frame = OffsetPositions(
                start,
                GetPatrolOffset(time));
            ApplyPipelinePositions(frame);
            yield return null;

            if (!HasAliveEnemies())
                yield break;
        }

        ReplacePositions(
            currentPositions,
            OffsetPositions(start, GetPatrolOffset(duration)));
        yield return HoldPipelinePositions(currentPositions, command.holdDuration);
    }

    private IEnumerator ExecutePipelineFormationMorph(
        DirectedWavePostCommand command,
        Dictionary<int, Vector3> currentPositions)
    {
        if (command != null && command.morphTarget != null)
        {
            Vector3 morphCenter = GetPositionsCenter(currentPositions);
            Dictionary<int, Vector3> target = BuildMorphTarget(
                currentPositions,
                command.morphTarget,
                morphCenter);
            yield return MovePipelinePositions(
                new Dictionary<int, Vector3>(currentPositions),
                target,
                Mathf.Max(0.01f, command.duration),
                command.curve,
                currentPositions);
            yield return HoldPipelinePositions(
                currentPositions,
                command.holdDuration);
            yield break;
        }

        if (formationMorphSteps == null || formationMorphSteps.Length == 0)
            yield break;

        Vector3 center = GetPositionsCenter(currentPositions);
        for (int i = 0; i < formationMorphSteps.Length; i++)
        {
            DirectedWaveFormationMorphStep step = formationMorphSteps[i];
            if (step == null)
                continue;

            Dictionary<int, Vector3> target = BuildMorphTarget(
                currentPositions,
                step,
                center);
            yield return MovePipelinePositions(
                new Dictionary<int, Vector3>(currentPositions),
                target,
                Mathf.Max(0.01f, step.durationToShape),
                step.easeToShape,
                currentPositions);
            yield return HoldPipelinePositions(
                currentPositions,
                step.holdDuration);
            center = GetPositionsCenter(currentPositions);
        }

        if (formationMorphLoop)
        {
            Dictionary<int, Vector3> target = new(formationPositionsByIndex);
            yield return MovePipelinePositions(
                new Dictionary<int, Vector3>(currentPositions),
                target,
                Mathf.Max(0.01f, formationMorphReturnDuration),
                formationMorphReturnCurve,
                currentPositions);
        }
    }

    private IEnumerator ExecutePipelineFormationRotation(
        DirectedWavePostCommand command,
        Dictionary<int, Vector3> currentPositions)
    {
        Dictionary<int, Vector3> start = new(currentPositions);
        Vector3 center = GetPositionsCenter(start);
        float duration = Mathf.Max(0.01f, command.duration);
        float totalAngle = Mathf.Abs(command.rotationDegrees) > 0.0001f
            ? command.continuousFormationRotation
                ? command.rotationDegrees * duration
                : command.rotationDegrees
            : duration * formationRotationDegreesPerSecond;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float normalized = Mathf.Clamp01(elapsed / duration);
            float curved = command.continuousFormationRotation
                ? normalized
                : EvaluateCurve(command.curve, normalized);
            Dictionary<int, Vector3> frame = RotatePositions(
                start,
                center,
                totalAngle * curved);
            ApplyPipelinePositions(frame);
            yield return null;

            if (!HasAliveEnemies())
                yield break;
        }

        ReplacePositions(currentPositions, RotatePositions(start, center, totalAngle));
        yield return HoldPipelinePositions(currentPositions, command.holdDuration);
    }

    private IEnumerator ExecutePipelineParallel(
        DirectedWavePostCommand command,
        Dictionary<int, Vector3> currentPositions)
    {
        Dictionary<int, Vector3> start = new(currentPositions);
        float duration = Mathf.Max(0.01f, command.duration);
        float elapsed = 0f;

        while (command.infiniteParallel || elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float time = command.infiniteParallel ? elapsed : Mathf.Min(elapsed, duration);
            float frameDuration = command.infiniteParallel
                ? Mathf.Max(0.01f, time)
                : duration;
            Dictionary<int, Vector3> frame = EvaluateParallelCommandFrame(
                command,
                start,
                time,
                frameDuration,
                false);
            ApplyPipelinePositions(frame);
            yield return null;

            if (!HasAliveEnemies())
                yield break;
        }

        ReplacePositions(
            currentPositions,
            EvaluateParallelCommandFrame(command, start, duration, duration, true));
        ApplyPipelinePositions(currentPositions);
        yield return HoldPipelinePositions(currentPositions, command.holdDuration);
    }

    private bool IsBackgroundParallel(DirectedWavePostCommand command)
    {
        return command != null
            && command.type == DirectedWavePostCommandType.Parallel
            && command.parallelExecutionMode
                == DirectedWaveParallelExecutionMode.Background;
    }

    private void StartBackgroundParallel(DirectedWavePostCommand command)
    {
        if (command == null)
            return;

        for (int i = 0; i < activeBackgroundParallels.Count; i++)
        {
            if (activeBackgroundParallels[i].command == command)
                return;
        }

        activeBackgroundParallels.Add(
            new ActiveBackgroundParallelCommand
            {
                command = command,
                elapsed = 0f
            });
    }

    private Dictionary<int, Vector3> EvaluateParallelCommandFrame(
        DirectedWavePostCommand parallelCommand,
        Dictionary<int, Vector3> start,
        float elapsed,
        float parallelDuration,
        bool finalFrame)
    {
        Dictionary<int, Vector3> frame = new(start);
        if (parallelCommand.parallelCommands == null)
            return frame;

        for (int i = 0; i < parallelCommand.parallelCommands.Length; i++)
        {
            DirectedWavePostCommand child = parallelCommand.parallelCommands[i];
            if (child == null || !child.enabled || child.type == DirectedWavePostCommandType.Parallel)
                continue;

            frame = EvaluatePostCommandFrame(
                child,
                frame,
                elapsed,
                parallelDuration,
                finalFrame);
        }

        return frame;
    }

    private Dictionary<int, Vector3> EvaluatePostCommandFrame(
        DirectedWavePostCommand command,
        Dictionary<int, Vector3> input,
        float elapsed,
        float parallelDuration,
        bool finalFrame)
    {
        float duration = Mathf.Max(0.01f, command.duration);
        float time = command.continuousFormationRotation
            || command.type == DirectedWavePostCommandType.Patrol
            || command.type == DirectedWavePostCommandType.Wobble
            || command.type == DirectedWavePostCommandType.CircularMovement
                ? Mathf.Min(elapsed, Mathf.Max(0.01f, parallelDuration))
                : Mathf.Min(elapsed, duration);

        switch (command.type)
        {
            case DirectedWavePostCommandType.LocalMovement:
            {
                Vector3 currentCenter = GetPositionsCenter(input);
                Vector3 targetCenter = GetStableFormationCenter() + command.targetOffset;
                Dictionary<int, Vector3> target =
                    OffsetPositions(input, targetCenter - currentCenter);
                float normalized = Mathf.Clamp01(time / duration);
                return LerpPositions(
                    input,
                    target,
                    EvaluateCurve(command.curve, normalized));
            }
            case DirectedWavePostCommandType.Patrol:
                return OffsetPositions(input, GetPatrolOffset(time));
            case DirectedWavePostCommandType.Wobble:
                return finalFrame
                    ? new Dictionary<int, Vector3>(input)
                    : ApplyOverlayFrame(input, true, false, time);
            case DirectedWavePostCommandType.CircularMovement:
                return finalFrame
                    ? new Dictionary<int, Vector3>(input)
                    : ApplyOverlayFrame(input, false, true, time);
            case DirectedWavePostCommandType.FormationRotation:
            {
                float totalAngle = GetFormationRotationAngle(command, time, duration);
                return RotatePositions(input, GetPositionsCenter(input), totalAngle);
            }
            case DirectedWavePostCommandType.FormationMorph:
            {
                if (command.morphTarget == null)
                    return new Dictionary<int, Vector3>(input);

                Dictionary<int, Vector3> target = BuildMorphTarget(
                    input,
                    command.morphTarget,
                    GetPositionsCenter(input));
                float normalized = Mathf.Clamp01(time / duration);
                return LerpPositions(
                    input,
                    target,
                    EvaluateCurve(command.curve, normalized));
            }
            default:
                return new Dictionary<int, Vector3>(input);
        }
    }

    private float GetFormationRotationAngle(
        DirectedWavePostCommand command,
        float elapsed,
        float duration)
    {
        if (command.continuousFormationRotation)
        {
            float degreesPerSecond = Mathf.Abs(command.rotationDegrees) > 0.0001f
                ? command.rotationDegrees
                : formationRotationDegreesPerSecond;
            return degreesPerSecond * elapsed;
        }

        float totalAngle = Mathf.Abs(command.rotationDegrees) > 0.0001f
            ? command.rotationDegrees
            : duration * formationRotationDegreesPerSecond;
        float normalized = Mathf.Clamp01(elapsed / duration);
        return totalAngle * EvaluateCurve(command.curve, normalized);
    }

    private Dictionary<int, Vector3> ApplyOverlayFrame(
        Dictionary<int, Vector3> input,
        bool includeWobble,
        bool includeCircularMovement,
        float elapsed)
    {
        Dictionary<int, Vector3> frame = new(input);
        float leadingProjection = includeWobble ? GetLeadingWobbleProjection(input) : 0f;
        foreach (int index in input.Keys)
        {
            Vector3 position = input[index];
            if (includeWobble)
                position += GetWobbleOffset(index, input[index], leadingProjection, elapsed);
            if (includeCircularMovement)
                position += GetSelfOrbitOffset(index, elapsed);

            frame[index] = position;
        }

        return frame;
    }

    private IEnumerator ExecutePipelineOverlay(
        DirectedWavePostCommand command,
        Dictionary<int, Vector3> currentPositions,
        bool includeWobble,
        bool includeCircularMovement)
    {
        float duration = Mathf.Max(0.01f, command.duration);
        float elapsed = 0f;
        float leadingProjection = includeWobble ? GetLeadingWobbleProjection(currentPositions) : 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            Dictionary<int, Vector3> frame = new(currentPositions);
            foreach (int index in currentPositions.Keys)
            {
                Vector3 position = currentPositions[index];
                if (includeWobble)
                    position += GetWobbleOffset(
                        index,
                        currentPositions[index],
                        leadingProjection,
                        elapsed);
                if (includeCircularMovement)
                    position += GetSelfOrbitOffset(index, elapsed);

                frame[index] = position;
            }

            ApplyPipelinePositions(frame);
            yield return null;

            if (!HasAliveEnemies())
                yield break;
        }

        ApplyPipelinePositions(currentPositions);
        yield return HoldPipelinePositions(currentPositions, command.holdDuration);
    }

    private IEnumerator ExecutePipelineAttack(
        DirectedWavePostCommand command,
        Dictionary<int, Vector3> currentPositions)
    {
        float endTime = Time.time + Mathf.Max(0.01f, command.duration);
        int cursor = 0;
        float nextAttackTime = Time.time;

        while (Time.time < endTime)
        {
            ApplyPipelinePositions(currentPositions);

            if (Time.time >= nextAttackTime)
            {
                Enemy enemy = GetNextAliveEnemy(ref cursor);
                if (enemy != null && playerController != null)
                {
                    yield return DiveEnemy(
                        enemy,
                        false,
                        false,
                        false,
                        false,
                        false,
                        false,
                        Time.time);
                }

                nextAttackTime = Time.time + Mathf.Max(0.1f, diveInterval);
            }

            yield return null;

            if (!HasAliveEnemies())
                yield break;
        }

        yield return HoldPipelinePositions(currentPositions, command.holdDuration);
    }

    private IEnumerator MovePipelinePositions(
        Dictionary<int, Vector3> from,
        Dictionary<int, Vector3> to,
        float duration,
        AnimationCurve curve,
        Dictionary<int, Vector3> currentPositions)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float normalized = Mathf.Clamp01(elapsed / duration);
            float curved = EvaluateCurve(curve, normalized);
            Dictionary<int, Vector3> frame = LerpPositions(from, to, curved);
            ApplyPipelinePositions(frame);
            yield return null;

            if (!HasAliveEnemies())
                yield break;
        }

        ReplacePositions(currentPositions, to);
        ApplyPipelinePositions(currentPositions);
    }

    private IEnumerator HoldPipelinePositions(
        Dictionary<int, Vector3> positions,
        float duration)
    {
        float endTime = Time.time + Mathf.Max(0f, duration);
        while (Time.time < endTime)
        {
            ApplyPipelinePositions(positions);
            yield return null;

            if (!HasAliveEnemies())
                yield break;
        }
    }

    private void ApplyPipelinePositions(Dictionary<int, Vector3> positions)
    {
        Dictionary<int, Vector3> visiblePositions =
            GetPositionsWithBackgroundParallels(positions);

        foreach (Enemy enemy in aliveEnemies)
        {
            if (enemy == null || enemy.isDead)
                continue;

            if (!formationIndices.TryGetValue(enemy, out int index))
                continue;

            if (!visiblePositions.TryGetValue(index, out Vector3 position))
                continue;

            Rigidbody2D body = enemy.GetComponent<Rigidbody2D>();
            SetEnemyPosition(enemy.transform, body, position);
        }
    }

    private Dictionary<int, Vector3> GetPositionsWithBackgroundParallels(
        Dictionary<int, Vector3> basePositions)
    {
        if (activeBackgroundParallels.Count == 0)
            return basePositions;

        UpdateBackgroundParallels();

        Dictionary<int, Vector3> frame = new(basePositions);
        for (int i = 0; i < activeBackgroundParallels.Count; i++)
        {
            ActiveBackgroundParallelCommand active = activeBackgroundParallels[i];
            if (active.command == null)
                continue;

            float duration = Mathf.Max(0.01f, active.command.duration);
            float time = active.command.infiniteParallel
                ? active.elapsed
                : Mathf.Min(active.elapsed, duration);
            float frameDuration = active.command.infiniteParallel
                ? Mathf.Max(0.01f, time)
                : duration;

            frame = EvaluateParallelCommandFrame(
                active.command,
                frame,
                time,
                frameDuration,
                false);
        }

        return frame;
    }

    private void UpdateBackgroundParallels()
    {
        if (lastBackgroundParallelFrame == Time.frameCount)
            return;

        lastBackgroundParallelFrame = Time.frameCount;
        float deltaTime = Time.deltaTime;
        for (int i = activeBackgroundParallels.Count - 1; i >= 0; i--)
        {
            ActiveBackgroundParallelCommand active = activeBackgroundParallels[i];
            if (active.command == null)
            {
                activeBackgroundParallels.RemoveAt(i);
                continue;
            }

            active.elapsed += deltaTime;
            if (!active.command.infiniteParallel
                && active.elapsed >= Mathf.Max(0.01f, active.command.duration))
            {
                activeBackgroundParallels.RemoveAt(i);
            }
        }
    }

    private bool HasAliveEnemies()
    {
        aliveEnemies.RemoveWhere(enemy => enemy == null || enemy.isDead);
        return aliveEnemies.Count > 0;
    }

    private static Dictionary<int, Vector3> OffsetPositions(
        Dictionary<int, Vector3> source,
        Vector3 offset)
    {
        Dictionary<int, Vector3> result = new(source.Count);
        foreach (KeyValuePair<int, Vector3> pair in source)
            result[pair.Key] = pair.Value + offset;

        return result;
    }

    private static Dictionary<int, Vector3> RotatePositions(
        Dictionary<int, Vector3> source,
        Vector3 center,
        float angleDegrees)
    {
        float radians = angleDegrees * Mathf.Deg2Rad;
        float sin = Mathf.Sin(radians);
        float cos = Mathf.Cos(radians);
        Dictionary<int, Vector3> result = new(source.Count);

        foreach (KeyValuePair<int, Vector3> pair in source)
        {
            Vector3 relative = pair.Value - center;
            result[pair.Key] = center + new Vector3(
                relative.x * cos - relative.y * sin,
                relative.x * sin + relative.y * cos,
                relative.z);
        }

        return result;
    }

    private static Dictionary<int, Vector3> LerpPositions(
        Dictionary<int, Vector3> from,
        Dictionary<int, Vector3> to,
        float time)
    {
        Dictionary<int, Vector3> result = new(from.Count);
        foreach (KeyValuePair<int, Vector3> pair in from)
        {
            Vector3 target = to.TryGetValue(pair.Key, out Vector3 value)
                ? value
                : pair.Value;
            result[pair.Key] = Vector3.LerpUnclamped(pair.Value, target, time);
        }

        return result;
    }

    private static void ReplacePositions(
        Dictionary<int, Vector3> target,
        Dictionary<int, Vector3> source)
    {
        target.Clear();
        foreach (KeyValuePair<int, Vector3> pair in source)
            target[pair.Key] = pair.Value;
    }

    private Vector3 GetPositionsCenter(Dictionary<int, Vector3> positions)
    {
        if (positions == null || positions.Count == 0)
            return GetStableFormationCenter();

        Vector3 center = Vector3.zero;
        foreach (Vector3 position in positions.Values)
            center += position;

        return center / positions.Count;
    }

    private void ApplyContinuousPostCommands(
        float time,
        Enemy excludedEnemy,
        bool includePatrol,
        bool includeLocalMove,
        bool includeWobble,
        bool includeSelfOrbit,
        bool includeFormationRotation,
        bool includeFormationMorph)
    {
        Vector3 patrolOffset = includePatrol ? GetPatrolOffset(time) : Vector3.zero;
        Vector3 localMovement = includeLocalMove
            ? GetLocalMovementOffset(time)
            : Vector3.zero;
        float leadingProjection = includeWobble
            ? GetLeadingWobbleProjection()
            : 0f;
        Vector3 formationRotationCenter = includeFormationRotation
            ? GetFormationRotationCenter(includeFormationMorph, time)
            : Vector3.zero;
        int fallbackIndex = 0;

        foreach (Enemy enemy in aliveEnemies)
        {
            if (enemy == null || enemy.isDead || enemy == excludedEnemy)
            {
                fallbackIndex++;
                continue;
            }

            if (!formationPositions.TryGetValue(enemy, out Vector3 basePosition))
            {
                fallbackIndex++;
                continue;
            }

            int formationIndex = formationIndices.TryGetValue(enemy, out int storedIndex)
                ? storedIndex
                : fallbackIndex;
            if (includeFormationMorph)
                basePosition = GetFormationMorphPosition(
                    formationIndex,
                    basePosition,
                    time);

            Vector3 position = basePosition + patrolOffset + localMovement;
            if (includeFormationRotation)
                position += GetFormationRotationOffset(
                    basePosition,
                    formationRotationCenter,
                    time);
            if (includeWobble)
                position += GetWobbleOffset(
                    formationIndex,
                    basePosition,
                    leadingProjection,
                    time);
            if (includeSelfOrbit)
                position += GetSelfOrbitOffset(formationIndex, time);

            Rigidbody2D body = enemy.GetComponent<Rigidbody2D>();
            SetEnemyPosition(enemy.transform, body, position);
            fallbackIndex++;
        }
    }

    private Vector3 GetFormationRotationCenter(bool includeFormationMorph, float time)
    {
        Vector3 center = Vector3.zero;
        int count = 0;

        foreach (KeyValuePair<int, Vector3> pair in formationPositionsByIndex)
        {
            Vector3 position = includeFormationMorph
                ? GetFormationMorphPosition(pair.Key, pair.Value, time)
                : pair.Value;
            center += position;
            count++;
        }

        return count > 0 ? center / count : transform.position;
    }

    private Vector3 GetFormationRotationOffset(
        Vector3 basePosition,
        Vector3 center,
        float time)
    {
        float angle = time * formationRotationDegreesPerSecond * Mathf.Deg2Rad;
        Vector3 relative = basePosition - center;
        float sin = Mathf.Sin(angle);
        float cos = Mathf.Cos(angle);
        Vector3 rotated = new Vector3(
            relative.x * cos - relative.y * sin,
            relative.x * sin + relative.y * cos,
            relative.z);

        return rotated - relative;
    }

    private void BuildFormationMorphSegments()
    {
        formationMorphSegments.Clear();

        if (formationMorphSteps == null
            || formationMorphSteps.Length == 0
            || formationPositionsByIndex.Count == 0)
        {
            return;
        }

        Dictionary<int, Vector3> initial = new(formationPositionsByIndex);
        Dictionary<int, Vector3> previous = new(initial);
        float startTime = 0f;
        Vector3 center = GetStableFormationCenter();

        for (int i = 0; i < formationMorphSteps.Length; i++)
        {
            DirectedWaveFormationMorphStep step = formationMorphSteps[i];
            if (step == null)
                continue;

            Dictionary<int, Vector3> target = BuildMorphTarget(
                previous,
                step,
                center);
            float duration = Mathf.Max(0.01f, step.durationToShape);
            formationMorphSegments.Add(new FormationMorphRuntimeSegment
            {
                from = previous,
                to = target,
                startTime = startTime,
                duration = duration,
                curve = step.easeToShape
            });

            startTime += duration + Mathf.Max(0f, step.holdDuration);
            previous = target;
        }

        if (formationMorphLoop && formationMorphSegments.Count > 0)
        {
            formationMorphSegments.Add(new FormationMorphRuntimeSegment
            {
                from = previous,
                to = initial,
                startTime = startTime,
                duration = Mathf.Max(0.01f, formationMorphReturnDuration),
                curve = formationMorphReturnCurve
            });
        }
    }

    private Dictionary<int, Vector3> BuildMorphTarget(
        Dictionary<int, Vector3> previous,
        DirectedWaveFormationMorphStep step,
        Vector3 center)
    {
        Vector3[] targetPositions = CreateMorphShapePositions(step, center);
        List<int> freeTargetIndices = new(targetPositions.Length);
        for (int i = 0; i < targetPositions.Length; i++)
            freeTargetIndices.Add(i);

        Dictionary<int, Vector3> result = new(previous.Count);
        foreach (KeyValuePair<int, Vector3> pair in previous)
        {
            if (freeTargetIndices.Count == 0)
            {
                result[pair.Key] = pair.Value;
                continue;
            }

            int closestListIndex = 0;
            float closestDistance = float.PositiveInfinity;
            for (int i = 0; i < freeTargetIndices.Count; i++)
            {
                int targetIndex = freeTargetIndices[i];
                float distance =
                    (targetPositions[targetIndex] - pair.Value).sqrMagnitude;
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestListIndex = i;
                }
            }

            int closestTargetIndex = freeTargetIndices[closestListIndex];
            result[pair.Key] = targetPositions[closestTargetIndex];
            freeTargetIndices.RemoveAt(closestListIndex);
        }

        return result;
    }

    private Vector3[] CreateMorphShapePositions(
        DirectedWaveFormationMorphStep step,
        Vector3 center)
    {
        int count = Mathf.Max(1, formationPositionsByIndex.Count);
        Vector3[] result = new Vector3[count];
        Vector3 morphCenter = center + step.centerOffset;
        Vector2 flattening = new Vector2(
            Mathf.Max(0.01f, step.shapeFlattening.x),
            Mathf.Max(0.01f, step.shapeFlattening.y));

        for (int i = 0; i < count; i++)
            result[i] = GetMorphShapePosition(i, count, step, morphCenter, flattening);

        return result;
    }

    private Vector3 GetMorphShapePosition(
        int index,
        int count,
        DirectedWaveFormationMorphStep step,
        Vector3 center,
        Vector2 flattening)
    {
        return step.layout switch
        {
            DirectedWaveFormationLayout.VerticalLine =>
                GetMorphVerticalLinePosition(index, count, step, center),
            DirectedWaveFormationLayout.Grid =>
                GetMorphGridPosition(index, count, step, center),
            DirectedWaveFormationLayout.VShape =>
                GetMorphVShapePosition(index, count, step, center),
            DirectedWaveFormationLayout.Arc =>
                GetMorphArcPosition(index, count, step, center, flattening),
            DirectedWaveFormationLayout.Circle =>
                GetMorphCirclePosition(index, count, step, center, flattening),
            DirectedWaveFormationLayout.Triangle =>
                GetMorphPolygonPosition(index, count, center, GetUnitTriangleVertices(), step, flattening),
            DirectedWaveFormationLayout.Square =>
                GetMorphPolygonPosition(index, count, center, GetUnitSquareVertices(), step, flattening),
            DirectedWaveFormationLayout.Diamond =>
                GetMorphPolygonPosition(index, count, center, GetUnitDiamondVertices(), step, flattening),
            DirectedWaveFormationLayout.CustomPoints =>
                GetMorphCustomPoint(index, step, center),
            _ => GetMorphHorizontalLinePosition(index, count, step, center)
        };
    }

    private static Vector3[] GetUnitTriangleVertices()
    {
        return new[]
        {
            GetUnitShapePoint(90f),
            GetUnitShapePoint(210f),
            GetUnitShapePoint(330f)
        };
    }

    private static Vector3[] GetUnitSquareVertices()
    {
        return new[]
        {
            new Vector3(-1f, 1f, 0f),
            new Vector3(1f, 1f, 0f),
            new Vector3(1f, -1f, 0f),
            new Vector3(-1f, -1f, 0f)
        };
    }

    private static Vector3[] GetUnitDiamondVertices()
    {
        return new[]
        {
            Vector3.up,
            Vector3.right,
            Vector3.down,
            Vector3.left
        };
    }

    private static Vector3 GetUnitShapePoint(float angleDegrees)
    {
        float radians = angleDegrees * Mathf.Deg2Rad;
        return new Vector3(Mathf.Cos(radians), Mathf.Sin(radians), 0f);
    }

    private static Vector3 GetMorphHorizontalLinePosition(
        int index,
        int count,
        DirectedWaveFormationMorphStep step,
        Vector3 center)
    {
        float spacing = Mathf.Max(0.01f, step.shapeRadius);
        float offset = (count - 1) * spacing * 0.5f;
        return center + new Vector3(index * spacing - offset, 0f, 0f);
    }

    private static Vector3 GetMorphVerticalLinePosition(
        int index,
        int count,
        DirectedWaveFormationMorphStep step,
        Vector3 center)
    {
        float spacing = Mathf.Max(0.01f, step.shapeRadius);
        float offset = (count - 1) * spacing * 0.5f;
        return center + new Vector3(0f, index * spacing - offset, 0f);
    }

    private static Vector3 GetMorphGridPosition(
        int index,
        int count,
        DirectedWaveFormationMorphStep step,
        Vector3 center)
    {
        int columns = Mathf.Max(1, step.columns);
        int rows = Mathf.Max(1, Mathf.CeilToInt(count / (float)columns));
        float spacing = Mathf.Max(0.01f, step.shapeRadius);
        int row = index / columns;
        int column = index % columns;
        float xOffset = (Mathf.Min(columns, count) - 1) * spacing * 0.5f;
        float yOffset = (rows - 1) * spacing * 0.5f;
        return center + new Vector3(
            column * spacing - xOffset,
            yOffset - row * spacing,
            0f);
    }

    private static Vector3 GetMorphVShapePosition(
        int index,
        int count,
        DirectedWaveFormationMorphStep step,
        Vector3 center)
    {
        if (index == 0)
            return center;

        float spacing = Mathf.Max(0.01f, step.shapeRadius);
        int sideIndex = (index + 1) / 2;
        int side = index % 2 == 0 ? 1 : -1;
        return center + new Vector3(
            side * sideIndex * spacing,
            sideIndex * spacing,
            0f);
    }

    private static Vector3 GetMorphArcPosition(
        int index,
        int count,
        DirectedWaveFormationMorphStep step,
        Vector3 center,
        Vector2 flattening)
    {
        if (count <= 1)
            return center;

        float radius = Mathf.Max(0f, step.arcRadius);
        float startAngle = 90f - step.arcDegrees * 0.5f;
        float angle = startAngle + step.arcDegrees * index / (count - 1);
        float radians = angle * Mathf.Deg2Rad;
        return center + new Vector3(
            Mathf.Cos(radians) * radius * flattening.x,
            Mathf.Sin(radians) * radius * flattening.y,
            0f);
    }

    private static Vector3 GetMorphCirclePosition(
        int index,
        int count,
        DirectedWaveFormationMorphStep step,
        Vector3 center,
        Vector2 flattening)
    {
        float angle = 90f - 360f * index / Mathf.Max(1, count);
        float radians = angle * Mathf.Deg2Rad;
        float radius = Mathf.Max(0f, step.shapeRadius);
        return center + new Vector3(
            Mathf.Cos(radians) * radius * flattening.x,
            Mathf.Sin(radians) * radius * flattening.y,
            0f);
    }

    private static Vector3 GetMorphPolygonPosition(
        int index,
        int count,
        Vector3 center,
        Vector3[] vertices,
        DirectedWaveFormationMorphStep step,
        Vector2 flattening)
    {
        Vector3 local = GetMorphPolygonPoint(index, count, vertices)
            * Mathf.Max(0f, step.shapeRadius);
        local.x *= flattening.x;
        local.y *= flattening.y;
        return center + local;
    }

    private static Vector3 GetMorphPolygonPoint(
        int index,
        int count,
        Vector3[] vertices)
    {
        if (count <= 1 || vertices == null || vertices.Length == 0)
            return Vector3.zero;

        float totalLength = 0f;
        for (int i = 0; i < vertices.Length; i++)
            totalLength += Vector3.Distance(
                vertices[i],
                vertices[(i + 1) % vertices.Length]);

        if (totalLength <= 0.0001f)
            return vertices[0];

        float remaining = index / (count - 1f) * totalLength;
        for (int i = 0; i < vertices.Length; i++)
        {
            Vector3 from = vertices[i];
            Vector3 to = vertices[(i + 1) % vertices.Length];
            float edgeLength = Vector3.Distance(from, to);
            if (remaining <= edgeLength)
            {
                float time = edgeLength <= 0.0001f
                    ? 0f
                    : remaining / edgeLength;
                return Vector3.LerpUnclamped(from, to, time);
            }

            remaining -= edgeLength;
        }

        return vertices[0];
    }

    private static Vector3 GetMorphCustomPoint(
        int index,
        DirectedWaveFormationMorphStep step,
        Vector3 center)
    {
        if (step.customPoints == null || step.customPoints.Length == 0)
            return center;

        int safeIndex = Mathf.Clamp(index, 0, step.customPoints.Length - 1);
        return center + step.customPoints[safeIndex];
    }

    private Vector3 GetFormationMorphPosition(
        int formationIndex,
        Vector3 fallback,
        float time)
    {
        if (formationMorphSegments.Count == 0)
            return fallback;

        float totalDuration = GetFormationMorphTotalDuration();
        if (totalDuration <= 0f)
            return fallback;

        float localTime = formationMorphLoop
            ? Mathf.Repeat(time, totalDuration)
            : Mathf.Min(time, totalDuration);

        FormationMorphRuntimeSegment lastSegment =
            formationMorphSegments[formationMorphSegments.Count - 1];
        for (int i = 0; i < formationMorphSegments.Count; i++)
        {
            FormationMorphRuntimeSegment segment = formationMorphSegments[i];
            float segmentEnd = segment.startTime + segment.duration;
            if (localTime <= segmentEnd)
                return EvaluateFormationMorphSegment(
                    segment,
                    formationIndex,
                    fallback,
                    localTime);

            float nextStart = i + 1 < formationMorphSegments.Count
                ? formationMorphSegments[i + 1].startTime
                : totalDuration;
            if (localTime < nextStart)
                return segment.to.TryGetValue(formationIndex, out Vector3 held)
                    ? held
                    : fallback;
        }

        return lastSegment.to.TryGetValue(formationIndex, out Vector3 result)
            ? result
            : fallback;
    }

    private Vector3 EvaluateFormationMorphSegment(
        FormationMorphRuntimeSegment segment,
        int formationIndex,
        Vector3 fallback,
        float localTime)
    {
        Vector3 from = segment.from.TryGetValue(formationIndex, out Vector3 fromValue)
            ? fromValue
            : fallback;
        Vector3 to = segment.to.TryGetValue(formationIndex, out Vector3 toValue)
            ? toValue
            : fallback;
        float normalized = Mathf.Clamp01(
            (localTime - segment.startTime) / Mathf.Max(0.01f, segment.duration));
        float curved = EvaluateCurve(segment.curve, normalized);
        return Vector3.LerpUnclamped(from, to, curved);
    }

    private float GetFormationMorphTotalDuration()
    {
        if (formationMorphSegments.Count == 0)
            return 0f;

        FormationMorphRuntimeSegment last =
            formationMorphSegments[formationMorphSegments.Count - 1];
        return last.startTime + last.duration;
    }

    private Vector3 GetStableFormationCenter()
    {
        if (formationPositionsByIndex.Count == 0)
            return transform.position;

        Vector3 center = Vector3.zero;
        foreach (Vector3 position in formationPositionsByIndex.Values)
            center += position;

        return center / formationPositionsByIndex.Count;
    }

    private Vector3 GetSelfOrbitOffset(int index, float time)
    {
        float phase = index * selfOrbitPhaseOffset;
        float angle = time * selfRotationDegreesPerSecond * Mathf.Deg2Rad + phase;

        return new Vector3(
            (Mathf.Cos(angle) - Mathf.Cos(phase)) * selfOrbitRadius.x,
            (Mathf.Sin(angle) - Mathf.Sin(phase)) * selfOrbitRadius.y,
            0f);
    }

    private Vector3 GetLocalMovementOffset(float time)
    {
        float duration = Mathf.Max(0.01f, localMovementDuration);
        float normalized = time / duration;

        if (localMovementPingPong)
            normalized = Mathf.PingPong(normalized, 1f);
        else if (localMovementLoop)
            normalized = Mathf.Repeat(normalized, 1f);
        else
            normalized = Mathf.Clamp01(normalized);

        float curved = EvaluateCurve(localMovementCurve, normalized);
        return localMovementOffset * curved;
    }

    private Vector3 GetWobbleOffset(
        int index,
        Vector3 basePosition,
        float leadingProjection,
        float time)
    {
        float phase = GetWobblePhase(index, basePosition, leadingProjection);
        float frequency = Mathf.Max(0f, wobbleFrequency);
        float angle = time * frequency + phase;

        return new Vector3(
            (Mathf.Sin(angle) - Mathf.Sin(phase)) * wobbleAmplitude.x,
            (Mathf.Cos(angle) - Mathf.Cos(phase)) * wobbleAmplitude.y,
            0f);
    }

    private Vector3 GetPatrolOffset(float time)
    {
        if (patrolPoints == null || patrolPoints.Length == 0)
            return Vector3.zero;

        if (patrolPoints.Length == 1)
            return patrolPoints[0] != null ? patrolPoints[0].offset : Vector3.zero;

        float remaining = Mathf.Max(0f, time);
        int lastSegmentIndex = patrolLoop
            ? patrolPoints.Length - 1
            : patrolPoints.Length - 2;

        if (lastSegmentIndex < 0)
            return Vector3.zero;

        float totalDuration = GetPatrolTotalDuration();
        if (patrolLoop && totalDuration > 0f)
            remaining = Mathf.Repeat(remaining, totalDuration);
        else if (!patrolLoop && remaining >= totalDuration)
            return GetPatrolPointOffset(patrolPoints.Length - 1);

        for (int i = 0; i <= lastSegmentIndex; i++)
        {
            DirectedWavePatrolPoint point = patrolPoints[i];
            if (point == null)
                continue;

            float duration = Mathf.Max(0.01f, point.durationToNext);
            if (remaining <= duration)
            {
                float normalized = Mathf.Clamp01(remaining / duration);
                float curved = EvaluateCurve(point.easeToNext, normalized);
                return EvaluatePatrolSegment(i, curved);
            }

            remaining -= duration;
        }

        return patrolLoop
            ? GetPatrolPointOffset(0)
            : GetPatrolPointOffset(patrolPoints.Length - 1);
    }

    private float GetPatrolTotalDuration()
    {
        if (patrolPoints == null || patrolPoints.Length < 2)
            return 0f;

        int lastSegmentIndex = patrolLoop
            ? patrolPoints.Length - 1
            : patrolPoints.Length - 2;
        float totalDuration = 0f;

        for (int i = 0; i <= lastSegmentIndex; i++)
        {
            if (patrolPoints[i] == null)
                continue;

            totalDuration += Mathf.Max(0.01f, patrolPoints[i].durationToNext);
        }

        return totalDuration;
    }

    private Vector3 EvaluatePatrolSegment(int segmentIndex, float time)
    {
        if (patrolPoints == null || patrolPoints.Length == 0)
            return Vector3.zero;

        int nextIndex = segmentIndex + 1;
        if (nextIndex >= patrolPoints.Length)
            nextIndex = patrolLoop ? 0 : patrolPoints.Length - 1;

        Vector3 current = GetPatrolPointOffset(segmentIndex);
        Vector3 next = GetPatrolPointOffset(nextIndex);

        return patrolPoints[segmentIndex].motionToNext switch
        {
            DirectedWaveSegmentMotion.Bezier =>
                EvaluatePatrolBezierSegment(segmentIndex, time),
            DirectedWaveSegmentMotion.CatmullRom =>
                EvaluatePatrolCatmullRomSegment(segmentIndex, time),
            _ => Vector3.LerpUnclamped(current, next, time)
        };
    }

    private Vector3 EvaluatePatrolBezierSegment(int segmentIndex, float time)
    {
        Vector3 p0 = GetPatrolPointOffset(segmentIndex);
        Vector3 p3 = GetPatrolPointOffset(GetNextPatrolIndex(segmentIndex));
        Vector3 previous = GetPatrolPointOffset(GetPreviousPatrolIndex(segmentIndex));
        Vector3 following = GetPatrolPointOffset(GetNextPatrolIndex(GetNextPatrolIndex(segmentIndex)));

        Vector3 p1 = p0 + (p3 - previous) / 6f;
        Vector3 p2 = p3 - (following - p0) / 6f;
        float t = Mathf.Clamp01(time);
        float oneMinusT = 1f - t;

        return oneMinusT * oneMinusT * oneMinusT * p0
            + 3f * oneMinusT * oneMinusT * t * p1
            + 3f * oneMinusT * t * t * p2
            + t * t * t * p3;
    }

    private Vector3 EvaluatePatrolCatmullRomSegment(int segmentIndex, float time)
    {
        int p1 = segmentIndex;
        int p0 = GetPreviousPatrolIndex(p1);
        int p2 = GetNextPatrolIndex(p1);
        int p3 = GetNextPatrolIndex(p2);
        float t = Mathf.Clamp01(time);

        return 0.5f * (
            2f * GetPatrolPointOffset(p1)
            + (-GetPatrolPointOffset(p0) + GetPatrolPointOffset(p2)) * t
            + (2f * GetPatrolPointOffset(p0) - 5f * GetPatrolPointOffset(p1)
                + 4f * GetPatrolPointOffset(p2) - GetPatrolPointOffset(p3))
            * t * t
            + (-GetPatrolPointOffset(p0) + 3f * GetPatrolPointOffset(p1)
                - 3f * GetPatrolPointOffset(p2) + GetPatrolPointOffset(p3))
            * t * t * t);
    }

    private int GetPreviousPatrolIndex(int index)
    {
        if (patrolPoints == null || patrolPoints.Length == 0)
            return 0;

        if (patrolLoop)
            return (index - 1 + patrolPoints.Length) % patrolPoints.Length;

        return Mathf.Max(0, index - 1);
    }

    private int GetNextPatrolIndex(int index)
    {
        if (patrolPoints == null || patrolPoints.Length == 0)
            return 0;

        if (patrolLoop)
            return (index + 1) % patrolPoints.Length;

        return Mathf.Min(patrolPoints.Length - 1, index + 1);
    }

    private Vector3 GetPatrolPointOffset(int index)
    {
        if (patrolPoints == null || patrolPoints.Length == 0)
            return Vector3.zero;

        int safeIndex = Mathf.Clamp(index, 0, patrolPoints.Length - 1);
        return patrolPoints[safeIndex] != null
            ? patrolPoints[safeIndex].offset
            : Vector3.zero;
    }

    private float GetWobblePhase(
        int index,
        Vector3 basePosition,
        float leadingProjection)
    {
        if (wobblePhaseMode != DirectedWaveWobblePhaseMode.Directional)
            return index * wobblePhaseOffset;

        Vector2 direction = GetWobbleDirection();
        float projection = Vector2.Dot(
            new Vector2(basePosition.x, basePosition.y),
            direction);
        float distanceFromWaveStart = projection - leadingProjection;
        float step = Mathf.Max(0.01f, wobbleDirectionStep);

        return distanceFromWaveStart / step * wobblePhaseOffset;
    }

    private float GetLeadingWobbleProjection()
    {
        if (wobblePhaseMode != DirectedWaveWobblePhaseMode.Directional)
            return 0f;

        Vector2 direction = GetWobbleDirection();
        float leadingProjection = float.PositiveInfinity;

        foreach (Vector3 basePosition in formationPositions.Values)
        {
            float projection = Vector2.Dot(
                new Vector2(basePosition.x, basePosition.y),
                direction);
            if (projection < leadingProjection)
                leadingProjection = projection;
        }

        return float.IsPositiveInfinity(leadingProjection)
            ? 0f
            : leadingProjection;
    }

    private float GetLeadingWobbleProjection(Dictionary<int, Vector3> positions)
    {
        if (wobblePhaseMode != DirectedWaveWobblePhaseMode.Directional)
            return 0f;

        Vector2 direction = GetWobbleDirection();
        float leadingProjection = float.PositiveInfinity;

        foreach (Vector3 position in positions.Values)
        {
            float projection = Vector2.Dot(
                new Vector2(position.x, position.y),
                direction);
            if (projection < leadingProjection)
                leadingProjection = projection;
        }

        return float.IsPositiveInfinity(leadingProjection)
            ? 0f
            : leadingProjection;
    }

    private Vector2 GetWobbleDirection()
    {
        float radians = wobbleDirectionAngle * Mathf.Deg2Rad;
        Vector2 direction = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
        return direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;
    }

    private bool HasAnyPostCommand()
    {
        if (postCommands == null)
            return false;

        for (int i = 0; i < postCommands.Length; i++)
        {
            if (postCommands[i] != null && postCommands[i].enabled)
                return true;
        }

        return false;
    }

    private bool HasPostCommand(DirectedWavePostCommandType type)
    {
        return HasPostCommandInArray(postCommands, type);
    }

    private bool HasPostCommandInArray(
        DirectedWavePostCommand[] commands,
        DirectedWavePostCommandType type)
    {
        if (commands == null)
            return false;

        for (int i = 0; i < commands.Length; i++)
        {
            DirectedWavePostCommand command = commands[i];
            if (command == null || !command.enabled)
                continue;

            if (command.type == type)
                return true;

            if (HasPostCommandInArray(command.parallelCommands, type))
                return true;

            if (HasPostCommandInArray(command.loopCommands, type))
                return true;
        }

        return false;
    }

    private string GetPostCommandSummary()
    {
        if (postCommands == null || postCommands.Length == 0)
            return "None";

        List<string> names = new();
        for (int i = 0; i < postCommands.Length; i++)
        {
            DirectedWavePostCommand command = postCommands[i];
            if (command != null && command.enabled)
                names.Add(command.type.ToString());
        }

        return names.Count > 0 ? string.Join(" -> ", names) : "None";
    }

    private Enemy GetNextAliveEnemy(ref int cursor)
    {
        if (aliveEnemies.Count <= 0)
            return null;

        List<Enemy> orderedEnemies = new List<Enemy>(aliveEnemies);
        orderedEnemies.RemoveAll(enemy => enemy == null || enemy.isDead);
        if (orderedEnemies.Count <= 0)
            return null;

        if (cursor >= orderedEnemies.Count)
            cursor = 0;

        Enemy enemy = orderedEnemies[cursor];
        cursor = (cursor + 1) % orderedEnemies.Count;
        return enemy;
    }

    private IEnumerator DiveEnemy(
        Enemy enemy,
        bool includePatrol,
        bool includeLocalMove,
        bool wobbleOtherEnemies,
        bool includeSelfOrbit,
        bool includeFormationRotation,
        bool includeFormationMorph,
        float postBehaviorStartTime)
    {
        if (enemy == null || enemy.isDead)
            yield break;

        Transform enemyTransform = enemy.transform;
        Rigidbody2D body = enemy.GetComponent<Rigidbody2D>();
        Vector3 start = enemyTransform.position;
        Vector3 target = GetPlayerTargetPosition();
        Vector3 direction = target - start;
        if (direction.sqrMagnitude < 0.0001f)
            direction = Vector3.down;
        else
            direction.Normalize();

        Vector3 end = target + direction * diveOvershootDistance;
        Log($"Enemy dives at player: {enemy.name}, target={target}, end={end}", enemy);

        yield return MovePostEnemy(
            enemy,
            body,
            start,
            end,
            diveDuration,
            diveCurve,
            includePatrol,
            includeLocalMove,
            wobbleOtherEnemies,
            includeSelfOrbit,
            includeFormationRotation,
            includeFormationMorph,
            postBehaviorStartTime);

        if (enemy == null || enemy.isDead)
            yield break;

        if (!formationPositions.TryGetValue(enemy, out Vector3 returnPosition))
            returnPosition = GetFormationPosition(0);
        else if (includeFormationMorph
            && formationIndices.TryGetValue(enemy, out int formationIndex))
        {
            returnPosition = GetFormationMorphPosition(
                formationIndex,
                returnPosition,
                Time.time - postBehaviorStartTime);
        }

        yield return MovePostEnemy(
            enemy,
            body,
            enemyTransform.position,
            returnPosition,
            diveReturnDuration,
            diveReturnCurve,
            includePatrol,
            includeLocalMove,
            wobbleOtherEnemies,
            includeSelfOrbit,
            includeFormationRotation,
            includeFormationMorph,
            postBehaviorStartTime);
    }

    private IEnumerator MovePostEnemy(
        Enemy enemy,
        Rigidbody2D body,
        Vector3 from,
        Vector3 to,
        float duration,
        AnimationCurve curve,
        bool includePatrol,
        bool includeLocalMove,
        bool wobbleOtherEnemies,
        bool includeSelfOrbit,
        bool includeFormationRotation,
        bool includeFormationMorph,
        float postBehaviorStartTime)
    {
        if (duration <= 0f)
        {
            if (enemy != null && !enemy.isDead)
                SetEnemyPosition(enemy.transform, body, to);

            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration && enemy != null && !enemy.isDead)
        {
            elapsed += Time.deltaTime;
            float time = Mathf.Clamp01(elapsed / duration);
            float curvedTime = EvaluateCurve(curve, time);

            ApplyContinuousPostCommands(
                Time.time - postBehaviorStartTime,
                enemy,
                includePatrol,
                includeLocalMove,
                wobbleOtherEnemies,
                includeSelfOrbit,
                includeFormationRotation,
                includeFormationMorph);

            SetEnemyPosition(
                enemy.transform,
                body,
                Vector3.LerpUnclamped(from, to, curvedTime));

            yield return null;
        }

        if (enemy != null && !enemy.isDead)
            SetEnemyPosition(enemy.transform, body, to);
    }

    private Vector3 GetPlayerTargetPosition()
    {
        if (playerController == null)
            return transform.position + Vector3.down;

        ParentShip currentShip = playerController.CurrentShip;
        if (currentShip != null)
            return currentShip.transform.position;

        return playerController.transform.position;
    }

    private void TryComplete()
    {
        aliveEnemies.RemoveWhere(enemy => enemy == null || enemy.isDead);

        if (!spawnFinished || aliveEnemies.Count > 0)
            return;

        if (enemyManager != null)
            enemyManager.OnEnemyDestroyed -= HandleEnemyDestroyed;

        Log("Subwave cleared. Notifying listeners.");
        NotifySubWaveCleared();
    }

    private void Log(string message, UnityEngine.Object context = null)
    {
        if (!enableDebugLogs)
            return;

        Debug.Log(
            $"[DirectedEnemySubWave:{name}] {message}",
            context != null ? context : this);
    }

    private void LogWarning(string message, UnityEngine.Object context = null)
    {
        if (!enableDebugLogs)
            return;

        Debug.LogWarning(
            $"[DirectedEnemySubWave:{name}] {message}",
            context != null ? context : this);
    }

    private void LogError(string message, UnityEngine.Object context = null)
    {
        Debug.LogError(
            $"[DirectedEnemySubWave:{name}] {message}",
            context != null ? context : this);
    }

    private void OnDrawGizmosSelected()
    {
        DrawPathGizmos();
        DrawFormationGizmos();
    }

    private void DrawPathGizmos()
    {
        DirectedWaveRuntimeCheckpoint[] checkpoints =
            GetWorldPathCheckpoints();
        if (checkpoints.Length == 0)
            return;

        Gizmos.color = Color.cyan;
        Vector3 previousCheckpointSample = checkpoints[0].position;
        int samplesPerSegment = 12;

        for (int segment = 0; segment < checkpoints.Length - 1; segment++)
        {
            for (int sample = 1; sample <= samplesPerSegment; sample++)
            {
                Vector3 current = EvaluateCheckpointSegment(
                    checkpoints,
                    segment,
                    sample / (float)samplesPerSegment);
                Gizmos.DrawLine(previousCheckpointSample, current);
                previousCheckpointSample = current;
            }
        }

        Gizmos.color = Color.blue;
        for (int i = 0; i < checkpoints.Length; i++)
            Gizmos.DrawSphere(checkpoints[i].position, 0.08f);
    }

    private void DrawFormationGizmos()
    {
        Gizmos.color = Color.yellow;
        int effectiveEnemyCount = GetEffectiveEnemyCount();
        for (int i = 0; i < effectiveEnemyCount; i++)
            Gizmos.DrawWireSphere(GetFormationPosition(i), 0.12f);
    }

    private void OnValidate()
    {
        enemyCount = Mathf.Max(1, enemyCount);
        spawnInterval = Mathf.Max(0f, spawnInterval);
        settleDuration = Mathf.Max(0f, settleDuration);
        columns = Mathf.Max(1, columns);
        rows = Mathf.Max(1, rows);
        arcRadius = Mathf.Max(0f, arcRadius);
        shapePointCount = Mathf.Max(1, shapePointCount);
        shapeRadius = Mathf.Max(0f, shapeRadius);
        shapeFlattening = new Vector2(
            Mathf.Max(0.01f, shapeFlattening.x),
            Mathf.Max(0.01f, shapeFlattening.y));
        postStartDelay = Mathf.Max(0f, postStartDelay);
        localMovementDuration = Mathf.Max(0.01f, localMovementDuration);
        wobbleFrequency = Mathf.Max(0f, wobbleFrequency);
        wobbleDirectionStep = Mathf.Max(0.01f, wobbleDirectionStep);
        diveInterval = Mathf.Max(0f, diveInterval);
        diveDuration = Mathf.Max(0.01f, diveDuration);
        diveReturnDuration = Mathf.Max(0f, diveReturnDuration);
        diveOvershootDistance = Mathf.Max(0f, diveOvershootDistance);
        selfOrbitRadius = new Vector2(
            Mathf.Max(0f, selfOrbitRadius.x),
            Mathf.Max(0f, selfOrbitRadius.y));
        formationMorphReturnDuration = Mathf.Max(0.01f, formationMorphReturnDuration);

        if (postCommands != null)
            ValidatePostCommands(postCommands);

        if (formationMorphSteps != null)
        {
            for (int i = 0; i < formationMorphSteps.Length; i++)
            {
                DirectedWaveFormationMorphStep step = formationMorphSteps[i];
                if (step == null)
                    continue;

                ValidateMorphStep(step);
            }
        }

        if (patrolPoints != null)
        {
            for (int i = 0; i < patrolPoints.Length; i++)
            {
                if (patrolPoints[i] == null)
                    continue;

                patrolPoints[i].durationToNext =
                    Mathf.Max(0.01f, patrolPoints[i].durationToNext);
                patrolPoints[i].speedToNext =
                    Mathf.Max(0.01f, patrolPoints[i].speedToNext);
            }
        }

        if (customFormationPoints != null)
        {
            int pointCount = customFormationPoints.Length;
            if (customFormationEnemyOverrides == null)
            {
                customFormationEnemyOverrides = new Enemy[pointCount];
            }
            else if (customFormationEnemyOverrides.Length != pointCount)
            {
                System.Array.Resize(
                    ref customFormationEnemyOverrides,
                    pointCount);
            }
        }

        if (pathCheckpoints == null)
            return;

        for (int i = 0; i < pathCheckpoints.Length; i++)
        {
            if (pathCheckpoints[i] == null)
                continue;

            pathCheckpoints[i].durationToNext =
                Mathf.Max(0.01f, pathCheckpoints[i].durationToNext);
            pathCheckpoints[i].speedToNext =
                Mathf.Max(0.01f, pathCheckpoints[i].speedToNext);
        }
    }

    private void ValidatePostCommands(DirectedWavePostCommand[] commands)
    {
        if (commands == null)
            return;

        for (int i = 0; i < commands.Length; i++)
        {
            DirectedWavePostCommand command = commands[i];
            if (command == null)
                continue;

            command.duration = Mathf.Max(0.01f, command.duration);
            command.holdDuration = Mathf.Max(0f, command.holdDuration);
            command.loopCount = Mathf.Max(1, command.loopCount);
            if (command.morphTarget != null)
                ValidateMorphStep(command.morphTarget);

            ValidatePostCommands(command.parallelCommands);
            ValidatePostCommands(command.loopCommands);
        }
    }

    private struct DirectedWaveRuntimeCheckpoint
    {
        public Vector3 position;
        public float durationToNext;
        public DirectedWaveSegmentMotion motionToNext;
        public AnimationCurve easeToNext;
    }

    private struct FormationMorphRuntimeSegment
    {
        public Dictionary<int, Vector3> from;
        public Dictionary<int, Vector3> to;
        public float startTime;
        public float duration;
        public AnimationCurve curve;
    }

    private static void ValidateMorphStep(DirectedWaveFormationMorphStep step)
    {
        step.columns = Mathf.Max(1, step.columns);
        step.rows = Mathf.Max(1, step.rows);
        step.arcRadius = Mathf.Max(0f, step.arcRadius);
        step.shapeRadius = Mathf.Max(0f, step.shapeRadius);
        step.shapeFlattening = new Vector2(
            Mathf.Max(0.01f, step.shapeFlattening.x),
            Mathf.Max(0.01f, step.shapeFlattening.y));
        step.durationToShape = Mathf.Max(0.01f, step.durationToShape);
        step.holdDuration = Mathf.Max(0f, step.holdDuration);
    }
}
