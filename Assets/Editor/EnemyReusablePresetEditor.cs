using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(EnemyProjectilePreset))]
[CanEditMultipleObjects]
public sealed class EnemyProjectilePresetEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        EnemyReusablePresetMetadata.Draw(serializedObject);
        var projectile = serializedObject.FindProperty("projectile");
        Rect rect = EditorGUILayout.GetControlRect(false, EnemyShootingSettingsDrawer.CategoryHeight(projectile, true));
        EnemyShootingSettingsDrawer.DrawGroup(ref rect, projectile, true, null, false);
        serializedObject.ApplyModifiedProperties();
        EnemyReusablePresetMetadata.Footer((EnemyReusablePreset)target);
    }
}

[CustomEditor(typeof(EnemyAttackPatternPreset))]
[CanEditMultipleObjects]
public sealed class EnemyAttackPatternPresetEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        EnemyReusablePresetMetadata.Draw(serializedObject);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("firing"), new GUIContent("Fire / Aim Settings"), true);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("suggestedProjectile"), new GUIContent("Optional Projectile Preset", "Applied only with the explicit Apply linked Projectile Preset action."));
        var timing = serializedObject.FindProperty("pattern");
        var shooting = timing.FindPropertyRelative("shooting");
        Rect rect = EditorGUILayout.GetControlRect(false, EnemyShootingSettingsDrawer.CategoryHeight(shooting, false, timing, true));
        EnemyShootingSettingsDrawer.DrawGroup(ref rect, shooting, false, timing, true);
        serializedObject.ApplyModifiedProperties();
        EnemyReusablePresetMetadata.Footer((EnemyReusablePreset)target);
    }
}

[CustomPropertyDrawer(typeof(EnemyPresetFiringSettings))]
public sealed class EnemyPresetFiringSettingsDrawer : PropertyDrawer
{
    private static float Line => EditorGUIUtility.singleLineHeight + 2f;
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        float height = Line;
        if (!property.FindPropertyRelative("enabled").boolValue) return height;
        height += Line;
        var mode = (DirectedWaveAttackFireMode)property.FindPropertyRelative("fireMode").intValue;
        if (mode == DirectedWaveAttackFireMode.Aimed || mode == DirectedWaveAttackFireMode.ForwardWhenPlayerAhead) height += Line;
        if (mode == DirectedWaveAttackFireMode.Stationary) height += EditorGUI.GetPropertyHeight(property.FindPropertyRelative("stationaryFireSettings"), true) + 2f;
        return height;
    }
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);
        position.height = EditorGUIUtility.singleLineHeight;
        EditorGUI.PropertyField(position, property.FindPropertyRelative("enabled"), new GUIContent("Override Fire / Aim Settings")); position.y += Line;
        if (property.FindPropertyRelative("enabled").boolValue)
        {
            EditorGUI.PropertyField(position, property.FindPropertyRelative("fireMode")); position.y += Line;
            var mode = (DirectedWaveAttackFireMode)property.FindPropertyRelative("fireMode").intValue;
            string field = mode == DirectedWaveAttackFireMode.Aimed ? "aimMode"
                : mode == DirectedWaveAttackFireMode.Stationary ? "stationaryFireSettings"
                : mode == DirectedWaveAttackFireMode.ForwardWhenPlayerAhead ? "forwardFireHalfAngle" : null;
            if (field != null)
            {
                var child = property.FindPropertyRelative(field); position.height = EditorGUI.GetPropertyHeight(child, true);
                EditorGUI.PropertyField(position, child, true);
            }
        }
        EditorGUI.EndProperty();
    }
}

internal static class EnemyReusablePresetMetadata
{
    public static void Draw(SerializedObject serialized)
    {
        foreach (string field in new[] { "displayName", "description", "category", "tags" })
            EditorGUILayout.PropertyField(serialized.FindProperty(field), true);
    }
    public static void Footer(EnemyReusablePreset preset)
    {
        if (EnemyPresetDependencies.HasCycle(preset))
            EditorGUILayout.HelpBox("Cyclic preset references: remove the reference before using this asset. Runtime refuses cyclic configurations.", MessageType.Error);
        if (GUILayout.Button("Open Preset Library / Preview")) EnemyPresetLibraryWindow.ShowPreset(preset);
    }
}
