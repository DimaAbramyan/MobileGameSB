using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(PlayerController)), CanEditMultipleObjects]
public sealed class PlayerControllerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        using (new EditorGUI.DisabledScope(true))
            EditorGUILayout.PropertyField(serializedObject.FindProperty("m_Script"));
        Draw("playerRB");
        Draw("gameplayCamera");
        Draw("movementMode");
        SerializedProperty mode = serializedObject.FindProperty("movementMode");
        if (mode.hasMultipleDifferentValues || mode.enumValueIndex == (int)PlayerMovementMode.Joystick
            || mode.enumValueIndex == (int)PlayerMovementMode.JoystickFocus)
        {
            Draw("joystick");
            Draw("overrideMaxSpeed");
            SerializedProperty speedOverride = serializedObject.FindProperty("overrideMaxSpeed");
            if (speedOverride.hasMultipleDifferentValues || speedOverride.boolValue)
                Draw("maxSpeed");
            if (speedOverride.hasMultipleDifferentValues || !speedOverride.boolValue)
                Draw("joystickSpeedMultiplier");
            if (mode.hasMultipleDifferentValues || mode.enumValueIndex == (int)PlayerMovementMode.JoystickFocus)
            {
                Draw("switchShipOnFocusRelease", "Switch Ship On Repeat Press");
                SerializedProperty releaseSwitch = serializedObject.FindProperty("switchShipOnFocusRelease");
                if (releaseSwitch.hasMultipleDifferentValues || !releaseSwitch.boolValue)
                    Draw("shipSwitchButton");
                EditorGUILayout.HelpBox("Пороги Focus/Normal проверяют исходное отклонение ручки. Speed Mode в Settings на PlayerJoystick: Constant — фиксированная скорость режима; Analog — скорость по отклонению. Пороги, множитель и Dead Zone — в Focus Settings. Max Speed задаёт максимальную скорость Normal.", MessageType.Info);
            }
            else
                EditorGUILayout.HelpBox("Корабль переключается при повторном нажатии рядом с последней позицией отпущенного пальца. Отпускание само по себе не переключает. Dead Zone и радиус находятся в Settings на компоненте PlayerJoystick.", MessageType.Info);
        }
        if (mode.hasMultipleDifferentValues || mode.enumValueIndex == (int)PlayerMovementMode.DirectTouch)
            Draw("directTouchSensitivity");
        if (mode.hasMultipleDifferentValues || mode.enumValueIndex == (int)PlayerMovementMode.PhysicsTouch)
            Draw("touchOffsetY");
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Ship Switching", EditorStyles.boldLabel);
        Draw("shipSwitchPressWindow", "Repeat Press Window (s)");
        Draw("shipSwitchTouchDistancePercent", "Maximum Touch Delta (%)");
        EditorGUILayout.HelpBox("Интервал считается от отпускания до следующего нажатия; расстояние — от последней позиции пальца. Дельта задана в % короткой стороны экрана. Остальной HUD участвует в жесте, кнопка способности — нет.", MessageType.None);
        if (mode.hasMultipleDifferentValues || mode.enumValueIndex != (int)PlayerMovementMode.PhysicsTouch)
        {
            Draw("maximumKnockbackSpeed");
            Draw("knockbackDeceleration");
        }
        Draw("effectController");
        Draw("teamBarrierController");
        serializedObject.ApplyModifiedProperties();
    }

    private void Draw(string name) => EditorGUILayout.PropertyField(serializedObject.FindProperty(name));
    private void Draw(string name, string label)
    {
        SerializedProperty property = serializedObject.FindProperty(name);
        EditorGUILayout.PropertyField(property, new GUIContent(label, property.tooltip));
    }
}
