using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Zenject;

public sealed partial class DirectedEnemySubWave : InfoAboutSubWave
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
    [SerializeField] private bool enableDebugLogs;

    [Header("Entrance path")]
    [SerializeField] private DirectedWaveEntranceMode entranceMode =
        DirectedWaveEntranceMode.Checkpoints;
    [SerializeField] private DirectedWaveCoordinateSpace pathCoordinateSpace =
        DirectedWaveCoordinateSpace.LocalToSubWave;
    [SerializeField] private DirectedWavePathCheckpoint[] pathCheckpoints =
        System.Array.Empty<DirectedWavePathCheckpoint>();
    [SerializeField] private DirectedWaveIndividualEntrancePoint[]
        individualEntrancePoints =
            System.Array.Empty<DirectedWaveIndividualEntrancePoint>();
    [SerializeField, Min(0f)] private float individualPointMovementStartDelay =
        0.1f;
    [SerializeField, Min(0f)] private float individualPointMovementDuration =
        0.35f;
    [SerializeField] private AnimationCurve individualPointMovementCurve =
        AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [SerializeField, HideInInspector] private Vector3 individualEntranceShapeCenter =
        new Vector3(0f, 5f, 0f);
    [SerializeField, HideInInspector, Min(0f)] private float individualEntranceShapeRadius =
        2f;
    [SerializeField, HideInInspector] private Vector2 individualEntranceShapeFlattening =
        Vector2.one;
    [SerializeField, HideInInspector] private float individualEntranceShapeRotationDegrees;

    [Header("Entrance completion")]
    [SerializeField] private DirectedWaveEntranceCompletionMode
        entranceCompletionMode =
            DirectedWaveEntranceCompletionMode.MoveToFormation;
    [SerializeField, Min(0)] private int entranceLoopStartCheckpointIndex;
    [SerializeField] private bool entranceLoopTeleportToStart;
    [SerializeField, Min(0f)] private float entranceLoopTeleportDelay;

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
    [SerializeField] private Enemy[] proceduralFormationEnemyOverrides =
        System.Array.Empty<Enemy>();
    [SerializeField] private Transform formationPointsRoot;
    [SerializeField, Min(0f)] private float settleDuration = 0.35f;
    [SerializeField] private AnimationCurve settleCurve =
        AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Post behavior")]
    [SerializeField] private DirectedWavePostCommand[] postCommands =
        System.Array.Empty<DirectedWavePostCommand>();
    [SerializeField, Min(0f)] private float postStartDelay = 0.25f;
    [SerializeField, Min(1)] private int postCommandPipelineFixedCount = 1;
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
    [SerializeField] private bool patrolLoop = true;
    [SerializeField] private DirectedWaveCoordinateSpace patrolCoordinateSpace =
        DirectedWaveCoordinateSpace.World;
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

    private readonly DirectedWaveEnemyTracker aliveEnemies = new();
    private readonly Dictionary<Enemy, Vector3> formationPositions = new();
    private readonly Dictionary<Enemy, int> formationIndices = new();
    private readonly Dictionary<Enemy, Rigidbody2D> enemyBodies = new();
    private readonly Dictionary<int, Vector3> formationPositionsByIndex = new();
    private readonly List<FormationMorphRuntimeSegment> formationMorphSegments = new();
    private readonly List<IDirectedWavePostTimelineBehaviour> postTimelineBehaviours =
        new(2);
    private readonly List<MonoBehaviour> formationAttackComponents = new(2);
    private readonly List<Coroutine> movementRoutines = new();
    private Coroutine spawnRoutine;
    private Coroutine postBehaviorRoutine;
    private DirectedWaveEnemyFactory enemyFactory;
    private DirectedWaveAttackBehaviour attackBehaviour;
    private int movingToFormationCount;
    private bool spawnFinished;
    private bool activated;
    private bool postBehaviorStarted;
    private bool individualEntrancePointWarningLogged;
    private bool entranceLoopConfigurationWarningLogged;

    protected override void Awake()
    {
        attackBehaviour = GetComponent<DirectedWaveAttackBehaviour>();
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
        formationAttackComponents.Clear();
        formationPositions.Clear();
        formationIndices.Clear();
        enemyBodies.Clear();
        formationPositionsByIndex.Clear();
        formationMorphSegments.Clear();
        ResetRuntimeTimelineState();
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
        individualEntrancePointWarningLogged = false;
        entranceLoopConfigurationWarningLogged = false;
        movingToFormationCount = 0;
        aliveEnemies.Clear();
        formationPositions.Clear();
        formationIndices.Clear();
        enemyBodies.Clear();
        formationPositionsByIndex.Clear();
        formationMorphSegments.Clear();
        ResetRuntimeTimelineState();
        attackBehaviour?.BeginEntranceAttacks();

        Log(
            $"Activated. EnemyPrefab={(enemyPrefab != null ? enemyPrefab.name : "NULL")}, " +
            $"PointOverrides={GetPointEnemyOverrideCount()}, " +
            $"Layout={formationLayout}, EffectiveEnemyCount={GetEffectiveEnemyCount()}, " +
            $"PostCommands={GetPostCommandSummary()}, " +
            $"EntranceMode={entranceMode}, " +
            $"EntranceCompletion={entranceCompletionMode}, " +
            $"Checkpoints={(pathCheckpoints != null ? pathCheckpoints.Length : 0)}, " +
            $"SpawnPoint={(spawnPoint != null ? spawnPoint.name : "NULL")}");

        if (container == null)
            LogWarning("DiContainer was not injected. Falling back to Unity Instantiate.");

        if (enemyManager == null)
            LogWarning("EnemyManager was not injected. Subwave can spawn enemies, but completion depends on null/dead cleanup only.");

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
            SpawnEnemy(spawnOrder[i], i, effectiveEnemyCount);

            if (spawnInterval > 0f && i < effectiveEnemyCount - 1)
                yield return new WaitForSeconds(spawnInterval);
        }

        FinishSpawning();
    }

    private void SpawnEnemy(
        int formationIndex,
        int spawnStep,
        int plannedEnemyCount)
    {
        Enemy prefabToSpawn = GetEnemyPrefabForIndex(formationIndex);
        if (prefabToSpawn == null)
        {
            LogError(
                $"No enemy prefab resolved for formation index {formationIndex}. " +
                "Set global Enemy Prefab or Enemy Override for this point.");
            return;
        }

        Vector3 spawnPosition = GetSpawnPosition(formationIndex);
        Transform parent = parentEnemiesToSubWave ? transform : null;

        Log(
            $"Spawning enemy {spawnStep + 1}/{plannedEnemyCount} " +
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

        GetTransformPointEnemyOverrideComponent(formationIndex)?.ApplyTo(enemy);

        SetFormationAttackReady(
            enemy,
            attackBehaviour != null
            && attackBehaviour.AllowAutonomousAttackDuringEntrance);
        aliveEnemies.Add(enemy);
        NotifyEnemySpawned(enemy, spawnStep, plannedEnemyCount);
        enemyBodies[enemy] = enemy.GetComponent<Rigidbody2D>();
        Log($"Spawned enemy instance: {enemy.name}. AliveEnemies={aliveEnemies.Count}", enemy);

        movingToFormationCount++;
        Coroutine routine = StartCoroutine(
            MoveEnemyToFormation(enemy, formationIndex, spawnStep));
        movementRoutines.Add(routine);
    }

    private int[] BuildSpawnOrder(int count)
    {
        count = Mathf.Max(0, count);
        Vector3[] positions = new Vector3[count];
        for (int i = 0; i < count; i++)
            positions[i] = GetFormationPosition(i);

        return DirectedWaveSpawnOrderResolver.Build(
            positions,
            spawnOrderMode,
            spawnOrderAngle,
            spawnOrderStartAngle);
    }

    private Enemy InstantiateEnemyPrefab(
        Enemy prefabToSpawn,
        Vector3 spawnPosition,
        Transform parent)
    {
        enemyFactory ??= new DirectedWaveEnemyFactory(container);
        return enemyFactory.Create(prefabToSpawn, spawnPosition, parent);
    }

    private IEnumerator MoveEnemyToFormation(
        Enemy enemy,
        int index,
        int spawnStep)
    {
        if (enemy == null)
            yield break;

        Transform enemyTransform = enemy.transform;
        Rigidbody2D body = GetCachedEnemyBody(enemy);
        Vector3 formationPosition = GetFormationPosition(index);

        Log(
            $"Moving enemy {enemy.name}. Index={index}, " +
            $"EntranceMode={entranceMode}, FormationPosition={formationPosition}",
            enemy);

        if (UsesIndividualEntrancePoints())
        {
            float movementStartDelay =
                GetIndividualPointMovementStartDelay(spawnStep);
            if (movementStartDelay > 0f)
                yield return new WaitForSeconds(movementStartDelay);

            if (enemy != null && individualPointMovementDuration > 0f)
            {
                yield return MoveBetween(
                    enemy,
                    enemyTransform,
                    body,
                    enemyTransform.position,
                    formationPosition,
                    individualPointMovementDuration,
                    individualPointMovementCurve);
            }
            else if (enemy != null)
            {
                SetEntranceRoutePosition(
                    enemy,
                    enemyTransform,
                    body,
                    formationPosition);
            }
        }
        else
        {
            DirectedWaveRuntimeCheckpoint[] checkpoints =
                GetWorldPathCheckpoints();

            if (CanUseEntrancePathLoop(checkpoints))
            {
                yield return MoveEnemyAlongEntranceLoop(
                    enemy,
                    index,
                    enemyTransform,
                    body,
                    checkpoints);
                yield break;
            }

            LogInvalidEntranceLoopConfigurationIfNeeded();

            if (checkpoints.Length > 0)
            {
                SetEntranceRoutePosition(
                    enemy,
                    enemyTransform,
                    body,
                    checkpoints[0].position);
                attackBehaviour?.NotifyEntranceCheckpointReached(
                    enemy,
                    index,
                    0,
                    checkpoints);

                if (checkpoints.Length > 1)
                {
                    yield return MoveAlongCheckpoints(
                        enemy,
                        index,
                        enemyTransform,
                        body,
                        checkpoints);
                }
            }

            if (enemy != null && settleDuration > 0f)
            {
                Vector3 from = GetEntranceRoutePosition(
                    enemy,
                    enemyTransform.position);
                yield return MoveBetween(
                    enemy,
                    enemyTransform,
                    body,
                    from,
                    formationPosition,
                    settleDuration,
                    settleCurve);
            }
            else if (enemy != null)
            {
                SetEntranceRoutePosition(
                    enemy,
                    enemyTransform,
                    body,
                    formationPosition);
            }
        }

        if (enemy != null && !enemy.isDead)
        {
            formationPositions[enemy] = formationPosition;
            formationIndices[enemy] = index;
            formationPositionsByIndex[index] = formationPosition;
            SetFormationAttackReady(enemy, true);
            attackBehaviour?.NotifyEnemyEntranceCompleted(enemy);
        }

        movingToFormationCount = Mathf.Max(0, movingToFormationCount - 1);
        TryStartPostBehavior();
    }

    private IEnumerator MoveEnemyAlongEntranceLoop(
        Enemy enemy,
        int index,
        Transform enemyTransform,
        Rigidbody2D body,
        DirectedWaveRuntimeCheckpoint[] checkpoints)
    {
        SetEntranceRoutePosition(
            enemy,
            enemyTransform,
            body,
            checkpoints[0].position);
        attackBehaviour?.NotifyEntranceCheckpointReached(
            enemy,
            index,
            0,
            checkpoints);
        yield return MoveAlongCheckpoints(
            enemy,
            index,
            enemyTransform,
            body,
            checkpoints);

        CompleteEntranceLoopFirstPass(enemy, index);
        if (enemy == null || enemy.isDead)
            yield break;

        yield return RepeatEntrancePathLoop(
            enemy,
            index,
            enemyTransform,
            body,
            checkpoints);
    }

    private void CompleteEntranceLoopFirstPass(Enemy enemy, int index)
    {
        if (enemy != null && !enemy.isDead)
        {
            Vector3 currentPosition = GetEntranceRoutePosition(
                enemy,
                enemy.transform.position);
            formationPositions[enemy] = currentPosition;
            formationIndices[enemy] = index;
            formationPositionsByIndex[index] = currentPosition;
            SetFormationAttackReady(enemy, true);
        }

        movingToFormationCount = Mathf.Max(0, movingToFormationCount - 1);
        TryStartPostBehavior();
    }

    private IEnumerator RepeatEntrancePathLoop(
        Enemy enemy,
        int formationIndex,
        Transform target,
        Rigidbody2D body,
        DirectedWaveRuntimeCheckpoint[] checkpoints)
    {
        int loopStartIndex =
            DirectedWaveEntranceLoopEvaluator.GetLoopStartCheckpointIndex(
                entranceLoopStartCheckpointIndex,
                checkpoints.Length);
        int lastIndex = checkpoints.Length - 1;

        while (enemy != null && !enemy.isDead && target != null)
        {
            if (entranceLoopTeleportToStart)
            {
                float elapsedDelay = 0f;
                while (elapsedDelay < entranceLoopTeleportDelay
                    && enemy != null
                    && !enemy.isDead
                    && target != null)
                {
                    elapsedDelay += Time.deltaTime;
                    yield return null;
                }

                if (enemy == null || enemy.isDead || target == null)
                    yield break;

                SetEntranceRoutePosition(
                    enemy,
                    target,
                    body,
                    checkpoints[loopStartIndex].position);
                attackBehaviour?.NotifyEntranceCheckpointReached(
                    enemy,
                    formationIndex,
                    loopStartIndex,
                    checkpoints,
                    true);
            }
            else
            {
                yield return MoveAlongLoopCheckpointSegment(
                    enemy,
                    formationIndex,
                    target,
                    body,
                    checkpoints,
                    Mathf.Max(0, lastIndex - 1),
                    lastIndex,
                    loopStartIndex,
                    Mathf.Min(lastIndex, loopStartIndex + 1),
                    true);
            }

            if (enemy == null || enemy.isDead || target == null)
                yield break;

            for (int currentIndex = loopStartIndex;
                 currentIndex < lastIndex;
                 currentIndex++)
            {
                int previousIndex = currentIndex == loopStartIndex
                    ? (entranceLoopTeleportToStart ? currentIndex : lastIndex)
                    : currentIndex - 1;
                int followingIndex = currentIndex + 1 == lastIndex
                    ? (entranceLoopTeleportToStart ? lastIndex : loopStartIndex)
                    : currentIndex + 2;

                yield return MoveAlongLoopCheckpointSegment(
                    enemy,
                    formationIndex,
                    target,
                    body,
                    checkpoints,
                    previousIndex,
                    currentIndex,
                    currentIndex + 1,
                    followingIndex);

                if (enemy == null || enemy.isDead || target == null)
                    yield break;
            }
        }
    }

    private IEnumerator MoveAlongLoopCheckpointSegment(
        Enemy enemy,
        int formationIndex,
        Transform target,
        Rigidbody2D body,
        DirectedWaveRuntimeCheckpoint[] checkpoints,
        int previousIndex,
        int currentIndex,
        int nextIndex,
        int followingIndex,
        bool isLoopRestart = false)
    {
        float duration = DirectedWaveEntranceLoopEvaluator.GetSegmentDuration(
            checkpoints,
            currentIndex);
        float elapsed = 0f;

        while (elapsed < duration
            && enemy != null
            && !enemy.isDead
            && target != null)
        {
            float stepDeltaTime = Mathf.Min(Time.deltaTime, duration - elapsed);
            elapsed += stepDeltaTime;
            float time = Mathf.Clamp01(elapsed / duration);
            float curvedTime = EvaluateCurve(
                checkpoints[currentIndex].easeToNext,
                time);
            float pathTime =
                DirectedWavePathEvaluator.GetParameterAtNormalizedDistance(
                    checkpoints[previousIndex].position,
                    checkpoints[currentIndex].position,
                    checkpoints[nextIndex].position,
                    checkpoints[followingIndex].position,
                    checkpoints[currentIndex].motionToNext,
                    curvedTime);
            Vector3 position =
                DirectedWaveEntranceLoopEvaluator.EvaluateLoopSegment(
                    checkpoints,
                    previousIndex,
                    currentIndex,
                    nextIndex,
                    followingIndex,
                    pathTime);
            SetEntranceRoutePosition(enemy, target, body, position);
            attackBehaviour?.NotifyEntranceSegmentAdvanced(
                enemy,
                stepDeltaTime);
            yield return null;
        }

        if (enemy != null && !enemy.isDead && target != null)
        {
            SetEntranceRoutePosition(
                enemy,
                target,
                body,
                checkpoints[nextIndex].position);
            attackBehaviour?.NotifyEntranceCheckpointReached(
                enemy,
                formationIndex,
                nextIndex,
                checkpoints,
                isLoopRestart);
        }
    }

    private void SetFormationAttackReady(Enemy enemy, bool isReady)
    {
        if (enemy == null)
            return;

        formationAttackComponents.Clear();
        enemy.GetComponents<MonoBehaviour>(formationAttackComponents);
        for (int i = 0; i < formationAttackComponents.Count; i++)
        {
            if (formationAttackComponents[i] is IFormationAttackActivation activation)
                activation.SetFormationAttackReady(isReady);
        }

        formationAttackComponents.Clear();
    }

    private IEnumerator MoveAlongCheckpoints(
        Enemy enemy,
        int formationIndex,
        Transform target,
        Rigidbody2D body,
        DirectedWaveRuntimeCheckpoint[] checkpoints)
    {
        for (int i = 0; i < checkpoints.Length - 1; i++)
        {
            float duration = DirectedWaveEntranceLoopEvaluator.GetSegmentDuration(
                checkpoints,
                i);
            float elapsed = 0f;

            while (elapsed < duration
                && enemy != null
                && !enemy.isDead
                && target != null)
            {
                float stepDeltaTime = Mathf.Min(
                    Time.deltaTime,
                    duration - elapsed);
                elapsed += stepDeltaTime;
                float time = Mathf.Clamp01(elapsed / duration);
                float curvedTime = EvaluateCurve(
                    checkpoints[i].easeToNext,
                    time);
                float pathTime =
                    DirectedWavePathEvaluator.GetParameterAtNormalizedDistance(
                        checkpoints,
                        i,
                        curvedTime);
                Vector3 position = DirectedWavePathEvaluator.EvaluateSegment(
                    checkpoints,
                    i,
                    pathTime);
                SetEntranceRoutePosition(enemy, target, body, position);
                attackBehaviour?.NotifyEntranceSegmentAdvanced(
                    enemy,
                    stepDeltaTime);
                yield return null;
            }

            if (enemy != null && !enemy.isDead && target != null)
            {
                SetEntranceRoutePosition(
                    enemy,
                    target,
                    body,
                    checkpoints[i + 1].position);
                attackBehaviour?.NotifyEntranceCheckpointReached(
                    enemy,
                    formationIndex,
                    i + 1,
                    checkpoints);
            }
        }
    }

    private IEnumerator MoveBetween(
        Enemy enemy,
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
            SetEntranceRoutePosition(
                enemy,
                target,
                body,
                Vector3.LerpUnclamped(from, to, curvedTime));
            yield return null;
        }

        if (target != null)
            SetEntranceRoutePosition(enemy, target, body, to);
    }

    private Vector3 GetSpawnPosition(int formationIndex)
    {
        if (UsesIndividualEntrancePoints()
            && TryGetIndividualEntrancePointPosition(
                formationIndex,
                out Vector3 position))
        {
            return position;
        }

        if (UsesIndividualEntrancePoints())
            LogMissingIndividualEntrancePointWarning(formationIndex);

        return GetSpawnPosition();
    }

    private Vector3 GetSpawnPosition()
    {
        if (!UsesIndividualEntrancePoints() && pathCheckpoints != null)
        {
            for (int i = 0; i < pathCheckpoints.Length; i++)
            {
                DirectedWavePathCheckpoint checkpoint = pathCheckpoints[i];
                if (checkpoint != null)
                    return ToWorld(checkpoint.position, pathCoordinateSpace);
            }
        }

        if (spawnPoint != null)
            return spawnPoint.position;

        return transform.position;
    }

    private bool UsesIndividualEntrancePoints()
    {
        return entranceMode == DirectedWaveEntranceMode.IndividualPoints;
    }

    private bool IsEntrancePathLoopRequested()
    {
        return entranceCompletionMode
            == DirectedWaveEntranceCompletionMode.LoopEntrancePath;
    }

    private bool HasValidEntranceLoopConfiguration()
    {
        if (!IsEntrancePathLoopRequested()
            || UsesIndividualEntrancePoints()
            || pathCheckpoints == null)
        {
            return false;
        }

        int validCheckpointCount = 0;
        for (int i = 0; i < pathCheckpoints.Length; i++)
        {
            if (pathCheckpoints[i] != null)
                validCheckpointCount++;
        }

        return validCheckpointCount >= 2;
    }

    private bool CanUseEntrancePathLoop(
        DirectedWaveRuntimeCheckpoint[] checkpoints)
    {
        return IsEntrancePathLoopRequested()
            && !UsesIndividualEntrancePoints()
            && checkpoints != null
            && checkpoints.Length >= 2;
    }

    private void LogInvalidEntranceLoopConfigurationIfNeeded()
    {
        if (!IsEntrancePathLoopRequested()
            || HasValidEntranceLoopConfiguration()
            || entranceLoopConfigurationWarningLogged)
        {
            return;
        }

        entranceLoopConfigurationWarningLogged = true;
        Debug.LogWarning(
            "Loop Entrance Path requires Checkpoints mode with at least two valid checkpoints. "
            + "The wave will use Move To Formation until the path is configured.",
            this);
    }

    private bool TryGetIndividualEntrancePointPosition(
        int formationIndex,
        out Vector3 position)
    {
        if (individualEntrancePoints != null
            && formationIndex >= 0
            && formationIndex < individualEntrancePoints.Length)
        {
            DirectedWaveIndividualEntrancePoint point =
                individualEntrancePoints[formationIndex];
            if (point != null)
            {
                position = ToWorld(point.position, pathCoordinateSpace);
                return true;
            }
        }

        position = default;
        return false;
    }

    private float GetIndividualPointMovementStartDelay(int spawnStep)
    {
        return Mathf.Max(0, spawnStep)
            * Mathf.Max(0f, individualPointMovementStartDelay);
    }

    private void LogMissingIndividualEntrancePointWarning(int formationIndex)
    {
        if (individualEntrancePointWarningLogged)
            return;

        individualEntrancePointWarningLogged = true;
        LogWarning(
            $"Individual entrance point is missing for formation index {formationIndex}. "
            + "The ship will use Spawn Point instead. Match Individual Points to the formation in the Inspector.");
    }

    private DirectedWaveRuntimeCheckpoint[] GetWorldPathCheckpoints()
    {
        if (UsesIndividualEntrancePoints()
            || pathCheckpoints == null
            || pathCheckpoints.Length == 0)
            return System.Array.Empty<DirectedWaveRuntimeCheckpoint>();

        int validCount = 0;
        for (int i = 0; i < pathCheckpoints.Length; i++)
        {
            if (pathCheckpoints[i] != null)
                validCount++;
        }

        if (validCount == 0)
            return System.Array.Empty<DirectedWaveRuntimeCheckpoint>();

        DirectedWaveRuntimeCheckpoint[] result =
            new DirectedWaveRuntimeCheckpoint[validCount];
        int resultIndex = 0;
        for (int i = 0; i < pathCheckpoints.Length; i++)
        {
            DirectedWavePathCheckpoint checkpoint = pathCheckpoints[i];
            if (checkpoint == null)
                continue;

            result[resultIndex++] = new DirectedWaveRuntimeCheckpoint
            {
                position = ToWorld(checkpoint.position, pathCoordinateSpace),
                durationToNext = checkpoint.durationToNext,
                motionToNext = checkpoint.motionToNext,
                easeToNext = checkpoint.easeToNext
            };
        }

        return result;
    }

    private Vector3 GetFormationPosition(int index)
    {
        DirectedWaveFormationSettings settings = new(
            formationFrozen,
            formationLayout,
            formationCoordinateSpace,
            formationCenter,
            spacing,
            columns,
            rows,
            arcRadius,
            arcDegrees,
            shapeRadius,
            shapeFlattening,
            customFormationPoints,
            formationPointsRoot,
            transform,
            spawnPoint,
            GetEffectiveEnemyCount());
        return DirectedWaveFormationSolver.GetPosition(index, settings);
    }

    public override int GetRewardEligibleEnemyCount()
    {
        return GetEffectiveEnemyCount();
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
            _ => GetProceduralFormationEnemyOverride(index)
        };
    }

    private Enemy GetTransformPointEnemyOverride(int index)
    {
        DirectedWaveEnemyOverride enemyOverride =
            GetTransformPointEnemyOverrideComponent(index);
        return enemyOverride != null
            ? enemyOverride.EnemyPrefabOverride
            : null;
    }

    private DirectedWaveEnemyOverride GetTransformPointEnemyOverrideComponent(
        int index)
    {
        if (formationPointsRoot == null
            || index < 0
            || index >= formationPointsRoot.childCount)
        {
            return null;
        }

        return formationPointsRoot
            .GetChild(index)
            .GetComponent<DirectedWaveEnemyOverride>();
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

    private Enemy GetProceduralFormationEnemyOverride(int index)
    {
        if (proceduralFormationEnemyOverrides == null
            || index < 0
            || index >= proceduralFormationEnemyOverrides.Length)
        {
            return null;
        }

        return proceduralFormationEnemyOverrides[index];
    }

    private bool HasPointEnemyOverridesConfigured()
    {
        if (formationFrozen
            || formationLayout == DirectedWaveFormationLayout.TransformPoints)
        {
            return HasTransformPointEnemyOverrides();
        }

        if (formationLayout == DirectedWaveFormationLayout.CustomPoints)
            return HasCustomFormationEnemyOverrides();

        return HasProceduralFormationEnemyOverrides();
    }

    private int GetPointEnemyOverrideCount()
    {
        if (formationFrozen
            || formationLayout == DirectedWaveFormationLayout.TransformPoints)
        {
            return GetTransformPointEnemyOverrideCount();
        }

        if (formationLayout == DirectedWaveFormationLayout.CustomPoints)
            return GetCustomFormationEnemyOverrideCount();

        return GetProceduralFormationEnemyOverrideCount();
    }

    private bool HasTransformPointEnemyOverrides()
    {
        return GetTransformPointEnemyOverrideCount() > 0;
    }

    private int GetTransformPointEnemyOverrideCount()
    {
        int count = 0;

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

    private int GetCustomFormationEnemyOverrideCount()
    {
        int count = 0;
        if (customFormationEnemyOverrides == null)
            return count;

        for (int i = 0; i < customFormationEnemyOverrides.Length; i++)
        {
            if (customFormationEnemyOverrides[i] != null)
                count++;
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

        enemyBodies.Remove(enemy);
        ClearEnemyMotionState(enemy);
        NotifyPostTimelineEnemyDestroyed(enemy);
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

        if (HasValidEntranceLoopConfiguration())
            return;

        if (!HasRuntimePostBehavior())
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
        yield return RunUnifiedTimeline();
    }

    private bool IsBackgroundParallel(DirectedWavePostCommand command)
    {
        return command != null
            && command.type == DirectedWavePostCommandType.Parallel
            && command.parallelExecutionMode
                == DirectedWaveParallelExecutionMode.Background;
    }

    private Dictionary<int, Vector3> EvaluateParallelCommandFrame(
        DirectedWavePostCommand parallelCommand,
        Dictionary<int, Vector3> start,
        float elapsed,
        float parallelDuration,
        bool finalFrame,
        RuntimeTimelineEvaluationContext runtimeContext = null)
    {
        Dictionary<int, Vector3> frame =
            CopySimulationPositions(start, runtimeContext);
        if (parallelCommand.parallelCommands == null)
            return frame;

        for (int i = 0; i < parallelCommand.parallelCommands.Length; i++)
        {
            DirectedWavePostCommand child = parallelCommand.parallelCommands[i];
            if (child == null || !child.enabled || child.type == DirectedWavePostCommandType.Parallel)
                continue;

            int marker = runtimeContext?.MarkPositionBuffers() ?? 0;
            Dictionary<int, Vector3> evaluated = EvaluatePostCommandFrame(
                child,
                frame,
                elapsed,
                parallelDuration,
                finalFrame,
                runtimeContext);
            if (runtimeContext == null)
            {
                frame = evaluated;
            }
            else
            {
                ReplacePositions(frame, evaluated);
                runtimeContext.RestorePositionBuffers(marker);
            }
        }

        return frame;
    }

    private Dictionary<int, Vector3> EvaluatePostCommandFrame(
        DirectedWavePostCommand command,
        Dictionary<int, Vector3> input,
        float elapsed,
        float parallelDuration,
        bool finalFrame,
        RuntimeTimelineEvaluationContext runtimeContext = null)
    {
        if (!TryGetPostCommandHandler(command.type, out var handler))
            return CopySimulationPositions(input, runtimeContext);

        return handler.EvaluateFrame(
            this,
            command,
            input,
            elapsed,
            parallelDuration,
            finalFrame,
            runtimeContext);
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
        float elapsed,
        RuntimeTimelineEvaluationContext runtimeContext = null)
    {
        Dictionary<int, Vector3> frame =
            CopySimulationPositions(input, runtimeContext);
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

    private void ApplyPipelinePositions(Dictionary<int, Vector3> positions)
    {
        foreach (Enemy enemy in aliveEnemies)
        {
            if (enemy == null || enemy.isDead)
                continue;

            if (timelineDetachedEnemies.Contains(enemy))
                continue;

            if (!formationIndices.TryGetValue(enemy, out int index))
                continue;

            if (!positions.TryGetValue(index, out Vector3 position))
                continue;

            Rigidbody2D body = GetCachedEnemyBody(enemy);
            SetEnemyPosition(enemy.transform, body, position);
        }
    }

    private Rigidbody2D GetCachedEnemyBody(Enemy enemy)
    {
        if (enemy == null)
            return null;

        if (enemyBodies.TryGetValue(enemy, out Rigidbody2D body))
            return body;

        body = enemy.GetComponent<Rigidbody2D>();
        enemyBodies[enemy] = body;
        return body;
    }

    private bool HasAliveEnemies()
    {
        return aliveEnemies.RemoveDeadAndHasAny();
    }

    private static Dictionary<int, Vector3> OffsetPositions(
        Dictionary<int, Vector3> source,
        Vector3 offset,
        RuntimeTimelineEvaluationContext runtimeContext = null)
    {
        Dictionary<int, Vector3> result = runtimeContext != null
            ? runtimeContext.RentPositions(source.Count)
            : new Dictionary<int, Vector3>(source.Count);
        foreach (KeyValuePair<int, Vector3> pair in source)
            result[pair.Key] = pair.Value + offset;

        return result;
    }

    private static Dictionary<int, Vector3> RotatePositions(
        Dictionary<int, Vector3> source,
        Vector3 center,
        float angleDegrees,
        RuntimeTimelineEvaluationContext runtimeContext = null)
    {
        float radians = angleDegrees * Mathf.Deg2Rad;
        float sin = Mathf.Sin(radians);
        float cos = Mathf.Cos(radians);
        Dictionary<int, Vector3> result = runtimeContext != null
            ? runtimeContext.RentPositions(source.Count)
            : new Dictionary<int, Vector3>(source.Count);

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
        float time,
        RuntimeTimelineEvaluationContext runtimeContext = null)
    {
        Dictionary<int, Vector3> result = runtimeContext != null
            ? runtimeContext.RentPositions(from.Count)
            : new Dictionary<int, Vector3>(from.Count);
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

            Rigidbody2D body = GetCachedEnemyBody(enemy);
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
        Vector3 center,
        RuntimeTimelineEvaluationContext runtimeContext = null)
    {
        int targetCount = previous.Count;
        Vector3[] targetPositions = CreateMorphShapePositions(
            step,
            center,
            targetCount,
            runtimeContext);
        List<int> freeTargetIndices = runtimeContext != null
            ? runtimeContext.morphFreeTargetIndices
            : new List<int>(targetCount);
        freeTargetIndices.Clear();
        for (int i = 0; i < targetCount; i++)
            freeTargetIndices.Add(i);

        Dictionary<int, Vector3> result = runtimeContext != null
            ? runtimeContext.RentPositions(previous.Count)
            : new Dictionary<int, Vector3>(previous.Count);
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
        Vector3 center,
        int positionCount,
        RuntimeTimelineEvaluationContext runtimeContext = null)
    {
        int count = Mathf.Max(1, positionCount);
        Vector3[] result = runtimeContext != null
            ? runtimeContext.RentMorphPositions(count)
            : new Vector3[count];
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

    private static readonly Vector3[] UnitTriangleVertices =
        {
            GetUnitShapePoint(90f),
            GetUnitShapePoint(210f),
            GetUnitShapePoint(330f)
        };

    private static readonly Vector3[] UnitSquareVertices =
        {
            new Vector3(-1f, 1f, 0f),
            new Vector3(1f, 1f, 0f),
            new Vector3(1f, -1f, 0f),
            new Vector3(-1f, -1f, 0f)
        };

    private static readonly Vector3[] UnitDiamondVertices =
        {
            Vector3.up,
            Vector3.right,
            Vector3.down,
            Vector3.left
        };

    private static Vector3[] GetUnitTriangleVertices()
    {
        return UnitTriangleVertices;
    }

    private static Vector3[] GetUnitSquareVertices()
    {
        return UnitSquareVertices;
    }

    private static Vector3[] GetUnitDiamondVertices()
    {
        return UnitDiamondVertices;
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
        return GetPatrolCenterPosition(time) - GetStableFormationCenter();
    }

    private Vector3 GetPatrolCenterPosition(float time)
    {
        if (patrolPoints == null || patrolPoints.Length == 0)
            return GetStableFormationCenter();

        if (patrolPoints.Length == 1)
            return GetPatrolPointPosition(0);

        float remaining = Mathf.Max(0f, time);
        int lastSegmentIndex = patrolLoop
            ? patrolPoints.Length - 1
            : patrolPoints.Length - 2;

        if (lastSegmentIndex < 0)
            return GetStableFormationCenter();

        float totalDuration = GetPatrolTotalDuration();
        if (patrolLoop && totalDuration > 0f)
            remaining = Mathf.Repeat(remaining, totalDuration);
        else if (!patrolLoop && remaining >= totalDuration)
            return GetPatrolPointPosition(patrolPoints.Length - 1);

        for (int i = 0; i < patrolPoints.Length; i++)
        {
            DirectedWavePatrolPoint point = patrolPoints[i];
            if (point == null)
                continue;

            float wait = Mathf.Max(0f, point.wait);
            if (remaining <= wait)
                return GetPatrolPointPosition(i);

            remaining -= wait;
            if (i > lastSegmentIndex)
                break;

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
            ? GetPatrolPointPosition(0)
            : GetPatrolPointPosition(patrolPoints.Length - 1);
    }

    private float GetPatrolTotalDuration()
    {
        if (patrolPoints == null || patrolPoints.Length < 2)
            return 0f;

        int lastSegmentIndex = patrolLoop
            ? patrolPoints.Length - 1
            : patrolPoints.Length - 2;
        float totalDuration = 0f;

        for (int i = 0; i < patrolPoints.Length; i++)
        {
            if (patrolPoints[i] == null)
                continue;

            totalDuration += Mathf.Max(0f, patrolPoints[i].wait);
            if (i <= lastSegmentIndex)
            {
                totalDuration += Mathf.Max(
                    0.01f,
                    patrolPoints[i].durationToNext);
            }
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

        Vector3 current = GetPatrolPointPosition(segmentIndex);
        Vector3 next = GetPatrolPointPosition(nextIndex);

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
        Vector3 p0 = GetPatrolPointPosition(segmentIndex);
        Vector3 p3 = GetPatrolPointPosition(GetNextPatrolIndex(segmentIndex));
        Vector3 previous = GetPatrolPointPosition(GetPreviousPatrolIndex(segmentIndex));
        Vector3 following = GetPatrolPointPosition(GetNextPatrolIndex(GetNextPatrolIndex(segmentIndex)));

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
            2f * GetPatrolPointPosition(p1)
            + (-GetPatrolPointPosition(p0) + GetPatrolPointPosition(p2)) * t
            + (2f * GetPatrolPointPosition(p0) - 5f * GetPatrolPointPosition(p1)
                + 4f * GetPatrolPointPosition(p2) - GetPatrolPointPosition(p3))
            * t * t
            + (-GetPatrolPointPosition(p0) + 3f * GetPatrolPointPosition(p1)
                - 3f * GetPatrolPointPosition(p2) + GetPatrolPointPosition(p3))
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

    private Vector3 GetPatrolPointPosition(int index)
    {
        if (patrolPoints == null || patrolPoints.Length == 0)
            return Vector3.zero;

        int safeIndex = Mathf.Clamp(index, 0, patrolPoints.Length - 1);
        DirectedWavePatrolPoint point = patrolPoints[safeIndex];
        return point != null
            ? ToWorld(point.offset, patrolCoordinateSpace)
            : GetStableFormationCenter();
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
            if (IsEnabledPostCommand(postCommands[i]))
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
            if (!IsEnabledPostCommand(command))
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
            if (IsEnabledPostCommand(command))
                names.Add(command.type.ToString());
        }

        return names.Count > 0 ? string.Join(" -> ", names) : "None";
    }

    private bool HasRuntimePostBehavior()
    {
        if (HasAnyPostCommand())
            return true;

        for (int i = 0; i < postTimelineBehaviours.Count; i++)
        {
            IDirectedWavePostTimelineBehaviour behaviour = postTimelineBehaviours[i];
            if (IsAlivePostTimelineBehaviour(behaviour)
                && behaviour.RequiresPostTimeline)
            {
                return true;
            }
        }

        return false;
    }

    private bool HasProceduralFormationEnemyOverrides()
    {
        return GetProceduralFormationEnemyOverrideCount() > 0;
    }

    private int GetProceduralFormationEnemyOverrideCount()
    {
        int count = 0;
        if (proceduralFormationEnemyOverrides == null)
            return count;

        for (int i = 0; i < proceduralFormationEnemyOverrides.Length; i++)
        {
            if (proceduralFormationEnemyOverrides[i] != null)
                count++;
        }

        return count;
    }

    internal void RegisterPostTimelineBehaviour(
        IDirectedWavePostTimelineBehaviour behaviour)
    {
        if (!IsAlivePostTimelineBehaviour(behaviour)
            || postTimelineBehaviours.Contains(behaviour))
        {
            return;
        }

        postTimelineBehaviours.Add(behaviour);
    }

    internal void UnregisterPostTimelineBehaviour(
        IDirectedWavePostTimelineBehaviour behaviour)
    {
        postTimelineBehaviours.Remove(behaviour);
    }

    private void BeginPostTimelineBehaviours()
    {
        for (int i = 0; i < postTimelineBehaviours.Count; i++)
        {
            IDirectedWavePostTimelineBehaviour behaviour = postTimelineBehaviours[i];
            if (IsAlivePostTimelineBehaviour(behaviour))
                behaviour.OnPostTimelineStarted(this);
        }
    }

    private void TickPostTimelineBehaviours()
    {
        for (int i = 0; i < postTimelineBehaviours.Count; i++)
        {
            IDirectedWavePostTimelineBehaviour behaviour = postTimelineBehaviours[i];
            if (IsAlivePostTimelineBehaviour(behaviour))
                behaviour.TickPostTimeline();
        }
    }

    private void StopPostTimelineBehaviours()
    {
        for (int i = postTimelineBehaviours.Count - 1; i >= 0; i--)
        {
            IDirectedWavePostTimelineBehaviour behaviour = postTimelineBehaviours[i];
            if (IsAlivePostTimelineBehaviour(behaviour))
                behaviour.OnPostTimelineStopped();
        }
    }

    private void NotifyPostTimelineEnemyDestroyed(Enemy enemy)
    {
        for (int i = 0; i < postTimelineBehaviours.Count; i++)
        {
            IDirectedWavePostTimelineBehaviour behaviour = postTimelineBehaviours[i];
            if (IsAlivePostTimelineBehaviour(behaviour))
                behaviour.OnWaveEnemyDestroyed(enemy);
        }
    }

    private static bool IsAlivePostTimelineBehaviour(
        IDirectedWavePostTimelineBehaviour behaviour)
    {
        return behaviour != null
            && (!(behaviour is Object unityObject) || unityObject != null);
    }

    private static bool IsEnabledPostCommand(DirectedWavePostCommand command)
    {
        return command != null
            && command.enabled
            && command.type != DirectedWavePostCommandType.LegacyAttack;
    }

    internal Vector3 GetPlayerTargetPosition()
    {
        if (playerController == null)
            return transform.position + Vector3.down;

        ParentShip currentShip = playerController.CurrentShip;
        if (currentShip != null)
            return currentShip.transform.position;

        return playerController.transform.position;
    }

    public bool TryGetFormationIndex(Enemy enemy, out int index)
    {
        if (enemy != null && formationIndices.TryGetValue(enemy, out index))
            return true;

        index = -1;
        return false;
    }

    public int GetConfiguredEnemySlotCount()
    {
        return Mathf.Max(0, GetEffectiveEnemyCount());
    }

    public bool UsesCheckpointEntrancePath => !UsesIndividualEntrancePoints();

    public int GetConfiguredEntranceCheckpointCount()
    {
        if (pathCheckpoints == null)
            return 0;

        int count = 0;
        for (int i = 0; i < pathCheckpoints.Length; i++)
        {
            if (pathCheckpoints[i] != null)
                count++;
        }

        return count;
    }

    public Vector3 GetConfiguredEntranceCheckpointPosition(int checkpointIndex)
    {
        if (checkpointIndex < 0 || pathCheckpoints == null)
            return transform.position;

        int validIndex = 0;
        for (int i = 0; i < pathCheckpoints.Length; i++)
        {
            DirectedWavePathCheckpoint checkpoint = pathCheckpoints[i];
            if (checkpoint == null)
                continue;

            if (validIndex == checkpointIndex)
                return ToWorld(checkpoint.position, pathCoordinateSpace);

            validIndex++;
        }

        return transform.position;
    }

    public Vector3 GetConfiguredFormationSlotPosition(int slotIndex)
    {
        int slotCount = GetConfiguredEnemySlotCount();
        if (slotIndex < 0 || slotIndex >= slotCount)
            return transform.position;

        return GetFormationPosition(slotIndex);
    }

    public Enemy GetConfiguredEnemyPrefabForSlot(int slotIndex)
    {
        int slotCount = GetConfiguredEnemySlotCount();
        return slotIndex >= 0 && slotIndex < slotCount
            ? GetEnemyPrefabForIndex(slotIndex)
            : null;
    }

    private void TryComplete()
    {
        if (!aliveEnemies.CanComplete(spawnFinished))
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
        if (!HasValidEntranceLoopConfiguration())
            DrawFormationGizmos();
    }

    private void DrawPathGizmos()
    {
        if (UsesIndividualEntrancePoints())
        {
            Gizmos.color = new Color(1f, 0.65f, 0.2f, 1f);
            int count = Mathf.Min(
                individualEntrancePoints != null
                    ? individualEntrancePoints.Length
                    : 0,
                GetEffectiveEnemyCount());
            for (int i = 0; i < count; i++)
            {
                if (!TryGetIndividualEntrancePointPosition(
                        i,
                        out Vector3 position))
                {
                    continue;
                }

                Gizmos.DrawSphere(position, 0.08f);
                Gizmos.DrawLine(position, GetFormationPosition(i));
            }

            return;
        }

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
                Vector3 current = DirectedWavePathEvaluator.EvaluateSegment(
                    checkpoints,
                    segment,
                    sample / (float)samplesPerSegment);
                Gizmos.DrawLine(previousCheckpointSample, current);
                previousCheckpointSample = current;
            }
        }

        if (CanUseEntrancePathLoop(checkpoints) && !entranceLoopTeleportToStart)
        {
            int loopStartIndex =
                DirectedWaveEntranceLoopEvaluator.GetLoopStartCheckpointIndex(
                    entranceLoopStartCheckpointIndex,
                    checkpoints.Length);
            int lastIndex = checkpoints.Length - 1;
            Vector3 previousLoopSample = checkpoints[lastIndex].position;
            int previousIndex = Mathf.Max(0, lastIndex - 1);
            int followingIndex = Mathf.Min(
                lastIndex,
                loopStartIndex + 1);

            Gizmos.color = Color.magenta;
            for (int sample = 1; sample <= samplesPerSegment; sample++)
            {
                float normalizedTime = sample / (float)samplesPerSegment;
                Vector3 current =
                    DirectedWaveEntranceLoopEvaluator.EvaluateLoopSegment(
                        checkpoints,
                        previousIndex,
                        lastIndex,
                        loopStartIndex,
                        followingIndex,
                        EvaluateCurve(
                            checkpoints[lastIndex].easeToNext,
                            normalizedTime));
                Gizmos.DrawLine(previousLoopSample, current);
                previousLoopSample = current;
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
        runtimeTimelineEvaluationContext?.Reset();
        runtimeTimelineFrame = null;
        ClearFormationReorderCache();

        enemyCount = Mathf.Max(1, enemyCount);
        spawnInterval = Mathf.Max(0f, spawnInterval);
        settleDuration = Mathf.Max(0f, settleDuration);
        individualPointMovementStartDelay = Mathf.Max(
            0f,
            individualPointMovementStartDelay);
        individualPointMovementDuration = Mathf.Max(
            0f,
            individualPointMovementDuration);
        entranceLoopTeleportDelay = Mathf.Max(0f, entranceLoopTeleportDelay);
        columns = Mathf.Max(1, columns);
        rows = Mathf.Max(1, rows);
        arcRadius = Mathf.Max(0f, arcRadius);
        shapePointCount = Mathf.Max(1, shapePointCount);
        shapeRadius = Mathf.Max(0f, shapeRadius);
        shapeFlattening = new Vector2(
            Mathf.Max(0.01f, shapeFlattening.x),
            Mathf.Max(0.01f, shapeFlattening.y));
        postStartDelay = Mathf.Max(0f, postStartDelay);
        postCommandPipelineFixedCount = Mathf.Max(
            1,
            postCommandPipelineFixedCount);
        localMovementDuration = Mathf.Max(0.01f, localMovementDuration);
        wobbleFrequency = Mathf.Max(0f, wobbleFrequency);
        wobbleDirectionStep = Mathf.Max(0.01f, wobbleDirectionStep);
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
                patrolPoints[i].wait = Mathf.Max(0f, patrolPoints[i].wait);
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

        if (!formationFrozen
            && formationLayout != DirectedWaveFormationLayout.TransformPoints
            && formationLayout != DirectedWaveFormationLayout.CustomPoints)
        {
            EnsureProceduralFormationOverrideCapacity(GetEffectiveEnemyCount());
        }

        if (pathCheckpoints == null)
        {
            entranceLoopStartCheckpointIndex = 0;
            return;
        }

        int validPathCheckpointCount = 0;
        for (int i = 0; i < pathCheckpoints.Length; i++)
        {
            if (pathCheckpoints[i] == null)
                continue;

            validPathCheckpointCount++;

            pathCheckpoints[i].durationToNext =
                Mathf.Max(0.01f, pathCheckpoints[i].durationToNext);
            pathCheckpoints[i].speedToNext =
                Mathf.Max(0.01f, pathCheckpoints[i].speedToNext);
        }

        entranceLoopStartCheckpointIndex =
            DirectedWaveEntranceLoopEvaluator.GetLoopStartCheckpointIndex(
                entranceLoopStartCheckpointIndex,
                validPathCheckpointCount);
    }

    private void EnsureProceduralFormationOverrideCapacity(int requiredCount)
    {
        requiredCount = Mathf.Max(0, requiredCount);

        if (proceduralFormationEnemyOverrides == null)
        {
            proceduralFormationEnemyOverrides = new Enemy[requiredCount];
            return;
        }

        if (proceduralFormationEnemyOverrides.Length < requiredCount)
        {
            System.Array.Resize(
                ref proceduralFormationEnemyOverrides,
                requiredCount);
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

            if (command.type == DirectedWavePostCommandType.LegacyAttack)
                command.enabled = false;

            command.duration = Mathf.Max(0.01f, command.duration);
            command.holdDuration = Mathf.Max(0f, command.holdDuration);
            command.loopCount = Mathf.Max(1, command.loopCount);
            command.formationReorderSpeed = Mathf.Max(
                0.01f,
                command.formationReorderSpeed);
            if (float.IsNaN(command.formationReorderTargetCenter.x)
                || float.IsInfinity(command.formationReorderTargetCenter.x)
                || float.IsNaN(command.formationReorderTargetCenter.y)
                || float.IsInfinity(command.formationReorderTargetCenter.y)
                || float.IsNaN(command.formationReorderTargetCenter.z)
                || float.IsInfinity(command.formationReorderTargetCenter.z))
            {
                command.formationReorderTargetCenter = Vector3.zero;
            }
            command.formationReorderStartInterval = Mathf.Max(
                0f,
                command.formationReorderStartInterval);
            command.formationReorderShipsPerBatch = Mathf.Max(
                1,
                command.formationReorderShipsPerBatch);
            if (command.morphTarget != null)
                ValidateMorphStep(command.morphTarget);

            ValidatePostCommands(command.parallelCommands);
            ValidatePostCommands(command.loopCommands);
        }
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
