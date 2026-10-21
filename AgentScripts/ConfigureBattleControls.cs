using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class ConfigureBattleControls
{
    public static object Run()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (scene.path != "Assets/Scenes/Fighting/Fighting.unity"
            || EditorApplication.isPlaying || EditorApplication.isCompiling || EditorApplication.isUpdating)
            throw new InvalidOperationException("Open Fighting in Edit Mode and finish compilation first.");

        GameObject hud = GameObject.Find("MainHUD");
        RectTransform bottom = hud.transform.Find("BottomHUD") as RectTransform;
        RectTransform surface = bottom.Find("JoyStick") as RectTransform;
        PlayerController player = GameObject.Find("MainHero").GetComponent<PlayerController>();
        BattleCameraViewport viewport = GameObject.Find("LevelConfig").GetComponent<BattleCameraViewport>();
        Camera camera = GameObject.Find("Fighting Camera").GetComponent<Camera>();
        var cinematic = GameObject.Find("CinemachineCamera").GetComponent<Unity.Cinemachine.CinemachineCamera>();
        if (bottom == null || surface == null || player == null || viewport == null || camera == null || cinematic == null)
            throw new InvalidOperationException("Required battle objects are missing.");

        const string settingsPath = "Assets/Config/Player/JoystickSettings.asset";
        PlayerJoystickSettings settings = AssetDatabase.LoadAssetAtPath<PlayerJoystickSettings>(settingsPath);
        if (settings == null)
        {
            if (!AssetDatabase.IsValidFolder("Assets/Config/Player"))
                AssetDatabase.CreateFolder("Assets/Config", "Player");
            settings = ScriptableObject.CreateInstance<PlayerJoystickSettings>();
            AssetDatabase.CreateAsset(settings, settingsPath);
        }

        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName("Configure battle joystick and bottom HUD viewport");
        PlayerJoystick joystick = surface.GetComponent<PlayerJoystick>();
        if (joystick == null)
            joystick = Undo.AddComponent<PlayerJoystick>(surface.gameObject);
        RectTransform handle = surface.Find("Handle") as RectTransform;
        if (handle == null)
        {
            GameObject knob = new GameObject("Handle", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            Undo.RegisterCreatedObjectUndo(knob, "Create joystick handle");
            handle = (RectTransform)knob.transform;
            handle.SetParent(surface, false);
            handle.anchorMin = handle.anchorMax = new Vector2(0.5f, 0.5f);
            handle.anchoredPosition = Vector2.zero;
            handle.sizeDelta = new Vector2(75f, 75f);
            Image image = knob.GetComponent<Image>();
            image.sprite = surface.GetComponent<Image>().sprite;
            image.color = new Color(0.9f, 0.9f, 1f, 1f);
            image.raycastTarget = false;
            image.preserveAspect = true;
        }
        var joystickData = new SerializedObject(joystick);
        joystickData.FindProperty("settings").objectReferenceValue = settings;
        joystickData.FindProperty("handle").objectReferenceValue = handle;
        joystickData.ApplyModifiedProperties();

        var playerData = new SerializedObject(player);
        playerData.FindProperty("movementMode").enumValueIndex = (int)PlayerMovementMode.Joystick;
        playerData.FindProperty("movementSerializationVersion").intValue = 1;
        playerData.FindProperty("joystick").objectReferenceValue = joystick;
        playerData.FindProperty("gameplayCamera").objectReferenceValue = camera;
        playerData.ApplyModifiedProperties();

        var viewData = new SerializedObject(viewport);
        viewData.FindProperty("topHudBoundary").objectReferenceValue = null;
        viewData.FindProperty("bottomHudBoundary").objectReferenceValue = bottom;
        viewData.FindProperty("respectSafeArea").boolValue = true;
        viewData.FindProperty("targetCamera").objectReferenceValue = camera;
        viewData.FindProperty("cinemachineCamera").objectReferenceValue = cinematic;
        viewData.ApplyModifiedProperties();
        CameraConstantWidth width = viewport.GetComponent<CameraConstantWidth>();
        if (width != null)
        {
            Undo.RecordObject(width, "Use battle Cinemachine lens as size source");
            width.enabled = false;
        }
        Canvas.ForceUpdateCanvases();
        Undo.RecordObject(camera, "Update battle viewport");
        Undo.RecordObject(cinematic, "Update battle lens");
        viewport.Apply();
        EditorSceneManager.MarkSceneDirty(scene);
        AssetDatabase.SaveAssetIfDirty(settings);
        Rect rect = viewport.GameplayScreenRect;
        return new { settings = settingsPath, mode = player.MovementMode.ToString(),
            handle = handle.name, viewport = new[] { rect.x, rect.y, rect.width, rect.height },
            sceneSaved = false };
    }
}
