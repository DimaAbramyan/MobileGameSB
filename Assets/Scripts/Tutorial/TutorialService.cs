#if UNITY_EDITOR || INCLUDE_TUTORIAL
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using Zenject;

namespace Game.Tutorial
{
    public interface ITutorialService
    {
        bool IsRunning { get; }
        TutorialStep CurrentStep { get; }
        TutorialSignals Signals { get; }
        event Action Changed;
        bool TryStart(bool developmentReplay = false);
        void Confirm();
        void Skip();
        void SkipStep();
        void Stop();
        void Publish(string signal);
        bool ResetProgressForDevelopment();
        Task<bool> LaunchConfiguredLevelAsync();
        void AttachPlayer(PlayerController player, bool twoShipsReady);
        void DetachPlayer(PlayerController player);
        void RegisterAnchor(string id, Transform target);
        void UnregisterAnchor(string id, Transform target);
    }

    public interface ITutorialPresentation : IDisposable
    {
        void Show(TutorialStep step, Action confirm, Action skip, Action skipStep);
        void Hide();
        void Tick(Transform target, Camera camera);
    }

    public sealed class TutorialService : ITutorialService, IInitializable, ITickable, IDisposable
    {
        private readonly TutorialConfiguration config;
        private readonly SaveManager saves;
        private readonly ITutorialPresentation view;
        private readonly ILocalizationService localization;
        private readonly BattleLaunchService launch;
        private readonly Dictionary<string, Transform> anchors = new(StringComparer.Ordinal);
        private TutorialSession session;
        private PlayerController player;
        private IDisposable restrictions;
        private bool persistent, disposed, initialized, launching, automaticAttempted;
        public TutorialSignals Signals { get; } = new();
        public bool IsRunning => session?.IsRunning == true;
        public TutorialStep CurrentStep => session?.CurrentStep;
        public event Action Changed;

        public TutorialService(TutorialConfiguration config, SaveManager saves, ITutorialPresentation view,
            ILocalizationService localization, BattleLaunchService launch)
        { this.config = config; this.saves = saves; this.view = view; this.localization = localization; this.launch = launch; }

        public void Initialize()
        {
            if (initialized || disposed) return;
            initialized = true;
            SceneManager.sceneLoaded += SceneLoaded;
            UpdateMenuState();
            TryAutoStart();
        }
        private void SceneLoaded(Scene scene, LoadSceneMode mode) { UpdateMenuState(); TryAutoStart(); }
        private void UpdateMenuState() => Signals.MainMenuReady = SceneManager.GetSceneByName(LevelLoader.MainMenuSceneName).isLoaded;
        private void TryAutoStart()
        {
            if (!config.includeTutorial || automaticAttempted || IsRunning || config.autoStart == TutorialAutoStart.Manual) return;
            bool menu = Signals.MainMenuReady && config.autoStart != TutorialAutoStart.Battle;
            bool battle = Signals.GameplayReady && config.autoStart != TutorialAutoStart.MainMenu;
            if (!menu && !battle) return;
            automaticAttempted = true;
            if (config.forceStartInDevelopment && DevelopmentAllowed) TryStart(true);
            else TryStart();
        }
        private bool DevelopmentAllowed => config.allowDevelopmentLaunch && (Application.isEditor || Debug.isDebugBuild);
        public bool TryStart(bool developmentReplay = false)
        {
            if (disposed || !config.includeTutorial || IsRunning || developmentReplay && !DevelopmentAllowed) return false;
            if (config.definition == null || !config.definition.TryValidate(out _))
            { Debug.LogWarning("Tutorial configuration needs a valid Definition."); return false; }
            var saved = saves.ReadOnboarding();
            bool sameVersion = saved.tutorialVersion == config.definition.version;
            if (!developmentReplay && (saved.tutorialStatus == TutorialStatus.Completed || saved.tutorialStatus == TutorialStatus.Skipped)
                && (sameVersion || !config.restartOnNewVersion)) return false;
            persistent = !developmentReplay;
            if (session != null) session.Changed -= SessionChanged;
            session = new TutorialSession(config.definition, Signals);
            session.Changed += SessionChanged;
            InitializeLocalization();
            session.Begin(!developmentReplay && sameVersion ? saved.currentStepId : null,
                !developmentReplay && sameVersion ? saved.completedStepIds : null);
            return true;
        }
        private async void InitializeLocalization()
        {
            try { await localization.InitializeAsync(); }
            catch (Exception exception) { Debug.LogWarning($"Tutorial localization initialization failed: {exception.Message}"); }
        }
        public void Tick()
        {
            if (!IsRunning || disposed) return;
            if (!config.includeTutorial) { Stop(); return; }
            if (NeedsGameplay(CurrentStep) && !Signals.GameplayReady) { view.Hide(); ReleaseRestrictions(); return; }
            session.Tick(Time.unscaledDeltaTime);
            if (IsRunning && session.IsPresenting) view.Tick(ResolveTarget(CurrentStep), player != null ? player.GameplayCamera : null);
        }
        private static bool NeedsGameplay(TutorialStep step) => step != null &&
            (step.target == TutorialTarget.Player || step.target == TutorialTarget.ActiveShip || step.restrictActions != PlayerActions.None
            || UsesGameplay(step.start) || UsesGameplay(step.completion));
        private static bool UsesGameplay(TutorialConditionGroup group)
        {
            if (group.conditions.Count == 0) return false;
            bool any = group.match == TutorialConditionMatch.Any;
            foreach (var c in group.conditions)
            {
                bool required = c.type == TutorialConditionType.GameplayReady || c.type == TutorialConditionType.TwoShipsReady
                    || c.type >= TutorialConditionType.PlayerMoved && c.type <= TutorialConditionType.PlayerDied;
                if (!any && required) return true;
                if (any && !required) return false;
            }
            return any;
        }
        private void SessionChanged()
        {
            ReleaseRestrictions();
            if (persistent)
            {
                bool saved = saves.UpdateOnboarding(data =>
                {
                    data.tutorialVersion = config.definition.version;
                    data.tutorialStatus = session.Status;
                    data.currentStepId = CurrentStep?.id ?? string.Empty;
                    data.completedStepIds = new List<string>(session.Completed);
                });
                if (!saved) Debug.LogWarning($"Tutorial progress could not be saved: {saves.LastError}");
            }
            RefreshPresentation(); Changed?.Invoke();
        }
        private void RefreshPresentation()
        {
            if (!IsRunning || !session.IsPresenting || NeedsGameplay(CurrentStep) && !Signals.GameplayReady) { view.Hide(); return; }
            if (player != null && CurrentStep.restrictActions != PlayerActions.None)
                restrictions = player.ActionLocks.Acquire(CurrentStep.restrictActions);
            view.Show(CurrentStep, Confirm, Skip, SkipStep);
        }
        private void ReleaseRestrictions() { restrictions?.Dispose(); restrictions = null; }
        public void Confirm() { if (session?.IsPresenting == true) session.Confirm(); }
        public void Skip() => session?.Skip();
        public void SkipStep() => session?.SkipStep();
        public void Publish(string signal) { if (IsRunning) Signals.Publish(signal); }
        public void Stop()
        {
            if (session != null) { session.Changed -= SessionChanged; session.Stop(); session = null; }
            ReleaseRestrictions(); view.Hide(); Changed?.Invoke();
        }
        public bool ResetProgressForDevelopment()
        {
            if (!DevelopmentAllowed) return false;
            Stop();
            return saves.UpdateOnboarding(data =>
            {
                data.tutorialStatus = TutorialStatus.NotStarted; data.currentStepId = string.Empty;
                data.completedStepIds.Clear(); data.tutorialVersion = config.definition != null ? config.definition.version : 1;
            });
        }
        public async Task<bool> LaunchConfiguredLevelAsync()
        {
            if (!config.includeTutorial || launching || config.definition == null || config.definition.level == null) return false;
            if (!launch.TryPrepareBattle(out string error)) { Debug.LogWarning(error); return false; }
            if (!IsRunning && !TryStart(DevelopmentAllowed)) return false;
            launching = true;
            try
            {
                LevelLoader.SelectLevel(config.definition.level);
                var operation = SceneManager.LoadSceneAsync(LevelLoader.FightingSceneName);
                if (operation == null) return false;
                while (!operation.isDone) await Task.Yield();
                return true;
            }
            finally { launching = false; }
        }
        public void AttachPlayer(PlayerController value, bool twoShipsReady)
        {
            bool changed = player != value;
            bool wasReady = Signals.GameplayReady;
            player = value; Signals.GameplayReady = value != null && value.CurrentShip != null;
            Signals.TwoShipsReady = twoShipsReady;
            TryAutoStart();
            if (changed || wasReady != Signals.GameplayReady) { ReleaseRestrictions(); RefreshPresentation(); }
        }
        public void DetachPlayer(PlayerController value)
        {
            if (player != value) return;
            player = null; Signals.GameplayReady = Signals.TwoShipsReady = false;
            ReleaseRestrictions(); view.Hide();
        }
        public void RegisterAnchor(string id, Transform target)
        {
            if (string.IsNullOrWhiteSpace(id) || target == null) return;
            if (anchors.TryGetValue(id, out var previous) && previous != null && previous != target)
                Debug.LogWarning($"Duplicate Tutorial Anchor ID '{id}'. Use unique IDs per scene.");
            anchors[id] = target;
        }
        public void UnregisterAnchor(string id, Transform target)
        { if (!string.IsNullOrWhiteSpace(id) && anchors.TryGetValue(id, out var current) && current == target) anchors.Remove(id); }
        private Transform ResolveTarget(TutorialStep step) => step.target switch
        {
            TutorialTarget.Player => player != null ? player.transform : null,
            TutorialTarget.ActiveShip => player != null && player.CurrentShip != null ? player.CurrentShip.transform : null,
            TutorialTarget.RegisteredAnchor => anchors.TryGetValue(step.anchorId, out var anchor) ? anchor : null,
            _ => null
        };
        public void Dispose()
        {
            if (disposed) return;
            disposed = true; SceneManager.sceneLoaded -= SceneLoaded; Stop(); view.Dispose(); anchors.Clear();
        }
    }
}
#endif
