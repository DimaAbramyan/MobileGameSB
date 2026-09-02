using UnityEditor;

[CustomEditor(typeof(BossController))]
public sealed class BossControllerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        DrawDefaultInspector();
        DrawPhaseWarnings();
        serializedObject.ApplyModifiedProperties();
    }

    private void DrawPhaseWarnings()
    {
        SerializedProperty phases = serializedObject.FindProperty("phases");
        if (phases == null || phases.arraySize == 0)
        {
            EditorGUILayout.HelpBox(
                "Add at least one boss phase.",
                MessageType.Warning);
            return;
        }

        float previousThreshold = 100f;
        for (int i = 0; i < phases.arraySize - 1; i++)
        {
            SerializedProperty phase = phases.GetArrayElementAtIndex(i);
            SerializedProperty threshold = phase.FindPropertyRelative(
                "nextPhaseHealthThresholdPercent");
            if (threshold == null)
                continue;

            if (threshold.floatValue <= 0f)
            {
                EditorGUILayout.HelpBox(
                    $"Phase {i + 1}: a zero threshold cannot transition before the boss dies.",
                    MessageType.Warning);
            }

            if (threshold.floatValue >= previousThreshold)
            {
                EditorGUILayout.HelpBox(
                    $"Phase {i + 1}: threshold must be lower than the previous phase threshold. Otherwise a phase can be skipped.",
                    MessageType.Warning);
            }

            previousThreshold = threshold.floatValue;
        }
    }
}
