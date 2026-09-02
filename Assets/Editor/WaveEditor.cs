using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(Wave))]
public sealed class WaveEditor : Editor
{
    private const string ShowCameraBoundsKey =
        "WaveEditor.ShowCameraBounds";
    private const string CameraBoundsOrthoSizeKey =
        "WaveEditor.CameraBoundsOrthoSize";
    private const string CameraBoundsAspectKey =
        "WaveEditor.CameraBoundsAspect";
    private const string CameraBoundsCenterXKey =
        "WaveEditor.CameraBoundsCenterX";
    private const string CameraBoundsCenterYKey =
        "WaveEditor.CameraBoundsCenterY";
    private const string PreviewDurationKey =
        "WaveEditor.PreviewDuration";
    private const string LegacyDirectedPostPreviewDurationKey =
        "WaveEditor.DirectedPostPreviewDuration";
    private const float DefaultPreviewDuration = 4f;
    private const double PreviewFrameInterval = 1d / 30d;

    private sealed class CuePreviewCache
    {
        public GameObject prefab;
        public DirectedEnemySubWave directed;
        public int dirtyCount = -1;
        public int sampleVersion = -1;
        public int[] spawnOrder;
        public GUIContent[] enemyLabels;
        public float cueTime;
        public Matrix4x4 parentMatrix;
        public string phaseName = "Entrance / Formation";
        public readonly Dictionary<int, Vector3> positions = new();
    }

    private SerializedProperty scheduledSubWaves;
    private SerializedProperty legacySubWaves;
    private SerializedProperty enableDebugLogs;

    private bool previewPlaying;
    private double previewStartTime;
    private double nextPreviewFrameTime;
    private float previewSampleElapsed;
    private int previewSampleVersion;
    private bool showCameraBounds;
    private float cameraBoundsOrthoSize;
    private float cameraBoundsAspect;
    private Vector2 cameraBoundsCenter;
    private float previewDuration;
    private readonly List<CuePreviewCache> cuePreviewCaches = new();
    private readonly List<Vector3> warningPreviewPoints = new();
    private readonly List<Vector3[]> warningPreviewPolygonBuffers = new();
    private readonly Vector3[] warningPreviewPlayfield = new Vector3[4];
    private readonly Vector3[] warningPreviewPathSegment = new Vector3[4];

    private void OnEnable()
    {
        scheduledSubWaves = serializedObject.FindProperty("scheduledSubWaves");
        legacySubWaves = serializedObject.FindProperty("SubWavesToCreate");
        enableDebugLogs = serializedObject.FindProperty("enableDebugLogs");

        showCameraBounds = EditorPrefs.GetBool(ShowCameraBoundsKey, true);
        cameraBoundsOrthoSize = EditorPrefs.GetFloat(
            CameraBoundsOrthoSizeKey,
            5f);
        cameraBoundsAspect = EditorPrefs.GetFloat(
            CameraBoundsAspectKey,
            9f / 16f);
        cameraBoundsCenter = new Vector2(
            EditorPrefs.GetFloat(CameraBoundsCenterXKey, 0f),
            EditorPrefs.GetFloat(CameraBoundsCenterYKey, 0f));
        if (EditorPrefs.HasKey(PreviewDurationKey))
        {
            previewDuration = EditorPrefs.GetFloat(
                PreviewDurationKey,
                DefaultPreviewDuration);
        }
        else if (EditorPrefs.HasKey(LegacyDirectedPostPreviewDurationKey))
        {
            previewDuration = EditorPrefs.GetFloat(
                LegacyDirectedPostPreviewDurationKey,
                DefaultPreviewDuration);
        }
        else
        {
            previewDuration = Mathf.Max(
                DefaultPreviewDuration,
                GetBaseRouteDuration());
        }

        Undo.undoRedoPerformed += InvalidatePreviewCaches;
    }

    private void OnDisable()
    {
        StopPreview();
        Undo.undoRedoPerformed -= InvalidatePreviewCaches;
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.HelpBox(
            "Wave Conductor controls which subwaves start together and which start later. "
            + "Use the same Start Delay for simultaneous groups.",
            MessageType.Info);

        EditorGUILayout.PropertyField(enableDebugLogs);

        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("Conductor Schedule", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(scheduledSubWaves, true);

        DrawScheduleWarnings();
        DrawLegacyTools();
        DrawPreviewControls();

        if (serializedObject.ApplyModifiedProperties())
            InvalidatePreviewCaches();
    }

    private void DrawLegacyTools()
    {
        if (legacySubWaves == null || legacySubWaves.arraySize <= 0)
            return;

        EditorGUILayout.Space(4f);
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("Legacy SubWavesToCreate", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(legacySubWaves, true);

            if (GUILayout.Button("Copy Legacy Subwaves To Schedule"))
                CopyLegacyToSchedule();
        }
    }

    private void CopyLegacyToSchedule()
    {
        scheduledSubWaves.arraySize = legacySubWaves.arraySize;

        for (int i = 0; i < legacySubWaves.arraySize; i++)
        {
            SerializedProperty cue = scheduledSubWaves.GetArrayElementAtIndex(i);
            cue.FindPropertyRelative("subWavePrefab").objectReferenceValue =
                legacySubWaves.GetArrayElementAtIndex(i).objectReferenceValue;
            cue.FindPropertyRelative("startDelay").floatValue = 0f;
        }
    }

    private void DrawPreviewControls()
    {
        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Conductor Preview", EditorStyles.boldLabel);

        float baseDuration = GetBaseRouteDuration();
        EditorGUILayout.LabelField(
            new GUIContent(
                "Base Route Duration",
                "Calculated time for one complete pass of the scheduled subwaves, including the initial danger warning."),
            $"{baseDuration:0.00}s");

        EditorGUI.BeginChangeCheck();
        previewDuration = Mathf.Max(
            0.1f,
            EditorGUILayout.FloatField(
                new GUIContent(
                    "Preview Duration",
                    "Exact time after which Conductor Preview stops."),
                previewDuration));
        if (EditorGUI.EndChangeCheck())
            SavePreviewDuration();

        if (GUILayout.Button("Use Base Route Duration"))
        {
            previewDuration = Mathf.Max(0.1f, baseDuration);
            SavePreviewDuration();
        }

        float totalDuration = GetTotalPreviewDuration();

        using (new EditorGUILayout.HorizontalScope())
        {
            GUI.enabled = !previewPlaying && scheduledSubWaves.arraySize > 0;
            if (GUILayout.Button("Preview Wave Schedule"))
                StartPreview();

            GUI.enabled = previewPlaying;
            if (GUILayout.Button("Stop Preview"))
                StopPreview();

            GUI.enabled = true;
        }

        if (previewPlaying)
        {
            EditorGUILayout.HelpBox(
                $"Previewing conductor: {GetPreviewElapsed():0.00}s / {totalDuration:0.00}s",
                MessageType.Info);
        }

        DrawCameraBoundsControls();
    }

    private void StartPreview()
    {
        InvalidatePreviewCaches();
        previewPlaying = true;
        previewStartTime = EditorApplication.timeSinceStartup;
        nextPreviewFrameTime = previewStartTime;
        previewSampleElapsed = 0f;
        previewSampleVersion++;
        SceneView.duringSceneGui -= DrawPreviewInScene;
        SceneView.duringSceneGui += DrawPreviewInScene;
        EditorApplication.update -= UpdatePreview;
        EditorApplication.update += UpdatePreview;
        SceneView.RepaintAll();
    }

    private void StopPreview()
    {
        if (!previewPlaying)
            return;

        previewPlaying = false;
        SceneView.duringSceneGui -= DrawPreviewInScene;
        EditorApplication.update -= UpdatePreview;
        InvalidatePreviewCaches();
        SceneView.RepaintAll();
        Repaint();
    }

    private void UpdatePreview()
    {
        if (!previewPlaying)
            return;

        double now = EditorApplication.timeSinceStartup;
        float elapsed = Mathf.Max(0f, (float)(now - previewStartTime));
        if (elapsed >= GetTotalPreviewDuration())
        {
            StopPreview();
            return;
        }

        if (now < nextPreviewFrameTime)
            return;

        previewSampleElapsed = elapsed;
        previewSampleVersion++;
        nextPreviewFrameTime = now + PreviewFrameInterval;
        SceneView.RepaintAll();
        Repaint();
    }

    private void DrawPreviewInScene(SceneView sceneView)
    {
        if (!previewPlaying
            || target == null
            || Event.current.type != EventType.Repaint)
            return;

        serializedObject.Update();

        Wave wave = (Wave)target;
        float elapsed = previewSampleElapsed;
        int visibleSubWaves = 0;
        WaveDangerWarningController dangerWarning =
            wave.GetComponent<WaveDangerWarningController>();
        float warningDuration = dangerWarning != null
            && dangerWarning.ShouldPlayWarning
            ? dangerWarning.WarningDuration
            : 0f;

        Handles.zTest = UnityEngine.Rendering.CompareFunction.Always;
        DrawCameraBoundsPreview();
        DrawDangerWarningPreview(wave, dangerWarning, elapsed);

        for (int i = 0; i < scheduledSubWaves.arraySize; i++)
        {
            SerializedProperty cue = scheduledSubWaves.GetArrayElementAtIndex(i);
            GameObject prefab = cue.FindPropertyRelative("subWavePrefab").objectReferenceValue as GameObject;
            float startDelay = GetCueStartDelay(cue) + warningDuration;

            if (prefab == null)
                continue;

            if (!TryGetReadyCuePreviewTime(elapsed, startDelay, out float cueTime))
            {
                DrawPendingSubWavePreviewLabel(wave, prefab, i, startDelay);
                continue;
            }

            visibleSubWaves++;
            DrawSubWavePreview(wave, prefab, i, startDelay, cueTime);
        }

        Handles.color = Color.white;
        Handles.Label(
            wave.transform.position + Vector3.up * 5f,
            $"Wave Conductor Preview\n"
            + $"Time: {elapsed:0.00}s / {GetTotalPreviewDuration():0.00}s\n"
            + $"Active scheduled subwaves: {visibleSubWaves}/{scheduledSubWaves.arraySize}");
    }

    private void DrawSubWavePreview(
        Wave wave,
        GameObject prefab,
        int cueIndex,
        float startDelay,
        float cueTime)
    {
        if (cueTime < 0f)
            return;

        CuePreviewCache cache = GetCuePreviewCache(cueIndex, prefab);
        DirectedEnemySubWave directed = cache.directed;
        if (directed == null)
        {
            Vector3 labelPosition = wave.transform.position
                + Vector3.up * (4.2f - cueIndex * 0.35f);
            Handles.color = Color.yellow;
            InfoAboutSubWave subWave = prefab.GetComponent<InfoAboutSubWave>();
            string message = subWave != null
                ? $"{prefab.name}: {subWave.GetType().Name} preview is not supported yet. Starts at {startDelay:0.##}s"
                : $"{prefab.name}: invalid schedule item. Drag a GameObject with InfoAboutSubWave/DirectedEnemySubWave.";

            Handles.Label(
                labelPosition,
                message);
            return;
        }

        int count = directed.GetSimulationEnemyCount();
        RefreshCuePreviewCache(cache, cueIndex, wave, cueTime, count);
        Dictionary<int, Vector3> previewPositions = cache.positions;
        int[] spawnOrder = cache.spawnOrder;
        float spawnInterval = directed.GetSimulationSpawnInterval();
        Color color = Color.HSVToRGB(Mathf.Repeat(cueIndex * 0.17f, 1f), 0.7f, 1f);

        int visibleEnemies = 0;
        Vector3 visibleCenter = Vector3.zero;
        Vector3 firstVisiblePosition = Vector3.zero;
        bool hasFirstVisiblePosition = false;
        int orderedCount = spawnOrder != null
            ? Mathf.Min(count, spawnOrder.Length)
            : 0;
        float radius = Mathf.Lerp(
            0.1f,
            0.16f,
            Mathf.PingPong(cueTime * 2f, 1f));
        for (int enemyIndex = 0; enemyIndex < orderedCount; enemyIndex++)
        {
            int formationIndex = spawnOrder[enemyIndex];
            float enemyTime = cueTime - enemyIndex * spawnInterval;
            if (enemyTime < 0f
                || !previewPositions.TryGetValue(
                    formationIndex,
                    out Vector3 position))
            {
                continue;
            }

            visibleEnemies++;
            visibleCenter += position;
            if (!hasFirstVisiblePosition)
            {
                firstVisiblePosition = position;
                hasFirstVisiblePosition = true;
            }

            Handles.color = new Color(color.r, color.g, color.b, 0.25f);
            Vector3 spawnPosition = directed.GetSimulationEntranceStartPosition(
                formationIndex,
                wave.transform);
            Handles.DrawAAPolyLine(2f, spawnPosition, position);
            Handles.color = new Color(color.r, color.g, color.b, 0.9f);
            Handles.DrawSolidDisc(position, Vector3.forward, radius * 1.5f);
            Handles.color = Color.black;
            Handles.DrawWireDisc(position, Vector3.forward, radius * 1.8f);
            Handles.color = Color.white;
            Handles.Label(
                position + Vector3.up * 0.2f,
                cache.enemyLabels[enemyIndex]);
        }

        if (visibleEnemies > 0 && hasFirstVisiblePosition)
        {
            visibleCenter /= visibleEnemies;
            Handles.color = new Color(1f, 0.45f, 0.05f, 0.95f);
            Handles.DrawAAPolyLine(4f, visibleCenter, firstVisiblePosition);
            Handles.DrawSolidDisc(visibleCenter, Vector3.forward, 0.08f);
            Handles.Label(
                firstVisiblePosition + Vector3.up * 0.45f,
                "orientation -> first visible");
        }

        Handles.color = color;
        Handles.Label(
            wave.transform.position + Vector3.up * (4.2f - cueIndex * 0.35f),
            $"{cueIndex}. {prefab.name} | delay {startDelay:0.##}s | visible {visibleEnemies}/{count}\n"
            + cache.phaseName);
    }

    private CuePreviewCache GetCuePreviewCache(
        int cueIndex,
        GameObject prefab)
    {
        while (cuePreviewCaches.Count <= cueIndex)
            cuePreviewCaches.Add(new CuePreviewCache());

        CuePreviewCache cache = cuePreviewCaches[cueIndex];
        if (cache.prefab == prefab)
            return cache;

        cache.directed?.InvalidateSimulationPreviewCache();
        cache.prefab = prefab;
        cache.directed = prefab != null
            ? prefab.GetComponent<DirectedEnemySubWave>()
            : null;
        cache.dirtyCount = -1;
        cache.sampleVersion = -1;
        cache.spawnOrder = null;
        cache.enemyLabels = null;
        cache.positions.Clear();
        return cache;
    }

    private void RefreshCuePreviewCache(
        CuePreviewCache cache,
        int cueIndex,
        Wave wave,
        float cueTime,
        int count)
    {
        DirectedEnemySubWave directed = cache.directed;
        int dirtyCount = EditorUtility.GetDirtyCount(directed);
        bool configurationChanged = cache.dirtyCount != dirtyCount
            || cache.spawnOrder == null
            || cache.spawnOrder.Length != count;

        if (configurationChanged)
        {
            directed.InvalidateSimulationPreviewCache();
            cache.spawnOrder = directed.GetSimulationSpawnOrder();
            cache.enemyLabels = BuildEnemyLabels(
                cueIndex,
                cache.spawnOrder);
            cache.dirtyCount = dirtyCount;
            cache.sampleVersion = -1;
        }

        Matrix4x4 parentMatrix = wave.transform.localToWorldMatrix;
        if (cache.sampleVersion == previewSampleVersion
            && Mathf.Approximately(cache.cueTime, cueTime)
            && cache.parentMatrix.Equals(parentMatrix))
        {
            return;
        }

        directed.EvaluateSimulationPreviewNonAlloc(
            cueTime,
            wave.transform,
            cache.spawnOrder,
            cache.positions,
            out cache.phaseName);
        cache.cueTime = cueTime;
        cache.parentMatrix = parentMatrix;
        cache.sampleVersion = previewSampleVersion;
    }

    private static GUIContent[] BuildEnemyLabels(
        int cueIndex,
        int[] spawnOrder)
    {
        if (spawnOrder == null)
            return System.Array.Empty<GUIContent>();

        GUIContent[] labels = new GUIContent[spawnOrder.Length];
        for (int i = 0; i < spawnOrder.Length; i++)
        {
            int formationIndex = spawnOrder[i];
            labels[i] = new GUIContent(
                formationIndex == i
                    ? $"{cueIndex}:{i}"
                    : $"{cueIndex}:{i}->{formationIndex}");
        }

        return labels;
    }

    private void InvalidatePreviewCaches()
    {
        for (int i = 0; i < cuePreviewCaches.Count; i++)
        {
            CuePreviewCache cache = cuePreviewCaches[i];
            cache.directed?.InvalidateSimulationPreviewCache();
            cache.sampleVersion = -1;
            cache.dirtyCount = -1;
            cache.spawnOrder = null;
            cache.enemyLabels = null;
            cache.positions.Clear();
        }

        previewSampleVersion++;
    }

    private static bool TryGetReadyCuePreviewTime(
        float waveElapsed,
        float startDelay,
        out float cueTime)
    {
        cueTime = waveElapsed - startDelay;
        if (cueTime < 0f)
            return false;

        cueTime = Mathf.Max(0f, cueTime);
        return true;
    }

    private static float GetCueStartDelay(SerializedProperty cue)
    {
        if (cue == null)
            return 0f;

        SerializedProperty startDelay = cue.FindPropertyRelative("startDelay");
        return startDelay != null ? Mathf.Max(0f, startDelay.floatValue) : 0f;
    }

    private static void DrawPendingSubWavePreviewLabel(
        Wave wave,
        GameObject prefab,
        int cueIndex,
        float startDelay)
    {
        if (wave == null || prefab == null)
            return;

        Handles.color = new Color(1f, 1f, 1f, 0.45f);
        Handles.Label(
            wave.transform.position + Vector3.up * (4.2f - cueIndex * 0.35f),
            $"{cueIndex}. {prefab.name} | starts at {startDelay:0.##}s | pending");
    }

    private void DrawDangerWarningPreview(
        Wave wave,
        WaveDangerWarningController dangerWarning,
        float elapsed)
    {
        if (wave == null
            || dangerWarning == null
            || !dangerWarning.IsWarningVisibleAt(elapsed))
        {
            return;
        }

        float warningAlpha = dangerWarning.GetWarningAlphaAt(elapsed);
        if (warningAlpha <= 0f)
            return;

        for (int i = 0; i < dangerWarning.ShapeCount; i++)
        {
            WaveDangerWarningShape shape = dangerWarning.GetShape(i);
            if (shape == null)
                continue;

            dangerWarning.GetShapeLocalPolygon(i, warningPreviewPoints);
            if (warningPreviewPoints.Count < (shape.IsOpenPath ? 2 : 3))
                continue;

            for (int pointIndex = 0;
                pointIndex < warningPreviewPoints.Count;
                pointIndex++)
            {
                warningPreviewPoints[pointIndex] = wave.transform.TransformPoint(
                    warningPreviewPoints[pointIndex]);
            }

            Vector3[] previewPolygon = GetWarningPreviewPolygonBuffer(
                i,
                warningPreviewPoints);

            Color dangerColor = dangerWarning.GetShapeColor(i);
            dangerColor.a = Mathf.Clamp01(dangerColor.a * warningAlpha);
            Handles.color = dangerColor;
            if (shape.IsOpenPath)
            {
                DrawDangerWarningOpenPathPreview(
                    previewPolygon,
                    GetDangerWarningWorldPathThickness(
                        shape,
                        dangerWarning.transform),
                    shape.ParabolaSegmentLengthScale,
                    dangerColor);
                continue;
            }

            if (shape.Inverted)
                DrawInvertedDangerWarningPreview(
                    wave,
                    dangerWarning,
                    previewPolygon,
                    dangerColor,
                    warningPreviewPlayfield);
            else
                Handles.DrawAAConvexPolygon(previewPolygon);

            Handles.color = new Color(
                dangerColor.r,
                dangerColor.g,
                dangerColor.b,
                0.95f * warningAlpha);
            for (int pointIndex = 0;
                pointIndex < warningPreviewPoints.Count;
                pointIndex++)
            {
                Handles.DrawLine(
                    previewPolygon[pointIndex],
                    previewPolygon[
                        (pointIndex + 1) % previewPolygon.Length]);
            }
        }
    }

    private void DrawDangerWarningOpenPathPreview(
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
            warningPreviewPathSegment[0] = from + normal;
            warningPreviewPathSegment[1] = from - normal;
            warningPreviewPathSegment[2] = to - normal;
            warningPreviewPathSegment[3] = to + normal;
            Handles.DrawSolidRectangleWithOutline(
                warningPreviewPathSegment,
                color,
                color);
        }
    }

    private static float GetDangerWarningWorldPathThickness(
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

    private Vector3[] GetWarningPreviewPolygonBuffer(
        int shapeIndex,
        List<Vector3> source)
    {
        while (warningPreviewPolygonBuffers.Count <= shapeIndex)
            warningPreviewPolygonBuffers.Add(System.Array.Empty<Vector3>());

        Vector3[] buffer = warningPreviewPolygonBuffers[shapeIndex];
        if (buffer.Length != source.Count)
        {
            buffer = new Vector3[source.Count];
            warningPreviewPolygonBuffers[shapeIndex] = buffer;
        }

        for (int i = 0; i < source.Count; i++)
            buffer[i] = source[i];

        return buffer;
    }

    private static void DrawInvertedDangerWarningPreview(
        Wave wave,
        WaveDangerWarningController dangerWarning,
        Vector3[] shapePoints,
        Color dangerColor,
        Vector3[] playfield)
    {
        Vector2 halfSize = dangerWarning.PlayfieldSize * 0.5f;
        Vector2 center = dangerWarning.PlayfieldCenter;
        playfield[0] = wave.transform.TransformPoint(new Vector3(
            center.x - halfSize.x,
            center.y - halfSize.y,
            0f));
        playfield[1] = wave.transform.TransformPoint(new Vector3(
            center.x - halfSize.x,
            center.y + halfSize.y,
            0f));
        playfield[2] = wave.transform.TransformPoint(new Vector3(
            center.x + halfSize.x,
            center.y + halfSize.y,
            0f));
        playfield[3] = wave.transform.TransformPoint(new Vector3(
            center.x + halfSize.x,
            center.y - halfSize.y,
            0f));

        Handles.DrawSolidRectangleWithOutline(
            playfield,
            dangerColor,
            new Color(dangerColor.r, dangerColor.g, dangerColor.b, 0.95f));
        Handles.color = new Color(0.2f, 0.9f, 0.7f, 0.22f);
        Handles.DrawAAConvexPolygon(shapePoints);
    }

    private float GetPreviewElapsed()
    {
        return Mathf.Max(0f, (float)(EditorApplication.timeSinceStartup - previewStartTime));
    }

    private void DrawCameraBoundsControls()
    {
        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("Camera View Bounds", EditorStyles.boldLabel);

        EditorGUI.BeginChangeCheck();

        showCameraBounds = EditorGUILayout.Toggle(
            "Show In Preview",
            showCameraBounds);
        cameraBoundsOrthoSize = Mathf.Max(
            0.01f,
            EditorGUILayout.FloatField(
                "Orthographic Size",
                cameraBoundsOrthoSize));
        cameraBoundsAspect = Mathf.Max(
            0.01f,
            EditorGUILayout.FloatField(
                "Aspect",
                cameraBoundsAspect));
        cameraBoundsCenter = EditorGUILayout.Vector2Field(
            "Center",
            cameraBoundsCenter);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("9:16"))
                cameraBoundsAspect = 9f / 16f;

            if (GUILayout.Button("9:19.5"))
                cameraBoundsAspect = 9f / 19.5f;

            if (GUILayout.Button("Use Camera"))
                ApplyCameraBoundsFromCurrentCamera();
        }

        if (EditorGUI.EndChangeCheck())
        {
            SaveCameraBoundsPrefs();
            SceneView.RepaintAll();
        }
    }

    private void ApplyCameraBoundsFromCurrentCamera()
    {
        Camera camera = Camera.main;
        if (camera == null)
            camera = SceneView.lastActiveSceneView?.camera;

        if (camera == null)
            return;

        if (camera.orthographic)
            cameraBoundsOrthoSize = camera.orthographicSize;

        cameraBoundsAspect = Mathf.Max(0.01f, camera.aspect);
        cameraBoundsCenter = camera.transform.position;
        SaveCameraBoundsPrefs();
        SceneView.RepaintAll();
    }

    private void SaveCameraBoundsPrefs()
    {
        EditorPrefs.SetBool(ShowCameraBoundsKey, showCameraBounds);
        EditorPrefs.SetFloat(CameraBoundsOrthoSizeKey, cameraBoundsOrthoSize);
        EditorPrefs.SetFloat(CameraBoundsAspectKey, cameraBoundsAspect);
        EditorPrefs.SetFloat(CameraBoundsCenterXKey, cameraBoundsCenter.x);
        EditorPrefs.SetFloat(CameraBoundsCenterYKey, cameraBoundsCenter.y);
    }

    private void DrawCameraBoundsPreview()
    {
        if (!showCameraBounds)
            return;

        float height = cameraBoundsOrthoSize * 2f;
        float width = height * cameraBoundsAspect;
        Vector3 center = new Vector3(
            cameraBoundsCenter.x,
            cameraBoundsCenter.y,
            0f);

        Vector3 leftTop = center + new Vector3(-width * 0.5f, height * 0.5f, 0f);
        Vector3 rightTop = center + new Vector3(width * 0.5f, height * 0.5f, 0f);
        Vector3 rightBottom = center + new Vector3(width * 0.5f, -height * 0.5f, 0f);
        Vector3 leftBottom = center + new Vector3(-width * 0.5f, -height * 0.5f, 0f);

        Handles.color = new Color(0.15f, 0.8f, 1f, 0.12f);
        Handles.DrawSolidRectangleWithOutline(
            new[] { leftTop, rightTop, rightBottom, leftBottom },
            new Color(0.15f, 0.8f, 1f, 0.08f),
            new Color(0.15f, 0.8f, 1f, 0.95f));

        Handles.color = new Color(0.15f, 0.8f, 1f, 0.45f);
        Handles.DrawDottedLine(
            center + Vector3.left * width * 0.5f,
            center + Vector3.right * width * 0.5f,
            4f);
        Handles.DrawDottedLine(
            center + Vector3.down * height * 0.5f,
            center + Vector3.up * height * 0.5f,
            4f);

        Handles.color = Color.white;
        Handles.Label(
            rightTop + new Vector3(0.15f, 0.15f, 0f),
            $"Camera View\n{width:0.00} x {height:0.00}\nAspect {cameraBoundsAspect:0.###}");
    }

    private float GetTotalPreviewDuration()
    {
        return Mathf.Max(0.1f, previewDuration);
    }

    private float GetBaseRouteDuration()
    {
        Wave wave = target as Wave;
        WaveDangerWarningController dangerWarning = wave != null
            ? wave.GetComponent<WaveDangerWarningController>()
            : null;
        float warningDuration = dangerWarning != null
            && dangerWarning.ShouldPlayWarning
            ? dangerWarning.WarningDuration
            : 0f;
        float duration = 0f;

        for (int i = 0; i < scheduledSubWaves.arraySize; i++)
        {
            SerializedProperty cue = scheduledSubWaves.GetArrayElementAtIndex(i);
            GameObject prefab = cue.FindPropertyRelative("subWavePrefab").objectReferenceValue as GameObject;
            float startDelay = GetCueStartDelay(cue);
            duration = Mathf.Max(
                duration,
                warningDuration
                + startDelay
                + GetSubWaveBaseRouteDuration(prefab));
        }

        return Mathf.Max(0f, duration);
    }

    private static float GetSubWaveBaseRouteDuration(GameObject prefab)
    {
        if (prefab == null)
            return 0f;

        DirectedEnemySubWave directed = prefab.GetComponent<DirectedEnemySubWave>();
        if (directed == null)
            return 3f;

        return directed.GetSimulationBaseRouteDuration();
    }

    private void SavePreviewDuration()
    {
        previewDuration = Mathf.Max(0.1f, previewDuration);
        EditorPrefs.SetFloat(
            PreviewDurationKey,
            previewDuration);
    }

    private void DrawScheduleWarnings()
    {
        if (scheduledSubWaves == null || scheduledSubWaves.arraySize <= 0)
            return;

        for (int i = 0; i < scheduledSubWaves.arraySize; i++)
        {
            SerializedProperty cue = scheduledSubWaves.GetArrayElementAtIndex(i);
            SerializedProperty prefabProperty = cue.FindPropertyRelative("subWavePrefab");
            GameObject prefab = prefabProperty.objectReferenceValue as GameObject;

            if (prefab == null)
            {
                EditorGUILayout.HelpBox(
                    $"Schedule item {i} has no subwave prefab.",
                    MessageType.Warning);
                continue;
            }

            if (prefab.GetComponent<InfoAboutSubWave>() != null)
                continue;

            InfoAboutSubWave suggestedSubWave = FindSubWaveNear(prefab);
            string message = suggestedSubWave != null
                ? $"Schedule item {i} points to '{prefab.name}', but this GameObject has no InfoAboutSubWave. "
                    + $"Did you mean '{suggestedSubWave.gameObject.name}'?"
                : $"Schedule item {i} points to '{prefab.name}', but this GameObject has no InfoAboutSubWave. "
                    + "Drag the subwave object itself, not a points/container object.";

            EditorGUILayout.HelpBox(message, MessageType.Warning);

            if (suggestedSubWave == null)
                continue;

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                if (GUILayout.Button($"Use {suggestedSubWave.gameObject.name}", GUILayout.Width(180f)))
                    prefabProperty.objectReferenceValue = suggestedSubWave.gameObject;
            }
        }
    }

    private static InfoAboutSubWave FindSubWaveNear(GameObject gameObject)
    {
        if (gameObject == null)
            return null;

        InfoAboutSubWave childSubWave = gameObject.GetComponentInChildren<InfoAboutSubWave>(true);
        if (childSubWave != null && childSubWave.gameObject != gameObject)
            return childSubWave;

        Transform root = gameObject.transform.root;
        if (root == null || root.gameObject == gameObject)
            return null;

        InfoAboutSubWave[] subWaves = root.GetComponentsInChildren<InfoAboutSubWave>(true);
        for (int i = 0; i < subWaves.Length; i++)
        {
            if (subWaves[i] != null && subWaves[i].gameObject != gameObject)
                return subWaves[i];
        }

        return null;
    }
}
