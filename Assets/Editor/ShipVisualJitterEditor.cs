using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(ShipVisualJitter))]
public class ShipVisualJitterEditor : Editor
{
    private SerializedProperty layers;
    private SerializedProperty frequency;
    private SerializedProperty intensity;
    private SerializedProperty animationEnabled;
    private SerializedProperty previewInEditor;

    private void OnEnable()
    {
        layers = serializedObject.FindProperty("layers");
        frequency = serializedObject.FindProperty("frequency");
        intensity = serializedObject.FindProperty("intensity");
        animationEnabled = serializedObject.FindProperty("animationEnabled");
        previewInEditor = serializedObject.FindProperty("previewInEditor");

        EditorApplication.update += EditorUpdate;
    }

    private void OnDisable()
    {
        EditorApplication.update -= EditorUpdate;
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.LabelField("Jitter Settings", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(frequency);
        EditorGUILayout.PropertyField(intensity);

        EditorGUI.BeginChangeCheck();
        EditorGUILayout.PropertyField(animationEnabled, new GUIContent("Animation Enabled"));
        if (EditorGUI.EndChangeCheck())
        {
            serializedObject.ApplyModifiedProperties();
            ForEachTarget(jitter =>
            {
                Undo.RegisterFullObjectHierarchyUndo(jitter.gameObject, "Toggle Ship Visual Jitter Animation");

                if (jitter.AnimationEnabled)
                    jitter.StartJitter();
                else
                    jitter.StopJitter();

                EditorUtility.SetDirty(jitter);
            });
            serializedObject.Update();
        }

        EditorGUI.BeginChangeCheck();
        EditorGUILayout.PropertyField(previewInEditor, new GUIContent("Preview In Editor"));
        if (EditorGUI.EndChangeCheck())
        {
            serializedObject.ApplyModifiedProperties();
            ForEachTarget(jitter =>
            {
                Undo.RecordObject(jitter, "Toggle Ship Visual Jitter Preview");
                jitter.SetEditorPreview(jitter.PreviewInEditor);
                EditorUtility.SetDirty(jitter);
            });
            serializedObject.Update();
        }

        EditorGUILayout.Space(6f);
        DrawPreviewControls();

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Layers", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Initial Position сохраняется в компоненте. Stop Preview и Disable возвращают части корабля в эти координаты.",
            MessageType.Info);

        EditorGUILayout.PropertyField(layers, true);

        EditorGUILayout.Space(6f);
        DrawLayerTools();

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawPreviewControls()
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Start Preview"))
            {
                serializedObject.ApplyModifiedProperties();
                ForEachTarget(jitter =>
                {
                    Undo.RecordObject(jitter, "Start Ship Visual Jitter Preview");
                    jitter.StartJitter();
                    jitter.SetEditorPreview(true);
                    EditorUtility.SetDirty(jitter);
                });
                serializedObject.Update();
            }

            if (GUILayout.Button("Stop Preview + Restore"))
            {
                serializedObject.ApplyModifiedProperties();
                ForEachTarget(jitter =>
                {
                    Undo.RegisterFullObjectHierarchyUndo(jitter.gameObject, "Stop Ship Visual Jitter Preview");
                    jitter.SetEditorPreview(false);
                    EditorUtility.SetDirty(jitter);
                });
                serializedObject.Update();
            }
        }
    }

    private void DrawLayerTools()
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Capture Current As Initial"))
            {
                serializedObject.ApplyModifiedProperties();
                ForEachTarget(jitter =>
                {
                    Undo.RecordObject(jitter, "Capture Ship Visual Jitter Positions");
                    jitter.CaptureInitialPositions(true);
                    EditorUtility.SetDirty(jitter);
                });
            }

            if (GUILayout.Button("Restore Initial Positions"))
            {
                serializedObject.ApplyModifiedProperties();
                ForEachTarget(jitter =>
                {
                    Undo.RegisterFullObjectHierarchyUndo(jitter.gameObject, "Restore Ship Visual Jitter Positions");
                    jitter.RestoreInitialPositions();
                    EditorUtility.SetDirty(jitter);
                });
            }
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Add Missing Children"))
            {
                serializedObject.ApplyModifiedProperties();
                ForEachTarget(jitter =>
                {
                    Undo.RecordObject(jitter, "Collect Ship Visual Jitter Children");
                    jitter.CollectDirectChildren(true, false);
                    jitter.CaptureInitialPositions(false);
                    EditorUtility.SetDirty(jitter);
                });
            }

            if (GUILayout.Button("Replace With Children"))
            {
                serializedObject.ApplyModifiedProperties();
                ForEachTarget(jitter =>
                {
                    Undo.RecordObject(jitter, "Replace Ship Visual Jitter Children");
                    jitter.CollectDirectChildren(true, true);
                    EditorUtility.SetDirty(jitter);
                });
            }
        }

        if (GUILayout.Button("Randomize Seeds"))
        {
            serializedObject.ApplyModifiedProperties();
            ForEachTarget(jitter =>
            {
                Undo.RecordObject(jitter, "Randomize Ship Visual Jitter Seeds");
                jitter.RandomizeSeeds();
                EditorUtility.SetDirty(jitter);
            });
        }
    }

    private void EditorUpdate()
    {
        if (Application.isPlaying)
            return;

        bool hasPreview = false;
        foreach (Object selectedTarget in targets)
        {
            if (selectedTarget is ShipVisualJitter jitter && jitter.PreviewInEditor)
            {
                hasPreview = true;
                break;
            }
        }

        if (!hasPreview)
            return;

        EditorApplication.QueuePlayerLoopUpdate();
        SceneView.RepaintAll();
        Repaint();
    }

    private void ForEachTarget(System.Action<ShipVisualJitter> action)
    {
        foreach (Object selectedTarget in targets)
        {
            if (selectedTarget is ShipVisualJitter jitter)
                action(jitter);
        }
    }
}

[CustomPropertyDrawer(typeof(JitterLayer))]
public class JitterLayerDrawer : PropertyDrawer
{
    private const float RowGap = 2f;

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        return EditorGUIUtility.singleLineHeight * 7f + RowGap * 6f + 4f;
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        float lineHeight = EditorGUIUtility.singleLineHeight;
        Rect row = new Rect(position.x, position.y + 2f, position.width, lineHeight);

        SerializedProperty transform = property.FindPropertyRelative("transform");
        SerializedProperty affectX = property.FindPropertyRelative("affectX");
        SerializedProperty affectY = property.FindPropertyRelative("affectY");
        SerializedProperty maxOffsetX = property.FindPropertyRelative("maxOffsetX");
        SerializedProperty maxOffsetY = property.FindPropertyRelative("maxOffsetY");
        SerializedProperty frequencyMultiplier = property.FindPropertyRelative("frequencyMultiplier");
        SerializedProperty seed = property.FindPropertyRelative("seed");
        SerializedProperty initialPosition = property.FindPropertyRelative("initialPosition");
        SerializedProperty hasInitialPosition = property.FindPropertyRelative("hasInitialPosition");

        EditorGUI.PropertyField(row, transform, new GUIContent(label.text));

        row.y += lineHeight + RowGap;
        float halfWidth = (row.width - 6f) * 0.5f;
        Rect affectXRect = new Rect(row.x, row.y, halfWidth, lineHeight);
        Rect affectYRect = new Rect(affectXRect.xMax + 6f, row.y, halfWidth, lineHeight);

        affectX.boolValue = EditorGUI.ToggleLeft(affectXRect, "Use X", affectX.boolValue);
        affectY.boolValue = EditorGUI.ToggleLeft(affectYRect, "Use Y", affectY.boolValue);

        row.y += lineHeight + RowGap;
        Rect offsetXRect = new Rect(row.x, row.y, halfWidth, lineHeight);
        Rect offsetYRect = new Rect(offsetXRect.xMax + 6f, row.y, halfWidth, lineHeight);
        EditorGUI.PropertyField(offsetXRect, maxOffsetX, new GUIContent("Max Offset X"));
        EditorGUI.PropertyField(offsetYRect, maxOffsetY, new GUIContent("Max Offset Y"));

        row.y += lineHeight + RowGap;
        EditorGUI.PropertyField(row, frequencyMultiplier, new GUIContent("Layer Frequency"));

        row.y += lineHeight + RowGap;
        EditorGUI.PropertyField(row, seed, new GUIContent("Seed"));

        row.y += lineHeight + RowGap;
        using (new EditorGUI.DisabledScope(true))
            EditorGUI.PropertyField(row, initialPosition, new GUIContent("Initial Position"));

        row.y += lineHeight + RowGap;
        using (new EditorGUI.DisabledScope(true))
            EditorGUI.ToggleLeft(row, hasInitialPosition.boolValue ? "Initial position captured" : "Initial position is not captured", hasInitialPosition.boolValue);

        EditorGUI.EndProperty();
    }
}
