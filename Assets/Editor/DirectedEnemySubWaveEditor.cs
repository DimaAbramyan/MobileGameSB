using System.Collections.Generic;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

[CustomEditor(typeof(DirectedEnemySubWave))]
public sealed class DirectedEnemySubWaveEditor : Editor
{
    private const string ShowMobileBoundsKey =
        "DirectedEnemySubWaveEditor.ShowMobileBounds";
    private const string MobileBoundsOrthoSizeKey =
        "DirectedEnemySubWaveEditor.MobileBoundsOrthoSize";
    private const string MobileBoundsAspectKey =
        "DirectedEnemySubWaveEditor.MobileBoundsAspect";
    private const string MobileBoundsCenterXKey =
        "DirectedEnemySubWaveEditor.MobileBoundsCenterX";
    private const string MobileBoundsCenterYKey =
        "DirectedEnemySubWaveEditor.MobileBoundsCenterY";
    private const string FinalPointFoldoutPrefix =
        "DirectedEnemySubWaveEditor.FinalPointFoldout";
    private const float PostBehaviorPreviewDuration = 4f;
    private const float InfiniteParallelPreviewExtraDuration = 60f;
    private const int MaxPreviewLoopIterationsPerRepaint = 256;

    private SerializedProperty enemyPrefab;
    private SerializedProperty enemyCount;
    private SerializedProperty spawnInterval;
    private SerializedProperty spawnOrderMode;
    private SerializedProperty spawnOrderAngle;
    private SerializedProperty spawnOrderStartAngle;
    private SerializedProperty spawnPoint;
    private SerializedProperty parentEnemiesToSubWave;

    private SerializedProperty pathCoordinateSpace;
    private SerializedProperty pathCheckpoints;

    private SerializedProperty formationLayout;
    private SerializedProperty formationFrozen;
    private SerializedProperty formationCoordinateSpace;
    private SerializedProperty formationCenter;
    private SerializedProperty spacing;
    private SerializedProperty columns;
    private SerializedProperty rows;
    private SerializedProperty gridMatrixCells;
    private SerializedProperty arcRadius;
    private SerializedProperty arcDegrees;
    private SerializedProperty shapePointCount;
    private SerializedProperty shapeRadius;
    private SerializedProperty shapeFlattening;
    private SerializedProperty customFormationPoints;
    private SerializedProperty customFormationEnemyOverrides;
    private SerializedProperty formationPointsRoot;
    private SerializedProperty settleDuration;
    private SerializedProperty settleCurve;

    private SerializedProperty postCommands;
    private SerializedProperty postStartDelay;
    private SerializedProperty postCommandPipelineLoop;
    private SerializedProperty localMovementOffset;
    private SerializedProperty localMovementDuration;
    private SerializedProperty localMovementLoop;
    private SerializedProperty localMovementPingPong;
    private SerializedProperty localMovementCurve;
    private SerializedProperty wobbleAmplitude;
    private SerializedProperty wobbleFrequency;
    private SerializedProperty wobblePhaseMode;
    private SerializedProperty wobblePhaseOffset;
    private SerializedProperty wobbleDirectionAngle;
    private SerializedProperty wobbleDirectionStep;
    private SerializedProperty diveInterval;
    private SerializedProperty diveDuration;
    private SerializedProperty diveReturnDuration;
    private SerializedProperty diveOvershootDistance;
    private SerializedProperty diveCurve;
    private SerializedProperty diveReturnCurve;
    private SerializedProperty patrolLoop;
    private SerializedProperty patrolPoints;
    private SerializedProperty selfOrbitRadius;
    private SerializedProperty selfOrbitPhaseOffset;
    private SerializedProperty selfRotationDegreesPerSecond;
    private SerializedProperty formationRotationDegreesPerSecond;
    private SerializedProperty formationMorphLoop;
    private SerializedProperty formationMorphReturnDuration;
    private SerializedProperty formationMorphReturnCurve;
    private SerializedProperty formationMorphSteps;

    private bool spawnFoldout = true;
    private bool spawnOrderFoldout = true;
    private bool pathFoldout = true;
    private bool formationFoldout = true;
    private bool gridMatrixFoldout = true;
    private bool customFinalPointsFoldout = true;
    private bool transformFinalPointsFoldout = true;
    private bool postBehaviorFoldout = true;
    private bool previewFoldout = true;
    private bool previewPlaying;
    private double previewStartTime;
    private bool showMobileBounds;
    private float mobileBoundsOrthoSize;
    private float mobileBoundsAspect;
    private Vector2 mobileBoundsCenter;
    private readonly System.Collections.Generic.List<CustomFinalPointOrderEntry>
        customFinalPointOrder = new();
    private readonly System.Collections.Generic.List<Transform>
        transformFinalPointOrder = new();
    private ReorderableList customFinalPointOrderList;
    private ReorderableList transformFinalPointOrderList;
    private Transform transformFinalPointOrderRoot;
    private int activePathCheckpointIndex = -1;
    private int activePatrolPointIndex = -1;
    private int activeCustomFormationPointIndex = -1;
    private int activePostCommandIndex = -1;
    private Transform activeTransformFormationPoint;

    private void OnEnable()
    {
        enemyPrefab = serializedObject.FindProperty("enemyPrefab");
        enemyCount = serializedObject.FindProperty("enemyCount");
        spawnInterval = serializedObject.FindProperty("spawnInterval");
        spawnOrderMode = serializedObject.FindProperty("spawnOrderMode");
        spawnOrderAngle = serializedObject.FindProperty("spawnOrderAngle");
        spawnOrderStartAngle =
            serializedObject.FindProperty("spawnOrderStartAngle");
        spawnPoint = serializedObject.FindProperty("spawnPoint");
        parentEnemiesToSubWave =
            serializedObject.FindProperty("parentEnemiesToSubWave");

        pathCoordinateSpace =
            serializedObject.FindProperty("pathCoordinateSpace");
        pathCheckpoints = serializedObject.FindProperty("pathCheckpoints");

        formationLayout = serializedObject.FindProperty("formationLayout");
        formationFrozen = serializedObject.FindProperty("formationFrozen");
        formationCoordinateSpace =
            serializedObject.FindProperty("formationCoordinateSpace");
        formationCenter = serializedObject.FindProperty("formationCenter");
        spacing = serializedObject.FindProperty("spacing");
        columns = serializedObject.FindProperty("columns");
        rows = serializedObject.FindProperty("rows");
        gridMatrixCells = serializedObject.FindProperty("gridMatrixCells");
        arcRadius = serializedObject.FindProperty("arcRadius");
        arcDegrees = serializedObject.FindProperty("arcDegrees");
        shapePointCount = serializedObject.FindProperty("shapePointCount");
        shapeRadius = serializedObject.FindProperty("shapeRadius");
        shapeFlattening = serializedObject.FindProperty("shapeFlattening");
        customFormationPoints =
            serializedObject.FindProperty("customFormationPoints");
        customFormationEnemyOverrides =
            serializedObject.FindProperty("customFormationEnemyOverrides");
        formationPointsRoot =
            serializedObject.FindProperty("formationPointsRoot");
        settleDuration = serializedObject.FindProperty("settleDuration");
        settleCurve = serializedObject.FindProperty("settleCurve");

        postCommands = serializedObject.FindProperty("postCommands");
        postStartDelay = serializedObject.FindProperty("postStartDelay");
        postCommandPipelineLoop =
            serializedObject.FindProperty("postCommandPipelineLoop");
        localMovementOffset =
            serializedObject.FindProperty("localMovementOffset");
        localMovementDuration =
            serializedObject.FindProperty("localMovementDuration");
        localMovementLoop =
            serializedObject.FindProperty("localMovementLoop");
        localMovementPingPong =
            serializedObject.FindProperty("localMovementPingPong");
        localMovementCurve =
            serializedObject.FindProperty("localMovementCurve");
        wobbleAmplitude = serializedObject.FindProperty("wobbleAmplitude");
        wobbleFrequency = serializedObject.FindProperty("wobbleFrequency");
        wobblePhaseMode = serializedObject.FindProperty("wobblePhaseMode");
        wobblePhaseOffset = serializedObject.FindProperty("wobblePhaseOffset");
        wobbleDirectionAngle = serializedObject.FindProperty("wobbleDirectionAngle");
        wobbleDirectionStep = serializedObject.FindProperty("wobbleDirectionStep");
        diveInterval = serializedObject.FindProperty("diveInterval");
        diveDuration = serializedObject.FindProperty("diveDuration");
        diveReturnDuration = serializedObject.FindProperty("diveReturnDuration");
        diveOvershootDistance =
            serializedObject.FindProperty("diveOvershootDistance");
        diveCurve = serializedObject.FindProperty("diveCurve");
        diveReturnCurve = serializedObject.FindProperty("diveReturnCurve");
        patrolLoop = serializedObject.FindProperty("patrolLoop");
        patrolPoints = serializedObject.FindProperty("patrolPoints");
        selfOrbitRadius = serializedObject.FindProperty("selfOrbitRadius");
        selfOrbitPhaseOffset =
            serializedObject.FindProperty("selfOrbitPhaseOffset");
        selfRotationDegreesPerSecond =
            serializedObject.FindProperty("selfRotationDegreesPerSecond");
        formationRotationDegreesPerSecond =
            serializedObject.FindProperty("formationRotationDegreesPerSecond");
        formationMorphLoop = serializedObject.FindProperty("formationMorphLoop");
        formationMorphReturnDuration =
            serializedObject.FindProperty("formationMorphReturnDuration");
        formationMorphReturnCurve =
            serializedObject.FindProperty("formationMorphReturnCurve");
        formationMorphSteps = serializedObject.FindProperty("formationMorphSteps");

        showMobileBounds = EditorPrefs.GetBool(ShowMobileBoundsKey, true);
        mobileBoundsOrthoSize = EditorPrefs.GetFloat(
            MobileBoundsOrthoSizeKey,
            5f);
        mobileBoundsAspect = EditorPrefs.GetFloat(
            MobileBoundsAspectKey,
            9f / 16f);
        mobileBoundsCenter = new Vector2(
            EditorPrefs.GetFloat(MobileBoundsCenterXKey, 0f),
            EditorPrefs.GetFloat(MobileBoundsCenterYKey, 0f));
    }

    private void OnDisable()
    {
        StopPreview();
        SceneView.duringSceneGui -= DrawPreviewDuringSceneGui;
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        DrawIntro();
        DrawSpawn();
        DrawPath();
        DrawFormation();
        DrawPostBehavior();
        DrawPreviewHelp();

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawIntro()
    {
        EditorGUILayout.HelpBox(
            "Directed Enemy Sub Wave creates enemies with interval, moves them "
            + "through an entrance path, then places them into a formation.",
            MessageType.Info);
    }

    private void DrawSpawn()
    {
        spawnFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(
            spawnFoldout,
            "1. Spawn");

        if (spawnFoldout)
        {
            EditorGUILayout.PropertyField(enemyPrefab);
            EditorGUILayout.PropertyField(enemyCount);
            EditorGUILayout.PropertyField(spawnInterval);
            DrawSpawnOrderSettings();
            EditorGUILayout.PropertyField(spawnPoint);
            EditorGUILayout.PropertyField(parentEnemiesToSubWave);

            if (IsTransformPointsFormation())
            {
                EditorGUILayout.HelpBox(
                    $"Free formation uses Final Points count as enemy count. Current spawn count: {GetEditorEffectiveEnemyCount()}. Empty slot overrides use the global Enemy Prefab.",
                    MessageType.Info);

                DrawTransformPointsSpawnWarnings();
            }

            if (enemyPrefab.objectReferenceValue == null
                && !HasEditorPointEnemyOverride())
            {
                EditorGUILayout.HelpBox(
                    "Set global Enemy Prefab or assign Enemy Override on at least one final point.",
                    MessageType.Warning);
            }
        }

        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    private void DrawSpawnOrderSettings()
    {
        EditorGUILayout.Space(4f);
        spawnOrderFoldout = EditorGUILayout.Foldout(
            spawnOrderFoldout,
            GetSpawnOrderFoldoutLabel(),
            true);

        if (!spawnOrderFoldout)
            return;

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.PropertyField(spawnOrderMode);

            DirectedWaveSpawnOrderMode mode =
                (DirectedWaveSpawnOrderMode)spawnOrderMode.enumValueIndex;

            if (mode == DirectedWaveSpawnOrderMode.DirectionAngle)
            {
                EditorGUILayout.PropertyField(
                    spawnOrderAngle,
                    new GUIContent(
                        "Direction Angle",
                        "0 = left to right, 90 = bottom to top, 180 = right to left, 270 = top to bottom."));
            }

            if (mode == DirectedWaveSpawnOrderMode.Clockwise
                || mode == DirectedWaveSpawnOrderMode.CounterClockwise)
            {
                EditorGUILayout.PropertyField(
                    spawnOrderStartAngle,
                    new GUIContent(
                        "Start Angle",
                        "Angle where circular spawn ordering starts. 0 = right, 90 = top, 180 = left, 270 = bottom."));
            }

            EditorGUILayout.HelpBox(
                "Manual keeps the visible point order. Other modes sort final formation slots at runtime and in Preview without moving the points themselves.",
                MessageType.None);

            using (new EditorGUI.DisabledScope(mode == DirectedWaveSpawnOrderMode.Manual
                || GetEditorEffectiveEnemyCount() <= 1))
            {
                if (GUILayout.Button("Rebuild Points From Spawn Order"))
                    RebuildPointsFromCurrentSpawnOrder();
            }
        }
    }

    private string GetSpawnOrderFoldoutLabel()
    {
        DirectedWaveSpawnOrderMode mode =
            (DirectedWaveSpawnOrderMode)spawnOrderMode.enumValueIndex;

        return mode switch
        {
            DirectedWaveSpawnOrderMode.DirectionAngle =>
                $"Spawn Order: {mode} ({spawnOrderAngle.floatValue:0.#}°)",
            DirectedWaveSpawnOrderMode.Clockwise
                or DirectedWaveSpawnOrderMode.CounterClockwise =>
                $"Spawn Order: {mode} (Start {spawnOrderStartAngle.floatValue:0.#}°)",
            _ => $"Spawn Order: {mode}"
        };
    }

    private bool IsComputedSpawnOrderMode()
    {
        return (DirectedWaveSpawnOrderMode)spawnOrderMode.enumValueIndex
            != DirectedWaveSpawnOrderMode.Manual;
    }

    private void RebuildPointsFromCurrentSpawnOrder()
    {
        DirectedEnemySubWave wave = (DirectedEnemySubWave)target;
        int count = GetEditorEffectiveEnemyCount();
        if (count <= 1)
            return;

        int[] spawnOrder = BuildEditorSpawnOrder(wave, count);
        if (spawnOrder == null || spawnOrder.Length <= 1)
            return;

        DirectedWaveFormationLayout layout =
            (DirectedWaveFormationLayout)formationLayout.enumValueIndex;

        if (layout == DirectedWaveFormationLayout.CustomPoints
            && customFormationPoints.arraySize == spawnOrder.Length)
        {
            RebuildCustomPointsFromSpawnOrder(spawnOrder);
            ReloadCustomFinalPointOrder();
        }
        else
        {
            if (layout != DirectedWaveFormationLayout.TransformPoints
                || formationPointsRoot.objectReferenceValue == null)
            {
                ConvertCurrentFormationToTransformPoints();
                formationLayout.enumValueIndex =
                    (int)DirectedWaveFormationLayout.TransformPoints;
                serializedObject.ApplyModifiedProperties();
                serializedObject.Update();
            }

            Transform root = formationPointsRoot.objectReferenceValue as Transform;
            RebuildTransformPointsFromSpawnOrder(root, spawnOrder);
            ReloadTransformFinalPointOrder(root);
        }

        spawnOrderMode.enumValueIndex =
            (int)DirectedWaveSpawnOrderMode.Manual;

        serializedObject.ApplyModifiedProperties();
        SceneView.RepaintAll();
    }

    private void RebuildCustomPointsFromSpawnOrder(int[] spawnOrder)
    {
        EnsureCustomFormationOverrideSize();

        Vector3[] oldPoints = new Vector3[customFormationPoints.arraySize];
        UnityEngine.Object[] oldOverrides =
            new UnityEngine.Object[customFormationEnemyOverrides.arraySize];

        for (int i = 0; i < customFormationPoints.arraySize; i++)
        {
            oldPoints[i] = customFormationPoints
                .GetArrayElementAtIndex(i)
                .vector3Value;
            oldOverrides[i] = customFormationEnemyOverrides
                .GetArrayElementAtIndex(i)
                .objectReferenceValue;
        }

        Undo.RecordObject(target, "Rebuild Custom Points From Spawn Order");

        for (int i = 0; i < spawnOrder.Length; i++)
        {
            int sourceIndex = Mathf.Clamp(spawnOrder[i], 0, oldPoints.Length - 1);
            customFormationPoints
                .GetArrayElementAtIndex(i)
                .vector3Value = oldPoints[sourceIndex];
            customFormationEnemyOverrides
                .GetArrayElementAtIndex(i)
                .objectReferenceValue = sourceIndex < oldOverrides.Length
                    ? oldOverrides[sourceIndex]
                    : null;
        }
    }

    private void RebuildTransformPointsFromSpawnOrder(
        Transform root,
        int[] spawnOrder)
    {
        if (root == null || root.childCount == 0)
            return;

        Transform[] oldChildren = new Transform[root.childCount];
        for (int i = 0; i < root.childCount; i++)
            oldChildren[i] = root.GetChild(i);

        Undo.RegisterFullObjectHierarchyUndo(
            root.gameObject,
            "Rebuild Transform Points From Spawn Order");

        for (int i = 0; i < spawnOrder.Length; i++)
        {
            int sourceIndex = Mathf.Clamp(spawnOrder[i], 0, oldChildren.Length - 1);
            Transform point = oldChildren[sourceIndex];
            if (point != null && point.parent == root)
                point.SetSiblingIndex(i);
        }

        RenameTransformPointSlots(root);
        EditorUtility.SetDirty(root.gameObject);
    }

    private void DrawTransformPointsSpawnWarnings()
    {
        int slotCount = GetEditorEffectiveEnemyCount();
        int enemyCountValue = Mathf.Max(1, enemyCount.intValue);
        int configuredCount = enemyCountValue;

        if (slotCount >= configuredCount)
            return;

        int missingSlots = configuredCount - slotCount;
        EditorGUILayout.HelpBox(
            $"Not enough Formation Points for configured enemies. " +
            $"Configured: {configuredCount} " +
            $"(Enemy Count: {enemyCountValue}), " +
            $"but Formation Points Root has only {slotCount} slots. " +
            $"Only {slotCount} enemies will spawn. " +
            $"Add {missingSlots} Slot object(s) to Formation Points Root or reduce enemy counts.",
            MessageType.Warning);
    }

    private void DrawPath()
    {
        pathFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(
            pathFoldout,
            "2. Entrance Path");

        if (pathFoldout)
        {
            EditorGUILayout.PropertyField(pathCoordinateSpace);

            EditorGUILayout.Space(4f);
            DrawPathPresetButtons();

            EditorGUILayout.Space(4f);
            DrawPathCheckpoints();

            if (pathCheckpoints.arraySize < 2)
            {
                EditorGUILayout.HelpBox(
                    "Add at least 2 checkpoints. Duration/Speed/Motion/Ease are used "
                    + "on the segment from this checkpoint to the next one.",
                    MessageType.Warning);
            }
        }

        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    private void DrawPathCheckpoints()
    {
        EditorGUILayout.LabelField("Path Checkpoints", EditorStyles.boldLabel);
        EnsurePathCheckpointSpeedsInitialized();

        using (new EditorGUILayout.HorizontalScope())
        {
            int newSize = Mathf.Max(
                0,
                EditorGUILayout.IntField("Size", pathCheckpoints.arraySize));

            if (newSize != pathCheckpoints.arraySize)
                ResizePathCheckpoints(newSize);
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Add Checkpoint"))
                AddPathCheckpoint();

            GUI.enabled = pathCheckpoints.arraySize > 0;
            if (GUILayout.Button("Remove Last"))
                pathCheckpoints.DeleteArrayElementAtIndex(pathCheckpoints.arraySize - 1);

            GUI.enabled = true;
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            GUI.enabled = pathCheckpoints.arraySize > 0;

            if (GUILayout.Button("Expand All"))
                SetAllPathCheckpointsExpanded(true);

            if (GUILayout.Button("Collapse All"))
                SetAllPathCheckpointsExpanded(false);

            GUI.enabled = true;
        }

        EditorGUILayout.Space(4f);

        for (int i = 0; i < pathCheckpoints.arraySize; i++)
        {
            SerializedProperty checkpoint =
                pathCheckpoints.GetArrayElementAtIndex(i);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                SerializedProperty position =
                    checkpoint.FindPropertyRelative("position");
                string title = $"Checkpoint {i}  {position.vector3Value}";
                checkpoint.isExpanded = EditorGUILayout.Foldout(
                    checkpoint.isExpanded,
                    title,
                    true,
                    EditorStyles.foldoutHeader);

                if (!checkpoint.isExpanded)
                    continue;

                SerializedProperty durationToNext =
                    checkpoint.FindPropertyRelative("durationToNext");
                SerializedProperty speedToNext =
                    checkpoint.FindPropertyRelative("speedToNext");
                SerializedProperty motionToNext =
                    checkpoint.FindPropertyRelative("motionToNext");
                SerializedProperty easeToNext =
                    checkpoint.FindPropertyRelative("easeToNext");

                EditorGUI.BeginChangeCheck();
                EditorGUILayout.PropertyField(position, new GUIContent("Position"));
                bool positionChanged = EditorGUI.EndChangeCheck();

                if (i < pathCheckpoints.arraySize - 1)
                {
                    EditorGUI.BeginChangeCheck();
                    EditorGUILayout.PropertyField(
                        durationToNext,
                        new GUIContent("Duration To Next"));
                    bool durationChanged = EditorGUI.EndChangeCheck();

                    EditorGUI.BeginChangeCheck();
                    EditorGUILayout.PropertyField(
                        speedToNext,
                        new GUIContent("Speed To Next"));
                    bool speedChanged = EditorGUI.EndChangeCheck();

                    EditorGUI.BeginChangeCheck();
                    EditorGUILayout.PropertyField(
                        motionToNext,
                        new GUIContent("Motion To Next"));
                    EditorGUILayout.PropertyField(
                        easeToNext,
                        new GUIContent("Ease To Next"));
                    bool pathShapeChanged = EditorGUI.EndChangeCheck();

                    SyncCheckpointDurationAndSpeed(
                        i,
                        durationChanged,
                        speedChanged,
                        positionChanged || pathShapeChanged);
                }

                if (positionChanged && i > 0)
                    SyncCheckpointDurationAndSpeed(i - 1, false, false, true);

                if (i == pathCheckpoints.arraySize - 1)
                {
                    EditorGUILayout.HelpBox(
                        "Last checkpoint has no next segment, so Duration/Speed/Motion/Ease are ignored.",
                        MessageType.None);
                }
            }
        }
    }

    private void EnsurePathCheckpointSpeedsInitialized()
    {
        for (int i = 0; i < pathCheckpoints.arraySize - 1; i++)
        {
            SerializedProperty checkpoint =
                pathCheckpoints.GetArrayElementAtIndex(i);
            SerializedProperty speedToNext =
                checkpoint.FindPropertyRelative("speedToNext");
            float duration = Mathf.Max(
                0.01f,
                checkpoint.FindPropertyRelative("durationToNext").floatValue);
            speedToNext.floatValue = GetCheckpointSegmentLength(i) / duration;
        }
    }

    private void SyncCheckpointDurationAndSpeed(
        int index,
        bool durationChanged,
        bool speedChanged,
        bool pathShapeChanged)
    {
        SerializedProperty checkpoint =
            pathCheckpoints.GetArrayElementAtIndex(index);
        SerializedProperty durationToNext =
            checkpoint.FindPropertyRelative("durationToNext");
        SerializedProperty speedToNext =
            checkpoint.FindPropertyRelative("speedToNext");

        float segmentLength = GetCheckpointSegmentLength(index);

        if (speedChanged)
        {
            float speed = Mathf.Max(0.01f, speedToNext.floatValue);
            speedToNext.floatValue = speed;
            durationToNext.floatValue = Mathf.Max(0.01f, segmentLength / speed);
            return;
        }

        if (durationChanged || pathShapeChanged)
        {
            float duration = Mathf.Max(0.01f, durationToNext.floatValue);
            durationToNext.floatValue = duration;
            speedToNext.floatValue = Mathf.Max(0.01f, segmentLength / duration);
        }
    }

    private void SetAllPathCheckpointsExpanded(bool expanded)
    {
        for (int i = 0; i < pathCheckpoints.arraySize; i++)
            pathCheckpoints.GetArrayElementAtIndex(i).isExpanded = expanded;
    }

    private void ResizePathCheckpoints(int newSize)
    {
        int oldSize = pathCheckpoints.arraySize;
        pathCheckpoints.arraySize = newSize;

        for (int i = oldSize; i < newSize; i++)
            InitializePathCheckpoint(i);
    }

    private void AddPathCheckpoint()
    {
        int index = pathCheckpoints.arraySize;
        pathCheckpoints.arraySize++;
        InitializePathCheckpoint(index);
    }

    private void InitializePathCheckpoint(int index)
    {
        SerializedProperty checkpoint =
            pathCheckpoints.GetArrayElementAtIndex(index);

        Vector3 position = Vector3.zero;
        if (index > 0)
        {
            position = pathCheckpoints
                .GetArrayElementAtIndex(index - 1)
                .FindPropertyRelative("position")
                .vector3Value + Vector3.down;
        }

        checkpoint.FindPropertyRelative("position").vector3Value = position;
        checkpoint.FindPropertyRelative("durationToNext").floatValue = 0.5f;
        checkpoint.FindPropertyRelative("speedToNext").floatValue = 1f;
        checkpoint.FindPropertyRelative("motionToNext").enumValueIndex =
            (int)DirectedWaveSegmentMotion.CatmullRom;
        checkpoint.FindPropertyRelative("easeToNext").animationCurveValue =
            AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        if (index > 0)
            SyncCheckpointDurationAndSpeed(index - 1, true, false, false);
    }

    private void DrawPathPresetButtons()
    {
        EditorGUILayout.LabelField("Quick Path Presets", EditorStyles.boldLabel);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Top Left Curve"))
            {
                SetPathPoints(
                    new Vector3(-5f, 6f, 0f),
                    new Vector3(-3f, 3f, 0f),
                    new Vector3(2.5f, 4f, 0f),
                    new Vector3(0f, 2.5f, 0f));
            }

            if (GUILayout.Button("Top Right Curve"))
            {
                SetPathPoints(
                    new Vector3(5f, 6f, 0f),
                    new Vector3(3f, 3f, 0f),
                    new Vector3(-2.5f, 4f, 0f),
                    new Vector3(0f, 2.5f, 0f));
            }
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Straight From Top"))
            {
                SetPathPoints(
                    new Vector3(0f, 6f, 0f),
                    new Vector3(0f, 2.5f, 0f));
            }

            if (GUILayout.Button("Side Sweep"))
            {
                SetPathPoints(
                    new Vector3(-6f, 3.5f, 0f),
                    new Vector3(-2f, 1.5f, 0f),
                    new Vector3(2f, 3.5f, 0f),
                    new Vector3(0f, 2.5f, 0f));
            }
        }
    }

    private void DrawFormation()
    {
        formationFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(
            formationFoldout,
            "3. Formation");

        if (formationFoldout)
        {
            DrawFormationFreezeControls();

            bool frozen = formationFrozen.boolValue;
            using (new EditorGUI.DisabledScope(frozen))
            {
                DrawFormationLayoutField();
                EditorGUILayout.PropertyField(formationCoordinateSpace);
                EditorGUILayout.PropertyField(formationCenter);
                EditorGUILayout.PropertyField(spacing);

                DirectedWaveFormationLayout layout =
                    (DirectedWaveFormationLayout)formationLayout.enumValueIndex;

                switch (layout)
                {
                    case DirectedWaveFormationLayout.Grid:
                        EditorGUILayout.PropertyField(columns);
                        EditorGUILayout.PropertyField(rows);
                        DrawGridMatrixBuilder(false);
                        break;

                    case DirectedWaveFormationLayout.Arc:
                        EditorGUILayout.PropertyField(arcRadius);
                        EditorGUILayout.PropertyField(arcDegrees);
                        break;

                    case DirectedWaveFormationLayout.Circle:
                    case DirectedWaveFormationLayout.Triangle:
                    case DirectedWaveFormationLayout.Square:
                    case DirectedWaveFormationLayout.Diamond:
                        DrawShapeFormationSettings();
                        break;

                    case DirectedWaveFormationLayout.CustomPoints:
                        DrawGridMatrixBuilder(true);
                        DrawCustomFinalPoints();
                        DrawCustomPointUtility();
                        break;

                    case DirectedWaveFormationLayout.TransformPoints:
                        EditorGUILayout.PropertyField(formationPointsRoot);
                        DrawTransformPointUtility();
                        break;
                }
            }

            if (frozen)
                DrawFrozenFormationSummary();

            EditorGUILayout.PropertyField(settleDuration);
            EditorGUILayout.PropertyField(settleCurve);

            EditorGUILayout.Space(4f);
            using (new EditorGUI.DisabledScope(frozen))
                DrawFormationPresetButtons();
        }

        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    private void DrawFormationLayoutField()
    {
        DirectedWaveFormationLayout currentLayout =
            (DirectedWaveFormationLayout)formationLayout.enumValueIndex;
        DirectedWaveFormationLayout newLayout =
            (DirectedWaveFormationLayout)EditorGUILayout.EnumPopup(
                formationLayout.displayName,
                currentLayout);

        if (newLayout == currentLayout)
            return;

        if (newLayout == DirectedWaveFormationLayout.TransformPoints)
            ConvertCurrentFormationToTransformPoints();

        formationLayout.enumValueIndex = (int)newLayout;
    }

    private void DrawFormationFreezeControls()
    {
        bool frozen = formationFrozen.boolValue;

        EditorGUILayout.Space(2f);
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("Freeze / Bake", EditorStyles.boldLabel);

            if (frozen)
            {
                EditorGUILayout.HelpBox(
                    "Formation is frozen. Runtime ignores procedural layout fields and uses baked Transform Points only.",
                    MessageType.Info);

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Unfreeze Formation"))
                        UnfreezeFormation();

                    GUI.enabled = false;
                    GUILayout.Button("Freeze / Bake Current Formation");
                    GUI.enabled = true;
                }
            }
            else
            {
                EditorGUILayout.HelpBox(
                    "Freeze converts the current visible formation to Transform Points and locks it, so future formula/script changes will not move this wave.",
                    MessageType.None);

                using (new EditorGUILayout.HorizontalScope())
                {
                    GUI.enabled = false;
                    GUILayout.Button("Unfreeze Formation");
                    GUI.enabled = true;

                    if (GUILayout.Button("Freeze / Bake Current Formation"))
                        FreezeFormation();
                }
            }
        }
    }

    private void FreezeFormation()
    {
        serializedObject.Update();

        ConvertCurrentFormationToTransformPoints();
        formationLayout.enumValueIndex =
            (int)DirectedWaveFormationLayout.TransformPoints;
        formationFrozen.boolValue = true;

        serializedObject.ApplyModifiedProperties();
        SceneView.RepaintAll();
    }

    private void UnfreezeFormation()
    {
        serializedObject.Update();

        formationFrozen.boolValue = false;
        if ((DirectedWaveFormationLayout)formationLayout.enumValueIndex
            != DirectedWaveFormationLayout.TransformPoints)
        {
            formationLayout.enumValueIndex =
                (int)DirectedWaveFormationLayout.TransformPoints;
        }

        serializedObject.ApplyModifiedProperties();
        SceneView.RepaintAll();
    }

    private void DrawFrozenFormationSummary()
    {
        Transform root = formationPointsRoot.objectReferenceValue as Transform;

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("Frozen Baked Points", EditorStyles.boldLabel);

            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.PropertyField(formationPointsRoot);

            int count = root != null ? root.childCount : 0;
            EditorGUILayout.LabelField("Baked Point Count", count.ToString());

            if (root == null || root.childCount == 0)
            {
                EditorGUILayout.HelpBox(
                    "Frozen formation has no baked points. Unfreeze and freeze again to bake visible points.",
                    MessageType.Warning);
            }
            else
            {
                EditorGUILayout.HelpBox(
                    "Unfreeze if you want to move/reorder these baked points.",
                    MessageType.None);
            }
        }
    }

    private void DrawShapeFormationSettings()
    {
        EditorGUILayout.PropertyField(
            shapePointCount,
            new GUIContent(
                "Point Count",
                "How many enemies/final points this geometric formation contains."));
        EditorGUILayout.PropertyField(
            shapeRadius,
            new GUIContent(
                "Radius",
                "Distance from Formation Center to the shape before flattening."));
        EditorGUILayout.PropertyField(
            shapeFlattening,
            new GUIContent(
                "Flattening X/Y",
                "Scales the shape on X and Y. Use values below 1 to squash it."));

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Use Enemy Count"))
                shapePointCount.intValue = Mathf.Max(1, enemyCount.intValue);

            if (GUILayout.Button("Convert Shape To Free"))
            {
                ConvertCurrentFormationToTransformPoints();
                formationLayout.enumValueIndex =
                    (int)DirectedWaveFormationLayout.TransformPoints;
            }
        }
    }

    private void DrawGridMatrixBuilder(bool showDimensionFields)
    {
        EditorGUILayout.Space(4f);
        gridMatrixFoldout = EditorGUILayout.Foldout(
            gridMatrixFoldout,
            "Grid Matrix Builder",
            true);

        if (!gridMatrixFoldout)
            return;

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.HelpBox(
                "Builds editable Custom Points from Rows x Columns. Green cells spawn enemies, red cells are empty. Disabled cells do not recenter the remaining formation.",
                MessageType.None);

            if (showDimensionFields)
            {
                EditorGUILayout.PropertyField(columns);
                EditorGUILayout.PropertyField(rows);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Build Full Matrix"))
                    BuildFullGridMatrix();

                using (new EditorGUI.DisabledScope(!HasValidGridMatrix()))
                {
                    if (GUILayout.Button("Apply Matrix To Points"))
                        ApplyGridMatrixToCustomPoints(
                            CaptureGridMatrixEnemyOverrides());
                }
            }

            int requiredSize = GetGridMatrixCellCount();
            if (gridMatrixCells.arraySize != requiredSize)
            {
                EditorGUILayout.HelpBox(
                    $"Matrix is not initialized for {rows.intValue} x {columns.intValue}. Press Build Full Matrix.",
                    MessageType.Info);
                return;
            }

            DrawGridMatrixCells();
        }
    }

    private void BuildFullGridMatrix()
    {
        System.Collections.Generic.Dictionary<int, UnityEngine.Object>
            overridesByCell = CaptureGridMatrixEnemyOverrides();

        Undo.RecordObject(target, "Build Editable Grid Matrix");
        EnsureGridMatrixSize(true);

        for (int i = 0; i < gridMatrixCells.arraySize; i++)
            gridMatrixCells.GetArrayElementAtIndex(i).boolValue = true;

        ApplyGridMatrixToCustomPoints(overridesByCell);
        formationLayout.enumValueIndex =
            (int)DirectedWaveFormationLayout.CustomPoints;

        serializedObject.ApplyModifiedProperties();
        ReloadCustomFinalPointOrder();
        SceneView.RepaintAll();
    }

    private void DrawGridMatrixCells()
    {
        int safeRows = Mathf.Max(1, rows.intValue);
        int safeColumns = Mathf.Max(1, columns.intValue);
        int activeCount = GetGridMatrixEnabledCellCount();

        EditorGUILayout.LabelField(
            $"Active Enemies: {activeCount}",
            EditorStyles.miniBoldLabel);

        for (int row = 0; row < safeRows; row++)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(EditorGUI.indentLevel * 12f);

                for (int column = 0; column < safeColumns; column++)
                {
                    int cellIndex = row * safeColumns + column;
                    SerializedProperty cell =
                        gridMatrixCells.GetArrayElementAtIndex(cellIndex);
                    bool enabled = cell.boolValue;
                    bool disableToggle = enabled && activeCount <= 1;

                    Color previousBackground = GUI.backgroundColor;
                    GUI.backgroundColor = enabled
                        ? new Color(0.35f, 0.9f, 0.35f)
                        : new Color(0.95f, 0.35f, 0.35f);

                    using (new EditorGUI.DisabledScope(disableToggle))
                    {
                        string label = enabled ? "YES" : "NO";
                        if (GUILayout.Button(
                            label,
                            GUILayout.Width(46f),
                            GUILayout.Height(24f)))
                        {
                            ToggleGridMatrixCell(cellIndex, !enabled);
                            GUI.backgroundColor = previousBackground;
                            GUIUtility.ExitGUI();
                        }
                    }

                    GUI.backgroundColor = previousBackground;
                }
            }
        }

        if (activeCount <= 1)
        {
            EditorGUILayout.HelpBox(
                "At least one cell must stay enabled, otherwise Custom Points would be empty and runtime would fall back to Enemy Count.",
                MessageType.None);
        }
    }

    private void ToggleGridMatrixCell(int cellIndex, bool enabled)
    {
        if (cellIndex < 0 || cellIndex >= gridMatrixCells.arraySize)
            return;

        if (!enabled && GetGridMatrixEnabledCellCount() <= 1)
            return;

        System.Collections.Generic.Dictionary<int, UnityEngine.Object>
            overridesByCell = CaptureGridMatrixEnemyOverrides();

        Undo.RecordObject(target, "Toggle Grid Matrix Cell");
        gridMatrixCells.GetArrayElementAtIndex(cellIndex).boolValue = enabled;
        ApplyGridMatrixToCustomPoints(overridesByCell);

        serializedObject.ApplyModifiedProperties();
        ReloadCustomFinalPointOrder();
        SceneView.RepaintAll();
    }

    private bool HasValidGridMatrix()
    {
        return gridMatrixCells.arraySize == GetGridMatrixCellCount();
    }

    private int GetGridMatrixCellCount()
    {
        return Mathf.Max(1, rows.intValue) * Mathf.Max(1, columns.intValue);
    }

    private int GetGridMatrixEnabledCellCount()
    {
        int count = 0;
        for (int i = 0; i < gridMatrixCells.arraySize; i++)
        {
            if (gridMatrixCells.GetArrayElementAtIndex(i).boolValue)
                count++;
        }

        return count;
    }

    private void EnsureGridMatrixSize(bool enableNewCells)
    {
        int requiredSize = GetGridMatrixCellCount();
        int oldSize = gridMatrixCells.arraySize;

        if (oldSize != requiredSize)
            gridMatrixCells.arraySize = requiredSize;

        for (int i = oldSize; i < requiredSize; i++)
            gridMatrixCells.GetArrayElementAtIndex(i).boolValue = enableNewCells;

        if (GetGridMatrixEnabledCellCount() == 0 && requiredSize > 0)
            gridMatrixCells.GetArrayElementAtIndex(0).boolValue = true;
    }

    private System.Collections.Generic.Dictionary<int, UnityEngine.Object>
        CaptureGridMatrixEnemyOverrides()
    {
        System.Collections.Generic.Dictionary<int, UnityEngine.Object>
            overridesByCell = new();

        if (!HasValidGridMatrix())
            return overridesByCell;

        EnsureCustomFormationOverrideSize();

        int customIndex = 0;
        for (int i = 0; i < gridMatrixCells.arraySize; i++)
        {
            if (!gridMatrixCells.GetArrayElementAtIndex(i).boolValue)
                continue;

            if (customIndex < customFormationEnemyOverrides.arraySize)
            {
                UnityEngine.Object enemyOverride =
                    customFormationEnemyOverrides
                        .GetArrayElementAtIndex(customIndex)
                        .objectReferenceValue;

                if (enemyOverride != null)
                    overridesByCell[i] = enemyOverride;
            }

            customIndex++;
        }

        return overridesByCell;
    }

    private void ApplyGridMatrixToCustomPoints(
        System.Collections.Generic.Dictionary<int, UnityEngine.Object>
            overridesByCell)
    {
        EnsureGridMatrixSize(true);

        int safeRows = Mathf.Max(1, rows.intValue);
        int safeColumns = Mathf.Max(1, columns.intValue);
        int activeCount = Mathf.Max(1, GetGridMatrixEnabledCellCount());

        ResizeCustomFormationData(activeCount);

        int customIndex = 0;
        for (int row = 0; row < safeRows; row++)
        {
            for (int column = 0; column < safeColumns; column++)
            {
                int cellIndex = row * safeColumns + column;
                if (!gridMatrixCells.GetArrayElementAtIndex(cellIndex).boolValue)
                    continue;

                customFormationPoints
                    .GetArrayElementAtIndex(customIndex)
                    .vector3Value = GetGridMatrixLocalPosition(
                        row,
                        column,
                        safeRows,
                        safeColumns);

                customFormationEnemyOverrides
                    .GetArrayElementAtIndex(customIndex)
                    .objectReferenceValue =
                        overridesByCell.TryGetValue(
                            cellIndex,
                            out UnityEngine.Object enemyOverride)
                            ? enemyOverride
                            : null;

                customIndex++;
            }
        }

        enemyCount.intValue = activeCount;
        formationLayout.enumValueIndex =
            (int)DirectedWaveFormationLayout.CustomPoints;
    }

    private Vector3 GetGridMatrixLocalPosition(
        int row,
        int column,
        int rowCount,
        int columnCount)
    {
        float xOffset = (columnCount - 1) * spacing.vector2Value.x * 0.5f;
        float yOffset = (rowCount - 1) * spacing.vector2Value.y * 0.5f;

        return formationCenter.vector3Value
            + new Vector3(
                column * spacing.vector2Value.x - xOffset,
                yOffset - row * spacing.vector2Value.y,
                0f);
    }

    private void DrawCustomPointUtility()
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Match Enemy Count"))
                ResizeCustomFormationData(
                    Mathf.Max(1, GetEditorConfiguredEnemyCount()));

            if (GUILayout.Button("Fill From Horizontal Line"))
            {
                int count = Mathf.Max(1, GetEditorConfiguredEnemyCount());
                ResizeCustomFormationData(count);

                for (int i = 0; i < count; i++)
                {
                    customFormationPoints
                        .GetArrayElementAtIndex(i)
                        .vector3Value = GetHorizontalLineLocalPosition(i);
                }
            }
        }
    }

    private void DrawCustomFinalPoints()
    {
        EditorGUILayout.Space(4f);
        customFinalPointsFoldout = EditorGUILayout.Foldout(
            customFinalPointsFoldout,
            $"Final Points ({customFormationPoints.arraySize})",
            true);

        if (!customFinalPointsFoldout)
        {
            EnsureCustomFormationOverrideSize();
            EditorGUILayout.HelpBox(
                $"Custom final points hidden. Current point count: {customFormationPoints.arraySize}.",
                MessageType.None);
            return;
        }

        EditorGUILayout.LabelField("Final Points", EditorStyles.boldLabel);

        int newSize = Mathf.Max(
            0,
            EditorGUILayout.IntField(
                "Size",
                customFormationPoints.arraySize));

        if (newSize != customFormationPoints.arraySize)
            ResizeCustomFormationData(newSize);

        EnsureCustomFormationOverrideSize();

        EditorGUILayout.HelpBox(
            "Enemy Override is optional. Empty override uses the global Enemy Prefab.",
            MessageType.Info);

        DrawCustomFinalPointSpawnOrder();
        EditorGUILayout.Space(4f);

        for (int i = 0; i < customFormationPoints.arraySize; i++)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField($"Point {i}", EditorStyles.boldLabel);

                    GUI.enabled = i > 0;
                    if (GUILayout.Button("↑", GUILayout.Width(28f)))
                    {
                        MoveCustomFinalPoint(i, i - 1);
                        GUI.enabled = true;
                        break;
                    }

                    GUI.enabled = i < customFormationPoints.arraySize - 1;
                    if (GUILayout.Button("↓", GUILayout.Width(28f)))
                    {
                        MoveCustomFinalPoint(i, i + 1);
                        GUI.enabled = true;
                        break;
                    }

                    GUI.enabled = true;
                }

                SerializedProperty point =
                    customFormationPoints.GetArrayElementAtIndex(i);
                point.vector3Value = EditorGUILayout.Vector3Field(
                    "Position",
                    point.vector3Value);

                SerializedProperty enemyOverride =
                    customFormationEnemyOverrides.GetArrayElementAtIndex(i);
                EditorGUILayout.PropertyField(
                    enemyOverride,
                    new GUIContent("Enemy Override"));
            }
        }
    }

    private void DrawCustomFinalPointSpawnOrder()
    {
        if (customFormationPoints.arraySize <= 1)
            return;

        if (IsComputedSpawnOrderMode())
        {
            DrawComputedSpawnOrderPreview(null);
            return;
        }

        EnsureCustomFinalPointOrderList();

        EditorGUILayout.LabelField("Spawn Order", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Drag rows by the handle to change which final point spawns earlier.",
            MessageType.None);
        customFinalPointOrderList.DoLayoutList();
    }

    private void DrawComputedSpawnOrderPreview(Transform transformRoot)
    {
        DirectedEnemySubWave wave = (DirectedEnemySubWave)target;
        int count = GetEditorEffectiveEnemyCount();
        int[] order = BuildEditorSpawnOrder(wave, count);

        EditorGUILayout.LabelField("Computed Spawn Order", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "This order is calculated from Spawn Order Mode. Use Rebuild Points From Spawn Order if you want to bake it into the actual point list.",
            MessageType.None);

        if (GUILayout.Button("Rebuild Points From Spawn Order"))
        {
            RebuildPointsFromCurrentSpawnOrder();
            return;
        }

        int maxVisibleRows = Mathf.Min(order.Length, 64);
        for (int step = 0; step < maxVisibleRows; step++)
        {
            int pointIndex = order[step];
            string pointName = GetComputedSpawnOrderPointName(
                transformRoot,
                pointIndex);
            Vector3 position = GetFormationWorldPosition(pointIndex, wave);

            EditorGUILayout.LabelField(
                $"#{step}",
                $"{pointName} | {position}");
        }

        if (order.Length > maxVisibleRows)
        {
            EditorGUILayout.HelpBox(
                $"Showing first {maxVisibleRows} of {order.Length} spawn steps.",
                MessageType.None);
        }
    }

    private string GetComputedSpawnOrderPointName(
        Transform transformRoot,
        int pointIndex)
    {
        if (transformRoot != null
            && pointIndex >= 0
            && pointIndex < transformRoot.childCount)
        {
            Transform point = transformRoot.GetChild(pointIndex);
            return $"Slot {pointIndex} ({point.name})";
        }

        return $"Point {pointIndex}";
    }

    private void EnsureCustomFinalPointOrderList()
    {
        if (IsCustomFinalPointOrderReloadNeeded())
            ReloadCustomFinalPointOrder();

        if (customFinalPointOrderList != null)
            return;

        customFinalPointOrderList = new ReorderableList(
            customFinalPointOrder,
            typeof(CustomFinalPointOrderEntry),
            true,
            false,
            false,
            false)
        {
            elementHeight = EditorGUIUtility.singleLineHeight + 6f,
            drawElementCallback = DrawCustomFinalPointOrderElement,
            onSelectCallback = _ => SceneView.RepaintAll(),
            onReorderCallback = _ => ApplyCustomFinalPointOrder()
        };
    }

    private bool IsCustomFinalPointOrderReloadNeeded()
    {
        if (customFinalPointOrder.Count != customFormationPoints.arraySize)
            return true;

        EnsureCustomFormationOverrideSize();

        for (int i = 0; i < customFinalPointOrder.Count; i++)
        {
            Vector3 position = customFormationPoints
                .GetArrayElementAtIndex(i)
                .vector3Value;
            Object enemyOverride = customFormationEnemyOverrides
                .GetArrayElementAtIndex(i)
                .objectReferenceValue;

            if (customFinalPointOrder[i].position != position
                || customFinalPointOrder[i].enemyOverride != enemyOverride)
            {
                return true;
            }
        }

        return false;
    }

    private void ReloadCustomFinalPointOrder()
    {
        EnsureCustomFormationOverrideSize();
        customFinalPointOrder.Clear();

        for (int i = 0; i < customFormationPoints.arraySize; i++)
        {
            customFinalPointOrder.Add(new CustomFinalPointOrderEntry
            {
                position = customFormationPoints
                    .GetArrayElementAtIndex(i)
                    .vector3Value,
                enemyOverride = customFormationEnemyOverrides
                    .GetArrayElementAtIndex(i)
                    .objectReferenceValue
            });
        }
    }

    private void DrawCustomFinalPointOrderElement(
        Rect rect,
        int index,
        bool active,
        bool focused)
    {
        if (index < 0 || index >= customFinalPointOrder.Count)
            return;

        rect.y += 2f;
        rect.height = EditorGUIUtility.singleLineHeight;

        bool isSelected = active
            || focused
            || customFinalPointOrderList != null
                && customFinalPointOrderList.index == index;
        if (isSelected)
        {
            EditorGUI.DrawRect(
                rect,
                new Color(1f, 0.65f, 0f, 0.18f));
            SceneView.RepaintAll();
        }

        CustomFinalPointOrderEntry entry = customFinalPointOrder[index];
        string enemyName = entry.enemyOverride != null
            ? entry.enemyOverride.name
            : "Global Enemy";
        EditorGUI.LabelField(
            rect,
            $"Point {index} | {entry.position} | {enemyName}");
    }

    private void ApplyCustomFinalPointOrder()
    {
        Undo.RecordObject(target, "Drag Reorder Custom Final Points");

        customFormationPoints.arraySize = customFinalPointOrder.Count;
        customFormationEnemyOverrides.arraySize = customFinalPointOrder.Count;

        for (int i = 0; i < customFinalPointOrder.Count; i++)
        {
            customFormationPoints
                .GetArrayElementAtIndex(i)
                .vector3Value = customFinalPointOrder[i].position;
            customFormationEnemyOverrides
                .GetArrayElementAtIndex(i)
                .objectReferenceValue = customFinalPointOrder[i].enemyOverride;
        }

        serializedObject.ApplyModifiedProperties();
        SceneView.RepaintAll();
    }

    private void ResizeCustomFormationData(int size)
    {
        int safeSize = Mathf.Max(0, size);
        customFormationPoints.arraySize = safeSize;
        customFormationEnemyOverrides.arraySize = safeSize;
    }

    private void EnsureCustomFormationOverrideSize()
    {
        if (customFormationEnemyOverrides.arraySize != customFormationPoints.arraySize)
            customFormationEnemyOverrides.arraySize = customFormationPoints.arraySize;
    }

    private void MoveCustomFinalPoint(int fromIndex, int toIndex)
    {
        if (fromIndex == toIndex
            || fromIndex < 0
            || toIndex < 0
            || fromIndex >= customFormationPoints.arraySize
            || toIndex >= customFormationPoints.arraySize)
        {
            return;
        }

        Undo.RecordObject(target, "Reorder Custom Final Point");
        customFormationPoints.MoveArrayElement(fromIndex, toIndex);

        EnsureCustomFormationOverrideSize();
        customFormationEnemyOverrides.MoveArrayElement(fromIndex, toIndex);
    }

    private void DrawTransformPointUtility()
    {
        EditorGUILayout.HelpBox(
            "Transform Points uses children of Formation Points Root as formation slots. "
            + "Place child objects in any shape you want.",
            MessageType.Info);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Create/Match Child Points"))
                CreateOrMatchTransformPoints();

            if (GUILayout.Button("Fill Children From Horizontal"))
                FillTransformPointsFromHorizontal();
        }

        Transform root = formationPointsRoot.objectReferenceValue as Transform;
        if (root != null && HasTransformPointsWithoutEnemyOverride(root))
        {
            if (GUILayout.Button("Add Enemy Override Fields To Slots"))
                AddEnemyOverrideFieldsToTransformPoints(root);
        }

        DrawTransformFinalPoints();
    }

    private void DrawTransformFinalPoints()
    {
        Transform root = formationPointsRoot.objectReferenceValue as Transform;

        EditorGUILayout.Space(4f);
        int pointCount = root != null ? root.childCount : 0;
        transformFinalPointsFoldout = EditorGUILayout.Foldout(
            transformFinalPointsFoldout,
            $"Final Points ({pointCount})",
            true);

        if (!transformFinalPointsFoldout)
        {
            EditorGUILayout.HelpBox(
                root != null
                    ? $"Transform final points hidden. Current point count: {pointCount}."
                    : "Transform final points hidden. Formation Points Root is not assigned.",
                MessageType.None);
            return;
        }

        EditorGUILayout.LabelField("Final Points", EditorStyles.boldLabel);

        if (root == null)
        {
            EditorGUILayout.HelpBox(
                "Create Formation Points Root first, or switch to Free through the preset button to convert the current formation.",
                MessageType.Warning);

            if (GUILayout.Button("Create Final Points From Current Formation"))
                ConvertCurrentFormationToTransformPoints();

            return;
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            int newSize = Mathf.Max(
                0,
                EditorGUILayout.IntField("Size", root.childCount));

            if (newSize != root.childCount)
                SetTransformPointCount(root, newSize);
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            GUI.enabled = root.childCount > 0;

            if (GUILayout.Button("Expand All"))
                SetAllFinalPointsExpanded(root, true);

            if (GUILayout.Button("Collapse All"))
                SetAllFinalPointsExpanded(root, false);

            GUI.enabled = true;
        }

        DrawTransformFinalPointSpawnOrder(root);
        EditorGUILayout.Space(4f);

        for (int i = 0; i < root.childCount; i++)
            DrawTransformFinalPoint(root.GetChild(i), i);
    }

    private void DrawTransformFinalPointSpawnOrder(Transform root)
    {
        if (root == null || root.childCount <= 1)
            return;

        if (IsComputedSpawnOrderMode())
        {
            DrawComputedSpawnOrderPreview(root);
            return;
        }

        EnsureTransformFinalPointOrderList(root);

        EditorGUILayout.LabelField("Spawn Order", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Drag rows by the handle to change child Slot order and spawn order.",
            MessageType.None);
        transformFinalPointOrderList.DoLayoutList();
    }

    private void EnsureTransformFinalPointOrderList(Transform root)
    {
        if (transformFinalPointOrderList == null
            || transformFinalPointOrderRoot != root)
        {
            transformFinalPointOrderRoot = root;
            ReloadTransformFinalPointOrder(root);
            transformFinalPointOrderList = new ReorderableList(
                transformFinalPointOrder,
                typeof(Transform),
                true,
                false,
                false,
                false)
            {
                elementHeight = EditorGUIUtility.singleLineHeight + 6f,
                drawElementCallback = DrawTransformFinalPointOrderElement,
                onSelectCallback = _ => SceneView.RepaintAll(),
                onReorderCallback = _ => ApplyTransformFinalPointOrder(root)
            };
            return;
        }

        if (IsTransformFinalPointOrderReloadNeeded(root))
            ReloadTransformFinalPointOrder(root);
    }

    private bool IsTransformFinalPointOrderReloadNeeded(Transform root)
    {
        if (root == null || transformFinalPointOrder.Count != root.childCount)
            return true;

        for (int i = 0; i < root.childCount; i++)
        {
            if (transformFinalPointOrder[i] != root.GetChild(i))
                return true;
        }

        return false;
    }

    private void ReloadTransformFinalPointOrder(Transform root)
    {
        transformFinalPointOrder.Clear();

        if (root == null)
            return;

        for (int i = 0; i < root.childCount; i++)
            transformFinalPointOrder.Add(root.GetChild(i));
    }

    private void DrawTransformFinalPointOrderElement(
        Rect rect,
        int index,
        bool active,
        bool focused)
    {
        if (index < 0 || index >= transformFinalPointOrder.Count)
            return;

        Transform point = transformFinalPointOrder[index];
        if (point == null)
            return;

        rect.y += 2f;
        rect.height = EditorGUIUtility.singleLineHeight;
        bool isSelected = active
            || focused
            || transformFinalPointOrderList != null
                && transformFinalPointOrderList.index == index;
        if (isSelected)
        {
            EditorGUI.DrawRect(
                rect,
                new Color(1f, 0.2f, 1f, 0.16f));
            SceneView.RepaintAll();
        }

        EditorGUI.LabelField(
            rect,
            $"Slot {index} | {point.name} | {point.position}");
    }

    private void ApplyTransformFinalPointOrder(Transform root)
    {
        if (root == null)
            return;

        Undo.RegisterFullObjectHierarchyUndo(root.gameObject, "Drag Reorder Transform Final Points");

        for (int i = 0; i < transformFinalPointOrder.Count; i++)
        {
            Transform point = transformFinalPointOrder[i];
            if (point != null && point.parent == root)
                point.SetSiblingIndex(i);
        }

        RenameTransformPointSlots(root);
        EditorUtility.SetDirty(root.gameObject);
        SceneView.RepaintAll();
    }

    private void DrawTransformFinalPoint(Transform point, int index)
    {
        if (point == null)
            return;

        string key = GetFinalPointFoldoutKey(index);
        bool expanded = SessionState.GetBool(key, false);
        string title = $"Slot {index}  {point.position}";

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            expanded = EditorGUILayout.Foldout(
                expanded,
                title,
                true,
                EditorStyles.foldoutHeader);
            SessionState.SetBool(key, expanded);

            if (!expanded)
                return;

            using (new EditorGUILayout.HorizontalScope())
            {
                GUI.enabled = index > 0;
                if (GUILayout.Button("Move Up / Earlier Spawn"))
                {
                    MoveTransformFinalPoint(point, index - 1);
                    GUI.enabled = true;
                    return;
                }

                GUI.enabled = point.parent != null
                    && index < point.parent.childCount - 1;
                if (GUILayout.Button("Move Down / Later Spawn"))
                {
                    MoveTransformFinalPoint(point, index + 1);
                    GUI.enabled = true;
                    return;
                }

                GUI.enabled = true;
            }

            EditorGUI.BeginChangeCheck();
            Vector3 worldPosition = EditorGUILayout.Vector3Field(
                "Position",
                point.position);

            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(point, "Move Final Formation Point");
                point.position = worldPosition;
            }

            DrawTransformPointEnemyOverride(point);
        }
    }

    private void MoveTransformFinalPoint(Transform point, int targetSiblingIndex)
    {
        if (point == null || point.parent == null)
            return;

        int safeIndex = Mathf.Clamp(targetSiblingIndex, 0, point.parent.childCount - 1);
        if (safeIndex == point.GetSiblingIndex())
            return;

        Undo.SetTransformParent(point, point.parent, "Reorder Transform Final Point");
        Undo.RecordObject(point, "Reorder Transform Final Point");
        point.SetSiblingIndex(safeIndex);
        RenameTransformPointSlots(point.parent);
        EditorUtility.SetDirty(point.parent.gameObject);
    }

    private void RenameTransformPointSlots(Transform root)
    {
        if (root == null)
            return;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (child != null)
                child.name = $"Slot_{i:00}";
        }
    }

    private void DrawTransformPointEnemyOverride(Transform point)
    {
        DirectedWaveEnemyOverride enemyOverride =
            point.GetComponent<DirectedWaveEnemyOverride>();

        if (enemyOverride == null)
        {
            if (GUILayout.Button("Add Enemy Override Field"))
                Undo.AddComponent<DirectedWaveEnemyOverride>(point.gameObject);

            return;
        }

        SerializedObject overrideObject = new SerializedObject(enemyOverride);
        SerializedProperty overridePrefab =
            overrideObject.FindProperty("enemyPrefabOverride");

        overrideObject.Update();
        EditorGUILayout.PropertyField(
            overridePrefab,
            new GUIContent("Enemy Override"));
        overrideObject.ApplyModifiedProperties();
    }

    private bool HasTransformPointsWithoutEnemyOverride(Transform root)
    {
        for (int i = 0; i < root.childCount; i++)
        {
            if (root.GetChild(i).GetComponent<DirectedWaveEnemyOverride>() == null)
                return true;
        }

        return false;
    }

    private void AddEnemyOverrideFieldsToTransformPoints(Transform root)
    {
        for (int i = 0; i < root.childCount; i++)
        {
            Transform point = root.GetChild(i);
            if (point.GetComponent<DirectedWaveEnemyOverride>() == null)
                Undo.AddComponent<DirectedWaveEnemyOverride>(point.gameObject);
        }
    }

    private void SetAllFinalPointsExpanded(Transform root, bool expanded)
    {
        for (int i = 0; i < root.childCount; i++)
            SessionState.SetBool(GetFinalPointFoldoutKey(i), expanded);
    }

    private string GetFinalPointFoldoutKey(int index)
    {
        return $"{FinalPointFoldoutPrefix}.{target.GetInstanceID()}.{index}";
    }

    private void SetTransformPointCount(Transform root, int targetCount)
    {
        while (root.childCount < targetCount)
        {
            GameObject point = new GameObject($"Slot_{root.childCount:00}");
            Undo.RegisterCreatedObjectUndo(point, "Create Formation Slot");
            point.transform.SetParent(root, false);
            point.transform.localRotation = Quaternion.identity;
            point.transform.localScale = Vector3.one;
            point.transform.position = GetNewTransformPointPosition(root);
            Undo.AddComponent<DirectedWaveEnemyOverride>(point);
        }

        while (root.childCount > targetCount)
            Undo.DestroyObjectImmediate(root.GetChild(root.childCount - 1).gameObject);
    }

    private Vector3 GetNewTransformPointPosition(Transform root)
    {
        if (root.childCount > 1)
            return root.GetChild(root.childCount - 2).position
                + new Vector3(spacing.vector2Value.x, 0f, 0f);

        DirectedEnemySubWave wave = (DirectedEnemySubWave)target;
        return ToWorld(
            wave,
            formationCenter.vector3Value,
            (DirectedWaveCoordinateSpace)formationCoordinateSpace.enumValueIndex);
    }

    private void DrawFormationPresetButtons()
    {
        EditorGUILayout.LabelField(
            "Quick Formation Presets",
            EditorStyles.boldLabel);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Horizontal"))
                formationLayout.enumValueIndex =
                    (int)DirectedWaveFormationLayout.HorizontalLine;

            if (GUILayout.Button("Vertical"))
                formationLayout.enumValueIndex =
                    (int)DirectedWaveFormationLayout.VerticalLine;

            if (GUILayout.Button("Grid"))
            {
                formationLayout.enumValueIndex =
                    (int)DirectedWaveFormationLayout.Grid;
                int count = Mathf.Max(1, GetEditorEffectiveEnemyCount());
                columns.intValue = Mathf.Max(1, Mathf.CeilToInt(
                    Mathf.Sqrt(count)));
                rows.intValue = Mathf.Max(1, Mathf.CeilToInt(
                    count / (float)columns.intValue));
            }

            if (GUILayout.Button("V Shape"))
                formationLayout.enumValueIndex =
                    (int)DirectedWaveFormationLayout.VShape;

            if (GUILayout.Button("Arc"))
                formationLayout.enumValueIndex =
                    (int)DirectedWaveFormationLayout.Arc;

            if (GUILayout.Button("Free"))
            {
                ConvertCurrentFormationToTransformPoints();
                formationLayout.enumValueIndex =
                    (int)DirectedWaveFormationLayout.TransformPoints;
            }
        }

        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField(
            "Shape Final Point Presets",
            EditorStyles.boldLabel);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Circle"))
                formationLayout.enumValueIndex =
                    (int)DirectedWaveFormationLayout.Circle;

            if (GUILayout.Button("Triangle"))
                formationLayout.enumValueIndex =
                    (int)DirectedWaveFormationLayout.Triangle;

            if (GUILayout.Button("Square"))
                formationLayout.enumValueIndex =
                    (int)DirectedWaveFormationLayout.Square;

            if (GUILayout.Button("Diamond"))
                formationLayout.enumValueIndex =
                    (int)DirectedWaveFormationLayout.Diamond;
        }

        EditorGUILayout.HelpBox(
            "Shape presets are configurable formations. Use Convert Shape To Free if you want editable Transform Points.",
            MessageType.None);
    }

    private void ApplyTransformPointShapePreset(FormationShapePreset preset)
    {
        DirectedEnemySubWave wave = (DirectedEnemySubWave)target;
        int count = GetShapePresetPointCount();
        Transform root = GetOrCreateFormationPointsRoot(wave);
        if (root == null)
            return;

        SetTransformPointCount(root, count);

        Vector3[] localPositions = CreateShapePresetLocalPoints(preset, count);
        DirectedWaveCoordinateSpace coordinateSpace =
            (DirectedWaveCoordinateSpace)formationCoordinateSpace.enumValueIndex;

        for (int i = 0; i < count; i++)
        {
            Transform child = root.GetChild(i);
            Undo.RecordObject(child, $"Apply {preset} Formation Preset");
            child.position = ToWorld(wave, localPositions[i], coordinateSpace);
        }

        formationLayout.enumValueIndex =
            (int)DirectedWaveFormationLayout.TransformPoints;
        RenameTransformPointSlots(root);
        ReloadTransformFinalPointOrder(root);
        SceneView.RepaintAll();
    }

    private int GetShapePresetPointCount()
    {
        int count = GetEditorEffectiveEnemyCount();
        if (count > 0)
            return count;

        return Mathf.Max(1, GetEditorConfiguredEnemyCount());
    }

    private Transform GetOrCreateFormationPointsRoot(DirectedEnemySubWave wave)
    {
        Transform root = formationPointsRoot.objectReferenceValue as Transform;
        if (root != null)
            return root;

        GameObject rootObject = new GameObject("FormationPoints");
        Undo.RegisterCreatedObjectUndo(
            rootObject,
            "Create Formation Points Root");
        rootObject.transform.SetParent(wave.transform, false);
        rootObject.transform.localPosition = formationCenter.vector3Value;
        rootObject.transform.localRotation = Quaternion.identity;
        rootObject.transform.localScale = Vector3.one;

        root = rootObject.transform;
        formationPointsRoot.objectReferenceValue = root;
        return root;
    }

    private Vector3[] CreateShapePresetLocalPoints(
        FormationShapePreset preset,
        int count)
    {
        count = Mathf.Max(1, count);

        return preset switch
        {
            FormationShapePreset.Triangle =>
                CreateTriangleLocalPoints(count),
            FormationShapePreset.Square =>
                CreatePolygonPerimeterLocalPoints(
                    CreateSquareVertices(),
                    count),
            FormationShapePreset.Diamond =>
                CreatePolygonPerimeterLocalPoints(
                    CreateDiamondVertices(),
                    count),
            _ => CreateCircleLocalPoints(count)
        };
    }

    private Vector3[] CreateCircleLocalPoints(int count)
    {
        Vector3[] points = new Vector3[count];
        Vector3 center = formationCenter.vector3Value;
        float radius = GetShapePresetRadius();

        if (count == 1)
        {
            points[0] = center;
            return points;
        }

        for (int i = 0; i < count; i++)
        {
            float angle = 90f - 360f * i / count;
            float radians = angle * Mathf.Deg2Rad;
            points[i] = center + new Vector3(
                Mathf.Cos(radians) * radius,
                Mathf.Sin(radians) * radius,
                0f);
        }

        return points;
    }

    private Vector3[] CreateTriangleLocalPoints(int count)
    {
        return CreatePolygonPerimeterLocalPoints(
            CreateTriangleVertices(),
            count);
    }

    private Vector3[] CreateTriangleVertices()
    {
        Vector3 center = formationCenter.vector3Value;
        float radius = GetShapePresetRadius();

        return new[]
        {
            center + Vector3.up * radius,
            center + new Vector3(
                Mathf.Cos(210f * Mathf.Deg2Rad) * radius,
                Mathf.Sin(210f * Mathf.Deg2Rad) * radius,
                0f),
            center + new Vector3(
                Mathf.Cos(330f * Mathf.Deg2Rad) * radius,
                Mathf.Sin(330f * Mathf.Deg2Rad) * radius,
                0f)
        };
    }

    private Vector3[] CreateSquareVertices()
    {
        Vector3 center = formationCenter.vector3Value;
        float radius = GetShapePresetRadius();

        return new[]
        {
            center + new Vector3(-radius, radius, 0f),
            center + new Vector3(radius, radius, 0f),
            center + new Vector3(radius, -radius, 0f),
            center + new Vector3(-radius, -radius, 0f)
        };
    }

    private Vector3[] CreateDiamondVertices()
    {
        Vector3 center = formationCenter.vector3Value;
        float radius = GetShapePresetRadius();

        return new[]
        {
            center + Vector3.up * radius,
            center + Vector3.right * radius,
            center + Vector3.down * radius,
            center + Vector3.left * radius
        };
    }

    private Vector3[] CreatePolygonPerimeterLocalPoints(
        Vector3[] vertices,
        int count)
    {
        count = Mathf.Max(1, count);
        Vector3[] points = new Vector3[count];

        if (vertices == null || vertices.Length == 0)
        {
            for (int i = 0; i < count; i++)
                points[i] = formationCenter.vector3Value;

            return points;
        }

        if (vertices.Length == 1 || count == 1)
        {
            points[0] = count == 1
                ? vertices[0]
                : formationCenter.vector3Value;
            for (int i = 1; i < count; i++)
                points[i] = vertices[0];

            return points;
        }

        float totalLength = 0f;
        for (int i = 0; i < vertices.Length; i++)
            totalLength += Vector3.Distance(
                vertices[i],
                vertices[(i + 1) % vertices.Length]);

        if (totalLength <= 0.0001f)
        {
            for (int i = 0; i < count; i++)
                points[i] = vertices[0];

            return points;
        }

        for (int pointIndex = 0; pointIndex < count; pointIndex++)
        {
            float distance = totalLength * pointIndex / count;
            points[pointIndex] = EvaluatePolygonPerimeter(
                vertices,
                distance,
                totalLength);
        }

        return points;
    }

    private static Vector3 EvaluatePolygonPerimeter(
        Vector3[] vertices,
        float distance,
        float totalLength)
    {
        float remaining = Mathf.Repeat(distance, totalLength);

        for (int i = 0; i < vertices.Length; i++)
        {
            Vector3 from = vertices[i];
            Vector3 to = vertices[(i + 1) % vertices.Length];
            float edgeLength = Vector3.Distance(from, to);

            if (remaining <= edgeLength)
            {
                float t = edgeLength <= 0.0001f
                    ? 0f
                    : remaining / edgeLength;
                return Vector3.LerpUnclamped(from, to, t);
            }

            remaining -= edgeLength;
        }

        return vertices[0];
    }

    private float GetShapePresetRadius()
    {
        float radius = Mathf.Max(0.1f, arcRadius.floatValue);
        if (radius > 0.1f)
            return radius;

        return Mathf.Max(
            0.5f,
            Mathf.Max(spacing.vector2Value.x, spacing.vector2Value.y));
    }

    private void ConvertCurrentFormationToTransformPoints()
    {
        DirectedEnemySubWave wave = (DirectedEnemySubWave)target;
        int count = Mathf.Max(1, GetEditorEffectiveEnemyCount());
        Vector3[] worldPositions = new Vector3[count];

        for (int i = 0; i < count; i++)
            worldPositions[i] = GetFormationWorldPosition(i, wave);

        Transform root = formationPointsRoot.objectReferenceValue as Transform;
        if (root == null)
        {
            GameObject rootObject = new GameObject("FormationPoints");
            Undo.RegisterCreatedObjectUndo(
                rootObject,
                "Create Formation Points Root");
            rootObject.transform.SetParent(wave.transform, false);
            rootObject.transform.localPosition = formationCenter.vector3Value;
            rootObject.transform.localRotation = Quaternion.identity;
            rootObject.transform.localScale = Vector3.one;

            root = rootObject.transform;
            formationPointsRoot.objectReferenceValue = root;
        }

        while (root.childCount < count)
        {
            GameObject point = new GameObject($"Slot_{root.childCount:00}");
            Undo.RegisterCreatedObjectUndo(point, "Create Formation Slot");
            point.transform.SetParent(root, false);
            point.transform.localRotation = Quaternion.identity;
            point.transform.localScale = Vector3.one;
        }

        while (root.childCount > count)
            Undo.DestroyObjectImmediate(root.GetChild(root.childCount - 1).gameObject);

        for (int i = 0; i < count; i++)
        {
            Transform child = root.GetChild(i);
            Undo.RecordObject(child, "Preserve Formation Slot Position");
            child.position = worldPositions[i];
        }
    }

    private void DrawPostBehavior()
    {
        postBehaviorFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(
            postBehaviorFoldout,
            "4. Post Behavior");

        if (postBehaviorFoldout)
        {
            EditorGUILayout.HelpBox(
                "Post Behavior starts after all enemies have reached their final formation positions.",
                MessageType.Info);

            DrawPostCommands();
            EditorGUILayout.PropertyField(postStartDelay);
            EditorGUILayout.PropertyField(
                postCommandPipelineLoop,
                new GUIContent(
                    "Pipeline Loop",
                    "Repeats the Post Commands sequence after the last command finishes."));

            bool usesWobble = HasPostCommand(DirectedWavePostCommandType.Wobble);
            bool usesAttack = HasPostCommand(DirectedWavePostCommandType.Attack);
            bool usesPatrol = HasPostCommand(DirectedWavePostCommandType.Patrol);
            bool usesLocalMovement =
                HasPostCommand(DirectedWavePostCommandType.LocalMovement);
            bool usesCircularMovement =
                HasPostCommand(DirectedWavePostCommandType.CircularMovement);
            bool usesFormationRotation =
                HasPostCommand(DirectedWavePostCommandType.FormationRotation);
            bool usesFormationMorph =
                HasPostCommand(DirectedWavePostCommandType.FormationMorph);

            if (usesLocalMovement)
            {
                EditorGUILayout.Space(4f);
                EditorGUILayout.LabelField("Local Movement", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(localMovementOffset);
                EditorGUILayout.PropertyField(localMovementDuration);
                EditorGUILayout.PropertyField(localMovementLoop);
                EditorGUILayout.PropertyField(localMovementPingPong);
                EditorGUILayout.PropertyField(localMovementCurve);
            }

            if (usesWobble)
            {
                EditorGUILayout.Space(4f);
                EditorGUILayout.LabelField("Wobble", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(
                    wobbleAmplitude,
                    new GUIContent(
                        "Amplitude X/Y",
                        "How far enemies wobble from their formation position on X and Y."));
                EditorGUILayout.PropertyField(
                    wobbleFrequency,
                    new GUIContent("Frequency", "Wobble speed."));
                EditorGUILayout.PropertyField(
                    wobblePhaseMode,
                    new GUIContent(
                        "Phase Mode",
                        "Spawn Order uses enemy order. Directional creates a travelling wave by position."));
                EditorGUILayout.PropertyField(
                    wobblePhaseOffset,
                    new GUIContent(
                        "Phase Offset",
                        "Phase delay in radians between spawn-order neighbours or directional steps."));

                bool directionalWobble =
                    (DirectedWaveWobblePhaseMode)wobblePhaseMode.enumValueIndex
                    == DirectedWaveWobblePhaseMode.Directional;

                if (!directionalWobble)
                {
                    EditorGUILayout.HelpBox(
                        "To make the wave go by angle, set Phase Mode to Directional.",
                        MessageType.None);
                }

                using (new EditorGUI.DisabledScope(!directionalWobble))
                {
                    EditorGUILayout.PropertyField(
                        wobbleDirectionAngle,
                        new GUIContent(
                            "Wave Direction Angle",
                            "Controls how the travelling wobble moves: 0 = left to right, 90 = bottom to top, 180 = right to left, 270 = top to bottom."));

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (GUILayout.Button("0° Left → Right"))
                            wobbleDirectionAngle.floatValue = 0f;

                        if (GUILayout.Button("90° Bottom → Top"))
                            wobbleDirectionAngle.floatValue = 90f;
                    }

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (GUILayout.Button("180° Right → Left"))
                            wobbleDirectionAngle.floatValue = 180f;

                        if (GUILayout.Button("270° Top → Bottom"))
                            wobbleDirectionAngle.floatValue = 270f;
                    }

                    EditorGUILayout.PropertyField(
                        wobbleDirectionStep,
                        new GUIContent(
                            "Wave Direction Step",
                            "World distance along the direction that equals one Phase Offset."));
                }

                if (directionalWobble)
                {
                    EditorGUILayout.HelpBox(
                        "Directional wobble starts from formation positions. Angle controls where the travelling wave begins: 0° left→right, 90° bottom→top, 180° right→left, 270° top→bottom.",
                        MessageType.None);
                }
            }

            if (usesAttack)
            {
                EditorGUILayout.Space(4f);
                EditorGUILayout.LabelField("Attack", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(diveInterval);
                EditorGUILayout.PropertyField(diveDuration);
                EditorGUILayout.PropertyField(diveReturnDuration);
                EditorGUILayout.PropertyField(diveOvershootDistance);
                EditorGUILayout.PropertyField(diveCurve);
                EditorGUILayout.PropertyField(diveReturnCurve);

                EditorGUILayout.HelpBox(
                    "Attack targets PlayerController.CurrentShip and uses the old dive-at-player behaviour.",
                    MessageType.None);
            }

            if (usesPatrol)
            {
                EditorGUILayout.Space(4f);
                DrawPatrol();
            }

            if (usesCircularMovement)
            {
                EditorGUILayout.Space(4f);
                EditorGUILayout.LabelField("Circular Movement", EditorStyles.boldLabel);
                EditorGUILayout.HelpBox(
                    "Moves each enemy around its formation point without rotating the enemy object itself.",
                    MessageType.None);
                EditorGUILayout.PropertyField(
                    selfOrbitRadius,
                    new GUIContent(
                        "Radius X/Y",
                        "Orbit radius around the enemy's own formation point."));
                EditorGUILayout.PropertyField(
                    selfOrbitPhaseOffset,
                    new GUIContent(
                        "Phase Offset",
                        "Phase delay in radians between neighbours."));
                EditorGUILayout.PropertyField(
                    selfRotationDegreesPerSecond,
                    new GUIContent(
                        "Degrees Per Second",
                        "Angular movement speed. Positive values move counter-clockwise, negative values move clockwise."));
            }

            if (usesFormationRotation)
            {
                EditorGUILayout.Space(4f);
                EditorGUILayout.LabelField("Formation Rotation", EditorStyles.boldLabel);
                EditorGUILayout.HelpBox(
                    "Rotates the whole formation around its own center like a wheel. Enemy objects keep their own rotation.",
                    MessageType.None);
                EditorGUILayout.PropertyField(
                    formationRotationDegreesPerSecond,
                    new GUIContent(
                        "Degrees Per Second",
                        "Angular speed of the whole formation. Positive values rotate counter-clockwise, negative values rotate clockwise."));
            }

            if (usesFormationMorph)
            {
                EditorGUILayout.Space(4f);
                EditorGUILayout.LabelField("Formation Morph", EditorStyles.boldLabel);
                EditorGUILayout.HelpBox(
                    "Changes the formation shape over time. Points are matched greedily by nearest target position.",
                    MessageType.None);
                EditorGUILayout.PropertyField(formationMorphLoop);
                EditorGUILayout.PropertyField(formationMorphReturnDuration);
                EditorGUILayout.PropertyField(formationMorphReturnCurve);
                EditorGUILayout.PropertyField(formationMorphSteps, true);
            }
        }

        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    private void DrawPostCommands()
    {
        EditorGUILayout.HelpBox(
            "Post Commands are executed from top to bottom as a pipeline. "
            + "Use Duration/Hold/Target Offset inside each command to build a timeline: "
            + "Move -> Morph -> Wheel -> Move Back -> Wheel...",
            MessageType.None);
        DrawPostCommandPipelineList();

        using (new EditorGUILayout.HorizontalScope())
        {
            DrawTogglePostCommandButton(
                DirectedWavePostCommandType.Patrol,
                "Add Patrol",
                "Remove Patrol");
            DrawTogglePostCommandButton(
                DirectedWavePostCommandType.LocalMovement,
                "Add Local",
                "Remove Local");
            DrawTogglePostCommandButton(
                DirectedWavePostCommandType.Wobble,
                "Add Wobble",
                "Remove Wobble");
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            DrawTogglePostCommandButton(
                DirectedWavePostCommandType.Attack,
                "Add Attack",
                "Remove Attack");
            DrawTogglePostCommandButton(
                DirectedWavePostCommandType.CircularMovement,
                "Add Circle",
                "Remove Circle");
            DrawTogglePostCommandButton(
                DirectedWavePostCommandType.FormationRotation,
                "Add Wheel",
                "Remove Wheel");
            DrawTogglePostCommandButton(
                DirectedWavePostCommandType.FormationMorph,
                "Add Morph",
                "Remove Morph");
            DrawTogglePostCommandButton(
                DirectedWavePostCommandType.Wait,
                "Add Wait",
                "Remove Wait");
            DrawTogglePostCommandButton(
                DirectedWavePostCommandType.Parallel,
                "Add Parallel",
                "Remove Parallel");
            DrawTogglePostCommandButton(
                DirectedWavePostCommandType.Loop,
                "Add Loop",
                "Remove Loop");

            if (GUILayout.Button("Clear Commands"))
                postCommands.arraySize = 0;
        }
    }

    private void DrawPostCommandPipelineList()
    {
        if (postCommands == null)
            return;

        if (postCommands.arraySize == 0)
        {
            EditorGUILayout.HelpBox(
                "Pipeline is empty. Add commands below.",
                MessageType.None);
            return;
        }

        int removeIndex = -1;
        for (int i = 0; i < postCommands.arraySize; i++)
        {
            SerializedProperty command = postCommands.GetArrayElementAtIndex(i);
            if (DrawPostCommandCell(command, i))
                removeIndex = i;
        }

        if (removeIndex >= 0)
        {
            postCommands.DeleteArrayElementAtIndex(removeIndex);
            if (activePostCommandIndex == removeIndex)
                activePostCommandIndex = -1;
            else if (activePostCommandIndex > removeIndex)
                activePostCommandIndex--;
        }

        if (activePostCommandIndex >= postCommands.arraySize)
            activePostCommandIndex = postCommands.arraySize - 1;
    }

    private bool DrawPostCommandCell(SerializedProperty command, int index)
    {
        SerializedProperty enabled = command.FindPropertyRelative("enabled");
        SerializedProperty type = command.FindPropertyRelative("type");
        DirectedWavePostCommandType commandType =
            (DirectedWavePostCommandType)type.enumValueIndex;

        EditorGUI.BeginChangeCheck();
        Rect cellRect = EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        bool remove = false;
        using (new EditorGUILayout.HorizontalScope())
        {
            enabled.boolValue = EditorGUILayout.Toggle(
                enabled.boolValue,
                GUILayout.Width(18f));
            EditorGUILayout.LabelField(
                $"#{index + 1}",
                GUILayout.Width(34f));
            EditorGUILayout.PropertyField(type, GUIContent.none);

            using (new EditorGUI.DisabledScope(index <= 0))
            {
                if (GUILayout.Button("↑", GUILayout.Width(28f)))
                {
                    postCommands.MoveArrayElement(index, index - 1);
                    SetActivePostCommandIndex(index - 1);
                }
            }

            using (new EditorGUI.DisabledScope(index >= postCommands.arraySize - 1))
            {
                if (GUILayout.Button("↓", GUILayout.Width(28f)))
                {
                    postCommands.MoveArrayElement(index, index + 1);
                    SetActivePostCommandIndex(index + 1);
                }
            }

            bool isActive = activePostCommandIndex == index;
            if (GUILayout.Button(isActive ? "Previewing" : "Preview", GUILayout.Width(86f)))
                SetActivePostCommandIndex(index);

            remove = GUILayout.Button("Remove", GUILayout.Width(72f));
        }

        using (new EditorGUI.DisabledScope(!enabled.boolValue))
            DrawPostCommandSettings(command, commandType);

        if (IsBlockingInfiniteParallel(command, commandType)
            && index < postCommands.arraySize - 1)
        {
            EditorGUILayout.HelpBox(
                "This Parallel command is Blocking and Infinite. Commands below it will never start.",
                MessageType.Warning);
        }

        if (IsInfiniteLoop(command, commandType)
            && index < postCommands.arraySize - 1)
        {
            EditorGUILayout.HelpBox(
                "This Loop command is Infinite. Commands below it will never start.",
                MessageType.Warning);
        }

        if (activePostCommandIndex == index)
        {
            EditorGUILayout.HelpBox(
                "This step is visualized in Scene View: cyan = before, green = after.",
                MessageType.None);
        }

        EditorGUILayout.EndVertical();

        if (EditorGUI.EndChangeCheck())
            SetActivePostCommandIndex(index);

        if (Event.current.type == EventType.MouseDown
            && cellRect.Contains(Event.current.mousePosition))
        {
            SetActivePostCommandIndex(index);
            Repaint();
            SceneView.RepaintAll();
        }

        return remove;
    }

    private bool IsBlockingInfiniteParallel(
        SerializedProperty command,
        DirectedWavePostCommandType commandType)
    {
        if (commandType != DirectedWavePostCommandType.Parallel)
            return false;

        SerializedProperty parallelExecutionMode =
            command.FindPropertyRelative("parallelExecutionMode");
        SerializedProperty infiniteParallel =
            command.FindPropertyRelative("infiniteParallel");

        return parallelExecutionMode != null
            && infiniteParallel != null
            && parallelExecutionMode.enumValueIndex
                == (int)DirectedWaveParallelExecutionMode.Blocking
            && infiniteParallel.boolValue;
    }

    private bool IsInfiniteLoop(
        SerializedProperty command,
        DirectedWavePostCommandType commandType)
    {
        if (commandType != DirectedWavePostCommandType.Loop)
            return false;

        SerializedProperty infiniteLoop =
            command.FindPropertyRelative("infiniteLoop");

        return infiniteLoop != null && infiniteLoop.boolValue;
    }

    private void SetActivePostCommandIndex(int index)
    {
        activePostCommandIndex = Mathf.Clamp(
            index,
            -1,
            postCommands != null ? postCommands.arraySize - 1 : -1);
        SceneView.RepaintAll();
    }

    private void DrawPostCommandSettings(
        SerializedProperty command,
        DirectedWavePostCommandType commandType)
    {
        SerializedProperty duration = command.FindPropertyRelative("duration");
        SerializedProperty holdDuration =
            command.FindPropertyRelative("holdDuration");
        SerializedProperty parallelExecutionMode =
            command.FindPropertyRelative("parallelExecutionMode");
        SerializedProperty infiniteParallel =
            command.FindPropertyRelative("infiniteParallel");
        SerializedProperty loopCount = command.FindPropertyRelative("loopCount");
        SerializedProperty infiniteLoop =
            command.FindPropertyRelative("infiniteLoop");
        SerializedProperty targetOffset =
            command.FindPropertyRelative("targetOffset");
        SerializedProperty rotationDegrees =
            command.FindPropertyRelative("rotationDegrees");
        SerializedProperty continuousFormationRotation =
            command.FindPropertyRelative("continuousFormationRotation");
        SerializedProperty curve = command.FindPropertyRelative("curve");
        SerializedProperty morphTarget =
            command.FindPropertyRelative("morphTarget");
        SerializedProperty parallelCommands =
            command.FindPropertyRelative("parallelCommands");
        SerializedProperty loopCommands =
            command.FindPropertyRelative("loopCommands");

        EditorGUI.indentLevel++;
        switch (commandType)
        {
            case DirectedWavePostCommandType.Wait:
                EditorGUILayout.PropertyField(
                    duration,
                    new GUIContent("Wait Duration"));
                break;

            case DirectedWavePostCommandType.LocalMovement:
                EditorGUILayout.PropertyField(
                    targetOffset,
                    new GUIContent(
                        "Target Offset",
                        "Target center offset relative to the original formation center."));
                DrawTimedCommandFields(duration, holdDuration, curve);
                break;

            case DirectedWavePostCommandType.FormationRotation:
                EditorGUILayout.PropertyField(
                    continuousFormationRotation,
                    new GUIContent(
                        "Continuous Rotation",
                        "If enabled, Rotation Degrees is treated as degrees per second for this command duration."));
                EditorGUILayout.PropertyField(
                    rotationDegrees,
                    new GUIContent(
                        continuousFormationRotation.boolValue
                            ? "Degrees Per Second"
                            : "Rotation Degrees",
                        continuousFormationRotation.boolValue
                            ? "Rotation speed in degrees per second."
                            : "Exact angle this command rotates the current formation."));
                DrawTimedCommandFields(duration, holdDuration, curve);
                break;

            case DirectedWavePostCommandType.FormationMorph:
                DrawTimedCommandFields(duration, holdDuration, curve);
                DrawMorphTargetFields(morphTarget);
                break;

            case DirectedWavePostCommandType.Patrol:
                DrawTimedCommandFields(duration, holdDuration, curve);
                EditorGUILayout.HelpBox(
                    "Uses the Patrol Points block below as a route for this command duration.",
                    MessageType.None);
                break;

            case DirectedWavePostCommandType.Wobble:
                DrawTimedCommandFields(duration, holdDuration, curve);
                EditorGUILayout.HelpBox(
                    "Uses the Wobble settings below. This command is temporary and stops after Duration.",
                    MessageType.None);
                break;

            case DirectedWavePostCommandType.CircularMovement:
                DrawTimedCommandFields(duration, holdDuration, curve);
                EditorGUILayout.HelpBox(
                    "Uses Circular Movement settings below. This command is temporary and stops after Duration.",
                    MessageType.None);
                break;

            case DirectedWavePostCommandType.Attack:
                DrawTimedCommandFields(duration, holdDuration, curve);
                EditorGUILayout.HelpBox(
                    "Runs repeated dive attacks for Duration using Attack settings below.",
                    MessageType.None);
                break;

            case DirectedWavePostCommandType.Parallel:
                EditorGUILayout.PropertyField(
                    parallelExecutionMode,
                    new GUIContent(
                        "Execution Mode",
                        "Blocking waits until this parallel block finishes. Background starts it and immediately continues the following commands."));
                EditorGUILayout.PropertyField(
                    infiniteParallel,
                    new GUIContent(
                        "Infinite",
                        "Runs this parallel block forever instead of using Parallel Duration."));
                using (new EditorGUI.DisabledScope(infiniteParallel.boolValue))
                {
                    EditorGUILayout.PropertyField(
                        duration,
                        new GUIContent(
                            "Parallel Duration",
                            "How long all nested commands run together."));
                }
                EditorGUILayout.PropertyField(
                    holdDuration,
                    new GUIContent(
                        "Hold Duration",
                        "Only affects Blocking mode after a finite Parallel block finishes."));
                DrawParallelCommandList(parallelCommands);
                break;

            case DirectedWavePostCommandType.Loop:
                EditorGUILayout.PropertyField(
                    infiniteLoop,
                    new GUIContent(
                        "Infinite Loop",
                        "Repeats nested commands forever. Commands below this Loop will never start."));
                using (new EditorGUI.DisabledScope(infiniteLoop.boolValue))
                {
                    EditorGUILayout.PropertyField(
                        loopCount,
                        new GUIContent(
                            "Loop Count",
                            "How many times the nested sequential commands will be executed."));
                }
                EditorGUILayout.PropertyField(
                    holdDuration,
                    new GUIContent(
                        "Hold Duration",
                        "Optional pause after the whole finite loop finishes."));
                EditorGUILayout.HelpBox(
                    "Nested commands run from top to bottom. Each child command keeps its own Duration and Hold Duration.",
                    MessageType.None);
                DrawLoopCommandList(loopCommands);
                break;
        }
        EditorGUI.indentLevel--;
    }

    private void DrawParallelCommandList(SerializedProperty parallelCommands)
    {
        if (parallelCommands == null)
            return;

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                parallelCommands.isExpanded = EditorGUILayout.Foldout(
                    parallelCommands.isExpanded,
                    $"Parallel Commands ({parallelCommands.arraySize})",
                    true);

                if (GUILayout.Button("Add", GUILayout.Width(58f)))
                    AddParallelCommand(parallelCommands);
            }

            if (parallelCommands.arraySize == 0)
            {
                EditorGUILayout.HelpBox(
                    "Add events here. They will run together for the Parallel Duration.",
                    MessageType.Info);
            }

            if (!parallelCommands.isExpanded)
                return;

            EditorGUI.indentLevel++;
            int removeIndex = -1;
            for (int i = 0; i < parallelCommands.arraySize; i++)
            {
                SerializedProperty child = parallelCommands.GetArrayElementAtIndex(i);
                SerializedProperty type = child.FindPropertyRelative("type");
                SerializedProperty enabled = child.FindPropertyRelative("enabled");
                DirectedWavePostCommandType childType =
                    (DirectedWavePostCommandType)type.enumValueIndex;

                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        enabled.boolValue = EditorGUILayout.Toggle(
                            enabled.boolValue,
                            GUILayout.Width(18f));
                        EditorGUILayout.LabelField($"#{i + 1}", GUILayout.Width(34f));
                        EditorGUILayout.PropertyField(type, GUIContent.none);
                        if (GUILayout.Button("Remove", GUILayout.Width(72f)))
                            removeIndex = i;
                    }

                    if (childType == DirectedWavePostCommandType.Parallel
                        || childType == DirectedWavePostCommandType.Loop)
                    {
                        EditorGUILayout.HelpBox(
                            "Nested Parallel/Loop is intentionally ignored inside Parallel. Use regular commands here, or put Parallel inside a Loop step.",
                            MessageType.Warning);
                    }
                    else
                    {
                        using (new EditorGUI.DisabledScope(!enabled.boolValue))
                            DrawPostCommandSettings(child, childType);
                    }
                }
            }

            if (removeIndex >= 0)
                parallelCommands.DeleteArrayElementAtIndex(removeIndex);

            if (GUILayout.Button("Add Parallel Event"))
                AddParallelCommand(parallelCommands);

            EditorGUI.indentLevel--;
        }
    }

    private void AddParallelCommand(SerializedProperty parallelCommands)
    {
        int index = parallelCommands.arraySize;
        parallelCommands.InsertArrayElementAtIndex(index);
        SerializedProperty child = parallelCommands.GetArrayElementAtIndex(index);
        InitializeNestedPostCommand(child, DirectedWavePostCommandType.Wobble);
        parallelCommands.isExpanded = true;
    }

    private void DrawLoopCommandList(SerializedProperty loopCommands)
    {
        if (loopCommands == null)
            return;

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                loopCommands.isExpanded = EditorGUILayout.Foldout(
                    loopCommands.isExpanded,
                    $"Loop Commands ({loopCommands.arraySize})",
                    true);

                if (GUILayout.Button("Add", GUILayout.Width(58f)))
                    AddLoopCommand(loopCommands);
            }

            if (loopCommands.arraySize == 0)
            {
                EditorGUILayout.HelpBox(
                    "Add sequential events here. They will run from top to bottom, then repeat.",
                    MessageType.Info);
            }

            if (!loopCommands.isExpanded)
                return;

            EditorGUI.indentLevel++;
            int removeIndex = -1;
            for (int i = 0; i < loopCommands.arraySize; i++)
            {
                SerializedProperty child = loopCommands.GetArrayElementAtIndex(i);
                SerializedProperty type = child.FindPropertyRelative("type");
                SerializedProperty enabled = child.FindPropertyRelative("enabled");
                DirectedWavePostCommandType childType =
                    (DirectedWavePostCommandType)type.enumValueIndex;

                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        enabled.boolValue = EditorGUILayout.Toggle(
                            enabled.boolValue,
                            GUILayout.Width(18f));
                        EditorGUILayout.LabelField($"#{i + 1}", GUILayout.Width(34f));
                        EditorGUILayout.PropertyField(type, GUIContent.none);

                        using (new EditorGUI.DisabledScope(i <= 0))
                        {
                            if (GUILayout.Button("↑", GUILayout.Width(28f)))
                                loopCommands.MoveArrayElement(i, i - 1);
                        }

                        using (new EditorGUI.DisabledScope(i >= loopCommands.arraySize - 1))
                        {
                            if (GUILayout.Button("↓", GUILayout.Width(28f)))
                                loopCommands.MoveArrayElement(i, i + 1);
                        }

                        if (GUILayout.Button("Remove", GUILayout.Width(72f)))
                            removeIndex = i;
                    }

                    child.isExpanded = EditorGUILayout.Foldout(
                        child.isExpanded,
                        "Settings",
                        true);

                    if (!child.isExpanded)
                        continue;

                    if (childType == DirectedWavePostCommandType.Loop)
                    {
                        EditorGUILayout.HelpBox(
                            "Nested Loop is disabled to prevent recursive Inspector rendering. Use regular commands or Parallel inside this Loop.",
                            MessageType.Warning);
                    }
                    else
                    {
                        using (new EditorGUI.DisabledScope(!enabled.boolValue))
                            DrawPostCommandSettings(child, childType);
                    }
                }
            }

            if (removeIndex >= 0)
                loopCommands.DeleteArrayElementAtIndex(removeIndex);

            if (GUILayout.Button("Add Loop Event"))
                AddLoopCommand(loopCommands);

            EditorGUI.indentLevel--;
        }
    }

    private void AddLoopCommand(SerializedProperty loopCommands)
    {
        int index = loopCommands.arraySize;
        loopCommands.InsertArrayElementAtIndex(index);
        SerializedProperty child = loopCommands.GetArrayElementAtIndex(index);
        InitializeNestedPostCommand(child, DirectedWavePostCommandType.Wait);
        child.isExpanded = true;
        loopCommands.isExpanded = true;
    }

    private void InitializeNestedPostCommand(
        SerializedProperty command,
        DirectedWavePostCommandType type)
    {
        command.FindPropertyRelative("type").enumValueIndex = (int)type;
        command.FindPropertyRelative("enabled").boolValue = true;
        command.FindPropertyRelative("duration").floatValue = 1f;
        command.FindPropertyRelative("holdDuration").floatValue = 0f;
        command.FindPropertyRelative("parallelExecutionMode").enumValueIndex =
            (int)DirectedWaveParallelExecutionMode.Blocking;
        command.FindPropertyRelative("infiniteParallel").boolValue = false;
        command.FindPropertyRelative("loopCount").intValue = 1;
        command.FindPropertyRelative("infiniteLoop").boolValue = false;
        command.FindPropertyRelative("targetOffset").vector3Value = Vector3.zero;
        command.FindPropertyRelative("rotationDegrees").floatValue = 45f;
        command.FindPropertyRelative("continuousFormationRotation").boolValue = false;
        command.FindPropertyRelative("curve").animationCurveValue =
            AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        ResetMorphTargetDefaults(command.FindPropertyRelative("morphTarget"));
        command.FindPropertyRelative("parallelCommands").arraySize = 0;
        command.FindPropertyRelative("loopCommands").arraySize = 0;
    }

    private void DrawTimedCommandFields(
        SerializedProperty duration,
        SerializedProperty holdDuration,
        SerializedProperty curve)
    {
        EditorGUILayout.PropertyField(duration);
        EditorGUILayout.PropertyField(holdDuration);
        EditorGUILayout.PropertyField(curve);
    }

    private void DrawMorphTargetFields(SerializedProperty morphTarget)
    {
        if (morphTarget == null)
            return;

        SerializedProperty layout = morphTarget.FindPropertyRelative("layout");
        DirectedWaveFormationLayout targetLayout =
            (DirectedWaveFormationLayout)layout.enumValueIndex;

        EditorGUILayout.PropertyField(
            layout,
            new GUIContent("Target Shape"));
        EditorGUILayout.PropertyField(
            morphTarget.FindPropertyRelative("centerOffset"));

        switch (targetLayout)
        {
            case DirectedWaveFormationLayout.Grid:
                EditorGUILayout.PropertyField(
                    morphTarget.FindPropertyRelative("columns"));
                EditorGUILayout.PropertyField(
                    morphTarget.FindPropertyRelative("rows"));
                EditorGUILayout.PropertyField(
                    morphTarget.FindPropertyRelative("shapeRadius"),
                    new GUIContent("Cell Spacing"));
                break;

            case DirectedWaveFormationLayout.Arc:
                EditorGUILayout.PropertyField(
                    morphTarget.FindPropertyRelative("arcRadius"));
                EditorGUILayout.PropertyField(
                    morphTarget.FindPropertyRelative("arcDegrees"));
                EditorGUILayout.PropertyField(
                    morphTarget.FindPropertyRelative("shapeFlattening"));
                break;

            case DirectedWaveFormationLayout.CustomPoints:
                EditorGUILayout.PropertyField(
                    morphTarget.FindPropertyRelative("customPoints"),
                    true);
                break;

            case DirectedWaveFormationLayout.HorizontalLine:
            case DirectedWaveFormationLayout.VerticalLine:
            case DirectedWaveFormationLayout.VShape:
            case DirectedWaveFormationLayout.Circle:
            case DirectedWaveFormationLayout.Triangle:
            case DirectedWaveFormationLayout.Square:
            case DirectedWaveFormationLayout.Diamond:
                EditorGUILayout.PropertyField(
                    morphTarget.FindPropertyRelative("shapeRadius"));
                EditorGUILayout.PropertyField(
                    morphTarget.FindPropertyRelative("shapeFlattening"));
                break;

            case DirectedWaveFormationLayout.TransformPoints:
                EditorGUILayout.HelpBox(
                    "TransformPoints cannot be used inside a Morph command yet. Use CustomPoints instead.",
                    MessageType.Warning);
                break;
        }
    }

    private void DrawTogglePostCommandButton(
        DirectedWavePostCommandType type,
        string addLabel,
        string removeLabel)
    {
        bool hasCommand = HasPostCommand(type);

        if (!hasCommand)
        {
            if (GUILayout.Button(addLabel))
                AddPostCommand(type);

            return;
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button(addLabel))
                AddPostCommand(type);

            if (GUILayout.Button(removeLabel))
                RemoveLastPostCommand(type);
        }
    }

    private void AddPostCommand(DirectedWavePostCommandType type)
    {
        int index = postCommands.arraySize;
        postCommands.arraySize++;

        SerializedProperty command = postCommands.GetArrayElementAtIndex(index);
        command.FindPropertyRelative("type").enumValueIndex = (int)type;
        command.FindPropertyRelative("enabled").boolValue = true;
        command.FindPropertyRelative("duration").floatValue = 1f;
        command.FindPropertyRelative("holdDuration").floatValue = 0f;
        command.FindPropertyRelative("parallelExecutionMode").enumValueIndex =
            (int)DirectedWaveParallelExecutionMode.Blocking;
        command.FindPropertyRelative("infiniteParallel").boolValue = false;
        command.FindPropertyRelative("loopCount").intValue = 1;
        command.FindPropertyRelative("infiniteLoop").boolValue = false;
        command.FindPropertyRelative("targetOffset").vector3Value = Vector3.zero;
        command.FindPropertyRelative("rotationDegrees").floatValue = 45f;
        command.FindPropertyRelative("continuousFormationRotation").boolValue = false;
        command.FindPropertyRelative("curve").animationCurveValue =
            AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        ResetMorphTargetDefaults(command.FindPropertyRelative("morphTarget"));
        command.FindPropertyRelative("parallelCommands").arraySize = 0;
        command.FindPropertyRelative("loopCommands").arraySize = 0;
    }

    private void ResetMorphTargetDefaults(SerializedProperty morphTarget)
    {
        if (morphTarget == null)
            return;

        morphTarget.FindPropertyRelative("layout").enumValueIndex =
            (int)DirectedWaveFormationLayout.Circle;
        morphTarget.FindPropertyRelative("centerOffset").vector3Value = Vector3.zero;
        morphTarget.FindPropertyRelative("columns").intValue = 5;
        morphTarget.FindPropertyRelative("rows").intValue = 3;
        morphTarget.FindPropertyRelative("arcRadius").floatValue = 2f;
        morphTarget.FindPropertyRelative("arcDegrees").floatValue = 120f;
        morphTarget.FindPropertyRelative("shapeRadius").floatValue = 2f;
        morphTarget.FindPropertyRelative("shapeFlattening").vector2Value = Vector2.one;
        morphTarget.FindPropertyRelative("customPoints").arraySize = 0;
        morphTarget.FindPropertyRelative("durationToShape").floatValue = 1f;
        morphTarget.FindPropertyRelative("holdDuration").floatValue = 0f;
        morphTarget.FindPropertyRelative("easeToShape").animationCurveValue =
            AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    }

    private void RemovePostCommand(DirectedWavePostCommandType type)
    {
        if (postCommands == null)
            return;

        for (int i = postCommands.arraySize - 1; i >= 0; i--)
        {
            SerializedProperty command = postCommands.GetArrayElementAtIndex(i);
            SerializedProperty commandType = command.FindPropertyRelative("type");
            if (commandType.enumValueIndex != (int)type)
                continue;

            postCommands.DeleteArrayElementAtIndex(i);
        }
    }

    private void RemoveLastPostCommand(DirectedWavePostCommandType type)
    {
        if (postCommands == null)
            return;

        for (int i = postCommands.arraySize - 1; i >= 0; i--)
        {
            SerializedProperty command = postCommands.GetArrayElementAtIndex(i);
            SerializedProperty commandType = command.FindPropertyRelative("type");
            if (commandType.enumValueIndex != (int)type)
                continue;

            postCommands.DeleteArrayElementAtIndex(i);
            return;
        }
    }

    private bool HasPostCommand(DirectedWavePostCommandType type)
    {
        return HasPostCommandInArray(postCommands, type, 0);
    }

    private bool HasPostCommandInArray(
        SerializedProperty commands,
        DirectedWavePostCommandType type,
        int depth)
    {
        if (commands == null || depth > 8)
            return false;

        for (int i = 0; i < commands.arraySize; i++)
        {
            SerializedProperty command = commands.GetArrayElementAtIndex(i);
            SerializedProperty enabled = command.FindPropertyRelative("enabled");
            SerializedProperty commandType = command.FindPropertyRelative("type");
            if (enabled == null || commandType == null || !enabled.boolValue)
                continue;

            if (commandType.enumValueIndex == (int)type)
            {
                return true;
            }

            if (commandType.enumValueIndex == (int)DirectedWavePostCommandType.Parallel)
            {
                SerializedProperty parallelCommands =
                    command.FindPropertyRelative("parallelCommands");
                if (HasPostCommandInArray(parallelCommands, type, depth + 1))
                    return true;
            }

            if (commandType.enumValueIndex == (int)DirectedWavePostCommandType.Loop)
            {
                SerializedProperty loopCommands =
                    command.FindPropertyRelative("loopCommands");
                if (HasPostCommandInArray(loopCommands, type, depth + 1))
                    return true;
            }
        }

        return false;
    }

    private void DrawPatrol()
    {
        EditorGUILayout.LabelField("Patrol", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Patrol points are offsets from each enemy's final formation position. "
            + "This keeps the formation shape while the whole subwave patrols.",
            MessageType.None);

        EditorGUILayout.PropertyField(
            patrolLoop,
            new GUIContent(
                "Loop",
                "If enabled, the last patrol point returns to the first one."));

        EnsurePatrolPointSpeedsInitialized();

        using (new EditorGUILayout.HorizontalScope())
        {
            int newSize = Mathf.Max(
                0,
                EditorGUILayout.IntField("Size", patrolPoints.arraySize));

            if (newSize != patrolPoints.arraySize)
                ResizePatrolPoints(newSize);
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Add Patrol Point"))
                AddPatrolPoint();

            GUI.enabled = patrolPoints.arraySize > 0;
            if (GUILayout.Button("Remove Last"))
                patrolPoints.DeleteArrayElementAtIndex(patrolPoints.arraySize - 1);

            GUI.enabled = true;
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            GUI.enabled = patrolPoints.arraySize > 0;

            if (GUILayout.Button("Expand All"))
                SetAllPatrolPointsExpanded(true);

            if (GUILayout.Button("Collapse All"))
                SetAllPatrolPointsExpanded(false);

            GUI.enabled = true;
        }

        if (patrolPoints.arraySize < 2)
        {
            EditorGUILayout.HelpBox(
                "Add at least 2 patrol points to make visible movement.",
                MessageType.Warning);
        }

        EditorGUILayout.Space(4f);

        for (int i = 0; i < patrolPoints.arraySize; i++)
        {
            SerializedProperty point = patrolPoints.GetArrayElementAtIndex(i);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                SerializedProperty offset = point.FindPropertyRelative("offset");
                string title = $"Patrol Point {i}  Offset {offset.vector3Value}";
                point.isExpanded = EditorGUILayout.Foldout(
                    point.isExpanded,
                    title,
                    true,
                    EditorStyles.foldoutHeader);

                if (!point.isExpanded)
                    continue;

                SerializedProperty durationToNext =
                    point.FindPropertyRelative("durationToNext");
                SerializedProperty speedToNext =
                    point.FindPropertyRelative("speedToNext");
                SerializedProperty motionToNext =
                    point.FindPropertyRelative("motionToNext");
                SerializedProperty easeToNext =
                    point.FindPropertyRelative("easeToNext");

                EditorGUI.BeginChangeCheck();
                EditorGUILayout.PropertyField(
                    offset,
                    new GUIContent(
                        "Offset",
                        "Offset from this enemy's own final formation position."));
                bool positionChanged = EditorGUI.EndChangeCheck();

                if (PatrolPointHasNextSegment(i))
                {
                    EditorGUI.BeginChangeCheck();
                    EditorGUILayout.PropertyField(
                        durationToNext,
                        new GUIContent("Duration To Next"));
                    bool durationChanged = EditorGUI.EndChangeCheck();

                    EditorGUI.BeginChangeCheck();
                    EditorGUILayout.PropertyField(
                        speedToNext,
                        new GUIContent("Speed To Next"));
                    bool speedChanged = EditorGUI.EndChangeCheck();

                    EditorGUI.BeginChangeCheck();
                    EditorGUILayout.PropertyField(
                        motionToNext,
                        new GUIContent("Motion To Next"));
                    EditorGUILayout.PropertyField(
                        easeToNext,
                        new GUIContent("Ease To Next"));
                    bool pathShapeChanged = EditorGUI.EndChangeCheck();

                    SyncPatrolDurationAndSpeed(
                        i,
                        durationChanged,
                        speedChanged,
                        positionChanged || pathShapeChanged);
                }
                else
                {
                    EditorGUILayout.HelpBox(
                        "Last patrol point has no next segment while Loop is disabled.",
                        MessageType.None);
                }

                if (positionChanged && i > 0)
                    SyncPatrolDurationAndSpeed(i - 1, false, false, true);

                if (positionChanged && patrolLoop.boolValue && i == 0)
                    SyncPatrolDurationAndSpeed(
                        patrolPoints.arraySize - 1,
                        false,
                        false,
                        true);
            }
        }
    }

    private void EnsurePatrolPointSpeedsInitialized()
    {
        for (int i = 0; i < patrolPoints.arraySize; i++)
        {
            if (!PatrolPointHasNextSegment(i))
                continue;

            SerializedProperty point = patrolPoints.GetArrayElementAtIndex(i);
            SerializedProperty speedToNext =
                point.FindPropertyRelative("speedToNext");
            float duration = Mathf.Max(
                0.01f,
                point.FindPropertyRelative("durationToNext").floatValue);
            speedToNext.floatValue = GetPatrolSegmentLength(i) / duration;
        }
    }

    private void SyncPatrolDurationAndSpeed(
        int index,
        bool durationChanged,
        bool speedChanged,
        bool pathShapeChanged)
    {
        if (!PatrolPointHasNextSegment(index))
            return;

        SerializedProperty point = patrolPoints.GetArrayElementAtIndex(index);
        SerializedProperty durationToNext =
            point.FindPropertyRelative("durationToNext");
        SerializedProperty speedToNext =
            point.FindPropertyRelative("speedToNext");

        float segmentLength = GetPatrolSegmentLength(index);

        if (speedChanged)
        {
            float speed = Mathf.Max(0.01f, speedToNext.floatValue);
            speedToNext.floatValue = speed;
            durationToNext.floatValue = Mathf.Max(0.01f, segmentLength / speed);
            return;
        }

        if (durationChanged || pathShapeChanged)
        {
            float duration = Mathf.Max(0.01f, durationToNext.floatValue);
            durationToNext.floatValue = duration;
            speedToNext.floatValue = Mathf.Max(0.01f, segmentLength / duration);
        }
    }

    private bool PatrolPointHasNextSegment(int index)
    {
        if (patrolPoints.arraySize < 2 || index < 0 || index >= patrolPoints.arraySize)
            return false;

        return patrolLoop.boolValue || index < patrolPoints.arraySize - 1;
    }

    private void ResizePatrolPoints(int newSize)
    {
        int oldSize = patrolPoints.arraySize;
        patrolPoints.arraySize = newSize;

        for (int i = oldSize; i < newSize; i++)
            InitializePatrolPoint(i);
    }

    private void AddPatrolPoint()
    {
        int index = patrolPoints.arraySize;
        patrolPoints.arraySize++;
        InitializePatrolPoint(index);
    }

    private void InitializePatrolPoint(int index)
    {
        SerializedProperty point = patrolPoints.GetArrayElementAtIndex(index);

        Vector3 offset = Vector3.zero;
        if (index > 0)
        {
            offset = patrolPoints
                .GetArrayElementAtIndex(index - 1)
                .FindPropertyRelative("offset")
                .vector3Value + Vector3.right;
        }

        point.FindPropertyRelative("offset").vector3Value = offset;
        point.FindPropertyRelative("durationToNext").floatValue = 0.5f;
        point.FindPropertyRelative("speedToNext").floatValue = 1f;
        point.FindPropertyRelative("motionToNext").enumValueIndex =
            (int)DirectedWaveSegmentMotion.Linear;
        point.FindPropertyRelative("easeToNext").animationCurveValue =
            AnimationCurve.Linear(0f, 0f, 1f, 1f);

        if (index > 0)
            SyncPatrolDurationAndSpeed(index - 1, true, false, false);
    }

    private void SetAllPatrolPointsExpanded(bool expanded)
    {
        for (int i = 0; i < patrolPoints.arraySize; i++)
            patrolPoints.GetArrayElementAtIndex(i).isExpanded = expanded;
    }

    private void DrawPreviewHelp()
    {
        previewFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(
            previewFoldout,
            "Scene View Editing");

        if (previewFoldout)
        {
            EditorGUILayout.HelpBox(
                "Select this object in Scene View:\n"
                + "- cyan handles edit entrance path points;\n"
                + "- yellow handle edits formation center;\n"
                + "- orange handles edit custom formation points;\n"
                + "- magenta handles edit Transform Points child slots.",
                MessageType.None);

            if (PrefabUtility.IsPartOfPrefabAsset(target))
            {
                EditorGUILayout.HelpBox(
                    "You are selecting a prefab asset. Preview is drawn in Scene View/Prefab Mode. Open the prefab or place it in a scene to see the animation.",
                    MessageType.Warning);
            }

            EditorGUILayout.Space(4f);
            DrawPreviewControls();
            EditorGUILayout.Space(4f);
            DrawMobileBoundsControls();
        }

        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    private void DrawPreviewControls()
    {
        float totalDuration = GetPreviewTotalDuration();
        int previewCount = GetEditorEffectiveEnemyCount();
        EditorGUILayout.LabelField(
            "Preview Duration",
            $"{totalDuration:0.00}s");
        EditorGUILayout.LabelField("Preview Enemy Count", previewCount.ToString());

        using (new EditorGUILayout.HorizontalScope())
        {
            GUI.enabled = !previewPlaying && previewCount > 0 && totalDuration > 0f;
            if (GUILayout.Button("Preview Wave"))
                StartPreview();

            GUI.enabled = previewPlaying;
            if (GUILayout.Button("Stop Preview"))
                StopPreview();

            GUI.enabled = true;

            if (GUILayout.Button("Frame Preview"))
                FramePreviewArea();
        }

        if (previewCount <= 0)
        {
            EditorGUILayout.HelpBox(
                "Preview cannot play because enemy count is 0. For Free formation, add Final Points/Slots.",
                MessageType.Warning);
        }
        else if (totalDuration <= 0f)
        {
            EditorGUILayout.HelpBox(
                "Preview cannot play because total duration is 0. Add path duration, settle duration, or spawn interval.",
                MessageType.Warning);
        }

        if (previewPlaying)
        {
            float elapsed = GetPreviewElapsed();
            EditorGUILayout.HelpBox(
                $"Previewing wave: {elapsed:0.00}s / {totalDuration:0.00}s",
                MessageType.Info);
        }
    }

    private void DrawMobileBoundsControls()
    {
        EditorGUILayout.LabelField("Mobile Screen Bounds", EditorStyles.boldLabel);

        EditorGUI.BeginChangeCheck();

        showMobileBounds = EditorGUILayout.Toggle(
            "Show Mobile Bounds",
            showMobileBounds);
        mobileBoundsOrthoSize = Mathf.Max(
            0.01f,
            EditorGUILayout.FloatField(
                "Orthographic Size",
                mobileBoundsOrthoSize));
        mobileBoundsAspect = Mathf.Max(
            0.01f,
            EditorGUILayout.FloatField(
                "Aspect",
                mobileBoundsAspect));
        mobileBoundsCenter = EditorGUILayout.Vector2Field(
            "Center",
            mobileBoundsCenter);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("9:16"))
                mobileBoundsAspect = 9f / 16f;

            if (GUILayout.Button("9:19.5"))
                mobileBoundsAspect = 9f / 19.5f;

            if (GUILayout.Button("Use Scene Camera"))
                ApplySceneCameraBounds();
        }

        if (EditorGUI.EndChangeCheck())
        {
            SaveMobileBoundsPrefs();
            SceneView.RepaintAll();
        }
    }

    private void ApplySceneCameraBounds()
    {
        Camera camera = Camera.main;
        if (camera == null)
            camera = SceneView.lastActiveSceneView?.camera;

        if (camera == null)
            return;

        mobileBoundsOrthoSize = camera.orthographic
            ? camera.orthographicSize
            : mobileBoundsOrthoSize;
        mobileBoundsAspect = Mathf.Max(0.01f, camera.aspect);
        mobileBoundsCenter = camera.transform.position;
        SaveMobileBoundsPrefs();
        SceneView.RepaintAll();
    }

    private void SaveMobileBoundsPrefs()
    {
        EditorPrefs.SetBool(ShowMobileBoundsKey, showMobileBounds);
        EditorPrefs.SetFloat(MobileBoundsOrthoSizeKey, mobileBoundsOrthoSize);
        EditorPrefs.SetFloat(MobileBoundsAspectKey, mobileBoundsAspect);
        EditorPrefs.SetFloat(MobileBoundsCenterXKey, mobileBoundsCenter.x);
        EditorPrefs.SetFloat(MobileBoundsCenterYKey, mobileBoundsCenter.y);
    }

    private void StartPreview()
    {
        if (GetEditorEffectiveEnemyCount() <= 0 || GetPreviewTotalDuration() <= 0f)
            return;

        FramePreviewArea();
        previewPlaying = true;
        previewStartTime = EditorApplication.timeSinceStartup;
        EditorApplication.update -= UpdatePreview;
        EditorApplication.update += UpdatePreview;
        SceneView.duringSceneGui -= DrawPreviewDuringSceneGui;
        SceneView.duringSceneGui += DrawPreviewDuringSceneGui;
        SceneView.RepaintAll();
        Repaint();
    }

    private void FramePreviewArea()
    {
        SceneView sceneView = SceneView.lastActiveSceneView;
        if (sceneView == null)
            return;

        Bounds bounds = GetPreviewBounds((DirectedEnemySubWave)target);
        sceneView.Frame(bounds, false);
        SceneView.RepaintAll();
    }

    private Bounds GetPreviewBounds(DirectedEnemySubWave wave)
    {
        Vector3 center = wave != null ? wave.transform.position : Vector3.zero;
        Bounds bounds = new Bounds(center, Vector3.one);

        EditorPathCheckpoint[] checkpoints = GetWorldPathCheckpoints(wave);
        for (int i = 0; i < checkpoints.Length; i++)
            bounds.Encapsulate(checkpoints[i].position);

        int count = Mathf.Max(1, GetEditorEffectiveEnemyCount());
        for (int i = 0; i < count; i++)
            bounds.Encapsulate(GetFormationWorldPosition(i, wave));

        bounds.Expand(1.5f);
        return bounds;
    }

    private void StopPreview()
    {
        if (!previewPlaying)
            return;

        previewPlaying = false;
        EditorApplication.update -= UpdatePreview;
        SceneView.duringSceneGui -= DrawPreviewDuringSceneGui;
        SceneView.RepaintAll();
        Repaint();
    }

    private void UpdatePreview()
    {
        if (!previewPlaying)
            return;

        if (GetPreviewElapsed() >= GetPreviewTotalDuration())
        {
            StopPreview();
            return;
        }

        SceneView.RepaintAll();
        Repaint();
    }

    private void DrawPreviewDuringSceneGui(SceneView sceneView)
    {
        if (!previewPlaying || target == null)
            return;

        serializedObject.Update();
        DrawWavePreview((DirectedEnemySubWave)target);
        serializedObject.ApplyModifiedProperties();
    }

    private void SetPathPoints(params Vector3[] points)
    {
        pathCheckpoints.arraySize = points.Length;

        for (int i = 0; i < points.Length; i++)
        {
            SerializedProperty checkpoint =
                pathCheckpoints.GetArrayElementAtIndex(i);
            checkpoint.FindPropertyRelative("position").vector3Value = points[i];
            checkpoint.FindPropertyRelative("durationToNext").floatValue =
                0.5f;
            checkpoint.FindPropertyRelative("speedToNext").floatValue =
                1f;
            checkpoint.FindPropertyRelative("motionToNext").enumValueIndex =
                (int)DirectedWaveSegmentMotion.CatmullRom;
            checkpoint.FindPropertyRelative("easeToNext").animationCurveValue =
                AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        }

        for (int i = 0; i < points.Length - 1; i++)
            SyncCheckpointDurationAndSpeed(i, true, false, false);
    }

    private Vector3 GetHorizontalLineLocalPosition(int index)
    {
        int count = Mathf.Max(1, GetEditorEffectiveEnemyCount());
        float xSpacing = spacing.vector2Value.x;
        float offset = (count - 1) * xSpacing * 0.5f;
        return formationCenter.vector3Value
            + new Vector3(index * xSpacing - offset, 0f, 0f);
    }

    private void CreateOrMatchTransformPoints()
    {
        DirectedEnemySubWave wave = (DirectedEnemySubWave)target;
        Transform root = formationPointsRoot.objectReferenceValue as Transform;

        if (root == null)
        {
            GameObject rootObject = new GameObject("FormationPoints");
            Undo.RegisterCreatedObjectUndo(rootObject, "Create Formation Points Root");
            rootObject.transform.SetParent(wave.transform);
            rootObject.transform.localPosition = formationCenter.vector3Value;
            rootObject.transform.localRotation = Quaternion.identity;
            rootObject.transform.localScale = Vector3.one;
            root = rootObject.transform;
            formationPointsRoot.objectReferenceValue = root;
        }

        int targetCount = Mathf.Max(1, GetEditorConfiguredEnemyCount());

        while (root.childCount < targetCount)
        {
            GameObject point = new GameObject($"Slot_{root.childCount:00}");
            Undo.RegisterCreatedObjectUndo(point, "Create Formation Slot");
            point.transform.SetParent(root);
            point.transform.localRotation = Quaternion.identity;
            point.transform.localScale = Vector3.one;
            point.transform.position = ToWorld(
                wave,
                GetHorizontalLineLocalPosition(root.childCount),
                (DirectedWaveCoordinateSpace)formationCoordinateSpace.enumValueIndex);
        }

        while (root.childCount > targetCount)
            Undo.DestroyObjectImmediate(root.GetChild(root.childCount - 1).gameObject);
    }

    private void FillTransformPointsFromHorizontal()
    {
        DirectedEnemySubWave wave = (DirectedEnemySubWave)target;
        Transform root = formationPointsRoot.objectReferenceValue as Transform;
        if (root == null)
        {
            CreateOrMatchTransformPoints();
            root = formationPointsRoot.objectReferenceValue as Transform;
        }

        if (root == null)
            return;

        int count = Mathf.Min(
            root.childCount,
            Mathf.Max(1, GetEditorEffectiveEnemyCount()));
        for (int i = 0; i < count; i++)
        {
            Transform child = root.GetChild(i);
            Undo.RecordObject(child, "Move Formation Slot");
            child.position = ToWorld(
                wave,
                GetHorizontalLineLocalPosition(i),
                (DirectedWaveCoordinateSpace)formationCoordinateSpace.enumValueIndex);
        }
    }

    private void OnSceneGUI()
    {
        serializedObject.Update();

        DirectedEnemySubWave wave = (DirectedEnemySubWave)target;

        DrawMobileScreenBounds();
        DrawPathSceneHandles(wave);
        DrawFormationSceneHandles(wave);
        DrawPatrolSceneHandles(wave);
        DrawActivePostCommandPreview(wave);

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawActivePostCommandPreview(DirectedEnemySubWave wave)
    {
        if (wave == null
            || postCommands == null
            || activePostCommandIndex < 0
            || activePostCommandIndex >= postCommands.arraySize)
        {
            return;
        }

        SerializedProperty command =
            postCommands.GetArrayElementAtIndex(activePostCommandIndex);
        if (command == null)
            return;

        Dictionary<int, Vector3> before = GetPreviewInitialPipelinePositions(wave);
        ApplyPreviewCommandsBeforeIndex(wave, before, activePostCommandIndex);

        Dictionary<int, Vector3> after = EvaluatePreviewPipelineCommand(
            wave,
            before,
            command,
            1f,
            GetPreviewCommandDuration(command));

        DirectedWavePostCommandType type =
            (DirectedWavePostCommandType)command
                .FindPropertyRelative("type")
                .enumValueIndex;
        bool enabled = command.FindPropertyRelative("enabled").boolValue;

        DrawPostCommandPreviewOverlay(
            before,
            after,
            activePostCommandIndex,
            type,
            enabled);
    }

    private void ApplyPreviewCommandsBeforeIndex(
        DirectedEnemySubWave wave,
        Dictionary<int, Vector3> positions,
        int commandIndex)
    {
        int safeEnd = Mathf.Clamp(commandIndex, 0, postCommands.arraySize);
        for (int i = 0; i < safeEnd; i++)
        {
            SerializedProperty previous = postCommands.GetArrayElementAtIndex(i);
            if (previous == null
                || !previous.FindPropertyRelative("enabled").boolValue)
            {
                continue;
            }

            ApplyPreviewPipelineCommandFinal(wave, positions, previous);
        }
    }

    private static void DrawPostCommandPreviewOverlay(
        Dictionary<int, Vector3> before,
        Dictionary<int, Vector3> after,
        int commandIndex,
        DirectedWavePostCommandType type,
        bool enabled)
    {
        if (before == null || before.Count == 0)
            return;

        Color beforeColor = new Color(0.15f, 0.85f, 1f, enabled ? 0.85f : 0.35f);
        Color afterColor = new Color(0.2f, 1f, 0.35f, enabled ? 0.95f : 0.35f);
        Color lineColor = new Color(1f, 0.85f, 0.1f, enabled ? 0.9f : 0.3f);
        Vector3 center = Vector3.zero;
        int count = 0;

        Handles.zTest = UnityEngine.Rendering.CompareFunction.Always;

        foreach (KeyValuePair<int, Vector3> pair in before)
        {
            Vector3 start = pair.Value;
            Vector3 end = after != null && after.TryGetValue(pair.Key, out Vector3 value)
                ? value
                : start;

            center += end;
            count++;

            Handles.color = lineColor;
            Handles.DrawAAPolyLine(3f, start, end);

            if ((end - start).sqrMagnitude > 0.0001f)
            {
                Handles.ArrowHandleCap(
                    0,
                    Vector3.Lerp(start, end, 0.72f),
                    Quaternion.LookRotation(Vector3.forward, end - start),
                    0.22f,
                    EventType.Repaint);
            }

            Handles.color = beforeColor;
            Handles.DrawWireDisc(start, Vector3.forward, 0.11f);
            Handles.color = afterColor;
            Handles.DrawSolidDisc(end, Vector3.forward, 0.08f);
        }

        center = count > 0 ? center / count : Vector3.zero;
        Handles.color = Color.white;
        Handles.Label(
            center + Vector3.up * 0.55f,
            $"Editing Post Step #{commandIndex + 1}: {GetPostCommandTypeLabel(type)}"
            + (enabled ? string.Empty : " (disabled)")
            + "\ncyan = before, green = after");
    }

    private void DrawWavePreview(DirectedEnemySubWave wave)
    {
        if (!previewPlaying)
            return;

        float elapsed = GetPreviewElapsed();
        int count = GetEditorEffectiveEnemyCount();

        Handles.zTest = UnityEngine.Rendering.CompareFunction.Always;
        Handles.color = new Color(0.35f, 1f, 0.45f, 0.9f);

        int visibleCount = 0;
        int[] previewSpawnOrder = BuildEditorSpawnOrder(wave, count);
        for (int i = 0; i < count; i++)
        {
            int formationIndex = previewSpawnOrder[i];
            float enemyTime = elapsed - i * Mathf.Max(0f, spawnInterval.floatValue);
            if (enemyTime < 0f)
                continue;

            visibleCount++;
            Vector3 position = GetPreviewEnemyPosition(
                wave,
                formationIndex,
                enemyTime,
                elapsed);
            float radius = Mathf.Lerp(0.11f, 0.18f, Mathf.PingPong(enemyTime * 2f, 1f));

            Handles.color = new Color(0.35f, 1f, 0.45f, 0.35f);
            Handles.DrawAAPolyLine(3f, GetEditorSpawnPosition(wave), position);
            Handles.color = new Color(0.35f, 1f, 0.45f, 0.9f);
            Handles.DrawSolidDisc(position, Vector3.forward, radius * 1.6f);
            Handles.color = Color.black;
            Handles.DrawWireDisc(position, Vector3.forward, radius * 1.9f);
            Handles.color = Color.white;
            Handles.Label(
                position + Vector3.up * 0.22f,
                formationIndex == i
                    ? $"{i}"
                    : $"{i}->{formationIndex}");
            Handles.color = new Color(0.35f, 1f, 0.45f, 0.9f);
        }

        Vector3 labelPosition = wave.transform.position + Vector3.up * 4.5f;
        Handles.color = Color.white;
        Handles.Label(
            labelPosition,
            $"Wave Preview {elapsed:0.00}s / {GetPreviewTotalDuration():0.00}s\n"
            + $"Phase: {GetPreviewPhaseName(elapsed)}\n"
            + $"Visible enemies: {visibleCount}/{count}");

        if (visibleCount == 0)
        {
            Handles.Label(
                labelPosition + Vector3.down * 0.6f,
                "No enemies visible yet. Check Spawn Interval or wait a moment.");
        }
    }

    private int[] BuildEditorSpawnOrder(DirectedEnemySubWave wave, int count)
    {
        count = Mathf.Max(0, count);
        int[] order = new int[count];
        for (int i = 0; i < count; i++)
            order[i] = i;

        DirectedWaveSpawnOrderMode mode =
            (DirectedWaveSpawnOrderMode)spawnOrderMode.enumValueIndex;
        if (count <= 1 || mode == DirectedWaveSpawnOrderMode.Manual)
            return order;

        Vector3[] positions = new Vector3[count];
        Vector3 center = Vector3.zero;
        for (int i = 0; i < count; i++)
        {
            positions[i] = GetFormationWorldPosition(i, wave);
            center += positions[i];
        }

        center /= count;
        System.Array.Sort(
            order,
            (left, right) => CompareEditorSpawnOrderIndices(
                left,
                right,
                positions,
                center,
                mode));

        return order;
    }

    private int CompareEditorSpawnOrderIndices(
        int left,
        int right,
        Vector3[] positions,
        Vector3 center,
        DirectedWaveSpawnOrderMode mode)
    {
        int result = mode switch
        {
            DirectedWaveSpawnOrderMode.DirectionAngle =>
                CompareEditorByDirectionProjection(
                    positions[left],
                    positions[right]),
            DirectedWaveSpawnOrderMode.CenterToOutside =>
                CompareEditorByDistanceFromCenter(
                    positions[left],
                    positions[right],
                    center,
                    false),
            DirectedWaveSpawnOrderMode.OutsideToCenter =>
                CompareEditorByDistanceFromCenter(
                    positions[left],
                    positions[right],
                    center,
                    true),
            DirectedWaveSpawnOrderMode.Clockwise =>
                CompareEditorByAngleAroundCenter(
                    positions[left],
                    positions[right],
                    center,
                    true),
            DirectedWaveSpawnOrderMode.CounterClockwise =>
                CompareEditorByAngleAroundCenter(
                    positions[left],
                    positions[right],
                    center,
                    false),
            _ => left.CompareTo(right)
        };

        return result != 0 ? result : left.CompareTo(right);
    }

    private int CompareEditorByDirectionProjection(Vector3 left, Vector3 right)
    {
        Vector2 direction = GetEditorSpawnOrderDirection(spawnOrderAngle.floatValue);
        float leftProjection = Vector2.Dot(left, direction);
        float rightProjection = Vector2.Dot(right, direction);
        return leftProjection.CompareTo(rightProjection);
    }

    private static int CompareEditorByDistanceFromCenter(
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

    private int CompareEditorByAngleAroundCenter(
        Vector3 left,
        Vector3 right,
        Vector3 center,
        bool clockwise)
    {
        float leftAngle = GetEditorNormalizedSpawnOrderAngle(left - center);
        float rightAngle = GetEditorNormalizedSpawnOrderAngle(right - center);
        int result = leftAngle.CompareTo(rightAngle);
        return clockwise ? result : -result;
    }

    private float GetEditorNormalizedSpawnOrderAngle(Vector3 offset)
    {
        float angle = Mathf.Atan2(offset.y, offset.x) * Mathf.Rad2Deg;
        float delta = Mathf.DeltaAngle(spawnOrderStartAngle.floatValue, angle);
        return Mathf.Repeat(-delta, 360f);
    }

    private static Vector2 GetEditorSpawnOrderDirection(float angleDegrees)
    {
        float radians = angleDegrees * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
    }

    private Vector3 GetPreviewEnemyPosition(
        DirectedEnemySubWave wave,
        int index,
        float enemyTime,
        float previewElapsed)
    {
        EditorPathCheckpoint[] checkpoints = GetWorldPathCheckpoints(wave);
        if (checkpoints.Length > 0)
        {
            Vector3 checkpointPathEnd =
                checkpoints[checkpoints.Length - 1].position;
            float pathDuration = GetCheckpointPathDuration(checkpoints);

            if (enemyTime <= pathDuration)
                return EvaluateCheckpointPath(checkpoints, enemyTime);

            Vector3 checkpointFormation =
                GetFormationWorldPosition(index, wave);
            float checkpointSettleTime =
                Mathf.Max(0f, settleDuration.floatValue);
            if (checkpointSettleTime <= 0f)
                return ApplyPreviewPostBehavior(
                    wave,
                    index,
                    checkpointFormation,
                    previewElapsed);

            float checkpointSettleNormalized = Mathf.Clamp01(
                (enemyTime - pathDuration) / checkpointSettleTime);
            float checkpointSettleCurved = EvaluateCurve(
                settleCurve.animationCurveValue,
                checkpointSettleNormalized);
            Vector3 settledPosition = Vector3.LerpUnclamped(
                checkpointPathEnd,
                checkpointFormation,
                checkpointSettleCurved);

            if (checkpointSettleNormalized < 1f)
                return settledPosition;

            return ApplyPreviewPostBehavior(
                wave,
                index,
                checkpointFormation,
                previewElapsed);
        }

        Vector3 pathEnd = GetEditorSpawnPosition(wave);
        Vector3 formation = GetFormationWorldPosition(index, wave);
        float settleTime = Mathf.Max(0f, settleDuration.floatValue);
        if (settleTime <= 0f)
            return ApplyPreviewPostBehavior(wave, index, formation, previewElapsed);

        float settleNormalized = Mathf.Clamp01(enemyTime / settleTime);
        float settleCurved = EvaluateCurve(settleCurve.animationCurveValue, settleNormalized);
        Vector3 position = Vector3.LerpUnclamped(pathEnd, formation, settleCurved);

        if (settleNormalized < 1f)
            return position;

        return ApplyPreviewPostBehavior(wave, index, formation, previewElapsed);
    }

    private Vector3 ApplyPreviewPostBehavior(
        DirectedEnemySubWave wave,
        int index,
        Vector3 formationPosition,
        float previewElapsed)
    {
        if (postCommands == null || postCommands.arraySize == 0)
            return formationPosition;

        float postBehaviorStart = GetPreviewPostBehaviorStartTime();
        if (previewElapsed < postBehaviorStart)
            return formationPosition;

        float postBehaviorTime = previewElapsed - postBehaviorStart;
        Dictionary<int, Vector3> positions = GetPreviewInitialPipelinePositions(wave);
        float totalDuration = GetPreviewPipelineDuration();
        if (totalDuration <= 0f)
        {
            SimulatePreviewPipelineUntil(
                wave,
                positions,
                postBehaviorTime,
                index,
                out Vector3 backgroundOnlyResult);
            return backgroundOnlyResult;
        }

        if (postCommandPipelineLoop.boolValue && !float.IsInfinity(totalDuration))
        {
            int completedCycles = Mathf.FloorToInt(postBehaviorTime / totalDuration);
            postBehaviorTime -= completedCycles * totalDuration;
            ApplyPreviewCompletedPipelineCycles(wave, positions, completedCycles);
        }
        else if (!float.IsInfinity(totalDuration) && postBehaviorTime >= totalDuration)
        {
            postBehaviorTime = totalDuration;
        }

        SimulatePreviewPipelineUntil(
            wave,
            positions,
            postBehaviorTime,
            index,
            out Vector3 result);

        return result;
    }

    private void ApplyPreviewCompletedPipelineCycles(
        DirectedEnemySubWave wave,
        Dictionary<int, Vector3> positions,
        int completedCycles)
    {
        if (completedCycles <= 0)
            return;

        for (int cycle = 0; cycle < completedCycles; cycle++)
        {
            for (int i = 0; i < postCommands.arraySize; i++)
            {
                SerializedProperty command = postCommands.GetArrayElementAtIndex(i);
                if (!command.FindPropertyRelative("enabled").boolValue)
                    continue;

                if (IsBackgroundParallel(command))
                    continue;

                ApplyPreviewPipelineCommandFinal(wave, positions, command);
            }
        }
    }

    private Dictionary<int, Vector3> GetPreviewInitialPipelinePositions(
        DirectedEnemySubWave wave)
    {
        int count = GetEditorEffectiveEnemyCount();
        Dictionary<int, Vector3> result = new(count);
        for (int i = 0; i < count; i++)
            result[i] = GetFormationWorldPosition(i, wave);

        return result;
    }

    private void SimulatePreviewPipelineUntil(
        DirectedEnemySubWave wave,
        Dictionary<int, Vector3> positions,
        float time,
        int previewIndex,
        out Vector3 result)
    {
        result = positions.TryGetValue(previewIndex, out Vector3 initial)
            ? initial
            : Vector3.zero;
        List<PreviewBackgroundParallelCommand> backgroundCommands = new();
        float timelineCursor = 0f;
        float remainingTime = time;

        for (int i = 0; i < postCommands.arraySize; i++)
        {
            SerializedProperty command = postCommands.GetArrayElementAtIndex(i);
            if (!command.FindPropertyRelative("enabled").boolValue)
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

            float duration = GetPreviewCommandDuration(command);
            float hold = Mathf.Max(
                0f,
                command.FindPropertyRelative("holdDuration").floatValue);

            if (remainingTime <= duration)
            {
                Dictionary<int, Vector3> frame = EvaluatePreviewPipelineCommand(
                    wave,
                    positions,
                    command,
                    float.IsInfinity(duration)
                        ? 0f
                        : Mathf.Clamp01(remainingTime / duration),
                    remainingTime);
                frame = ApplyPreviewBackgroundParallels(
                    wave,
                    frame,
                    backgroundCommands,
                    timelineCursor + remainingTime);
                result = frame.TryGetValue(previewIndex, out Vector3 current)
                    ? current
                    : result;
                return;
            }

            ApplyPreviewPipelineCommandFinal(wave, positions, command);
            remainingTime -= duration;
            timelineCursor += duration;

            if (remainingTime <= hold)
            {
                Dictionary<int, Vector3> frame = ApplyPreviewBackgroundParallels(
                    wave,
                    positions,
                    backgroundCommands,
                    timelineCursor + remainingTime);
                result = frame.TryGetValue(previewIndex, out Vector3 held)
                    ? held
                    : result;
                return;
            }

            remainingTime -= hold;
            timelineCursor += hold;
        }

        Dictionary<int, Vector3> finalFrame = ApplyPreviewBackgroundParallels(
            wave,
            positions,
            backgroundCommands,
            timelineCursor + remainingTime);
        result = finalFrame.TryGetValue(previewIndex, out Vector3 final)
            ? final
            : result;
    }

    private sealed class PreviewBackgroundParallelCommand
    {
        public SerializedProperty command;
        public float startTime;
    }

    private Dictionary<int, Vector3> ApplyPreviewBackgroundParallels(
        DirectedEnemySubWave wave,
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
                && elapsed > GetPreviewCommandDuration(background.command))
            {
                continue;
            }

            frame = EvaluatePreviewParallelCommand(
                wave,
                frame,
                background.command,
                elapsed,
                false);
        }

        return frame;
    }

    private Dictionary<int, Vector3> EvaluatePreviewPipelineCommand(
        DirectedEnemySubWave wave,
        Dictionary<int, Vector3> positions,
        SerializedProperty command,
        float normalizedTime,
        float elapsedInCommand)
    {
        DirectedWavePostCommandType type =
            (DirectedWavePostCommandType)command
                .FindPropertyRelative("type")
                .enumValueIndex;
        AnimationCurve curve = command.FindPropertyRelative("curve").animationCurveValue;
        float curved = EvaluateCurve(curve, normalizedTime);

        return type switch
        {
            DirectedWavePostCommandType.LocalMovement =>
                LerpPreviewPositions(
                    positions,
                    GetPreviewMoveTargetPositions(wave, command, positions),
                    curved),
            DirectedWavePostCommandType.Patrol =>
                OffsetPreviewPositions(
                    positions,
                    GetPreviewPatrolOffset(elapsedInCommand)),
            DirectedWavePostCommandType.FormationRotation =>
                RotatePreviewPositions(
                    positions,
                    GetPreviewPositionsCenter(positions),
                    GetPreviewFormationRotationAngle(
                        command,
                        elapsedInCommand,
                        GetPreviewCommandDuration(command),
                        curved)),
            DirectedWavePostCommandType.FormationMorph =>
                LerpPreviewPositions(
                    positions,
                    GetPreviewMorphTargetPositions(wave, command, positions),
                    curved),
            DirectedWavePostCommandType.Wobble =>
                ApplyPreviewWobbleOverlay(wave, positions, elapsedInCommand),
            DirectedWavePostCommandType.CircularMovement =>
                ApplyPreviewCircularOverlay(positions, elapsedInCommand),
            DirectedWavePostCommandType.Parallel =>
                EvaluatePreviewParallelCommand(
                    wave,
                    positions,
                    command,
                    elapsedInCommand,
                    false),
            DirectedWavePostCommandType.Loop =>
                EvaluatePreviewLoopCommand(
                    wave,
                    positions,
                    command,
                    elapsedInCommand),
            _ => new Dictionary<int, Vector3>(positions)
        };
    }

    private void ApplyPreviewPipelineCommandFinal(
        DirectedEnemySubWave wave,
        Dictionary<int, Vector3> positions,
        SerializedProperty command)
    {
        DirectedWavePostCommandType type =
            (DirectedWavePostCommandType)command
                .FindPropertyRelative("type")
                .enumValueIndex;

        Dictionary<int, Vector3> final = type switch
        {
            DirectedWavePostCommandType.LocalMovement =>
                GetPreviewMoveTargetPositions(wave, command, positions),
            DirectedWavePostCommandType.Patrol =>
                OffsetPreviewPositions(
                    positions,
                    GetPreviewPatrolOffset(GetPreviewCommandDuration(command))),
            DirectedWavePostCommandType.FormationRotation =>
                RotatePreviewPositions(
                    positions,
                    GetPreviewPositionsCenter(positions),
                    GetPreviewFormationRotationAngle(
                        command,
                        GetPreviewCommandDuration(command),
                        GetPreviewCommandDuration(command),
                        1f)),
            DirectedWavePostCommandType.FormationMorph =>
                GetPreviewMorphTargetPositions(wave, command, positions),
            DirectedWavePostCommandType.Parallel =>
                EvaluatePreviewParallelCommand(
                    wave,
                    positions,
                    command,
                    GetPreviewCommandDuration(command),
                    true),
            DirectedWavePostCommandType.Loop =>
                EvaluatePreviewLoopCommand(
                    wave,
                    positions,
                    command,
                    float.IsInfinity(GetPreviewCommandDuration(command))
                        ? InfiniteParallelPreviewExtraDuration
                        : GetPreviewCommandDuration(command)),
            _ => positions
        };

        ReplacePreviewPositions(positions, final);
    }

    private Dictionary<int, Vector3> EvaluatePreviewParallelCommand(
        DirectedEnemySubWave wave,
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
        float parallelDuration = GetPreviewCommandDuration(command);
        for (int i = 0; i < parallelCommands.arraySize; i++)
        {
            SerializedProperty child = parallelCommands.GetArrayElementAtIndex(i);
            if (child == null || !child.FindPropertyRelative("enabled").boolValue)
                continue;

            DirectedWavePostCommandType childType =
                (DirectedWavePostCommandType)child
                    .FindPropertyRelative("type")
                    .enumValueIndex;
            if (childType == DirectedWavePostCommandType.Parallel)
                continue;

            if (finalFrame
                && (childType == DirectedWavePostCommandType.Wobble
                    || childType == DirectedWavePostCommandType.CircularMovement))
            {
                continue;
            }

            float childDuration = Mathf.Max(
                0.01f,
                child.FindPropertyRelative("duration").floatValue);
            float childElapsed =
                childType == DirectedWavePostCommandType.Patrol
                || childType == DirectedWavePostCommandType.Wobble
                || childType == DirectedWavePostCommandType.CircularMovement
                || child.FindPropertyRelative("continuousFormationRotation").boolValue
                    ? Mathf.Min(elapsed, parallelDuration)
                    : Mathf.Min(elapsed, childDuration);
            float normalized = Mathf.Clamp01(childElapsed / childDuration);

            frame = EvaluatePreviewPipelineCommand(
                wave,
                frame,
                child,
                normalized,
                childElapsed);
        }

        return frame;
    }

    private Dictionary<int, Vector3> EvaluatePreviewLoopCommand(
        DirectedEnemySubWave wave,
        Dictionary<int, Vector3> positions,
        SerializedProperty command,
        float elapsed)
    {
        SerializedProperty loopCommands =
            command.FindPropertyRelative("loopCommands");
        if (loopCommands == null || loopCommands.arraySize == 0)
            return new Dictionary<int, Vector3>(positions);

        Dictionary<int, Vector3> frame = new(positions);
        float iterationDuration = GetPreviewCommandArrayDuration(loopCommands);
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
            {
                return EvaluatePreviewCommandArrayUntil(
                    wave,
                    frame,
                    loopCommands,
                    remaining);
            }

            ApplyPreviewCommandArrayFinal(wave, frame, loopCommands);
            remaining -= iterationDuration;
        }

        return frame;
    }

    private Dictionary<int, Vector3> EvaluatePreviewCommandArrayUntil(
        DirectedEnemySubWave wave,
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

            if (GetSerializedPostCommandType(child) == DirectedWavePostCommandType.Loop)
                continue;

            float duration = GetPreviewCommandDuration(child);
            float hold = Mathf.Max(
                0f,
                child.FindPropertyRelative("holdDuration").floatValue);

            if (remaining <= duration)
            {
                return EvaluatePreviewPipelineCommand(
                    wave,
                    frame,
                    child,
                    float.IsInfinity(duration)
                        ? 0f
                        : Mathf.Clamp01(remaining / duration),
                    remaining);
            }

            ApplyPreviewPipelineCommandFinal(wave, frame, child);
            remaining -= duration;

            if (remaining <= hold)
                return frame;

            remaining -= hold;
        }

        return frame;
    }

    private void ApplyPreviewCommandArrayFinal(
        DirectedEnemySubWave wave,
        Dictionary<int, Vector3> positions,
        SerializedProperty commands)
    {
        for (int i = 0; i < commands.arraySize; i++)
        {
            SerializedProperty child = commands.GetArrayElementAtIndex(i);
            if (!IsPostCommandEnabled(child) || IsBackgroundParallel(child))
                continue;

            if (GetSerializedPostCommandType(child) == DirectedWavePostCommandType.Loop)
                continue;

            ApplyPreviewPipelineCommandFinal(wave, positions, child);
        }
    }

    private float GetPreviewCommandArrayDuration(SerializedProperty commands)
    {
        if (commands == null)
            return 0f;

        float duration = 0f;
        for (int i = 0; i < commands.arraySize; i++)
        {
            SerializedProperty child = commands.GetArrayElementAtIndex(i);
            if (!IsPostCommandEnabled(child) || IsBackgroundParallel(child))
                continue;

            if (GetSerializedPostCommandType(child) == DirectedWavePostCommandType.Loop)
                continue;

            duration += GetPreviewCommandDuration(child);
            if (float.IsInfinity(duration))
                return duration;

            duration += Mathf.Max(
                0f,
                child.FindPropertyRelative("holdDuration").floatValue);
        }

        return duration;
    }

    private float GetPreviewFormationRotationAngle(
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
                : formationRotationDegreesPerSecond.floatValue;
            return degreesPerSecond * elapsed;
        }

        float totalAngle = Mathf.Abs(rotationValue) > 0.0001f
            ? rotationValue
            : duration * formationRotationDegreesPerSecond.floatValue;
        return totalAngle * curved;
    }

    private float GetPreviewCommandDuration(SerializedProperty command)
    {
        if (IsInfiniteParallel(command))
            return Mathf.Infinity;

        DirectedWavePostCommandType type =
            (DirectedWavePostCommandType)command
                .FindPropertyRelative("type")
                .enumValueIndex;
        if (type == DirectedWavePostCommandType.Loop)
        {
            if (command.FindPropertyRelative("infiniteLoop").boolValue)
                return Mathf.Infinity;

            float iterationDuration = GetPreviewCommandArrayDuration(
                command.FindPropertyRelative("loopCommands"));
            return iterationDuration
                * Mathf.Max(1, command.FindPropertyRelative("loopCount").intValue);
        }

        return Mathf.Max(
            0.01f,
            command.FindPropertyRelative("duration").floatValue);
    }

    private bool IsBackgroundParallel(SerializedProperty command)
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

    private static bool IsPostCommandEnabled(SerializedProperty command)
    {
        return command != null
            && command.FindPropertyRelative("enabled") != null
            && command.FindPropertyRelative("enabled").boolValue;
    }

    private static DirectedWavePostCommandType GetSerializedPostCommandType(
        SerializedProperty command)
    {
        SerializedProperty type = command.FindPropertyRelative("type");
        return type != null
            ? (DirectedWavePostCommandType)type.enumValueIndex
            : DirectedWavePostCommandType.Wait;
    }

    private bool IsInfiniteParallel(SerializedProperty command)
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

    private bool HasInfiniteParallel(SerializedProperty commands)
    {
        return HasInfiniteParallel(commands, 0);
    }

    private bool HasInfiniteParallel(SerializedProperty commands, int depth)
    {
        if (commands == null || depth > 8)
            return false;

        for (int i = 0; i < commands.arraySize; i++)
        {
            SerializedProperty command = commands.GetArrayElementAtIndex(i);
            if (command == null
                || !command.FindPropertyRelative("enabled").boolValue)
            {
                continue;
            }

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

    private bool HasInfiniteLoop(SerializedProperty commands)
    {
        return HasInfiniteLoop(commands, 0);
    }

    private bool HasInfiniteLoop(SerializedProperty commands, int depth)
    {
        if (commands == null || depth > 8)
            return false;

        for (int i = 0; i < commands.arraySize; i++)
        {
            SerializedProperty command = commands.GetArrayElementAtIndex(i);
            if (!IsPostCommandEnabled(command))
                continue;

            SerializedProperty type = command.FindPropertyRelative("type");
            SerializedProperty infiniteLoop =
                command.FindPropertyRelative("infiniteLoop");
            if (type != null
                && infiniteLoop != null
                && type.enumValueIndex == (int)DirectedWavePostCommandType.Loop
                && infiniteLoop.boolValue)
            {
                return true;
            }

            if (HasInfiniteLoop(command.FindPropertyRelative("parallelCommands"), depth + 1))
                return true;

            if (HasInfiniteLoop(command.FindPropertyRelative("loopCommands"), depth + 1))
                return true;
        }

        return false;
    }

    private float GetPreviewPipelineDuration()
    {
        if (postCommands == null || postCommands.arraySize == 0)
            return 0f;

        float total = 0f;
        for (int i = 0; i < postCommands.arraySize; i++)
        {
            SerializedProperty command = postCommands.GetArrayElementAtIndex(i);
            if (!command.FindPropertyRelative("enabled").boolValue)
                continue;

            if (IsBackgroundParallel(command))
                continue;

            total += GetPreviewCommandDuration(command);
            if (float.IsInfinity(total))
                return total;

            total += Mathf.Max(
                0f,
                command.FindPropertyRelative("holdDuration").floatValue);
        }

        return total;
    }

    private Dictionary<int, Vector3> GetPreviewMoveTargetPositions(
        DirectedEnemySubWave wave,
        SerializedProperty command,
        Dictionary<int, Vector3> positions)
    {
        Vector3 currentCenter = GetPreviewPositionsCenter(positions);
        Vector3 targetCenter = GetPreviewFormationCenter(wave)
            + command.FindPropertyRelative("targetOffset").vector3Value;
        return OffsetPreviewPositions(positions, targetCenter - currentCenter);
    }

    private Dictionary<int, Vector3> GetPreviewMorphTargetPositions(
        DirectedEnemySubWave wave,
        SerializedProperty command,
        Dictionary<int, Vector3> positions)
    {
        SerializedProperty morphTarget = command.FindPropertyRelative("morphTarget");
        Vector3[] targets = CreatePreviewMorphShapePositions(
            wave,
            morphTarget,
            positions.Count);
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

    private Vector3[] CreatePreviewMorphShapePositions(
        DirectedEnemySubWave wave,
        SerializedProperty morphTarget,
        int count)
    {
        count = Mathf.Max(1, count);
        Vector3[] result = new Vector3[count];
        Vector3 center = GetPreviewFormationCenter(wave)
            + morphTarget.FindPropertyRelative("centerOffset").vector3Value;
        Vector2 flattening = morphTarget
            .FindPropertyRelative("shapeFlattening")
            .vector2Value;
        flattening = new Vector2(
            Mathf.Max(0.01f, flattening.x),
            Mathf.Max(0.01f, flattening.y));

        for (int i = 0; i < count; i++)
            result[i] = GetPreviewMorphShapePosition(
                i,
                count,
                center,
                morphTarget,
                flattening);

        return result;
    }

    private Vector3 GetPreviewMorphShapePosition(
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

        switch (layout)
        {
            case DirectedWaveFormationLayout.VerticalLine:
                return GetPreviewMorphLinePosition(index, count, center, radius, true);
            case DirectedWaveFormationLayout.Grid:
                return GetPreviewMorphGridPosition(index, count, center, morphTarget);
            case DirectedWaveFormationLayout.VShape:
                return GetPreviewMorphVShapePosition(index, center, radius);
            case DirectedWaveFormationLayout.Arc:
                return GetPreviewMorphArcPosition(index, count, center, morphTarget, flattening);
            case DirectedWaveFormationLayout.Circle:
                return GetPreviewMorphCirclePosition(index, count, center, radius, flattening);
            case DirectedWaveFormationLayout.Triangle:
            {
                Vector3 local = GetPreviewPolygonPoint(index, count, GetPreviewUnitTriangleVertices())
                    * radius;
                local.x *= flattening.x;
                local.y *= flattening.y;
                return center + local;
            }
            case DirectedWaveFormationLayout.Square:
            {
                Vector3 local = GetPreviewPolygonPoint(index, count, GetPreviewUnitSquareVertices())
                    * radius;
                local.x *= flattening.x;
                local.y *= flattening.y;
                return center + local;
            }
            case DirectedWaveFormationLayout.Diamond:
            {
                Vector3 local = GetPreviewPolygonPoint(index, count, GetPreviewUnitDiamondVertices())
                    * radius;
                local.x *= flattening.x;
                local.y *= flattening.y;
                return center + local;
            }
            case DirectedWaveFormationLayout.CustomPoints:
                return GetPreviewMorphCustomPoint(index, center, morphTarget);
            default:
                return GetPreviewMorphLinePosition(index, count, center, radius, false);
        }
    }

    private static Vector3 GetPreviewMorphLinePosition(
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

    private static Vector3 GetPreviewMorphVShapePosition(
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

    private static Vector3 GetPreviewMorphGridPosition(
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

    private static Vector3 GetPreviewMorphArcPosition(
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

    private static Vector3 GetPreviewMorphCirclePosition(
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

    private static Vector3 GetPreviewPolygonPoint(
        int index,
        int count,
        Vector3[] vertices)
    {
        if (count <= 1 || vertices == null || vertices.Length == 0)
            return Vector3.zero;

        float totalLength = 0f;
        for (int i = 0; i < vertices.Length; i++)
            totalLength += Vector3.Distance(
                vertices[i],
                vertices[(i + 1) % vertices.Length]);

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

    private static Vector3[] GetPreviewUnitTriangleVertices()
    {
        return new[]
        {
            GetPreviewUnitShapePoint(90f),
            GetPreviewUnitShapePoint(210f),
            GetPreviewUnitShapePoint(330f)
        };
    }

    private static Vector3[] GetPreviewUnitSquareVertices()
    {
        return new[]
        {
            new Vector3(-1f, 1f, 0f),
            new Vector3(1f, 1f, 0f),
            new Vector3(1f, -1f, 0f),
            new Vector3(-1f, -1f, 0f)
        };
    }

    private static Vector3[] GetPreviewUnitDiamondVertices()
    {
        return new[]
        {
            Vector3.up,
            Vector3.right,
            Vector3.down,
            Vector3.left
        };
    }

    private static Vector3 GetPreviewUnitShapePoint(float angleDegrees)
    {
        float radians = angleDegrees * Mathf.Deg2Rad;
        return new Vector3(Mathf.Cos(radians), Mathf.Sin(radians), 0f);
    }

    private static Vector3 GetPreviewMorphCustomPoint(
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

    private Dictionary<int, Vector3> ApplyPreviewWobbleOverlay(
        DirectedEnemySubWave wave,
        Dictionary<int, Vector3> positions,
        float elapsed)
    {
        Dictionary<int, Vector3> result = new(positions.Count);
        float leading = GetPreviewLeadingWobbleProjection(positions);
        foreach (KeyValuePair<int, Vector3> pair in positions)
        {
            result[pair.Key] = pair.Value + GetPreviewWobbleOffset(
                pair.Key,
                pair.Value,
                elapsed,
                leading);
        }

        return result;
    }

    private Dictionary<int, Vector3> ApplyPreviewCircularOverlay(
        Dictionary<int, Vector3> positions,
        float elapsed)
    {
        Dictionary<int, Vector3> result = new(positions.Count);
        foreach (KeyValuePair<int, Vector3> pair in positions)
            result[pair.Key] = pair.Value + GetPreviewCircularMovementOffset(
                pair.Key,
                elapsed);

        return result;
    }

    private static Dictionary<int, Vector3> OffsetPreviewPositions(
        Dictionary<int, Vector3> positions,
        Vector3 offset)
    {
        Dictionary<int, Vector3> result = new(positions.Count);
        foreach (KeyValuePair<int, Vector3> pair in positions)
            result[pair.Key] = pair.Value + offset;

        return result;
    }

    private static Dictionary<int, Vector3> RotatePreviewPositions(
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

    private static Dictionary<int, Vector3> LerpPreviewPositions(
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

    private static void ReplacePreviewPositions(
        Dictionary<int, Vector3> target,
        Dictionary<int, Vector3> source)
    {
        target.Clear();
        foreach (KeyValuePair<int, Vector3> pair in source)
            target[pair.Key] = pair.Value;
    }

    private static Vector3 GetPreviewPositionsCenter(
        Dictionary<int, Vector3> positions)
    {
        if (positions == null || positions.Count == 0)
            return Vector3.zero;

        Vector3 center = Vector3.zero;
        foreach (Vector3 position in positions.Values)
            center += position;

        return center / positions.Count;
    }

    private Vector3 GetPreviewFormationRotationOffset(
        DirectedEnemySubWave wave,
        Vector3 formationPosition,
        float postBehaviorTime)
    {
        Vector3 center = GetPreviewFormationCenter(wave);
        Vector3 relative = formationPosition - center;
        float angle = postBehaviorTime
            * formationRotationDegreesPerSecond.floatValue
            * Mathf.Deg2Rad;
        float sin = Mathf.Sin(angle);
        float cos = Mathf.Cos(angle);
        Vector3 rotated = new Vector3(
            relative.x * cos - relative.y * sin,
            relative.x * sin + relative.y * cos,
            relative.z);

        return rotated - relative;
    }

    private Vector3 GetPreviewFormationCenter(DirectedEnemySubWave wave)
    {
        int count = GetEditorEffectiveEnemyCount();
        if (count <= 0)
            return wave != null ? wave.transform.position : Vector3.zero;

        Vector3 center = Vector3.zero;
        for (int i = 0; i < count; i++)
            center += GetFormationWorldPosition(i, wave);

        return center / count;
    }

    private Vector3 GetPreviewCircularMovementOffset(
        int index,
        float postBehaviorTime)
    {
        Vector2 radius = selfOrbitRadius.vector2Value;
        float phase = index * selfOrbitPhaseOffset.floatValue;
        float angle = postBehaviorTime
            * selfRotationDegreesPerSecond.floatValue
            * Mathf.Deg2Rad
            + phase;

        return new Vector3(
            (Mathf.Cos(angle) - Mathf.Cos(phase)) * radius.x,
            (Mathf.Sin(angle) - Mathf.Sin(phase)) * radius.y,
            0f);
    }

    private Vector3 GetPreviewLocalMovementOffset(float postBehaviorTime)
    {
        float duration = Mathf.Max(0.01f, localMovementDuration.floatValue);
        float normalized = postBehaviorTime / duration;

        if (localMovementPingPong.boolValue)
            normalized = Mathf.PingPong(normalized, 1f);
        else if (localMovementLoop.boolValue)
            normalized = Mathf.Repeat(normalized, 1f);
        else
            normalized = Mathf.Clamp01(normalized);

        float curved = EvaluateCurve(
            localMovementCurve.animationCurveValue,
            normalized);
        return localMovementOffset.vector3Value * curved;
    }

    private Vector3 GetPreviewWobbleOffset(
        DirectedEnemySubWave wave,
        int index,
        Vector3 formationPosition,
        float postBehaviorTime)
    {
        float phase = GetPreviewWobblePhase(wave, index, formationPosition);
        float frequency = Mathf.Max(0f, wobbleFrequency.floatValue);
        float angle = postBehaviorTime * frequency + phase;
        Vector2 amplitude = wobbleAmplitude.vector2Value;

        return new Vector3(
            (Mathf.Sin(angle) - Mathf.Sin(phase)) * amplitude.x,
            (Mathf.Cos(angle) - Mathf.Cos(phase)) * amplitude.y,
            0f);
    }

    private Vector3 GetPreviewWobbleOffset(
        int index,
        Vector3 formationPosition,
        float postBehaviorTime,
        float leadingProjection)
    {
        float phase = GetPreviewWobblePhase(index, formationPosition, leadingProjection);
        float frequency = Mathf.Max(0f, wobbleFrequency.floatValue);
        float angle = postBehaviorTime * frequency + phase;
        Vector2 amplitude = wobbleAmplitude.vector2Value;

        return new Vector3(
            (Mathf.Sin(angle) - Mathf.Sin(phase)) * amplitude.x,
            (Mathf.Cos(angle) - Mathf.Cos(phase)) * amplitude.y,
            0f);
    }

    private float GetPreviewWobblePhase(
        DirectedEnemySubWave wave,
        int index,
        Vector3 formationPosition)
    {
        if ((DirectedWaveWobblePhaseMode)wobblePhaseMode.enumValueIndex
            != DirectedWaveWobblePhaseMode.Directional)
        {
            return index * wobblePhaseOffset.floatValue;
        }

        Vector2 direction = GetPreviewWobbleDirection();
        float leadingProjection = GetPreviewLeadingWobbleProjection(wave, direction);
        float projection = Vector2.Dot(
            new Vector2(formationPosition.x, formationPosition.y),
            direction);
        float distanceFromWaveStart = projection - leadingProjection;
        float step = Mathf.Max(0.01f, wobbleDirectionStep.floatValue);

        return distanceFromWaveStart / step * wobblePhaseOffset.floatValue;
    }

    private float GetPreviewWobblePhase(
        int index,
        Vector3 formationPosition,
        float leadingProjection)
    {
        if ((DirectedWaveWobblePhaseMode)wobblePhaseMode.enumValueIndex
            != DirectedWaveWobblePhaseMode.Directional)
        {
            return index * wobblePhaseOffset.floatValue;
        }

        Vector2 direction = GetPreviewWobbleDirection();
        float projection = Vector2.Dot(
            new Vector2(formationPosition.x, formationPosition.y),
            direction);
        float distanceFromWaveStart = projection - leadingProjection;
        float step = Mathf.Max(0.01f, wobbleDirectionStep.floatValue);

        return distanceFromWaveStart / step * wobblePhaseOffset.floatValue;
    }

    private float GetPreviewLeadingWobbleProjection(
        DirectedEnemySubWave wave,
        Vector2 direction)
    {
        int count = GetEditorEffectiveEnemyCount();
        if (count <= 0)
            return 0f;

        float leadingProjection = float.PositiveInfinity;
        for (int i = 0; i < count; i++)
        {
            Vector3 position = GetFormationWorldPosition(i, wave);
            float projection = Vector2.Dot(
                new Vector2(position.x, position.y),
                direction);
            if (projection < leadingProjection)
                leadingProjection = projection;
        }

        return float.IsPositiveInfinity(leadingProjection)
            ? 0f
            : leadingProjection;
    }

    private float GetPreviewLeadingWobbleProjection(
        Dictionary<int, Vector3> positions)
    {
        if ((DirectedWaveWobblePhaseMode)wobblePhaseMode.enumValueIndex
            != DirectedWaveWobblePhaseMode.Directional)
        {
            return 0f;
        }

        Vector2 direction = GetPreviewWobbleDirection();
        float leadingProjection = float.PositiveInfinity;
        foreach (Vector3 position in positions.Values)
        {
            float projection = Vector2.Dot(
                new Vector2(position.x, position.y),
                direction);
            if (projection < leadingProjection)
                leadingProjection = projection;
        }

        return float.IsPositiveInfinity(leadingProjection)
            ? 0f
            : leadingProjection;
    }

    private Vector2 GetPreviewWobbleDirection()
    {
        float radians = wobbleDirectionAngle.floatValue * Mathf.Deg2Rad;
        Vector2 direction = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
        return direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;
    }

    private bool PreviewUsesWobble()
    {
        return HasPostCommand(DirectedWavePostCommandType.Wobble);
    }

    private bool PreviewUsesPatrol()
    {
        return HasPostCommand(DirectedWavePostCommandType.Patrol);
    }

    private bool PreviewUsesLocalMovement()
    {
        return HasPostCommand(DirectedWavePostCommandType.LocalMovement);
    }

    private bool PreviewUsesCircularMovement()
    {
        return HasPostCommand(DirectedWavePostCommandType.CircularMovement);
    }

    private bool PreviewUsesFormationRotation()
    {
        return HasPostCommand(DirectedWavePostCommandType.FormationRotation);
    }

    private string GetPreviewPhaseName(float elapsed)
    {
        if (postCommands == null || postCommands.arraySize == 0)
            return "Entrance / Formation";

        float postBehaviorStart = GetPreviewPostBehaviorStartTime();
        if (elapsed < postBehaviorStart)
            return "Entrance / Formation";

        float pipelineTime = elapsed - postBehaviorStart;
        float pipelineDuration = GetPreviewPipelineDuration();
        if (pipelineDuration <= 0f)
            return "Entrance / Formation";

        if (postCommandPipelineLoop.boolValue)
            pipelineTime = Mathf.Repeat(pipelineTime, pipelineDuration);
        else
            pipelineTime = Mathf.Min(pipelineTime, pipelineDuration);

        for (int i = 0; i < postCommands.arraySize; i++)
        {
            SerializedProperty command = postCommands.GetArrayElementAtIndex(i);
            if (!command.FindPropertyRelative("enabled").boolValue)
                continue;

            DirectedWavePostCommandType type =
                (DirectedWavePostCommandType)command
                    .FindPropertyRelative("type")
                    .enumValueIndex;
            float duration = GetPreviewCommandDuration(command);
            float hold = Mathf.Max(
                0f,
                command.FindPropertyRelative("holdDuration").floatValue);

            if (pipelineTime <= duration)
                return $"Post Pipeline / {i + 1}: {GetPostCommandTypeLabel(type)}";

            pipelineTime -= duration;
            if (pipelineTime <= hold)
                return $"Post Pipeline / {i + 1}: Hold";

            pipelineTime -= hold;
        }

        return "Post Pipeline / Complete";
    }

    private static string GetPostCommandTypeLabel(DirectedWavePostCommandType type)
    {
        return type switch
        {
            DirectedWavePostCommandType.Patrol => "Patrol",
            DirectedWavePostCommandType.LocalMovement => "Local Movement",
            DirectedWavePostCommandType.Wobble => "Wobble",
            DirectedWavePostCommandType.Attack => "Attack",
            DirectedWavePostCommandType.CircularMovement => "Circular Movement",
            DirectedWavePostCommandType.FormationRotation => "Formation Rotation",
            DirectedWavePostCommandType.FormationMorph => "Formation Morph",
            DirectedWavePostCommandType.Wait => "Wait",
            DirectedWavePostCommandType.Parallel => "Parallel",
            DirectedWavePostCommandType.Loop => "Loop",
            _ => type.ToString()
        };
    }

    private void DrawPathSceneHandles(DirectedEnemySubWave wave)
    {
        if (pathCheckpoints == null || pathCheckpoints.arraySize == 0)
            return;

        DrawCheckpointSceneHandles(wave);
    }

    private void DrawCheckpointSceneHandles(DirectedEnemySubWave wave)
    {
        Handles.color = Color.cyan;

        Vector3 previous = Vector3.zero;
        bool hasPrevious = false;

        for (int i = 0; i < pathCheckpoints.arraySize; i++)
        {
            SerializedProperty checkpoint =
                pathCheckpoints.GetArrayElementAtIndex(i);
            SerializedProperty position =
                checkpoint.FindPropertyRelative("position");
            Vector3 world = ToWorld(
                wave,
                position.vector3Value,
                (DirectedWaveCoordinateSpace)pathCoordinateSpace.enumValueIndex);

            EditorGUI.BeginChangeCheck();
            Vector3 changedWorld = Handles.PositionHandle(
                world,
                Quaternion.identity);

            Handles.Label(
                changedWorld + Vector3.up * 0.15f,
                $"Checkpoint {i}");

            if (activePathCheckpointIndex == i)
            {
                DrawScenePointHighlight(
                    changedWorld,
                    $"Checkpoint {i}",
                    Color.cyan);
            }

            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(target, "Move Directed Wave Checkpoint");
                position.vector3Value = FromWorld(
                    wave,
                    changedWorld,
                    (DirectedWaveCoordinateSpace)pathCoordinateSpace.enumValueIndex);
                activePathCheckpointIndex = i;
                SceneView.RepaintAll();
            }

            if (hasPrevious)
                Handles.DrawLine(previous, world);

            previous = world;
            hasPrevious = true;
        }
    }

    private void DrawPatrolSceneHandles(DirectedEnemySubWave wave)
    {
        if (patrolPoints == null || patrolPoints.arraySize == 0)
            return;

        DirectedWaveCoordinateSpace coordinateSpace =
            (DirectedWaveCoordinateSpace)formationCoordinateSpace.enumValueIndex;
        Vector3 centerWorld = GetPatrolSceneCenter(wave, coordinateSpace);

        DrawPatrolRoute(centerWorld);

        Handles.color = new Color(0.35f, 1f, 0.9f, 1f);
        for (int i = 0; i < patrolPoints.arraySize; i++)
        {
            SerializedProperty point = patrolPoints.GetArrayElementAtIndex(i);
            SerializedProperty offset = point.FindPropertyRelative("offset");
            Vector3 world = centerWorld + offset.vector3Value;

            EditorGUI.BeginChangeCheck();
            Vector3 changedWorld = Handles.PositionHandle(
                world,
                Quaternion.identity);

            Handles.Label(
                changedWorld + Vector3.up * 0.15f,
                $"Patrol {i}");

            if (activePatrolPointIndex == i)
            {
                DrawScenePointHighlight(
                    changedWorld,
                    $"Patrol {i}",
                    new Color(0.35f, 1f, 0.9f, 1f));
            }

            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(target, "Move Patrol Point");
                offset.vector3Value = changedWorld - centerWorld;
                activePatrolPointIndex = i;

                if (PatrolPointHasNextSegment(i))
                    SyncPatrolDurationAndSpeed(i, false, false, true);

                if (i > 0 && PatrolPointHasNextSegment(i - 1))
                    SyncPatrolDurationAndSpeed(i - 1, false, false, true);

                if (i == 0
                    && patrolLoop.boolValue
                    && patrolPoints.arraySize > 1
                    && PatrolPointHasNextSegment(patrolPoints.arraySize - 1))
                {
                    SyncPatrolDurationAndSpeed(
                        patrolPoints.arraySize - 1,
                        false,
                        false,
                        true);
                }

                SceneView.RepaintAll();
            }
        }
    }

    private Vector3 GetPatrolSceneCenter(
        DirectedEnemySubWave wave,
        DirectedWaveCoordinateSpace coordinateSpace)
    {
        if (!formationFrozen.boolValue)
        {
            return ToWorld(
                wave,
                formationCenter.vector3Value,
                coordinateSpace);
        }

        Transform root = formationPointsRoot.objectReferenceValue as Transform;
        if (root == null || root.childCount == 0)
        {
            return ToWorld(
                wave,
                formationCenter.vector3Value,
                coordinateSpace);
        }

        Vector3 center = Vector3.zero;
        int count = 0;
        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (child == null)
                continue;

            center += child.position;
            count++;
        }

        return count > 0 ? center / count : root.position;
    }

    private void DrawPatrolRoute(Vector3 centerWorld)
    {
        if (patrolPoints == null || patrolPoints.arraySize < 2)
            return;

        const int samplesPerSegment = 16;
        Color routeColor = new Color(0.35f, 1f, 0.9f, 0.85f);
        Color inactiveRouteColor = new Color(0.35f, 1f, 0.9f, 0.35f);

        for (int segment = 0; segment < patrolPoints.arraySize; segment++)
        {
            if (!PatrolPointHasNextSegment(segment))
                continue;

            Vector3 previous = centerWorld + EvaluatePatrolSegment(segment, 0f);
            for (int sample = 1; sample <= samplesPerSegment; sample++)
            {
                Vector3 current = centerWorld + EvaluatePatrolSegment(
                    segment,
                    sample / (float)samplesPerSegment);
                Handles.color = PreviewUsesPatrol()
                    ? routeColor
                    : inactiveRouteColor;
                Handles.DrawAAPolyLine(2.5f, previous, current);
                previous = current;
            }
        }

        if (!PreviewUsesPatrol())
        {
            Handles.color = Color.white;
            Handles.Label(
                centerWorld + Vector3.up * 0.6f,
                "Patrol points exist, but Post Behavior is not Patrol/WobbleAndPatrol.");
        }
    }

    private void DrawMobileScreenBounds()
    {
        if (!showMobileBounds)
            return;

        float height = mobileBoundsOrthoSize * 2f;
        float width = height * mobileBoundsAspect;
        Vector3 center = new Vector3(
            mobileBoundsCenter.x,
            mobileBoundsCenter.y,
            0f);
        Vector3 leftTop = center + new Vector3(-width * 0.5f, height * 0.5f, 0f);
        Vector3 rightTop = center + new Vector3(width * 0.5f, height * 0.5f, 0f);
        Vector3 rightBottom = center + new Vector3(width * 0.5f, -height * 0.5f, 0f);
        Vector3 leftBottom = center + new Vector3(-width * 0.5f, -height * 0.5f, 0f);

        Handles.zTest = UnityEngine.Rendering.CompareFunction.Always;
        Handles.color = new Color(0.2f, 0.9f, 1f, 0.9f);
        Handles.DrawAAPolyLine(3f, leftTop, rightTop, rightBottom, leftBottom, leftTop);

        Handles.color = new Color(0.2f, 0.9f, 1f, 0.35f);
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
            $"Mobile Bounds\n{width:0.00} x {height:0.00}\nAspect {mobileBoundsAspect:0.###}");
    }

    private void DrawFormationSceneHandles(DirectedEnemySubWave wave)
    {
        if (formationFrozen.boolValue)
        {
            DrawFrozenFormationPreview(wave);
            return;
        }

        DirectedWaveCoordinateSpace coordinateSpace =
            (DirectedWaveCoordinateSpace)formationCoordinateSpace.enumValueIndex;

        Vector3 worldCenter = ToWorld(
            wave,
            formationCenter.vector3Value,
            coordinateSpace);

        Handles.color = Color.yellow;

        EditorGUI.BeginChangeCheck();
        Vector3 changedCenter = Handles.PositionHandle(
            worldCenter,
            Quaternion.identity);

        Handles.Label(
            changedCenter + Vector3.up * 0.2f,
            "Formation Center");

        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(target, "Move Directed Wave Formation Center");
            formationCenter.vector3Value = FromWorld(
                wave,
                changedCenter,
                coordinateSpace);
        }

        DrawFormationPreview(wave, coordinateSpace);

        if ((DirectedWaveFormationLayout)formationLayout.enumValueIndex
            == DirectedWaveFormationLayout.CustomPoints)
        {
            DrawCustomFormationHandles(wave, coordinateSpace);
        }

        if ((DirectedWaveFormationLayout)formationLayout.enumValueIndex
            == DirectedWaveFormationLayout.TransformPoints)
        {
            DrawTransformFormationHandles();
        }
    }

    private void DrawFrozenFormationPreview(DirectedEnemySubWave wave)
    {
        Transform root = formationPointsRoot.objectReferenceValue as Transform;
        if (root == null || root.childCount == 0)
            return;

        Handles.color = new Color(0.55f, 0.95f, 1f, 0.95f);
        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (child == null)
                continue;

            Handles.DrawWireDisc(child.position, Vector3.forward, 0.18f);
            Handles.Label(
                child.position + Vector3.up * 0.15f,
                $"Frozen Slot {i}");
        }

        Handles.color = Color.white;
        Handles.Label(
            wave.transform.position + Vector3.up * 0.7f,
            "Formation Frozen\nUnfreeze to edit final points.");
    }

    private void DrawCustomFormationHandles(
        DirectedEnemySubWave wave,
        DirectedWaveCoordinateSpace coordinateSpace)
    {
        if (customFormationPoints == null)
            return;

        Handles.color = new Color(1f, 0.55f, 0f);

        for (int i = 0; i < customFormationPoints.arraySize; i++)
        {
            SerializedProperty point =
                customFormationPoints.GetArrayElementAtIndex(i);
            Vector3 world = ToWorld(wave, point.vector3Value, coordinateSpace);

            EditorGUI.BeginChangeCheck();
            Vector3 changedWorld = Handles.PositionHandle(
                world,
                Quaternion.identity);

            Handles.Label(
                changedWorld + Vector3.up * 0.15f,
                $"Slot {i}");

            if (activeCustomFormationPointIndex == i)
            {
                DrawScenePointHighlight(
                    changedWorld,
                    $"Slot {i}",
                    new Color(1f, 0.55f, 0f, 1f));
            }

            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(target, "Move Directed Wave Formation Slot");
                point.vector3Value = FromWorld(
                    wave,
                    changedWorld,
                    coordinateSpace);
                activeCustomFormationPointIndex = i;
                SceneView.RepaintAll();
            }
        }

        DrawSelectedCustomSpawnOrderHighlight(wave, coordinateSpace);
    }

    private void DrawTransformFormationHandles()
    {
        Transform root = formationPointsRoot.objectReferenceValue as Transform;
        if (root == null)
            return;

        Handles.color = Color.magenta;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (child == null)
                continue;

            EditorGUI.BeginChangeCheck();
            Vector3 changedWorld = Handles.PositionHandle(
                child.position,
                child.rotation);

            Handles.Label(
                changedWorld + Vector3.up * 0.15f,
                $"Slot {i}");

            if (activeTransformFormationPoint == child)
            {
                DrawScenePointHighlight(
                    changedWorld,
                    $"Slot {i}",
                    Color.magenta);
            }

            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(child, "Move Formation Transform Slot");
                child.position = changedWorld;
                activeTransformFormationPoint = child;
                SceneView.RepaintAll();
            }
        }

        DrawSelectedTransformSpawnOrderHighlight();
    }

    private void DrawSelectedCustomSpawnOrderHighlight(
        DirectedEnemySubWave wave,
        DirectedWaveCoordinateSpace coordinateSpace)
    {
        if (customFinalPointOrderList == null)
            return;

        int index = customFinalPointOrderList.index;
        if (index < 0 || index >= customFinalPointOrder.Count)
            return;

        Vector3 world = ToWorld(
            wave,
            customFinalPointOrder[index].position,
            coordinateSpace);
        DrawScenePointHighlight(
            world,
            $"Spawn Order {index}",
            new Color(1f, 0.55f, 0f, 1f));
    }

    private void DrawSelectedTransformSpawnOrderHighlight()
    {
        if (transformFinalPointOrderList == null)
            return;

        int index = transformFinalPointOrderList.index;
        if (index < 0 || index >= transformFinalPointOrder.Count)
            return;

        Transform point = transformFinalPointOrder[index];
        if (point == null)
            return;

        DrawScenePointHighlight(
            point.position,
            $"Spawn Order {index}",
            Color.magenta);
    }

    private static void DrawScenePointHighlight(
        Vector3 position,
        string label,
        Color color)
    {
        Color fill = new Color(color.r, color.g, color.b, 0.16f);
        Color outline = new Color(color.r, color.g, color.b, 0.95f);

        Handles.color = fill;
        Handles.DrawSolidDisc(position, Vector3.forward, 0.32f);

        Handles.color = outline;
        Handles.DrawWireDisc(position, Vector3.forward, 0.34f);
        Handles.DrawWireDisc(position, Vector3.forward, 0.46f);

        Handles.color = Color.white;
        Handles.Label(
            position + Vector3.up * 0.5f,
            $"Dragging / Selected\n{label}");
    }

    private void DrawFormationPreview(
        DirectedEnemySubWave wave,
        DirectedWaveCoordinateSpace coordinateSpace)
    {
        Handles.color = Color.yellow;

        int count = GetEditorEffectiveEnemyCount();
        for (int i = 0; i < count; i++)
        {
            Vector3 local = GetFormationLocalPosition(i, wave);
            Vector3 world = ToWorld(wave, local, coordinateSpace);
            Handles.DrawWireDisc(world, Vector3.forward, 0.16f);
        }
    }

    private Vector3 GetFormationLocalPosition(
        int index,
        DirectedEnemySubWave wave)
    {
        DirectedWaveFormationLayout layout =
            (DirectedWaveFormationLayout)formationLayout.enumValueIndex;

        return layout switch
        {
            DirectedWaveFormationLayout.VerticalLine =>
                GetVerticalLineLocalPosition(index),
            DirectedWaveFormationLayout.Grid =>
                GetGridLocalPosition(index),
            DirectedWaveFormationLayout.VShape =>
                GetVShapeLocalPosition(index),
            DirectedWaveFormationLayout.Arc =>
                GetArcLocalPosition(index),
            DirectedWaveFormationLayout.Circle =>
                GetCircleLocalPosition(index),
            DirectedWaveFormationLayout.Triangle =>
                GetPolygonPerimeterLocalPosition(index, GetTriangleVertices()),
            DirectedWaveFormationLayout.Square =>
                GetPolygonPerimeterLocalPosition(index, GetSquareVertices()),
            DirectedWaveFormationLayout.Diamond =>
                GetPolygonPerimeterLocalPosition(index, GetDiamondVertices()),
            DirectedWaveFormationLayout.CustomPoints =>
                GetCustomLocalPosition(index),
            DirectedWaveFormationLayout.TransformPoints =>
                GetTransformPointPosition(index, wave),
            _ => GetHorizontalLineLocalPosition(index)
        };
    }

    private Vector3 GetVerticalLineLocalPosition(int index)
    {
        int count = Mathf.Max(1, GetEditorEffectiveEnemyCount());
        float ySpacing = spacing.vector2Value.y;
        float offset = (count - 1) * ySpacing * 0.5f;
        return formationCenter.vector3Value
            + new Vector3(0f, offset - index * ySpacing, 0f);
    }

    private Vector3 GetGridLocalPosition(int index)
    {
        int safeColumns = Mathf.Max(1, columns.intValue);
        int safeRows = Mathf.Max(1, rows.intValue);
        int column = index % safeColumns;
        int row = Mathf.Min(index / safeColumns, safeRows - 1);
        int usedRows = Mathf.Min(
            safeRows,
            Mathf.CeilToInt(GetEditorEffectiveEnemyCount() / (float)safeColumns));

        float xOffset = (safeColumns - 1) * spacing.vector2Value.x * 0.5f;
        float yOffset = (usedRows - 1) * spacing.vector2Value.y * 0.5f;

        return formationCenter.vector3Value
            + new Vector3(
                column * spacing.vector2Value.x - xOffset,
                yOffset - row * spacing.vector2Value.y,
                0f);
    }

    private Vector3 GetVShapeLocalPosition(int index)
    {
        if (index == 0)
            return formationCenter.vector3Value;

        int pairIndex = (index + 1) / 2;
        float side = index % 2 == 0 ? 1f : -1f;

        return formationCenter.vector3Value
            + new Vector3(
                side * pairIndex * spacing.vector2Value.x,
                -pairIndex * spacing.vector2Value.y,
                0f);
    }

    private Vector3 GetArcLocalPosition(int index)
    {
        int count = Mathf.Max(1, GetEditorEffectiveEnemyCount());
        if (count <= 1)
            return formationCenter.vector3Value + Vector3.up * arcRadius.floatValue;

        float halfArc = arcDegrees.floatValue * 0.5f;
        float angle = Mathf.Lerp(-halfArc, halfArc, index / (count - 1f));
        float radians = (90f + angle) * Mathf.Deg2Rad;

        return formationCenter.vector3Value
            + new Vector3(
                Mathf.Cos(radians) * arcRadius.floatValue,
                Mathf.Sin(radians) * arcRadius.floatValue,
                0f);
    }

    private Vector3 GetCircleLocalPosition(int index)
    {
        int count = Mathf.Max(1, GetEditorEffectiveEnemyCount());
        if (count <= 1)
            return formationCenter.vector3Value;

        float angle = 90f - 360f * index / count;
        float radians = angle * Mathf.Deg2Rad;
        Vector2 flattening = GetSafeShapeFlattening();
        float radius = Mathf.Max(0f, shapeRadius.floatValue);

        return formationCenter.vector3Value
            + new Vector3(
                Mathf.Cos(radians) * radius * flattening.x,
                Mathf.Sin(radians) * radius * flattening.y,
                0f);
    }

    private Vector3 GetPolygonPerimeterLocalPosition(
        int index,
        Vector3[] vertices)
    {
        int count = Mathf.Max(1, GetEditorEffectiveEnemyCount());
        if (count <= 1 || vertices == null || vertices.Length == 0)
            return formationCenter.vector3Value;

        float totalLength = 0f;
        for (int i = 0; i < vertices.Length; i++)
        {
            totalLength += Vector3.Distance(
                vertices[i],
                vertices[(i + 1) % vertices.Length]);
        }

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
        Vector2 flattening = GetSafeShapeFlattening();
        return new[]
        {
            GetShapePoint(90f, flattening),
            GetShapePoint(210f, flattening),
            GetShapePoint(330f, flattening)
        };
    }

    private Vector3[] GetSquareVertices()
    {
        Vector3 center = formationCenter.vector3Value;
        Vector2 flattening = GetSafeShapeFlattening();
        float radius = Mathf.Max(0f, shapeRadius.floatValue);
        float x = radius * flattening.x;
        float y = radius * flattening.y;

        return new[]
        {
            center + new Vector3(-x, y, 0f),
            center + new Vector3(x, y, 0f),
            center + new Vector3(x, -y, 0f),
            center + new Vector3(-x, -y, 0f)
        };
    }

    private Vector3[] GetDiamondVertices()
    {
        Vector3 center = formationCenter.vector3Value;
        Vector2 flattening = GetSafeShapeFlattening();
        float radius = Mathf.Max(0f, shapeRadius.floatValue);

        return new[]
        {
            center + Vector3.up * radius * flattening.y,
            center + Vector3.right * radius * flattening.x,
            center + Vector3.down * radius * flattening.y,
            center + Vector3.left * radius * flattening.x
        };
    }

    private Vector3 GetShapePoint(float angleDegrees, Vector2 flattening)
    {
        float radians = angleDegrees * Mathf.Deg2Rad;
        float radius = Mathf.Max(0f, shapeRadius.floatValue);

        return formationCenter.vector3Value
            + new Vector3(
                Mathf.Cos(radians) * radius * flattening.x,
                Mathf.Sin(radians) * radius * flattening.y,
                0f);
    }

    private Vector2 GetSafeShapeFlattening()
    {
        Vector2 flattening = shapeFlattening.vector2Value;
        return new Vector2(
            Mathf.Max(0.01f, flattening.x),
            Mathf.Max(0.01f, flattening.y));
    }

    private Vector3 GetCustomLocalPosition(int index)
    {
        if (customFormationPoints == null
            || customFormationPoints.arraySize == 0)
        {
            return GetHorizontalLineLocalPosition(index);
        }

        if (index < customFormationPoints.arraySize)
            return customFormationPoints.GetArrayElementAtIndex(index).vector3Value;

        return customFormationPoints
            .GetArrayElementAtIndex(customFormationPoints.arraySize - 1)
            .vector3Value;
    }

    private Vector3 GetTransformPointPosition(
        int index,
        DirectedEnemySubWave wave)
    {
        Transform root = formationPointsRoot.objectReferenceValue as Transform;
        if (root == null || root.childCount == 0)
            return GetHorizontalLineLocalPosition(index);

        int safeIndex = Mathf.Clamp(index, 0, root.childCount - 1);
        Vector3 world = root.GetChild(safeIndex).position;
        return FromWorld(
            wave,
            world,
            (DirectedWaveCoordinateSpace)formationCoordinateSpace.enumValueIndex);
    }

    private float GetPreviewElapsed()
    {
        return Mathf.Max(
            0f,
            (float)(EditorApplication.timeSinceStartup - previewStartTime));
    }

    private Vector3 GetEditorSpawnPosition(DirectedEnemySubWave wave)
    {
        EditorPathCheckpoint[] checkpoints = GetWorldPathCheckpoints(wave);
        if (checkpoints.Length > 0)
            return checkpoints[0].position;

        Transform spawn = GetSpawnPoint(wave);
        return spawn != null ? spawn.position : wave.transform.position;
    }

    private float GetPreviewTotalDuration()
    {
        int count = GetEditorEffectiveEnemyCount();
        if (count <= 0)
            return 0f;

        float lastSpawnTime = (count - 1) * Mathf.Max(0f, spawnInterval.floatValue);
        float pathDuration = pathCheckpoints != null && pathCheckpoints.arraySize > 1
            ? GetCheckpointPathDuration()
            : 0f;
        return lastSpawnTime
            + pathDuration
            + Mathf.Max(0f, settleDuration.floatValue)
            + GetPreviewPostBehaviorDuration()
            + 0.25f;
    }

    private float GetPreviewPostBehaviorStartTime()
    {
        int count = GetEditorEffectiveEnemyCount();
        if (count <= 0)
            return 0f;

        float lastSpawnTime = (count - 1) * Mathf.Max(0f, spawnInterval.floatValue);
        float pathDuration = pathCheckpoints != null && pathCheckpoints.arraySize > 1
            ? GetCheckpointPathDuration()
            : 0f;

        return lastSpawnTime
            + pathDuration
            + Mathf.Max(0f, settleDuration.floatValue)
            + Mathf.Max(0f, postStartDelay.floatValue);
    }

    private float GetPreviewPostBehaviorDuration()
    {
        float duration = GetPreviewPipelineDuration();
        bool hasInfiniteParallel = HasInfiniteParallel(postCommands);
        bool hasInfiniteLoop = HasInfiniteLoop(postCommands);
        bool hasInfiniteContainer = hasInfiniteParallel || hasInfiniteLoop;
        if (duration <= 0f)
            return hasInfiniteContainer
                ? Mathf.Max(0f, postStartDelay.floatValue)
                    + InfiniteParallelPreviewExtraDuration
                : 0f;

        if (float.IsInfinity(duration))
            duration = InfiniteParallelPreviewExtraDuration;
        else if (hasInfiniteContainer)
            duration += InfiniteParallelPreviewExtraDuration;

        return Mathf.Max(0f, postStartDelay.floatValue)
            + duration;
    }

    private int GetEditorEffectiveEnemyCount()
    {
        if (IsTransformPointsFormation())
        {
            Transform root = formationPointsRoot.objectReferenceValue as Transform;
            return root != null ? root.childCount : 0;
        }

        if (IsCustomPointsFormation() && customFormationPoints.arraySize > 0)
            return customFormationPoints.arraySize;

        if (IsShapeFormation())
            return Mathf.Max(1, shapePointCount.intValue);

        return GetEditorConfiguredEnemyCount();
    }

    private int GetEditorConfiguredEnemyCount()
    {
        return Mathf.Max(1, enemyCount.intValue);
    }

    private bool HasEditorPointEnemyOverride()
    {
        if (HasEditorCustomFormationEnemyOverride())
            return true;

        Transform root = formationPointsRoot.objectReferenceValue as Transform;
        if (root == null)
            return false;

        for (int i = 0; i < root.childCount; i++)
        {
            DirectedWaveEnemyOverride enemyOverride =
                root.GetChild(i).GetComponent<DirectedWaveEnemyOverride>();

            if (enemyOverride != null && enemyOverride.EnemyPrefabOverride != null)
                return true;
        }

        return false;
    }

    private bool HasEditorCustomFormationEnemyOverride()
    {
        if (customFormationEnemyOverrides == null)
            return false;

        for (int i = 0; i < customFormationEnemyOverrides.arraySize; i++)
        {
            if (customFormationEnemyOverrides
                .GetArrayElementAtIndex(i)
                .objectReferenceValue != null)
            {
                return true;
            }
        }

        return false;
    }

    private bool IsTransformPointsFormation()
    {
        return (DirectedWaveFormationLayout)formationLayout.enumValueIndex
            == DirectedWaveFormationLayout.TransformPoints;
    }

    private bool IsCustomPointsFormation()
    {
        return (DirectedWaveFormationLayout)formationLayout.enumValueIndex
            == DirectedWaveFormationLayout.CustomPoints;
    }

    private bool IsShapeFormation()
    {
        DirectedWaveFormationLayout layout =
            (DirectedWaveFormationLayout)formationLayout.enumValueIndex;
        return layout == DirectedWaveFormationLayout.Circle
            || layout == DirectedWaveFormationLayout.Triangle
            || layout == DirectedWaveFormationLayout.Square
            || layout == DirectedWaveFormationLayout.Diamond;
    }

    private float GetCheckpointPathDuration()
    {
        float total = 0f;
        for (int i = 0; i < pathCheckpoints.arraySize - 1; i++)
        {
            total += Mathf.Max(
                0.01f,
                pathCheckpoints
                    .GetArrayElementAtIndex(i)
                    .FindPropertyRelative("durationToNext")
                    .floatValue);
        }

        return Mathf.Max(0.01f, total);
    }

    private static float GetCheckpointPathDuration(
        EditorPathCheckpoint[] checkpoints)
    {
        float total = 0f;
        for (int i = 0; i < checkpoints.Length - 1; i++)
            total += Mathf.Max(0.01f, checkpoints[i].durationToNext);

        return Mathf.Max(0.01f, total);
    }

    private float GetCheckpointSegmentLength(int segmentIndex)
    {
        EditorPathCheckpoint[] checkpoints =
            GetWorldPathCheckpoints((DirectedEnemySubWave)target);

        if (checkpoints.Length < 2
            || segmentIndex < 0
            || segmentIndex >= checkpoints.Length - 1)
        {
            return 0.01f;
        }

        const int sampleCount = 16;
        float length = 0f;
        Vector3 previous = EvaluateCheckpointSegment(
            checkpoints,
            segmentIndex,
            0f);

        for (int i = 1; i <= sampleCount; i++)
        {
            Vector3 current = EvaluateCheckpointSegment(
                checkpoints,
                segmentIndex,
                i / (float)sampleCount);
            length += Vector3.Distance(previous, current);
            previous = current;
        }

        return Mathf.Max(0.01f, length);
    }

    private float GetPatrolSegmentLength(int segmentIndex)
    {
        if (!PatrolPointHasNextSegment(segmentIndex))
            return 0.01f;

        const int sampleCount = 16;
        float length = 0f;
        Vector3 previous = EvaluatePatrolSegment(segmentIndex, 0f);

        for (int i = 1; i <= sampleCount; i++)
        {
            Vector3 current = EvaluatePatrolSegment(
                segmentIndex,
                i / (float)sampleCount);
            length += Vector3.Distance(previous, current);
            previous = current;
        }

        return Mathf.Max(0.01f, length);
    }

    private float GetPreviewPatrolDuration()
    {
        if (patrolPoints == null || patrolPoints.arraySize < 2)
            return 0f;

        if (patrolLoop.boolValue)
            return PostBehaviorPreviewDuration;

        float duration = 0f;
        for (int i = 0; i < patrolPoints.arraySize - 1; i++)
        {
            duration += Mathf.Max(
                0.01f,
                patrolPoints
                    .GetArrayElementAtIndex(i)
                    .FindPropertyRelative("durationToNext")
                    .floatValue);
        }

        return Mathf.Max(0f, duration);
    }

    private Vector3 GetPreviewPatrolOffset(float postBehaviorTime)
    {
        if (patrolPoints == null || patrolPoints.arraySize == 0)
            return Vector3.zero;

        if (patrolPoints.arraySize == 1)
        {
            return patrolPoints
                .GetArrayElementAtIndex(0)
                .FindPropertyRelative("offset")
                .vector3Value;
        }

        float remaining = Mathf.Max(0f, postBehaviorTime);
        float totalDuration = GetPreviewPatrolPathDuration();
        if (patrolLoop.boolValue && totalDuration > 0f)
            remaining = Mathf.Repeat(remaining, totalDuration);
        else if (!patrolLoop.boolValue && remaining >= totalDuration)
            return GetPatrolPointOffset(patrolPoints.arraySize - 1);

        int lastSegment = patrolLoop.boolValue
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
                float normalized = Mathf.Clamp01(remaining / duration);
                float curved = EvaluateCurve(
                    point.FindPropertyRelative("easeToNext").animationCurveValue,
                    normalized);
                return EvaluatePatrolSegment(i, curved);
            }

            remaining -= duration;
        }

        return patrolLoop.boolValue
            ? GetPatrolPointOffset(0)
            : GetPatrolPointOffset(patrolPoints.arraySize - 1);
    }

    private float GetPreviewPatrolPathDuration()
    {
        if (patrolPoints == null || patrolPoints.arraySize < 2)
            return 0f;

        int lastSegment = patrolLoop.boolValue
            ? patrolPoints.arraySize - 1
            : patrolPoints.arraySize - 2;
        float duration = 0f;

        for (int i = 0; i <= lastSegment; i++)
        {
            duration += Mathf.Max(
                0.01f,
                patrolPoints
                    .GetArrayElementAtIndex(i)
                    .FindPropertyRelative("durationToNext")
                    .floatValue);
        }

        return duration;
    }

    private Vector3 EvaluatePatrolSegment(int segmentIndex, float time)
    {
        if (patrolPoints == null || patrolPoints.arraySize == 0)
            return Vector3.zero;

        SerializedProperty point = patrolPoints.GetArrayElementAtIndex(segmentIndex);
        DirectedWaveSegmentMotion motion =
            (DirectedWaveSegmentMotion)point
                .FindPropertyRelative("motionToNext")
                .enumValueIndex;

        return motion switch
        {
            DirectedWaveSegmentMotion.Bezier =>
                EvaluatePatrolBezierSegment(segmentIndex, time),
            DirectedWaveSegmentMotion.CatmullRom =>
                EvaluatePatrolCatmullRomSegment(segmentIndex, time),
            _ => Vector3.LerpUnclamped(
                GetPatrolPointOffset(segmentIndex),
                GetPatrolPointOffset(GetNextPatrolPointIndex(segmentIndex)),
                time)
        };
    }

    private Vector3 EvaluatePatrolBezierSegment(int segmentIndex, float time)
    {
        Vector3 p0 = GetPatrolPointOffset(segmentIndex);
        Vector3 p3 = GetPatrolPointOffset(GetNextPatrolPointIndex(segmentIndex));
        Vector3 previous = GetPatrolPointOffset(GetPreviousPatrolPointIndex(segmentIndex));
        Vector3 following = GetPatrolPointOffset(
            GetNextPatrolPointIndex(GetNextPatrolPointIndex(segmentIndex)));

        Vector3 p1 = p0 + (p3 - previous) / 6f;
        Vector3 p2 = p3 - (following - p0) / 6f;
        float t = Mathf.Clamp01(time);
        float oneMinusT = 1f - t;

        return oneMinusT * oneMinusT * oneMinusT * p0
            + 3f * oneMinusT * oneMinusT * t * p1
            + 3f * oneMinusT * t * t * p2
            + t * t * t * p3;
    }

    private Vector3 EvaluatePatrolCatmullRomSegment(int segmentIndex, float time)
    {
        int p1 = segmentIndex;
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

        if (patrolLoop.boolValue)
            return (index - 1 + patrolPoints.arraySize) % patrolPoints.arraySize;

        return Mathf.Max(0, index - 1);
    }

    private int GetNextPatrolPointIndex(int index)
    {
        if (patrolPoints == null || patrolPoints.arraySize == 0)
            return 0;

        if (patrolLoop.boolValue)
            return (index + 1) % patrolPoints.arraySize;

        return Mathf.Min(patrolPoints.arraySize - 1, index + 1);
    }

    private Vector3 GetPatrolPointOffset(int index)
    {
        if (patrolPoints == null || patrolPoints.arraySize == 0)
            return Vector3.zero;

        int safeIndex = Mathf.Clamp(index, 0, patrolPoints.arraySize - 1);
        return patrolPoints
            .GetArrayElementAtIndex(safeIndex)
            .FindPropertyRelative("offset")
            .vector3Value;
    }

    private EditorPathCheckpoint[] GetWorldPathCheckpoints(
        DirectedEnemySubWave wave)
    {
        if (pathCheckpoints == null || pathCheckpoints.arraySize == 0)
            return System.Array.Empty<EditorPathCheckpoint>();

        DirectedWaveCoordinateSpace coordinateSpace =
            (DirectedWaveCoordinateSpace)pathCoordinateSpace.enumValueIndex;
        EditorPathCheckpoint[] checkpoints =
            new EditorPathCheckpoint[pathCheckpoints.arraySize];

        for (int i = 0; i < pathCheckpoints.arraySize; i++)
        {
            SerializedProperty checkpoint =
                pathCheckpoints.GetArrayElementAtIndex(i);
            checkpoints[i] = new EditorPathCheckpoint
            {
                position = ToWorld(
                    wave,
                    checkpoint.FindPropertyRelative("position").vector3Value,
                    coordinateSpace),
                durationToNext = checkpoint
                    .FindPropertyRelative("durationToNext")
                    .floatValue,
                motionToNext =
                    (DirectedWaveSegmentMotion)checkpoint
                        .FindPropertyRelative("motionToNext")
                        .enumValueIndex,
                easeToNext = checkpoint
                    .FindPropertyRelative("easeToNext")
                    .animationCurveValue
            };
        }

        return checkpoints;
    }

    private Vector3 GetFormationWorldPosition(
        int index,
        DirectedEnemySubWave wave)
    {
        DirectedWaveCoordinateSpace coordinateSpace =
            (DirectedWaveCoordinateSpace)formationCoordinateSpace.enumValueIndex;
        return ToWorld(
            wave,
            GetFormationLocalPosition(index, wave),
            coordinateSpace);
    }

    private static Vector3 EvaluateCheckpointPath(
        EditorPathCheckpoint[] checkpoints,
        float elapsed)
    {
        if (checkpoints == null || checkpoints.Length == 0)
            return Vector3.zero;

        if (checkpoints.Length == 1)
            return checkpoints[0].position;

        float remaining = Mathf.Max(0f, elapsed);
        for (int i = 0; i < checkpoints.Length - 1; i++)
        {
            float duration = Mathf.Max(0.01f, checkpoints[i].durationToNext);
            if (remaining <= duration)
            {
                float time = Mathf.Clamp01(remaining / duration);
                float curved = EvaluateCurve(checkpoints[i].easeToNext, time);
                return EvaluateCheckpointSegment(checkpoints, i, curved);
            }

            remaining -= duration;
        }

        return checkpoints[checkpoints.Length - 1].position;
    }

    private static Vector3 EvaluateCheckpointSegment(
        EditorPathCheckpoint[] checkpoints,
        int segmentIndex,
        float time)
    {
        EditorPathCheckpoint current = checkpoints[segmentIndex];
        EditorPathCheckpoint next = checkpoints[segmentIndex + 1];

        return current.motionToNext switch
        {
            DirectedWaveSegmentMotion.Bezier =>
                EvaluateBezierSegment(checkpoints, segmentIndex, time),
            DirectedWaveSegmentMotion.CatmullRom =>
                EvaluateCatmullRomSegment(checkpoints, segmentIndex, time),
            _ => Vector3.LerpUnclamped(current.position, next.position, time)
        };
    }

    private static Vector3 EvaluateBezierSegment(
        EditorPathCheckpoint[] checkpoints,
        int segmentIndex,
        float time)
    {
        Vector3 p0 = checkpoints[segmentIndex].position;
        Vector3 p3 = checkpoints[segmentIndex + 1].position;

        Vector3 previous = segmentIndex > 0
            ? checkpoints[segmentIndex - 1].position
            : p0;
        Vector3 following = segmentIndex + 2 < checkpoints.Length
            ? checkpoints[segmentIndex + 2].position
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

    private static Vector3 EvaluateCatmullRomSegment(
        EditorPathCheckpoint[] checkpoints,
        int segmentIndex,
        float time)
    {
        int p1 = segmentIndex;
        int p0 = Mathf.Max(p1 - 1, 0);
        int p2 = Mathf.Min(p1 + 1, checkpoints.Length - 1);
        int p3 = Mathf.Min(p1 + 2, checkpoints.Length - 1);
        float t = Mathf.Clamp01(time);

        return 0.5f * (
            2f * checkpoints[p1].position
            + (-checkpoints[p0].position + checkpoints[p2].position) * t
            + (2f * checkpoints[p0].position - 5f * checkpoints[p1].position
                + 4f * checkpoints[p2].position - checkpoints[p3].position)
            * t * t
            + (-checkpoints[p0].position + 3f * checkpoints[p1].position
                - 3f * checkpoints[p2].position + checkpoints[p3].position)
            * t * t * t);
    }

    private static float EvaluateCurve(AnimationCurve curve, float time)
    {
        return curve != null ? curve.Evaluate(time) : time;
    }

    private static Vector3 ToWorld(
        DirectedEnemySubWave wave,
        Vector3 position,
        DirectedWaveCoordinateSpace coordinateSpace)
    {
        Transform spawn = GetSpawnPoint(wave);

        return coordinateSpace switch
        {
            DirectedWaveCoordinateSpace.LocalToSpawnPoint when spawn != null =>
                spawn.TransformPoint(position),
            DirectedWaveCoordinateSpace.LocalToSubWave =>
                wave.transform.TransformPoint(position),
            _ => position
        };
    }

    private static Vector3 FromWorld(
        DirectedEnemySubWave wave,
        Vector3 position,
        DirectedWaveCoordinateSpace coordinateSpace)
    {
        Transform spawn = GetSpawnPoint(wave);

        return coordinateSpace switch
        {
            DirectedWaveCoordinateSpace.LocalToSpawnPoint when spawn != null =>
                spawn.InverseTransformPoint(position),
            DirectedWaveCoordinateSpace.LocalToSubWave =>
                wave.transform.InverseTransformPoint(position),
            _ => position
        };
    }

    private static Transform GetSpawnPoint(DirectedEnemySubWave wave)
    {
        SerializedObject serializedWave = new SerializedObject(wave);
        SerializedProperty property = serializedWave.FindProperty("spawnPoint");
        return property.objectReferenceValue as Transform;
    }

    private struct EditorPathCheckpoint
    {
        public Vector3 position;
        public float durationToNext;
        public DirectedWaveSegmentMotion motionToNext;
        public AnimationCurve easeToNext;
    }

    private struct CustomFinalPointOrderEntry
    {
        public Vector3 position;
        public Object enemyOverride;
    }

    private enum FormationShapePreset
    {
        Circle,
        Triangle,
        Square,
        Diamond
    }
}
