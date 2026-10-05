using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(EnemyRotationSettings), true)]
public sealed class EnemyRotationSettingsDrawer : PropertyDrawer
{
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
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        float height = EditorGUIUtility.singleLineHeight + 2f;
        foreach (string field in Fields(property))
            height += EditorGUI.GetPropertyHeight(property.FindPropertyRelative(field), true) + 2f;
        return height;
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);
        position.height = EditorGUIUtility.singleLineHeight;
        EditorGUI.LabelField(position, label, EditorStyles.boldLabel);
        EditorGUI.indentLevel++;
        foreach (string field in Fields(property))
        {
            position.y += position.height + 2f;
            var child = property.FindPropertyRelative(field);
            position.height = EditorGUI.GetPropertyHeight(child, true);
            GUIContent fieldLabel = field switch
            {
                "rotationSpeedDegreesPerSecond" => new GUIContent("Rotation Speed (deg/s)"),
                "rotationFromAngle" => new GUIContent("From Angle"),
                "rotationAngle" => new GUIContent("To Angle"),
                "rotationProgressCurve" => new GUIContent("Rotation Progress Curve", "X: normalized pass time. Y: completed turn progress (0 to 1)."),
                _ => new GUIContent(child.displayName)
            };
            EditorGUI.PropertyField(position, child, fieldLabel, true);
        }
        EditorGUI.indentLevel--;
        EditorGUI.EndProperty();
    }
}
