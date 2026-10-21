using System;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class VerifyJoystickFocusPause
{
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
    private static int checks;
    public static object Run()
    {
        checks = 0;
        var source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefub/UI Prefab/ShowTables/JoystickFocusSettings.prefab");
        if (source == null) throw new Exception("Focus panel not persisted.");
        var preview = EditorSceneManager.NewPreviewScene();
        var settings = ScriptableObject.CreateInstance<PlayerJoystickFocusSettings>();
        var reloaded = ScriptableObject.CreateInstance<PlayerJoystickFocusSettings>();
        var keys = new[] {"Speed", "Exit", "Return", "DeadZone"};
        var existed = new bool[4];
        var values = new float[4];
        for (int i = 0; i < 4; i++)
        {
            string key = "Fighting.JoystickFocus." + keys[i];
            existed[i] = PlayerPrefs.HasKey(key);
            values[i] = PlayerPrefs.GetFloat(key);
        }
        float timeScale = Time.timeScale;
        try
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(source, preview);
            var view = instance.GetComponent<JoystickFocusPauseSettings>();
            var serialized = new SerializedObject(view);
            serialized.FindProperty("settings").objectReferenceValue = settings;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            var sliders = instance.GetComponentsInChildren<Slider>(true);
            Check(sliders.Length == 4, "exactly four sliders");
            foreach (var slider in sliders)
                Check(slider.handleRect != null && slider.fillRect != null && slider.targetGraphic != null, "slider graphics wired");
            foreach (var text in instance.GetComponentsInChildren<TMP_Text>(true))
                Check(!text.raycastTarget, "text does not block slider pointer");
            Time.timeScale = 0;
            Invoke(view, "OnEnable");
            Slider speed = Field<Slider>(view, "focusSpeedSlider");
            Slider exit = Field<Slider>(view, "focusToNormalSlider");
            Slider back = Field<Slider>(view, "normalToFocusSlider");
            Slider dead = Field<Slider>(view, "deadZoneSlider");
            Near(speed.value, 0.4f, "initial multiplier");
            Near(exit.value, 0.6f, "initial exit threshold");
            Near(back.value, 0.4f, "initial return threshold");
            Near(dead.value, 0.1f, "initial dead zone");
            speed.value = 0.25f;
            Near(settings.FocusSpeedMultiplier, 0.25f, "speed applies during pause");
            bool focused = true;
            Near(settings.EvaluateInput(Vector2.right * 0.2f, ref focused).magnitude, 0.25f, "runtime reads same setting");
            dead.value = 0.3f;
            Near(settings.EvaluateInput(Vector2.right * 0.2f, ref focused).magnitude, 0f, "dead zone applies immediately");
            back.value = 1f;
            Check(settings.NormalToFocusThreshold < settings.FocusToNormalThreshold, "return cannot cross exit");
            Near(back.value, settings.NormalToFocusThreshold, "rejected return snaps UI to validated value");
            exit.value = 0;
            Check(settings.FocusToNormalThreshold > settings.NormalToFocusThreshold, "exit cannot cross return");
            Near(exit.value, settings.FocusToNormalThreshold, "rejected exit snaps UI");
            back.value = 0.2f;
            exit.value = 0.8f;
            Near(settings.NormalToFocusThreshold, 0.2f, "independent return");
            Near(settings.FocusToNormalThreshold, 0.8f, "independent exit");
            dead.value = 1;
            Near(settings.DeadZone, 0.95f, "dead zone maximum");
            Check(Field<TMP_Text>(view, "focusSpeedValue").text.Contains("0.250"), "numeric label updates");
            settings.SavePlayerPreferences();
            reloaded.LoadPlayerPreferences();
            Near(reloaded.FocusSpeedMultiplier, settings.FocusSpeedMultiplier, "saved speed reloads");
            Near(reloaded.FocusToNormalThreshold, settings.FocusToNormalThreshold, "saved exit reloads");
            Near(reloaded.NormalToFocusThreshold, settings.NormalToFocusThreshold, "saved return reloads");
            Near(reloaded.DeadZone, settings.DeadZone, "saved dead zone reloads");
            Invoke(view, "OnDisable");
            speed.value = 0.1f;
            Near(settings.FocusSpeedMultiplier, 0.25f, "disabled view unsubscribes");
            Invoke(view, "OnEnable");
            Near(speed.value, 0.25f, "reopen refreshes values");
            speed.value = 0.5f;
            Near(settings.FocusSpeedMultiplier, 0.5f, "reopen rebinds");
            Invoke(view, "OnDisable");

            GameObject livePause = null;
            PlayerJoystick joystick = null;
            foreach (var root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (root.name == "Pause") livePause = root;
                var j = root.GetComponentInChildren<PlayerJoystick>(true);
                if (j != null) joystick = j;
            }
            Check(livePause != null && joystick != null, "live pause and joystick exist");
            var livePanel = livePause.transform.Find("JoystickFocusSettings");
            Check(livePanel != null, "panel installed in live pause");
            var liveView = livePanel.GetComponent<JoystickFocusPauseSettings>();
            Check(Field<PlayerJoystickFocusSettings>(liveView, "settings") == joystick.FocusSettings, "UI and joystick share config");
            Check(PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(livePanel.gameObject) == AssetDatabase.GetAssetPath(source), "live panel linked to persisted prefab");
            return new { passed = checks, preferencesRestored = true, isolatedPreviewScene = true };
        }
        finally
        {
            Time.timeScale = timeScale;
            EditorSceneManager.ClosePreviewScene(preview);
            UnityEngine.Object.DestroyImmediate(settings);
            UnityEngine.Object.DestroyImmediate(reloaded);
            for (int i = 0; i < 4; i++)
            {
                string key = "Fighting.JoystickFocus." + keys[i];
                if (existed[i]) PlayerPrefs.SetFloat(key, values[i]);
                else PlayerPrefs.DeleteKey(key);
            }
            PlayerPrefs.Save();
        }
    }
    private static T Field<T>(object target, string name) => (T)target.GetType().GetField(name, Flags).GetValue(target);
    private static void Invoke(object target, string name) => target.GetType().GetMethod(name, Flags).Invoke(target, null);
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
        checks++;
    }
    private static void Near(float actual, float expected, string message) => Check(Mathf.Abs(actual - expected) < 0.0001f, message + ": " + actual);
}
