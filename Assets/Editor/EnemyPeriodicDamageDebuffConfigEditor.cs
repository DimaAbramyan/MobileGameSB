using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(EnemyPeriodicDamageDebuffConfig)), CanEditMultipleObjects]
public sealed class EnemyPeriodicDamageDebuffConfigEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        DrawPropertiesExcluding(serializedObject, "bypassesEnemyShield");
        var bypass = serializedObject.FindProperty("bypassesEnemyShield");
        EditorGUILayout.Space();
        EditorGUI.showMixedValue = bypass.hasMultipleDifferentValues;
        EditorGUI.BeginChangeCheck();
        bool shieldFirst = EditorGUILayout.Toggle(new GUIContent(
            "Сначала урон щиту",
            "Выключено: урон сразу корпусу. Включено: сначала щиту, затем остаток корпусу, включая Chemical."),
            !bypass.boolValue);
        if (EditorGUI.EndChangeCheck()) bypass.boolValue = !shieldFirst;
        EditorGUI.showMixedValue = false;
        serializedObject.ApplyModifiedProperties();
    }
}
