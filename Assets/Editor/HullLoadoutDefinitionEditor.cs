using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(HullLoadoutDefinition))]
public sealed class HullLoadoutDefinitionEditor : Editor
{
    private readonly List<string> validationIssues = new();

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        DrawDefaultInspector();
        serializedObject.ApplyModifiedProperties();

        HullLoadoutDefinition definition = (HullLoadoutDefinition)target;
        definition.CollectValidationIssues(validationIssues);
        if (validationIssues.Count == 0)
            return;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Configuration Warnings", EditorStyles.boldLabel);

        for (int i = 0; i < validationIssues.Count; i++)
            EditorGUILayout.HelpBox(validationIssues[i], MessageType.Warning);
    }
}
