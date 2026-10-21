using System;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public static class ConfigureJoystickFocus
{
    public static object Run()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (scene.path != "Assets/Scenes/Fighting/Fighting.unity" || EditorApplication.isPlaying
            || EditorApplication.isCompiling || EditorApplication.isUpdating)
            throw new InvalidOperationException("Open Fighting in Edit Mode after compilation.");
        var hud = GameObject.Find("MainHUD");
        var bottom = hud.transform.Find("BottomHUD");
        var joystick = bottom.Find("JoyStick").GetComponent<PlayerJoystick>();
        var player = GameObject.Find("MainHero").GetComponent<PlayerController>();
        var swap = bottom.Find("ShipSwap");
        if (joystick == null || player == null || swap == null)
            throw new InvalidOperationException("Expected JoyStick, MainHero and ShipSwap.");

        const string path = "Assets/Config/Player/JoystickFocusSettings.asset";
        var settings = AssetDatabase.LoadAssetAtPath<PlayerJoystickFocusSettings>(path);
        if (settings == null)
        {
            settings = ScriptableObject.CreateInstance<PlayerJoystickFocusSettings>();
            AssetDatabase.CreateAsset(settings, path);
        }
        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName("Configure Joystick Focus and ShipSwap");
        Button button = swap.GetComponent<Button>();
        if (button == null)
            button = Undo.AddComponent<Button>(swap.gameObject);
        // ShipSwap was copied from ActiveAbility. Keep the user's artwork and
        // placement, but stop its copied ability listeners and cooldown view.
        foreach (var c in swap.GetComponents<MonoBehaviour>())
        {
            if (c is AbilityController || c is AbilityButtonView)
            {
                Undo.RecordObject(c, "Disable copied ability handling on ShipSwap");
                c.enabled = false;
            }
        }
        Undo.RecordObject(button, "Configure ShipSwap click");
        int removedAbilityCalls = 0;
        for (int i = button.onClick.GetPersistentEventCount() - 1; i >= 0; i--)
        {
            var target = button.onClick.GetPersistentTarget(i);
            string method = button.onClick.GetPersistentMethodName(i);
            if (target is AbilityController
                || (target is ShipSelect && method == nameof(ShipSelect.SwitchShip))
                || (target == player && method == nameof(PlayerController.SwitchShipFromButton)))
            {
                UnityEventTools.RemovePersistentListener(button.onClick, i);
                removedAbilityCalls++;
            }
        }
        button.interactable = true;
        if (button.targetGraphic == null)
            button.targetGraphic = swap.GetComponent<Image>();

        var label = swap.Find("SwitchLabel");
        if (label == null)
        {
            var labelObject = new GameObject("SwitchLabel", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            Undo.RegisterCreatedObjectUndo(labelObject, "Label ShipSwap");
            var rect = (RectTransform)labelObject.transform;
            rect.SetParent(swap, false);
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, 0.3f);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            var text = labelObject.GetComponent<TextMeshProUGUI>();
            var sample = hud.GetComponentInChildren<TMP_Text>(true);
            if (sample != null)
                text.font = sample.font;
            text.text = "Смена";
            text.fontSize = 30f;
            text.color = Color.white;
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;
        }
        var joystickData = new SerializedObject(joystick);
        joystickData.FindProperty("focusSettings").objectReferenceValue = settings;
        joystickData.ApplyModifiedProperties();
        var playerData = new SerializedObject(player);
        playerData.FindProperty("movementMode").enumValueIndex = (int)PlayerMovementMode.JoystickFocus;
        playerData.FindProperty("switchShipOnFocusRelease").boolValue = false;
        playerData.FindProperty("shipSwitchButton").objectReferenceValue = button;
        playerData.ApplyModifiedProperties();
        EditorSceneManager.MarkSceneDirty(scene);
        AssetDatabase.SaveAssetIfDirty(settings);
        return new { mode = player.MovementMode.ToString(), settings = path, button = swap.name,
            removedAbilityCalls, savedScene = false, maxSpeed = playerData.FindProperty("maxSpeed").floatValue };
    }
}
