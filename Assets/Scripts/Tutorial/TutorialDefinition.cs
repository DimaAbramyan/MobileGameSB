#if UNITY_EDITOR || INCLUDE_TUTORIAL
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Localization;

namespace Game.Tutorial
{
    public enum TutorialConditionType
    {
        Always, GameplayReady, MainMenuReady, TwoShipsReady, TimeElapsed, ManualConfirmation,
        PlayerMoved, ShipSwitched, AbilityUsed, DamageTaken, EnemyDefeated, WaveCleared, PlayerDied, Signal
    }
    public enum TutorialConditionMatch { All, Any }
    public enum TutorialPlacement { Top, Bottom, NearTarget }
    public enum TutorialTarget { None, Player, ActiveShip, RegisteredAnchor }

    [Serializable]
    public sealed class TutorialCondition
    {
        public TutorialConditionType type;
        [Min(0f)] public float value = 1f;
        public string signal;
    }

    [Serializable]
    public sealed class TutorialConditionGroup
    {
        public TutorialConditionMatch match;
        public List<TutorialCondition> conditions = new();
        public bool Uses(TutorialConditionType type)
        {
            for (int i = 0; i < conditions.Count; i++) if (conditions[i]?.type == type) return true;
            return false;
        }
    }

    [Serializable]
    public sealed class TutorialStep
    {
        public string id = "step";
        public LocalizedString hint = new("UI", "tutorial.welcome");
        public TutorialConditionGroup start = new();
        public TutorialConditionGroup completion = new()
        { conditions = new() { new() { type = TutorialConditionType.ManualConfirmation } } };
        [Min(0f)] public float startDelay;
        public bool canSkip = true;
        public TutorialPlacement placement;
        public TutorialTarget target;
        public string anchorId;
        public bool highlightTarget;
        public bool showPointer;
        public Vector2 pointerOffset = new(0f, 80f);
        [Tooltip("Scoped input restrictions. Released on step completion, skip, stop, scene exit and disposal. Never required for ordinary play.")]
        public PlayerActions restrictActions;
    }

    [CreateAssetMenu(menuName = "Game/Tutorial/Definition", fileName = "TutorialDefinition")]
    public sealed class TutorialDefinition : ScriptableObject
    {
        [Min(1)] public int version = 1;
        [Tooltip("Optional existing LevelConfig. Explicit launch uses the ordinary selected team and Fighting scene. Empty teaches inside the battle the player launches normally.")]
        public LevelConfig level;
        public List<TutorialStep> steps = new();

        public bool TryValidate(out string error)
        {
            if (version < 1 || steps == null || steps.Count == 0) { error = "A positive version and at least one step are required."; return false; }
            var ids = new HashSet<string>();
            for (int i = 0; i < steps.Count; i++)
            {
                var step = steps[i];
                if (step == null || string.IsNullOrWhiteSpace(step.id) || !ids.Add(step.id))
                { error = $"Step {i + 1} needs a unique stable ID."; return false; }
                if (step.hint == null || step.hint.IsEmpty) { error = $"Step '{step.id}' needs a localized hint."; return false; }
                if (!float.IsFinite(step.startDelay) || step.startDelay < 0f)
                { error = $"Step '{step.id}' needs a finite, non-negative delay."; return false; }
                if (!ValidateGroup(step.start) || !ValidateGroup(step.completion))
                { error = $"Step '{step.id}' has an invalid condition or signal."; return false; }
                if (step.target == TutorialTarget.RegisteredAnchor && string.IsNullOrWhiteSpace(step.anchorId))
                { error = $"Step '{step.id}' needs an Anchor ID."; return false; }
            }
            error = null; return true;
        }
        private static bool ValidateGroup(TutorialConditionGroup group)
        {
            if (group?.conditions == null) return false;
            foreach (var c in group.conditions)
                if (c == null || !Enum.IsDefined(typeof(TutorialConditionType), c.type)
                    || !float.IsFinite(c.value) || c.value < 0f
                    || c.type == TutorialConditionType.Signal && string.IsNullOrWhiteSpace(c.signal)) return false;
            return true;
        }
    }
}
#endif
