using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(EnemyProjectileScreenSettings))]
public sealed class EnemyProjectileScreenSettingsDrawer : PropertyDrawer
{
    private static float Line => EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        => Line * ((property.FindPropertyRelative("disabled").boolValue ? 1
            : property.FindPropertyRelative("offsetMode").enumValueIndex == (int)EnemyProjectileBoundaryOffsetMode.Custom ? 3 : 2)
            + (property.FindPropertyRelative("bounceOnScreenBounds").boolValue
                ? 3 + (property.FindPropertyRelative("overrideBaseBounceTime").boolValue ? 1 : 0) : 1));
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);
        position.height = EditorGUIUtility.singleLineHeight;
        var disabled = property.FindPropertyRelative("disabled");
        EditorGUI.showMixedValue = disabled.hasMultipleDifferentValues;
        EditorGUI.BeginChangeCheck();
        bool enabled = EditorGUI.Toggle(position, new GUIContent("Destroy Outside Screen", "Removes projectiles after entering and then leaving the gameplay viewport."), !disabled.boolValue);
        if (EditorGUI.EndChangeCheck()) disabled.boolValue = !enabled;
        EditorGUI.showMixedValue = false;
        if (enabled)
        {
            position.y += Line;
            var mode = property.FindPropertyRelative("offsetMode");
            EditorGUI.PropertyField(position, mode, new GUIContent("Boundary Offset Mode"));
            if (mode.enumValueIndex == (int)EnemyProjectileBoundaryOffsetMode.Custom)
            {
                position.y += Line;
                var offset = property.FindPropertyRelative("boundaryOffset");
                EditorGUI.PropertyField(position, offset, new GUIContent("Boundary Offset", "World units outside the gameplay viewport."));
                if (offset.floatValue < 0f) offset.floatValue = 0f;
            }
        }
        position.y += Line;
        var bounce = property.FindPropertyRelative("bounceOnScreenBounds");
        EditorGUI.PropertyField(position, bounce, new GUIContent("Bounce On Screen Bounds", "Projectiles and lasers reflect from selected gameplay edges. Lasers use one shared path for Telegraph, visuals and damage; their length is the total distance along all segments. Use Custom Length or Length Offset to extend beyond the first edge."));
        if (bounce.boolValue)
        {
            position.y += Line;
            var edges = property.FindPropertyRelative("bounceEdges");
            EditorGUI.BeginChangeCheck();
            var selected = (EnemyChildAttackBoundaries)EditorGUI.EnumFlagsField(position, new GUIContent("Bounce Edges"),
                (EnemyChildAttackBoundaries)(edges.intValue ^ (int)EnemyProjectileScreenSettings.DefaultBounceEdges));
            if (EditorGUI.EndChangeCheck()) edges.intValue = (int)((selected & EnemyChildAttackBoundaries.All) ^ EnemyProjectileScreenSettings.DefaultBounceEdges);
            position.y += Line;
            var limit = property.FindPropertyRelative("overrideBaseBounceTime");
            EditorGUI.PropertyField(position, limit, new GUIContent("Override Base Bounce Time", "Limit screen bounces over a projectile's lifetime or along a laser's path. Disabled means unlimited bounces within the laser's total length."));
            if (limit.boolValue)
            {
                position.y += Line;
                var count = property.FindPropertyRelative("extraBounceCount");
                EditorGUI.BeginProperty(position, new GUIContent("Maximum Bounces"), count);
                EditorGUI.showMixedValue = count.hasMultipleDifferentValues;
                EditorGUI.BeginChangeCheck();
                int value = EditorGUI.IntField(position, new GUIContent("Maximum Bounces", "Total bounces over this projectile's lifetime or along the complete laser path. After the limit, the remaining distance continues straight. A simultaneous corner contact counts as one bounce."),
                    count.intValue >= int.MaxValue - 1 ? int.MaxValue : Mathf.Max(0, count.intValue) + 1);
                if (EditorGUI.EndChangeCheck()) count.intValue = Mathf.Max(1, value) - 1;
                EditorGUI.showMixedValue = false;
                EditorGUI.EndProperty();
            }
        }
        EditorGUI.EndProperty();
    }
}
