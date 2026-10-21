using System;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class BuildJoystickFocusPause
{
    private const string PrefabPath = "Assets/Prefub/UI Prefab/ShowTables/Pause.prefab";
    private const string FocusPrefabPath = "Assets/Prefub/UI Prefab/ShowTables/JoystickFocusSettings.prefab";

    public static object Run()
    {
        if (EditorApplication.isPlaying || EditorApplication.isCompiling || EditorApplication.isUpdating)
            throw new InvalidOperationException("Requires ready Edit Mode.");
        var settings = AssetDatabase.LoadAssetAtPath<PlayerJoystickFocusSettings>("Assets/Config/Player/JoystickFocusSettings.asset");
        if (settings == null) throw new InvalidOperationException("Focus settings missing.");
        TMP_FontAsset font = TMP_Settings.defaultFontAsset;
        foreach (var root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
        {
            if (root.name != "MainHUD") continue;
            var texts = root.GetComponentsInChildren<TMP_Text>(true);
            if (texts.Length > 0 && texts[0].font != null) font = texts[0].font;
        }
        if (font == null) throw new InvalidOperationException("TMP font missing.");
        GameObject livePause = null;
        foreach (var root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
            if (root.name == "Pause") livePause = root;
        if (livePause == null) throw new InvalidOperationException("Live Pause menu missing.");
        var prefab = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            if (livePause.transform.Find("JoystickFocusSettings") != null)
                return new { alreadyExists = true, prefab = FocusPrefabPath };
            if (AssetDatabase.LoadAssetAtPath<GameObject>(FocusPrefabPath) != null)
                throw new InvalidOperationException("Focus panel asset already exists; inspect before replacing.");
            var panel = Rect("JoystickFocusSettings", prefab.transform);
            panel.anchorMin = new Vector2(0.08f, 0.5f);
            panel.anchorMax = new Vector2(0.92f, 0.5f);
            panel.sizeDelta = new Vector2(0, 620);
            panel.anchoredPosition = new Vector2(0, -685);
            var background = panel.gameObject.AddComponent<Image>();
            background.color = new Color(0.035f, 0.055f, 0.10f, 0.97f);
            Text(panel, "Title", "Joystick Focus", font, 40, new Vector2(0, -20), new Vector2(0, 55));
            Text(panel, "Description", "Настройки применяются сразу и сохраняются", font, 25, new Vector2(0, -78), new Vector2(0, 36));

            var view = panel.gameObject.AddComponent<JoystickFocusPauseSettings>();
            var serialized = new SerializedObject(view);
            serialized.FindProperty("settings").objectReferenceValue = settings;
            Row(panel, font, serialized, "FocusSpeed", "Скорость Focus", "focusSpeedSlider", "focusSpeedValue", 0, settings.FocusSpeedMultiplier, 1);
            Row(panel, font, serialized, "FocusToNormal", "Порог Focus → Normal", "focusToNormalSlider", "focusToNormalValue", 1, settings.FocusToNormalThreshold, 1);
            Row(panel, font, serialized, "NormalToFocus", "Порог Normal → Focus", "normalToFocusSlider", "normalToFocusValue", 2, settings.NormalToFocusThreshold, 1);
            Row(panel, font, serialized, "DeadZone", "Мёртвая зона", "deadZoneSlider", "deadZoneValue", 3, settings.DeadZone, 0.95f);
            Text(panel, "Hint", "Порог выхода из Focus выше порога возврата", font, 24, new Vector2(0, -578), new Vector2(0, 30));
            serialized.ApplyModifiedPropertiesWithoutUndo();
            bool saved;
            var asset = PrefabUtility.SaveAsPrefabAsset(panel.gameObject, FocusPrefabPath, out saved);
            if (!saved || asset == null) throw new InvalidOperationException("Focus panel prefab was not saved.");
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(asset, livePause.transform);
            Undo.RegisterCreatedObjectUndo(instance, "Add Joystick Focus pause settings");
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(livePause.scene);
            return new { prefab = FocusPrefabPath, sliders = 4, settings = AssetDatabase.GetAssetPath(settings), font = AssetDatabase.GetAssetPath(font), sceneSaved = false };
        }
        finally { PrefabUtility.UnloadPrefabContents(prefab); }
    }

    private static RectTransform Rect(string name, Transform parent)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        return rect;
    }

    private static TMP_Text Text(Transform parent, string name, string content, TMP_FontAsset font, float size, Vector2 position, Vector2 dimensions)
    {
        var rect = Rect(name, parent);
        rect.anchorMin = new Vector2(0.04f, 1);
        rect.anchorMax = new Vector2(0.96f, 1);
        rect.pivot = new Vector2(0.5f, 1);
        rect.anchoredPosition = position;
        rect.sizeDelta = dimensions;
        var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        text.font = font;
        text.fontSize = size;
        text.text = content;
        text.color = Color.white;
        text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false;
        return text;
    }

    private static void Row(RectTransform panel, TMP_FontAsset font, SerializedObject serialized, string name, string title,
        string sliderField, string valueField, int index, float value, float maximum)
    {
        var row = Rect(name, panel);
        row.anchorMin = new Vector2(0.05f, 1);
        row.anchorMax = new Vector2(0.95f, 1);
        row.pivot = new Vector2(0.5f, 1);
        row.anchoredPosition = new Vector2(0, -126 - index * 110);
        row.sizeDelta = new Vector2(0, 94);
        var label = Text(row, "Label", title, font, 30, Vector2.zero, new Vector2(-100, 38));
        label.alignment = TextAlignmentOptions.Left;
        var labelRect = (RectTransform)label.transform;
        labelRect.anchorMin = new Vector2(0, 1);
        labelRect.anchorMax = new Vector2(1, 1);
        labelRect.anchoredPosition = new Vector2(-50, 0);
        var number = Text(row, "Value", value.ToString("F3", System.Globalization.CultureInfo.InvariantCulture), font, 30, Vector2.zero, new Vector2(100, 38));
        var numberRect = (RectTransform)number.transform;
        numberRect.anchorMin = numberRect.anchorMax = new Vector2(1, 1);
        numberRect.pivot = new Vector2(1, 1);
        number.alignment = TextAlignmentOptions.Right;

        var sliderRect = Rect("Slider", row);
        sliderRect.anchorMin = new Vector2(0, 0);
        sliderRect.anchorMax = new Vector2(1, 0);
        sliderRect.sizeDelta = new Vector2(0, 50);
        sliderRect.anchoredPosition = new Vector2(0, 28);
        var hit = sliderRect.gameObject.AddComponent<Image>();
        hit.color = Color.clear;
        var slider = sliderRect.gameObject.AddComponent<Slider>();
        var track = Rect("Background", sliderRect);
        StretchTrack(track, 0);
        var trackImage = track.gameObject.AddComponent<Image>();
        trackImage.color = new Color(0.20f, 0.25f, 0.32f);
        trackImage.raycastTarget = false;
        var fillArea = Rect("FillArea", sliderRect);
        StretchTrack(fillArea, -32);
        var fill = Rect("Fill", fillArea);
        fill.anchorMin = Vector2.zero;
        fill.anchorMax = Vector2.one;
        fill.sizeDelta = Vector2.zero;
        var fillImage = fill.gameObject.AddComponent<Image>();
        fillImage.color = new Color(0.15f, 0.80f, 0.94f);
        fillImage.raycastTarget = false;
        var handleArea = Rect("HandleArea", sliderRect);
        StretchTrack(handleArea, -32);
        var handle = Rect("Handle", handleArea);
        handle.sizeDelta = new Vector2(32, 44);
        var handleImage = handle.gameObject.AddComponent<Image>();
        handleImage.color = Color.white;
        slider.fillRect = fill;
        slider.handleRect = handle;
        slider.targetGraphic = handleImage;
        slider.minValue = 0;
        slider.maxValue = maximum;
        slider.wholeNumbers = false;
        slider.SetValueWithoutNotify(value);
        serialized.FindProperty(sliderField).objectReferenceValue = slider;
        serialized.FindProperty(valueField).objectReferenceValue = number;
    }

    private static void StretchTrack(RectTransform rect, float width)
    {
        rect.anchorMin = new Vector2(0, 0.5f);
        rect.anchorMax = new Vector2(1, 0.5f);
        rect.sizeDelta = new Vector2(width, 14);
    }
}
