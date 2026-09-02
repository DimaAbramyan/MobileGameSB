using System.Collections;
using System.Collections.Generic;
using UnityEngine;

internal sealed class DirectedWaveAttackController
{
    private readonly DirectedEnemySubWave wave;
    private readonly DirectedWaveAttackSettings settings;
    private readonly System.Func<Enemy, bool> isEnemyAllowed;
    private readonly List<Enemy> candidates = new(16);
    private readonly List<Enemy> attackQueue = new(16);
    private readonly List<Enemy> deferredAttackQueue = new(16);
    private readonly Dictionary<Enemy, IWaveAttackExecutor> executors = new();
    private readonly Dictionary<Enemy, float> nextAttackReadyTimes = new();
    private readonly List<MonoBehaviour> componentBuffer = new(4);
    private readonly HashSet<Enemy> activeMovementEnemies = new();
    private readonly Dictionary<Enemy, Coroutine> movementRoutines = new();
    private readonly HashSet<Enemy> activeFormationBurstEnemies = new();
    private readonly Dictionary<Enemy, Coroutine> formationBurstRoutines = new();
    private readonly HashSet<Enemy> activeSequentialAttackEnemies = new();

    private int nextQueueIndex;
    private float nextAttackTime;
    private bool isRunning;

    public DirectedWaveAttackController(
        DirectedEnemySubWave wave,
        DirectedWaveAttackSettings settings,
        System.Func<Enemy, bool> isEnemyAllowed = null)
    {
        this.wave = wave;
        this.settings = settings;
        this.isEnemyAllowed = isEnemyAllowed;
    }

    public void Begin()
    {
        Stop();
        if (settings == null || !settings.IsEnabled)
            return;

        isRunning = true;
        nextAttackTime = Time.time + settings.AttackStartDelay;
    }

    public void Stop()
    {
        isRunning = false;
        attackQueue.Clear();
        deferredAttackQueue.Clear();
        nextAttackReadyTimes.Clear();
        nextQueueIndex = 0;
        nextAttackTime = 0f;

        foreach (Coroutine routine in movementRoutines.Values)
        {
            if (routine != null)
                wave.StopCoroutine(routine);
        }

        foreach (Enemy enemy in activeMovementEnemies)
        {
            if (enemy != null)
                wave.SetTimelineEnemyDetached(enemy, false);
        }

        movementRoutines.Clear();
        activeMovementEnemies.Clear();

        foreach (Coroutine routine in formationBurstRoutines.Values)
        {
            if (routine != null)
                wave.StopCoroutine(routine);
        }

        formationBurstRoutines.Clear();
        activeFormationBurstEnemies.Clear();
        activeSequentialAttackEnemies.Clear();
        foreach (IWaveAttackExecutor executor in executors.Values)
        {
            if (!IsAliveExecutor(executor))
                continue;

            executor.SetWaveAttackControl(false);
        }

        executors.Clear();
        componentBuffer.Clear();
        candidates.Clear();
    }

    public void NotifyEnemyDestroyed(Enemy enemy)
    {
        if (enemy == null)
            return;

        if (executors.TryGetValue(enemy, out IWaveAttackExecutor executor)
            && IsAliveExecutor(executor))
        {
            executor.SetWaveAttackControl(false);
        }

        executors.Remove(enemy);
        nextAttackReadyTimes.Remove(enemy);
        CompleteSequentialAttack(enemy);

        if (movementRoutines.TryGetValue(enemy, out Coroutine movementRoutine)
            && movementRoutine != null)
        {
            wave.StopCoroutine(movementRoutine);
        }

        movementRoutines.Remove(enemy);
        if (activeMovementEnemies.Remove(enemy))
            wave.SetTimelineEnemyDetached(enemy, false);

        StopFormationBurst(enemy);
    }

    public void Tick()
    {
        if (!isRunning
            || settings == null
            || !settings.IsEnabled
            || Time.time < nextAttackTime)
        {
            return;
        }

        if (settings.RequiresPlayerTarget && !wave.HasAttackTarget)
        {
            return;
        }

        if (settings.UsesDiveMovement
            && !settings.AllowsConcurrentMovements
            && activeMovementEnemies.Count > 0)
        {
            return;
        }

        if (settings.WaitsForPreviousAttack
            && activeSequentialAttackEnemies.Count > 0)
        {
            return;
        }

        if (!TryTakeNextExecutor(
                out Enemy enemy,
                out IWaveAttackExecutor executor,
                out float nextAttackReadyTime))
        {
            ScheduleNextReadyAttack(nextAttackReadyTime);
            return;
        }

        if (settings.UsesDiveMovement)
        {
            activeMovementEnemies.Add(enemy);
            BeginSequentialAttack(enemy);
            wave.SetTimelineEnemyDetached(enemy, true);
            Coroutine movementRoutine = wave.StartCoroutine(
                settings.UsesFlyThroughDive
                    ? RunFlyThroughDiveAndShoot(enemy, executor)
                    : RunMoveToPlayerAndShoot(enemy, executor));
            if (activeMovementEnemies.Contains(enemy))
                movementRoutines[enemy] = movementRoutine;

            if (!settings.WaitsForPreviousAttack)
                ScheduleNextAttack(enemy);
            return;
        }

        EnemyBurstAttackSettings attackSettings = GetBurstSettings(executor);
        activeFormationBurstEnemies.Add(enemy);
        BeginSequentialAttack(enemy);
        Coroutine formationRoutine = wave.StartCoroutine(
            RunFormationAttack(enemy, executor, attackSettings));
        if (activeFormationBurstEnemies.Contains(enemy))
            formationBurstRoutines[enemy] = formationRoutine;

        if (!settings.WaitsForPreviousAttack)
            ScheduleNextAttack(enemy);
    }

    private bool TryTakeNextExecutor(
        out Enemy enemy,
        out IWaveAttackExecutor executor,
        out float nextAttackReadyTime)
    {
        enemy = null;
        executor = null;
        nextAttackReadyTime = float.PositiveInfinity;

        PrunePendingQueueEntries();
        PruneDeferredAttackQueueEntries();
        if (nextQueueIndex >= attackQueue.Count)
        {
            if (deferredAttackQueue.Count > 0)
                RefillAttackQueueFromDeferredAttacks();
            else if (!BuildAttackCycle())
                return false;
        }

        int remainingCandidates = attackQueue.Count - nextQueueIndex;
        for (int i = 0; i < remainingCandidates; i++)
        {
            Enemy candidate = attackQueue[nextQueueIndex++];
            if (IsAttackInProgress(candidate))
            {
                deferredAttackQueue.Add(candidate);
                continue;
            }

            if (!IsAttackReady(candidate))
            {
                deferredAttackQueue.Add(candidate);
                nextAttackReadyTime = Mathf.Min(
                    nextAttackReadyTime,
                    GetAttackReadyTime(candidate));
                continue;
            }

            if (!CanUseAttackCandidate(candidate))
                continue;

            IWaveAttackExecutor candidateExecutor = null;
            if (settings.HasFireMode
                && !TryGetExecutor(candidate, out candidateExecutor))
            {
                continue;
            }

            enemy = candidate;
            executor = candidateExecutor;
            return true;
        }

        if (deferredAttackQueue.Count > 0)
            RefillAttackQueueFromDeferredAttacks();

        return false;
    }

    private bool BuildAttackCycle()
    {
        attackQueue.Clear();
        nextQueueIndex = 0;
        wave.GetWaveAttackCandidates(candidates);

        int eligibleCount = 0;
        for (int i = 0; i < candidates.Count; i++)
        {
            Enemy candidate = candidates[i];
            if (isEnemyAllowed != null && !isEnemyAllowed(candidate))
                continue;

            if (!CanUseAttackCandidate(candidate))
                continue;

            candidates[eligibleCount++] = candidate;
        }

        if (eligibleCount == 0)
            return false;

        for (int round = 0; round < settings.AttacksPerEnemyPerCycle; round++)
        {
            int roundStart = attackQueue.Count;
            for (int i = 0; i < eligibleCount; i++)
                attackQueue.Add(candidates[i]);

            ShuffleQueueRange(roundStart, eligibleCount);
        }

        return true;
    }

    private void PrunePendingQueueEntries()
    {
        int writeIndex = nextQueueIndex;
        for (int readIndex = nextQueueIndex;
             readIndex < attackQueue.Count;
             readIndex++)
        {
            Enemy candidate = attackQueue[readIndex];
            if (!CanUseAttackCandidate(candidate))
                continue;

            attackQueue[writeIndex++] = candidate;
        }

        if (writeIndex < attackQueue.Count)
            attackQueue.RemoveRange(writeIndex, attackQueue.Count - writeIndex);
    }

    private void PruneDeferredAttackQueueEntries()
    {
        int writeIndex = 0;
        for (int readIndex = 0;
             readIndex < deferredAttackQueue.Count;
             readIndex++)
        {
            Enemy candidate = deferredAttackQueue[readIndex];
            if (!CanUseAttackCandidate(candidate))
                continue;

            deferredAttackQueue[writeIndex++] = candidate;
        }

        if (writeIndex < deferredAttackQueue.Count)
        {
            deferredAttackQueue.RemoveRange(
                writeIndex,
                deferredAttackQueue.Count - writeIndex);
        }
    }

    private void RefillAttackQueueFromDeferredAttacks()
    {
        attackQueue.Clear();
        attackQueue.AddRange(deferredAttackQueue);
        deferredAttackQueue.Clear();
        nextQueueIndex = 0;
    }

    private bool TryGetExecutor(
        Enemy enemy,
        out IWaveAttackExecutor executor)
    {
        executor = null;
        if (enemy == null
            || enemy.isDead
            || !enemy.isActiveAndEnabled
            || (isEnemyAllowed != null && !isEnemyAllowed(enemy)))
        return false;

        if (!executors.TryGetValue(enemy, out executor)
            || !IsAliveExecutor(executor))
        {
            executor = FindExecutor(enemy);
            if (executor == null)
            {
                executors.Remove(enemy);
                return false;
            }

            executors[enemy] = executor;
        }

        if (!executor.CanPerformWaveAttack)
            return false;

        executor.SetWaveAttackControl(true);
        return true;
    }

    private IWaveAttackExecutor FindExecutor(Enemy enemy)
    {
        componentBuffer.Clear();
        enemy.GetComponents<MonoBehaviour>(componentBuffer);
        for (int i = 0; i < componentBuffer.Count; i++)
        {
            if (componentBuffer[i] is IWaveAttackExecutor executor)
                return executor;
        }

        return null;
    }

    private bool CanUseAttackCandidate(Enemy enemy)
    {
        if (enemy == null
            || enemy.isDead
            || !enemy.isActiveAndEnabled
            || (isEnemyAllowed != null && !isEnemyAllowed(enemy)))
        {
            return false;
        }

        return !settings.HasFireMode || TryGetExecutor(enemy, out _);
    }

    private IEnumerator RunMoveToPlayerAndShoot(
        Enemy enemy,
        IWaveAttackExecutor executor)
    {
        if (enemy == null || enemy.isDead)
        {
            CompleteSequentialAttack(enemy);
            CompleteMovement(enemy);
            yield break;
        }

        Vector3 startPosition = enemy.transform.position;
        Vector3 targetPosition = wave.GetPlayerTargetPosition();
        Vector3 direction = targetPosition - startPosition;
        if (direction.sqrMagnitude < 0.0001f)
            direction = Vector3.down;
        else
            direction.Normalize();

        yield return RunDivePreparation(enemy, direction);
        if (enemy == null || enemy.isDead)
        {
            CompleteSequentialAttack(enemy);
            CompleteMovement(enemy);
            yield break;
        }

        float diveSpeed = settings.GetRandomDiveSpeed();
        bool stopsAtPlayerRadius =
            settings.DiveTargetMode == DirectedWaveDiveTargetMode.StopAtPlayerRadius;
        Vector3 diveEndPosition = stopsAtPlayerRadius
            ? GetPlayerStandoffPosition(startPosition, targetPosition)
            : targetPosition + direction * settings.GetRandomDiveDepth();
        yield return MoveEnemy(
            enemy,
            enemy.transform.position,
            diveEndPosition,
            diveSpeed,
            settings.DiveSpeedCurve,
            false,
            stopsAtPlayerRadius,
            targetPosition);

        if (settings.HasFireMode && CanFire(enemy, executor))
        {
            EnemyBurstAttackSettings attackSettings = GetBurstSettings(executor);
            yield return FireFullAttack(enemy, executor, attackSettings);
        }

        CompleteSequentialAttack(enemy);

        if (enemy != null && !enemy.isDead)
        {
            Vector3 returnTarget = wave.GetTimelineReturnPosition(enemy);
            yield return MoveEnemy(
                enemy,
                enemy.transform.position,
                returnTarget,
                diveSpeed * settings.ReturnSpeedMultiplier,
                settings.ReturnSpeedCurve,
                true,
                false,
                Vector3.zero);
        }

        CompleteMovement(enemy);
    }

    private IEnumerator RunFlyThroughDiveAndShoot(
        Enemy enemy,
        IWaveAttackExecutor executor)
    {
        if (enemy == null || enemy.isDead)
        {
            CompleteSequentialAttack(enemy);
            CompleteMovement(enemy);
            yield break;
        }

        Vector3 startPosition = enemy.transform.position;
        Vector3 playerSnapshot = wave.GetPlayerTargetPosition();
        Vector3 direction = playerSnapshot - startPosition;
        if (direction.sqrMagnitude < 0.0001f)
            direction = Vector3.down;
        else
            direction.Normalize();

        yield return RunDivePreparation(enemy, direction);
        if (enemy == null || enemy.isDead)
        {
            CompleteSequentialAttack(enemy);
            CompleteMovement(enemy);
            yield break;
        }

        Vector3 diveStartPosition = enemy.transform.position;

        if (settings.HasFireMode && CanFire(enemy, executor))
        {
            EnemyBurstAttackSettings attackSettings = GetBurstSettings(executor);
            activeFormationBurstEnemies.Add(enemy);
            Coroutine fireRoutine = wave.StartCoroutine(
                RunFlyThroughAttack(enemy, executor, attackSettings));
            if (activeFormationBurstEnemies.Contains(enemy))
                formationBurstRoutines[enemy] = fireRoutine;
        }

        Vector3 exitPosition = GetFlyThroughExitPosition(
            playerSnapshot,
            direction,
            startPosition.z);
        yield return MoveEnemy(
            enemy,
            enemy.transform.position,
            exitPosition,
            settings.FlyThroughApproachSpeed,
            settings.DiveSpeedCurve,
            false,
            false,
            Vector3.zero);

        if (enemy != null && !enemy.isDead)
        {
            switch (settings.FlyThroughReturnMode)
            {
                case DirectedWaveFlyThroughReturnMode.ReverseDivePath:
                    yield return ReturnAlongReverseDivePath(
                        enemy,
                        diveStartPosition,
                        startPosition);
                    break;

                case DirectedWaveFlyThroughReturnMode.TeleportPosition:
                    Vector3 teleportPosition = settings
                        .GetFlyThroughReturnTeleportPosition(startPosition.z);
                    wave.MoveAttackEnemy(enemy, teleportPosition);
                    yield return MoveEnemy(
                        enemy,
                        teleportPosition,
                        wave.GetTimelineReturnPosition(enemy),
                        settings.FlyThroughApproachSpeed
                            * settings.ReturnSpeedMultiplier,
                        settings.ReturnSpeedCurve,
                        true,
                        false,
                        Vector3.zero);
                    break;

                default:
                    yield return wave.ReturnAttackEnemyAlongEntrancePath(
                        enemy,
                        settings.ReturnSpeedMultiplier);
                    break;
            }
        }

        while (enemy != null
            && !enemy.isDead
            && activeFormationBurstEnemies.Contains(enemy))
        {
            yield return null;
        }

        RegisterFlyThroughDiveCooldown(enemy);
        CompleteSequentialAttack(enemy);
        CompleteMovement(enemy);
    }

    private IEnumerator ReturnAlongReverseDivePath(
        Enemy enemy,
        Vector3 diveStartPosition,
        Vector3 startPosition)
    {
        yield return MoveEnemy(
            enemy,
            enemy.transform.position,
            diveStartPosition,
            settings.FlyThroughApproachSpeed * settings.ReturnSpeedMultiplier,
            settings.ReturnSpeedCurve,
            false,
            false,
            Vector3.zero);

        if (enemy != null
            && !enemy.isDead
            && Vector3.Distance(diveStartPosition, startPosition) > 0.0001f)
        {
            yield return MoveEnemy(
                enemy,
                diveStartPosition,
                startPosition,
                settings.FlyThroughApproachSpeed
                    * settings.ReturnSpeedMultiplier,
                settings.ReturnSpeedCurve,
                false,
                false,
                Vector3.zero);
        }
    }

    private IEnumerator RunDivePreparation(
        Enemy enemy,
        Vector3 diveDirection)
    {
        if (!settings.UsesDivePreparation
            || enemy == null
            || enemy.isDead)
        {
            yield break;
        }

        if (diveDirection.sqrMagnitude < 0.0001f)
            diveDirection = Vector3.down;
        else
            diveDirection.Normalize();

        float distance = settings.DivePreparationDistance;
        float speed = distance / settings.DivePreparationDuration;
        Vector3 startPosition = enemy.transform.position;
        Vector3 preparationTarget = startPosition - diveDirection * distance;
        yield return MoveEnemy(
            enemy,
            startPosition,
            preparationTarget,
            speed,
            settings.DivePreparationSpeedCurve,
            false,
            false,
            Vector3.zero);
    }

    private IEnumerator RunFlyThroughAttack(
        Enemy enemy,
        IWaveAttackExecutor executor,
        EnemyBurstAttackSettings attackSettings)
    {
        yield return FireFullAttack(enemy, executor, attackSettings);
        CompleteFormationBurst(enemy);
    }

    private IEnumerator RunFormationAttack(
        Enemy enemy,
        IWaveAttackExecutor executor,
        EnemyBurstAttackSettings attackSettings)
    {
        if (settings.HasFireMode)
        {
            yield return FireFullAttack(
                enemy,
                executor,
                attackSettings);
        }
        CompleteSequentialAttack(enemy);
        CompleteFormationBurst(enemy);
    }

    private IEnumerator FireFullAttack(
        Enemy enemy,
        IWaveAttackExecutor executor,
        EnemyBurstAttackSettings attackSettings)
    {
        int attackShotCount = attackSettings.GetAttackShotCountForFireRate(
            GetFireRateMultiplier(enemy));
        for (int attackShotIndex = 0;
             attackShotIndex < attackShotCount;
             attackShotIndex++)
        {
            if (attackShotIndex > 0)
            {
                float nextAttackShotTime = Time.time
                    + (attackSettings.RepeatBurst
                        ? attackSettings.AttackShotInterval
                        : attackSettings.AttackShotInterval
                            / GetFireRateMultiplier(enemy));
                while (Time.time < nextAttackShotTime)
                {
                    if (!CanFire(enemy, executor))
                        yield break;

                    yield return null;
                }
            }

            if (!attackSettings.RepeatBurst)
            {
                if (!CanFire(enemy, executor)
                    || !TryFireShot(enemy, executor, attackSettings))
                {
                    yield break;
                }

                continue;
            }

            for (int burstShotIndex = 0;
                 burstShotIndex < attackSettings.BurstShotCount;
                 burstShotIndex++)
            {
                if (burstShotIndex > 0)
                {
                    float nextBurstShotTime = Time.time
                        + attackSettings.BurstShotInterval;
                    while (Time.time < nextBurstShotTime)
                    {
                        if (!CanFire(enemy, executor))
                            yield break;

                        yield return null;
                    }
                }

                if (!CanFire(enemy, executor)
                    || !TryFireShot(enemy, executor, attackSettings))
                {
                    yield break;
                }
            }
        }

        RegisterAttackCooldown(enemy, attackSettings);
    }

    private IEnumerator MoveEnemy(
        Enemy enemy,
        Vector3 from,
        Vector3 initialTarget,
        float speed,
        AnimationCurve speedCurve,
        bool trackTimelineTarget,
        bool keepPlayerStandoff,
        Vector3 playerStandoffCenter)
    {
        float distance = Vector3.Distance(from, initialTarget);
        float duration = distance <= 0.0001f
            ? 0f
            : distance / Mathf.Max(0.01f, speed);
        if (duration <= 0f)
        {
            if (enemy != null && !enemy.isDead)
            {
                Vector3 target = GetMoveTarget(
                    enemy,
                    initialTarget,
                    trackTimelineTarget);
                if (keepPlayerStandoff)
                {
                    target = ClampToPlayerStandoffRadius(
                        target,
                        target,
                        playerStandoffCenter);
                }

                wave.MoveAttackEnemy(enemy, target);
            }

            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration && enemy != null && !enemy.isDead)
        {
            elapsed += Time.deltaTime;
            float normalized = Mathf.Clamp01(elapsed / duration);
            float curved = EvaluateCurve(speedCurve, normalized);
            Vector3 target = GetMoveTarget(
                enemy,
                initialTarget,
                trackTimelineTarget);
            Vector3 nextPosition = Vector3.LerpUnclamped(from, target, curved);
            if (keepPlayerStandoff)
            {
                nextPosition = ClampToPlayerStandoffRadius(
                    nextPosition,
                    target,
                    playerStandoffCenter);
            }

            wave.MoveAttackEnemy(
                enemy,
                nextPosition);
            yield return null;
        }

        if (enemy != null && !enemy.isDead)
        {
            Vector3 target = GetMoveTarget(
                enemy,
                initialTarget,
                trackTimelineTarget);
            if (keepPlayerStandoff)
            {
                target = ClampToPlayerStandoffRadius(
                    target,
                    target,
                    playerStandoffCenter);
            }

            wave.MoveAttackEnemy(enemy, target);
        }
    }

    private Vector3 GetMoveTarget(
        Enemy enemy,
        Vector3 initialTarget,
        bool trackTimelineTarget)
    {
        if (trackTimelineTarget)
            return wave.GetTimelineReturnPosition(enemy);

        return initialTarget;
    }

    private Vector3 GetFlyThroughExitPosition(
        Vector3 playerSnapshot,
        Vector3 direction,
        float worldZ)
    {
        Camera gameplayCamera = Camera.main;
        if (gameplayCamera == null)
        {
            return playerSnapshot + direction * Mathf.Max(
                settings.FlyThroughExitPadding,
                settings.GetRandomDiveDepth());
        }

        float cameraDistance = Mathf.Abs(
            worldZ - gameplayCamera.transform.position.z);
        Vector3 bottomLeft = gameplayCamera.ViewportToWorldPoint(
            new Vector3(0f, 0f, cameraDistance));
        Vector3 topRight = gameplayCamera.ViewportToWorldPoint(
            new Vector3(1f, 1f, cameraDistance));
        float edgeDistance = GetRayExitDistance(
            playerSnapshot,
            direction,
            bottomLeft,
            topRight);
        if (float.IsPositiveInfinity(edgeDistance))
            edgeDistance = settings.GetRandomDiveDepth();

        return playerSnapshot + direction * (
            Mathf.Max(0f, edgeDistance) + settings.FlyThroughExitPadding);
    }

    private static float GetRayExitDistance(
        Vector3 origin,
        Vector3 direction,
        Vector3 bottomLeft,
        Vector3 topRight)
    {
        const float DirectionEpsilon = 0.0001f;
        float distance = float.PositiveInfinity;

        if (direction.x > DirectionEpsilon)
            distance = Mathf.Min(distance, (topRight.x - origin.x) / direction.x);
        else if (direction.x < -DirectionEpsilon)
            distance = Mathf.Min(distance, (bottomLeft.x - origin.x) / direction.x);

        if (direction.y > DirectionEpsilon)
            distance = Mathf.Min(distance, (topRight.y - origin.y) / direction.y);
        else if (direction.y < -DirectionEpsilon)
            distance = Mathf.Min(distance, (bottomLeft.y - origin.y) / direction.y);

        return distance >= 0f ? distance : float.PositiveInfinity;
    }

    private Vector3 GetPlayerStandoffPosition(
        Vector3 referencePosition,
        Vector3 playerPosition)
    {
        Vector3 direction = referencePosition - playerPosition;
        if (direction.sqrMagnitude < 0.0001f)
            direction = Vector3.up;

        return playerPosition
            + direction.normalized * settings.PlayerStandoffRadius;
    }

    private Vector3 ClampToPlayerStandoffRadius(
        Vector3 position,
        Vector3 fallbackPosition,
        Vector3 playerStandoffCenter)
    {
        float radius = settings.PlayerStandoffRadius;
        if (radius <= 0f)
            return position;

        Vector3 direction = position - playerStandoffCenter;
        float radiusSqr = radius * radius;
        if (direction.sqrMagnitude >= radiusSqr)
            return position;

        if (direction.sqrMagnitude < 0.0001f)
        {
            direction = fallbackPosition - playerStandoffCenter;
            if (direction.sqrMagnitude < 0.0001f)
                direction = Vector3.up;
        }

        return playerStandoffCenter + direction.normalized * radius;
    }

    private void CompleteMovement(Enemy enemy)
    {
        if (enemy != null)
        {
            wave.SetTimelineEnemyDetached(enemy, false);
            activeMovementEnemies.Remove(enemy);
            movementRoutines.Remove(enemy);
        }
    }

    private void CompleteFormationBurst(Enemy enemy)
    {
        if (enemy == null)
            return;

        activeFormationBurstEnemies.Remove(enemy);
        formationBurstRoutines.Remove(enemy);
    }

    private void BeginSequentialAttack(Enemy enemy)
    {
        if (!settings.WaitsForPreviousAttack || enemy == null)
            return;

        activeSequentialAttackEnemies.Add(enemy);
    }

    private void CompleteSequentialAttack(Enemy enemy)
    {
        if (ReferenceEquals(enemy, null)
            || !activeSequentialAttackEnemies.Remove(enemy)
            || !settings.WaitsForPreviousAttack)
        {
            return;
        }

        nextAttackTime = Mathf.Max(
            nextAttackTime,
            Time.time + settings.DelayAfterAttack
                / GetFireRateMultiplier(enemy));
    }

    private void StopFormationBurst(Enemy enemy)
    {
        if (enemy == null)
            return;

        if (formationBurstRoutines.TryGetValue(enemy, out Coroutine routine)
            && routine != null)
        {
            wave.StopCoroutine(routine);
        }

        formationBurstRoutines.Remove(enemy);
        activeFormationBurstEnemies.Remove(enemy);
    }

    private bool IsAttackInProgress(Enemy enemy)
    {
        return activeMovementEnemies.Contains(enemy)
            || activeFormationBurstEnemies.Contains(enemy);
    }

    private bool IsAttackReady(Enemy enemy)
    {
        return !nextAttackReadyTimes.TryGetValue(enemy, out float readyTime)
            || Time.time >= readyTime;
    }

    private float GetAttackReadyTime(Enemy enemy)
    {
        return nextAttackReadyTimes.TryGetValue(enemy, out float readyTime)
            ? readyTime
            : Time.time;
    }

    private EnemyBurstAttackSettings GetBurstSettings(
        IWaveAttackExecutor executor)
    {
        if (settings.UsesEnemyBurstSettings
            && executor is IEnemyBurstAttackExecutor burstExecutor
            && burstExecutor.BurstAttackSettings != null)
        {
            return burstExecutor.BurstAttackSettings;
        }

        return settings.WaveBurstSettings;
    }

    private void RegisterAttackCooldown(
        Enemy enemy,
        EnemyBurstAttackSettings attackSettings)
    {
        nextAttackReadyTimes[enemy] = Time.time
            + settings.ResolveAttackCooldown(attackSettings.AttackCooldown)
                / GetFireRateMultiplier(enemy);
    }

    private void RegisterFlyThroughDiveCooldown(Enemy enemy)
    {
        if (enemy == null || enemy.isDead)
            return;

        float diveReadyTime = Time.time
            + settings.FlyThroughDiveCooldown / GetFireRateMultiplier(enemy);
        float attackReadyTime = GetAttackReadyTime(enemy);
        nextAttackReadyTimes[enemy] = Mathf.Max(diveReadyTime, attackReadyTime);
    }

    private void ScheduleNextReadyAttack(float nextAttackReadyTime)
    {
        if (float.IsPositiveInfinity(nextAttackReadyTime)
            || activeMovementEnemies.Count > 0
            || activeFormationBurstEnemies.Count > 0)
        {
            return;
        }

        nextAttackTime = Mathf.Max(Time.time, nextAttackReadyTime);
    }

    private static bool CanFire(
        Enemy enemy,
        IWaveAttackExecutor executor)
    {
        return enemy != null
            && !enemy.isDead
            && IsAliveExecutor(executor)
            && executor.CanPerformWaveAttack;
    }

    private bool TryFireShot(
        Enemy enemy,
        IWaveAttackExecutor executor,
        EnemyBurstAttackSettings attackSettings)
    {
        if (!settings.HasFireMode)
            return false;

        if (settings.FireMode == DirectedWaveAttackFireMode.Forward)
        {
            return executor.TryFireInDirection(
                enemy.transform.up,
                attackSettings);
        }

        return executor.TryFireAt(
            wave.GetPlayerTargetPosition(),
            attackSettings);
    }

    private void ScheduleNextAttack(Enemy enemy)
    {
        nextAttackTime = Time.time + 1f / (
            settings.AttacksPerSecond * GetFireRateMultiplier(enemy));
    }

    private static float GetFireRateMultiplier(Enemy enemy)
    {
        return enemy != null ? enemy.FireRateMultiplier : 1f;
    }

    private void ShuffleQueueRange(int startIndex, int count)
    {
        for (int index = startIndex + count - 1; index > startIndex; index--)
        {
            int swapIndex = Random.Range(startIndex, index + 1);
            (attackQueue[index], attackQueue[swapIndex]) =
                (attackQueue[swapIndex], attackQueue[index]);
        }
    }

    private static float EvaluateCurve(AnimationCurve curve, float value)
    {
        return curve != null
            ? Mathf.Clamp01(curve.Evaluate(value))
            : value;
    }

    private static bool IsAliveExecutor(IWaveAttackExecutor executor)
    {
        return executor != null
            && (!(executor is Object unityObject) || unityObject != null);
    }
}
