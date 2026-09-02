using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

[CustomEditor(typeof(WaveDangerWarningController))]
public sealed class WaveDangerWarningControllerEditor : Editor
{
    private const double PreviewFrameInterval = 1d / 30d;
    private const string DefaultPresetFolder = "Assets/Config/PatternWarnings";

    private readonly List<Vector3> previewLocalPoints = new(64);
    private readonly Vector3[] previewPlayfield = new Vector3[4];
    private readonly Vector3[] previewPathSegment = new Vector3[4];
    private Vector3[] previewPolygon = new Vector3[0];
    private bool isAreaPreviewVisible;
    private bool isWarningPreviewPlaying;
    private double warningPreviewStartTime;
    private double nextPreviewFrameTime;

    private void OnEnable()
    {
        SceneView.duringSceneGui += DrawAreaPreview;
    }

    private void OnDisable()
    {
        StopWarningAnimationPreview();
        SceneView.duringSceneGui -= DrawAreaPreview;
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.HelpBox(
            "All configured danger shapes flash before this Wave starts its scheduled subwaves. "
            + "The original Start Delay of every subwave begins after the warning. "
            + "Intersections use one alpha layer instead of becoming darker.",
            MessageType.Info);

        bool usesVisualPreset = DrawVisualPresetSettings();

        DrawPropertiesExcluding(
            serializedObject,
            "m_Script",
            "visualPreset",
            "flashCount",
            "visibleDuration",
            "hiddenInterval",
            "useAlphaTransition",
            "alphaFadeDuration",
            "alphaFadeCurve");

        if (!usesVisualPreset)
            DrawLocalVisualSettings();

        WaveDangerWarningController controller =
            target as WaveDangerWarningController;
        if (controller != null)
        {
            if (!controller.HasConfiguredWarnings)
            {
                EditorGUILayout.HelpBox(
                    "Add at least one Danger Shape to enable the warning.",
                    MessageType.Warning);
            }
            else
            {
                EditorGUILayout.LabelField(
                    "Warning Duration",
                    $"{controller.WarningDuration:0.00}s");
                EditorGUILayout.Space(3f);
                if (GUILayout.Button(isWarningPreviewPlaying
                        ? "Stop Warning Animation"
                        : "Play Warning Animation"))
                {
                    if (isWarningPreviewPlaying)
                        StopWarningAnimationPreview();
                    else
                        StartWarningAnimationPreview(controller);
                }

                if (GUILayout.Button(isAreaPreviewVisible
                        ? "Hide Danger Area Preview"
                        : "Show Danger Area Preview"))
                {
                    isAreaPreviewVisible = !isAreaPreviewVisible;
                    if (isAreaPreviewVisible)
                        StopWarningAnimationPreview();

                    SceneView.RepaintAll();
                }

                if (isAreaPreviewVisible)
                {
                    EditorGUILayout.HelpBox(
                        "The static warning area is displayed in Scene View. "
                        + "Use the Wave inspector preview to see the flash timing together with subwaves.",
                        MessageType.None);
                }
                else if (isWarningPreviewPlaying)
                {
                    EditorGUILayout.HelpBox(
                        "The warning is playing in Scene View with its configured flash timing.",
                        MessageType.None);
                }
            }
        }

        if (serializedObject.ApplyModifiedProperties())
            SceneView.RepaintAll();
    }

    private bool DrawVisualPresetSettings()
    {
        SerializedProperty visualPreset = serializedObject
            .FindProperty("visualPreset");
        EditorGUILayout.Space(3f);
        EditorGUILayout.LabelField("Visual Preset", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(visualPreset, new GUIContent("Preset"));

        if (visualPreset.objectReferenceValue == null)
        {
            if (GUILayout.Button("Export Current Settings As Preset"))
                ExportCurrentSettingsAsPreset();

            EditorGUILayout.Space(3f);
            return false;
        }

        EditorGUILayout.HelpBox(
            "This preset controls flash timing and alpha transition. Danger shapes and the global color remain local to this wave.",
            MessageType.None);
        if (GUILayout.Button("Use Preset As Local Settings"))
        {
            serializedObject.ApplyModifiedProperties();
            WaveDangerWarningController controller =
                target as WaveDangerWarningController;
            if (controller != null)
            {
                Undo.RecordObject(controller, "Use Warning Visual Preset As Local Settings");
                controller.UseVisualPresetAsLocalSettings();
                EditorUtility.SetDirty(controller);
            }

            serializedObject.Update();
            return false;
        }

        if (GUILayout.Button("Export Resolved Settings As Preset"))
            ExportCurrentSettingsAsPreset();

        EditorGUILayout.Space(3f);
        return true;
    }

    private void DrawLocalVisualSettings()
    {
        SerializedProperty useAlphaTransition = serializedObject
            .FindProperty("useAlphaTransition");
        SerializedProperty alphaFadeDuration = serializedObject
            .FindProperty("alphaFadeDuration");
        SerializedProperty alphaFadeCurve = serializedObject
            .FindProperty("alphaFadeCurve");

        EditorGUILayout.LabelField("Flash Timing", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(
            serializedObject.FindProperty("flashCount"),
            new GUIContent("Flash Count"));
        EditorGUILayout.PropertyField(
            serializedObject.FindProperty("visibleDuration"),
            new GUIContent("Visible Duration"));
        EditorGUILayout.PropertyField(
            serializedObject.FindProperty("hiddenInterval"),
            new GUIContent("Hidden Interval"));

        EditorGUILayout.Space(3f);
        EditorGUILayout.LabelField("Alpha Transition", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(
            useAlphaTransition,
            new GUIContent("Use Alpha Transition"));
        if (useAlphaTransition.boolValue)
        {
            EditorGUILayout.PropertyField(
                alphaFadeDuration,
                new GUIContent("Alpha Fade Duration"));
            EditorGUILayout.PropertyField(
                alphaFadeCurve,
                new GUIContent("Alpha Fade Curve"));
        }
    }

    private void ExportCurrentSettingsAsPreset()
    {
        serializedObject.ApplyModifiedProperties();
        EnsureProjectFolder(DefaultPresetFolder);

        string assetPath = EditorUtility.SaveFilePanelInProject(
            "Export Wave Danger Warning Visual Preset",
            "WaveDangerWarningVisualPreset",
            "asset",
            "Choose where to save the reusable warning visual preset.",
            DefaultPresetFolder);
        if (string.IsNullOrEmpty(assetPath))
            return;

        var preset = CreateInstance<WaveDangerWarningVisualPreset>();
        ((WaveDangerWarningController)target)
            .CopyResolvedVisualSettingsTo(preset);
        AssetDatabase.CreateAsset(preset, assetPath);
        Undo.RegisterCreatedObjectUndo(
            preset,
            "Export Wave Danger Warning Visual Preset");
        AssetDatabase.SaveAssets();
        Selection.activeObject = preset;
        EditorGUIUtility.PingObject(preset);
        serializedObject.Update();
    }

    private static void EnsureProjectFolder(string folderPath)
    {
        string[] segments = folderPath.Split('/');
        string currentFolder = segments[0];
        for (int i = 1; i < segments.Length; i++)
        {
            string nextFolder = currentFolder + "/" + segments[i];
            if (!AssetDatabase.IsValidFolder(nextFolder))
                AssetDatabase.CreateFolder(currentFolder, segments[i]);

            currentFolder = nextFolder;
        }
    }

    private void DrawAreaPreview(SceneView sceneView)
    {
        if (Event.current.type != EventType.Repaint
            || target is not WaveDangerWarningController controller
            || !controller.HasConfiguredWarnings)
        {
            return;
        }

        bool isAnimatedFrame = isWarningPreviewPlaying;
        if (!isAreaPreviewVisible && !isAnimatedFrame)
            return;

        float warningAlpha = 1f;
        if (isAnimatedFrame)
        {
            float elapsed = (float)(EditorApplication.timeSinceStartup
                - warningPreviewStartTime);
            warningAlpha = controller.GetWarningAlphaAt(elapsed);
            if (warningAlpha <= 0f)
                return;
        }

        CompareFunction previousZTest = Handles.zTest;
        Color previousColor = Handles.color;
        Handles.zTest = CompareFunction.Always;

        for (int i = 0; i < controller.ShapeCount; i++)
        {
            WaveDangerWarningShape shape = controller.GetShape(i);
            if (shape == null)
                continue;

            controller.GetShapeLocalPolygon(i, previewLocalPoints);
            if (previewLocalPoints.Count < (shape.IsOpenPath ? 2 : 3))
                continue;

            EnsurePreviewPolygonCapacity(previewLocalPoints.Count);
            for (int pointIndex = 0;
                pointIndex < previewLocalPoints.Count;
                pointIndex++)
            {
                previewPolygon[pointIndex] = controller.transform.TransformPoint(
                    previewLocalPoints[pointIndex]);
            }

            Color fillColor = controller.GetShapeColor(i);
            fillColor.a = isAnimatedFrame
                ? Mathf.Clamp01(fillColor.a * warningAlpha)
                : Mathf.Clamp01(fillColor.a * 0.55f);
            Color outlineColor = new Color(
                fillColor.r,
                fillColor.g,
                fillColor.b,
                isAnimatedFrame ? 0.95f * warningAlpha : 0.95f);

            if (shape.IsOpenPath)
            {
                Handles.color = fillColor;
                DrawOpenPathPreview(
                    previewPolygon,
                    GetWorldPathThickness(shape, controller.transform),
                    shape.ParabolaSegmentLengthScale,
                    fillColor);
                continue;
            }

            if (shape.Inverted)
                DrawInvertedAreaPreview(controller, fillColor, outlineColor);
            else
            {
                Handles.color = fillColor;
                Handles.DrawAAConvexPolygon(previewPolygon);
            }

            Handles.color = outlineColor;
            for (int pointIndex = 0;
                pointIndex < previewLocalPoints.Count;
                pointIndex++)
            {
                Handles.DrawLine(
                    previewPolygon[pointIndex],
                    previewPolygon[
                        (pointIndex + 1) % previewLocalPoints.Count]);
            }
        }

        Handles.color = previousColor;
        Handles.zTest = previousZTest;
    }

    private void DrawOpenPathPreview(
        Vector3[] points,
        float worldThickness,
        float segmentLengthScale,
        Color color)
    {
        if (points == null || points.Length < 2)
            return;

        color.a = 1f;
        for (int i = 0; i < points.Length - 1; i++)
        {
            Vector3 from = points[i];
            Vector3 to = points[i + 1];
            Vector3 tangent = to - from;
            if (tangent.sqrMagnitude <= 0.000001f)
                continue;

            Vector3 segmentCenter = (from + to) * 0.5f;
            Vector3 halfTangent = tangent * segmentLengthScale * 0.5f;
            from = segmentCenter - halfTangent;
            to = segmentCenter + halfTangent;
            float halfThickness = Mathf.Max(0.01f, worldThickness) * 0.5f;
            Vector3 normal = new Vector3(-tangent.y, tangent.x, 0f)
                .normalized * halfThickness;
            previewPathSegment[0] = from + normal;
            previewPathSegment[1] = from - normal;
            previewPathSegment[2] = to - normal;
            previewPathSegment[3] = to + normal;
            Handles.DrawSolidRectangleWithOutline(
                previewPathSegment,
                color,
                color);
        }
    }

    private static float GetWorldPathThickness(
        WaveDangerWarningShape shape,
        Transform transform)
    {
        float scale = transform == null
            ? 1f
            : Mathf.Max(
                Mathf.Abs(transform.lossyScale.x),
                Mathf.Abs(transform.lossyScale.y));
        return shape.LineThickness * scale;
    }

    private void StartWarningAnimationPreview(
        WaveDangerWarningController controller)
    {
        if (controller == null || controller.WarningDuration <= 0f)
            return;

        isAreaPreviewVisible = false;
        isWarningPreviewPlaying = true;
        warningPreviewStartTime = EditorApplication.timeSinceStartup;
        nextPreviewFrameTime = warningPreviewStartTime;
        EditorApplication.update += UpdateWarningAnimationPreview;
        SceneView.RepaintAll();
    }

    private void StopWarningAnimationPreview()
    {
        if (!isWarningPreviewPlaying)
            return;

        isWarningPreviewPlaying = false;
        EditorApplication.update -= UpdateWarningAnimationPreview;
        SceneView.RepaintAll();
    }

    private void UpdateWarningAnimationPreview()
    {
        WaveDangerWarningController controller =
            target as WaveDangerWarningController;
        if (controller == null || !controller.HasConfiguredWarnings)
        {
            StopWarningAnimationPreview();
            return;
        }

        double currentTime = EditorApplication.timeSinceStartup;
        if (currentTime - warningPreviewStartTime >= controller.WarningDuration)
        {
            StopWarningAnimationPreview();
            return;
        }

        if (currentTime < nextPreviewFrameTime)
            return;

        nextPreviewFrameTime = currentTime + PreviewFrameInterval;
        SceneView.RepaintAll();
    }

    private void DrawInvertedAreaPreview(
        WaveDangerWarningController controller,
        Color fillColor,
        Color outlineColor)
    {
        Vector2 halfSize = controller.PlayfieldSize * 0.5f;
        Vector2 center = controller.PlayfieldCenter;
        previewPlayfield[0] = controller.transform.TransformPoint(new Vector3(
            center.x - halfSize.x,
            center.y - halfSize.y,
            0f));
        previewPlayfield[1] = controller.transform.TransformPoint(new Vector3(
            center.x - halfSize.x,
            center.y + halfSize.y,
            0f));
        previewPlayfield[2] = controller.transform.TransformPoint(new Vector3(
            center.x + halfSize.x,
            center.y + halfSize.y,
            0f));
        previewPlayfield[3] = controller.transform.TransformPoint(new Vector3(
            center.x + halfSize.x,
            center.y - halfSize.y,
            0f));

        Handles.DrawSolidRectangleWithOutline(
            previewPlayfield,
            fillColor,
            outlineColor);
        Handles.color = new Color(0.2f, 0.9f, 0.7f, 0.22f);
        Handles.DrawAAConvexPolygon(previewPolygon);
    }

    private void EnsurePreviewPolygonCapacity(int count)
    {
        if (previewPolygon.Length == count)
            return;

        previewPolygon = new Vector3[count];
    }
}
