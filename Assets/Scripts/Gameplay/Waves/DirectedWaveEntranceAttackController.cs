using System.Collections.Generic;
using UnityEngine;

internal sealed class DirectedWaveEntranceAttackController
{
    private const float MinimumTraversalDuration = 0.01f;

    private readonly DirectedEnemySubWave wave;
    private readonly DirectedWaveAttackSettings sharedAttackSettings;
    private readonly DirectedWaveEntranceAttackSettings settings;
    private readonly List<MonoBehaviour> componentBuffer = new(4);
    private readonly Dictionary<Enemy, IWaveAttackExecutor> executors = new();
    private readonly Dictionary<Enemy, EnemyBurstAttackSettings>
        projectileBaseSpeedOverrideSettings = new();
    private readonly HashSet<Enemy> controlledEnemies = new();
    private readonly List<PerEnemyAttackSchedule> perEnemySchedules = new(16);
    private readonly List<GroupAttackSchedule> groupSchedules = new(8);
    private readonly List<GroupAttackCandidate> groupCandidates = new(16);

    private bool isRunning;
    private bool missingTargetWarningLogged;

    private struct PerEnemyAttackSchedule
    {
        public Enemy enemy;
        public int ruleIndex;
        public float elapsed;
        public float duration;
        public int nextShotIndex;
        public DirectedWaveAttackAimState aim;
    }

    private struct GroupAttackSchedule
    {
        public int ruleIndex;
        public bool isRunning;
        public float startTime;
        public float duration;
        public int nextShotIndex;
        public int nextCandidateIndex;
    }

    private struct GroupAttackCandidate
    {
        public int ruleIndex;
        public Enemy enemy;
        public int slotIndex;
        public int shotsFired;
        public DirectedWaveAttackAimState aim;
    }

    public DirectedWaveEntranceAttackController(
        DirectedEnemySubWave wave,
        DirectedWaveAttackSettings sharedAttackSettings,
        DirectedWaveEntranceAttackSettings settings)
    {
        this.wave = wave;
        this.sharedAttackSettings = sharedAttackSettings;
        this.settings = settings;
    }

    public void Begin()
    {
        Stop();
        if (settings == null || !settings.IsEnabled)
            return;

        isRunning = true;
    }

    public void Stop()
    {
        isRunning = false;
        perEnemySchedules.Clear();
        groupSchedules.Clear();
        groupCandidates.Clear();
        projectileBaseSpeedOverrideSettings.Clear();
        missingTargetWarningLogged = false;

        foreach (KeyValuePair<Enemy, IWaveAttackExecutor> pair in executors)
        {
            if (IsAliveExecutor(pair.Value))
                pair.Value.SetWaveAttackControl(false);
        }

        executors.Clear();
        controlledEnemies.Clear();
        componentBuffer.Clear();
    }

    public void NotifyCheckpointReached(
        Enemy enemy,
        int enemySlotIndex,
        int checkpointIndex,
        DirectedWaveRuntimeCheckpoint[] checkpoints)
    {
        if (!CanProcess(enemy) || checkpoints == null)
            return;

        TriggerCheckpointRules(enemy, enemySlotIndex, checkpointIndex);
        BeginAcrossCheckpointRules(
            enemy,
            enemySlotIndex,
            checkpointIndex,
            checkpoints);
        CompleteAcrossCheckpointRules(enemy, checkpointIndex);
        RemoveGroupCandidatesAtCheckpoint(enemy, checkpointIndex);
    }

    public bool ShouldActivateContinuousRouteAttack(
        Enemy enemy,
        int checkpointIndex,
        bool isLoopRestart)
    {
        return CanProcess(enemy)
            && settings.ContinuousAttackRule != null
            && settings.ContinuousAttackRule.Matches(
                checkpointIndex,
                isLoopRestart);
    }

    public void NotifySegmentAdvanced(Enemy enemy, float deltaTime)
    {
        if (!CanProcess(enemy))
            return;

        AdvancePerEnemySchedules(enemy, deltaTime);
        AdvanceGroupSchedules();
    }

    public void NotifyEnemyEntranceCompleted(Enemy enemy)
    {
        if (enemy == null)
            return;

        RemovePerEnemySchedules(enemy);
        RemoveGroupCandidates(enemy);
        ReleaseEnemyControl(enemy);
    }

    public void NotifyEnemyDestroyed(Enemy enemy)
    {
        if (enemy == null)
            return;

        RemovePerEnemySchedules(enemy);
        RemoveGroupCandidates(enemy);
        ReleaseEnemyControl(enemy);
        executors.Remove(enemy);
        projectileBaseSpeedOverrideSettings.Remove(enemy);
    }

    private bool CanProcess(Enemy enemy)
    {
        return isRunning
            && settings != null
            && settings.IsEnabled
            && IsAliveEnemy(enemy);
    }

    private void TriggerCheckpointRules(
        Enemy enemy,
        int enemySlotIndex,
        int checkpointIndex)
    {
        List<DirectedWaveAtCheckpointAttackRule> rules =
            settings.AtCheckpointRules;
        if (rules == null)
            return;

        for (int i = 0; i < rules.Count; i++)
        {
            DirectedWaveAtCheckpointAttackRule rule = rules[i];
            if (rule == null
                || !rule.IsEnabled
                || rule.CheckpointIndex != checkpointIndex
                || !rule.AllowsEnemySlot(enemySlotIndex))
            {
                continue;
            }

            DirectedWaveAttackAimState aim = default;
            for (int shotIndex = 0; shotIndex < rule.ShotCount; shotIndex++)
                TryFireSequence(enemy, ref aim, shotIndex);
        }
    }

    private void BeginAcrossCheckpointRules(
        Enemy enemy,
        int enemySlotIndex,
        int checkpointIndex,
        DirectedWaveRuntimeCheckpoint[] checkpoints)
    {
        List<DirectedWaveAcrossCheckpointsAttackRule> rules =
            settings.AcrossCheckpointRules;
        if (rules == null)
            return;

        for (int i = 0; i < rules.Count; i++)
        {
            DirectedWaveAcrossCheckpointsAttackRule rule = rules[i];
            if (rule == null
                || !rule.IsEnabled
                || rule.StartCheckpointIndex != checkpointIndex
                || rule.StartCheckpointIndex == rule.EndCheckpointIndex
                || !rule.AllowsEnemySlot(enemySlotIndex))
            {
                continue;
            }

            float duration = GetTraversalDuration(
                checkpoints,
                rule.StartCheckpointIndex,
                rule.EndCheckpointIndex);
            if (duration <= 0f)
                continue;

            if (rule.AttackCountMode
                == DirectedWaveEntranceAttackCountMode.PerEnemy)
            {
                BeginPerEnemySchedule(enemy, i, duration);
                continue;
            }

            BeginGroupSchedule(enemy, enemySlotIndex, i, duration);
        }
    }

    private void CompleteAcrossCheckpointRules(
        Enemy enemy,
        int checkpointIndex)
    {
        List<DirectedWaveAcrossCheckpointsAttackRule> rules =
            settings.AcrossCheckpointRules;
        if (rules == null)
            return;

        for (int i = perEnemySchedules.Count - 1; i >= 0; i--)
        {
            PerEnemyAttackSchedule schedule = perEnemySchedules[i];
            if (schedule.enemy != enemy
                || !TryGetAcrossRule(schedule.ruleIndex, out
                    DirectedWaveAcrossCheckpointsAttackRule rule)
                || rule.EndCheckpointIndex != checkpointIndex)
            {
                continue;
            }

            perEnemySchedules.RemoveAt(i);
        }
    }

    private void BeginPerEnemySchedule(
        Enemy enemy,
        int ruleIndex,
        float duration)
    {
        for (int i = 0; i < perEnemySchedules.Count; i++)
        {
            PerEnemyAttackSchedule schedule = perEnemySchedules[i];
            if (schedule.enemy != enemy || schedule.ruleIndex != ruleIndex)
                continue;

            schedule.elapsed = 0f;
            schedule.duration = duration;
            schedule.nextShotIndex = 0;
            schedule.aim = default;
            perEnemySchedules[i] = schedule;
            return;
        }

        perEnemySchedules.Add(new PerEnemyAttackSchedule
        {
            enemy = enemy,
            ruleIndex = ruleIndex,
            duration = duration
        });
    }

    private void BeginGroupSchedule(
        Enemy enemy,
        int enemySlotIndex,
        int ruleIndex,
        float duration)
    {
        int scheduleIndex = GetOrCreateGroupScheduleIndex(ruleIndex);
        GroupAttackSchedule schedule = groupSchedules[scheduleIndex];
        if (!schedule.isRunning)
        {
            RemoveGroupCandidatesForRule(ruleIndex);
            schedule.isRunning = true;
            schedule.startTime = Time.time;
            schedule.duration = duration;
            schedule.nextShotIndex = 0;
            schedule.nextCandidateIndex = 0;
        }

        AddGroupCandidate(ruleIndex, enemy, enemySlotIndex);
        groupSchedules[scheduleIndex] = schedule;
    }

    private void AdvancePerEnemySchedules(Enemy enemy, float deltaTime)
    {
        float safeDeltaTime = Mathf.Max(0f, deltaTime);
        for (int i = perEnemySchedules.Count - 1; i >= 0; i--)
        {
            PerEnemyAttackSchedule schedule = perEnemySchedules[i];
            if (schedule.enemy != enemy)
                continue;

            if (!TryGetAcrossRule(schedule.ruleIndex, out
                    DirectedWaveAcrossCheckpointsAttackRule rule)
                || !rule.IsEnabled
                || !IsAliveEnemy(enemy))
            {
                perEnemySchedules.RemoveAt(i);
                continue;
            }

            schedule.elapsed = Mathf.Min(
                schedule.duration,
                schedule.elapsed + safeDeltaTime);

            while (schedule.nextShotIndex < rule.AttackCount
                && schedule.elapsed + 0.0001f >= GetShotTime(
                    schedule.duration,
                    rule.AttackCount,
                    schedule.nextShotIndex))
            {
                TryFireSequence(enemy, ref schedule.aim, schedule.nextShotIndex);
                schedule.nextShotIndex++;
            }

            perEnemySchedules[i] = schedule;
        }
    }

    private void AdvanceGroupSchedules()
    {
        for (int i = 0; i < groupSchedules.Count; i++)
        {
            GroupAttackSchedule schedule = groupSchedules[i];
            if (!schedule.isRunning)
                continue;

            if (!TryGetAcrossRule(schedule.ruleIndex, out
                    DirectedWaveAcrossCheckpointsAttackRule rule)
                || !rule.IsEnabled)
            {
                schedule.isRunning = false;
                groupSchedules[i] = schedule;
                continue;
            }

            float elapsed = Mathf.Max(0f, Time.time - schedule.startTime);
            while (schedule.nextShotIndex < rule.AttackCount
                && elapsed + 0.0001f >= GetShotTime(
                    schedule.duration,
                    rule.AttackCount,
                    schedule.nextShotIndex))
            {
                Enemy enemy = SelectGroupCandidate(
                    schedule.ruleIndex,
                    rule.AttackOrder,
                    ref schedule);
                if (enemy != null)
                    TryFireGroupCandidate(enemy, schedule.ruleIndex);

                schedule.nextShotIndex++;
            }

            if (elapsed >= schedule.duration
                && schedule.nextShotIndex >= rule.AttackCount)
            {
                schedule.isRunning = false;
            }

            groupSchedules[i] = schedule;
        }
    }

    private Enemy SelectGroupCandidate(
        int ruleIndex,
        DirectedWaveEntranceAttackOrder order,
        ref GroupAttackSchedule schedule)
    {
        if (order == DirectedWaveEntranceAttackOrder.Random)
            return SelectRandomGroupCandidate(ruleIndex);

        int candidateCount = groupCandidates.Count;
        if (candidateCount == 0)
            return null;

        int startIndex = Mathf.Clamp(
            schedule.nextCandidateIndex,
            0,
            candidateCount - 1);
        for (int offset = 0; offset < candidateCount; offset++)
        {
            int candidateIndex = (startIndex + offset) % candidateCount;
            GroupAttackCandidate candidate = groupCandidates[candidateIndex];
            if (candidate.ruleIndex != ruleIndex
                || !IsAliveEnemy(candidate.enemy))
            {
                continue;
            }

            schedule.nextCandidateIndex =
                (candidateIndex + 1) % Mathf.Max(1, candidateCount);
            return candidate.enemy;
        }

        return null;
    }

    private Enemy SelectRandomGroupCandidate(int ruleIndex)
    {
        int validCandidateCount = 0;
        for (int i = 0; i < groupCandidates.Count; i++)
        {
            GroupAttackCandidate candidate = groupCandidates[i];
            if (candidate.ruleIndex == ruleIndex
                && IsAliveEnemy(candidate.enemy))
            {
                validCandidateCount++;
            }
        }

        if (validCandidateCount == 0)
            return null;

        int selectedCandidate = Random.Range(0, validCandidateCount);
        for (int i = 0; i < groupCandidates.Count; i++)
        {
            GroupAttackCandidate candidate = groupCandidates[i];
            if (candidate.ruleIndex != ruleIndex
                || !IsAliveEnemy(candidate.enemy))
            {
                continue;
            }

            if (selectedCandidate-- == 0)
                return candidate.enemy;
        }

        return null;
    }

    private void AddGroupCandidate(int ruleIndex, Enemy enemy, int enemySlotIndex)
    {
        for (int i = 0; i < groupCandidates.Count; i++)
        {
            GroupAttackCandidate candidate = groupCandidates[i];
            if (candidate.ruleIndex == ruleIndex && candidate.enemy == enemy)
                return;
        }

        int insertIndex = groupCandidates.Count;
        while (insertIndex > 0)
        {
            GroupAttackCandidate previous = groupCandidates[insertIndex - 1];
            if (previous.ruleIndex < ruleIndex
                || (previous.ruleIndex == ruleIndex
                    && previous.slotIndex <= enemySlotIndex))
            {
                break;
            }

            insertIndex--;
        }

        groupCandidates.Insert(insertIndex, new GroupAttackCandidate
        {
            ruleIndex = ruleIndex,
            enemy = enemy,
            slotIndex = enemySlotIndex
        });
    }

    private void RemoveGroupCandidatesAtCheckpoint(
        Enemy enemy,
        int checkpointIndex)
    {
        for (int i = groupCandidates.Count - 1; i >= 0; i--)
        {
            GroupAttackCandidate candidate = groupCandidates[i];
            if (candidate.enemy != enemy
                || !TryGetAcrossRule(candidate.ruleIndex, out
                    DirectedWaveAcrossCheckpointsAttackRule rule)
                || rule.EndCheckpointIndex != checkpointIndex)
            {
                continue;
            }

            groupCandidates.RemoveAt(i);
        }
    }

    private void RemovePerEnemySchedules(Enemy enemy)
    {
        for (int i = perEnemySchedules.Count - 1; i >= 0; i--)
        {
            if (perEnemySchedules[i].enemy == enemy)
                perEnemySchedules.RemoveAt(i);
        }
    }

    private void RemoveGroupCandidates(Enemy enemy)
    {
        for (int i = groupCandidates.Count - 1; i >= 0; i--)
        {
            if (groupCandidates[i].enemy == enemy)
                groupCandidates.RemoveAt(i);
        }
    }

    private void RemoveGroupCandidatesForRule(int ruleIndex)
    {
        for (int i = groupCandidates.Count - 1; i >= 0; i--)
        {
            if (groupCandidates[i].ruleIndex == ruleIndex)
                groupCandidates.RemoveAt(i);
        }
    }

    private int GetOrCreateGroupScheduleIndex(int ruleIndex)
    {
        for (int i = 0; i < groupSchedules.Count; i++)
        {
            if (groupSchedules[i].ruleIndex == ruleIndex)
                return i;
        }

        groupSchedules.Add(new GroupAttackSchedule { ruleIndex = ruleIndex });
        return groupSchedules.Count - 1;
    }

    private bool TryGetAcrossRule(
        int ruleIndex,
        out DirectedWaveAcrossCheckpointsAttackRule rule)
    {
        rule = null;
        List<DirectedWaveAcrossCheckpointsAttackRule> rules =
            settings != null ? settings.AcrossCheckpointRules : null;
        if (rules == null || ruleIndex < 0 || ruleIndex >= rules.Count)
            return false;

        rule = rules[ruleIndex];
        return rule != null;
    }

    private bool TryFire(Enemy enemy)
    {
        DirectedWaveAttackAimState aim = default;
        return TryFireSequence(enemy, ref aim, 0);
    }

    private void TryFireGroupCandidate(Enemy enemy, int ruleIndex)
    {
        for (int i = 0; i < groupCandidates.Count; i++)
        {
            GroupAttackCandidate candidate = groupCandidates[i];
            if (candidate.enemy != enemy || candidate.ruleIndex != ruleIndex)
                continue;
            TryFireSequence(enemy, ref candidate.aim, candidate.shotsFired);
            candidate.shotsFired++;
            groupCandidates[i] = candidate;
            return;
        }
    }

    private bool TryFireSequence(Enemy enemy, ref DirectedWaveAttackAimState aim, int shotIndex)
    {
        if (!IsAliveEnemy(enemy))
            return false;

        if (sharedAttackSettings == null)
            return false;

        DirectedWaveAttackFireMode fireMode = GetFireMode(enemy);
        if (fireMode == DirectedWaveAttackFireMode.None)
            return false;

        bool requiresPlayerTarget = fireMode
                == DirectedWaveAttackFireMode.Aimed
            || fireMode
                == DirectedWaveAttackFireMode.ForwardWhenPlayerAhead;
        if (requiresPlayerTarget && !wave.HasAttackTarget)
        {
            if (!missingTargetWarningLogged)
            {
                missingTargetWarningLogged = true;
                Debug.LogWarning(
                    "PlayerController was not injected. Entrance attacks requiring a player target cannot fire.",
                    wave);
            }

            return false;
        }

        if (!TryGetExecutor(enemy, out IWaveAttackExecutor executor))
            return false;

        EnemyBurstAttackSettings burstSettings = GetBurstSettings(enemy, executor);
        Vector3 forward = executor is IEnemyAttackDirectionProvider directionProvider
            ? directionProvider.AttackForward : enemy.transform.up;
        if (fireMode
            == DirectedWaveAttackFireMode.ForwardWhenPlayerAhead
            && !IsPlayerInForwardFireSector(
                enemy,
                enemy.transform.position,
                forward,
                wave.GetPlayerTargetPosition()))
        {
            return false;
        }

        if (fireMode == DirectedWaveAttackFireMode.Default
            || fireMode == DirectedWaveAttackFireMode.Forward
            || fireMode
                == DirectedWaveAttackFireMode.ForwardWhenPlayerAhead)
        {
            return executor.TryFireInDirection(
                forward,
                burstSettings);
        }

        DirectedWaveEnemyOverride enemyOverride = GetAttackOverride(enemy);
        DirectedWaveAttackAimMode aimMode = enemyOverride != null
            ? enemyOverride.ResolveAimMode(sharedAttackSettings.AimMode) : sharedAttackSettings.AimMode;
        if (aimMode == DirectedWaveAttackAimMode.AimContinuous)
            return executor.TryFireAt(wave.GetPlayerTargetPosition(), burstSettings);
        EnemyBurstAttackSettings firstPattern = burstSettings.GetPattern(0);
        bool refresh = aimMode == DirectedWaveAttackAimMode.AimOnBurst
            && firstPattern.RepeatBurst && shotIndex % firstPattern.BurstShotCount == 0;
        return aim.TryFire(enemy, executor, burstSettings, wave.GetPlayerTargetPosition(), refresh);
    }

    private bool TryGetExecutor(
        Enemy enemy,
        out IWaveAttackExecutor executor)
    {
        executor = null;
        if (!IsAliveEnemy(enemy))
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
        {
            DirectedWaveEnemyOverride enemyOverride = GetAttackOverride(enemy);
            DirectedWaveAttackAimMode aimMode = enemyOverride != null
                ? enemyOverride.ResolveAimMode(sharedAttackSettings.AimMode) : sharedAttackSettings.AimMode;
            aimReceiver.SetWaveAimTarget(GetFireMode(enemy) == DirectedWaveAttackFireMode.Aimed
                && aimMode == DirectedWaveAttackAimMode.AimContinuous ? wave : null);
        }

        if (!executor.CanPerformWaveAttack)
            return false;

        executor.SetWaveAttackControl(true);
        controlledEnemies.Add(enemy);
        return true;
    }

    private IWaveAttackExecutor FindExecutor(Enemy enemy)
    {
        componentBuffer.Clear();
        enemy.GetComponents<MonoBehaviour>(componentBuffer);
        for (int i = 0; i < componentBuffer.Count; i++)
        {
            if (componentBuffer[i] is IWaveAttackExecutor executor)
            {
                componentBuffer.Clear();
                return executor;
            }
        }

        componentBuffer.Clear();
        return null;
    }

    private EnemyBurstAttackSettings GetBurstSettings(
        Enemy enemy,
        IWaveAttackExecutor executor)
    {
        return ResolveAttackSettings(enemy, executor, out _);
    }

    private DirectedWaveEnemyOverride GetAttackOverride(Enemy enemy)
    {
        return enemy != null
            && wave.TryGetFormationIndex(enemy, out int slotIndex)
            ? wave.GetConfiguredEnemyOverrideForSlot(slotIndex)
            : null;
    }

    private DirectedWaveAttackFireMode GetFireMode(Enemy enemy)
    {
        DirectedWaveEnemyOverride enemyOverride = GetAttackOverride(enemy);
        return enemyOverride != null
            ? enemyOverride.ResolveFireMode(sharedAttackSettings.FireMode)
            : sharedAttackSettings.FireMode;
    }

    private bool IsPlayerInForwardFireSector(
        Enemy enemy,
        Vector3 enemyPosition,
        Vector2 forwardDirection,
        Vector3 playerPosition)
    {
        DirectedWaveEnemyOverride enemyOverride = GetAttackOverride(enemy);
        float halfAngle = enemyOverride != null
            ? enemyOverride.ResolveForwardFireHalfAngle(
                sharedAttackSettings.ForwardFireHalfAngle)
            : sharedAttackSettings.ForwardFireHalfAngle;
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
        bool usesEnemySettings = EnemyAttackSettingsResolver.UsesEnemySettings(
            executor,
            sharedAttackSettings.UsesEnemyBurstSettings);
        EnemyBurstAttackSettings source = EnemyAttackSettingsResolver.Resolve(
            executor,
            sharedAttackSettings.UsesEnemyBurstSettings,
            sharedAttackSettings.WaveBurstSettings);
        DirectedWaveEnemyOverride enemyOverride = GetAttackOverride(enemy);
        bool hasLocalOverride = enemyOverride != null
            && enemyOverride.HasAttackPatternOverride;
        bool usesLegacyFullSettings = enemyOverride != null
            && enemyOverride.UsesLegacyFullAttackSettings;
        bool needsRuntimeCopy = hasLocalOverride
            || sharedAttackSettings.HasRuntimeProjectileBaseSpeedOverride;
        if (!needsRuntimeCopy
            || enemy == null
            || source == null && !usesLegacyFullSettings)
        {
            setWaveAttackSettings = !usesEnemySettings;
            return source;
        }

        if (!projectileBaseSpeedOverrideSettings.TryGetValue(
                enemy,
                out EnemyBurstAttackSettings runtimeCopy))
        {
            runtimeCopy = new EnemyBurstAttackSettings();
            if (enemyOverride == null
                || !enemyOverride.TryCopyLegacyFullAttackSettings(runtimeCopy))
                runtimeCopy.CopyFrom(source);
            if (hasLocalOverride)
            {
                enemyOverride.ApplyAttackPatternOverride(
                    runtimeCopy,
                    sharedAttackSettings.HasRuntimeProjectileBaseSpeedOverride
                        ? sharedAttackSettings.RuntimeProjectileBaseSpeedOverride
                        : (float?)null);
            }
            else if (sharedAttackSettings.HasRuntimeProjectileBaseSpeedOverride)
            {
                runtimeCopy.SetRuntimeProjectileBaseSpeedOverrideForAllPatterns(
                    sharedAttackSettings.RuntimeProjectileBaseSpeedOverride);
            }

            projectileBaseSpeedOverrideSettings.Add(enemy, runtimeCopy);
        }

        setWaveAttackSettings = !usesEnemySettings || needsRuntimeCopy;
        return runtimeCopy;
    }

    private void ReleaseEnemyControl(Enemy enemy)
    {
        if (enemy == null || !controlledEnemies.Remove(enemy))
            return;

        if (executors.TryGetValue(enemy, out IWaveAttackExecutor executor)
            && IsAliveExecutor(executor))
        {
            executor.SetWaveAttackControl(false);
        }
    }

    private static float GetTraversalDuration(
        DirectedWaveRuntimeCheckpoint[] checkpoints,
        int startCheckpointIndex,
        int endCheckpointIndex)
    {
        if (checkpoints == null
            || checkpoints.Length < 2
            || startCheckpointIndex < 0
            || endCheckpointIndex < 0
            || startCheckpointIndex >= checkpoints.Length
            || endCheckpointIndex >= checkpoints.Length
            || startCheckpointIndex == endCheckpointIndex)
        {
            return 0f;
        }

        float duration = 0f;
        int checkpointIndex = startCheckpointIndex;
        for (int segmentCount = 0;
             segmentCount < checkpoints.Length
             && checkpointIndex != endCheckpointIndex;
             segmentCount++)
        {
            duration += Mathf.Max(
                MinimumTraversalDuration,
                checkpoints[checkpointIndex].durationToNext);
            checkpointIndex = (checkpointIndex + 1) % checkpoints.Length;
        }

        return checkpointIndex == endCheckpointIndex ? duration : 0f;
    }

    private static float GetShotTime(
        float duration,
        int shotCount,
        int shotIndex)
    {
        return duration * (shotIndex + 1) / (Mathf.Max(1, shotCount) + 1f);
    }

    private static bool IsAliveEnemy(Enemy enemy)
    {
        return enemy != null && !enemy.isDead && enemy.isActiveAndEnabled;
    }

    private static bool IsAliveExecutor(IWaveAttackExecutor executor)
    {
        return executor != null
            && (!(executor is Object unityObject) || unityObject != null);
    }
}
