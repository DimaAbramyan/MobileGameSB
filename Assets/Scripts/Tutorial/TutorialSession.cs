#if UNITY_EDITOR || INCLUDE_TUTORIAL
using System;
using System.Collections.Generic;

namespace Game.Tutorial
{
    // Event counters and movement are observed through the existing gameplay, never simulated here.
    public sealed class TutorialSignals
    {
        public bool GameplayReady, MainMenuReady, TwoShipsReady;
        public float Movement;
        public int Switches, Abilities, Damage, EnemyDeaths, Waves, PlayerDeaths, Confirmations;
        private readonly Dictionary<string, int> signals = new();
        public void Publish(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return;
            signals.TryGetValue(id, out int count); signals[id] = count + 1;
        }
        public Snapshot Capture() => new()
        {
            Movement = Movement, Switches = Switches, Abilities = Abilities, Damage = Damage,
            EnemyDeaths = EnemyDeaths, Waves = Waves, PlayerDeaths = PlayerDeaths, Confirmations = Confirmations,
            Signals = new Dictionary<string, int>(signals)
        };
        public struct Snapshot
        {
            public float Movement;
            public int Switches, Abilities, Damage, EnemyDeaths, Waves, PlayerDeaths, Confirmations;
            public Dictionary<string, int> Signals;
        }
        public bool Evaluate(TutorialConditionGroup group, in Snapshot baseline, float elapsed)
        {
            if (group.conditions.Count == 0) return true;
            bool any = group.match == TutorialConditionMatch.Any;
            foreach (var c in group.conditions)
            {
                bool met = Evaluate(c, baseline, elapsed);
                if (any && met) return true;
                if (!any && !met) return false;
            }
            return !any;
        }
        private bool Evaluate(TutorialCondition c, in Snapshot b, float elapsed)
        {
            float count = c.type switch
            {
                TutorialConditionType.Always => float.PositiveInfinity,
                TutorialConditionType.GameplayReady => GameplayReady ? float.PositiveInfinity : -1f,
                TutorialConditionType.MainMenuReady => MainMenuReady ? float.PositiveInfinity : -1f,
                TutorialConditionType.TwoShipsReady => TwoShipsReady ? float.PositiveInfinity : -1f,
                TutorialConditionType.TimeElapsed => elapsed,
                TutorialConditionType.ManualConfirmation => Confirmations - b.Confirmations,
                TutorialConditionType.PlayerMoved => Movement - b.Movement,
                TutorialConditionType.ShipSwitched => Switches - b.Switches,
                TutorialConditionType.AbilityUsed => Abilities - b.Abilities,
                TutorialConditionType.DamageTaken => Damage - b.Damage,
                TutorialConditionType.EnemyDefeated => EnemyDeaths - b.EnemyDeaths,
                TutorialConditionType.WaveCleared => Waves - b.Waves,
                TutorialConditionType.PlayerDied => PlayerDeaths - b.PlayerDeaths,
                TutorialConditionType.Signal => Signal(c.signal) - (b.Signals.TryGetValue(c.signal, out int value) ? value : 0),
                _ => -1f
            };
            return count >= (c.type == TutorialConditionType.ManualConfirmation ? 1f : c.value);
        }
        private int Signal(string id) => signals.TryGetValue(id, out int value) ? value : 0;
    }

    // Small state machine; scheduling never creates enemies, projectiles or player input.
    public sealed class TutorialSession
    {
        private readonly TutorialDefinition definition;
        private readonly TutorialSignals signals;
        private TutorialSignals.Snapshot baseline;
        private int index;
        private float elapsed, delayRemaining;
        private bool startLatched;
        public bool IsRunning { get; private set; }
        public bool IsPresenting { get; private set; }
        public TutorialStatus Status { get; private set; }
        public TutorialStep CurrentStep => IsRunning ? definition.steps[index] : null;
        public IReadOnlyList<string> Completed => completed;
        private readonly List<string> completed = new();
        public event Action Changed;

        public TutorialSession(TutorialDefinition definition, TutorialSignals signals)
        {
            if (!definition.TryValidate(out string error)) throw new ArgumentException(error);
            this.definition = definition; this.signals = signals;
        }
        public void Begin(string resumeStep = null, IReadOnlyList<string> previous = null)
        {
            completed.Clear(); index = 0;
            if (previous != null)
                for (int i = 0; i < previous.Count; i++)
                    if (definition.steps.Exists(s => s.id == previous[i]) && !completed.Contains(previous[i])) completed.Add(previous[i]);
            int resume = string.IsNullOrEmpty(resumeStep) ? -1 : definition.steps.FindIndex(s => s.id == resumeStep);
            if (resume >= 0) index = resume;
            else while (index < definition.steps.Count && completed.Contains(definition.steps[index].id)) index++;
            Status = TutorialStatus.InProgress; IsRunning = true;
            if (index >= definition.steps.Count) { Finish(TutorialStatus.Completed); return; }
            WaitForStep();
        }
        public void Tick(float delta)
        {
            if (!IsRunning || !float.IsFinite(delta) || delta < 0f) return;
            elapsed += delta;
            var step = CurrentStep;
            if (!IsPresenting)
            {
                if (!startLatched)
                {
                    if (!signals.Evaluate(step.start, baseline, elapsed)) return;
                    startLatched = true; delayRemaining = Math.Max(0f, step.startDelay);
                }
                delayRemaining -= delta;
                if (delayRemaining > 0f) return;
                baseline = signals.Capture(); elapsed = 0f; IsPresenting = true; Changed?.Invoke();
                return;
            }
            if (signals.Evaluate(step.completion, baseline, elapsed)) Advance();
        }
        public void Confirm() { if (IsRunning) signals.Confirmations++; }
        public void SkipStep() { if (IsRunning && IsPresenting && CurrentStep.canSkip) Advance(); }
        public void Skip() { if (IsRunning) Finish(TutorialStatus.Skipped); }
        public void Stop() { IsRunning = IsPresenting = false; Changed?.Invoke(); }
        private void Advance()
        {
            if (!completed.Contains(CurrentStep.id)) completed.Add(CurrentStep.id);
            if (++index >= definition.steps.Count) Finish(TutorialStatus.Completed);
            else WaitForStep();
        }
        private void WaitForStep()
        {
            IsPresenting = startLatched = false; elapsed = delayRemaining = 0f;
            baseline = signals.Capture(); Changed?.Invoke();
        }
        private void Finish(TutorialStatus status)
        {
            Status = status; IsRunning = IsPresenting = false; Changed?.Invoke();
        }
    }
}
#endif
