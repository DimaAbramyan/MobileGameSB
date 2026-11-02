using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(EnemyRotationSettings), true)]
public sealed class EnemyRotationSettingsDrawer : PropertyDrawer
{
    private static bool UsesLayoutRandom(SerializedProperty property) => property.name == "rotation";
    private static IEnumerable<string> Fields(SerializedProperty property)
    {
        yield return "direction";
        yield return "rotationMode";
        yield return "rotationSpeedDegreesPerSecond";
        yield return "rotationProgressCurve";
        var mode = (FourWayEnemyRotationMode)property.FindPropertyRelative("rotationMode").enumValueIndex;
        if (mode == FourWayEnemyRotationMode.PingPongByAngle)
            yield return "rotationFromAngle";
        if (mode != FourWayEnemyRotationMode.Continuous)
            yield return "rotationAngle";
        yield return "resetRotationOnEnable";
        if (UsesLayoutRandom(property)) yield return "onAttackFinished";
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        bool hasHeading = !UsesLayoutRandom(property);
        float height = hasHeading ? WaveInspectorSections.Line : 0f;
        if (hasHeading && !WaveInspectorSections.Expanded(property, "Settings")) return height;
        foreach (string field in Fields(property))
        {
            var child = property.FindPropertyRelative(field);
            height += (!UsesLayoutRandom(property) && child.propertyType == SerializedPropertyType.Float
                ? EditorGUIUtility.singleLineHeight : EditorGUI.GetPropertyHeight(child, true)) + 2f;
        }
        return height;
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);
        position.height = EditorGUIUtility.singleLineHeight;
        if (!UsesLayoutRandom(property) && !WaveInspectorSections.Header(ref position, property, "Settings", label))
        { EditorGUI.EndProperty(); return; }
        EditorGUI.indentLevel++;
        foreach (string field in Fields(property))
        {
            var child = property.FindPropertyRelative(field);
            position.height = EditorGUI.GetPropertyHeight(child, true);
            if (!UsesLayoutRandom(property) && child.propertyType == SerializedPropertyType.Float) position.height = EditorGUIUtility.singleLineHeight;
            GUIContent fieldLabel = field switch
            {
                "direction" => new GUIContent("Direction", "Clockwise — по часовой стрелке. Counterclockwise — против часовой стрелки."),
                "rotationMode" => new GUIContent("Rotation Mode", "Continuous — постоянное вращение. By Angle — поворот до To Angle. Ping Pong By Angle — повторяющийся проход между From Angle и To Angle."),
                "rotationSpeedDegreesPerSecond" => new GUIContent("Rotation Speed (deg/s)", "Скорость поворота в градусах в секунду; длительность прохода зависит от углового диапазона."),
                "rotationFromAngle" => new GUIContent("From Angle", "Начальный угол каждого прохода Ping Pong относительно исходного layout."),
                "rotationAngle" => new GUIContent("To Angle", "Конечный угол прохода By Angle или Ping Pong относительно исходного layout."),
                "resetRotationOnEnable" => new GUIContent("Reset Rotation On Enable", "Начать вращение с исходного угла при повторном включении."),
                "rotationProgressCurve" => new GUIContent("Rotation Progress Curve", "X: normalized pass time. Y: completed turn progress (0 to 1)."),
                _ => new GUIContent(child.displayName)
            };
            if (!UsesLayoutRandom(property) && child.propertyType == SerializedPropertyType.Float)
            {
                EditorGUI.BeginProperty(position, fieldLabel, child);
                child.floatValue = EditorGUI.FloatField(position, fieldLabel, child.floatValue);
                EditorGUI.EndProperty();
            }
            else EditorGUI.PropertyField(position, child, fieldLabel, true);
            position.y += position.height + 2f;
        }
        EditorGUI.indentLevel--;
        EditorGUI.EndProperty();
    }
}
