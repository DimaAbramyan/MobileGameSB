using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(EnemyEmissionOriginSettings))]
public sealed class EnemyEmissionOriginSettingsDrawer : PropertyDrawer
{
    static float Line => EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
    static int AxisLines(SerializedProperty axis)
        => 3 + (axis.FindPropertyRelative("offsetRandom.enabled").boolValue ? 1 : 0)
        + (axis.FindPropertyRelative("source").intValue == (int)EnemyEmissionOriginSource.World
            ? 1 + (axis.FindPropertyRelative("referenceMode").intValue == (int)EnemyEmissionOriginReferenceMode.Boundary ? 1 : 0)
            : axis.FindPropertyRelative("source").intValue == (int)EnemyEmissionOriginSource.Player ? 1 : 0);
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        => Line * (1 + AxisLines(property.FindPropertyRelative("x")) + AxisLines(property.FindPropertyRelative("y")));
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);
        Rect row = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
        EditorGUI.LabelField(row, label, EditorStyles.boldLabel); row.y += Line;
        DrawAxis(ref row, property.FindPropertyRelative("x"), true);
        DrawAxis(ref row, property.FindPropertyRelative("y"), false);
        EditorGUI.EndProperty();
    }
    static void DrawAxis(ref Rect row, SerializedProperty axis, bool horizontal)
    {
        EditorGUI.LabelField(row, horizontal ? "X Axis" : "Y Axis", EditorStyles.miniBoldLabel); row.y += Line;
        var source = axis.FindPropertyRelative("source");
        EditorGUI.PropertyField(row, source, new GUIContent("Source")); row.y += Line;
        if (source.intValue == (int)EnemyEmissionOriginSource.Player)
        {
            EditorGUI.PropertyField(row, axis.FindPropertyRelative("captureMode"), new GUIContent("Capture Mode",
                "Capture this Player coordinate at attack start, burst start or before each volley. X and Y are independent; Sequential Sector keeps one origin throughout a volley.")); row.y += Line;
        }
        if (source.intValue == (int)EnemyEmissionOriginSource.World)
        {
            var reference = axis.FindPropertyRelative("referenceMode");
            EditorGUI.PropertyField(row, reference, new GUIContent("Reference Mode")); row.y += Line;
            if (reference.intValue == (int)EnemyEmissionOriginReferenceMode.Boundary)
            {
                var boundary = axis.FindPropertyRelative("boundary");
                if (horizontal) boundary.intValue = (int)(EnemyEmissionOriginXBoundary)EditorGUI.EnumPopup(row, "Boundary", (EnemyEmissionOriginXBoundary)boundary.intValue);
                else boundary.intValue = (int)(EnemyEmissionOriginYBoundary)EditorGUI.EnumPopup(row, "Boundary", (EnemyEmissionOriginYBoundary)boundary.intValue);
                row.y += Line;
            }
        }
        EditorGUI.PropertyField(row, axis.FindPropertyRelative("offset"), new GUIContent("Offset",
            "Enemy / Player: world-axis offset. World Absolute: world coordinate. Boundary: positive inward, negative outward.")); row.y += Line;
        if (axis.FindPropertyRelative("offsetRandom.enabled").boolValue)
        { EditorGUI.PropertyField(row, axis.FindPropertyRelative("randomizeOn"), new GUIContent("Randomize On")); row.y += Line; }
    }
}
