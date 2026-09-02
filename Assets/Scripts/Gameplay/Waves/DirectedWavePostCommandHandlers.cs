using System.Collections.Generic;
using UnityEngine;

public sealed partial class DirectedEnemySubWave
{
    private interface IDirectedWavePostCommandHandler
    {
        DirectedWavePostCommandType Type { get; }

        float GetDuration(
            DirectedEnemySubWave wave,
            DirectedWavePostCommand command);

        Dictionary<int, Vector3> EvaluateFrame(
            DirectedEnemySubWave wave,
            DirectedWavePostCommand command,
            Dictionary<int, Vector3> input,
            float elapsed,
            float timelineDuration,
            bool finalFrame,
            RuntimeTimelineEvaluationContext runtimeContext);

    }

    private abstract class DirectedWavePostCommandHandler
        : IDirectedWavePostCommandHandler
    {
        public abstract DirectedWavePostCommandType Type { get; }

        public virtual float GetDuration(
            DirectedEnemySubWave wave,
            DirectedWavePostCommand command)
        {
            return Mathf.Max(0.01f, command.duration);
        }

        public virtual Dictionary<int, Vector3> EvaluateFrame(
            DirectedEnemySubWave wave,
            DirectedWavePostCommand command,
            Dictionary<int, Vector3> input,
            float elapsed,
            float timelineDuration,
            bool finalFrame,
            RuntimeTimelineEvaluationContext runtimeContext)
        {
            return CopySimulationPositions(input, runtimeContext);
        }

        protected static float GetCommandTime(
            DirectedWavePostCommand command,
            float elapsed)
        {
            return Mathf.Min(elapsed, Mathf.Max(0.01f, command.duration));
        }

        protected static float GetContinuousTime(
            float elapsed,
            float timelineDuration)
        {
            return Mathf.Min(elapsed, Mathf.Max(0.01f, timelineDuration));
        }

        protected static float GetContinuousCommandDuration(
            DirectedWavePostCommand command,
            float naturalDuration)
        {
            switch (command.completionMode)
            {
                case DirectedWavePostCommandCompletionMode.Infinite:
                    return Mathf.Infinity;

                case DirectedWavePostCommandCompletionMode.CompleteRoute:
                    return naturalDuration > 0.0001f
                        ? naturalDuration
                        : Mathf.Max(0.01f, command.duration);

                default:
                    return Mathf.Max(0.01f, command.duration);
            }
        }
    }

    private sealed class LocalMovementPostCommandHandler
        : DirectedWavePostCommandHandler
    {
        public override DirectedWavePostCommandType Type =>
            DirectedWavePostCommandType.LocalMovement;

        public override Dictionary<int, Vector3> EvaluateFrame(
            DirectedEnemySubWave wave,
            DirectedWavePostCommand command,
            Dictionary<int, Vector3> input,
            float elapsed,
            float timelineDuration,
            bool finalFrame,
            RuntimeTimelineEvaluationContext runtimeContext)
        {
            float duration = GetDuration(wave, command);
            float time = GetCommandTime(command, elapsed);
            Vector3 currentCenter = wave.GetPositionsCenter(input);
            Vector3 targetCenter = wave.ToWorld(
                command.targetOffset,
                command.targetOffsetCoordinateSpace);
            Dictionary<int, Vector3> target = OffsetPositions(
                input,
                targetCenter - currentCenter,
                runtimeContext);
            float normalized = Mathf.Clamp01(time / duration);
            return LerpPositions(
                input,
                target,
                EvaluateCurve(command.curve, normalized),
                runtimeContext);
        }
    }

    private sealed class PatrolPostCommandHandler
        : DirectedWavePostCommandHandler
    {
        public override DirectedWavePostCommandType Type =>
            DirectedWavePostCommandType.Patrol;

        public override float GetDuration(
            DirectedEnemySubWave wave,
            DirectedWavePostCommand command)
        {
            return GetContinuousCommandDuration(
                command,
                wave.GetPatrolTotalDuration());
        }

        public override Dictionary<int, Vector3> EvaluateFrame(
            DirectedEnemySubWave wave,
            DirectedWavePostCommand command,
            Dictionary<int, Vector3> input,
            float elapsed,
            float timelineDuration,
            bool finalFrame,
            RuntimeTimelineEvaluationContext runtimeContext)
        {
            float time = GetContinuousTime(elapsed, timelineDuration);
            Vector3 currentCenter = wave.GetPositionsCenter(input);
            Vector3 targetCenter = wave.GetPatrolCenterPosition(time);
            return OffsetPositions(
                input,
                targetCenter - currentCenter,
                runtimeContext);
        }
    }

    private sealed class WobblePostCommandHandler
        : DirectedWavePostCommandHandler
    {
        public override DirectedWavePostCommandType Type =>
            DirectedWavePostCommandType.Wobble;

        public override float GetDuration(
            DirectedEnemySubWave wave,
            DirectedWavePostCommand command)
        {
            float frequency = Mathf.Abs(wave.wobbleFrequency);
            float cycleDuration = frequency > 0.0001f
                ? Mathf.PI * 2f / frequency
                : 0f;
            return GetContinuousCommandDuration(command, cycleDuration);
        }

        public override Dictionary<int, Vector3> EvaluateFrame(
            DirectedEnemySubWave wave,
            DirectedWavePostCommand command,
            Dictionary<int, Vector3> input,
            float elapsed,
            float timelineDuration,
            bool finalFrame,
            RuntimeTimelineEvaluationContext runtimeContext)
        {
            if (finalFrame)
                return CopySimulationPositions(input, runtimeContext);

            float time = GetContinuousTime(elapsed, timelineDuration);
            return wave.ApplyOverlayFrame(
                input,
                true,
                false,
                time,
                runtimeContext);
        }
    }

    private sealed class CircularMovementPostCommandHandler
        : DirectedWavePostCommandHandler
    {
        public override DirectedWavePostCommandType Type =>
            DirectedWavePostCommandType.CircularMovement;

        public override float GetDuration(
            DirectedEnemySubWave wave,
            DirectedWavePostCommand command)
        {
            float speed = Mathf.Abs(wave.selfRotationDegreesPerSecond);
            float cycleDuration = speed > 0.0001f ? 360f / speed : 0f;
            return GetContinuousCommandDuration(command, cycleDuration);
        }

        public override Dictionary<int, Vector3> EvaluateFrame(
            DirectedEnemySubWave wave,
            DirectedWavePostCommand command,
            Dictionary<int, Vector3> input,
            float elapsed,
            float timelineDuration,
            bool finalFrame,
            RuntimeTimelineEvaluationContext runtimeContext)
        {
            if (finalFrame)
                return CopySimulationPositions(input, runtimeContext);

            float time = GetContinuousTime(elapsed, timelineDuration);
            return wave.ApplyOverlayFrame(
                input,
                false,
                true,
                time,
                runtimeContext);
        }
    }

    private sealed class FormationRotationPostCommandHandler
        : DirectedWavePostCommandHandler
    {
        public override DirectedWavePostCommandType Type =>
            DirectedWavePostCommandType.FormationRotation;

        public override float GetDuration(
            DirectedEnemySubWave wave,
            DirectedWavePostCommand command)
        {
            if (!command.continuousFormationRotation)
                return base.GetDuration(wave, command);

            float speed = Mathf.Abs(command.rotationDegrees) > 0.0001f
                ? Mathf.Abs(command.rotationDegrees)
                : Mathf.Abs(wave.formationRotationDegreesPerSecond);
            float cycleDuration = speed > 0.0001f ? 360f / speed : 0f;
            return GetContinuousCommandDuration(command, cycleDuration);
        }

        public override Dictionary<int, Vector3> EvaluateFrame(
            DirectedEnemySubWave wave,
            DirectedWavePostCommand command,
            Dictionary<int, Vector3> input,
            float elapsed,
            float timelineDuration,
            bool finalFrame,
            RuntimeTimelineEvaluationContext runtimeContext)
        {
            float duration = GetDuration(wave, command);
            float time = command.continuousFormationRotation
                ? GetContinuousTime(elapsed, timelineDuration)
                : GetCommandTime(command, elapsed);
            float angle = wave.GetFormationRotationAngle(
                command,
                time,
                duration);
            return RotatePositions(
                input,
                wave.GetPositionsCenter(input),
                angle,
                runtimeContext);
        }
    }

    private sealed class FormationMorphPostCommandHandler
        : DirectedWavePostCommandHandler
    {
        public override DirectedWavePostCommandType Type =>
            DirectedWavePostCommandType.FormationMorph;

        public override Dictionary<int, Vector3> EvaluateFrame(
            DirectedEnemySubWave wave,
            DirectedWavePostCommand command,
            Dictionary<int, Vector3> input,
            float elapsed,
            float timelineDuration,
            bool finalFrame,
            RuntimeTimelineEvaluationContext runtimeContext)
        {
            if (command.morphTarget == null)
                return CopySimulationPositions(input, runtimeContext);

            float duration = GetDuration(wave, command);
            float time = GetCommandTime(command, elapsed);
            Dictionary<int, Vector3> target = wave.BuildMorphTarget(
                input,
                command.morphTarget,
                wave.GetPositionsCenter(input),
                runtimeContext);
            float normalized = Mathf.Clamp01(time / duration);
            return LerpPositions(
                input,
                target,
                EvaluateCurve(command.curve, normalized),
                runtimeContext);
        }
    }

    private sealed class FormationReorderPostCommandHandler
        : DirectedWavePostCommandHandler
    {
        public override DirectedWavePostCommandType Type =>
            DirectedWavePostCommandType.FormationReorder;

        public override float GetDuration(
            DirectedEnemySubWave wave,
            DirectedWavePostCommand command)
        {
            return wave.GetFormationReorderDuration(command);
        }

        public override Dictionary<int, Vector3> EvaluateFrame(
            DirectedEnemySubWave wave,
            DirectedWavePostCommand command,
            Dictionary<int, Vector3> input,
            float elapsed,
            float timelineDuration,
            bool finalFrame,
            RuntimeTimelineEvaluationContext runtimeContext)
        {
            if (input == null)
            {
                return runtimeContext != null
                    ? runtimeContext.RentPositions(0)
                    : new Dictionary<int, Vector3>();
            }

            if (input.Count == 0)
                return CopySimulationPositions(input, runtimeContext);

            int[] targetIndices = wave.GetFormationReorderTargets(
                command,
                input.Count);
            Vector3 targetCenterOffset = command.formationReorderUseTargetCenter
                ? command.formationReorderTargetCenter
                    - GetFormationReorderCenter(input)
                : Vector3.zero;
            float speed = Mathf.Max(0.01f, command.formationReorderSpeed);
            float startInterval = Mathf.Max(
                0f,
                command.formationReorderStartInterval);
            int shipsPerBatch = Mathf.Max(
                1,
                command.formationReorderShipsPerBatch);
            Dictionary<int, Vector3> result = runtimeContext != null
                ? runtimeContext.RentPositions(input.Count)
                : new Dictionary<int, Vector3>(input.Count);

            foreach (KeyValuePair<int, Vector3> pair in input)
            {
                int sourceIndex = pair.Key;
                int targetIndex = sourceIndex >= 0
                    && sourceIndex < targetIndices.Length
                    ? targetIndices[sourceIndex]
                    : sourceIndex;
                if (!input.TryGetValue(targetIndex, out Vector3 target))
                {
                    result[sourceIndex] = pair.Value;
                    continue;
                }

                target += targetCenterOffset;

                if (finalFrame)
                {
                    result[sourceIndex] = target;
                    continue;
                }

                float startTime = sourceIndex >= 0
                    ? sourceIndex / shipsPerBatch * startInterval
                    : 0f;
                float moveTime = Mathf.Max(0f, elapsed - startTime);
                result[sourceIndex] = Vector3.MoveTowards(
                    pair.Value,
                    target,
                    speed * moveTime);
            }

            return result;
        }

        private static Vector3 GetFormationReorderCenter(
            Dictionary<int, Vector3> positions)
        {
            if (positions == null || positions.Count == 0)
                return Vector3.zero;

            Vector3 center = Vector3.zero;
            foreach (Vector3 position in positions.Values)
                center += position;

            return center / positions.Count;
        }
    }

    private sealed class WaitPostCommandHandler
        : DirectedWavePostCommandHandler
    {
        public override DirectedWavePostCommandType Type =>
            DirectedWavePostCommandType.Wait;
    }

    private static readonly IDirectedWavePostCommandHandler[]
        PostCommandHandlers =
        {
            new PatrolPostCommandHandler(),
            new LocalMovementPostCommandHandler(),
            new WobblePostCommandHandler(),
            new CircularMovementPostCommandHandler(),
            new FormationRotationPostCommandHandler(),
            new FormationMorphPostCommandHandler(),
            new FormationReorderPostCommandHandler(),
            new WaitPostCommandHandler()
        };

    private static bool TryGetPostCommandHandler(
        DirectedWavePostCommandType type,
        out IDirectedWavePostCommandHandler handler)
    {
        for (int i = 0; i < PostCommandHandlers.Length; i++)
        {
            IDirectedWavePostCommandHandler candidate = PostCommandHandlers[i];
            if (candidate.Type == type)
            {
                handler = candidate;
                return true;
            }
        }

        handler = null;
        return false;
    }
}
