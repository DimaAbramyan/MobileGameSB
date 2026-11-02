using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Game.Tutorial;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Tables;
using Zenject;

public static class VerifyTutorialFoundation
{
    private static readonly List<string> checks = new();
    private static void Check(bool valid, string name)
    { if (!valid) throw new Exception("Tutorial regression: " + name); checks.Add(name); }

    public static object Run()
    {
        checks.Clear();
        var original = TutorialEditorSettings.Configuration;
        Check(original != null && original.includeTutorial && original.definition.TryValidate(out _), "Enabled configuration / valid six-step demo");
        Check(original.definition.steps.Count == 6 && original.definition.level == null, "Demo uses the ordinary player-launched battle, without changing waves");
        TutorialBuildValidation.Validate(BuildTarget.Android);
        Check(true, "Android build configuration passes include/define/preload guard");
        var config = UnityEngine.Object.Instantiate(original); config.autoStart = TutorialAutoStart.Manual;
        config.allowDevelopmentLaunch = true;
        string root = Path.GetFullPath(Path.Combine("AgentArtifacts", "TutorialVerificationData", Guid.NewGuid().ToString("N")));
        var saves = new SaveManager(root);
        var view = new FakeView();
        var service = new TutorialService(config, saves, view, new FakeLocalization(), null);
        try
        {
            Check(saves.ReadOnboarding().tutorialStatus == TutorialStatus.NotStarted, "Fresh profile is explicitly NotStarted");
            var initialSettings = JsonUtility.ToJson(saves.ReadSettings());
            var initialProgression = JsonUtility.ToJson(saves.ReadProgression());
            service.Initialize(); service.Initialize();
            Check(!service.IsRunning && view.Shows == 0, "Manual policy does not create presentation / repeated initialization is safe");
            service.Signals.GameplayReady = service.Signals.TwoShipsReady = true;
            Check(service.TryStart(), "First-start session begins");
            Check(!service.TryStart(), "Duplicate start is rejected");
            service.Tick(); Check(service.CurrentStep.id == "welcome" && view.Visible, "Welcome is presented");
            service.Confirm(); service.Tick(); service.Tick();
            Check(service.CurrentStep.id == "movement_explanation", "Welcome advances to movement explanation");
            service.Confirm(); service.Tick(); service.Tick();
            Check(service.CurrentStep.id == "move", "Real movement condition is active");
            for (int i = 0; i < 5; i++) service.Tick();
            Check(service.CurrentStep.id == "move", "Time alone does not satisfy movement");
            service.Stop();
            Check(!view.Visible && saves.ReadOnboarding().currentStepId == "move", "Stop preserves checkpoint and hides presentation");
            service.Dispose();
            saves = new SaveManager(root); view = new FakeView();
            service = new TutorialService(config, saves, view, new FakeLocalization(), null);
            service.Signals.GameplayReady = service.Signals.TwoShipsReady = true;
            Check(service.TryStart(), "InProgress resumes after reconstruction"); service.Tick();
            Check(service.CurrentStep.id == "move", "Stable step ID resumes the saved checkpoint");
            service.Signals.Movement += .6f; service.Tick(); service.Tick();
            Check(service.CurrentStep.id == "switch_explanation", "Observed movement advances the step");
            service.Confirm(); service.Tick(); service.Tick();
            Check(service.CurrentStep.id == "switch", "Switch condition is active");
            service.Signals.Abilities++; service.Tick();
            Check(service.CurrentStep.id == "switch", "Unrelated gameplay event cannot complete a step");
            service.Signals.Switches++; service.Tick(); service.Tick();
            Check(service.CurrentStep.id == "finish", "Successful switch advances to final confirmation");
            service.Confirm(); service.Tick();
            var saved = new SaveManager(root).ReadOnboarding();
            Check(!service.IsRunning && !view.Visible && saved.tutorialStatus == TutorialStatus.Completed
                && saved.completedStepIds.Count == 6 && saved.tutorialVersion == config.definition.version,
                "Completion, all stable IDs and version survive disk reload");
            Check(!service.TryStart(), "Completed tutorial does not start again");
            Check(service.TryStart(true), "Development replay bypasses completion");
            service.Tick(); service.Skip();
            Check(saves.ReadOnboarding().tutorialStatus == TutorialStatus.Completed, "Replay and Skip never overwrite real completion");
            Check(service.ResetProgressForDevelopment(), "Explicit development reset succeeds");
            Check(service.TryStart(), "Reset allows a normal first-start session"); service.Tick(); service.Skip();
            Check(new SaveManager(root).ReadOnboarding().tutorialStatus == TutorialStatus.Skipped && !service.TryStart(), "Skip is persisted and prevents future automatic starts");
            Check(initialSettings == JsonUtility.ToJson(saves.ReadSettings()) && initialProgression == JsonUtility.ToJson(saves.ReadProgression()),
                "Settings and progression remain unchanged");
            config.includeTutorial = false;
            Check(!service.TryStart(true), "Include OFF wins over force/replay");
            Check(service.ResetProgressForDevelopment(), "Reset remains an explicit development operation");
            Check(!service.TryStart(), "Disabled fresh profile cannot start");
            VerifyConditions(config.definition);
            VerifyMenuConditionAlternative(config, saves);
            VerifyLocks();
            VerifyBridge(config, saves);
            VerifyOffBinding(original);
            VerifyOnBinding(config, saves);
            VerifyFirstLaunch(config, root + "-auto");
            VerifyLocalizationReferences(original);
            VerifySerialization(config.definition);
            string resultPath = "AgentArtifacts/tutorial-verification.json";
            var result = new Result { checks = checks.ToArray(), isolatedSaveRoot = root };
            File.WriteAllText(resultPath, JsonUtility.ToJson(result, true));
            return new { passed = checks.Count, report = resultPath, isolatedSaveRoot = root };
        }
        finally { service.Dispose(); UnityEngine.Object.DestroyImmediate(config); }
    }

    private static void VerifyConditions(TutorialDefinition demo)
    {
        var definition = UnityEngine.Object.Instantiate(demo);
        definition.steps = new List<TutorialStep> { new()
        {
            id = "conditions", hint = new LocalizedString("UI", "tutorial.welcome"), startDelay = .5f,
            start = new() { conditions = new() { new() { type = TutorialConditionType.Signal, signal = "ready", value = 1 } } },
            completion = new() { match = TutorialConditionMatch.All, conditions = new()
            {
                new() { type = TutorialConditionType.TimeElapsed, value = 1 },
                new() { type = TutorialConditionType.AbilityUsed, value = 1 }
            } }
        } };
        try
        {
            var signals = new TutorialSignals(); var session = new TutorialSession(definition, signals);
            session.Begin(); session.Tick(10);
            Check(!session.IsPresenting, "Start signal is required regardless of elapsed time");
            signals.Publish("ready"); session.Tick(.1f);
            Check(!session.IsPresenting, "Optional delay is honored");
            session.Tick(.4f); Check(session.IsPresenting, "Latched start proceeds after delay");
            session.Tick(2); Check(session.IsRunning, "ALL requires time and the gameplay event");
            signals.Abilities++; session.Tick(0); Check(session.Status == TutorialStatus.Completed, "ALL completes when both conditions are satisfied");
            definition.steps[0].completion.match = TutorialConditionMatch.Any;
            session = new TutorialSession(definition, signals); session.Begin(); signals.Publish("ready"); session.Tick(1);
            signals.Abilities++; session.Tick(0);
            Check(session.Status == TutorialStatus.Completed, "ANY accepts one of the configured conditions");
        }
        finally { UnityEngine.Object.DestroyImmediate(definition); }
    }
    private static void VerifyLocks()
    {
        var locks = new PlayerActionLocks();
        var one = locks.Acquire(PlayerActions.Movement | PlayerActions.Ability);
        var two = locks.Acquire(PlayerActions.Movement);
        Check(locks.Blocks(PlayerActions.Movement) && !locks.Blocks(PlayerActions.ShipSwitch), "Restrictions are independent by action");
        one.Dispose(); one.Dispose();
        Check(locks.Blocks(PlayerActions.Movement) && !locks.Blocks(PlayerActions.Ability), "Idempotent release preserves another caller's lock");
        two.Dispose(); Check(!locks.Blocks(PlayerActions.Movement | PlayerActions.Ability), "Final lease releases only its own restrictions");
    }
    private static void VerifyBridge(TutorialConfiguration config, SaveManager saves)
    {
        var root = new GameObject("Tutorial isolated bridge verification"); root.SetActive(false);
        var first = new GameObject("Ship A"); first.SetActive(false); first.transform.SetParent(root.transform);
        var second = new GameObject("Ship B"); second.SetActive(false); second.transform.SetParent(root.transform);
        var data = ScriptableObject.CreateInstance<ShipData>();
        data.mass = 1f; data.speed = 5f;
        var view = new FakeView(); config.includeTutorial = true;
        var service = new TutorialService(config, saves, view, new FakeLocalization(), null);
        TutorialBattleBridge bridge = null;
        try
        {
            var body = root.AddComponent<Rigidbody2D>(); var player = root.AddComponent<PlayerController>();
            Set(player, "playerRB", body);
            var a = first.AddComponent<ParentShip>(); var b = second.AddComponent<ParentShip>(); a.ShipData = b.ShipData = data;
            player.ChangeShipData(a);
            var wave = root.AddComponent<WaveManager>();
            bridge = new TutorialBattleBridge(service, player, new EnemyManager(), wave); bridge.Initialize(); bridge.Tick();
            Check(service.Signals.GameplayReady && service.Signals.TwoShipsReady, "Scene bridge observes the actual player and two ships");
            root.transform.position += new Vector3(.4f, .3f, 0); bridge.Tick();
            Check(Mathf.Approximately(service.Signals.Movement, .5f), "Bridge observes real XY displacement");
            player.ChangeShipData(b);
            Check(service.Signals.Switches == 1, "Real PlayerController ship-change event reaches the tutorial");
            bridge.Initialize(); player.ChangeShipData(a);
            Check(service.Signals.Switches == 2, "Repeated bridge initialization does not duplicate subscriptions");
            bridge.Dispose(); bridge.Dispose(); player.ChangeShipData(a);
            Check(service.Signals.Switches == 2 && !service.Signals.GameplayReady, "Scene disposal unsubscribes and clears the gameplay context");
        }
        finally
        {
            bridge?.Dispose(); service.Dispose(); UnityEngine.Object.DestroyImmediate(root); UnityEngine.Object.DestroyImmediate(data);
        }
    }
    private static void Set(object target, string name, object value) => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    private static void VerifyOffBinding(TutorialConfiguration original)
    {
        var config = UnityEngine.Object.Instantiate(original); config.includeTutorial = false;
        try
        {
            var container = new DiContainer(); TutorialFeature.Install(container, config);
            Check(!container.HasBinding<ITutorialService>() && !container.HasBinding<ITutorialPresentation>(), "Include OFF installs neither tutorial service nor UI into Zenject");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(config);
        }
    }
    private static void VerifyLocalizationReferences(TutorialConfiguration config)
    {
        var collection = LocalizationEditorSettings.GetStringTableCollection("UI");
        var table = collection.GetTable("en") as StringTable;
        foreach (var step in config.definition.steps) Check(table.GetEntry(step.hint.TableEntryReference.Key) != null, "Native localized tutorial key: " + step.id);
        Check(LocalizationEditorSettings.GetLocales().Count == 8, "Existing eight locales are preserved");
    }
    private static void VerifyOnBinding(TutorialConfiguration config, SaveManager saves)
    {
        config.includeTutorial = true;
        var container = new DiContainer();
        container.Bind<SaveManager>().FromInstance(saves);
        container.Bind<ILocalizationService>().FromInstance(new FakeLocalization());
        container.Bind<BattleLaunchService>().FromInstance(new BattleLaunchService(null, null));
        TutorialFeature.Install(container, config);
        var service = container.Resolve<ITutorialService>();
        Check(ReferenceEquals(service, container.Resolve<TutorialService>()), "Zenject interface and concrete service share one instance");
        Check(new DiContainer(container).HasBinding<ITutorialService>(), "Fighting child container discovers the optional ProjectContext binding");
        Check(ReferenceEquals(service, container.Resolve<IInitializable>()) && ReferenceEquals(service, container.Resolve<ITickable>()),
            "Zenject initializes and ticks the same tutorial instance");
        ((IDisposable)service).Dispose();
    }
    private static void VerifyMenuConditionAlternative(TutorialConfiguration config, SaveManager saves)
    {
        var original = config.definition;
        bool included = config.includeTutorial; config.includeTutorial = true;
        var definition = UnityEngine.Object.Instantiate(original);
        definition.steps = new List<TutorialStep> { new()
        {
            id = "menu_or_battle", hint = new("UI", "tutorial.welcome"),
            start = new() { match = TutorialConditionMatch.Any, conditions = new()
            {
                new() { type = TutorialConditionType.MainMenuReady }, new() { type = TutorialConditionType.GameplayReady }
            } }
        } };
        config.definition = definition;
        var view = new FakeView(); var service = new TutorialService(config, saves, view, new FakeLocalization(), null);
        try
        {
            service.Signals.MainMenuReady = true;
            service.TryStart(true); service.Tick();
            Check(view.Visible, "ANY start can use the menu alternative without requiring battle context");
        }
        finally { service.Dispose(); config.definition = original; config.includeTutorial = included; UnityEngine.Object.DestroyImmediate(definition); }
    }
    private static void VerifyFirstLaunch(TutorialConfiguration config, string root)
    {
        var previous = config.autoStart; config.autoStart = TutorialAutoStart.MainMenu;
        var saves = new SaveManager(root); var view = new FakeView();
        var service = new TutorialService(config, saves, view, new FakeLocalization(), null);
        try
        {
            service.Initialize(); service.Tick();
            Check(service.IsRunning && view.Visible && service.CurrentStep.id == "welcome", "Configured first launch automatically offers welcome in the real Main Menu scene");
            service.Skip(); service.Dispose();
            service = new TutorialService(config, new SaveManager(root), view, new FakeLocalization(), null); service.Initialize();
            Check(!service.IsRunning, "Skipped automatic tutorial stays stopped after restart");
        }
        finally { service.Dispose(); config.autoStart = previous; }
    }
    private static void VerifySerialization(TutorialDefinition original)
    {
        var copy = ScriptableObject.CreateInstance<TutorialDefinition>();
        try
        {
            JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(original), copy);
            Check(copy.TryValidate(out _) && copy.steps.Count == original.steps.Count && copy.steps[2].completion.conditions[0].type == TutorialConditionType.PlayerMoved,
                "Definition/conditions survive serialization without gameplay references");
        }
        finally { UnityEngine.Object.DestroyImmediate(copy); }
    }
    [Serializable] private sealed class Result { public string[] checks; public string isolatedSaveRoot; }
    private sealed class FakeView : ITutorialPresentation
    {
        public int Shows; public bool Visible;
        public void Show(TutorialStep step, Action confirm, Action skip, Action skipStep) { Shows++; Visible = true; }
        public void Hide() => Visible = false;
        public void Tick(Transform target, Camera camera) { }
        public void Dispose() => Visible = false;
    }
    private sealed class FakeLocalization : ILocalizationService
    {
        public bool IsInitialized => true;
        public Locale SelectedLocale => null;
        public event Action<Locale> LocaleChanged { add { } remove { } }
        public Task InitializeAsync() => Task.CompletedTask;
        public Task SetLocaleAsync(string code) => Task.CompletedTask;
        public Task<string> GetStringAsync(TableReference table, TableEntryReference key, params object[] args) => Task.FromResult(key.Key);
        public LocalizedString CreateString(TableReference table, TableEntryReference key, params object[] args) => new(table, key) { Arguments = args };
    }
}
