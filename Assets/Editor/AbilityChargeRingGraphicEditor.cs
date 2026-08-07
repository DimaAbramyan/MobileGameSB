using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(AbilityChargeRingGraphic))]
public class AbilityChargeRingGraphicEditor : Editor
{
    private SerializedProperty segmentCount;
    private SerializedProperty filledSegments;
    private SerializedProperty startAngle;
    private SerializedProperty endAngle;
    private SerializedProperty thickness;
    private SerializedProperty segmentGapDegrees;
    private SerializedProperty outerPadding;
    private SerializedProperty maxDegreesPerQuad;
    private SerializedProperty filledColor;
    private SerializedProperty emptyColor;
    private SerializedProperty drawEmptySegments;
    private SerializedProperty previewSegmentCount;
    private SerializedProperty previewFilledSegments;

    private void OnEnable()
    {
        segmentCount = serializedObject.FindProperty("segmentCount");
        filledSegments = serializedObject.FindProperty("filledSegments");
        startAngle = serializedObject.FindProperty("startAngle");
        endAngle = serializedObject.FindProperty("endAngle");
        thickness = serializedObject.FindProperty("thickness");
        segmentGapDegrees = serializedObject.FindProperty("segmentGapDegrees");
        outerPadding = serializedObject.FindProperty("outerPadding");
        maxDegreesPerQuad = serializedObject.FindProperty("maxDegreesPerQuad");
        filledColor = serializedObject.FindProperty("filledColor");
        emptyColor = serializedObject.FindProperty("emptyColor");
        drawEmptySegments = serializedObject.FindProperty("drawEmptySegments");
        previewSegmentCount = serializedObject.FindProperty("previewSegmentCount");
        previewFilledSegments = serializedObject.FindProperty("previewFilledSegments");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.HelpBox(
            "0° находится сверху. Углы растут по часовой стрелке. Если Start и End совпадают — рисуется полный круг.",
            MessageType.Info);

        DrawRuntimeState();
        DrawArcSettings();
        DrawColorSettings();
        DrawPreviewSettings();

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawRuntimeState()
    {
        EditorGUILayout.LabelField("Runtime State", EditorStyles.boldLabel);
        using (new EditorGUI.DisabledScope(Application.isPlaying))
        {
            EditorGUILayout.PropertyField(segmentCount, new GUIContent("Segments"));
            EditorGUILayout.PropertyField(filledSegments, new GUIContent("Filled Segments"));
        }
    }

    private void DrawArcSettings()
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Ring Shape", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(startAngle, new GUIContent("Start Angle"));
        EditorGUILayout.PropertyField(endAngle, new GUIContent("End Angle"));
        EditorGUILayout.PropertyField(thickness, new GUIContent("Thickness"));
        EditorGUILayout.PropertyField(segmentGapDegrees, new GUIContent("Gap Degrees"));
        EditorGUILayout.PropertyField(outerPadding, new GUIContent("Outer Padding"));
        EditorGUILayout.PropertyField(maxDegreesPerQuad, new GUIContent("Smoothness"));
    }

    private void DrawColorSettings()
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Colors", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(filledColor, new GUIContent("Filled Color"));
        EditorGUILayout.PropertyField(emptyColor, new GUIContent("Empty Color"));
        EditorGUILayout.PropertyField(drawEmptySegments, new GUIContent("Draw Empty Segments"));
    }

    private void DrawPreviewSettings()
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Preview", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(previewSegmentCount, new GUIContent("Preview Segments"));
        EditorGUILayout.Slider(
            previewFilledSegments,
            0f,
            Mathf.Max(1, previewSegmentCount.intValue),
            new GUIContent("Preview Filled"));

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Empty"))
            ApplyPreview(0f);
        if (GUILayout.Button("Half"))
            ApplyPreview(Mathf.Max(1, previewSegmentCount.intValue) * 0.5f);
        if (GUILayout.Button("Full"))
            ApplyPreview(Mathf.Max(1, previewSegmentCount.intValue));
        EditorGUILayout.EndHorizontal();

        if (GUILayout.Button("Apply Preview"))
            ApplyPreview(previewFilledSegments.floatValue);
    }

    private void ApplyPreview(float filled)
    {
        serializedObject.ApplyModifiedProperties();

        AbilityChargeRingGraphic ring = (AbilityChargeRingGraphic)target;
        Undo.RecordObject(ring, "Preview Ability Charge Ring");
        ring.SetPreviewState(previewSegmentCount.intValue, filled);
        EditorUtility.SetDirty(ring);

        serializedObject.Update();
    }
}
