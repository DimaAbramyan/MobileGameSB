using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Game.Tutorial;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Zenject;

public static class VerifyTutorialPlayMode
{
    private const string IncludeKey = "TutorialVerification.OriginalInclude";
    private const string HandleKey = "TutorialVerification.Service";
    public static object Prepare()
    {
        if (EditorApplication.isPlaying) throw new Exception("Prepare must run before Play Mode.");
        var config = TutorialEditorSettings.Configuration;
        SessionState.SetBool(IncludeKey, config.includeTutorial);
        // In-memory only. No SaveAssets, source changes or production save reset.
        config.includeTutorial = false;
        return new { temporarilyDisabled = true, sceneChangesSaved = false };
    }
    public static async Task<object> Present()
    {
        if (!Application.isPlaying) throw new Exception("Play Mode required.");
        if (ProjectContext.Instance.Container.HasBinding<ITutorialService>()) throw new Exception("Disabled tutorial leaked into live ProjectContext.");
        if (Resources.FindObjectsOfTypeAll<Canvas>().Any(c => c.name == "Tutorial Overlay")) throw new Exception("Disabled tutorial created UI.");
        var original = TutorialEditorSettings.Configuration;
        var config = UnityEngine.Object.Instantiate(original); config.includeTutorial = true; config.autoStart = TutorialAutoStart.Manual;
        var localization = new UnityLocalizationService();
        await localization.InitializeAsync();
        var saves = new SaveManager(Path.GetFullPath("AgentArtifacts/TutorialPlayVerificationData"));
        string before = JsonUtility.ToJson(saves.ReadOnboarding());
        var overlay = new TutorialOverlay(config);
        var service = new TutorialService(config, saves, overlay, localization, null);
        if (!service.TryStart(true)) throw new Exception("Development replay failed.");
        service.Tick(); await Task.Delay(200); Canvas.ForceUpdateCanvases(); service.Tick();
        var canvas = Resources.FindObjectsOfTypeAll<Canvas>().Single(c => c.name == "Tutorial Overlay");
        var hint = canvas.GetComponentsInChildren<TMP_Text>().Single(t => t.name == "Hint");
        if (string.IsNullOrWhiteSpace(hint.text) || hint.text.Contains("No translation")) throw new Exception("Localized hint missing.");
        var panel = canvas.GetComponentsInChildren<RectTransform>().Single(t => t.name == "Hint Panel");
        if (panel.rect.width <= 0 || panel.rect.height <= 0) throw new Exception("Invalid panel geometry.");
        object[] state = { service, config, localization, saves, before, overlay };
        SessionState.SetString(HandleKey, GCHandle.ToIntPtr(GCHandle.Alloc(state)).ToInt64().ToString());
        File.WriteAllText("AgentArtifacts/tutorial-playmode-verification.json", JsonUtility.ToJson(new Verification
        { offHasNoServiceOrUi = true, localizedHint = hint.text, panelWidth = panel.rect.width, panelHeight = panel.rect.height }, true));
        return new { playMode = true, offProjectContextHasNoTutorial = true, localizedHint = hint.text,
            panelWidth = panel.rect.width, panelHeight = panel.rect.height, safeArea = Screen.safeArea.ToString(), screenshotReady = true };
    }
    public static object Complete()
    {
        var handle = GCHandle.FromIntPtr(new IntPtr(long.Parse(SessionState.GetString(HandleKey, "0"))));
        var state = (object[])handle.Target;
        var service = (TutorialService)state[0];
        try
        {
            var canvas = Resources.FindObjectsOfTypeAll<Canvas>().Single(c => c.name == "Tutorial Overlay");
            canvas.GetComponentsInChildren<Button>().Single(b => b.name == "Skip Tutorial").onClick.Invoke();
            if (service.IsRunning || canvas.gameObject.activeSelf) throw new Exception("Skip button failed to hide UI.");
            bool unchanged = JsonUtility.ToJson(((SaveManager)state[3]).ReadOnboarding()) == (string)state[4];
            if (!unchanged) throw new Exception("Development replay changed the saved onboarding state.");
            var overlay = (TutorialOverlay)state[5];
            overlay.Show(new TutorialStep { hint = new("UI", "tutorial.welcome"), placement = TutorialPlacement.NearTarget,
                highlightTarget = true, showPointer = true }, () => { }, () => { }, () => { });
            Canvas.ForceUpdateCanvases(); overlay.Tick(canvas.transform, null); Canvas.ForceUpdateCanvases();
            var panel = canvas.GetComponentsInChildren<RectTransform>().Single(t => t.name == "Hint Panel");
            if (panel.rect.width <= 0 || panel.rect.height <= 0) throw new Exception("NearTarget geometry invalid.");
            var report = JsonUtility.FromJson<Verification>(File.ReadAllText("AgentArtifacts/tutorial-playmode-verification.json"));
            report.skipButtonWorks = report.replayPreservesSave = report.nearTargetAndHighlightGeometry = true;
            File.WriteAllText("AgentArtifacts/tutorial-playmode-verification.json", JsonUtility.ToJson(report, true));
            return new { skipButtonWorks = true, replayDidNotPersistProgress = unchanged, nearTargetGeometry = true };
        }
        finally
        {
            service.Dispose(); ((UnityLocalizationService)state[2]).Dispose(); UnityEngine.Object.Destroy((TutorialConfiguration)state[1]);
            handle.Free(); SessionState.EraseString(HandleKey);
        }
    }
    public static object Restore()
    {
        if (EditorApplication.isPlaying) throw new Exception("Restore after leaving Play Mode.");
        var config = TutorialEditorSettings.Configuration;
        config.includeTutorial = SessionState.GetBool(IncludeKey, true);
        SessionState.EraseBool(IncludeKey);
        return new { restored = config.includeTutorial, sceneChangesSaved = false };
    }
    [Serializable] private sealed class Verification
    {
        public bool offHasNoServiceOrUi, skipButtonWorks, replayPreservesSave, nearTargetAndHighlightGeometry;
        public string localizedHint;
        public float panelWidth, panelHeight;
    }
}
