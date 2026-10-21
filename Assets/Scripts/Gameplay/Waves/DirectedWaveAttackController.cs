using System.Collections;
using System.Collections.Generic;
using UnityEngine;

internal sealed partial class DirectedWaveAttackController
{
    private static readonly WaitForFixedUpdate DiveFixedStep = new();
    private readonly DirectedEnemySubWave wave;
    private readonly DirectedWaveAttackSettings settings;
    private readonly System.Func<Enemy, bool> isEnemyAllowed;
    private readonly List<Enemy> candidates = new(16);
    private readonly List<Enemy> attackQueue = new(16);
    private readonly List<Enemy> deferredAttackQueue = new(16);
    private readonly Dictionary<Enemy, IWaveAttackExecutor> executors = new();
    private readonly Dictionary<Enemy, EnemyBurstAttackSettings>
        projectileBaseSpeedOverrideSettings = new();
    private readonly Dictionary<Enemy, float> nextAttackReadyTimes = new();
    private readonly List<MonoBehaviour> componentBuffer = new(4);
    private readonly HashSet<Enemy> activeMovementEnemies = new();
    private readonly Dictionary<Enemy, Coroutine> movementRoutines = new();
    private readonly HashSet<Enemy> activeFormationBurstEnemies = new();
    private readonly Dictionary<Enemy, Coroutine> formationBurstRoutines = new();
    private readonly HashSet<Enemy> activeSequentialAttackEnemies = new();

    private int nextQueueIndex;
    private struct PatternAttackState
    {
        public EnemyAttackPatternSchedule Schedule;
        public DirectedWaveAttackAimState Aim;
    }
    private float nextAttackTime;
    private Enemy dispatchClockOwner, pendingReadyEnemy;
    private bool waitingForReady;
    private float DispatchTime => dispatchClockOwner != null ? dispatchClockOwner.AttackTime : Time.time;
    private bool isRunning;
    private bool singleCycle, cyclePrepared;
    private float sequenceSpeed = 1f;
    private EnemyAttackCompletionTracker completionTracker;
    private int completedCycles, cycleLimit = int.MaxValue;
    private bool cycleCounted;
    private float permissionTick = -1f;
    public int CompletedCycles { get { CountCompletedCycle(); return completedCycles; } }
    public bool IsIdle => activeMovementEnemies.Count == 0 && activeFormationBurstEnemies.Count == 0;
    public void SetCycleLimit(int limit) => cycleLimit = limit < 0 ? int.MaxValue : limit;
    public Dictionary<Enemy, float> CaptureAttackCooldowns()
    {
        if (nextAttackReadyTimes.Count == 0) return null;
        var cooldowns = new Dictionary<Enemy, float>(nextAttackReadyTimes.Count);
        foreach (var entry in nextAttackReadyTimes)
            if (entry.Key != null && !entry.Key.isDead) cooldowns.Add(entry.Key, entry.Value);
        return cooldowns;
    }
    public void RestoreAttackCooldowns(Dictionary<Enemy, float> cooldowns)
    {
        if (cooldowns == null) return;
        foreach (var entry in cooldowns)
            if (entry.Key != null && !entry.Key.isDead) nextAttackReadyTimes[entry.Key] = entry.Value;
    }
    private void CountCompletedCycle()
    {
        if (cycleLimit == int.MaxValue && !singleCycle) return;
        if (cycleCounted || !CycleWorkComplete) return;
        cycleCounted = true; completedCycles++;
    }

    public bool HasCompletedSingleCycle
        => singleCycle && CycleWorkComplete;
    private bool CycleWorkComplete
    {
        get
        {
            if (!cyclePrepared
                || (settings.UsesAttackOrder ? !orderPlan.Exhausted
                    : nextQueueIndex < attackQueue.Count || deferredAttackQueue.Count > 0)
                || activeMovementEnemies.Count > 0
                || activeFormationBurstEnemies.Count > 0) return false;
            foreach (var ready in nextAttackReadyTimes)
                if (ready.Key != null && !ready.Key.isDead && ready.Key.isActiveAndEnabled && ready.Key.AttackTime < ready.Value) return false;
            return true;
        }
    }

    public void BeginSingleCycle(float speed, EnemyAttackCompletionTracker tracker)
    {
        Begin(); singleCycle = true; sequenceSpeed = Mathf.Max(.01f, speed); completionTracker = tracker;
    }

    public DirectedWaveAttackController(
        DirectedEnemySubWave wave,
        DirectedWaveAttackSettings settings,
        System.Func<Enemy, bool> isEnemyAllowed = null)
    {
        this.wave = wave;
        this.settings = settings;
        this.isEnemyAllowed = isEnemyAllowed;
        schedulingNumeric = settings?.CreateSchedulingNumeric();
    }

    public void Begin()
    {
        Stop();
        if (settings == null || !settings.IsEnabled)
            return;

        isRunning = true;
        nextAttackTime = Time.time + settings.SampleStartDelay(schedulingNumeric);
    }

    public void Stop()
    {
        completedCycles = 0; cycleLimit = int.MaxValue; cycleCounted = false; permissionTick = -1f;
        isRunning = false;
        singleCycle = cyclePrepared = false; sequenceSpeed = 1f; completionTracker = null;
        attackQueue.Clear();
        deferredAttackQueue.Clear();
        nextAttackReadyTimes.Clear(); numericFallback.Clear();
        projectileBaseSpeedOverrideSettings.Clear();
        nextQueueIndex = 0;
        nextAttackTime = 0f;
        dispatchClockOwner = pendingReadyEnemy = null;
        waitingForReady = false;
        ResetAttackOrder();

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

            if (executor is IEnemyAttackCompletionReceiver completionReceiver)
                completionReceiver.SetAttackCompletionTracker(null);
            executor.SetWaveAttackControl(false);
        }

        executors.Clear();
        componentBuffer.Clear();
        candidates.Clear();
    }

    public void NotifyEnemyDestroyed(Enemy enemy)
    {
        if (ReferenceEquals(dispatchClockOwner, enemy))
        {
            dispatchClockOwner = null;
            waitingForReady = false;
            nextAttackTime = Time.time;
        }
        if (enemy == null)
            return;

        if (executors.TryGetValue(enemy, out IWaveAttackExecutor executor)
            && IsAliveExecutor(executor))
        {
            executor.SetWaveAttackControl(false);
        }

        executors.Remove(enemy);
        projectileBaseSpeedOverrideSettings.Remove(enemy);
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
        var permissions = wave.AttackPatternBehaviour?.AttackPermissions;
        float now = Time.time;
        if (permissions != null && permissions.Interrupted && permissionTick >= 0f && dispatchClockOwner == null)
            nextAttackTime += Mathf.Max(0f, now - permissionTick);
        permissionTick = now;
        CountCompletedCycle();
        if (permissions != null && !permissions.AllowNewAttacks || completedCycles >= cycleLimit) return;
        if (cycleLimit != int.MaxValue && cyclePrepared && !cycleCounted
            && (settings.UsesAttackOrder ? orderPlan.Exhausted : nextQueueIndex >= attackQueue.Count && deferredAttackQueue.Count == 0)) return;
        if (HasCompletedSingleCycle) return;
        if (settings != null && settings.UsesAttackOrder) { TickOrderedAttacks(); return; }
        if (waitingForReady) RefreshReadyDispatch();
        if (!isRunning
            || settings == null
            || !settings.IsEnabled
            || DispatchTime < nextAttackTime)
        {
            return;
        }

        if (settings.UsesDiveMovement
            && settings.RequiresPlayerTarget
            && !wave.HasAttackTarget)
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

        StartEnemyAttack(enemy, executor);
    }

    private void StartEnemyAttack(Enemy enemy, IWaveAttackExecutor executor)
    {
        if (executor is IEnemyEmissionOriginReceiver originReceiver)
            originReceiver.BeginEmissionOriginAttack(GetBurstSettings(enemy, executor));

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

            if (!settings.UsesAttackOrder && !settings.WaitsForPreviousAttack)
                ScheduleNextAttack(enemy);
            return;
        }

        EnemyBurstAttackSettings attackSettings = GetBurstSettings(enemy, executor);
        activeFormationBurstEnemies.Add(enemy);
        BeginSequentialAttack(enemy);
        Coroutine formationRoutine = wave.StartCoroutine(
            RunFormationAttack(enemy, executor, attackSettings));
        if (activeFormationBurstEnemies.Contains(enemy))
            formationBurstRoutines[enemy] = formationRoutine;

        if (!settings.UsesAttackOrder && !settings.WaitsForPreviousAttack)
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
        pendingReadyEnemy = null;

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
                float predicted = Time.time + Mathf.Max(0f, GetAttackReadyTime(candidate) - candidate.AttackTime)
                    / Mathf.Max(0.01f, candidate.AttackSpeedMultiplier);
                if (predicted < nextAttackReadyTime)
                { nextAttackReadyTime = predicted; pendingReadyEnemy = candidate; }
                continue;
            }

            if (!CanUseAttackCandidate(candidate))
                continue;

            IWaveAttackExecutor candidateExecutor = null;
            if (HasFireMode(candidate)
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
        if (singleCycle && cyclePrepared || completedCycles >= cycleLimit) return false;
        cycleCounted = false;
        cyclePrepared = true;
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

        int rounds = settings.SampleAttacksPerCycle(schedulingNumeric);
        for (int round = 0; round < rounds; round++)
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

        EnemyBurstAttackSettings resolvedAttackSettings = ResolveAttackSettings(
            enemy,
            executor,
            out bool setWaveAttackSettings);
        if (executor is IEnemyAttackPatternSettingsReceiver patternReceiver)
            patternReceiver.SetWaveAttackSettings(
                setWaveAttackSettings ? resolvedAttackSettings : null);
        else if (executor is IEnemyShootingSettingsReceiver shootingReceiver)
            shootingReceiver.SetWaveShootingSettings(setWaveAttackSettings
                ? resolvedAttackSettings?.Shooting
                : null);

        if (executor is IEnemyAttackAimReceiver aimReceiver)
            aimReceiver.SetWaveAimTarget(wave);

        if (!executor.CanPerformWaveAttack)
            return false;

        if (executor is IEnemyAttackNumericSource numeric && wave.TryGetFormationIndex(enemy, out int numericSlot)) numeric.SetNumericOwner(numericSlot);
        executor.SetWaveAttackControl(true);
        if (executor is IEnemyAttackCompletionReceiver completionReceiver)
            completionReceiver.SetAttackCompletionTracker(completionTracker);
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

        return !HasFireMode(enemy) || TryGetExecutor(enemy, out _);
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
        bool stopsAtPlayerRadius =
            settings.DiveTargetMode == DirectedWaveDiveTargetMode.StopAtPlayerRadius;
        if (stopsAtPlayerRadius
            && IsInsidePlayerStandoffRadius(startPosition, targetPosition))
        {
            if (HasFireMode(enemy) && CanFire(enemy, executor))
            {
                EnemyBurstAttackSettings attackSettings = GetBurstSettings(enemy, executor);
                yield return FireFullAttack(enemy, executor, attackSettings);
            }

            CompleteSequentialAttack(enemy);
            CompleteMovement(enemy);
            yield break;
        }

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
            targetPosition,
            true);

        if (HasFireMode(enemy) && CanFire(enemy, executor))
        {
            EnemyBurstAttackSettings attackSettings = GetBurstSettings(enemy, executor);
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

        if (HasFireMode(enemy) && CanFire(enemy, executor))
        {
            EnemyBurstAttackSettings attackSettings = GetBurstSettings(enemy, executor);
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
        if (HasFireMode(enemy))
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
        BeginAttackSequence(executor, attackSettings);
        DirectedWaveAttackAimState aim = default;
        try
        {
            if (attackSettings.HasMultiplePatterns)
            {
                yield return FireMultiplePatterns(enemy, executor, attackSettings);
                yield break;
            }
            var numeric = GetNumericRegistry(enemy).Get(attackSettings);
            var schedule = new EnemyAttackPatternSchedule();
            schedule.Begin(attackSettings, enemy.AttackTime, GetFireRateMultiplier(enemy),
                attackSettings.AttackStartDelayRandom.enabled, numeric);
            int events = 0;
            while (!schedule.IsComplete)
            {
                if (attackSettings.UsesRandomTiming && ++events > 128) { events = 0; yield return null; }
                while (CanFire(enemy, executor) && enemy.AttackPermissions != null && enemy.AttackPermissions.Interrupted)
                    yield return null;
                if (!CanFire(enemy, executor)) yield break;
                if (!schedule.IsDue(enemy.AttackTime)) { yield return null; continue; }
                if (!TryFireSequenceShot(enemy, executor, attackSettings, ref aim,
                    attackSettings.RepeatBurst && schedule.IsBurstStart)) yield break;
                schedule.Advance(attackSettings, enemy.AttackTime, GetFireRateMultiplier(enemy), 0f);
            }

            while (executor is IEnemyPendingEmissionExecutor pending && pending.HasPendingEmissions(attackSettings))
            {
                if (!CanFire(enemy, executor)) yield break;
                yield return null;
            }
            RegisterAttackCooldown(enemy, attackSettings);
        }
        finally
        {
            EndAttackSequence(executor, attackSettings);
        }
    }

    private IEnumerator FireMultiplePatterns(Enemy enemy, IWaveAttackExecutor executor,
        EnemyBurstAttackSettings attackSettings)
    {
        var schedules = new PatternAttackState[attackSettings.PatternCount];
        DirectedWaveAttackAimState attackAim = default;
        for (int i = 0; i < schedules.Length; i++)
            schedules[i].Schedule.Begin(attackSettings.GetPattern(i), enemy.AttackTime, GetFireRateMultiplier(enemy), true, GetNumericRegistry(enemy).Get(attackSettings.GetPattern(i)));
        bool pending;
        do
        {
            while (CanFire(enemy, executor) && enemy.AttackPermissions != null && enemy.AttackPermissions.Interrupted)
                yield return null;
            if (!CanFire(enemy, executor))
                yield break;
            pending = false;
            for (int i = 0; i < schedules.Length; i++)
            {
                EnemyBurstAttackSettings pattern = attackSettings.GetPattern(i);
                while (schedules[i].Schedule.IsDue(enemy.AttackTime))
                {
                    bool fired = GetAimMode(enemy) == DirectedWaveAttackAimMode.AimOnStart
                        ? TryFireSequenceShotWithOriginBurst(enemy, executor, pattern, ref attackAim, false,
                            schedules[i].Schedule.IsBurstStart)
                        : TryFireSequenceShot(enemy, executor, pattern, ref schedules[i].Aim,
                            schedules[i].Schedule.IsBurstStart);
                    if (!fired)
                        yield break;
                    schedules[i].Schedule.Advance(pattern, enemy.AttackTime, GetFireRateMultiplier(enemy));
                }
                pending |= !schedules[i].Schedule.IsComplete;
            }
            pending |= executor is IEnemyPendingEmissionExecutor emissions && emissions.HasPendingEmissions(attackSettings);
            if (pending)
                yield return null;
        } while (pending);
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
        Vector3 playerStandoffCenter,
        bool applyDiveLimits = false)
    {
        bool limited = applyDiveLimits && settings.MovesToPlayer
            && (settings.MinimumDiveDuration > 0f || settings.MaximumDiveSpeed > 0f);
        float distance = Vector3.Distance(from, initialTarget);
        var diveProgress = limited
            ? new DirectedWaveDiveMotionProgress(from, distance, speed, settings.MinimumDiveDuration)
            : default;
        float duration = distance <= 0.0001f
            ? 0f
            : distance / Mathf.Max(0.01f, speed);
        if (limited) duration = diveProgress.Duration;
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

        bool fixedStep = limited && wave.UsesFixedAttackMovement(enemy);
        if (fixedStep) yield return DiveFixedStep;
        float elapsed = 0f;
        while ((limited ? !diveProgress.IsComplete : elapsed < duration) && enemy != null && !enemy.isDead)
        {
            float deltaTime = fixedStep ? Time.fixedDeltaTime : Time.deltaTime;
            if (limited && (deltaTime <= 0f || enemy.MovementSpeedMultiplier <= 0f))
            { yield return fixedStep ? DiveFixedStep : null; continue; }
            float normalized;
            if (limited)
                normalized = diveProgress.PrepareStep(deltaTime, enemy.MovementSpeedMultiplier);
            else
            {
                elapsed += Time.deltaTime * enemy.MovementSpeedMultiplier;
                normalized = Mathf.Clamp01(elapsed / duration);
            }
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
            if (limited)
                nextPosition = diveProgress.ApplyStep(nextPosition, deltaTime, settings.MaximumDiveSpeed);

            wave.MoveAttackEnemy(
                enemy,
                nextPosition);
            yield return fixedStep ? DiveFixedStep : null;
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

            if (limited)
            {
                // Nonstandard curves may end before 1. Finish along the same
                // segment without bypassing the speed limit at the stage boundary.
                while (!diveProgress.Position.Equals(target) && enemy != null && !enemy.isDead)
                {
                    float tailSpeed = Mathf.Max(.01f, speed) * enemy.MovementSpeedMultiplier;
                    if (settings.MaximumDiveSpeed > 0f) tailSpeed = Mathf.Min(tailSpeed, settings.MaximumDiveSpeed);
                    float deltaTime = fixedStep ? Time.fixedDeltaTime : Time.deltaTime;
                    if (deltaTime > 0f && tailSpeed > 0f && enemy.MovementSpeedMultiplier > 0f)
                        wave.MoveAttackEnemy(enemy, diveProgress.MovePosition(target, deltaTime, tailSpeed));
                    yield return fixedStep ? DiveFixedStep : null;
                }
            }
            else wave.MoveAttackEnemy(enemy, target);
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

    private bool IsInsidePlayerStandoffRadius(
        Vector3 position,
        Vector3 playerStandoffCenter)
    {
        float radius = settings.PlayerStandoffRadius;
        if (radius <= 0f)
            return false;

        return (position - playerStandoffCenter).sqrMagnitude <= radius * radius;
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
            || !settings.WaitsForPreviousAttack || settings.UsesAttackOrder)
        {
            return;
        }

        float pendingWorldTime = Time.time + Mathf.Max(0f, nextAttackTime - DispatchTime)
            / (dispatchClockOwner != null ? Mathf.Max(0.01f, dispatchClockOwner.AttackSpeedMultiplier) : 1f);
        dispatchClockOwner = null;
        waitingForReady = false;
        nextAttackTime = Mathf.Max(
            pendingWorldTime,
            Time.time + settings.SampleDelayAfterAttack(schedulingNumeric)
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
            || enemy.AttackTime >= readyTime;
    }

    private float GetAttackReadyTime(Enemy enemy)
    {
        return nextAttackReadyTimes.TryGetValue(enemy, out float readyTime)
            ? readyTime
            : enemy.AttackTime;
    }

    private EnemyBurstAttackSettings GetBurstSettings(
        Enemy enemy,
        IWaveAttackExecutor executor)
    {
        return ResolveAttackSettings(enemy, executor, out _);
    }

    private void RegisterAttackCooldown(
        Enemy enemy,
        EnemyBurstAttackSettings attackSettings)
    {
        nextAttackReadyTimes[enemy] = enemy.AttackTime
            + SampleNumericCooldown(enemy, attackSettings)
                / GetFireRateMultiplier(enemy);
    }

    private void RegisterFlyThroughDiveCooldown(Enemy enemy)
    {
        if (enemy == null || enemy.isDead)
            return;

        float diveReadyTime = enemy.AttackTime
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

        waitingForReady = true;
        dispatchClockOwner = pendingReadyEnemy;
        nextAttackTime = pendingReadyEnemy != null ? GetAttackReadyTime(pendingReadyEnemy)
            : Mathf.Max(Time.time, nextAttackReadyTime);
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
        DirectedWaveAttackFireMode fireMode = GetFireMode(enemy, attackSettings);
        if (fireMode == DirectedWaveAttackFireMode.None)
            return false;

        Vector3 forward = executor is IEnemyAttackDirectionProvider directionProvider
            ? directionProvider.AttackForward : enemy.transform.up;
        if (fireMode
            == DirectedWaveAttackFireMode.ForwardWhenPlayerAhead
            && (!wave.HasAttackTarget
                || !IsPlayerInForwardFireSector(
                    enemy,
                    enemy.transform.position,
                    forward,
                    wave.GetPlayerTargetPosition(), attackSettings)))
        {
            return false;
        }

        if (fireMode == DirectedWaveAttackFireMode.Stationary)
        {
            var local = GetAttackOverride(enemy);
            var firing = attackSettings?.Shooting.PresetFiring;
            var fallback = firing != null && firing.enabled ? firing.stationaryFireSettings : settings.StationaryFireSettings;
            var stationary = local != null ? local.ResolveStationaryFireSettings(fallback) : fallback;
            return stationary.TryFire(executor, attackSettings, forward);
        }
        if (fireMode == DirectedWaveAttackFireMode.Default
            || fireMode
                == DirectedWaveAttackFireMode.ForwardWhenPlayerAhead)
        {
            return executor.TryFireInDirection(
                forward,
                attackSettings);
        }

        return executor.TryFireAt(
            wave.GetPlayerTargetPosition(),
            attackSettings);
    }

    private DirectedWaveAttackAimMode GetAimMode(Enemy enemy, EnemyBurstAttackSettings profile = null)
    {
        DirectedWaveEnemyOverride enemyOverride = GetAttackOverride(enemy);
        var firing = profile?.Shooting.PresetFiring;
        var fallback = firing != null && firing.enabled ? firing.aimMode : settings.AimMode;
        return enemyOverride != null ? enemyOverride.ResolveAimMode(fallback) : fallback;
    }

    private bool TryFireSequenceShot(Enemy enemy, IWaveAttackExecutor executor,
        EnemyBurstAttackSettings pattern, ref DirectedWaveAttackAimState aim, bool burstStart)
        => TryFireSequenceShotWithOriginBurst(enemy, executor, pattern, ref aim, burstStart, burstStart);

    private bool TryFireSequenceShotWithOriginBurst(Enemy enemy, IWaveAttackExecutor executor,
        EnemyBurstAttackSettings pattern, ref DirectedWaveAttackAimState aim, bool burstStart, bool originBurstStart)
    {
        if (executor is IEnemyEmissionOriginReceiver originReceiver
            && !originReceiver.PrepareEmissionOriginShot(pattern, originBurstStart)) return false;
        if (GetFireMode(enemy, pattern) != DirectedWaveAttackFireMode.Aimed)
            return TryFireShot(enemy, executor, pattern);
        if (!wave.HasAttackTarget)
            return false;
        DirectedWaveAttackAimMode mode = GetAimMode(enemy, pattern);
        if (mode == DirectedWaveAttackAimMode.AimContinuous)
            return executor.TryFireAt(wave.GetPlayerTargetPosition(), pattern);
        bool refresh = mode == DirectedWaveAttackAimMode.AimOnBurst
            && pattern.RepeatBurst && burstStart;
        return aim.TryFire(enemy, executor, pattern, wave.GetPlayerTargetPosition(), refresh);
    }

    private DirectedWaveEnemyOverride GetAttackOverride(Enemy enemy)
    {
        return enemy != null
            && wave.TryGetFormationIndex(enemy, out int slotIndex)
            ? wave.GetConfiguredEnemyOverrideForSlot(slotIndex)
            : null;
    }

    private DirectedWaveAttackFireMode GetFireMode(Enemy enemy, EnemyBurstAttackSettings profile = null)
    {
        DirectedWaveEnemyOverride enemyOverride = GetAttackOverride(enemy);
        var firing = profile?.Shooting.PresetFiring;
        var fallback = firing != null && firing.enabled ? firing.fireMode : settings.FireMode;
        return enemyOverride != null ? enemyOverride.ResolveFireMode(fallback) : fallback;
    }

    private bool HasFireMode(Enemy enemy)
    {
        return GetFireMode(enemy) != DirectedWaveAttackFireMode.None;
    }

    private bool IsPlayerInForwardFireSector(
        Enemy enemy,
        Vector3 enemyPosition,
        Vector2 forwardDirection,
        Vector3 playerPosition,
        EnemyBurstAttackSettings profile = null)
    {
        DirectedWaveEnemyOverride enemyOverride = GetAttackOverride(enemy);
        var firing = profile?.Shooting.PresetFiring;
        float fallback = firing != null && firing.enabled ? firing.forwardFireHalfAngle : settings.ForwardFireHalfAngle;
        float halfAngle = enemyOverride != null
            ? enemyOverride.ResolveForwardFireHalfAngle(
                fallback)
            : fallback;
        Vector2 directionToPlayer = new(
            playerPosition.x - enemyPosition.x,
            playerPosition.y - enemyPosition.y);
        if (directionToPlayer.sqrMagnitude < 0.0001f)
            return true;

        if (forwardDirection.sqrMagnitude < 0.0001f)
            return false;

        return Vector2.Dot(
            forwardDirection.normalized,
            directionToPlayer.normalized) >= Mathf.Cos(
                halfAngle * Mathf.Deg2Rad);
    }

    private EnemyBurstAttackSettings ResolveAttackSettings(
        Enemy enemy,
        IWaveAttackExecutor executor,
        out bool setWaveAttackSettings)
    {
        return EnemyAttackSettingsResolver.ResolveForWave(
            enemy, executor, settings, GetAttackOverride(enemy),
            projectileBaseSpeedOverrideSettings, out setWaveAttackSettings, sequenceSpeed);
    }

    private static void BeginAttackSequence(
        IWaveAttackExecutor executor,
        EnemyBurstAttackSettings settings)
    {
        if (executor is IEnemyAttackSequenceExecutor sequenceExecutor)
            sequenceExecutor.BeginAttackSequence(settings);
    }

    private static void EndAttackSequence(
        IWaveAttackExecutor executor,
        EnemyBurstAttackSettings settings)
    {
        if (executor is IEnemyAttackSequenceExecutor sequenceExecutor)
            sequenceExecutor.EndAttackSequence(settings);
    }

    private void ScheduleNextAttack(Enemy enemy)
    {
        waitingForReady = false;
        dispatchClockOwner = enemy;
        nextAttackTime = DispatchTime + 1f / (
            settings.SampleAttacksPerSecond(schedulingNumeric) * GetFireRateMultiplier(enemy));
    }

    private void RefreshReadyDispatch()
    {
        float earliest = float.PositiveInfinity;
        Enemy selected = null;
        foreach (var entry in nextAttackReadyTimes)
        {
            Enemy enemy = entry.Key;
            if (enemy == null || enemy.isDead || IsAttackInProgress(enemy)) continue;
            float remaining = Mathf.Max(0f, entry.Value - enemy.AttackTime)
                / Mathf.Max(0.01f, enemy.AttackSpeedMultiplier);
            if (remaining >= earliest) continue;
            earliest = remaining;
            selected = enemy;
        }
        if (selected == null) return;
        dispatchClockOwner = selected;
        nextAttackTime = GetAttackReadyTime(selected);
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
