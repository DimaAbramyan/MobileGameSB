using System.Collections.Generic;
using UnityEngine;

public sealed partial class DirectedEnemySubWave
{
    private const int RuntimeMaxCompletedCycles = 1024;
    private const int PreviewMaxCompletedCycles = 24;
    private const int CommandPreviewMaxCompletedCycles = 3;

    private readonly struct SimulatedBackgroundCommand
    {
        public readonly DirectedWavePostCommand command;
        public readonly float startTime;

        public SimulatedBackgroundCommand(
            DirectedWavePostCommand command,
            float startTime)
        {
            this.command = command;
            this.startTime = startTime;
        }
    }

    private readonly struct SimulationTimelineCommandState
    {
        public readonly DirectedWavePostCommand command;
        public readonly float elapsed;
        public readonly float duration;

        public SimulationTimelineCommandState(
            DirectedWavePostCommand command,
            float elapsed,
            float duration)
        {
            this.command = command;
            this.elapsed = elapsed;
            this.duration = duration;
        }
    }

    private readonly List<SimulationTimelineCommandState>
        previewActiveTimelineCommands = new(4);
    private readonly RuntimeTimelineEvaluationContext
        previewTimelineEvaluationContext = new();

    public int GetSimulationEnemyCount()
    {
        return GetEffectiveEnemyCount();
    }

    public int[] GetSimulationSpawnOrder()
    {
        return BuildSpawnOrder(GetEffectiveEnemyCount());
    }

    public Vector3 GetSimulationSpawnPosition()
    {
        return GetSimulationSpawnPosition(null);
    }

    public Vector3 GetSimulationSpawnPosition(Transform previewParent)
    {
        return GetSimulationEntranceStartPosition(0, previewParent);
    }

    public Vector3 GetSimulationEntranceStartPosition(int formationIndex)
    {
        return GetSimulationEntranceStartPosition(formationIndex, null);
    }

    public Vector3 GetSimulationEntranceStartPosition(
        int formationIndex,
        Transform previewParent)
    {
        Vector3 position = GetSpawnPosition(formationIndex);
        if (previewParent == null)
            return position;

        bool usesLocalSpace = UsesIndividualEntrancePoints()
            ? pathCoordinateSpace != DirectedWaveCoordinateSpace.World
            : pathCheckpoints != null && pathCheckpoints.Length > 0
                ? pathCoordinateSpace != DirectedWaveCoordinateSpace.World
                : spawnPoint != null || transform != null;
        return usesLocalSpace
            ? MapSimulationPointToPreviewParent(position, previewParent)
            : position;
    }

    public Vector3 GetSimulationFormationPosition(int index)
    {
        return GetSimulationFormationPosition(index, null);
    }

    public Vector3 GetSimulationFormationPosition(
        int index,
        Transform previewParent)
    {
        Vector3 position = GetFormationPosition(index);
        return previewParent != null
            && formationCoordinateSpace != DirectedWaveCoordinateSpace.World
            ? MapSimulationPointToPreviewParent(position, previewParent)
            : position;
    }

    public float GetSimulationSpawnInterval()
    {
        return GetFiniteNonNegativeSimulationValue(spawnInterval);
    }

    public bool SimulationUsesCommand(DirectedWavePostCommandType type)
    {
        return HasPostCommand(type);
    }

    public float GetSimulationPreviewTotalDuration(float infinitePreviewDuration)
    {
        int count = GetEffectiveEnemyCount();
        if (count <= 0)
            return 0f;

        if (HasValidEntranceLoopConfiguration())
        {
            float loopPreviewDuration = GetFiniteNonNegativeSimulationValue(
                infinitePreviewDuration);
            return GetSimulationEntranceCompletionTime()
                + loopPreviewDuration
                + 0.25f;
        }

        float duration = GetSimulationEntranceCompletionTime()
            + GetSimulationPreviewPostDuration(infinitePreviewDuration)
            + 0.25f;
        return float.IsNaN(duration) ? 0f : Mathf.Max(0f, duration);
    }

    public float GetSimulationBaseRouteDuration()
    {
        int count = GetEffectiveEnemyCount();
        if (count <= 0)
            return 0f;

        float duration = GetSimulationEntranceCompletionTime();

        if (HasValidEntranceLoopConfiguration())
        {
            duration += GetSimulationEntranceLoopDuration();
            return float.IsNaN(duration) ? 0f : Mathf.Max(0f, duration);
        }

        if (HasAnyPostCommand())
        {
            duration += GetFiniteNonNegativeSimulationValue(postStartDelay);
            float pipelineDuration = GetSimulationBaseCommandArrayDuration(
                postCommands,
                true,
                0);
            int repeatCount = postCommandPipelineLoop
                ? 1
                : GetSimulationPipelineFixedCount();
            duration += MultiplySimulationDuration(
                pipelineDuration,
                repeatCount);
        }

        return float.IsNaN(duration) ? 0f : Mathf.Max(0f, duration);
    }

    public float GetSimulationPreviewPostStartTime()
    {
        int count = GetEffectiveEnemyCount();
        if (count <= 0)
            return 0f;

        if (HasValidEntranceLoopConfiguration())
            return float.PositiveInfinity;

        return GetSimulationEntranceCompletionTime()
            + GetFiniteNonNegativeSimulationValue(postStartDelay);
    }

    public float GetSimulationPipelineDuration()
    {
        return GetSimulationCommandArrayDuration(postCommands, true);
    }

    public string GetSimulationPreviewPhaseName(float elapsed)
    {
        if (HasValidEntranceLoopConfiguration())
            return "Entrance Loop";

        float postStart = GetSimulationPreviewPostStartTime();
        if (elapsed < postStart || !HasAnyPostCommand())
            return "Entrance / Formation";

        List<SimulationTimelineCommandState> activeCommands = new();
        EvaluateSimulationPipeline(
            Mathf.Max(0f, elapsed - postStart),
            activeCommands);
        return activeCommands.Count > 0
            ? $"Post: {activeCommands[activeCommands.Count - 1].command.type}"
            : "Post: Complete";
    }

    public Dictionary<int, Vector3> EvaluateSimulationPreview(float elapsed)
    {
        return EvaluateSimulationPreview(elapsed, out _);
    }

    public Dictionary<int, Vector3> EvaluateSimulationPreview(
        float elapsed,
        out string phaseName)
    {
        return EvaluateSimulationPreview(elapsed, null, out phaseName);
    }

    public Dictionary<int, Vector3> EvaluateSimulationPreview(
        float elapsed,
        Transform previewParent,
        out string phaseName)
    {
        int count = GetEffectiveEnemyCount();
        Dictionary<int, Vector3> result = new(Mathf.Max(0, count));
        EvaluateSimulationPreviewInternal(
            elapsed,
            previewParent,
            null,
            result,
            null,
            out phaseName);
        return result;
    }

    public void EvaluateSimulationPreviewNonAlloc(
        float elapsed,
        Transform previewParent,
        int[] spawnOrder,
        Dictionary<int, Vector3> result,
        out string phaseName)
    {
        if (result == null)
            throw new System.ArgumentNullException(nameof(result));

        EvaluateSimulationPreviewInternal(
            elapsed,
            previewParent,
            spawnOrder,
            result,
            previewTimelineEvaluationContext,
            out phaseName);
    }

    public void InvalidateSimulationPreviewCache()
    {
        previewTimelineEvaluationContext.Reset();
    }

    private void EvaluateSimulationPreviewInternal(
        float elapsed,
        Transform previewParent,
        int[] spawnOrder,
        Dictionary<int, Vector3> result,
        RuntimeTimelineEvaluationContext evaluationContext,
        out string phaseName)
    {
        elapsed = GetFiniteNonNegativeSimulationValue(elapsed);
        int count = GetEffectiveEnemyCount();
        result.Clear();
        bool entranceLoops = HasValidEntranceLoopConfiguration();
        phaseName = entranceLoops ? "Entrance Loop" : "Entrance / Formation";
        if (count <= 0)
        {
            evaluationContext?.BeginFrame(elapsed);
            return;
        }

        int[] order = spawnOrder != null && spawnOrder.Length == count
            ? spawnOrder
            : BuildSpawnOrder(count);
        float postStart = GetSimulationPreviewPostStartTime();
        previewActiveTimelineCommands.Clear();
        Dictionary<int, Vector3> pipelineFrame = !entranceLoops && elapsed >= postStart
            ? EvaluateSimulationPipeline(
                Mathf.Max(0f, elapsed - postStart),
                previewActiveTimelineCommands,
                evaluationContext,
                previewParent,
                useRuntimeLimits: false)
            : null;

        if (pipelineFrame == null)
            evaluationContext?.BeginFrame(elapsed);

        if (pipelineFrame != null && HasAnyPostCommand())
        {
            phaseName = previewActiveTimelineCommands.Count > 0
                ? $"Post: {previewActiveTimelineCommands[previewActiveTimelineCommands.Count - 1].command.type}"
                : "Post: Complete";
        }

        DirectedWaveRuntimeCheckpoint[] entranceCheckpoints = null;
        float entrancePathDuration = 0f;
        for (int spawnStep = 0; spawnStep < order.Length; spawnStep++)
        {
            float enemyTime = elapsed
                - spawnStep * Mathf.Max(0f, spawnInterval);
            if (enemyTime < 0f)
                continue;

            int formationIndex = order[spawnStep];
            if (pipelineFrame != null
                && pipelineFrame.TryGetValue(formationIndex, out Vector3 pipelinePosition))
            {
                result[formationIndex] = pipelinePosition;
                continue;
            }

            if (!UsesIndividualEntrancePoints()
                && entranceCheckpoints == null)
            {
                entranceCheckpoints = GetSimulationPathCheckpoints(
                    previewParent);
                entrancePathDuration = GetSimulationPathDuration(
                    entranceCheckpoints);
            }

            result[formationIndex] = EvaluateSimulationEntrancePosition(
                formationIndex,
                spawnStep,
                enemyTime,
                previewParent,
                entranceCheckpoints,
                entrancePathDuration);
        }

    }

    public bool EvaluateSimulationCommandPreview(
        int commandIndex,
        out Dictionary<int, Vector3> before,
        out Dictionary<int, Vector3> after)
    {
        before = CreateSimulationFormationPositions();
        after = new Dictionary<int, Vector3>(before);
        if (postCommands == null
            || commandIndex < 0
            || commandIndex >= postCommands.Length)
        {
            return false;
        }

        for (int i = 0; i < commandIndex; i++)
        {
            DirectedWavePostCommand previous = postCommands[i];
            if (previous == null
                || !previous.enabled
                || IsBackgroundParallel(previous))
            {
                continue;
            }

            ApplySimulationCommandFinal(
                before,
                previous,
                true,
                maxCompletedCycles: CommandPreviewMaxCompletedCycles);
        }

        DirectedWavePostCommand command = postCommands[commandIndex];
        if (command == null)
            return false;

        float duration = GetSimulationCommandDuration(command, true);
        float sampleTime = GetSimulationCommandPreviewTime(
            command,
            duration);
        after = EvaluateSimulationCommandFrame(
            command,
            before,
            sampleTime,
            sampleTime,
            false,
            true,
            maxCompletedCycles: CommandPreviewMaxCompletedCycles);
        return true;
    }

    private float GetSimulationCommandPreviewTime(
        DirectedWavePostCommand command,
        float duration)
    {
        if (command.type != DirectedWavePostCommandType.Loop)
        {
            return IsFiniteSimulationDuration(duration)
                ? Mathf.Max(0.01f, duration)
                : 1f;
        }

        float iterationDuration = GetSimulationCommandArrayDuration(
            command.loopCommands,
            false);
        if (!IsFinitePositiveSimulationDuration(iterationDuration))
            return 0f;

        int previewCycles = command.infiniteLoop
            ? CommandPreviewMaxCompletedCycles
            : Mathf.Min(
                Mathf.Max(1, command.loopCount),
                CommandPreviewMaxCompletedCycles);
        return iterationDuration * previewCycles;
    }

    private Dictionary<int, Vector3> EvaluateSimulationPipeline(
        float elapsed,
        List<SimulationTimelineCommandState> activeCommands,
        RuntimeTimelineEvaluationContext runtimeContext = null,
        Transform previewParent = null,
        bool useRuntimeLimits = true)
    {
        elapsed = GetFiniteNonNegativeSimulationValue(elapsed);
        activeCommands?.Clear();
        runtimeContext?.BeginFrame(elapsed);
        int maxCompletedCycles = runtimeContext != null && useRuntimeLimits
            ? RuntimeMaxCompletedCycles
            : PreviewMaxCompletedCycles;
        Dictionary<int, Vector3> positions =
            CreateSimulationFormationPositions(runtimeContext, previewParent);
        if (postCommands == null || postCommands.Length == 0)
            return positions;

        float pipelineDuration = GetSimulationCommandArrayDuration(
            postCommands,
            true,
            runtimeContext);
        float foregroundTime = elapsed;
        float cycleStartTime = 0f;
        List<SimulatedBackgroundCommand> backgrounds = runtimeContext != null
            ? runtimeContext.backgrounds
            : new List<SimulatedBackgroundCommand>();

        int fixedCount = GetSimulationPipelineFixedCount();
        bool repeatsPipeline = postCommandPipelineLoop || fixedCount > 1;
        if (repeatsPipeline
            && IsFinitePositiveSimulationDuration(pipelineDuration))
        {
            int completedCycles = Mathf.FloorToInt(elapsed / pipelineDuration);
            if (!postCommandPipelineLoop)
                completedCycles = Mathf.Min(completedCycles, fixedCount);

            int safeCycles = Mathf.Min(
                completedCycles,
                maxCompletedCycles);
            for (int i = 0; i < safeCycles; i++)
            {
                CollectSimulationBackgroundCommands(
                    postCommands,
                    i * pipelineDuration,
                    true,
                    backgrounds,
                    runtimeContext,
                    maxCompletedCycles);
                ApplySimulationCommandArrayFinal(
                    positions,
                    postCommands,
                    true,
                    runtimeContext,
                    maxCompletedCycles);
            }

            if (!postCommandPipelineLoop && completedCycles >= fixedCount)
            {
                return ApplySimulationBackgroundCommands(
                    positions,
                    backgrounds,
                    elapsed,
                    runtimeContext);
            }

            foregroundTime = elapsed - completedCycles * pipelineDuration;
            cycleStartTime = completedCycles * pipelineDuration;
        }
        else if (IsFinitePositiveSimulationDuration(pipelineDuration))
        {
            foregroundTime = Mathf.Min(elapsed, pipelineDuration);
        }

        Dictionary<int, Vector3> frame = EvaluateSimulationCommandArrayUntil(
            positions,
            postCommands,
            foregroundTime,
            cycleStartTime,
            backgrounds,
            true,
            activeCommands,
            runtimeContext,
            maxCompletedCycles);

        return ApplySimulationBackgroundCommands(
            frame,
            backgrounds,
            elapsed,
            runtimeContext);
    }

    private Dictionary<int, Vector3> EvaluateSimulationCommandArrayUntil(
        Dictionary<int, Vector3> positions,
        DirectedWavePostCommand[] commands,
        float time,
        float timelineStart,
        List<SimulatedBackgroundCommand> backgrounds,
        bool allowLoops,
        List<SimulationTimelineCommandState> activeCommands,
        RuntimeTimelineEvaluationContext runtimeContext = null,
        int maxCompletedCycles = PreviewMaxCompletedCycles)
    {
        if (runtimeContext != null)
        {
            return EvaluateRuntimeSimulationCommandArrayUntil(
                positions,
                commands,
                time,
                timelineStart,
                backgrounds,
                allowLoops,
                activeCommands,
                runtimeContext,
                maxCompletedCycles);
        }

        Dictionary<int, Vector3> frame = new(positions);
        if (commands == null)
            return frame;

        float remaining = Mathf.Max(0f, time);
        float cursor = timelineStart;
        for (int i = 0; i < commands.Length; i++)
        {
            DirectedWavePostCommand command = commands[i];
            if (command == null || !command.enabled)
                continue;

            if (IsBackgroundParallel(command))
            {
                AddSimulationBackgroundCommand(
                    backgrounds,
                    command,
                    cursor,
                    null);
                continue;
            }

            if (!allowLoops && command.type == DirectedWavePostCommandType.Loop)
                continue;

            float duration = GetSimulationCommandDuration(command, allowLoops);
            if (remaining <= duration || float.IsInfinity(duration))
            {
                activeCommands?.Add(new SimulationTimelineCommandState(
                    command,
                    remaining,
                    duration));
                return EvaluateSimulationCommandFrame(
                    command,
                    frame,
                    remaining,
                    duration,
                    false,
                    allowLoops,
                    cursor,
                    backgrounds,
                    activeCommands,
                    null,
                    maxCompletedCycles);
            }

            ApplySimulationCommandFinal(
                frame,
                command,
                allowLoops,
                null,
                maxCompletedCycles);
            remaining -= duration;
            cursor += duration;

            float hold = Mathf.Max(0f, command.holdDuration);
            if (remaining <= hold)
                return frame;

            remaining -= hold;
            cursor += hold;
        }

        return frame;
    }

    private Dictionary<int, Vector3> EvaluateRuntimeSimulationCommandArrayUntil(
        Dictionary<int, Vector3> positions,
        DirectedWavePostCommand[] commands,
        float time,
        float timelineStart,
        List<SimulatedBackgroundCommand> backgrounds,
        bool allowLoops,
        List<SimulationTimelineCommandState> activeCommands,
        RuntimeTimelineEvaluationContext runtimeContext,
        int maxCompletedCycles)
    {
        Dictionary<int, Vector3> frame =
            runtimeContext.RentPositions(positions);
        if (commands == null)
            return frame;

        SimulationCommandArrayPlan plan = GetRuntimeCommandArrayPlan(
            commands,
            allowLoops,
            runtimeContext);
        float safeTime = Mathf.Max(0f, time);

        for (int i = 0; i < plan.count; i++)
        {
            SimulationCommandBoundary boundary = plan.boundaries[i];
            DirectedWavePostCommand command = boundary.command;
            if (boundary.isBackground)
            {
                AddSimulationBackgroundCommand(
                    backgrounds,
                    command,
                    timelineStart + boundary.startTime,
                    runtimeContext);
                continue;
            }

            if (safeTime < boundary.startTime)
                return frame;

            float commandElapsed = safeTime - boundary.startTime;
            if (commandElapsed <= boundary.duration
                || float.IsInfinity(boundary.duration))
            {
                activeCommands?.Add(new SimulationTimelineCommandState(
                    command,
                    commandElapsed,
                    boundary.duration));
                int marker = runtimeContext.MarkPositionBuffers();
                Dictionary<int, Vector3> evaluated =
                    EvaluateSimulationCommandFrame(
                        command,
                        frame,
                        commandElapsed,
                        boundary.duration,
                        false,
                        allowLoops,
                        timelineStart + boundary.startTime,
                        backgrounds,
                        activeCommands,
                        runtimeContext,
                        maxCompletedCycles);
                ReplacePositions(frame, evaluated);
                runtimeContext.RestorePositionBuffers(marker);
                return frame;
            }

            ApplySimulationCommandFinal(
                frame,
                command,
                allowLoops,
                runtimeContext,
                maxCompletedCycles);
            if (safeTime <= boundary.startTime
                + boundary.duration
                + boundary.holdDuration)
            {
                return frame;
            }
        }

        return frame;
    }

    private Dictionary<int, Vector3> EvaluateSimulationCommandFrame(
        DirectedWavePostCommand command,
        Dictionary<int, Vector3> input,
        float elapsed,
        float commandDuration,
        bool finalFrame,
        bool allowLoops,
        float commandStartTime = 0f,
        List<SimulatedBackgroundCommand> backgrounds = null,
        List<SimulationTimelineCommandState> activeCommands = null,
        RuntimeTimelineEvaluationContext runtimeContext = null,
        int maxCompletedCycles = PreviewMaxCompletedCycles)
    {
        if (command == null)
            return CopySimulationPositions(input, runtimeContext);

        if (command.type == DirectedWavePostCommandType.Loop)
        {
            return allowLoops
                ? EvaluateSimulationLoopCommand(
                    command,
                    input,
                    elapsed,
                    commandStartTime,
                    backgrounds,
                    activeCommands,
                    runtimeContext,
                    maxCompletedCycles)
                : CopySimulationPositions(input, runtimeContext);
        }

        if (command.type == DirectedWavePostCommandType.Parallel)
        {
            float duration = float.IsInfinity(commandDuration)
                ? Mathf.Max(0.01f, elapsed)
                : Mathf.Max(0.01f, commandDuration);
            return EvaluateParallelCommandFrame(
                command,
                input,
                elapsed,
                duration,
                finalFrame,
                runtimeContext);
        }

        float safeDuration = float.IsInfinity(commandDuration)
            ? Mathf.Max(0.01f, elapsed)
            : Mathf.Max(0.01f, commandDuration);
        return EvaluatePostCommandFrame(
            command,
            input,
            elapsed,
            safeDuration,
            finalFrame,
            runtimeContext);
    }

    private Dictionary<int, Vector3> EvaluateSimulationLoopCommand(
        DirectedWavePostCommand command,
        Dictionary<int, Vector3> positions,
        float elapsed,
        float timelineStart,
        List<SimulatedBackgroundCommand> backgrounds,
        List<SimulationTimelineCommandState> activeCommands,
        RuntimeTimelineEvaluationContext runtimeContext,
        int maxCompletedCycles)
    {
        Dictionary<int, Vector3> frame =
            CopySimulationPositions(positions, runtimeContext);
        float iterationDuration = GetSimulationCommandArrayDuration(
            command.loopCommands,
            false,
            runtimeContext);
        if (!IsFinitePositiveSimulationDuration(iterationDuration))
            return frame;

        float safeElapsed = GetFiniteNonNegativeSimulationValue(elapsed);
        int completedCycles;
        float remaining;
        if (command.infiniteLoop)
        {
            completedCycles = Mathf.FloorToInt(safeElapsed / iterationDuration);
            remaining = Mathf.Repeat(safeElapsed, iterationDuration);
        }
        else
        {
            int loopCount = Mathf.Max(1, command.loopCount);
            float totalDuration = iterationDuration * loopCount;
            if (safeElapsed >= totalDuration)
            {
                completedCycles = loopCount;
                remaining = 0f;
            }
            else
            {
                completedCycles = Mathf.FloorToInt(safeElapsed / iterationDuration);
                remaining = safeElapsed - completedCycles * iterationDuration;
            }
        }

        int safeCycles = Mathf.Min(
            completedCycles,
            maxCompletedCycles);
        for (int i = 0; i < safeCycles; i++)
        {
            CollectSimulationBackgroundCommands(
                command.loopCommands,
                timelineStart + i * iterationDuration,
                false,
                backgrounds,
                runtimeContext,
                maxCompletedCycles);
            ApplySimulationCommandArrayFinal(
                frame,
                command.loopCommands,
                false,
                runtimeContext,
                maxCompletedCycles);
        }

        if (remaining <= 0f)
            return frame;

        return EvaluateSimulationCommandArrayUntil(
            frame,
            command.loopCommands,
            remaining,
            timelineStart + completedCycles * iterationDuration,
            backgrounds ?? new List<SimulatedBackgroundCommand>(),
            false,
            activeCommands,
            runtimeContext,
            maxCompletedCycles);
    }

    private void ApplySimulationCommandArrayFinal(
        Dictionary<int, Vector3> positions,
        DirectedWavePostCommand[] commands,
        bool allowLoops,
        RuntimeTimelineEvaluationContext runtimeContext = null,
        int maxCompletedCycles = PreviewMaxCompletedCycles)
    {
        if (commands == null)
            return;

        for (int i = 0; i < commands.Length; i++)
        {
            DirectedWavePostCommand command = commands[i];
            if (command == null
                || !command.enabled
                || IsBackgroundParallel(command)
                || (!allowLoops && command.type == DirectedWavePostCommandType.Loop))
            {
                continue;
            }

            ApplySimulationCommandFinal(
                positions,
                command,
                allowLoops,
                runtimeContext,
                maxCompletedCycles);
        }
    }

    private void ApplySimulationCommandFinal(
        Dictionary<int, Vector3> positions,
        DirectedWavePostCommand command,
        bool allowLoops,
        RuntimeTimelineEvaluationContext runtimeContext = null,
        int maxCompletedCycles = PreviewMaxCompletedCycles)
    {
        if (command == null)
            return;

        if (command.type == DirectedWavePostCommandType.Loop)
        {
            if (!allowLoops || command.infiniteLoop)
                return;

            int loopCount = Mathf.Max(1, command.loopCount);
            for (int i = 0; i < Mathf.Min(
                    loopCount,
                    maxCompletedCycles); i++)
            {
                ApplySimulationCommandArrayFinal(
                    positions,
                    command.loopCommands,
                    false,
                    runtimeContext,
                    maxCompletedCycles);
            }
            return;
        }

        float duration = GetSimulationCommandDuration(
            command,
            allowLoops,
            runtimeContext);
        if (float.IsInfinity(duration))
            return;

        int marker = runtimeContext?.MarkPositionBuffers() ?? 0;
        Dictionary<int, Vector3> final = EvaluateSimulationCommandFrame(
            command,
            positions,
            duration,
            duration,
            true,
            allowLoops,
            runtimeContext: runtimeContext,
            maxCompletedCycles: maxCompletedCycles);
        ReplacePositions(positions, final);
        runtimeContext?.RestorePositionBuffers(marker);
    }

    private Dictionary<int, Vector3> ApplySimulationBackgroundCommands(
        Dictionary<int, Vector3> positions,
        List<SimulatedBackgroundCommand> backgrounds,
        float elapsed,
        RuntimeTimelineEvaluationContext runtimeContext = null)
    {
        Dictionary<int, Vector3> frame =
            CopySimulationPositions(positions, runtimeContext);
        Dictionary<DirectedWavePostCommand, SimulatedBackgroundCommand> active =
            runtimeContext != null
                ? runtimeContext.activeBackgrounds
                : new Dictionary<
                    DirectedWavePostCommand,
                    SimulatedBackgroundCommand>();
        for (int i = 0; i < backgrounds.Count; i++)
        {
            SimulatedBackgroundCommand background = backgrounds[i];
            DirectedWavePostCommand command = background.command;
            if (command == null || background.startTime > elapsed)
                continue;

            if (active.TryGetValue(
                    command,
                    out SimulatedBackgroundCommand previous))
            {
                float previousEnd = previous.command.infiniteParallel
                    ? Mathf.Infinity
                    : previous.startTime
                        + Mathf.Max(0.01f, previous.command.duration);
                if (background.startTime < previousEnd)
                    continue;
            }

            active[command] = background;
        }

        foreach (SimulatedBackgroundCommand background in active.Values)
        {
            DirectedWavePostCommand command = background.command;
            float commandElapsed = Mathf.Max(0f, elapsed - background.startTime);
            float duration = Mathf.Max(0.01f, command.duration);
            if (!command.infiniteParallel && commandElapsed >= duration)
                continue;

            float sampleTime = command.infiniteParallel
                ? commandElapsed
                : Mathf.Min(commandElapsed, duration);
            float sampleDuration = command.infiniteParallel
                ? Mathf.Max(0.01f, sampleTime)
                : duration;
            int marker = runtimeContext?.MarkPositionBuffers() ?? 0;
            Dictionary<int, Vector3> evaluated = EvaluateParallelCommandFrame(
                command,
                frame,
                sampleTime,
                sampleDuration,
                false,
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

    private void CollectSimulationBackgroundCommands(
        DirectedWavePostCommand[] commands,
        float timelineStart,
        bool allowLoops,
        List<SimulatedBackgroundCommand> backgrounds,
        RuntimeTimelineEvaluationContext runtimeContext = null,
        int maxCompletedCycles = PreviewMaxCompletedCycles)
    {
        if (commands == null)
            return;

        if (runtimeContext != null)
        {
            CollectRuntimeSimulationBackgroundCommands(
                commands,
                timelineStart,
                allowLoops,
                backgrounds,
                runtimeContext,
                maxCompletedCycles);
            return;
        }

        float cursor = timelineStart;
        for (int i = 0; i < commands.Length; i++)
        {
            DirectedWavePostCommand command = commands[i];
            if (command == null || !command.enabled)
                continue;

            if (IsBackgroundParallel(command))
            {
                AddSimulationBackgroundCommand(
                    backgrounds,
                    command,
                    cursor,
                    null);
                continue;
            }

            if (command.type == DirectedWavePostCommandType.Loop)
            {
                if (!allowLoops || command.infiniteLoop)
                    return;

                float iterationDuration = GetSimulationCommandArrayDuration(
                    command.loopCommands,
                    false);
                if (!IsFinitePositiveSimulationDuration(iterationDuration))
                    return;

                int loopCount = Mathf.Min(
                    Mathf.Max(1, command.loopCount),
                    maxCompletedCycles);
                for (int cycle = 0; cycle < loopCount; cycle++)
                {
                    CollectSimulationBackgroundCommands(
                        command.loopCommands,
                        cursor + cycle * iterationDuration,
                        false,
                        backgrounds,
                        null,
                        maxCompletedCycles);
                }
            }

            float duration = GetSimulationCommandDuration(command, allowLoops);
            if (float.IsInfinity(duration))
                return;

            cursor += duration + Mathf.Max(0f, command.holdDuration);
        }
    }

    private void CollectRuntimeSimulationBackgroundCommands(
        DirectedWavePostCommand[] commands,
        float timelineStart,
        bool allowLoops,
        List<SimulatedBackgroundCommand> backgrounds,
        RuntimeTimelineEvaluationContext runtimeContext,
        int maxCompletedCycles)
    {
        SimulationCommandArrayPlan plan = GetRuntimeCommandArrayPlan(
            commands,
            allowLoops,
            runtimeContext);
        for (int i = 0; i < plan.count; i++)
        {
            SimulationCommandBoundary boundary = plan.boundaries[i];
            DirectedWavePostCommand command = boundary.command;
            if (boundary.isBackground)
            {
                AddSimulationBackgroundCommand(
                    backgrounds,
                    command,
                    timelineStart + boundary.startTime,
                    runtimeContext);
                continue;
            }

            if (command.type != DirectedWavePostCommandType.Loop)
                continue;

            if (!allowLoops || command.infiniteLoop)
                return;

            SimulationCommandArrayPlan loopPlan = GetRuntimeCommandArrayPlan(
                command.loopCommands,
                false,
                runtimeContext);
            float iterationDuration = loopPlan != null
                ? loopPlan.duration
                : 0f;
            if (!IsFinitePositiveSimulationDuration(iterationDuration))
                return;

            int loopCount = Mathf.Min(
                Mathf.Max(1, command.loopCount),
                maxCompletedCycles);
            for (int cycle = 0; cycle < loopCount; cycle++)
            {
                CollectRuntimeSimulationBackgroundCommands(
                    command.loopCommands,
                    timelineStart
                        + boundary.startTime
                        + cycle * iterationDuration,
                    false,
                    backgrounds,
                    runtimeContext,
                    maxCompletedCycles);
            }
        }
    }

    private static void AddSimulationBackgroundCommand(
        List<SimulatedBackgroundCommand> backgrounds,
        DirectedWavePostCommand command,
        float startTime,
        RuntimeTimelineEvaluationContext runtimeContext)
    {
        if (runtimeContext != null)
        {
            runtimeContext.RecordBackground(command, startTime);
            return;
        }

        backgrounds.Add(new SimulatedBackgroundCommand(command, startTime));
    }

    private float GetSimulationCommandArrayDuration(
        DirectedWavePostCommand[] commands,
        bool allowLoops,
        RuntimeTimelineEvaluationContext runtimeContext = null)
    {
        if (commands == null)
            return 0f;

        if (runtimeContext != null)
        {
            return GetRuntimeCommandArrayPlan(
                commands,
                allowLoops,
                runtimeContext).duration;
        }

        float total = 0f;
        for (int i = 0; i < commands.Length; i++)
        {
            DirectedWavePostCommand command = commands[i];
            if (command == null || !command.enabled || IsBackgroundParallel(command))
                continue;

            if (!allowLoops && command.type == DirectedWavePostCommandType.Loop)
                continue;

            float commandDuration = GetSimulationCommandDuration(
                command,
                allowLoops);
            if (float.IsNaN(commandDuration))
                continue;

            total += commandDuration;
            if (float.IsInfinity(total))
                return total;

            total += GetFiniteNonNegativeSimulationValue(
                command.holdDuration);
        }

        return total;
    }

    private float GetSimulationBaseCommandArrayDuration(
        DirectedWavePostCommand[] commands,
        bool allowLoops,
        int depth)
    {
        if (commands == null || depth > 8)
            return 0f;

        float total = 0f;
        for (int i = 0; i < commands.Length; i++)
        {
            DirectedWavePostCommand command = commands[i];
            if (command == null || !command.enabled || IsBackgroundParallel(command))
                continue;

            if (!allowLoops && command.type == DirectedWavePostCommandType.Loop)
                continue;

            bool stopsPipeline = false;
            float duration;
            if (command.type == DirectedWavePostCommandType.Loop)
            {
                float iterationDuration = GetSimulationBaseCommandArrayDuration(
                    command.loopCommands,
                    false,
                    depth + 1);
                int cycleCount = command.infiniteLoop
                    ? 1
                    : Mathf.Max(1, command.loopCount);
                duration = iterationDuration * cycleCount;
                stopsPipeline = command.infiniteLoop;
            }
            else if (command.type == DirectedWavePostCommandType.Parallel
                && command.infiniteParallel)
            {
                duration = GetFiniteNonNegativeSimulationValue(command.duration);
                stopsPipeline = command.parallelExecutionMode
                    == DirectedWaveParallelExecutionMode.Blocking;
            }
            else
            {
                duration = GetSimulationCommandDuration(command, allowLoops);
            }

            if (float.IsNaN(duration))
                duration = 0f;
            if (float.IsInfinity(duration))
                duration = 0f;

            total += Mathf.Max(0f, duration);
            if (stopsPipeline)
                break;

            total += GetFiniteNonNegativeSimulationValue(
                command.holdDuration);
        }

        return float.IsNaN(total) ? 0f : Mathf.Max(0f, total);
    }

    private float GetSimulationCommandDuration(
        DirectedWavePostCommand command,
        bool allowLoops,
        RuntimeTimelineEvaluationContext runtimeContext = null)
    {
        if (command == null)
            return 0f;

        if (command.type == DirectedWavePostCommandType.LegacyAttack)
        {
            runtimeContext?.StoreCommandDuration(command, 0f);
            return 0f;
        }

        if (runtimeContext != null
            && runtimeContext.TryGetCommandDuration(command, out float cached))
        {
            return cached;
        }

        if (command.type == DirectedWavePostCommandType.Parallel
            && command.infiniteParallel)
        {
            runtimeContext?.StoreCommandDuration(command, Mathf.Infinity);
            return Mathf.Infinity;
        }

        if (command.type == DirectedWavePostCommandType.Loop)
        {
            if (!allowLoops)
                return 0f;
            if (command.infiniteLoop)
            {
                runtimeContext?.StoreCommandDuration(command, Mathf.Infinity);
                return Mathf.Infinity;
            }

            float loopDuration = GetSimulationCommandArrayDuration(
                    command.loopCommands,
                    false,
                    runtimeContext)
                * Mathf.Max(1, command.loopCount);
            runtimeContext?.StoreCommandDuration(command, loopDuration);
            return loopDuration;
        }

        float duration = TryGetPostCommandHandler(command.type, out var handler)
            ? handler.GetDuration(this, command)
            : Mathf.Max(0.01f, command.duration);
        if (float.IsNaN(duration))
            duration = 0f;
        runtimeContext?.StoreCommandDuration(command, duration);
        return duration;
    }

    private SimulationCommandArrayPlan GetRuntimeCommandArrayPlan(
        DirectedWavePostCommand[] commands,
        bool allowLoops,
        RuntimeTimelineEvaluationContext runtimeContext)
    {
        if (commands == null)
            return null;

        if (runtimeContext.TryGetPlan(commands, allowLoops, out var cached))
            return cached;

        SimulationCommandArrayPlan plan = new(commands, allowLoops);
        runtimeContext.StorePlan(plan);
        float cursor = 0f;
        for (int i = 0; i < commands.Length; i++)
        {
            DirectedWavePostCommand command = commands[i];
            if (command == null || !command.enabled)
                continue;

            bool isBackground = IsBackgroundParallel(command);
            if (!allowLoops && command.type == DirectedWavePostCommandType.Loop)
                continue;

            float duration = isBackground
                ? Mathf.Max(0.01f, command.duration)
                : GetSimulationCommandDuration(command, allowLoops, runtimeContext);
            if (float.IsNaN(duration))
                duration = 0f;
            float hold = isBackground
                ? 0f
                : GetFiniteNonNegativeSimulationValue(command.holdDuration);
            plan.boundaries[plan.count++] = new SimulationCommandBoundary(
                command,
                cursor,
                duration,
                hold,
                isBackground);

            if (isBackground)
                continue;

            cursor += duration;
            if (float.IsInfinity(cursor))
                break;

            cursor += hold;
        }

        plan.duration = cursor;
        return plan;
    }

    private float GetSimulationPreviewPostDuration(float infinitePreviewDuration)
    {
        infinitePreviewDuration =
            GetFiniteNonNegativeSimulationValue(infinitePreviewDuration);
        float duration = GetSimulationPipelineDuration();
        if (float.IsNaN(duration))
            duration = 0f;
        bool hasInfinite = HasSimulationInfiniteCommand(postCommands, 0);
        bool loopsPipelineForever = postCommandPipelineLoop
            && IsFinitePositiveSimulationDuration(duration);
        if (duration <= 0f)
            return hasInfinite
                ? Mathf.Max(0f, postStartDelay)
                    + Mathf.Max(0f, infinitePreviewDuration)
                : 0f;

        if (loopsPipelineForever || float.IsInfinity(duration))
            duration = Mathf.Max(0f, infinitePreviewDuration);
        else
        {
            duration = MultiplySimulationDuration(
                duration,
                GetSimulationPipelineFixedCount());
            if (hasInfinite)
                duration += Mathf.Max(0f, infinitePreviewDuration);
        }

        return Mathf.Max(0f, postStartDelay) + duration;
    }

    private int GetSimulationPipelineFixedCount()
    {
        return Mathf.Max(1, postCommandPipelineFixedCount);
    }

    private static float MultiplySimulationDuration(float duration, int count)
    {
        if (!IsFiniteSimulationDuration(duration))
            return duration;

        double result = (double)Mathf.Max(0f, duration) * Mathf.Max(1, count);
        return result >= float.MaxValue ? float.MaxValue : (float)result;
    }

    private static bool IsFiniteSimulationDuration(float duration)
    {
        return !float.IsNaN(duration) && !float.IsInfinity(duration);
    }

    private static bool IsFinitePositiveSimulationDuration(float duration)
    {
        return duration > 0f && IsFiniteSimulationDuration(duration);
    }

    private static float GetFiniteNonNegativeSimulationValue(float value)
    {
        return IsFiniteSimulationDuration(value)
            ? Mathf.Max(0f, value)
            : 0f;
    }

    private bool HasSimulationInfiniteCommand(
        DirectedWavePostCommand[] commands,
        int depth)
    {
        if (commands == null || depth > 8)
            return false;

        for (int i = 0; i < commands.Length; i++)
        {
            DirectedWavePostCommand command = commands[i];
            if (command == null || !command.enabled)
                continue;

            if ((command.type == DirectedWavePostCommandType.Parallel
                    && command.infiniteParallel)
                || (command.type == DirectedWavePostCommandType.Loop
                    && command.infiniteLoop)
                || IsInfiniteContinuousCommand(command))
            {
                return true;
            }

            if (HasSimulationInfiniteCommand(command.parallelCommands, depth + 1)
                || HasSimulationInfiniteCommand(command.loopCommands, depth + 1))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsInfiniteContinuousCommand(
        DirectedWavePostCommand command)
    {
        if (command.completionMode
            != DirectedWavePostCommandCompletionMode.Infinite)
        {
            return false;
        }

        return command.type == DirectedWavePostCommandType.Patrol
            || command.type == DirectedWavePostCommandType.Wobble
            || command.type == DirectedWavePostCommandType.CircularMovement
            || (command.type == DirectedWavePostCommandType.FormationRotation
                && command.continuousFormationRotation);
    }

    private Dictionary<int, Vector3> CreateSimulationFormationPositions(
        RuntimeTimelineEvaluationContext runtimeContext = null,
        Transform previewParent = null)
    {
        int count = GetEffectiveEnemyCount();
        Dictionary<int, Vector3> positions = runtimeContext != null
            ? runtimeContext.RentPositions(count)
            : new Dictionary<int, Vector3>(count);
        for (int i = 0; i < count; i++)
        {
            positions[i] = GetSimulationFormationPosition(
                i,
                previewParent);
        }

        return positions;
    }

    private static Dictionary<int, Vector3> CopySimulationPositions(
        Dictionary<int, Vector3> source,
        RuntimeTimelineEvaluationContext runtimeContext)
    {
        return runtimeContext != null
            ? runtimeContext.RentPositions(source)
            : new Dictionary<int, Vector3>(source);
    }

    private float GetSimulationEntranceCompletionTime()
    {
        int count = GetEffectiveEnemyCount();
        if (count <= 0)
            return 0f;

        float lastSpawnTime = (count - 1)
            * GetFiniteNonNegativeSimulationValue(spawnInterval);
        if (!UsesIndividualEntrancePoints())
        {
            return lastSpawnTime
                + GetSimulationPathDuration()
                + (HasValidEntranceLoopConfiguration()
                    ? 0f
                    : GetFiniteNonNegativeSimulationValue(settleDuration));
        }

        return lastSpawnTime
            + GetSimulationIndividualPointMovementStartDelay(count - 1)
            + GetFiniteNonNegativeSimulationValue(
                individualPointMovementDuration);
    }

    private float GetSimulationIndividualPointMovementStartDelay(int spawnStep)
    {
        return Mathf.Max(0, spawnStep)
            * GetFiniteNonNegativeSimulationValue(
                individualPointMovementStartDelay);
    }

    private float GetSimulationPathDuration()
    {
        if (pathCheckpoints == null || pathCheckpoints.Length <= 1)
            return 0f;

        float duration = 0f;
        DirectedWavePathCheckpoint previous = null;
        for (int i = 0; i < pathCheckpoints.Length; i++)
        {
            DirectedWavePathCheckpoint checkpoint = pathCheckpoints[i];
            if (checkpoint == null)
                continue;

            if (previous != null)
            {
                float segmentDuration = previous.durationToNext;
                duration += IsFiniteSimulationDuration(segmentDuration)
                    ? Mathf.Max(0.01f, segmentDuration)
                    : 0.01f;
            }

            previous = checkpoint;
        }

        return duration;
    }

    private float GetSimulationEntranceLoopDuration()
    {
        if (!HasValidEntranceLoopConfiguration())
            return 0f;

        int validCheckpointCount = 0;
        for (int i = 0; i < pathCheckpoints.Length; i++)
        {
            if (pathCheckpoints[i] != null)
                validCheckpointCount++;
        }

        int loopStartIndex =
            DirectedWaveEntranceLoopEvaluator.GetLoopStartCheckpointIndex(
                entranceLoopStartCheckpointIndex,
                validCheckpointCount);
        int validIndex = 0;
        float duration = 0f;
        for (int i = 0; i < pathCheckpoints.Length; i++)
        {
            DirectedWavePathCheckpoint checkpoint = pathCheckpoints[i];
            if (checkpoint == null)
                continue;

            bool isLoopReturnSegment = validIndex == validCheckpointCount - 1;
            if (validIndex >= loopStartIndex
                && (!entranceLoopTeleportToStart || !isLoopReturnSegment))
            {
                float segmentDuration = checkpoint.durationToNext;
                duration += IsFiniteSimulationDuration(segmentDuration)
                    ? Mathf.Max(0.01f, segmentDuration)
                    : 0.01f;
            }

            validIndex++;
        }

        if (entranceLoopTeleportToStart)
        {
            duration += GetFiniteNonNegativeSimulationValue(
                entranceLoopTeleportDelay);
        }

        return duration;
    }

    private static float GetSimulationPathDuration(
        DirectedWaveRuntimeCheckpoint[] checkpoints)
    {
        if (checkpoints.Length <= 1)
            return 0f;

        float duration = 0f;
        for (int i = 0; i < checkpoints.Length - 1; i++)
        {
            float segmentDuration = checkpoints[i].durationToNext;
            duration += IsFiniteSimulationDuration(segmentDuration)
                ? Mathf.Max(0.01f, segmentDuration)
                : 0.01f;
        }

        return duration;
    }

    private Vector3 EvaluateSimulationEntrancePosition(
        int formationIndex,
        int spawnStep,
        float enemyTime,
        Transform previewParent,
        DirectedWaveRuntimeCheckpoint[] checkpoints,
        float pathDuration)
    {
        if (UsesIndividualEntrancePoints())
        {
            return EvaluateSimulationIndividualPointEntrancePosition(
                formationIndex,
                spawnStep,
                enemyTime,
                previewParent);
        }

        if (HasValidEntranceLoopConfiguration()
            && checkpoints != null
            && checkpoints.Length >= 2)
        {
            return DirectedWaveEntranceLoopEvaluator.EvaluatePosition(
                checkpoints,
                enemyTime,
                DirectedWaveEntranceLoopEvaluator.GetLoopStartCheckpointIndex(
                    entranceLoopStartCheckpointIndex,
                    checkpoints.Length),
                entranceLoopTeleportToStart,
                entranceLoopTeleportDelay);
        }

        Vector3 formationPosition = GetSimulationFormationPosition(
            formationIndex,
            previewParent);
        Vector3 settleStart = checkpoints.Length > 0
            ? checkpoints[checkpoints.Length - 1].position
            : GetSimulationSpawnPosition(previewParent);

        if (checkpoints.Length > 0)
        {
            if (checkpoints.Length > 1 && enemyTime <= pathDuration)
                return EvaluateSimulationCheckpointPath(checkpoints, enemyTime);
        }

        float settleTime =
            GetFiniteNonNegativeSimulationValue(settleDuration);
        if (settleTime <= 0f)
            return formationPosition;

        float normalized = Mathf.Clamp01((enemyTime - pathDuration) / settleTime);
        float curved = EvaluateCurve(settleCurve, normalized);
        return Vector3.LerpUnclamped(
            settleStart,
            formationPosition,
            curved);
    }

    private Vector3 EvaluateSimulationIndividualPointEntrancePosition(
        int formationIndex,
        int spawnStep,
        float enemyTime,
        Transform previewParent)
    {
        Vector3 startPosition = GetSimulationEntranceStartPosition(
            formationIndex,
            previewParent);
        float movementStartDelay =
            GetSimulationIndividualPointMovementStartDelay(spawnStep);
        if (enemyTime <= movementStartDelay)
            return startPosition;

        Vector3 formationPosition = GetSimulationFormationPosition(
            formationIndex,
            previewParent);
        float duration = GetFiniteNonNegativeSimulationValue(
            individualPointMovementDuration);
        if (duration <= 0f)
            return formationPosition;

        float normalized = Mathf.Clamp01(
            (enemyTime - movementStartDelay) / duration);
        float curved = EvaluateCurve(individualPointMovementCurve, normalized);
        return Vector3.LerpUnclamped(startPosition, formationPosition, curved);
    }

    private DirectedWaveRuntimeCheckpoint[] GetSimulationPathCheckpoints(
        Transform previewParent)
    {
        DirectedWaveRuntimeCheckpoint[] checkpoints = GetWorldPathCheckpoints();
        if (previewParent == null
            || pathCoordinateSpace == DirectedWaveCoordinateSpace.World)
        {
            return checkpoints;
        }

        for (int i = 0; i < checkpoints.Length; i++)
        {
            DirectedWaveRuntimeCheckpoint checkpoint = checkpoints[i];
            checkpoint.position = MapSimulationPointToPreviewParent(
                checkpoint.position,
                previewParent);
            checkpoints[i] = checkpoint;
        }

        return checkpoints;
    }

    private Vector3 MapSimulationPointToPreviewParent(
        Vector3 position,
        Transform previewParent)
    {
        if (previewParent == null)
            return position;

        Matrix4x4 previewLocalToWorld = previewParent.localToWorldMatrix
            * Matrix4x4.TRS(
                transform.localPosition,
                transform.localRotation,
                transform.localScale);
        return previewLocalToWorld.MultiplyPoint3x4(
            transform.worldToLocalMatrix.MultiplyPoint3x4(position));
    }

    private Vector3 EvaluateSimulationCheckpointPath(
        DirectedWaveRuntimeCheckpoint[] checkpoints,
        float elapsed)
    {
        float remaining = Mathf.Max(0f, elapsed);
        for (int i = 0; i < checkpoints.Length - 1; i++)
        {
            float duration = Mathf.Max(0.01f, checkpoints[i].durationToNext);
            if (remaining <= duration)
            {
                float normalized = Mathf.Clamp01(remaining / duration);
                float curved = EvaluateCurve(checkpoints[i].easeToNext, normalized);
                float pathTime =
                    DirectedWavePathEvaluator.GetParameterAtNormalizedDistance(
                        checkpoints,
                        i,
                        curved);
                return DirectedWavePathEvaluator.EvaluateSegment(
                    checkpoints,
                    i,
                    pathTime);
            }

            remaining -= duration;
        }

        return checkpoints[checkpoints.Length - 1].position;
    }
}
