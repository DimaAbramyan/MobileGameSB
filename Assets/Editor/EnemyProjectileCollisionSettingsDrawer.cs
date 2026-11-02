using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(EnemyProjectileCollisionSettings))]
public sealed class EnemyProjectileCollisionSettingsDrawer : PropertyDrawer
{
    private static float Line => EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        => Line * (property.FindPropertyRelative("disabled").boolValue ? 1
            : property.FindPropertyRelative("disableCondition").intValue == (int)EnemyProjectileCollisionDisableCondition.ScaleBelowThreshold ? 3 : 2);

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);
        position.height = EditorGUIUtility.singleLineHeight;
        var disabled = property.FindPropertyRelative("disabled");
        EditorGUI.showMixedValue = disabled.hasMultipleDifferentValues;
        EditorGUI.BeginChangeCheck();
        bool enabled = EditorGUI.Toggle(position, new GUIContent("Collision Enabled", "Contact damage and interception. Does not affect rendering, movement or lifetime."), !disabled.boolValue);
        if (EditorGUI.EndChangeCheck()) disabled.boolValue = !enabled;
        EditorGUI.showMixedValue = false;
        if (enabled)
        {
            position.y += Line;
            var condition = property.FindPropertyRelative("disableCondition");
            EditorGUI.PropertyField(position, condition, new GUIContent("Disable Condition"));
            if (condition.intValue == (int)EnemyProjectileCollisionDisableCondition.ScaleBelowThreshold)
            {
                position.y += Line;
                var threshold = property.FindPropertyRelative("scaleThresholdDelta");
                EditorGUI.BeginProperty(position, GUIContent.none, threshold);
                EditorGUI.showMixedValue = threshold.hasMultipleDifferentValues;
                EditorGUI.BeginChangeCheck();
                float value = EditorGUI.FloatField(position, new GUIContent("Scale Threshold", "Uniform size multiplier including Base Size and Size Curve. Disabling is irreversible for this projectile."), .5f + threshold.floatValue);
                if (EditorGUI.EndChangeCheck()) threshold.floatValue = (float.IsFinite(value) ? Mathf.Max(0f, value) : .5f) - .5f;
                EditorGUI.showMixedValue = false;
                EditorGUI.EndProperty();
            }
        }
        EditorGUI.EndProperty();
    }
}
