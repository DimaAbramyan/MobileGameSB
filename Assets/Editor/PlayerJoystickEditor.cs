using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(PlayerJoystick))]
public sealed class PlayerJoystickEditor : Editor
{
    private Editor settingsEditor;
    private Editor focusEditor;

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        PlayerJoystick joystick = (PlayerJoystick)target;
        if (joystick.Settings == null)
        {
            EditorGUILayout.HelpBox("Назначь PlayerJoystickSettings — радиус, Dead Zone и режим скорости.", MessageType.Warning);
            return;
        }
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Shared Radius / Speed Mode", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Movement Radius и Speed Mode общие для Joystick и Joystick Focus. Analog меняет скорость по отклонению; Constant задаёт полную скорость текущего режима. Focus использует свой Dead Zone и дополнительно умножает скорость на Focus Speed Multiplier.", MessageType.Info);
        CreateCachedEditor(joystick.Settings, null, ref settingsEditor);
        settingsEditor.OnInspectorGUI();
        if (joystick.FocusSettings != null)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Focus Settings (Joystick Focus Only)", EditorStyles.boldLabel);
            CreateCachedEditor(joystick.FocusSettings, null, ref focusEditor);
            focusEditor.OnInspectorGUI();
        }
    }

    private void OnDisable()
    {
        if (settingsEditor != null)
            DestroyImmediate(settingsEditor);
        if (focusEditor != null)
            DestroyImmediate(focusEditor);
    }
}
