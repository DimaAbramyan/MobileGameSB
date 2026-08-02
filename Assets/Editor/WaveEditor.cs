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
    private const string DirectedPostPreviewDurationKey =
        "WaveEditor.DirectedPostPreviewDuration";
    private const float DefaultDirectedPostPreviewDuration = 4f;
    private const float InfiniteParallelPreviewExtraDuration = 60f;
    private const float LoopingPipelinePreviewCycles = 3f;
    private const int MaxPreviewLoopIterationsPerRepaint = 256;

    private SerializedProperty scheduledSubWaves;
    private SerializedProperty legacySubWaves;
    private SerializedProperty enableDebugLogs;

    private bool previewPlaying;
    private double previewStartTime;
    private bool showCameraBounds;
    private float cameraBoundsOrthoSize;
    private float cameraBoundsAspect;
    private Vector2 cameraBoundsCenter;
    private float directedPostPreviewDuration;

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
        directedPostPreviewDuration = EditorPrefs.GetFloat(
            DirectedPostPreviewDurationKey,
            DefaultDirectedPostPreviewDuration);
    }

    private void OnDisable()
    {
        StopPreview();
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

        serializedObject.ApplyModifiedProperties();
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

        EditorGUI.BeginChangeCheck();
        directedPostPreviewDuration = Mathf.Max(
            0f,
            EditorGUILayout.FloatField(
                new GUIContent(
                    "Post Preview Seconds",
                    "How many seconds Conductor Preview keeps showing directed subwave post behavior after enemies settle. Affects wobble/patrol preview tail."),
                directedPostPreviewDuration));
        if (EditorGUI.EndChangeCheck())
            SaveDirectedPostPreviewDuration();

        float totalDuration = GetTotalPreviewDuration();
        EditorGUILayout.LabelField("Preview Duration", $"{totalDuration:0.00}s");

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
        previewPlaying = true;
        previewStartTime = EditorApplication.timeSinceStartup;
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
        SceneView.RepaintAll();
        Repaint();
    }

    private void UpdatePreview()
    {
        if (!previewPlaying)
            return;

        if (GetPreviewElapsed() >= GetTotalPreviewDuration())
        {
            StopPreview();
            return;
        }

        SceneView.RepaintAll();
        Repaint();
    }

    private void DrawPreviewInScene(SceneView sceneView)
    {
        if (!previewPlaying || target == null)
            return;

        serializedObject.Update();

        Wave wave = (Wave)target;
        float elapsed = GetPreviewElapsed();
        int visibleSubWaves = 0;

        Handles.zTest = UnityEngine.Rendering.CompareFunction.Always;
        DrawCameraBoundsPreview();

        for (int i = 0; i < scheduledSubWaves.arraySize; i++)
        {
            SerializedProperty cue = scheduledSubWaves.GetArrayElementAtIndex(i);
            GameObject prefab = cue.FindPropertyRelative("subWavePrefab").objectReferenceValue as GameObject;
            float startDelay = GetCueStartDelay(cue);

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

        DirectedEnemySubWave directed = prefab.GetComponent<DirectedEnemySubWave>();
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

        DirectedPreviewData data = new DirectedPreviewData(directed, wave.transform);
        int count = data.GetEnemyCount();
        Color color = Color.HSVToRGB(Mathf.Repeat(cueIndex * 0.17f, 1f), 0.7f, 1f);

        int visibleEnemies = 0;
        Vector3 visibleCenter = Vector3.zero;
        Vector3 firstVisiblePosition = Vector3.zero;
        bool hasFirstVisiblePosition = false;
        for (int enemyIndex = 0; enemyIndex < count; enemyIndex++)
        {
            int formationIndex = data.GetFormationIndexForSpawnStep(enemyIndex);
            float enemyTime = cueTime - enemyIndex * data.spawnInterval;
            if (enemyTime < 0f)
                continue;

            visibleEnemies++;
            Vector3 position = data.GetEnemyPosition(formationIndex, enemyTime, cueTime);
            visibleCenter += position;
            if (!hasFirstVisiblePosition)
            {
                firstVisiblePosition = position;
                hasFirstVisiblePosition = true;
            }

            float radius = Mathf.Lerp(0.1f, 0.16f, Mathf.PingPong(cueTime * 2f, 1f));

            Handles.color = new Color(color.r, color.g, color.b, 0.25f);
            Handles.DrawAAPolyLine(2f, data.GetSpawnPosition(), position);
            Handles.color = new Color(color.r, color.g, color.b, 0.9f);
            Handles.DrawSolidDisc(position, Vector3.forward, radius * 1.5f);
            Handles.color = Color.black;
            Handles.DrawWireDisc(position, Vector3.forward, radius * 1.8f);
            Handles.color = Color.white;
            Handles.Label(
                position + Vector3.up * 0.2f,
                formationIndex == enemyIndex
                    ? $"{cueIndex}:{enemyIndex}"
                    : $"{cueIndex}:{enemyIndex}->{formationIndex}");
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
            + data.GetPostPipelinePreviewStatus(cueTime));
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
        float duration = 0f;

        for (int i = 0; i < scheduledSubWaves.arraySize; i++)
        {
            SerializedProperty cue = scheduledSubWaves.GetArrayElementAtIndex(i);
            GameObject prefab = cue.FindPropertyRelative("subWavePrefab").objectReferenceValue as GameObject;
            float startDelay = GetCueStartDelay(cue);
            duration = Mathf.Max(duration, startDelay + GetSubWavePreviewDuration(prefab));
        }

        return Mathf.Max(0.1f, duration + 0.25f);
    }

    private float GetSubWavePreviewDuration(GameObject prefab)
    {
        if (prefab == null)
            return 0f;

        DirectedEnemySubWave directed = prefab.GetComponent<DirectedEnemySubWave>();
        if (directed == null)
            return 3f;

        DirectedPreviewData data = new DirectedPreviewData(directed, null);
        int count = data.GetEnemyCount();
        float lastSpawn = Mathf.Max(0, count - 1) * data.spawnInterval;
        return lastSpawn
            + data.GetPathDuration()
            + data.settleDuration
            + data.postStartDelay
            + data.GetPostPreviewDuration(
                directedPostPreviewDuration,
                InfiniteParallelPreviewExtraDuration);
    }

    private void SaveDirectedPostPreviewDuration()
    {
        directedPostPreviewDuration = Mathf.Max(0f, directedPostPreviewDuration);
        EditorPrefs.SetFloat(
            DirectedPostPreviewDurationKey,
            directedPostPreviewDuration);
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

    private sealed class DirectedPreviewData
    {
        public readonly float spawnInterval;
        public readonly float settleDuration;
        public readonly float postStartDelay;

        private readonly Transform waveTransform;
        private readonly Transform prefabTransform;
        private readonly SerializedObject serializedSubWave;
        private readonly SerializedProperty pathCheckpoints;
        private readonly SerializedProperty patrolPoints;
        private readonly SerializedProperty customFormationPoints;
        private readonly SerializedProperty formationPointsRoot;
        private readonly DirectedWaveCoordinateSpace pathCoordinateSpace;
        private readonly DirectedWaveFormationLayout formationLayout;
        private readonly DirectedWaveCoordinateSpace formationCoordinateSpace;
        private readonly Vector3 formationCenter;
        private readonly Vector2 spacing;
        private readonly int enemyCount;
        private readonly DirectedWaveSpawnOrderMode spawnOrderMode;
        private readonly float spawnOrderAngle;
        private readonly float spawnOrderStartAngle;
        private readonly int columns;
        private readonly int rows;
        private readonly float arcRadius;
        private readonly float arcDegrees;
        private readonly int shapePointCount;
        private readonly float shapeRadius;
        private readonly Vector2 shapeFlattening;
        private readonly AnimationCurve settleCurve;
        private readonly SerializedProperty postCommands;
        private readonly bool postCommandPipelineLoop;
        private readonly Vector3 localMovementOffset;
        private readonly float localMovementDuration;
        private readonly bool localMovementLoop;
        private readonly bool localMovementPingPong;
        private readonly AnimationCurve localMovementCurve;
        private readonly Vector2 wobbleAmplitude;
        private readonly float wobbleFrequency;
        private readonly DirectedWaveWobblePhaseMode wobblePhaseMode;
        private readonly float wobblePhaseOffset;
        private readonly float wobbleDirectionAngle;
        private readonly float wobbleDirectionStep;
        private readonly bool patrolLoop;
        private readonly Vector2 selfOrbitRadius;
        private readonly float selfOrbitPhaseOffset;
        private readonly float selfRotationDegreesPerSecond;
        private readonly float formationRotationDegreesPerSecond;

        public DirectedPreviewData(DirectedEnemySubWave subWave, Transform waveTransform)
        {
            this.waveTransform = waveTransform;
            prefabTransform = subWave.transform;
            serializedSubWave = new SerializedObject(subWave);

            pathCheckpoints = serializedSubWave.FindProperty("pathCheckpoints");
            patrolPoints = serializedSubWave.FindProperty("patrolPoints");
            customFormationPoints = serializedSubWave.FindProperty("customFormationPoints");
            formationPointsRoot = serializedSubWave.FindProperty("formationPointsRoot");

            pathCoordinateSpace = (DirectedWaveCoordinateSpace)serializedSubWave
                .FindProperty("pathCoordinateSpace").enumValueIndex;
            formationLayout = (DirectedWaveFormationLayout)serializedSubWave
                .FindProperty("formationLayout").enumValueIndex;
            formationCoordinateSpace = (DirectedWaveCoordinateSpace)serializedSubWave
                .FindProperty("formationCoordinateSpace").enumValueIndex;
            formationCenter = serializedSubWave.FindProperty("formationCenter").vector3Value;
            spacing = serializedSubWave.FindProperty("spacing").vector2Value;
            enemyCount = Mathf.Max(1, serializedSubWave.FindProperty("enemyCount").intValue);
            spawnOrderMode = (DirectedWaveSpawnOrderMode)serializedSubWave
                .FindProperty("spawnOrderMode").enumValueIndex;
            spawnOrderAngle = serializedSubWave
                .FindProperty("spawnOrderAngle").floatValue;
            spawnOrderStartAngle = serializedSubWave
                .FindProperty("spawnOrderStartAngle").floatValue;
            columns = Mathf.Max(1, serializedSubWave.FindProperty("columns").intValue);
            rows = Mathf.Max(1, serializedSubWave.FindProperty("rows").intValue);
            arcRadius = Mathf.Max(0f, serializedSubWave.FindProperty("arcRadius").floatValue);
            arcDegrees = serializedSubWave.FindProperty("arcDegrees").floatValue;
            shapePointCount = Mathf.Max(1, serializedSubWave.FindProperty("shapePointCount").intValue);
            shapeRadius = Mathf.Max(0f, serializedSubWave.FindProperty("shapeRadius").floatValue);
            shapeFlattening = GetSafeFlattening(
                serializedSubWave.FindProperty("shapeFlattening").vector2Value);
            spawnInterval = Mathf.Max(0f, serializedSubWave.FindProperty("spawnInterval").floatValue);
            settleDuration = Mathf.Max(0f, serializedSubWave.FindProperty("settleDuration").floatValue);
            settleCurve = serializedSubWave.FindProperty("settleCurve").animationCurveValue;
            postCommands = serializedSubWave.FindProperty("postCommands");
            SerializedProperty pipelineLoopProperty =
                serializedSubWave.FindProperty("postCommandPipelineLoop");
            postCommandPipelineLoop = pipelineLoopProperty != null
                && pipelineLoopProperty.boolValue;
            postStartDelay = Mathf.Max(0f, serializedSubWave.FindProperty("postStartDelay").floatValue);
            localMovementOffset = serializedSubWave
                .FindProperty("localMovementOffset").vector3Value;
            localMovementDuration = Mathf.Max(
                0.01f,
                serializedSubWave.FindProperty("localMovementDuration").floatValue);
            localMovementLoop = serializedSubWave
                .FindProperty("localMovementLoop").boolValue;
            localMovementPingPong = serializedSubWave
                .FindProperty("localMovementPingPong").boolValue;
            localMovementCurve = serializedSubWave
                .FindProperty("localMovementCurve").animationCurveValue;
            wobbleAmplitude = serializedSubWave.FindProperty("wobbleAmplitude").vector2Value;
            wobbleFrequency = Mathf.Max(0f, serializedSubWave.FindProperty("wobbleFrequency").floatValue);
            wobblePhaseMode = (DirectedWaveWobblePhaseMode)serializedSubWave
                .FindProperty("wobblePhaseMode").enumValueIndex;
            wobblePhaseOffset = serializedSubWave.FindProperty("wobblePhaseOffset").floatValue;
            wobbleDirectionAngle = serializedSubWave.FindProperty("wobbleDirectionAngle").floatValue;
            wobbleDirectionStep = Mathf.Max(
                0.01f,
                serializedSubWave.FindProperty("wobbleDirectionStep").floatValue);
            patrolLoop = serializedSubWave.FindProperty("patrolLoop").boolValue;
            selfOrbitRadius = serializedSubWave
                .FindProperty("selfOrbitRadius").vector2Value;
            selfOrbitPhaseOffset = serializedSubWave
                .FindProperty("selfOrbitPhaseOffset").floatValue;
            selfRotationDegreesPerSecond = serializedSubWave
                .FindProperty("selfRotationDegreesPerSecond").floatValue;
            formationRotationDegreesPerSecond = serializedSubWave
                .FindProperty("formationRotationDegreesPerSecond").floatValue;
        }

        public int GetEnemyCount()
        {
            if (formationLayout == DirectedWaveFormationLayout.TransformPoints)
            {
                Transform root = formationPointsRoot.objectReferenceValue as Transform;
                return root != null ? root.childCount : 0;
            }

            if (formationLayout == DirectedWaveFormationLayout.CustomPoints
                && customFormationPoints != null
                && customFormationPoints.arraySize > 0)
            {
                return customFormationPoints.arraySize;
            }

            if (UsesShapeFormation())
                return shapePointCount;

            return enemyCount;
        }

        public int GetFormationIndexForSpawnStep(int spawnStep)
        {
            int count = GetEnemyCount();
            if (spawnStep < 0 || spawnStep >= count)
                return Mathf.Clamp(spawnStep, 0, Mathf.Max(0, count - 1));

            if (count <= 1 || spawnOrderMode == DirectedWaveSpawnOrderMode.Manual)
                return spawnStep;

            int[] order = BuildSpawnOrder(count);
            return spawnStep < order.Length ? order[spawnStep] : spawnStep;
        }

        private int[] BuildSpawnOrder(int count)
        {
            count = Mathf.Max(0, count);
            int[] order = new int[count];
            for (int i = 0; i < count; i++)
                order[i] = i;

            if (count <= 1 || spawnOrderMode == DirectedWaveSpawnOrderMode.Manual)
                return order;

            Vector3[] positions = new Vector3[count];
            Vector3 center = Vector3.zero;
            for (int i = 0; i < count; i++)
            {
                positions[i] = GetFormationPosition(i);
                center += positions[i];
            }

            center /= count;
            System.Array.Sort(
                order,
                (left, right) => CompareSpawnOrderIndices(
                    left,
                    right,
                    positions,
                    center));

            return order;
        }

        private int CompareSpawnOrderIndices(
            int left,
            int right,
            Vector3[] positions,
            Vector3 center)
        {
            int result = spawnOrderMode switch
            {
                DirectedWaveSpawnOrderMode.DirectionAngle =>
                    CompareByDirectionProjection(
                        positions[left],
                        positions[right]),
                DirectedWaveSpawnOrderMode.CenterToOutside =>
                    CompareByDistanceFromCenter(
                        positions[left],
                        positions[right],
                        center,
                        false),
                DirectedWaveSpawnOrderMode.OutsideToCenter =>
                    CompareByDistanceFromCenter(
                        positions[left],
                        positions[right],
                        center,
                        true),
                DirectedWaveSpawnOrderMode.Clockwise =>
                    CompareByAngleAroundCenter(
                        positions[left],
                        positions[right],
                        center,
                        true),
                DirectedWaveSpawnOrderMode.CounterClockwise =>
                    CompareByAngleAroundCenter(
                        positions[left],
                        positions[right],
                        center,
                        false),
                _ => left.CompareTo(right)
            };

            return result != 0 ? result : left.CompareTo(right);
        }

        private int CompareByDirectionProjection(Vector3 left, Vector3 right)
        {
            Vector2 direction = GetSpawnOrderDirection(spawnOrderAngle);
            float leftProjection = Vector2.Dot(left, direction);
            float rightProjection = Vector2.Dot(right, direction);
            return leftProjection.CompareTo(rightProjection);
        }

        private static int CompareByDistanceFromCenter(
            Vector3 left,
            Vector3 right,
            Vector3 center,
            bool outsideFirst)
        {
            float leftDistance = ((Vector2)(left - center)).sqrMagnitude;
            float rightDistance = ((Vector2)(right - center)).sqrMagnitude;
            int result = leftDistance.CompareTo(rightDistance);
            return outsideFirst ? -result : result;
        }

        private int CompareByAngleAroundCenter(
            Vector3 left,
            Vector3 right,
            Vector3 center,
            bool clockwise)
        {
            float leftAngle = GetNormalizedSpawnOrderAngle(left - center);
            float rightAngle = GetNormalizedSpawnOrderAngle(right - center);
            int result = leftAngle.CompareTo(rightAngle);
            return clockwise ? result : -result;
        }

        private float GetNormalizedSpawnOrderAngle(Vector3 offset)
        {
            float angle = Mathf.Atan2(offset.y, offset.x) * Mathf.Rad2Deg;
            float delta = Mathf.DeltaAngle(spawnOrderStartAngle, angle);
            return Mathf.Repeat(-delta, 360f);
        }

        private static Vector2 GetSpawnOrderDirection(float angleDegrees)
        {
            float radians = angleDegrees * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
        }

        public bool UsesWobble()
        {
            return HasPostCommand(DirectedWavePostCommandType.Wobble);
        }

        public bool UsesPatrol()
        {
            return HasPostCommand(DirectedWavePostCommandType.Patrol);
        }

        public bool UsesLocalMovement()
        {
            return HasPostCommand(DirectedWavePostCommandType.LocalMovement);
        }

        public bool UsesCircularMovement()
        {
            return HasPostCommand(DirectedWavePostCommandType.CircularMovement);
        }

        public bool UsesFormationRotation()
        {
            return HasPostCommand(DirectedWavePostCommandType.FormationRotation);
        }

        private bool HasPostCommand(DirectedWavePostCommandType type)
        {
            return HasPostCommandInArray(postCommands, type, 0);
        }

        private static bool HasPostCommandInArray(
            SerializedProperty commands,
            DirectedWavePostCommandType type,
            int depth)
        {
            if (commands == null || depth > 8)
                return false;

            for (int i = 0; i < commands.arraySize; i++)
            {
                SerializedProperty command = commands.GetArrayElementAtIndex(i);
                if (!IsPostCommandEnabled(command))
                    continue;

                DirectedWavePostCommandType commandType = GetPostCommandType(command);
                if (commandType == type)
                    return true;

                if (HasPostCommandInArray(
                        command.FindPropertyRelative("parallelCommands"),
                        type,
                        depth + 1))
                    return true;

                if (HasPostCommandInArray(
                        command.FindPropertyRelative("loopCommands"),
                        type,
                        depth + 1))
                    return true;
            }

            return false;
        }

        public float GetPostPreviewDuration(
            float fallbackLoopDuration,
            float infiniteParallelExtraDuration)
        {
            float duration = GetPostCommandPipelineDuration();
            bool hasInfiniteParallel = HasInfiniteParallel(postCommands);
            bool hasInfiniteLoop = HasInfiniteLoop(postCommands);
            bool hasInfiniteContainer = hasInfiniteParallel || hasInfiniteLoop;
            if (duration <= 0f)
                return hasInfiniteContainer
                    ? Mathf.Max(0f, infiniteParallelExtraDuration)
                    : 0f;

            if (float.IsInfinity(duration))
                duration = Mathf.Max(0f, infiniteParallelExtraDuration);
            else if (hasInfiniteContainer)
                duration += Mathf.Max(0f, infiniteParallelExtraDuration);

            return postCommandPipelineLoop
                ? duration * LoopingPipelinePreviewCycles
                    + Mathf.Max(0f, fallbackLoopDuration)
                : duration;
        }

        public string GetPostPipelinePreviewStatus(float subWaveTime)
        {
            if (!HasEnabledPostCommands())
                return "Post Pipeline: none";

            float postStart = (Mathf.Max(0, GetEnemyCount() - 1) * spawnInterval)
                + GetPathDuration()
                + settleDuration
                + postStartDelay;
            if (subWaveTime < postStart)
                return $"Post Pipeline: waiting start {subWaveTime:0.00}/{postStart:0.00}s";

            float pipelineDuration = GetPostCommandPipelineDuration();
            if (pipelineDuration <= 0f)
                return "Post Pipeline: duration 0";

            float postTime = subWaveTime - postStart;
            int cycle = postCommandPipelineLoop
                ? Mathf.FloorToInt(postTime / pipelineDuration) + 1
                : 1;
            float cycleTime = postCommandPipelineLoop
                ? postTime - Mathf.Floor(postTime / pipelineDuration) * pipelineDuration
                : Mathf.Min(postTime, pipelineDuration);

            return "Post Pipeline: "
                + $"loop={postCommandPipelineLoop}, "
                + $"cycle={cycle}, "
                + $"time={cycleTime:0.00}/{pipelineDuration:0.00}s, "
                + $"command={GetPostCommandNameAtTime(cycleTime)}";
        }

        private string GetPostCommandNameAtTime(float cycleTime)
        {
            if (postCommands == null)
                return "none";

            for (int i = 0; i < postCommands.arraySize; i++)
            {
                SerializedProperty command = postCommands.GetArrayElementAtIndex(i);
                if (!IsPostCommandEnabled(command))
                    continue;

                DirectedWavePostCommandType type = GetPostCommandType(command);
                float duration = GetPostCommandDuration(command);
                float holdDuration = GetPostCommandHoldDuration(command);

                if (cycleTime <= duration)
                    return $"{i + 1}:{type}";

                cycleTime -= duration;
                if (cycleTime <= holdDuration)
                    return $"{i + 1}:{type} Hold";

                cycleTime -= holdDuration;
            }

            return "complete";
        }

        public float GetPathDuration()
        {
            if (pathCheckpoints == null || pathCheckpoints.arraySize < 2)
                return 0f;

            float total = 0f;
            for (int i = 0; i < pathCheckpoints.arraySize - 1; i++)
            {
                total += Mathf.Max(
                    0.01f,
                    pathCheckpoints.GetArrayElementAtIndex(i)
                        .FindPropertyRelative("durationToNext").floatValue);
            }

            return total;
        }

        public Vector3 GetSpawnPosition()
        {
            if (pathCheckpoints != null && pathCheckpoints.arraySize > 0)
                return GetCheckpointPosition(0);

            return ToWorld(Vector3.zero, DirectedWaveCoordinateSpace.LocalToSubWave);
        }

        public Vector3 GetEnemyPosition(int index, float enemyTime, float subWaveTime)
        {
            float pathDuration = GetPathDuration();
            Vector3 formation = GetFormationPosition(index);
            Vector3 pathEnd = pathCheckpoints != null && pathCheckpoints.arraySize > 0
                ? GetCheckpointPosition(pathCheckpoints.arraySize - 1)
                : GetSpawnPosition();

            if (pathCheckpoints != null && pathCheckpoints.arraySize > 0 && enemyTime <= pathDuration)
                return EvaluateCheckpointPath(enemyTime);

            if (settleDuration > 0f)
            {
                float settleTime = Mathf.Clamp01((enemyTime - pathDuration) / settleDuration);
                float curved = EvaluateCurve(settleCurve, settleTime);
                if (settleTime < 1f)
                    return Vector3.LerpUnclamped(pathEnd, formation, curved);
            }

            return ApplyPostBehavior(index, formation, subWaveTime);
        }

        private Vector3 ApplyPostBehavior(int index, Vector3 formation, float subWaveTime)
        {
            if (!HasEnabledPostCommands())
                return formation;

            float postStart = (Mathf.Max(0, GetEnemyCount() - 1) * spawnInterval)
                + GetPathDuration()
                + settleDuration
                + postStartDelay;

            if (subWaveTime < postStart)
                return formation;

            float postTime = subWaveTime - postStart;
            float pipelineDuration = GetPostCommandPipelineDuration();
            if (pipelineDuration <= 0f)
            {
                Dictionary<int, Vector3> backgroundOnlyPositions =
                    GetInitialPostPositions();
                return SimulatePostPipelineUntil(
                    backgroundOnlyPositions,
                    index,
                    postTime,
                    formation);
            }

            Dictionary<int, Vector3> positions = GetInitialPostPositions();

            if (postCommandPipelineLoop && !float.IsInfinity(pipelineDuration))
                return SimulateLoopingPostPipelineUntil(
                    positions,
                    index,
                    postTime,
                    formation);

            if (!float.IsInfinity(pipelineDuration))
                postTime = Mathf.Min(postTime, pipelineDuration);
            return SimulatePostPipelineUntil(positions, index, postTime, formation);
        }

        private Vector3 SimulateLoopingPostPipelineUntil(
            Dictionary<int, Vector3> positions,
            int previewIndex,
            float time,
            Vector3 fallback)
        {
            const int MaxPreviewPipelineCycles = 256;
            int completedCycles = 0;

            while (time > 0f && completedCycles < MaxPreviewPipelineCycles)
            {
                bool usedAnyCommand = false;
                for (int i = 0; i < postCommands.arraySize; i++)
                {
                    SerializedProperty command = postCommands.GetArrayElementAtIndex(i);
                    if (!IsPostCommandEnabled(command))
                        continue;

                    if (IsBackgroundParallel(command))
                        continue;

                    usedAnyCommand = true;
                    float duration = GetPostCommandDuration(command);
                    float holdDuration = GetPostCommandHoldDuration(command);

                    if (time <= duration)
                    {
                        Dictionary<int, Vector3> frame = EvaluatePostCommand(
                            positions,
                            command,
                            Mathf.Clamp01(time / duration),
                            time);
                        return frame.TryGetValue(previewIndex, out Vector3 current)
                            ? current
                            : fallback;
                    }

                    ApplyPostCommandFinal(positions, command);
                    time -= duration;

                    if (time <= holdDuration)
                    {
                        return positions.TryGetValue(previewIndex, out Vector3 held)
                            ? held
                            : fallback;
                    }

                    time -= holdDuration;
                }

                if (!usedAnyCommand)
                    break;

                completedCycles++;
            }

            return positions.TryGetValue(previewIndex, out Vector3 final)
                ? final
                : fallback;
        }

        private bool HasEnabledPostCommands()
        {
            if (postCommands == null)
                return false;

            for (int i = 0; i < postCommands.arraySize; i++)
            {
                SerializedProperty command = postCommands.GetArrayElementAtIndex(i);
                if (IsPostCommandEnabled(command))
                    return true;
            }

            return false;
        }

        private float GetPostCommandPipelineDuration()
        {
            if (postCommands == null)
                return 0f;

            float duration = 0f;
            for (int i = 0; i < postCommands.arraySize; i++)
            {
                SerializedProperty command = postCommands.GetArrayElementAtIndex(i);
                if (!IsPostCommandEnabled(command))
                    continue;

                if (IsBackgroundParallel(command))
                    continue;

                duration += GetPostCommandDuration(command);
                if (float.IsInfinity(duration))
                    return duration;

                duration += GetPostCommandHoldDuration(command);
            }

            return duration;
        }

        private Dictionary<int, Vector3> GetInitialPostPositions()
        {
            int count = GetEnemyCount();
            Dictionary<int, Vector3> positions = new(count);
            for (int i = 0; i < count; i++)
                positions[i] = GetFormationPosition(i);

            return positions;
        }

        private Vector3 SimulatePostPipelineUntil(
            Dictionary<int, Vector3> positions,
            int previewIndex,
            float time,
            Vector3 fallback)
        {
            List<PreviewBackgroundParallelCommand> backgroundCommands = new();
            float timelineCursor = 0f;
            float remainingTime = time;

            for (int i = 0; i < postCommands.arraySize; i++)
            {
                SerializedProperty command = postCommands.GetArrayElementAtIndex(i);
                if (!IsPostCommandEnabled(command))
                    continue;

                if (IsBackgroundParallel(command))
                {
                    backgroundCommands.Add(
                        new PreviewBackgroundParallelCommand
                        {
                            command = command.Copy(),
                            startTime = timelineCursor
                        });
                    continue;
                }

                float duration = GetPostCommandDuration(command);
                float holdDuration = GetPostCommandHoldDuration(command);

                if (remainingTime <= duration)
                {
                    Dictionary<int, Vector3> frame = EvaluatePostCommand(
                        positions,
                        command,
                        float.IsInfinity(duration)
                            ? 0f
                            : Mathf.Clamp01(remainingTime / duration),
                        remainingTime);
                    frame = ApplyBackgroundParallels(
                        frame,
                        backgroundCommands,
                        timelineCursor + remainingTime);
                    return frame.TryGetValue(previewIndex, out Vector3 current)
                        ? current
                        : fallback;
                }

                ApplyPostCommandFinal(positions, command);
                remainingTime -= duration;
                timelineCursor += duration;

                if (remainingTime <= holdDuration)
                {
                    Dictionary<int, Vector3> frame = ApplyBackgroundParallels(
                        positions,
                        backgroundCommands,
                        timelineCursor + remainingTime);
                    return frame.TryGetValue(previewIndex, out Vector3 held)
                        ? held
                        : fallback;
                }

                remainingTime -= holdDuration;
                timelineCursor += holdDuration;
            }

            Dictionary<int, Vector3> finalFrame = ApplyBackgroundParallels(
                positions,
                backgroundCommands,
                timelineCursor + remainingTime);
            return finalFrame.TryGetValue(previewIndex, out Vector3 final)
                ? final
                : fallback;
        }

        private sealed class PreviewBackgroundParallelCommand
        {
            public SerializedProperty command;
            public float startTime;
        }

        private Dictionary<int, Vector3> ApplyBackgroundParallels(
            Dictionary<int, Vector3> positions,
            List<PreviewBackgroundParallelCommand> backgroundCommands,
            float timelineTime)
        {
            if (backgroundCommands.Count == 0)
                return positions;

            Dictionary<int, Vector3> frame = new(positions);
            for (int i = 0; i < backgroundCommands.Count; i++)
            {
                PreviewBackgroundParallelCommand background = backgroundCommands[i];
                if (background.command == null)
                    continue;

                float elapsed = timelineTime - background.startTime;
                if (elapsed < 0f)
                    continue;

                if (!IsInfiniteParallel(background.command)
                    && elapsed > GetPostCommandDuration(background.command))
                {
                    continue;
                }

                frame = EvaluateParallelPostCommand(
                    frame,
                    background.command,
                    elapsed,
                    false);
            }

            return frame;
        }

        private Dictionary<int, Vector3> EvaluatePostCommand(
            Dictionary<int, Vector3> positions,
            SerializedProperty command,
            float normalizedTime,
            float elapsedInCommand)
        {
            DirectedWavePostCommandType type = GetPostCommandType(command);
            float curved = EvaluateCurve(
                command.FindPropertyRelative("curve").animationCurveValue,
                normalizedTime);

            return type switch
            {
                DirectedWavePostCommandType.LocalMovement =>
                    LerpPositions(
                        positions,
                        GetPipelineMoveTargetPositions(command, positions),
                        curved),
                DirectedWavePostCommandType.Patrol =>
                    OffsetPositions(positions, GetPatrolOffset(elapsedInCommand)),
                DirectedWavePostCommandType.Wobble =>
                    ApplyWobbleOverlay(positions, elapsedInCommand),
                DirectedWavePostCommandType.CircularMovement =>
                    ApplyCircularOverlay(positions, elapsedInCommand),
                DirectedWavePostCommandType.FormationRotation =>
                    RotatePositions(
                        positions,
                        GetPositionsCenter(positions),
                        GetFormationRotationAngle(
                            command,
                            elapsedInCommand,
                            GetPostCommandDuration(command),
                            curved)),
                DirectedWavePostCommandType.FormationMorph =>
                    LerpPositions(
                        positions,
                        GetMorphTargetPositions(command, positions),
                        curved),
                DirectedWavePostCommandType.Parallel =>
                    EvaluateParallelPostCommand(
                        positions,
                        command,
                        elapsedInCommand,
                        false),
                DirectedWavePostCommandType.Loop =>
                    EvaluateLoopPostCommand(
                        positions,
                        command,
                        elapsedInCommand),
                _ => new Dictionary<int, Vector3>(positions)
            };
        }

        private void ApplyPostCommandFinal(
            Dictionary<int, Vector3> positions,
            SerializedProperty command)
        {
            DirectedWavePostCommandType type = GetPostCommandType(command);
            Dictionary<int, Vector3> final = type switch
            {
                DirectedWavePostCommandType.LocalMovement =>
                    GetPipelineMoveTargetPositions(command, positions),
                DirectedWavePostCommandType.Patrol =>
                    OffsetPositions(
                        positions,
                        GetPatrolOffset(GetPostCommandDuration(command))),
                DirectedWavePostCommandType.FormationRotation =>
                    RotatePositions(
                        positions,
                        GetPositionsCenter(positions),
                        GetFormationRotationAngle(
                            command,
                            GetPostCommandDuration(command),
                            GetPostCommandDuration(command),
                            1f)),
                DirectedWavePostCommandType.FormationMorph =>
                    GetMorphTargetPositions(command, positions),
                DirectedWavePostCommandType.Parallel =>
                    EvaluateParallelPostCommand(
                        positions,
                        command,
                        GetPostCommandDuration(command),
                        true),
                DirectedWavePostCommandType.Loop =>
                    EvaluateLoopPostCommand(
                        positions,
                        command,
                        float.IsInfinity(GetPostCommandDuration(command))
                            ? InfiniteParallelPreviewExtraDuration
                            : GetPostCommandDuration(command)),
                _ => positions
            };

            ReplacePositions(positions, final);
        }

        private Dictionary<int, Vector3> EvaluateParallelPostCommand(
            Dictionary<int, Vector3> positions,
            SerializedProperty command,
            float elapsed,
            bool finalFrame)
        {
            SerializedProperty parallelCommands =
                command.FindPropertyRelative("parallelCommands");
            if (parallelCommands == null || parallelCommands.arraySize == 0)
                return new Dictionary<int, Vector3>(positions);

            Dictionary<int, Vector3> frame = new(positions);
            float parallelDuration = GetPostCommandDuration(command);
            for (int i = 0; i < parallelCommands.arraySize; i++)
            {
                SerializedProperty child = parallelCommands.GetArrayElementAtIndex(i);
                if (!IsPostCommandEnabled(child))
                    continue;

                DirectedWavePostCommandType childType = GetPostCommandType(child);
                if (childType == DirectedWavePostCommandType.Parallel)
                    continue;

                if (finalFrame
                    && (childType == DirectedWavePostCommandType.Wobble
                        || childType == DirectedWavePostCommandType.CircularMovement))
                {
                    continue;
                }

                float childDuration = GetPostCommandDuration(child);
                bool continuousRotation = child
                    .FindPropertyRelative("continuousFormationRotation")
                    .boolValue;
                float childElapsed =
                    childType == DirectedWavePostCommandType.Patrol
                    || childType == DirectedWavePostCommandType.Wobble
                    || childType == DirectedWavePostCommandType.CircularMovement
                    || continuousRotation
                        ? Mathf.Min(elapsed, parallelDuration)
                        : Mathf.Min(elapsed, childDuration);
                float normalized = Mathf.Clamp01(childElapsed / childDuration);

                frame = EvaluatePostCommand(
                    frame,
                    child,
                    normalized,
                    childElapsed);
            }

            return frame;
        }

        private Dictionary<int, Vector3> EvaluateLoopPostCommand(
            Dictionary<int, Vector3> positions,
            SerializedProperty command,
            float elapsed)
        {
            SerializedProperty loopCommands =
                command.FindPropertyRelative("loopCommands");
            if (loopCommands == null || loopCommands.arraySize == 0)
                return new Dictionary<int, Vector3>(positions);

            Dictionary<int, Vector3> frame = new(positions);
            float iterationDuration = GetPostCommandArrayDuration(loopCommands);
            if (iterationDuration <= 0f)
                return frame;

            bool infinite = command.FindPropertyRelative("infiniteLoop").boolValue;
            int loopCount = infinite
                ? MaxPreviewLoopIterationsPerRepaint
                : Mathf.Max(1, command.FindPropertyRelative("loopCount").intValue);
            loopCount = Mathf.Min(loopCount, MaxPreviewLoopIterationsPerRepaint);
            float remaining = Mathf.Max(0f, elapsed);

            for (int iteration = 0; iteration < loopCount; iteration++)
            {
                if (remaining <= iterationDuration)
                    return EvaluatePostCommandArrayUntil(
                        frame,
                        loopCommands,
                        remaining);

                ApplyPostCommandArrayFinal(frame, loopCommands);
                remaining -= iterationDuration;
            }

            return frame;
        }

        private Dictionary<int, Vector3> EvaluatePostCommandArrayUntil(
            Dictionary<int, Vector3> positions,
            SerializedProperty commands,
            float time)
        {
            Dictionary<int, Vector3> frame = new(positions);
            float remaining = Mathf.Max(0f, time);
            for (int i = 0; i < commands.arraySize; i++)
            {
                SerializedProperty child = commands.GetArrayElementAtIndex(i);
                if (!IsPostCommandEnabled(child) || IsBackgroundParallel(child))
                    continue;

                if (GetPostCommandType(child) == DirectedWavePostCommandType.Loop)
                    continue;

                float duration = GetPostCommandDuration(child);
                float hold = GetPostCommandHoldDuration(child);

                if (remaining <= duration)
                {
                    return EvaluatePostCommand(
                        frame,
                        child,
                        float.IsInfinity(duration)
                            ? 0f
                            : Mathf.Clamp01(remaining / duration),
                        remaining);
                }

                ApplyPostCommandFinal(frame, child);
                remaining -= duration;

                if (remaining <= hold)
                    return frame;

                remaining -= hold;
            }

            return frame;
        }

        private void ApplyPostCommandArrayFinal(
            Dictionary<int, Vector3> positions,
            SerializedProperty commands)
        {
            for (int i = 0; i < commands.arraySize; i++)
            {
                SerializedProperty child = commands.GetArrayElementAtIndex(i);
                if (!IsPostCommandEnabled(child) || IsBackgroundParallel(child))
                    continue;

                if (GetPostCommandType(child) == DirectedWavePostCommandType.Loop)
                    continue;

                ApplyPostCommandFinal(positions, child);
            }
        }

        private static float GetPostCommandArrayDuration(SerializedProperty commands)
        {
            if (commands == null)
                return 0f;

            float duration = 0f;
            for (int i = 0; i < commands.arraySize; i++)
            {
                SerializedProperty child = commands.GetArrayElementAtIndex(i);
                if (!IsPostCommandEnabled(child) || IsBackgroundParallel(child))
                    continue;

                if (GetPostCommandType(child) == DirectedWavePostCommandType.Loop)
                    continue;

                duration += GetPostCommandDuration(child);
                if (float.IsInfinity(duration))
                    return duration;

                duration += GetPostCommandHoldDuration(child);
            }

            return duration;
        }

        private float GetFormationRotationAngle(
            SerializedProperty command,
            float elapsed,
            float duration,
            float curved)
        {
            bool continuous = command
                .FindPropertyRelative("continuousFormationRotation")
                .boolValue;
            float rotationValue = command
                .FindPropertyRelative("rotationDegrees")
                .floatValue;

            if (continuous)
            {
                float degreesPerSecond = Mathf.Abs(rotationValue) > 0.0001f
                    ? rotationValue
                    : formationRotationDegreesPerSecond;
                return degreesPerSecond * elapsed;
            }

            float totalAngle = Mathf.Abs(rotationValue) > 0.0001f
                ? rotationValue
                : duration * formationRotationDegreesPerSecond;
            return totalAngle * curved;
        }

        private static bool IsPostCommandEnabled(SerializedProperty command)
        {
            return command != null
                && command.FindPropertyRelative("enabled").boolValue;
        }

        private static DirectedWavePostCommandType GetPostCommandType(
            SerializedProperty command)
        {
            return (DirectedWavePostCommandType)command
                .FindPropertyRelative("type")
                .enumValueIndex;
        }

        private static float GetPostCommandDuration(SerializedProperty command)
        {
            if (IsInfiniteParallel(command))
                return Mathf.Infinity;

            if (GetPostCommandType(command) == DirectedWavePostCommandType.Loop)
            {
                if (command.FindPropertyRelative("infiniteLoop").boolValue)
                    return Mathf.Infinity;

                float iterationDuration = GetPostCommandArrayDuration(
                    command.FindPropertyRelative("loopCommands"));
                return iterationDuration
                    * Mathf.Max(
                        1,
                        command.FindPropertyRelative("loopCount").intValue);
            }

            return Mathf.Max(
                0.01f,
                command.FindPropertyRelative("duration").floatValue);
        }

        private static bool IsBackgroundParallel(SerializedProperty command)
        {
            if (command == null)
                return false;

            SerializedProperty type = command.FindPropertyRelative("type");
            SerializedProperty parallelExecutionMode =
                command.FindPropertyRelative("parallelExecutionMode");

            return type != null
                && parallelExecutionMode != null
                && type.enumValueIndex == (int)DirectedWavePostCommandType.Parallel
                && parallelExecutionMode.enumValueIndex
                    == (int)DirectedWaveParallelExecutionMode.Background;
        }

        private static bool IsInfiniteParallel(SerializedProperty command)
        {
            if (command == null)
                return false;

            SerializedProperty type = command.FindPropertyRelative("type");
            SerializedProperty infiniteParallel =
                command.FindPropertyRelative("infiniteParallel");

            return type != null
                && infiniteParallel != null
                && type.enumValueIndex == (int)DirectedWavePostCommandType.Parallel
                && infiniteParallel.boolValue;
        }

        private static bool HasInfiniteParallel(SerializedProperty commands)
        {
            return HasInfiniteParallel(commands, 0);
        }

        private static bool HasInfiniteParallel(SerializedProperty commands, int depth)
        {
            if (commands == null || depth > 8)
                return false;

            for (int i = 0; i < commands.arraySize; i++)
            {
                SerializedProperty command = commands.GetArrayElementAtIndex(i);
                if (command == null || !IsPostCommandEnabled(command))
                    continue;

                if (IsInfiniteParallel(command))
                    return true;

                SerializedProperty parallelCommands =
                    command.FindPropertyRelative("parallelCommands");
                if (HasInfiniteParallel(parallelCommands, depth + 1))
                    return true;

                SerializedProperty loopCommands =
                    command.FindPropertyRelative("loopCommands");
                if (HasInfiniteParallel(loopCommands, depth + 1))
                    return true;
            }

            return false;
        }

        private static bool HasInfiniteLoop(SerializedProperty commands)
        {
            return HasInfiniteLoop(commands, 0);
        }

        private static bool HasInfiniteLoop(SerializedProperty commands, int depth)
        {
            if (commands == null || depth > 8)
                return false;

            for (int i = 0; i < commands.arraySize; i++)
            {
                SerializedProperty command = commands.GetArrayElementAtIndex(i);
                if (command == null || !IsPostCommandEnabled(command))
                    continue;

                if (GetPostCommandType(command) == DirectedWavePostCommandType.Loop
                    && command.FindPropertyRelative("infiniteLoop").boolValue)
                {
                    return true;
                }

                if (HasInfiniteLoop(
                        command.FindPropertyRelative("parallelCommands"),
                        depth + 1))
                    return true;

                if (HasInfiniteLoop(
                        command.FindPropertyRelative("loopCommands"),
                        depth + 1))
                    return true;
            }

            return false;
        }

        private static float GetPostCommandHoldDuration(SerializedProperty command)
        {
            return Mathf.Max(
                0f,
                command.FindPropertyRelative("holdDuration").floatValue);
        }

        private Dictionary<int, Vector3> GetPipelineMoveTargetPositions(
            SerializedProperty command,
            Dictionary<int, Vector3> positions)
        {
            Vector3 currentCenter = GetPositionsCenter(positions);
            Vector3 targetCenter = GetFormationCenter()
                + command.FindPropertyRelative("targetOffset").vector3Value;
            return OffsetPositions(positions, targetCenter - currentCenter);
        }

        private Dictionary<int, Vector3> GetMorphTargetPositions(
            SerializedProperty command,
            Dictionary<int, Vector3> positions)
        {
            SerializedProperty morphTarget = command.FindPropertyRelative("morphTarget");
            if (morphTarget == null)
                return new Dictionary<int, Vector3>(positions);

            Vector3[] targets = CreateMorphTargetShapePositions(
                morphTarget,
                positions.Count,
                GetPositionsCenter(positions));
            List<int> freeTargetIndices = new(targets.Length);
            for (int i = 0; i < targets.Length; i++)
                freeTargetIndices.Add(i);

            Dictionary<int, Vector3> result = new(positions.Count);
            foreach (KeyValuePair<int, Vector3> pair in positions)
            {
                if (freeTargetIndices.Count == 0)
                {
                    result[pair.Key] = pair.Value;
                    continue;
                }

                int closestListIndex = 0;
                float closestDistance = float.PositiveInfinity;
                for (int i = 0; i < freeTargetIndices.Count; i++)
                {
                    int targetIndex = freeTargetIndices[i];
                    float distance = (targets[targetIndex] - pair.Value).sqrMagnitude;
                    if (distance < closestDistance)
                    {
                        closestDistance = distance;
                        closestListIndex = i;
                    }
                }

                int closestTargetIndex = freeTargetIndices[closestListIndex];
                result[pair.Key] = targets[closestTargetIndex];
                freeTargetIndices.RemoveAt(closestListIndex);
            }

            return result;
        }

        private Vector3[] CreateMorphTargetShapePositions(
            SerializedProperty morphTarget,
            int count,
            Vector3 currentCenter)
        {
            count = Mathf.Max(1, count);
            Vector3[] positions = new Vector3[count];
            Vector3 center = currentCenter
                + morphTarget.FindPropertyRelative("centerOffset").vector3Value;
            Vector2 flattening = GetSafeFlattening(
                morphTarget.FindPropertyRelative("shapeFlattening").vector2Value);

            for (int i = 0; i < count; i++)
                positions[i] = GetMorphTargetShapePosition(
                    i,
                    count,
                    center,
                    morphTarget,
                    flattening);

            return positions;
        }

        private Vector3 GetMorphTargetShapePosition(
            int index,
            int count,
            Vector3 center,
            SerializedProperty morphTarget,
            Vector2 flattening)
        {
            DirectedWaveFormationLayout layout =
                (DirectedWaveFormationLayout)morphTarget
                    .FindPropertyRelative("layout")
                    .enumValueIndex;
            float radius = Mathf.Max(
                0f,
                morphTarget.FindPropertyRelative("shapeRadius").floatValue);

            return layout switch
            {
                DirectedWaveFormationLayout.VerticalLine =>
                    GetMorphLinePosition(index, count, center, radius, true),
                DirectedWaveFormationLayout.Grid =>
                    GetMorphGridPosition(index, count, center, morphTarget),
                DirectedWaveFormationLayout.VShape =>
                    GetMorphVShapePosition(index, center, radius),
                DirectedWaveFormationLayout.Arc =>
                    GetMorphArcPosition(index, count, center, morphTarget, flattening),
                DirectedWaveFormationLayout.Circle =>
                    GetMorphCirclePosition(index, count, center, radius, flattening),
                DirectedWaveFormationLayout.Triangle =>
                    center + GetPolygonPoint(
                        index,
                        count,
                        GetMorphTriangleVertices(flattening))
                        * radius,
                DirectedWaveFormationLayout.Square =>
                    center + GetPolygonPoint(
                        index,
                        count,
                        GetMorphSquareVertices(flattening))
                        * radius,
                DirectedWaveFormationLayout.Diamond =>
                    center + GetPolygonPoint(
                        index,
                        count,
                        GetMorphDiamondVertices(flattening))
                        * radius,
                DirectedWaveFormationLayout.CustomPoints =>
                    GetMorphCustomPoint(index, center, morphTarget),
                _ => GetMorphLinePosition(index, count, center, radius, false)
            };
        }

        private static Vector3 GetMorphLinePosition(
            int index,
            int count,
            Vector3 center,
            float spacing,
            bool vertical)
        {
            spacing = Mathf.Max(0.01f, spacing);
            float offset = (count - 1) * spacing * 0.5f;
            return vertical
                ? center + new Vector3(0f, index * spacing - offset, 0f)
                : center + new Vector3(index * spacing - offset, 0f, 0f);
        }

        private static Vector3 GetMorphVShapePosition(
            int index,
            Vector3 center,
            float spacing)
        {
            if (index == 0)
                return center;

            spacing = Mathf.Max(0.01f, spacing);
            int sideIndex = (index + 1) / 2;
            int side = index % 2 == 0 ? 1 : -1;
            return center + new Vector3(
                side * sideIndex * spacing,
                sideIndex * spacing,
                0f);
        }

        private static Vector3 GetMorphGridPosition(
            int index,
            int count,
            Vector3 center,
            SerializedProperty morphTarget)
        {
            int columns = Mathf.Max(
                1,
                morphTarget.FindPropertyRelative("columns").intValue);
            int rows = Mathf.Max(1, Mathf.CeilToInt(count / (float)columns));
            float spacing = Mathf.Max(
                0.01f,
                morphTarget.FindPropertyRelative("shapeRadius").floatValue);
            int row = index / columns;
            int column = index % columns;
            float xOffset = (Mathf.Min(columns, count) - 1) * spacing * 0.5f;
            float yOffset = (rows - 1) * spacing * 0.5f;
            return center + new Vector3(
                column * spacing - xOffset,
                yOffset - row * spacing,
                0f);
        }

        private static Vector3 GetMorphArcPosition(
            int index,
            int count,
            Vector3 center,
            SerializedProperty morphTarget,
            Vector2 flattening)
        {
            if (count <= 1)
                return center;

            float arcRadius = Mathf.Max(
                0f,
                morphTarget.FindPropertyRelative("arcRadius").floatValue);
            float arcDegrees = morphTarget.FindPropertyRelative("arcDegrees").floatValue;
            float startAngle = 90f - arcDegrees * 0.5f;
            float angle = startAngle + arcDegrees * index / (count - 1);
            float radians = angle * Mathf.Deg2Rad;
            return center + new Vector3(
                Mathf.Cos(radians) * arcRadius * flattening.x,
                Mathf.Sin(radians) * arcRadius * flattening.y,
                0f);
        }

        private static Vector3 GetMorphCirclePosition(
            int index,
            int count,
            Vector3 center,
            float radius,
            Vector2 flattening)
        {
            float angle = 90f - 360f * index / Mathf.Max(1, count);
            float radians = angle * Mathf.Deg2Rad;
            return center + new Vector3(
                Mathf.Cos(radians) * radius * flattening.x,
                Mathf.Sin(radians) * radius * flattening.y,
                0f);
        }

        private static Vector3 GetPolygonPoint(
            int index,
            int count,
            Vector3[] vertices)
        {
            if (count <= 1 || vertices == null || vertices.Length == 0)
                return Vector3.zero;

            float totalLength = 0f;
            for (int i = 0; i < vertices.Length; i++)
            {
                totalLength += Vector3.Distance(
                    vertices[i],
                    vertices[(i + 1) % vertices.Length]);
            }

            if (totalLength <= 0.0001f)
                return vertices[0];

            float remaining = index / (count - 1f) * totalLength;
            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 from = vertices[i];
                Vector3 to = vertices[(i + 1) % vertices.Length];
                float edgeLength = Vector3.Distance(from, to);
                if (remaining <= edgeLength)
                {
                    float time = edgeLength <= 0.0001f
                        ? 0f
                        : remaining / edgeLength;
                    return Vector3.LerpUnclamped(from, to, time);
                }

                remaining -= edgeLength;
            }

            return vertices[0];
        }

        private static Vector3[] GetMorphTriangleVertices(Vector2 flattening)
        {
            return new[]
            {
                GetMorphShapePoint(90f, flattening),
                GetMorphShapePoint(210f, flattening),
                GetMorphShapePoint(330f, flattening)
            };
        }

        private static Vector3[] GetMorphSquareVertices(Vector2 flattening)
        {
            return new[]
            {
                new Vector3(-flattening.x, flattening.y, 0f),
                new Vector3(flattening.x, flattening.y, 0f),
                new Vector3(flattening.x, -flattening.y, 0f),
                new Vector3(-flattening.x, -flattening.y, 0f)
            };
        }

        private static Vector3[] GetMorphDiamondVertices(Vector2 flattening)
        {
            return new[]
            {
                Vector3.up * flattening.y,
                Vector3.right * flattening.x,
                Vector3.down * flattening.y,
                Vector3.left * flattening.x
            };
        }

        private static Vector3 GetMorphShapePoint(
            float angleDegrees,
            Vector2 flattening)
        {
            float radians = angleDegrees * Mathf.Deg2Rad;
            return new Vector3(
                Mathf.Cos(radians) * flattening.x,
                Mathf.Sin(radians) * flattening.y,
                0f);
        }

        private static Vector3 GetMorphCustomPoint(
            int index,
            Vector3 center,
            SerializedProperty morphTarget)
        {
            SerializedProperty customPoints = morphTarget.FindPropertyRelative("customPoints");
            if (customPoints == null || customPoints.arraySize == 0)
                return center;

            int safeIndex = Mathf.Clamp(index, 0, customPoints.arraySize - 1);
            return center + customPoints.GetArrayElementAtIndex(safeIndex).vector3Value;
        }

        private Dictionary<int, Vector3> ApplyWobbleOverlay(
            Dictionary<int, Vector3> positions,
            float elapsed)
        {
            Dictionary<int, Vector3> result = new(positions.Count);
            float leading = GetLeadingProjection(positions);
            foreach (KeyValuePair<int, Vector3> pair in positions)
                result[pair.Key] = pair.Value
                    + GetWobbleOffset(pair.Key, pair.Value, elapsed, leading);

            return result;
        }

        private Dictionary<int, Vector3> ApplyCircularOverlay(
            Dictionary<int, Vector3> positions,
            float elapsed)
        {
            Dictionary<int, Vector3> result = new(positions.Count);
            foreach (KeyValuePair<int, Vector3> pair in positions)
                result[pair.Key] = pair.Value + GetCircularMovementOffset(pair.Key, elapsed);

            return result;
        }

        private Vector3 GetWobbleOffset(
            int index,
            Vector3 position,
            float elapsed,
            float leadingProjection)
        {
            float phase = GetWobblePhase(index, position, leadingProjection);
            float angle = elapsed * wobbleFrequency + phase;
            return new Vector3(
                (Mathf.Sin(angle) - Mathf.Sin(phase)) * wobbleAmplitude.x,
                (Mathf.Cos(angle) - Mathf.Cos(phase)) * wobbleAmplitude.y,
                0f);
        }

        private float GetWobblePhase(
            int index,
            Vector3 position,
            float leadingProjection)
        {
            if (wobblePhaseMode != DirectedWaveWobblePhaseMode.Directional)
                return index * wobblePhaseOffset;

            Vector2 direction = GetWobbleDirection();
            float projection = Vector2.Dot(new Vector2(position.x, position.y), direction);
            return (projection - leadingProjection) / wobbleDirectionStep
                * wobblePhaseOffset;
        }

        private float GetLeadingProjection(Dictionary<int, Vector3> positions)
        {
            if (wobblePhaseMode != DirectedWaveWobblePhaseMode.Directional)
                return 0f;

            Vector2 direction = GetWobbleDirection();
            float leadingProjection = float.PositiveInfinity;
            foreach (Vector3 position in positions.Values)
            {
                float projection = Vector2.Dot(new Vector2(position.x, position.y), direction);
                if (projection < leadingProjection)
                    leadingProjection = projection;
            }

            return float.IsPositiveInfinity(leadingProjection)
                ? 0f
                : leadingProjection;
        }

        private static Dictionary<int, Vector3> OffsetPositions(
            Dictionary<int, Vector3> positions,
            Vector3 offset)
        {
            Dictionary<int, Vector3> result = new(positions.Count);
            foreach (KeyValuePair<int, Vector3> pair in positions)
                result[pair.Key] = pair.Value + offset;

            return result;
        }

        private static Dictionary<int, Vector3> RotatePositions(
            Dictionary<int, Vector3> positions,
            Vector3 center,
            float angleDegrees)
        {
            float radians = angleDegrees * Mathf.Deg2Rad;
            float sin = Mathf.Sin(radians);
            float cos = Mathf.Cos(radians);
            Dictionary<int, Vector3> result = new(positions.Count);
            foreach (KeyValuePair<int, Vector3> pair in positions)
            {
                Vector3 relative = pair.Value - center;
                result[pair.Key] = center + new Vector3(
                    relative.x * cos - relative.y * sin,
                    relative.x * sin + relative.y * cos,
                    relative.z);
            }

            return result;
        }

        private static Dictionary<int, Vector3> LerpPositions(
            Dictionary<int, Vector3> from,
            Dictionary<int, Vector3> to,
            float time)
        {
            Dictionary<int, Vector3> result = new(from.Count);
            foreach (KeyValuePair<int, Vector3> pair in from)
            {
                Vector3 target = to.TryGetValue(pair.Key, out Vector3 value)
                    ? value
                    : pair.Value;
                result[pair.Key] = Vector3.LerpUnclamped(pair.Value, target, time);
            }

            return result;
        }

        private static void ReplacePositions(
            Dictionary<int, Vector3> target,
            Dictionary<int, Vector3> source)
        {
            target.Clear();
            foreach (KeyValuePair<int, Vector3> pair in source)
                target[pair.Key] = pair.Value;
        }

        private static Vector3 GetPositionsCenter(Dictionary<int, Vector3> positions)
        {
            if (positions == null || positions.Count == 0)
                return Vector3.zero;

            Vector3 center = Vector3.zero;
            foreach (Vector3 position in positions.Values)
                center += position;

            return center / positions.Count;
        }

        private Vector3 GetFormationRotationOffset(
            Vector3 formation,
            float postTime)
        {
            Vector3 center = GetFormationCenter();
            Vector3 relative = formation - center;
            float angle = postTime
                * formationRotationDegreesPerSecond
                * Mathf.Deg2Rad;
            float sin = Mathf.Sin(angle);
            float cos = Mathf.Cos(angle);
            Vector3 rotated = new Vector3(
                relative.x * cos - relative.y * sin,
                relative.x * sin + relative.y * cos,
                relative.z);

            return rotated - relative;
        }

        private Vector3 GetFormationCenter()
        {
            int count = GetEnemyCount();
            if (count <= 0)
                return ToWorld(Vector3.zero, DirectedWaveCoordinateSpace.LocalToSubWave);

            Vector3 center = Vector3.zero;
            for (int i = 0; i < count; i++)
                center += GetFormationPosition(i);

            return center / count;
        }

        private Vector3 GetCircularMovementOffset(int index, float postTime)
        {
            float phase = index * selfOrbitPhaseOffset;
            float angle = postTime
                * selfRotationDegreesPerSecond
                * Mathf.Deg2Rad
                + phase;

            return new Vector3(
                (Mathf.Cos(angle) - Mathf.Cos(phase)) * selfOrbitRadius.x,
                (Mathf.Sin(angle) - Mathf.Sin(phase)) * selfOrbitRadius.y,
                0f);
        }

        private Vector3 GetLocalMovementOffset(float postTime)
        {
            float normalized = postTime / localMovementDuration;

            if (localMovementPingPong)
                normalized = Mathf.PingPong(normalized, 1f);
            else if (localMovementLoop)
                normalized = Mathf.Repeat(normalized, 1f);
            else
                normalized = Mathf.Clamp01(normalized);

            return localMovementOffset * EvaluateCurve(localMovementCurve, normalized);
        }

        private float GetWobblePhase(int index, Vector3 formation)
        {
            if (wobblePhaseMode != DirectedWaveWobblePhaseMode.Directional)
                return index * wobblePhaseOffset;

            Vector2 direction = GetWobbleDirection();
            float leadingProjection = GetLeadingProjection(direction);
            float projection = Vector2.Dot(new Vector2(formation.x, formation.y), direction);
            return (projection - leadingProjection) / wobbleDirectionStep * wobblePhaseOffset;
        }

        private float GetLeadingProjection(Vector2 direction)
        {
            int count = GetEnemyCount();
            float leadingProjection = float.PositiveInfinity;

            for (int i = 0; i < count; i++)
            {
                Vector3 position = GetFormationPosition(i);
                float projection = Vector2.Dot(new Vector2(position.x, position.y), direction);
                if (projection < leadingProjection)
                    leadingProjection = projection;
            }

            return float.IsPositiveInfinity(leadingProjection) ? 0f : leadingProjection;
        }

        private Vector2 GetWobbleDirection()
        {
            float radians = wobbleDirectionAngle * Mathf.Deg2Rad;
            Vector2 direction = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
            return direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;
        }

        private Vector3 GetFormationPosition(int index)
        {
            Vector3 local = formationLayout switch
            {
                DirectedWaveFormationLayout.VerticalLine => GetVerticalLinePosition(index),
                DirectedWaveFormationLayout.Grid => GetGridPosition(index),
                DirectedWaveFormationLayout.VShape => GetVShapePosition(index),
                DirectedWaveFormationLayout.Arc => GetArcPosition(index),
                DirectedWaveFormationLayout.Circle => GetCirclePosition(index),
                DirectedWaveFormationLayout.Triangle =>
                    GetPolygonPerimeterPosition(index, GetTriangleVertices()),
                DirectedWaveFormationLayout.Square =>
                    GetPolygonPerimeterPosition(index, GetSquareVertices()),
                DirectedWaveFormationLayout.Diamond =>
                    GetPolygonPerimeterPosition(index, GetDiamondVertices()),
                DirectedWaveFormationLayout.CustomPoints => GetCustomPosition(index),
                DirectedWaveFormationLayout.TransformPoints => GetTransformPointPosition(index),
                _ => GetHorizontalLinePosition(index)
            };

            if (formationLayout == DirectedWaveFormationLayout.TransformPoints)
                return local;

            return ToWorld(local, formationCoordinateSpace);
        }

        private Vector3 GetHorizontalLinePosition(int index)
        {
            int count = Mathf.Max(1, GetEnemyCount());
            float offset = (count - 1) * spacing.x * 0.5f;
            return formationCenter + new Vector3(index * spacing.x - offset, 0f, 0f);
        }

        private Vector3 GetVerticalLinePosition(int index)
        {
            int count = Mathf.Max(1, GetEnemyCount());
            float offset = (count - 1) * spacing.y * 0.5f;
            return formationCenter + new Vector3(0f, offset - index * spacing.y, 0f);
        }

        private Vector3 GetGridPosition(int index)
        {
            int column = index % columns;
            int row = Mathf.Min(index / columns, rows - 1);
            int usedRows = Mathf.Min(rows, Mathf.CeilToInt(GetEnemyCount() / (float)columns));
            float xOffset = (columns - 1) * spacing.x * 0.5f;
            float yOffset = (usedRows - 1) * spacing.y * 0.5f;
            return formationCenter
                + new Vector3(column * spacing.x - xOffset, yOffset - row * spacing.y, 0f);
        }

        private Vector3 GetVShapePosition(int index)
        {
            if (index == 0)
                return formationCenter;

            int pairIndex = (index + 1) / 2;
            float side = index % 2 == 0 ? 1f : -1f;
            return formationCenter
                + new Vector3(side * pairIndex * spacing.x, -pairIndex * spacing.y, 0f);
        }

        private Vector3 GetArcPosition(int index)
        {
            int count = Mathf.Max(1, GetEnemyCount());
            if (count <= 1)
                return formationCenter + Vector3.up * arcRadius;

            float halfArc = arcDegrees * 0.5f;
            float angle = Mathf.Lerp(-halfArc, halfArc, index / (count - 1f));
            float radians = (90f + angle) * Mathf.Deg2Rad;
            return formationCenter
                + new Vector3(Mathf.Cos(radians) * arcRadius, Mathf.Sin(radians) * arcRadius, 0f);
        }

        private Vector3 GetCirclePosition(int index)
        {
            int count = Mathf.Max(1, GetEnemyCount());
            if (count <= 1)
                return formationCenter;

            float angle = 90f - 360f * index / count;
            float radians = angle * Mathf.Deg2Rad;
            return formationCenter
                + new Vector3(
                    Mathf.Cos(radians) * shapeRadius * shapeFlattening.x,
                    Mathf.Sin(radians) * shapeRadius * shapeFlattening.y,
                    0f);
        }

        private Vector3 GetPolygonPerimeterPosition(
            int index,
            Vector3[] vertices)
        {
            int count = Mathf.Max(1, GetEnemyCount());
            if (count <= 1 || vertices == null || vertices.Length == 0)
                return formationCenter;

            float totalLength = 0f;
            for (int i = 0; i < vertices.Length; i++)
                totalLength += Vector3.Distance(
                    vertices[i],
                    vertices[(i + 1) % vertices.Length]);

            if (totalLength <= 0.0001f)
                return vertices[0];

            float remaining = totalLength * index / count;
            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 from = vertices[i];
                Vector3 to = vertices[(i + 1) % vertices.Length];
                float edgeLength = Vector3.Distance(from, to);

                if (remaining <= edgeLength)
                {
                    float time = edgeLength <= 0.0001f
                        ? 0f
                        : remaining / edgeLength;
                    return Vector3.LerpUnclamped(from, to, time);
                }

                remaining -= edgeLength;
            }

            return vertices[0];
        }

        private Vector3[] GetTriangleVertices()
        {
            return new[]
            {
                GetShapePoint(90f),
                GetShapePoint(210f),
                GetShapePoint(330f)
            };
        }

        private Vector3[] GetSquareVertices()
        {
            float x = shapeRadius * shapeFlattening.x;
            float y = shapeRadius * shapeFlattening.y;
            return new[]
            {
                formationCenter + new Vector3(-x, y, 0f),
                formationCenter + new Vector3(x, y, 0f),
                formationCenter + new Vector3(x, -y, 0f),
                formationCenter + new Vector3(-x, -y, 0f)
            };
        }

        private Vector3[] GetDiamondVertices()
        {
            return new[]
            {
                formationCenter + Vector3.up * shapeRadius * shapeFlattening.y,
                formationCenter + Vector3.right * shapeRadius * shapeFlattening.x,
                formationCenter + Vector3.down * shapeRadius * shapeFlattening.y,
                formationCenter + Vector3.left * shapeRadius * shapeFlattening.x
            };
        }

        private Vector3 GetShapePoint(float angleDegrees)
        {
            float radians = angleDegrees * Mathf.Deg2Rad;
            return formationCenter
                + new Vector3(
                    Mathf.Cos(radians) * shapeRadius * shapeFlattening.x,
                    Mathf.Sin(radians) * shapeRadius * shapeFlattening.y,
                    0f);
        }

        private bool UsesShapeFormation()
        {
            return formationLayout == DirectedWaveFormationLayout.Circle
                || formationLayout == DirectedWaveFormationLayout.Triangle
                || formationLayout == DirectedWaveFormationLayout.Square
                || formationLayout == DirectedWaveFormationLayout.Diamond;
        }

        private static Vector2 GetSafeFlattening(Vector2 value)
        {
            return new Vector2(
                Mathf.Max(0.01f, value.x),
                Mathf.Max(0.01f, value.y));
        }

        private Vector3 GetCustomPosition(int index)
        {
            if (customFormationPoints == null || customFormationPoints.arraySize == 0)
                return GetHorizontalLinePosition(index);

            int safeIndex = Mathf.Clamp(index, 0, customFormationPoints.arraySize - 1);
            return customFormationPoints.GetArrayElementAtIndex(safeIndex).vector3Value;
        }

        private Vector3 GetTransformPointPosition(int index)
        {
            Transform root = formationPointsRoot.objectReferenceValue as Transform;
            if (root == null || root.childCount == 0)
                return ToWorld(GetHorizontalLinePosition(index), formationCoordinateSpace);

            int safeIndex = Mathf.Clamp(index, 0, root.childCount - 1);
            return root.GetChild(safeIndex).position;
        }

        private Vector3 GetPatrolOffset(float postTime)
        {
            if (patrolPoints == null || patrolPoints.arraySize == 0)
                return Vector3.zero;

            if (patrolPoints.arraySize == 1)
                return GetPatrolPointOffset(0);

            float totalDuration = GetPatrolPathDuration();
            if (totalDuration <= 0f)
                return GetPatrolPointOffset(0);

            float remaining = Mathf.Max(0f, postTime);
            if (patrolLoop)
                remaining = Mathf.Repeat(remaining, totalDuration);
            else if (remaining >= totalDuration)
                return GetPatrolPointOffset(patrolPoints.arraySize - 1);

            int lastSegment = patrolLoop
                ? patrolPoints.arraySize - 1
                : patrolPoints.arraySize - 2;

            for (int i = 0; i <= lastSegment; i++)
            {
                SerializedProperty point = patrolPoints.GetArrayElementAtIndex(i);
                float duration = Mathf.Max(
                    0.01f,
                    point.FindPropertyRelative("durationToNext").floatValue);

                if (remaining <= duration)
                {
                    AnimationCurve curve = point
                        .FindPropertyRelative("easeToNext")
                        .animationCurveValue;
                    float time = EvaluateCurve(
                        curve,
                        Mathf.Clamp01(remaining / duration));
                    return EvaluatePatrolSegment(i, time);
                }

                remaining -= duration;
            }

            return patrolLoop
                ? GetPatrolPointOffset(0)
                : GetPatrolPointOffset(patrolPoints.arraySize - 1);
        }

        private float GetPatrolPathDuration()
        {
            if (patrolPoints == null || patrolPoints.arraySize < 2)
                return 0f;

            int lastSegment = patrolLoop
                ? patrolPoints.arraySize - 1
                : patrolPoints.arraySize - 2;
            float duration = 0f;

            for (int i = 0; i <= lastSegment; i++)
            {
                duration += Mathf.Max(
                    0.01f,
                    patrolPoints.GetArrayElementAtIndex(i)
                        .FindPropertyRelative("durationToNext")
                        .floatValue);
            }

            return duration;
        }

        private Vector3 EvaluatePatrolSegment(int index, float time)
        {
            DirectedWaveSegmentMotion motion =
                (DirectedWaveSegmentMotion)patrolPoints
                    .GetArrayElementAtIndex(index)
                    .FindPropertyRelative("motionToNext")
                    .enumValueIndex;

            return motion switch
            {
                DirectedWaveSegmentMotion.Bezier =>
                    EvaluatePatrolBezierSegment(index, time),
                DirectedWaveSegmentMotion.CatmullRom =>
                    EvaluatePatrolCatmullRomSegment(index, time),
                _ => Vector3.LerpUnclamped(
                    GetPatrolPointOffset(index),
                    GetPatrolPointOffset(GetNextPatrolPointIndex(index)),
                    time)
            };
        }

        private Vector3 EvaluatePatrolBezierSegment(int index, float time)
        {
            Vector3 p0 = GetPatrolPointOffset(index);
            Vector3 p3 = GetPatrolPointOffset(GetNextPatrolPointIndex(index));
            Vector3 previous = GetPatrolPointOffset(GetPreviousPatrolPointIndex(index));
            Vector3 following = GetPatrolPointOffset(
                GetNextPatrolPointIndex(GetNextPatrolPointIndex(index)));

            Vector3 p1 = p0 + (p3 - previous) / 6f;
            Vector3 p2 = p3 - (following - p0) / 6f;
            float t = Mathf.Clamp01(time);
            float oneMinusT = 1f - t;

            return oneMinusT * oneMinusT * oneMinusT * p0
                + 3f * oneMinusT * oneMinusT * t * p1
                + 3f * oneMinusT * t * t * p2
                + t * t * t * p3;
        }

        private Vector3 EvaluatePatrolCatmullRomSegment(int index, float time)
        {
            int p1 = index;
            int p0 = GetPreviousPatrolPointIndex(p1);
            int p2 = GetNextPatrolPointIndex(p1);
            int p3 = GetNextPatrolPointIndex(p2);
            float t = Mathf.Clamp01(time);

            return 0.5f * (
                2f * GetPatrolPointOffset(p1)
                + (-GetPatrolPointOffset(p0) + GetPatrolPointOffset(p2)) * t
                + (2f * GetPatrolPointOffset(p0) - 5f * GetPatrolPointOffset(p1)
                    + 4f * GetPatrolPointOffset(p2) - GetPatrolPointOffset(p3))
                * t * t
                + (-GetPatrolPointOffset(p0) + 3f * GetPatrolPointOffset(p1)
                    - 3f * GetPatrolPointOffset(p2) + GetPatrolPointOffset(p3))
                * t * t * t);
        }

        private int GetPreviousPatrolPointIndex(int index)
        {
            if (patrolPoints == null || patrolPoints.arraySize == 0)
                return 0;

            if (patrolLoop)
                return (index - 1 + patrolPoints.arraySize) % patrolPoints.arraySize;

            return Mathf.Max(0, index - 1);
        }

        private int GetNextPatrolPointIndex(int index)
        {
            if (patrolPoints == null || patrolPoints.arraySize == 0)
                return 0;

            if (patrolLoop)
                return (index + 1) % patrolPoints.arraySize;

            return Mathf.Min(patrolPoints.arraySize - 1, index + 1);
        }

        private Vector3 GetPatrolPointOffset(int index)
        {
            if (patrolPoints == null || patrolPoints.arraySize == 0)
                return Vector3.zero;

            int safeIndex = Mathf.Clamp(index, 0, patrolPoints.arraySize - 1);
            return patrolPoints.GetArrayElementAtIndex(safeIndex)
                .FindPropertyRelative("offset")
                .vector3Value;
        }

        private Vector3 EvaluateCheckpointPath(float elapsed)
        {
            if (pathCheckpoints == null || pathCheckpoints.arraySize == 0)
                return Vector3.zero;

            if (pathCheckpoints.arraySize == 1)
                return GetCheckpointPosition(0);

            float remaining = Mathf.Max(0f, elapsed);
            for (int i = 0; i < pathCheckpoints.arraySize - 1; i++)
            {
                float duration = Mathf.Max(
                    0.01f,
                    pathCheckpoints.GetArrayElementAtIndex(i)
                        .FindPropertyRelative("durationToNext").floatValue);

                if (remaining <= duration)
                {
                    AnimationCurve curve = pathCheckpoints.GetArrayElementAtIndex(i)
                        .FindPropertyRelative("easeToNext").animationCurveValue;
                    float time = EvaluateCurve(curve, Mathf.Clamp01(remaining / duration));
                    return EvaluateCheckpointSegment(i, time);
                }

                remaining -= duration;
            }

            return GetCheckpointPosition(pathCheckpoints.arraySize - 1);
        }

        private Vector3 EvaluateCheckpointSegment(int index, float time)
        {
            DirectedWaveSegmentMotion motion = (DirectedWaveSegmentMotion)pathCheckpoints
                .GetArrayElementAtIndex(index)
                .FindPropertyRelative("motionToNext").enumValueIndex;

            return motion switch
            {
                DirectedWaveSegmentMotion.Bezier => EvaluateBezierSegment(index, time),
                DirectedWaveSegmentMotion.CatmullRom => EvaluateCatmullRomSegment(index, time),
                _ => Vector3.LerpUnclamped(GetCheckpointPosition(index), GetCheckpointPosition(index + 1), time)
            };
        }

        private Vector3 EvaluateBezierSegment(int index, float time)
        {
            Vector3 p0 = GetCheckpointPosition(index);
            Vector3 p3 = GetCheckpointPosition(index + 1);
            Vector3 previous = index > 0 ? GetCheckpointPosition(index - 1) : p0;
            Vector3 following = index + 2 < pathCheckpoints.arraySize
                ? GetCheckpointPosition(index + 2)
                : p3;

            Vector3 p1 = p0 + (p3 - previous) / 6f;
            Vector3 p2 = p3 - (following - p0) / 6f;
            float t = Mathf.Clamp01(time);
            float oneMinusT = 1f - t;

            return oneMinusT * oneMinusT * oneMinusT * p0
                + 3f * oneMinusT * oneMinusT * t * p1
                + 3f * oneMinusT * t * t * p2
                + t * t * t * p3;
        }

        private Vector3 EvaluateCatmullRomSegment(int index, float time)
        {
            int p1 = index;
            int p0 = Mathf.Max(p1 - 1, 0);
            int p2 = Mathf.Min(p1 + 1, pathCheckpoints.arraySize - 1);
            int p3 = Mathf.Min(p1 + 2, pathCheckpoints.arraySize - 1);
            float t = Mathf.Clamp01(time);

            return 0.5f * (
                2f * GetCheckpointPosition(p1)
                + (-GetCheckpointPosition(p0) + GetCheckpointPosition(p2)) * t
                + (2f * GetCheckpointPosition(p0) - 5f * GetCheckpointPosition(p1)
                    + 4f * GetCheckpointPosition(p2) - GetCheckpointPosition(p3))
                * t * t
                + (-GetCheckpointPosition(p0) + 3f * GetCheckpointPosition(p1)
                    - 3f * GetCheckpointPosition(p2) + GetCheckpointPosition(p3))
                * t * t * t);
        }

        private Vector3 GetCheckpointPosition(int index)
        {
            Vector3 local = pathCheckpoints.GetArrayElementAtIndex(index)
                .FindPropertyRelative("position").vector3Value;
            return ToWorld(local, pathCoordinateSpace);
        }

        private Vector3 ToWorld(Vector3 position, DirectedWaveCoordinateSpace space)
        {
            Transform root = waveTransform != null ? waveTransform : prefabTransform;
            Vector3 subWaveOrigin = root != null
                ? root.TransformPoint(prefabTransform.localPosition)
                : prefabTransform.position;

            return space switch
            {
                DirectedWaveCoordinateSpace.LocalToSubWave => subWaveOrigin + position,
                _ => position
            };
        }

        private static float EvaluateCurve(AnimationCurve curve, float time)
        {
            return curve != null ? curve.Evaluate(time) : time;
        }
    }
}
