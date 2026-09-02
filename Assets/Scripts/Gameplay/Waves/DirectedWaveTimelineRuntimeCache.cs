using System.Collections.Generic;
using UnityEngine;

public sealed partial class DirectedEnemySubWave
{
    private readonly struct SimulationCommandBoundary
    {
        public readonly DirectedWavePostCommand command;
        public readonly float startTime;
        public readonly float duration;
        public readonly float holdDuration;
        public readonly bool isBackground;

        public SimulationCommandBoundary(
            DirectedWavePostCommand command,
            float startTime,
            float duration,
            float holdDuration,
            bool isBackground)
        {
            this.command = command;
            this.startTime = startTime;
            this.duration = duration;
            this.holdDuration = holdDuration;
            this.isBackground = isBackground;
        }
    }

    private sealed class SimulationCommandArrayPlan
    {
        public readonly DirectedWavePostCommand[] commands;
        public readonly bool allowLoops;
        public readonly SimulationCommandBoundary[] boundaries;
        public int count;
        public float duration;

        public SimulationCommandArrayPlan(
            DirectedWavePostCommand[] commands,
            bool allowLoops)
        {
            this.commands = commands;
            this.allowLoops = allowLoops;
            boundaries = new SimulationCommandBoundary[commands.Length];
        }
    }

    private sealed class RuntimeTimelineEvaluationContext
    {
        private readonly List<Dictionary<int, Vector3>> positionBuffers = new();
        private readonly Dictionary<
            DirectedWavePostCommand[],
            SimulationCommandArrayPlan> commandArrayPlans = new();
        private readonly Dictionary<DirectedWavePostCommand, float>
            commandDurations = new();
        private Vector3[] morphPositions = System.Array.Empty<Vector3>();
        private int positionBufferCursor;
        private float frameElapsed;

        public readonly List<SimulatedBackgroundCommand> backgrounds = new(8);
        public readonly Dictionary<
            DirectedWavePostCommand,
            SimulatedBackgroundCommand> activeBackgrounds = new(16);
        public readonly List<int> morphFreeTargetIndices = new(16);

        public void BeginFrame(float elapsed)
        {
            positionBufferCursor = 0;
            backgrounds.Clear();
            activeBackgrounds.Clear();
            morphFreeTargetIndices.Clear();
            frameElapsed = Mathf.Max(0f, elapsed);
        }

        public void RecordBackground(
            DirectedWavePostCommand command,
            float startTime)
        {
            if (command == null || startTime > frameElapsed)
                return;

            if (activeBackgrounds.TryGetValue(
                    command,
                    out SimulatedBackgroundCommand previous))
            {
                float previousEnd = previous.command.infiniteParallel
                    ? Mathf.Infinity
                    : previous.startTime
                        + Mathf.Max(0.01f, previous.command.duration);
                if (startTime < previousEnd)
                    return;
            }

            activeBackgrounds[command] = new SimulatedBackgroundCommand(
                command,
                startTime);
        }

        public Dictionary<int, Vector3> RentPositions(int capacity)
        {
            Dictionary<int, Vector3> result;
            if (positionBufferCursor < positionBuffers.Count)
            {
                result = positionBuffers[positionBufferCursor];
                result.Clear();
            }
            else
            {
                result = new Dictionary<int, Vector3>(Mathf.Max(1, capacity));
                positionBuffers.Add(result);
            }

            positionBufferCursor++;
            return result;
        }

        public Dictionary<int, Vector3> RentPositions(
            Dictionary<int, Vector3> source)
        {
            Dictionary<int, Vector3> result = RentPositions(source.Count);
            foreach (KeyValuePair<int, Vector3> pair in source)
                result[pair.Key] = pair.Value;

            return result;
        }

        public int MarkPositionBuffers()
        {
            return positionBufferCursor;
        }

        public void RestorePositionBuffers(int marker)
        {
            positionBufferCursor = Mathf.Clamp(
                marker,
                0,
                positionBufferCursor);
        }

        public Vector3[] RentMorphPositions(int count)
        {
            if (morphPositions.Length < count)
                morphPositions = new Vector3[count];

            return morphPositions;
        }

        public bool TryGetPlan(
            DirectedWavePostCommand[] commands,
            bool allowLoops,
            out SimulationCommandArrayPlan plan)
        {
            if (commands != null
                && commandArrayPlans.TryGetValue(commands, out plan)
                && plan.allowLoops == allowLoops)
            {
                return true;
            }

            plan = null;
            return false;
        }

        public void StorePlan(SimulationCommandArrayPlan plan)
        {
            commandArrayPlans[plan.commands] = plan;
        }

        public bool TryGetCommandDuration(
            DirectedWavePostCommand command,
            out float duration)
        {
            return commandDurations.TryGetValue(command, out duration);
        }

        public void StoreCommandDuration(
            DirectedWavePostCommand command,
            float duration)
        {
            commandDurations[command] = duration;
        }

        public void Reset()
        {
            BeginFrame(0f);
            commandArrayPlans.Clear();
            commandDurations.Clear();
        }
    }

    private readonly RuntimeTimelineEvaluationContext
        runtimeTimelineEvaluationContext = new();
}
