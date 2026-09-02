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
    private const string PreviewDurationKeyPrefix =
        "DirectedEnemySubWaveEditor.PreviewDuration.";
    private const float PostBehaviorPreviewDuration = 4f;
    private const float InfiniteParallelPreviewExtraDuration = 60f;
    private const double PreviewFrameInterval = 1d / 30d;
    private const float PatrolMetricEpsilon = 0.0001f;
    private const int PatrolRouteSamplesPerSegment = 16;

    private static readonly DirectedWavePostCommandType[]
        SelectablePostCommandTypes =
        {
            DirectedWavePostCommandType.Patrol,
            DirectedWavePostCommandType.LocalMovement,
            DirectedWavePostCommandType.Wobble,
            DirectedWavePostCommandType.CircularMovement,
            DirectedWavePostCommandType.FormationRotation,
            DirectedWavePostCommandType.FormationMorph,
            DirectedWavePostCommandType.FormationReorder,
            DirectedWavePostCommandType.Wait,
            DirectedWavePostCommandType.Parallel,
            DirectedWavePostCommandType.Loop
        };

    private static readonly GUIContent[] SelectablePostCommandLabels =
    {
        new("Patrol"),
        new("Local Movement"),
        new("Wobble"),
        new("Circular Movement"),
        new("Formation Rotation"),
        new("Formation Morph"),
        new("Formation Reorder"),
        new("Wait"),
        new("Parallel"),
        new("Loop")
    };

    private SerializedProperty enemyPrefab;
    private SerializedProperty enemyCount;
    private SerializedProperty spawnInterval;
    private SerializedProperty spawnOrderMode;
    private SerializedProperty spawnOrderAngle;
    private SerializedProperty spawnOrderStartAngle;
    private SerializedProperty spawnPoint;
    private SerializedProperty parentEnemiesToSubWave;
    private SerializedProperty enableDebugLogs;

    private SerializedProperty entranceMode;
    private SerializedProperty entranceCompletionMode;
    private SerializedProperty entranceLoopStartCheckpointIndex;
    private SerializedProperty entranceLoopTeleportToStart;
    private SerializedProperty entranceLoopTeleportDelay;
    private SerializedProperty pathCoordinateSpace;
    private SerializedProperty pathCheckpoints;
    private SerializedProperty individualEntrancePoints;
    private SerializedProperty individualPointMovementStartDelay;
    private SerializedProperty individualPointMovementDuration;
    private SerializedProperty individualPointMovementCurve;
    private SerializedProperty individualEntranceShapeCenter;
    private SerializedProperty individualEntranceShapeRadius;
    private SerializedProperty individualEntranceShapeFlattening;
    private SerializedProperty individualEntranceShapeRotationDegrees;

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
    private SerializedProperty proceduralFormationEnemyOverrides;
    private SerializedProperty formationPointsRoot;
    private SerializedProperty settleDuration;
    private SerializedProperty settleCurve;

    private SerializedProperty postCommands;
    private SerializedProperty postStartDelay;
    private SerializedProperty postCommandPipelineFixedCount;
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
    private SerializedProperty patrolLoop;
    private SerializedProperty patrolCoordinateSpace;
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
    private double nextPreviewFrameTime;
    private float previewSampleElapsed;
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
    private int activeIndividualEntrancePointIndex = -1;
    private int activePatrolPointIndex = -1;
    private int activePatrolCommandIndex = -1;
    private int activeCustomFormationPointIndex = -1;
    private int activePostCommandIndex = -1;
    private int selectedEnemySlotIndex = -1;
    private bool enemySlotSelectionMode;
    private Transform activeTransformFormationPoint;
    private int previewConfigurationVersion;
    private int patrolGeometryVersion;
    private int cachedCommandPreviewVersion = -1;
    private int cachedCommandPreviewIndex = -1;
    private Dictionary<int, Vector3> cachedCommandPreviewBefore;
    private Dictionary<int, Vector3> cachedCommandPreviewAfter;
    private int cachedWavePreviewVersion = -1;
    private float cachedWavePreviewElapsed = float.NaN;
    private Dictionary<int, Vector3> cachedWavePreviewPositions;
    private int[] cachedWavePreviewSpawnOrder;
    private GUIContent[] cachedWavePreviewLabels;
    private string cachedWavePreviewPhaseName = "Entrance / Formation";
    private int cachedPatrolRouteVersion = -1;
    private Vector3 cachedPatrolRouteCenter;
    private Matrix4x4 cachedPatrolRouteOffsetMatrix;
    private bool hasCachedPatrolRouteOffsetMatrix;
    private Vector3[] cachedPatrolRoutePoints = System.Array.Empty<Vector3>();
    private bool cachedPatrolCommandActive;
    private bool patrolRouteCacheRequiresFullRebuild = true;
    private int patrolRouteDirtyPointIndex = -1;
    private Dictionary<int, Vector3> cachedPatrolPreviewBasePositions;
    private int cachedPatrolPreviewCommandIndex = -1;
    private bool patrolPointDragActive;
    private int patrolPointDragIndex = -1;
    private Vector3 patrolPointDragWorldPosition;
    private bool patrolSceneCommitThisEvent;
    private bool patrolInspectorChangeThisEvent;
    private bool patrolInspectorInteractionPending;
    private bool patrolInspectorHeavyInvalidationPending;
    private int pendingPatrolMetricPointIndex = -1;
    private readonly int[] patrolAffectedSegmentIndices = new int[4];
    private float previewDuration;
    private bool previewDurationOverridden;
    private readonly Vector3[] previewLinePoints = new Vector3[2];
    private readonly Vector3[] mobileBoundsLinePoints = new Vector3[5];
    private SceneView previewSceneView;
    private int draggedPathCheckpointIndex = -1;
    private int pathCheckpointDropIndex = -1;

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
        enableDebugLogs = serializedObject.FindProperty("enableDebugLogs");

        entranceMode = serializedObject.FindProperty("entranceMode");
        entranceCompletionMode = serializedObject.FindProperty(
            "entranceCompletionMode");
        entranceLoopStartCheckpointIndex = serializedObject.FindProperty(
            "entranceLoopStartCheckpointIndex");
        entranceLoopTeleportToStart = serializedObject.FindProperty(
            "entranceLoopTeleportToStart");
        entranceLoopTeleportDelay = serializedObject.FindProperty(
            "entranceLoopTeleportDelay");
        pathCoordinateSpace =
            serializedObject.FindProperty("pathCoordinateSpace");
        pathCheckpoints = serializedObject.FindProperty("pathCheckpoints");
        individualEntrancePoints =
            serializedObject.FindProperty("individualEntrancePoints");
        individualPointMovementStartDelay = serializedObject.FindProperty(
            "individualPointMovementStartDelay");
        individualPointMovementDuration = serializedObject.FindProperty(
            "individualPointMovementDuration");
        individualPointMovementCurve = serializedObject.FindProperty(
            "individualPointMovementCurve");
        individualEntranceShapeCenter = serializedObject.FindProperty(
            "individualEntranceShapeCenter");
        individualEntranceShapeRadius = serializedObject.FindProperty(
            "individualEntranceShapeRadius");
        individualEntranceShapeFlattening = serializedObject.FindProperty(
            "individualEntranceShapeFlattening");
        individualEntranceShapeRotationDegrees = serializedObject.FindProperty(
            "individualEntranceShapeRotationDegrees");

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
        proceduralFormationEnemyOverrides =
            serializedObject.FindProperty("proceduralFormationEnemyOverrides");
        formationPointsRoot =
            serializedObject.FindProperty("formationPointsRoot");
        settleDuration = serializedObject.FindProperty("settleDuration");
        settleCurve = serializedObject.FindProperty("settleCurve");

        postCommands = serializedObject.FindProperty("postCommands");
        postStartDelay = serializedObject.FindProperty("postStartDelay");
        postCommandPipelineFixedCount =
            serializedObject.FindProperty("postCommandPipelineFixedCount");
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
        patrolLoop = serializedObject.FindProperty("patrolLoop");
        patrolCoordinateSpace =
            serializedObject.FindProperty("patrolCoordinateSpace");
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
        LoadPreviewDuration();
        Undo.undoRedoPerformed += InvalidatePreviewSession;
        InvalidatePreviewSession();
    }

    private void OnDisable()
    {
        CancelPatrolPointDrag();
        CancelPathCheckpointDrag();
        StopPreview();
        Undo.undoRedoPerformed -= InvalidatePreviewSession;
    }

    private void ApplyModifiedPropertiesAndInvalidatePreview(
        bool patrolOnlyChange = false,
        bool deferPatrolInvalidation = false)
    {
        bool modified = serializedObject.ApplyModifiedProperties();
        if (!modified)
        {
            if (patrolOnlyChange
                && !deferPatrolInvalidation
                && patrolInspectorHeavyInvalidationPending)
            {
                patrolInspectorHeavyInvalidationPending = false;
                InvalidatePatrolDataPreview();
            }

            return;
        }

        if (!patrolOnlyChange)
        {
            patrolInspectorHeavyInvalidationPending = false;
            InvalidatePreviewSession();
            return;
        }

        if (deferPatrolInvalidation)
        {
            patrolInspectorHeavyInvalidationPending = true;
            if (pendingPatrolMetricPointIndex >= 0)
            {
                InvalidatePatrolGeometryPreview(
                    pendingPatrolMetricPointIndex);
            }
            else
            {
                RepaintPreviewSceneView();
            }

            return;
        }

        patrolInspectorHeavyInvalidationPending = false;
        InvalidatePatrolDataPreview();
    }

    private void InvalidatePreviewSession()
    {
        InvalidatePreviewSessionCore(
            clearPatrolBasePositions: true,
            repaintAllSceneViews: true);
    }

    private void InvalidatePatrolDataPreview()
    {
        InvalidatePreviewSessionCore(
            clearPatrolBasePositions: false,
            repaintAllSceneViews: false);
    }

    private void InvalidatePatrolGeometryPreview(int pointIndex = -1)
    {
        unchecked
        {
            patrolGeometryVersion++;
        }

        cachedPatrolRouteVersion = -1;
        if (pointIndex >= 0 && !patrolRouteCacheRequiresFullRebuild)
        {
            patrolRouteDirtyPointIndex = pointIndex;
        }
        else
        {
            patrolRouteCacheRequiresFullRebuild = true;
            patrolRouteDirtyPointIndex = -1;
            hasCachedPatrolRouteOffsetMatrix = false;
        }

        RepaintPreviewSceneView();
    }

    private void InvalidatePreviewSessionCore(
        bool clearPatrolBasePositions,
        bool repaintAllSceneViews)
    {
        unchecked
        {
            previewConfigurationVersion++;
            patrolGeometryVersion++;
        }

        cachedCommandPreviewVersion = -1;
        cachedCommandPreviewIndex = -1;
        cachedCommandPreviewBefore = null;
        cachedCommandPreviewAfter = null;
        cachedWavePreviewVersion = -1;
        cachedWavePreviewElapsed = float.NaN;
        cachedWavePreviewPositions?.Clear();
        cachedWavePreviewSpawnOrder = null;
        cachedWavePreviewLabels = null;
        cachedWavePreviewPhaseName = "Entrance / Formation";
        cachedPatrolRouteVersion = -1;
        patrolRouteCacheRequiresFullRebuild = true;
        patrolRouteDirtyPointIndex = -1;
        hasCachedPatrolRouteOffsetMatrix = false;
        if (clearPatrolBasePositions)
        {
            cachedPatrolPreviewBasePositions = null;
            cachedPatrolPreviewCommandIndex = -1;
        }

        if (target is DirectedEnemySubWave wave)
            wave.InvalidateSimulationPreviewCache();

        if (repaintAllSceneViews)
            SceneView.RepaintAll();
        else
            RepaintPreviewSceneView();

        Repaint();
    }

    public override void OnInspectorGUI()
    {
        patrolInspectorChangeThisEvent = false;
        serializedObject.Update();

        DrawIntro();
        DrawSpawn();
        DrawPath();
        DrawFormation();
        DrawPostBehavior();
        DrawPreviewHelp();

        FinalizePendingPatrolInspectorMetrics();
        bool deferPatrolInvalidation = patrolInspectorInteractionPending
            && GUIUtility.hotControl != 0;
        ApplyModifiedPropertiesAndInvalidatePreview(
            patrolInspectorChangeThisEvent,
            deferPatrolInvalidation);
    }

    private void DrawIntro()
    {
        EditorGUILayout.HelpBox(
            "Directed Enemy Sub Wave creates enemies with interval, moves them "
            + "through an entrance path, then places them into a formation.",
            MessageType.Info);
        EditorGUILayout.PropertyField(
            enableDebugLogs,
            new GUIContent(
                "Enable Debug Logs",
                "Verbose diagnostic messages. Keep this disabled for normal play and large wave previews."));
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
                ApplyModifiedPropertiesAndInvalidatePreview();
                serializedObject.Update();
            }

            Transform root = formationPointsRoot.objectReferenceValue as Transform;
            RebuildTransformPointsFromSpawnOrder(root, spawnOrder);
            ReloadTransformFinalPointOrder(root);
        }

        spawnOrderMode.enumValueIndex =
            (int)DirectedWaveSpawnOrderMode.Manual;

        ApplyModifiedPropertiesAndInvalidatePreview();
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
            EditorGUILayout.PropertyField(entranceMode);
            EditorGUILayout.PropertyField(
                entranceCompletionMode,
                new GUIContent(
                    "After Entrance",
                    "Move To Formation places ships into final slots. Loop Entrance Path repeats a checkpoint path and never moves ships into formation."));
            EditorGUILayout.PropertyField(pathCoordinateSpace);

            if (UsesEntrancePathLoop())
                DrawEntranceLoopSettings();

            if (UsesIndividualEntrancePoints())
            {
                EditorGUILayout.Space(4f);
                DrawIndividualEntrancePoints();
            }
            else
            {
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
        }

        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    private bool UsesIndividualEntrancePoints()
    {
        return entranceMode != null
            && (DirectedWaveEntranceMode)entranceMode.enumValueIndex
                == DirectedWaveEntranceMode.IndividualPoints;
    }

    private bool UsesEntrancePathLoop()
    {
        return entranceCompletionMode != null
            && (DirectedWaveEntranceCompletionMode)
                entranceCompletionMode.enumValueIndex
                == DirectedWaveEntranceCompletionMode.LoopEntrancePath;
    }

    private void DrawEntranceLoopSettings()
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("Entrance Loop", EditorStyles.boldLabel);

            if (UsesIndividualEntrancePoints())
            {
                EditorGUILayout.HelpBox(
                    "Entrance loops use Checkpoints. Switch Entrance Mode to Checkpoints to configure the repeated route.",
                    MessageType.Warning);
                return;
            }

            if (pathCheckpoints.arraySize < 2)
            {
                EditorGUILayout.HelpBox(
                    "Add at least two checkpoints. The initial pass goes through the whole route; later passes start from the selected checkpoint.",
                    MessageType.Warning);
                return;
            }

            int maximumStartIndex = pathCheckpoints.arraySize - 2;
            entranceLoopStartCheckpointIndex.intValue = Mathf.Clamp(
                entranceLoopStartCheckpointIndex.intValue,
                0,
                maximumStartIndex);
            entranceLoopStartCheckpointIndex.intValue = EditorGUILayout.IntSlider(
                new GUIContent(
                    "Loop Start Checkpoint",
                    "The first pass uses every checkpoint. Afterwards the route repeats from this checkpoint through the last one."),
                entranceLoopStartCheckpointIndex.intValue,
                0,
                maximumStartIndex);

            EditorGUILayout.PropertyField(
                entranceLoopTeleportToStart,
                new GUIContent(
                    "Teleport To Loop Start",
                    "Instead of travelling from the last checkpoint back to Loop Start, enemies wait and reappear there instantly."));
            if (entranceLoopTeleportToStart.boolValue)
            {
                EditorGUILayout.PropertyField(
                    entranceLoopTeleportDelay,
                    new GUIContent(
                        "Reappearance Delay",
                        "How long an enemy stays at the last checkpoint before teleporting to Loop Start."));
                EditorGUILayout.HelpBox(
                    "For a clean exit and re-entry, keep the last checkpoint and Loop Start outside the visible play area.",
                    MessageType.None);
            }

            int lastIndex = pathCheckpoints.arraySize - 1;
            EditorGUILayout.HelpBox(
                $"First pass: Checkpoint 0 to Checkpoint {lastIndex}. Then: Checkpoint {entranceLoopStartCheckpointIndex.intValue} to Checkpoint {lastIndex}, returning from the last checkpoint to Checkpoint {entranceLoopStartCheckpointIndex.intValue}.",
                MessageType.Info);
            EditorGUILayout.HelpBox(
                "Post Behaviour is not started while Entrance Loop is active, because it would otherwise overwrite the loop movement.",
                MessageType.None);
        }
    }

    private void DrawIndividualEntrancePoints()
    {
        EditorGUILayout.LabelField(
            "Individual Entry Points",
            EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(
            individualPointMovementStartDelay,
            new GUIContent(
                "Delay Between Movement Starts",
                "Additional delay per ship in spawn order. The first ship starts immediately, the second after one delay, and so on."));
        EditorGUILayout.PropertyField(
            individualPointMovementDuration,
            new GUIContent("Movement Duration"));
        EditorGUILayout.PropertyField(
            individualPointMovementCurve,
            new GUIContent(
                "Movement Speed Curve",
                "Applied to every ship while it moves from its entry point to its formation slot."));

        DrawIndividualEntranceShapePresets();

        int expectedCount = GetEditorEffectiveEnemyCount();
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField(
                $"Formation slots: {expectedCount}",
                EditorStyles.miniLabel);

            if (GUILayout.Button("Match Formation"))
                ResizeIndividualEntrancePoints(expectedCount);
        }

        int newSize = Mathf.Max(
            0,
            EditorGUILayout.IntField(
                "Point Count",
                individualEntrancePoints.arraySize));
        if (newSize != individualEntrancePoints.arraySize)
            ResizeIndividualEntrancePoints(newSize);

        if (individualEntrancePoints.arraySize < expectedCount)
        {
            EditorGUILayout.HelpBox(
                $"Create {expectedCount - individualEntrancePoints.arraySize} more point(s). "
                + "Each entry point maps to the formation slot with the same index.",
                MessageType.Warning);
        }

        for (int i = 0; i < individualEntrancePoints.arraySize; i++)
        {
            SerializedProperty point =
                individualEntrancePoints.GetArrayElementAtIndex(i);
            SerializedProperty position =
                point.FindPropertyRelative("position");

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField(
                    $"Ship {i} -> Formation Slot {i}",
                    EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(position, new GUIContent("Start Position"));
            }
        }
    }

    private void DrawIndividualEntranceShapePresets()
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("Entry Shape Presets", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(
                individualEntranceShapeCenter,
                new GUIContent(
                    "Shape Center",
                    "Center in the coordinate space selected for Entrance Path."));
            EditorGUILayout.PropertyField(
                individualEntranceShapeRadius,
                new GUIContent("Radius"));
            EditorGUILayout.PropertyField(
                individualEntranceShapeFlattening,
                new GUIContent(
                    "Flattening X/Y",
                    "Scales the shape horizontally and vertically. Use different values for an ellipse or rectangle."));
            EditorGUILayout.PropertyField(
                individualEntranceShapeRotationDegrees,
                new GUIContent(
                    "Rotation (Degrees)",
                    "Rotates the generated shape around Shape Center. Positive values rotate counter-clockwise."));

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Circle"))
                    ApplyIndividualEntranceShapePreset(IndividualEntranceShapePreset.Circle);

                if (GUILayout.Button("Triangle"))
                    ApplyIndividualEntranceShapePreset(IndividualEntranceShapePreset.Triangle);

                if (GUILayout.Button("Rectangle"))
                    ApplyIndividualEntranceShapePreset(IndividualEntranceShapePreset.Rectangle);

                if (GUILayout.Button("Diamond"))
                    ApplyIndividualEntranceShapePreset(IndividualEntranceShapePreset.Diamond);
            }

            EditorGUILayout.HelpBox(
                "Set the shape values, then press a preset. It applies one entry point per formation slot. The resulting points can still be moved independently in the Inspector or Scene view.",
                MessageType.None);
        }
    }

    private void ApplyIndividualEntranceShapePreset(
        IndividualEntranceShapePreset preset)
    {
        int pointCount = Mathf.Max(1, GetEditorEffectiveEnemyCount());
        ResizeIndividualEntrancePoints(pointCount);

        Vector3[] positions = CreateIndividualEntranceShapePositions(preset, pointCount);
        for (int i = 0; i < positions.Length; i++)
        {
            SerializedProperty point =
                individualEntrancePoints.GetArrayElementAtIndex(i);
            point.FindPropertyRelative("position").vector3Value = positions[i];
        }

        activeIndividualEntrancePointIndex = -1;
        SceneView.RepaintAll();
    }

    private Vector3[] CreateIndividualEntranceShapePositions(
        IndividualEntranceShapePreset preset,
        int pointCount)
    {
        Vector3 center = individualEntranceShapeCenter.vector3Value;
        float radius = Mathf.Max(0f, individualEntranceShapeRadius.floatValue);
        Vector2 flattening = individualEntranceShapeFlattening.vector2Value;
        flattening.x = Mathf.Max(0.01f, flattening.x);
        flattening.y = Mathf.Max(0.01f, flattening.y);

        Vector3[] positions;
        switch (preset)
        {
            case IndividualEntranceShapePreset.Circle:
                positions = CreateIndividualEntranceCirclePositions(
                    center,
                    radius,
                    flattening,
                    pointCount);
                break;

            case IndividualEntranceShapePreset.Triangle:
                positions = CreateIndividualEntrancePolygonPositions(
                    center,
                    new[]
                    {
                        center + new Vector3(0f, radius * flattening.y, 0f),
                        center + new Vector3(
                            -0.8660254f * radius * flattening.x,
                            -0.5f * radius * flattening.y,
                            0f),
                        center + new Vector3(
                            0.8660254f * radius * flattening.x,
                            -0.5f * radius * flattening.y,
                            0f)
                    },
                    pointCount);
                break;

            case IndividualEntranceShapePreset.Rectangle:
                positions = CreateIndividualEntrancePolygonPositions(
                    center,
                    new[]
                    {
                        center + new Vector3(-radius * flattening.x, radius * flattening.y, 0f),
                        center + new Vector3(radius * flattening.x, radius * flattening.y, 0f),
                        center + new Vector3(radius * flattening.x, -radius * flattening.y, 0f),
                        center + new Vector3(-radius * flattening.x, -radius * flattening.y, 0f)
                    },
                    pointCount);
                break;

            default:
                positions = CreateIndividualEntrancePolygonPositions(
                    center,
                    new[]
                    {
                        center + new Vector3(0f, radius * flattening.y, 0f),
                        center + new Vector3(radius * flattening.x, 0f, 0f),
                        center + new Vector3(0f, -radius * flattening.y, 0f),
                        center + new Vector3(-radius * flattening.x, 0f, 0f)
                    },
                    pointCount);
                break;
        }

        RotateIndividualEntranceShapePositions(
            positions,
            center,
            individualEntranceShapeRotationDegrees.floatValue);
        return positions;
    }

    private static void RotateIndividualEntranceShapePositions(
        Vector3[] positions,
        Vector3 center,
        float rotationDegrees)
    {
        if (positions == null
            || positions.Length == 0
            || Mathf.Approximately(rotationDegrees, 0f))
        {
            return;
        }

        float radians = rotationDegrees * Mathf.Deg2Rad;
        float sin = Mathf.Sin(radians);
        float cos = Mathf.Cos(radians);
        for (int i = 0; i < positions.Length; i++)
        {
            Vector3 offset = positions[i] - center;
            positions[i] = center + new Vector3(
                offset.x * cos - offset.y * sin,
                offset.x * sin + offset.y * cos,
                offset.z);
        }
    }

    private static Vector3[] CreateIndividualEntranceCirclePositions(
        Vector3 center,
        float radius,
        Vector2 flattening,
        int pointCount)
    {
        Vector3[] positions = new Vector3[pointCount];
        if (pointCount == 1)
        {
            positions[0] = center;
            return positions;
        }

        for (int i = 0; i < pointCount; i++)
        {
            float angle = 90f - 360f * i / pointCount;
            float radians = angle * Mathf.Deg2Rad;
            positions[i] = center + new Vector3(
                Mathf.Cos(radians) * radius * flattening.x,
                Mathf.Sin(radians) * radius * flattening.y,
                0f);
        }

        return positions;
    }

    private static Vector3[] CreateIndividualEntrancePolygonPositions(
        Vector3 center,
        Vector3[] vertices,
        int pointCount)
    {
        Vector3[] positions = new Vector3[pointCount];
        if (pointCount == 1)
        {
            positions[0] = center;
            return positions;
        }

        float perimeter = 0f;
        for (int i = 0; i < vertices.Length; i++)
            perimeter += Vector3.Distance(vertices[i], vertices[(i + 1) % vertices.Length]);

        if (perimeter <= Mathf.Epsilon)
        {
            for (int i = 0; i < positions.Length; i++)
                positions[i] = center;

            return positions;
        }

        for (int pointIndex = 0; pointIndex < positions.Length; pointIndex++)
        {
            float remainingDistance = perimeter * pointIndex / positions.Length;
            for (int edgeIndex = 0; edgeIndex < vertices.Length; edgeIndex++)
            {
                Vector3 start = vertices[edgeIndex];
                Vector3 end = vertices[(edgeIndex + 1) % vertices.Length];
                float edgeLength = Vector3.Distance(start, end);
                if (remainingDistance <= edgeLength || edgeIndex == vertices.Length - 1)
                {
                    float t = edgeLength <= Mathf.Epsilon
                        ? 0f
                        : Mathf.Clamp01(remainingDistance / edgeLength);
                    positions[pointIndex] = Vector3.Lerp(start, end, t);
                    break;
                }

                remainingDistance -= edgeLength;
            }
        }

        return positions;
    }

    private void ResizeIndividualEntrancePoints(int newSize)
    {
        int oldSize = individualEntrancePoints.arraySize;
        individualEntrancePoints.arraySize = Mathf.Max(0, newSize);

        for (int i = oldSize; i < individualEntrancePoints.arraySize; i++)
            InitializeIndividualEntrancePoint(i);

        if (activeIndividualEntrancePointIndex >= individualEntrancePoints.arraySize)
            activeIndividualEntrancePointIndex = individualEntrancePoints.arraySize - 1;
    }

    private void InitializeIndividualEntrancePoint(int index)
    {
        SerializedProperty point =
            individualEntrancePoints.GetArrayElementAtIndex(index);
        DirectedEnemySubWave wave = (DirectedEnemySubWave)target;
        int count = Mathf.Max(1, GetEditorEffectiveEnemyCount());
        float horizontalOffset = (index - (count - 1) * 0.5f) * 0.65f;
        Vector3 worldPosition = GetEditorSpawnPosition(wave)
            + Vector3.right * horizontalOffset;

        point.FindPropertyRelative("position").vector3Value = FromWorld(
            wave,
            worldPosition,
            (DirectedWaveCoordinateSpace)pathCoordinateSpace.enumValueIndex);
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
                DeletePathCheckpoint(pathCheckpoints.arraySize - 1);

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

        int checkpointToDelete = -1;
        for (int i = 0; i < pathCheckpoints.arraySize; i++)
        {
            SerializedProperty checkpoint =
                pathCheckpoints.GetArrayElementAtIndex(i);

            Rect checkpointRect = EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            SerializedProperty position = checkpoint.FindPropertyRelative("position");
            Rect headerRect = GUILayoutUtility.GetRect(
                0f,
                EditorGUIUtility.singleLineHeight,
                GUILayout.ExpandWidth(true));
            Rect dragHandleRect = new Rect(
                headerRect.x,
                headerRect.y,
                EditorGUIUtility.singleLineHeight,
                headerRect.height);
            Rect deleteRect = new Rect(
                headerRect.xMax - 56f,
                headerRect.y,
                56f,
                headerRect.height);
            Rect foldoutRect = new Rect(
                dragHandleRect.xMax + 2f,
                headerRect.y,
                headerRect.width - dragHandleRect.width - deleteRect.width - 6f,
                headerRect.height);

            EditorGUIUtility.AddCursorRect(dragHandleRect, MouseCursor.Pan);
            GUI.Label(dragHandleRect, EditorGUIUtility.IconContent("d_ToolHandleCenter"));
            BeginPathCheckpointDrag(i, dragHandleRect);
            checkpoint.isExpanded = EditorGUI.Foldout(
                foldoutRect,
                checkpoint.isExpanded,
                $"Checkpoint {i}  {position.vector3Value}",
                true,
                EditorStyles.foldoutHeader);
            if (GUI.Button(deleteRect, "Delete"))
                checkpointToDelete = i;

            if (checkpoint.isExpanded)
            {
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
                else if (UsesEntrancePathLoop()
                    && !UsesIndividualEntrancePoints()
                    && pathCheckpoints.arraySize >= 2)
                {
                    if (entranceLoopTeleportToStart.boolValue)
                    {
                        EditorGUILayout.HelpBox(
                            "This checkpoint is the teleport exit. Its Duration/Motion/Ease to Loop Start are ignored.",
                            MessageType.None);
                    }
                    else
                    {
                        EditorGUILayout.PropertyField(
                            durationToNext,
                            new GUIContent(
                                "Duration To Loop Start",
                                "Duration of the segment from the last checkpoint back to the selected Loop Start Checkpoint."));
                        EditorGUILayout.PropertyField(
                            motionToNext,
                            new GUIContent("Motion To Loop Start"));
                        EditorGUILayout.PropertyField(
                            easeToNext,
                            new GUIContent("Ease To Loop Start"));
                    }
                }

                if (positionChanged && i > 0)
                    SyncCheckpointDurationAndSpeed(i - 1, false, false, true);

                if (i == pathCheckpoints.arraySize - 1
                    && !(UsesEntrancePathLoop()
                        && !UsesIndividualEntrancePoints()
                        && pathCheckpoints.arraySize >= 2))
                {
                    EditorGUILayout.HelpBox(
                        "Last checkpoint has no next segment, so Duration/Speed/Motion/Ease are ignored.",
                        MessageType.None);
                }
            }

            EditorGUILayout.EndVertical();
            UpdatePathCheckpointDragTarget(i, checkpointRect);
            DrawPathCheckpointDropMarker(i, checkpointRect);
        }

        CompletePathCheckpointDrag();
        if (checkpointToDelete >= 0)
            DeletePathCheckpoint(checkpointToDelete);
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

    private void BeginPathCheckpointDrag(int checkpointIndex, Rect dragHandleRect)
    {
        Event currentEvent = Event.current;
        if (currentEvent.type != EventType.MouseDown
            || currentEvent.button != 0
            || !dragHandleRect.Contains(currentEvent.mousePosition))
        {
            return;
        }

        draggedPathCheckpointIndex = checkpointIndex;
        pathCheckpointDropIndex = checkpointIndex;
        GUIUtility.hotControl = GUIUtility.GetControlID(FocusType.Passive);
        currentEvent.Use();
    }

    private void UpdatePathCheckpointDragTarget(
        int checkpointIndex,
        Rect checkpointRect)
    {
        if (draggedPathCheckpointIndex < 0)
            return;

        Event currentEvent = Event.current;
        EventType rawType = currentEvent.rawType;
        if ((rawType != EventType.MouseDrag && rawType != EventType.MouseUp)
            || !checkpointRect.Contains(currentEvent.mousePosition))
        {
            return;
        }

        pathCheckpointDropIndex = currentEvent.mousePosition.y < checkpointRect.center.y
            ? checkpointIndex
            : checkpointIndex + 1;
        Repaint();
    }

    private void DrawPathCheckpointDropMarker(int checkpointIndex, Rect checkpointRect)
    {
        if (draggedPathCheckpointIndex < 0
            || pathCheckpointDropIndex < 0)
        {
            return;
        }

        bool drawBefore = pathCheckpointDropIndex == checkpointIndex;
        bool drawAfter = checkpointIndex == pathCheckpoints.arraySize - 1
            && pathCheckpointDropIndex == pathCheckpoints.arraySize;
        if (!drawBefore && !drawAfter)
            return;

        float y = drawBefore ? checkpointRect.yMin : checkpointRect.yMax;
        EditorGUI.DrawRect(
            new Rect(checkpointRect.xMin, y - 1f, checkpointRect.width, 2f),
            new Color(0.2f, 0.65f, 1f, 0.9f));
    }

    private void CompletePathCheckpointDrag()
    {
        if (draggedPathCheckpointIndex < 0
            || Event.current.rawType != EventType.MouseUp)
        {
            return;
        }

        int fromIndex = draggedPathCheckpointIndex;
        int insertionIndex = Mathf.Clamp(
            pathCheckpointDropIndex,
            0,
            pathCheckpoints.arraySize);
        int toIndex = insertionIndex > fromIndex
            ? insertionIndex - 1
            : insertionIndex;

        if (fromIndex != toIndex)
            MovePathCheckpoint(fromIndex, toIndex);

        CancelPathCheckpointDrag();
        Event.current.Use();
    }

    private void CancelPathCheckpointDrag()
    {
        draggedPathCheckpointIndex = -1;
        pathCheckpointDropIndex = -1;
        GUIUtility.hotControl = 0;
    }

    private void MovePathCheckpoint(int fromIndex, int toIndex)
    {
        if (fromIndex < 0
            || toIndex < 0
            || fromIndex >= pathCheckpoints.arraySize
            || toIndex >= pathCheckpoints.arraySize)
        {
            return;
        }

        Undo.RecordObject(target, "Reorder Path Checkpoint");
        int loopStartIndex = entranceLoopStartCheckpointIndex.intValue;
        pathCheckpoints.MoveArrayElement(fromIndex, toIndex);
        entranceLoopStartCheckpointIndex.intValue = GetMovedCheckpointIndex(
            loopStartIndex,
            fromIndex,
            toIndex);
        ClampEntranceLoopStartCheckpointIndex();
        EnsurePathCheckpointSpeedsInitialized();
    }

    private void DeletePathCheckpoint(int index)
    {
        if (index < 0 || index >= pathCheckpoints.arraySize)
            return;

        Undo.RecordObject(target, "Delete Path Checkpoint");
        int loopStartIndex = entranceLoopStartCheckpointIndex.intValue;
        pathCheckpoints.DeleteArrayElementAtIndex(index);

        if (index < loopStartIndex)
            loopStartIndex--;
        else if (index == loopStartIndex)
            loopStartIndex = Mathf.Min(loopStartIndex, pathCheckpoints.arraySize - 2);

        entranceLoopStartCheckpointIndex.intValue = Mathf.Max(0, loopStartIndex);
        ClampEntranceLoopStartCheckpointIndex();
        EnsurePathCheckpointSpeedsInitialized();
    }

    private static int GetMovedCheckpointIndex(
        int currentIndex,
        int fromIndex,
        int toIndex)
    {
        if (currentIndex == fromIndex)
            return toIndex;

        if (fromIndex < currentIndex && currentIndex <= toIndex)
            return currentIndex - 1;

        if (toIndex <= currentIndex && currentIndex < fromIndex)
            return currentIndex + 1;

        return currentIndex;
    }

    private void ClampEntranceLoopStartCheckpointIndex()
    {
        int maximumStartIndex = Mathf.Max(0, pathCheckpoints.arraySize - 2);
        entranceLoopStartCheckpointIndex.intValue = Mathf.Clamp(
            entranceLoopStartCheckpointIndex.intValue,
            0,
            maximumStartIndex);
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

        ClampEntranceLoopStartCheckpointIndex();
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

                DirectedWaveFormationLayout layout =
                    (DirectedWaveFormationLayout)formationLayout.enumValueIndex;

                if (layout != DirectedWaveFormationLayout.Grid
                    && layout != DirectedWaveFormationLayout.CustomPoints)
                {
                    EditorGUILayout.PropertyField(spacing);
                }

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

            DrawEnemySlotSelectionControls();

            EditorGUILayout.PropertyField(settleDuration);
            EditorGUILayout.PropertyField(settleCurve);

            EditorGUILayout.Space(4f);
            using (new EditorGUI.DisabledScope(frozen))
                DrawFormationPresetButtons();
        }

        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    private void DrawEnemySlotSelectionControls()
    {
        ClampSelectedEnemySlot();

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("Enemy Slots", EditorStyles.boldLabel);

            int slotCount = GetEditorEnemySlotCount();
            if (slotCount <= 0)
            {
                enemySlotSelectionMode = false;
                selectedEnemySlotIndex = -1;
                EditorGUILayout.HelpBox(
                    "Create at least one final formation slot before assigning enemy prefabs.",
                    MessageType.Info);
                return;
            }

            EditorGUI.BeginChangeCheck();
            bool selectionMode = EditorGUILayout.ToggleLeft(
                "Select Enemy Slots In Scene",
                enemySlotSelectionMode);
            if (EditorGUI.EndChangeCheck())
            {
                enemySlotSelectionMode = selectionMode;
                SceneView.RepaintAll();
            }

            EditorGUILayout.HelpBox(
                "Click a final formation point in Scene View, then assign its Enemy Prefab Override here. "
                + "An empty override uses the global Enemy Prefab. Slot index identifies the final formation point; Spawn Order only changes when it enters.",
                MessageType.None);

            if (selectedEnemySlotIndex < 0)
            {
                EditorGUILayout.LabelField("Selected Slot", "None");
                return;
            }

            Enemy enemyOverride = GetEditorEnemySlotOverride(selectedEnemySlotIndex);
            EditorGUILayout.LabelField(
                "Selected Slot",
                $"Slot {selectedEnemySlotIndex}");
            EditorGUILayout.LabelField(
                "Resolved Prefab",
                GetEditorEnemySlotPrefabLabel(enemyOverride));

            if (UsesTransformEnemySlots())
            {
                Transform root = formationPointsRoot.objectReferenceValue as Transform;
                if (root != null && selectedEnemySlotIndex < root.childCount)
                    DrawTransformPointEnemyOverrideFields(
                        root.GetChild(selectedEnemySlotIndex));
            }
            else
            {
                EditorGUI.BeginChangeCheck();
                Enemy changedOverride = (Enemy)EditorGUILayout.ObjectField(
                    new GUIContent(
                        "Enemy Prefab Override",
                        "Leave empty to use the global Enemy Prefab."),
                    enemyOverride,
                    typeof(Enemy),
                    false);
                if (EditorGUI.EndChangeCheck())
                    SetEditorEnemySlotOverride(selectedEnemySlotIndex, changedOverride);

                using (new EditorGUI.DisabledScope(enemyOverride == null))
                {
                    if (GUILayout.Button("Clear Selected Override"))
                        SetEditorEnemySlotOverride(selectedEnemySlotIndex, null);
                }

                EditorGUILayout.HelpBox(
                    "Behavior overrides are available after converting this formation to Transform Points.",
                    MessageType.Info);
            }
        }
    }

    private int GetEditorEnemySlotCount()
    {
        return Mathf.Max(0, GetEditorEffectiveEnemyCount());
    }

    private void ClampSelectedEnemySlot()
    {
        if (selectedEnemySlotIndex < 0)
            return;

        int slotCount = GetEditorEnemySlotCount();
        selectedEnemySlotIndex = slotCount > 0
            ? Mathf.Min(selectedEnemySlotIndex, slotCount - 1)
            : -1;
    }

    private bool UsesTransformEnemySlots()
    {
        return formationFrozen.boolValue || IsTransformPointsFormation();
    }

    private Enemy GetEditorEnemySlotOverride(int index)
    {
        if (index < 0)
            return null;

        if (UsesTransformEnemySlots())
        {
            Transform root = formationPointsRoot.objectReferenceValue as Transform;
            if (root == null || index >= root.childCount)
                return null;

            DirectedWaveEnemyOverride enemyOverride =
                root.GetChild(index).GetComponent<DirectedWaveEnemyOverride>();
            return enemyOverride != null
                ? enemyOverride.EnemyPrefabOverride
                : null;
        }

        SerializedProperty overrides = IsCustomPointsFormation()
            ? customFormationEnemyOverrides
            : proceduralFormationEnemyOverrides;
        if (overrides == null || index >= overrides.arraySize)
            return null;

        return overrides.GetArrayElementAtIndex(index)
            .objectReferenceValue as Enemy;
    }

    private string GetEditorEnemySlotPrefabLabel(Enemy enemyOverride)
    {
        if (enemyOverride != null)
            return enemyOverride.name;

        Enemy globalEnemy = enemyPrefab.objectReferenceValue as Enemy;
        return globalEnemy != null
            ? $"Global: {globalEnemy.name}"
            : "None";
    }

    private void SetEditorEnemySlotOverride(int index, Enemy enemyOverride)
    {
        if (index < 0 || index >= GetEditorEnemySlotCount())
            return;

        if (UsesTransformEnemySlots())
        {
            Transform root = formationPointsRoot.objectReferenceValue as Transform;
            if (root == null || index >= root.childCount)
                return;

            if (!SetTransformPointEnemyOverride(
                    root.GetChild(index),
                    enemyOverride,
                    "Set Formation Slot Enemy Override"))
            {
                return;
            }

            InvalidatePreviewSession();
            return;
        }

        SerializedProperty overrides;
        if (IsCustomPointsFormation())
        {
            EnsureCustomFormationOverrideSize();
            overrides = customFormationEnemyOverrides;
        }
        else
        {
            EnsureProceduralFormationOverrideSize();
            overrides = proceduralFormationEnemyOverrides;
        }

        if (overrides == null || index >= overrides.arraySize)
            return;

        SerializedProperty element = overrides.GetArrayElementAtIndex(index);
        if (element.objectReferenceValue == enemyOverride)
            return;

        Undo.RecordObject(target, "Set Formation Slot Enemy Override");
        element.objectReferenceValue = enemyOverride;
        InvalidatePreviewSession();
    }

    private bool SetTransformPointEnemyOverride(
        Transform point,
        Enemy enemyOverridePrefab,
        string undoName)
    {
        if (point == null)
            return false;

        DirectedWaveEnemyOverride enemyOverride =
            point.GetComponent<DirectedWaveEnemyOverride>();
        if (enemyOverride != null
            && enemyOverride.EnemyPrefabOverride == enemyOverridePrefab)
        {
            return false;
        }

        if (enemyOverride == null)
        {
            if (enemyOverridePrefab == null)
                return false;

            enemyOverride = Undo.AddComponent<DirectedWaveEnemyOverride>(
                point.gameObject);
        }

        Undo.RecordObject(enemyOverride, undoName);

        SerializedObject overrideObject = new SerializedObject(enemyOverride);
        SerializedProperty overridePrefab =
            overrideObject.FindProperty("enemyPrefabOverride");
        overrideObject.Update();
        overridePrefab.objectReferenceValue = enemyOverridePrefab;
        overrideObject.ApplyModifiedProperties();

        EditorUtility.SetDirty(enemyOverride);
        if (PrefabUtility.IsPartOfPrefabInstance(enemyOverride))
            PrefabUtility.RecordPrefabInstancePropertyModifications(enemyOverride);

        return true;
    }

    private void EnsureProceduralFormationOverrideSize()
    {
        if (proceduralFormationEnemyOverrides == null
            || UsesTransformEnemySlots()
            || IsCustomPointsFormation())
        {
            return;
        }

        int requiredSize = GetEditorEffectiveEnemyCount();
        if (proceduralFormationEnemyOverrides.arraySize < requiredSize)
            proceduralFormationEnemyOverrides.arraySize = requiredSize;
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

        ApplyModifiedPropertiesAndInvalidatePreview();
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

        ApplyModifiedPropertiesAndInvalidatePreview();
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

            DrawGridMatrixSpacingFields(showDimensionFields);

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

    private void DrawGridMatrixSpacingFields(bool applyToCustomPoints)
    {
        Vector2 currentSpacing = spacing.vector2Value;
        EditorGUILayout.LabelField("Cell Spacing", EditorStyles.miniBoldLabel);

        EditorGUI.BeginChangeCheck();
        float horizontalSpacing = EditorGUILayout.FloatField(
            "Horizontal Spacing",
            currentSpacing.x);
        float verticalSpacing = EditorGUILayout.FloatField(
            "Vertical Spacing",
            currentSpacing.y);
        if (!EditorGUI.EndChangeCheck())
            return;

        System.Collections.Generic.Dictionary<int, UnityEngine.Object>
            overridesByCell = CaptureGridMatrixEnemyOverrides();
        Undo.RecordObject(target, "Update Grid Matrix Spacing");
        spacing.vector2Value = new Vector2(
            Mathf.Max(0f, horizontalSpacing),
            Mathf.Max(0f, verticalSpacing));

        if (applyToCustomPoints && HasValidGridMatrix())
            ApplyGridMatrixToCustomPoints(overridesByCell);
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

        ApplyModifiedPropertiesAndInvalidatePreview();
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

        ApplyModifiedPropertiesAndInvalidatePreview();
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

        ApplyModifiedPropertiesAndInvalidatePreview();
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
        DrawTransformPointEnemyOverrideFields(point);
    }

    private void DrawTransformPointEnemyOverrideFields(Transform point)
    {
        if (point == null)
            return;

        DirectedWaveEnemyOverride enemyOverride =
            point.GetComponent<DirectedWaveEnemyOverride>();
        if (enemyOverride == null)
        {
            if (GUILayout.Button("Add Enemy Slot Override"))
            {
                Undo.AddComponent<DirectedWaveEnemyOverride>(point.gameObject);
                InvalidatePreviewSession();
            }

            return;
        }

        SerializedObject overrideObject = new SerializedObject(enemyOverride);
        SerializedProperty overridePrefab =
            overrideObject.FindProperty("enemyPrefabOverride");
        SerializedProperty overrideTint =
            overrideObject.FindProperty("overrideSpriteTint");
        SerializedProperty tint = overrideObject.FindProperty("spriteTint");
        SerializedProperty overrideAttack =
            overrideObject.FindProperty("overrideBurstAttackSettings");
        SerializedProperty attackSettings =
            overrideObject.FindProperty("burstAttackSettings");
        SerializedProperty overrideRotation =
            overrideObject.FindProperty("overrideFourWayRotation");
        SerializedProperty rotationSettings =
            overrideObject.FindProperty("fourWayRotation");

        overrideObject.Update();
        EditorGUI.BeginChangeCheck();
        EditorGUILayout.PropertyField(
            overridePrefab,
            new GUIContent("Enemy Prefab Override"));
        EditorGUILayout.PropertyField(
            overrideTint,
            new GUIContent("Override Sprite Tint"));
        if (overrideTint.boolValue)
            EditorGUILayout.PropertyField(tint, new GUIContent("Sprite Tint"));

        EditorGUILayout.Space(2f);
        EditorGUILayout.PropertyField(
            overrideAttack,
            new GUIContent("Override Attack Pattern"));
        if (overrideAttack.boolValue)
            EditorGUILayout.PropertyField(
                attackSettings,
                new GUIContent("Attack Pattern"),
                true);

        EditorGUILayout.Space(2f);
        EditorGUILayout.PropertyField(
            overrideRotation,
            new GUIContent("Override Four Way Rotation"));
        if (overrideRotation.boolValue)
            EditorGUILayout.PropertyField(
                rotationSettings,
                new GUIContent("Four Way Rotation"),
                true);

        if (!EditorGUI.EndChangeCheck())
            return;

        overrideObject.ApplyModifiedProperties();
        EditorUtility.SetDirty(enemyOverride);
        if (PrefabUtility.IsPartOfPrefabInstance(enemyOverride))
            PrefabUtility.RecordPrefabInstancePropertyModifications(enemyOverride);

        InvalidatePreviewSession();
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
        Enemy[] enemyOverrides = new Enemy[count];

        for (int i = 0; i < count; i++)
        {
            worldPositions[i] = GetFormationWorldPosition(i, wave);
            enemyOverrides[i] = GetEditorEnemySlotOverride(i);
        }

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
            SetTransformPointEnemyOverride(
                child,
                enemyOverrides[i],
                "Preserve Formation Slot Enemy Override");
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
            EditorGUILayout.Space(4f);
            EditorGUILayout.PropertyField(postStartDelay);
            EditorGUILayout.PropertyField(
                postCommandPipelineLoop,
                new GUIContent(
                    "Infinite",
                    "Repeats the complete Post Commands sequence indefinitely."));
            using (new EditorGUI.DisabledScope(postCommandPipelineLoop.boolValue))
            {
                EditorGUILayout.PropertyField(
                    postCommandPipelineFixedCount,
                    new GUIContent(
                        "Fixated",
                        "Number of complete Post Commands sequence activations."));
            }
        }

        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    private void DrawPostCommands()
    {
        EditorGUILayout.HelpBox(
            "Post Commands are executed from top to bottom as a pipeline. "
            + "Use Duration/Hold/Target Position inside each command to build a timeline: "
            + "Move -> Morph -> Wheel -> Move Back -> Wheel...",
            MessageType.None);
        DrawPostCommandPipelineList();

        using (new EditorGUILayout.HorizontalScope())
        {
            DrawAddPostCommandButton(
                DirectedWavePostCommandType.Patrol,
                "Add Patrol");
            DrawAddPostCommandButton(
                DirectedWavePostCommandType.LocalMovement,
                "Add Local Behaviour");
            DrawAddPostCommandButton(
                DirectedWavePostCommandType.Wobble,
                "Add Wobble");
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            DrawAddPostCommandButton(
                DirectedWavePostCommandType.CircularMovement,
                "Add Circle");
            DrawAddPostCommandButton(
                DirectedWavePostCommandType.FormationRotation,
                "Add Wheel");
            DrawAddPostCommandButton(
                DirectedWavePostCommandType.FormationMorph,
                "Add Morph");
            DrawAddPostCommandButton(
                DirectedWavePostCommandType.FormationReorder,
                "Add Reorder");
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            DrawAddPostCommandButton(
                DirectedWavePostCommandType.Wait,
                "Add Wait");
            DrawAddPostCommandButton(
                DirectedWavePostCommandType.Parallel,
                "Add Parallel");
            DrawAddPostCommandButton(
                DirectedWavePostCommandType.Loop,
                "Add Loop");

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

    private static void DrawCommandFoldout(SerializedProperty command)
    {
        Rect rect = EditorGUILayout.GetControlRect(
            false,
            EditorGUIUtility.singleLineHeight,
            GUILayout.Width(14f));
        command.isExpanded = EditorGUI.Foldout(
            rect,
            command.isExpanded,
            GUIContent.none,
            false);
    }

    private static void DrawPostCommandTypeField(
        SerializedProperty command,
        SerializedProperty type)
    {
        int previousType = type.enumValueIndex;
        if (previousType == (int)DirectedWavePostCommandType.LegacyAttack)
        {
            EditorGUILayout.LabelField(
                "Removed Attack",
                EditorStyles.miniLabel);
            return;
        }

        int selectedIndex = 0;
        for (int i = 0; i < SelectablePostCommandTypes.Length; i++)
        {
            if ((int)SelectablePostCommandTypes[i] != previousType)
                continue;

            selectedIndex = i;
            break;
        }

        int nextIndex = EditorGUILayout.Popup(
            selectedIndex,
            SelectablePostCommandLabels);
        type.enumValueIndex = (int)SelectablePostCommandTypes[nextIndex];

        if (previousType == (int)DirectedWavePostCommandType.LocalMovement
            || type.enumValueIndex != (int)DirectedWavePostCommandType.LocalMovement)
        {
            return;
        }

        SerializedProperty coordinateSpace =
            command.FindPropertyRelative("targetOffsetCoordinateSpace");
        if (coordinateSpace != null)
        {
            coordinateSpace.enumValueIndex =
                (int)DirectedWaveCoordinateSpace.LocalToSubWave;
        }
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
            DrawCommandFoldout(command);
            enabled.boolValue = EditorGUILayout.Toggle(
                enabled.boolValue,
                GUILayout.Width(18f));
            EditorGUILayout.LabelField(
                $"#{index + 1}",
                GUILayout.Width(34f));
            DrawPostCommandTypeField(command, type);
            commandType = (DirectedWavePostCommandType)type.enumValueIndex;

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

        if (command.isExpanded)
        {
            using (new EditorGUI.DisabledScope(!enabled.boolValue))
                DrawPostCommandSettings(command, commandType, index);
        }

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

        if (IsInfiniteContinuousCommand(command, commandType)
            && index < postCommands.arraySize - 1)
        {
            EditorGUILayout.HelpBox(
                "This movement command is Infinite. Commands below it will never start.",
                MessageType.Warning);
        }

        if (command.isExpanded && activePostCommandIndex == index)
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

    private static bool IsInfiniteContinuousCommand(
        SerializedProperty command,
        DirectedWavePostCommandType commandType)
    {
        SerializedProperty completionMode =
            command.FindPropertyRelative("completionMode");
        if (completionMode == null
            || completionMode.enumValueIndex
                != (int)DirectedWavePostCommandCompletionMode.Infinite)
        {
            return false;
        }

        if (commandType == DirectedWavePostCommandType.FormationRotation)
        {
            SerializedProperty continuous =
                command.FindPropertyRelative("continuousFormationRotation");
            return continuous != null && continuous.boolValue;
        }

        return commandType == DirectedWavePostCommandType.Patrol
            || commandType == DirectedWavePostCommandType.Wobble
            || commandType == DirectedWavePostCommandType.CircularMovement;
    }

    private void SetActivePatrolPoint(int pointIndex, int commandIndex)
    {
        bool pointSelectionChanged = activePatrolPointIndex != pointIndex;
        bool commandSelectionChanged = activePatrolCommandIndex != commandIndex;
        activePatrolPointIndex = pointIndex;

        if (IsTopLevelPatrolCommand(commandIndex))
        {
            activePatrolCommandIndex = commandIndex;
            SetActivePostCommandIndex(commandIndex);
        }
        else
        {
            int resolvedCommandIndex = GetPatrolPreviewCommandIndex();
            if (resolvedCommandIndex >= 0)
            {
                commandSelectionChanged |=
                    activePatrolCommandIndex != resolvedCommandIndex;
                activePatrolCommandIndex = resolvedCommandIndex;
            }
        }

        if (pointSelectionChanged || commandSelectionChanged)
            RepaintPreviewSceneView();
    }

    private int GetPatrolPreviewCommandIndex()
    {
        if (IsTopLevelPatrolCommand(activePatrolCommandIndex))
            return activePatrolCommandIndex;

        if (IsTopLevelPatrolCommand(activePostCommandIndex))
            return activePostCommandIndex;

        if (postCommands == null)
            return -1;

        for (int i = 0; i < postCommands.arraySize; i++)
        {
            SerializedProperty command = postCommands.GetArrayElementAtIndex(i);
            if (command == null
                || !command.FindPropertyRelative("enabled").boolValue)
            {
                continue;
            }

            if (IsTopLevelPatrolCommand(i))
                return i;
        }

        return -1;
    }

    private bool IsTopLevelPatrolCommand(int commandIndex)
    {
        if (postCommands == null
            || commandIndex < 0
            || commandIndex >= postCommands.arraySize)
        {
            return false;
        }

        SerializedProperty command = postCommands.GetArrayElementAtIndex(commandIndex);
        SerializedProperty type = command?.FindPropertyRelative("type");
        return type != null
            && type.enumValueIndex == (int)DirectedWavePostCommandType.Patrol;
    }
    private void SetActivePostCommandIndex(int index)
    {
        int nextIndex = Mathf.Clamp(
            index,
            -1,
            postCommands != null ? postCommands.arraySize - 1 : -1);
        if (activePostCommandIndex == nextIndex)
            return;

        activePostCommandIndex = nextIndex;
        InvalidatePreviewSession();
        SceneView.RepaintAll();
    }

    private void DrawPostCommandSettings(
        SerializedProperty command,
        DirectedWavePostCommandType commandType,
        int commandIndex = -1)
    {
        SerializedProperty duration = command.FindPropertyRelative("duration");
        SerializedProperty completionMode =
            command.FindPropertyRelative("completionMode");
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
        SerializedProperty targetOffsetCoordinateSpace =
            command.FindPropertyRelative("targetOffsetCoordinateSpace");
        SerializedProperty rotationDegrees =
            command.FindPropertyRelative("rotationDegrees");
        SerializedProperty continuousFormationRotation =
            command.FindPropertyRelative("continuousFormationRotation");
        SerializedProperty curve = command.FindPropertyRelative("curve");
        SerializedProperty formationReorderMode =
            command.FindPropertyRelative("formationReorderMode");
        SerializedProperty formationReorderUseTargetCenter =
            command.FindPropertyRelative("formationReorderUseTargetCenter");
        SerializedProperty formationReorderTargetCenter =
            command.FindPropertyRelative("formationReorderTargetCenter");
        SerializedProperty formationReorderSpeed =
            command.FindPropertyRelative("formationReorderSpeed");
        SerializedProperty formationReorderStartInterval =
            command.FindPropertyRelative("formationReorderStartInterval");
        SerializedProperty formationReorderShipsPerBatch =
            command.FindPropertyRelative("formationReorderShipsPerBatch");
        SerializedProperty formationReorderRandomSeed =
            command.FindPropertyRelative("formationReorderRandomSeed");
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
                    targetOffsetCoordinateSpace,
                    new GUIContent(
                        "Coordinate Space",
                        "World treats the value as an absolute world position. Local spaces transform it as a point, including the origin position, rotation and scale."));
                EditorGUILayout.PropertyField(
                    targetOffset,
                    new GUIContent(
                        "Target Position",
                        "Target position for the formation center in the selected coordinate space."));
                DrawTimedCommandFields(duration, holdDuration, curve);
                DrawLocalMovementSettings();
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
                if (continuousFormationRotation.boolValue)
                {
                    DrawContinuousCommandFields(
                        duration,
                        holdDuration,
                        curve,
                        completionMode,
                        "one complete formation rotation");
                }
                else
                {
                    DrawTimedCommandFields(duration, holdDuration, curve);
                }
                DrawFormationRotationSettings();
                break;

            case DirectedWavePostCommandType.FormationMorph:
                DrawTimedCommandFields(duration, holdDuration, curve);
                DrawMorphTargetFields(morphTarget);
                DrawFormationMorphSettings();
                break;

            case DirectedWavePostCommandType.FormationReorder:
                EditorGUILayout.LabelField(
                    "Target Formation Transform",
                    EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(
                    formationReorderUseTargetCenter,
                    new GUIContent(
                        "Use Target Center",
                        "Moves the entire resulting formation so its center reaches the world position below."));
                if (formationReorderUseTargetCenter.boolValue)
                {
                    EditorGUILayout.PropertyField(
                        formationReorderTargetCenter,
                        new GUIContent(
                            "Target Center (World)",
                            "World position of the resulting formation center."));
                }

                EditorGUILayout.Space(3f);
                EditorGUILayout.PropertyField(
                    formationReorderMode,
                    new GUIContent(
                        "Target Placement",
                        "Mirror reverses formation slots, Default keeps them unchanged, and Random uses a deterministic shuffled assignment."));
                if (formationReorderMode.enumValueIndex
                    == (int)DirectedWaveFormationReorderMode.Random)
                {
                    EditorGUILayout.PropertyField(
                        formationReorderRandomSeed,
                        new GUIContent(
                            "Random Seed",
                            "The same seed keeps the random assignment identical in Preview and Runtime. Change it to produce another arrangement."));
                }
                EditorGUILayout.PropertyField(
                    formationReorderSpeed,
                    new GUIContent(
                        "Ship Speed",
                        "Movement speed in world units per second while a ship changes its assigned formation position."));
                EditorGUILayout.PropertyField(
                    formationReorderStartInterval,
                    new GUIContent(
                        "Batch Start Interval",
                        "Delay between the start of consecutive groups. With Ships Per Batch = 1, this is the delay between ships."));
                EditorGUILayout.PropertyField(
                    formationReorderShipsPerBatch,
                    new GUIContent(
                        "Ships Per Batch",
                        "How many ships start their rebuild simultaneously."));
                EditorGUILayout.PropertyField(
                    holdDuration,
                    new GUIContent(
                        "Hold Duration",
                        "Optional pause after every ship has reached its new formation position."));
                EditorGUILayout.HelpBox(
                    "The command duration is calculated automatically from the longest route, target center, Ship Speed and Batch Start Interval.",
                    MessageType.None);
                break;

            case DirectedWavePostCommandType.Patrol:
                DrawContinuousCommandFields(
                    duration,
                    holdDuration,
                    curve,
                    completionMode,
                    "the complete Patrol route");
                DrawPatrol(commandIndex);
                break;

            case DirectedWavePostCommandType.Wobble:
                DrawContinuousCommandFields(
                    duration,
                    holdDuration,
                    curve,
                    completionMode,
                    "one complete Wobble cycle");
                DrawWobbleSettings();
                break;

            case DirectedWavePostCommandType.CircularMovement:
                DrawContinuousCommandFields(
                    duration,
                    holdDuration,
                    curve,
                    completionMode,
                    "one complete circular movement cycle");
                DrawCircularMovementSettings();
                break;

            case DirectedWavePostCommandType.LegacyAttack:
                EditorGUILayout.HelpBox(
                    "The old Attack command was removed. Delete this entry and configure Attack Controller on the subwave instead.",
                    MessageType.Warning);
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
                        DrawCommandFoldout(child);
                        enabled.boolValue = EditorGUILayout.Toggle(
                            enabled.boolValue,
                            GUILayout.Width(18f));
                        EditorGUILayout.LabelField($"#{i + 1}", GUILayout.Width(34f));
                        DrawPostCommandTypeField(child, type);
                        childType = (DirectedWavePostCommandType)type.enumValueIndex;
                        if (GUILayout.Button("Remove", GUILayout.Width(72f)))
                            removeIndex = i;
                    }

                    if (!child.isExpanded)
                        continue;

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
                        DrawCommandFoldout(child);
                        enabled.boolValue = EditorGUILayout.Toggle(
                            enabled.boolValue,
                            GUILayout.Width(18f));
                        EditorGUILayout.LabelField($"#{i + 1}", GUILayout.Width(34f));
                        DrawPostCommandTypeField(child, type);
                        childType = (DirectedWavePostCommandType)type.enumValueIndex;

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
        command.FindPropertyRelative("completionMode").enumValueIndex =
            (int)DirectedWavePostCommandCompletionMode.Timed;
        command.FindPropertyRelative("duration").floatValue = 1f;
        command.FindPropertyRelative("holdDuration").floatValue = 0f;
        command.FindPropertyRelative("parallelExecutionMode").enumValueIndex =
            (int)DirectedWaveParallelExecutionMode.Blocking;
        command.FindPropertyRelative("infiniteParallel").boolValue = false;
        command.FindPropertyRelative("loopCount").intValue = 1;
        command.FindPropertyRelative("infiniteLoop").boolValue = false;
        command.FindPropertyRelative("targetOffset").vector3Value = Vector3.zero;
        command.FindPropertyRelative("targetOffsetCoordinateSpace").enumValueIndex =
            (int)DirectedWaveCoordinateSpace.LocalToSubWave;
        command.FindPropertyRelative("rotationDegrees").floatValue = 45f;
        command.FindPropertyRelative("continuousFormationRotation").boolValue = false;
        command.FindPropertyRelative("curve").animationCurveValue =
            AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        command.FindPropertyRelative("formationReorderMode").enumValueIndex =
            (int)DirectedWaveFormationReorderMode.Mirror;
        command.FindPropertyRelative("formationReorderUseTargetCenter").boolValue =
            false;
        command.FindPropertyRelative("formationReorderTargetCenter").vector3Value =
            Vector3.zero;
        command.FindPropertyRelative("formationReorderSpeed").floatValue = 5f;
        command.FindPropertyRelative("formationReorderStartInterval").floatValue =
            0.1f;
        command.FindPropertyRelative("formationReorderShipsPerBatch").intValue =
            1;
        command.FindPropertyRelative("formationReorderRandomSeed").intValue =
            12345;
        ResetMorphTargetDefaults(command.FindPropertyRelative("morphTarget"));
        command.FindPropertyRelative("parallelCommands").arraySize = 0;
        command.FindPropertyRelative("loopCommands").arraySize = 0;
        command.isExpanded = true;
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

    private void DrawContinuousCommandFields(
        SerializedProperty duration,
        SerializedProperty holdDuration,
        SerializedProperty curve,
        SerializedProperty completionMode,
        string naturalCompletionDescription)
    {
        EditorGUILayout.PropertyField(
            completionMode,
            new GUIContent(
                "Completion",
                "Timed uses Duration. Complete Route derives its duration from movement settings. Infinite keeps the command active forever."));

        DirectedWavePostCommandCompletionMode mode =
            (DirectedWavePostCommandCompletionMode)completionMode.enumValueIndex;
        if (mode == DirectedWavePostCommandCompletionMode.Timed)
        {
            EditorGUILayout.PropertyField(duration);
        }
        else if (mode == DirectedWavePostCommandCompletionMode.CompleteRoute)
        {
            EditorGUILayout.HelpBox(
                $"Duration is calculated automatically from {naturalCompletionDescription}.",
                MessageType.None);
        }
        else
        {
            EditorGUILayout.HelpBox(
                "The command remains active indefinitely. Following sequential commands will not start.",
                MessageType.None);
        }

        using (new EditorGUI.DisabledScope(
                   mode == DirectedWavePostCommandCompletionMode.Infinite))
        {
            EditorGUILayout.PropertyField(holdDuration);
        }
        EditorGUILayout.PropertyField(curve);
    }

    private void DrawLocalMovementSettings()
    {
        EditorGUILayout.Space(3f);
        EditorGUILayout.LabelField("Movement Settings", EditorStyles.miniBoldLabel);
        EditorGUILayout.PropertyField(localMovementOffset);
        EditorGUILayout.PropertyField(localMovementDuration);
        EditorGUILayout.PropertyField(localMovementLoop);
        EditorGUILayout.PropertyField(localMovementPingPong);
        EditorGUILayout.PropertyField(localMovementCurve);
    }

    private void DrawWobbleSettings()
    {
        EditorGUILayout.Space(3f);
        EditorGUILayout.LabelField("Wobble Settings", EditorStyles.miniBoldLabel);
        EditorGUILayout.PropertyField(
            wobbleAmplitude,
            new GUIContent(
                "Amplitude X/Y",
                "How far enemies wobble from their formation position on X and Y."));
        EditorGUILayout.PropertyField(
            wobbleFrequency,
            new GUIContent("Frequency", "Wobble speed."));
        if (wobbleFrequency.floatValue <= 0.0001f)
        {
            EditorGUILayout.HelpBox(
                "Complete Route requires Frequency greater than zero. Timed Duration is used as a fallback while Frequency is zero.",
                MessageType.Warning);
        }
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

    private void DrawCircularMovementSettings()
    {
        EditorGUILayout.Space(3f);
        EditorGUILayout.LabelField("Circular Movement Settings", EditorStyles.miniBoldLabel);
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
        if (Mathf.Abs(selfRotationDegreesPerSecond.floatValue) <= 0.0001f)
        {
            EditorGUILayout.HelpBox(
                "Complete Route requires a non-zero angular speed. Timed Duration is used as a fallback while the speed is zero.",
                MessageType.Warning);
        }
    }

    private void DrawFormationRotationSettings()
    {
        EditorGUILayout.Space(3f);
        EditorGUILayout.LabelField("Formation Rotation Settings", EditorStyles.miniBoldLabel);
        EditorGUILayout.HelpBox(
            "Rotates the whole formation around its own center like a wheel. Enemy objects keep their own rotation.",
            MessageType.None);
        EditorGUILayout.PropertyField(
            formationRotationDegreesPerSecond,
            new GUIContent(
                "Default Degrees Per Second",
                "Fallback angular speed when the command-specific rotation value is zero."));
    }

    private void DrawFormationMorphSettings()
    {
        EditorGUILayout.Space(3f);
        EditorGUILayout.LabelField("Formation Morph Settings", EditorStyles.miniBoldLabel);
        EditorGUILayout.HelpBox(
            "Changes the formation shape over time. Points are matched greedily by nearest target position.",
            MessageType.None);
        EditorGUILayout.PropertyField(formationMorphLoop);
        EditorGUILayout.PropertyField(formationMorphReturnDuration);
        EditorGUILayout.PropertyField(formationMorphReturnCurve);
        EditorGUILayout.PropertyField(formationMorphSteps, true);
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

    private void DrawAddPostCommandButton(
        DirectedWavePostCommandType type,
        string label)
    {
        if (GUILayout.Button(label))
            AddPostCommand(type);
    }

    private void AddPostCommand(DirectedWavePostCommandType type)
    {
        int index = postCommands.arraySize;
        postCommands.arraySize++;

        SerializedProperty command = postCommands.GetArrayElementAtIndex(index);
        command.FindPropertyRelative("type").enumValueIndex = (int)type;
        command.FindPropertyRelative("enabled").boolValue = true;
        command.FindPropertyRelative("completionMode").enumValueIndex =
            (int)DirectedWavePostCommandCompletionMode.Timed;
        command.FindPropertyRelative("duration").floatValue = 1f;
        command.FindPropertyRelative("holdDuration").floatValue = 0f;
        command.FindPropertyRelative("parallelExecutionMode").enumValueIndex =
            (int)DirectedWaveParallelExecutionMode.Blocking;
        command.FindPropertyRelative("infiniteParallel").boolValue = false;
        command.FindPropertyRelative("loopCount").intValue = 1;
        command.FindPropertyRelative("infiniteLoop").boolValue = false;
        command.FindPropertyRelative("targetOffset").vector3Value = Vector3.zero;
        command.FindPropertyRelative("targetOffsetCoordinateSpace").enumValueIndex =
            (int)DirectedWaveCoordinateSpace.LocalToSubWave;
        command.FindPropertyRelative("rotationDegrees").floatValue = 45f;
        command.FindPropertyRelative("continuousFormationRotation").boolValue = false;
        command.FindPropertyRelative("curve").animationCurveValue =
            AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        ResetMorphTargetDefaults(command.FindPropertyRelative("morphTarget"));
        command.FindPropertyRelative("parallelCommands").arraySize = 0;
        command.FindPropertyRelative("loopCommands").arraySize = 0;
        command.isExpanded = true;
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

    private void DrawPatrol(int commandIndex)
    {
        EditorGUILayout.LabelField("Patrol", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Patrol points are target positions for the formation center in the selected coordinate space. "
            + "The Scene preview includes all preceding Post Commands.",
            MessageType.None);

        EditorGUI.BeginChangeCheck();
        EditorGUILayout.PropertyField(
            patrolLoop,
            new GUIContent(
                "Loop",
                "If enabled, the last patrol point returns to the first one."));
        EditorGUILayout.PropertyField(
            patrolCoordinateSpace,
            new GUIContent(
                "Coordinate Space",
                "World treats the value as an absolute world position. Local spaces transform it as a point, including the origin position, rotation and scale."));
        bool patrolRouteSettingsChanged = EditorGUI.EndChangeCheck();
        if (patrolRouteSettingsChanged)
        {
            RecalculateAllPatrolSegmentMetrics();
            MarkPatrolInspectorInteraction();
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            int newSize = Mathf.Max(
                0,
                EditorGUILayout.IntField("Size", patrolPoints.arraySize));

            if (newSize != patrolPoints.arraySize)
            {
                ResizePatrolPoints(newSize);
                MarkPatrolInspectorInteraction();
            }
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Add Patrol Point"))
            {
                AddPatrolPoint();
                MarkPatrolInspectorInteraction();
            }

            GUI.enabled = patrolPoints.arraySize > 0;
            if (GUILayout.Button("Remove Last"))
            {
                RemoveLastPatrolPoint();
                MarkPatrolInspectorInteraction();
            }

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
                string title = $"Patrol Point {i}  Position {offset.vector3Value}";
                point.isExpanded = EditorGUILayout.Foldout(
                    point.isExpanded,
                    title,
                    true,
                    EditorStyles.foldoutHeader);

                using (new EditorGUILayout.HorizontalScope())
                {
                    bool isActive = activePatrolPointIndex == i;
                    if (GUILayout.Button(
                            isActive ? "Editing in Scene" : "Edit in Scene"))
                    {
                        if (isActive)
                            activePatrolPointIndex = -1;
                        else
                            SetActivePatrolPoint(i, commandIndex);

                        SceneView.RepaintAll();
                    }
                }

                if (!point.isExpanded)
                    continue;

                SerializedProperty durationToNext =
                    point.FindPropertyRelative("durationToNext");
                SerializedProperty wait = point.FindPropertyRelative("wait");
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
                        "Position",
                        "Target position for the formation center in the selected coordinate space."));
                bool positionChanged = EditorGUI.EndChangeCheck();
                if (positionChanged)
                {
                    SetActivePatrolPoint(i, commandIndex);
                    QueuePatrolPointMetricRecalculation(i);
                    MarkPatrolInspectorInteraction();
                }

                EditorGUILayout.PropertyField(
                    wait,
                    new GUIContent(
                        "Wait",
                        "Delay at this patrol point before moving to the next point."));

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

                    if (durationChanged || speedChanged)
                    {
                        SyncPatrolDurationAndSpeed(
                            i,
                            durationChanged,
                            speedChanged,
                            false);
                        MarkPatrolInspectorInteraction();
                    }

                    if (pathShapeChanged)
                    {
                        QueuePatrolPointMetricRecalculation(i);
                        MarkPatrolInspectorInteraction();
                    }
                }
                else
                {
                    EditorGUILayout.HelpBox(
                        "Last patrol point has no next segment while Loop is disabled.",
                        MessageType.None);
                }


            }
        }
    }

    private void MarkPatrolInspectorInteraction()
    {
        patrolInspectorChangeThisEvent = true;
        if (GUIUtility.hotControl != 0)
            patrolInspectorInteractionPending = true;
    }

    private void QueuePatrolPointMetricRecalculation(int pointIndex)
    {
        if (GUIUtility.hotControl != 0)
        {
            pendingPatrolMetricPointIndex = pointIndex;
            return;
        }

        SyncPatrolSegmentsAffectedByPoint(pointIndex);
    }

    private void FinalizePendingPatrolInspectorMetrics()
    {
        if (!patrolInspectorInteractionPending || GUIUtility.hotControl != 0)
            return;

        if (pendingPatrolMetricPointIndex >= 0)
            SyncPatrolSegmentsAffectedByPoint(pendingPatrolMetricPointIndex);

        pendingPatrolMetricPointIndex = -1;
        patrolInspectorInteractionPending = false;
        patrolInspectorChangeThisEvent = true;
    }

    private void RecalculateAllPatrolSegmentMetrics()
    {
        for (int i = 0; i < patrolPoints.arraySize; i++)
            SyncPatrolDurationAndSpeed(i, false, false, true);
    }

    private void SyncPatrolSegmentsAffectedByPoint(int pointIndex)
    {
        if (patrolPoints == null || patrolPoints.arraySize < 2)
            return;

        int affectedCount = CollectPatrolSegmentsAffectedByPoint(pointIndex);
        for (int i = 0; i < affectedCount; i++)
        {
            SyncPatrolDurationAndSpeed(
                patrolAffectedSegmentIndices[i],
                false,
                false,
                true);
        }
    }

    private int CollectPatrolSegmentsAffectedByPoint(int pointIndex)
    {
        int affectedCount = 0;
        AddAffectedPatrolSegment(pointIndex, ref affectedCount);
        int previous = GetPreviousPatrolPointIndex(pointIndex);
        AddAffectedPatrolSegment(previous, ref affectedCount);
        AddAffectedPatrolSegment(
            GetPreviousPatrolPointIndex(previous),
            ref affectedCount);
        AddAffectedPatrolSegment(
            GetNextPatrolPointIndex(pointIndex),
            ref affectedCount);
        return affectedCount;
    }

    private void AddAffectedPatrolSegment(int index, ref int affectedCount)
    {
        if (!PatrolPointHasNextSegment(index))
            return;

        for (int i = 0; i < affectedCount; i++)
        {
            if (patrolAffectedSegmentIndices[i] == index)
                return;
        }

        patrolAffectedSegmentIndices[affectedCount++] = index;
    }

    private void SyncPatrolDurationAndSpeed(
        int index,
        bool durationChanged,
        bool speedChanged,
        bool pathShapeChanged)
    {
        if ((!durationChanged && !speedChanged && !pathShapeChanged)
            || !PatrolPointHasNextSegment(index))
        {
            return;
        }

        SerializedProperty point = patrolPoints.GetArrayElementAtIndex(index);
        SerializedProperty durationToNext =
            point.FindPropertyRelative("durationToNext");
        SerializedProperty speedToNext =
            point.FindPropertyRelative("speedToNext");
        float segmentLength = GetPatrolSegmentLength(index);

        if (speedChanged)
        {
            float speed = Mathf.Max(0.01f, speedToNext.floatValue);
            SetPatrolFloatIfChanged(speedToNext, speed);
            SetPatrolFloatIfChanged(
                durationToNext,
                Mathf.Max(0.01f, segmentLength / speed));
            return;
        }

        float duration = Mathf.Max(0.01f, durationToNext.floatValue);
        SetPatrolFloatIfChanged(durationToNext, duration);
        SetPatrolFloatIfChanged(
            speedToNext,
            Mathf.Max(0.01f, segmentLength / duration));
    }

    private static void SetPatrolFloatIfChanged(
        SerializedProperty property,
        float value)
    {
        if (Mathf.Abs(property.floatValue - value) <= PatrolMetricEpsilon)
            return;

        property.floatValue = value;
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

        RecalculateAllPatrolSegmentMetrics();
    }

    private void AddPatrolPoint()
    {
        int index = patrolPoints.arraySize;
        patrolPoints.arraySize++;
        InitializePatrolPoint(index);
        RecalculateAllPatrolSegmentMetrics();
    }

    private void RemoveLastPatrolPoint()
    {
        if (patrolPoints.arraySize <= 0)
            return;

        patrolPoints.DeleteArrayElementAtIndex(patrolPoints.arraySize - 1);
        activePatrolPointIndex = Mathf.Min(
            activePatrolPointIndex,
            patrolPoints.arraySize - 1);
        RecalculateAllPatrolSegmentMetrics();
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
        point.FindPropertyRelative("wait").floatValue = 0f;
        point.FindPropertyRelative("durationToNext").floatValue = 0.5f;
        point.FindPropertyRelative("speedToNext").floatValue = 1f;
        point.FindPropertyRelative("motionToNext").enumValueIndex =
            (int)DirectedWaveSegmentMotion.Linear;
        point.FindPropertyRelative("easeToNext").animationCurveValue =
            AnimationCurve.Linear(0f, 0f, 1f, 1f);

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
        float baseDuration = GetPreviewBaseRouteDuration();
        if (!previewDurationOverridden)
            previewDuration = Mathf.Max(0.1f, baseDuration);

        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField(
                new GUIContent(
                    "Base Route Duration",
                    "Calculated time for one complete entrance and post-command route."));
            GUILayout.Label($"{baseDuration:0.00}s", EditorStyles.label);
        }

        EditorGUI.BeginChangeCheck();
        float editedPreviewDuration = Mathf.Max(
            0.1f,
            EditorGUILayout.FloatField(
                new GUIContent(
                    "Preview Duration",
                    "Exact time after which Scene View preview stops."),
                previewDuration));
        if (EditorGUI.EndChangeCheck())
        {
            previewDuration = editedPreviewDuration;
            previewDurationOverridden = true;
            SavePreviewDuration();
        }

        if (previewDurationOverridden
            && GUILayout.Button("Use Base Route Duration"))
        {
            previewDurationOverridden = false;
            previewDuration = Mathf.Max(0.1f, baseDuration);
            EditorPrefs.DeleteKey(GetPreviewDurationPreferenceKey());
        }

        float totalDuration = GetPreviewTotalDuration();
        int previewCount = GetEditorEffectiveEnemyCount();
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

        InvalidatePreviewSession();
        FramePreviewArea();
        previewPlaying = true;
        previewStartTime = EditorApplication.timeSinceStartup;
        nextPreviewFrameTime = previewStartTime;
        previewSampleElapsed = 0f;
        EditorApplication.update -= UpdatePreview;
        EditorApplication.update += UpdatePreview;
        RepaintPreviewSceneView();
        Repaint();
    }

    private void FramePreviewArea()
    {
        SceneView sceneView = SceneView.lastActiveSceneView;
        if (sceneView == null)
            return;

        Bounds bounds = GetPreviewBounds((DirectedEnemySubWave)target);
        previewSceneView = sceneView;
        sceneView.Frame(bounds, false);
        sceneView.Repaint();
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
        cachedWavePreviewVersion = -1;
        cachedWavePreviewElapsed = float.NaN;
        cachedWavePreviewPositions?.Clear();
        EditorApplication.update -= UpdatePreview;
        RepaintPreviewSceneView();
        Repaint();
        previewSceneView = null;
    }

    private void UpdatePreview()
    {
        if (!previewPlaying)
            return;

        double now = EditorApplication.timeSinceStartup;
        float elapsed = Mathf.Max(0f, (float)(now - previewStartTime));
        if (elapsed >= GetPreviewTotalDuration())
        {
            StopPreview();
            return;
        }

        if (now < nextPreviewFrameTime)
            return;

        previewSampleElapsed = elapsed;
        nextPreviewFrameTime = now + PreviewFrameInterval;
        RepaintPreviewSceneView();
        Repaint();
    }

    private void RepaintPreviewSceneView()
    {
        SceneView activeSceneView = SceneView.lastActiveSceneView;
        if (activeSceneView != null)
            previewSceneView = activeSceneView;

        if (previewSceneView != null)
            previewSceneView.Repaint();
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
        patrolSceneCommitThisEvent = false;
        serializedObject.Update();

        DirectedEnemySubWave wave = (DirectedEnemySubWave)target;

        DrawMobileScreenBounds();
        if (enemySlotSelectionMode)
        {
            DrawEnemySlotSelectionHandles(wave);
        }
        else
        {
            DrawPathSceneHandles(wave);
            DrawFormationSceneHandles(wave);
            DrawPatrolSceneHandles(wave);
        }

        CompleteOrCancelPatrolPointDrag(wave);
        DrawActivePostCommandPreview(wave);
        DrawWavePreview(wave);

        if (patrolSceneCommitThisEvent)
        {
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
            if (PrefabUtility.IsPartOfPrefabInstance(target))
                PrefabUtility.RecordPrefabInstancePropertyModifications(target);
            Undo.FlushUndoRecordObjects();
            InvalidatePatrolDataPreview();
        }
        else
        {
            ApplyModifiedPropertiesAndInvalidatePreview();
        }
    }

    private void DrawActivePostCommandPreview(DirectedEnemySubWave wave)
    {
        if (Event.current.type != EventType.Repaint
            || wave == null
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

        DirectedWavePostCommandType type =
            (DirectedWavePostCommandType)command
                .FindPropertyRelative("type")
                .enumValueIndex;
        if (activePatrolPointIndex >= 0
            && type == DirectedWavePostCommandType.Patrol)
        {
            return;
        }

        if (!TryGetCachedCommandPreview(wave, activePostCommandIndex))
            return;

        bool enabled = command.FindPropertyRelative("enabled").boolValue;

        DrawPostCommandPreviewOverlay(
            cachedCommandPreviewBefore,
            cachedCommandPreviewAfter,
            activePostCommandIndex,
            type,
            enabled);
    }

    private bool TryGetCachedCommandPreview(
        DirectedEnemySubWave wave,
        int commandIndex)
    {
        if (wave == null || commandIndex < 0)
            return false;

        if (cachedCommandPreviewVersion != previewConfigurationVersion
            || cachedCommandPreviewIndex != commandIndex)
        {
            if (!wave.EvaluateSimulationCommandPreview(
                    commandIndex,
                    out cachedCommandPreviewBefore,
                    out cachedCommandPreviewAfter))
            {
                cachedCommandPreviewBefore = null;
                cachedCommandPreviewAfter = null;
                return false;
            }

            cachedCommandPreviewVersion = previewConfigurationVersion;
            cachedCommandPreviewIndex = commandIndex;
        }

        return cachedCommandPreviewBefore != null
            && cachedCommandPreviewAfter != null;
    }
    private void DrawPostCommandPreviewOverlay(
        Dictionary<int, Vector3> before,
        Dictionary<int, Vector3> after,
        int commandIndex,
        DirectedWavePostCommandType type,
        bool enabled)
    {
        if (Event.current.type != EventType.Repaint)
            return;

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
            previewLinePoints[0] = start;
            previewLinePoints[1] = end;
            Handles.DrawAAPolyLine(3f, previewLinePoints);

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
        if (!previewPlaying || Event.current.type != EventType.Repaint)
            return;

        float elapsed = previewSampleElapsed;
        int count = wave.GetSimulationEnemyCount();
        RefreshWavePreviewCache(wave, elapsed);
        Dictionary<int, Vector3> previewPositions = cachedWavePreviewPositions;
        if (previewPositions == null)
            return;

        Handles.zTest = UnityEngine.Rendering.CompareFunction.Always;
        Handles.color = new Color(0.35f, 1f, 0.45f, 0.9f);

        int visibleCount = 0;
        int[] previewSpawnOrder = cachedWavePreviewSpawnOrder;
        int orderedCount = previewSpawnOrder != null
            ? Mathf.Min(count, previewSpawnOrder.Length)
            : 0;
        float safeSpawnInterval = Mathf.Max(0f, spawnInterval.floatValue);
        for (int i = 0; i < orderedCount; i++)
        {
            int formationIndex = previewSpawnOrder[i];
            float enemyTime = elapsed - i * safeSpawnInterval;
            if (enemyTime < 0f
                || !previewPositions.TryGetValue(
                    formationIndex,
                    out Vector3 position))
            {
                continue;
            }

            visibleCount++;
            float radius = Mathf.Lerp(0.11f, 0.18f, Mathf.PingPong(enemyTime * 2f, 1f));

            Handles.color = new Color(0.35f, 1f, 0.45f, 0.35f);
            Vector3 previewSpawnPosition =
                wave.GetSimulationEntranceStartPosition(formationIndex);
            previewLinePoints[0] = previewSpawnPosition;
            previewLinePoints[1] = position;
            Handles.DrawAAPolyLine(
                3f,
                previewLinePoints);
            Handles.color = new Color(0.35f, 1f, 0.45f, 0.9f);
            Handles.DrawSolidDisc(position, Vector3.forward, radius * 1.6f);
            Handles.color = Color.black;
            Handles.DrawWireDisc(position, Vector3.forward, radius * 1.9f);
            Handles.color = Color.white;
            Handles.Label(
                position + Vector3.up * 0.22f,
                cachedWavePreviewLabels[i]);
            Handles.color = new Color(0.35f, 1f, 0.45f, 0.9f);
        }

        Vector3 labelPosition = wave.transform.position + Vector3.up * 4.5f;
        Handles.color = Color.white;
        Handles.Label(
            labelPosition,
            $"Wave Preview {elapsed:0.00}s / {GetPreviewTotalDuration():0.00}s\n"
            + $"Phase: {cachedWavePreviewPhaseName}\n"
            + $"Visible enemies: {visibleCount}/{count}");

        if (visibleCount == 0)
        {
            Handles.Label(
                labelPosition + Vector3.down * 0.6f,
                "No enemies visible yet. Check Spawn Interval or wait a moment.");
        }
    }

    private void RefreshWavePreviewCache(
        DirectedEnemySubWave wave,
        float elapsed)
    {
        bool configurationChanged =
            cachedWavePreviewVersion != previewConfigurationVersion;
        if (!configurationChanged && cachedWavePreviewElapsed == elapsed)
            return;

        if (configurationChanged || cachedWavePreviewSpawnOrder == null)
        {
            wave.InvalidateSimulationPreviewCache();
            cachedWavePreviewSpawnOrder = wave.GetSimulationSpawnOrder();
            cachedWavePreviewLabels = BuildWavePreviewLabels(
                cachedWavePreviewSpawnOrder);
        }

        cachedWavePreviewPositions ??= new Dictionary<int, Vector3>(
            Mathf.Max(1, wave.GetSimulationEnemyCount()));
        wave.EvaluateSimulationPreviewNonAlloc(
            elapsed,
            null,
            cachedWavePreviewSpawnOrder,
            cachedWavePreviewPositions,
            out cachedWavePreviewPhaseName);

        cachedWavePreviewVersion = previewConfigurationVersion;
        cachedWavePreviewElapsed = elapsed;
    }

    private static GUIContent[] BuildWavePreviewLabels(int[] spawnOrder)
    {
        if (spawnOrder == null)
            return System.Array.Empty<GUIContent>();

        GUIContent[] labels = new GUIContent[spawnOrder.Length];
        for (int i = 0; i < spawnOrder.Length; i++)
        {
            int formationIndex = spawnOrder[i];
            labels[i] = new GUIContent(
                formationIndex == i ? $"{i}" : $"{i}->{formationIndex}");
        }

        return labels;
    }

    private int[] BuildEditorSpawnOrder(DirectedEnemySubWave wave, int count)
    {
        count = Mathf.Max(0, count);
        Vector3[] positions = new Vector3[count];
        for (int i = 0; i < count; i++)
            positions[i] = GetFormationWorldPosition(i, wave);

        return DirectedWaveSpawnOrderResolver.Build(
            positions,
            (DirectedWaveSpawnOrderMode)spawnOrderMode.enumValueIndex,
            spawnOrderAngle.floatValue,
            spawnOrderStartAngle.floatValue);
    }

    private static string GetPostCommandTypeLabel(DirectedWavePostCommandType type)
    {
        return type switch
        {
            DirectedWavePostCommandType.Patrol => "Patrol",
            DirectedWavePostCommandType.LocalMovement => "Local Movement",
            DirectedWavePostCommandType.Wobble => "Wobble",
            DirectedWavePostCommandType.LegacyAttack => "Removed Attack",
            DirectedWavePostCommandType.CircularMovement => "Circular Movement",
            DirectedWavePostCommandType.FormationRotation => "Formation Rotation",
            DirectedWavePostCommandType.FormationMorph => "Formation Morph",
            DirectedWavePostCommandType.FormationReorder => "Formation Reorder",
            DirectedWavePostCommandType.Wait => "Wait",
            DirectedWavePostCommandType.Parallel => "Parallel",
            DirectedWavePostCommandType.Loop => "Loop",
            _ => type.ToString()
        };
    }

    private void DrawPathSceneHandles(DirectedEnemySubWave wave)
    {
        if (UsesIndividualEntrancePoints())
        {
            DrawIndividualEntrancePointSceneHandles(wave);
            return;
        }

        if (pathCheckpoints == null || pathCheckpoints.arraySize == 0)
            return;

        DrawCheckpointSceneHandles(wave);
    }

    private void DrawIndividualEntrancePointSceneHandles(
        DirectedEnemySubWave wave)
    {
        if (individualEntrancePoints == null
            || individualEntrancePoints.arraySize == 0)
        {
            activeIndividualEntrancePointIndex = -1;
            return;
        }

        int count = Mathf.Min(
            individualEntrancePoints.arraySize,
            Mathf.Max(0, GetEditorEffectiveEnemyCount()));
        Handles.color = new Color(1f, 0.65f, 0.2f, 1f);

        for (int i = 0; i < count; i++)
        {
            SerializedProperty point =
                individualEntrancePoints.GetArrayElementAtIndex(i);
            SerializedProperty position =
                point.FindPropertyRelative("position");
            DirectedWaveCoordinateSpace coordinateSpace =
                (DirectedWaveCoordinateSpace)pathCoordinateSpace.enumValueIndex;
            Vector3 world = ToWorld(
                wave,
                position.vector3Value,
                coordinateSpace);
            Vector3 formationPosition = GetFormationWorldPosition(i, wave);

            EditorGUI.BeginChangeCheck();
            Vector3 changedWorld = Handles.PositionHandle(
                world,
                Quaternion.identity);
            bool positionChanged = EditorGUI.EndChangeCheck();

            Handles.color = new Color(1f, 0.65f, 0.2f, 0.55f);
            Handles.DrawDottedLine(changedWorld, formationPosition, 4f);
            Handles.color = new Color(1f, 0.65f, 0.2f, 1f);
            Handles.Label(
                changedWorld + Vector3.up * 0.15f,
                $"Entry {i} -> Slot {i}");

            if (activeIndividualEntrancePointIndex == i)
            {
                DrawScenePointHighlight(
                    changedWorld,
                    $"Entry {i}",
                    new Color(1f, 0.65f, 0.2f, 1f));
            }

            if (!positionChanged)
                continue;

            Undo.RecordObject(target, "Move Directed Wave Entry Point");
            position.vector3Value = FromWorld(
                wave,
                changedWorld,
                coordinateSpace);
            activeIndividualEntrancePointIndex = i;
            SceneView.RepaintAll();
        }
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

        if (UsesEntrancePathLoop() && pathCheckpoints.arraySize >= 2)
        {
            int lastIndex = pathCheckpoints.arraySize - 1;
            int loopStartIndex = Mathf.Clamp(
                entranceLoopStartCheckpointIndex.intValue,
                0,
                lastIndex - 1);
            Vector3 last = ToWorld(
                wave,
                pathCheckpoints.GetArrayElementAtIndex(lastIndex)
                    .FindPropertyRelative("position").vector3Value,
                (DirectedWaveCoordinateSpace)pathCoordinateSpace.enumValueIndex);
            Vector3 loopStart = ToWorld(
                wave,
                pathCheckpoints.GetArrayElementAtIndex(loopStartIndex)
                    .FindPropertyRelative("position").vector3Value,
                (DirectedWaveCoordinateSpace)pathCoordinateSpace.enumValueIndex);

            Handles.color = Color.magenta;
            Handles.DrawDottedLine(last, loopStart, 4f);
            Handles.Label(
                loopStart + Vector3.down * 0.15f,
                $"Loop starts here ({loopStartIndex})");
        }
    }

    private void DrawPatrolSceneHandles(DirectedEnemySubWave wave)
    {
        if (patrolPoints == null || patrolPoints.arraySize == 0)
        {
            activePatrolPointIndex = -1;
            return;
        }

        if (activePatrolPointIndex >= patrolPoints.arraySize)
            activePatrolPointIndex = patrolPoints.arraySize - 1;

        DirectedWaveCoordinateSpace coordinateSpace =
            (DirectedWaveCoordinateSpace)formationCoordinateSpace.enumValueIndex;
        DirectedWaveCoordinateSpace patrolPointCoordinateSpace =
            (DirectedWaveCoordinateSpace)patrolCoordinateSpace.enumValueIndex;
        Vector3 centerWorld = GetPatrolSceneCenter(wave, coordinateSpace);
        Dictionary<int, Vector3> patrolBasePositions = null;
        if (TryGetPatrolPreviewBasePositions(wave, out Dictionary<int, Vector3> previewBasePositions))
        {
            patrolBasePositions = previewBasePositions;
            centerWorld = GetPreviewPositionsCenter(
                patrolBasePositions,
                centerWorld);
        }

        DrawPatrolRoute(centerWorld);

        Handles.color = new Color(0.35f, 1f, 0.9f, 1f);
        for (int i = 0; i < patrolPoints.arraySize; i++)
        {
            SerializedProperty point = patrolPoints.GetArrayElementAtIndex(i);
            SerializedProperty offset = point.FindPropertyRelative("offset");
            Vector3 world = IsDraggingPatrolPoint(i)
                ? patrolPointDragWorldPosition
                : ToWorld(
                    wave,
                    offset.vector3Value,
                    patrolPointCoordinateSpace);

            if (activePatrolPointIndex != i)
            {
                Handles.color = new Color(0.35f, 1f, 0.9f, 0.95f);
                if (Handles.Button(
                        world,
                        Quaternion.identity,
                        0.11f,
                        0.15f,
                        Handles.DotHandleCap))
                {
                    SetActivePatrolPoint(
                        i,
                        GetPatrolPreviewCommandIndex());
                }

                Handles.Label(
                    world + Vector3.up * 0.15f,
                    $"Patrol {i}");
                continue;
            }

            EditorGUI.BeginChangeCheck();
            Vector3 changedWorld = Handles.PositionHandle(
                world,
                Quaternion.identity);

            Handles.Label(
                changedWorld + Vector3.up * 0.15f,
                $"Patrol {i}");

            DrawScenePointHighlight(
                changedWorld,
                $"Patrol {i}",
                new Color(0.35f, 1f, 0.9f, 1f));

            bool positionChanged = EditorGUI.EndChangeCheck();
            Vector3 previewOffset = changedWorld - centerWorld;
            if (positionChanged)
                UpdatePatrolPointDrag(i, changedWorld);

            DrawPatrolFormationPreview(
                wave,
                patrolBasePositions,
                previewOffset);
        }
    }

    private bool IsDraggingPatrolPoint(int pointIndex)
    {
        return patrolPointDragActive && patrolPointDragIndex == pointIndex;
    }

    private void UpdatePatrolPointDrag(int pointIndex, Vector3 worldPosition)
    {
        if (!IsDraggingPatrolPoint(pointIndex))
        {
            patrolPointDragActive = true;
            patrolPointDragIndex = pointIndex;
        }

        if ((patrolPointDragWorldPosition - worldPosition).sqrMagnitude
            <= PatrolMetricEpsilon * PatrolMetricEpsilon)
        {
            return;
        }

        patrolPointDragWorldPosition = worldPosition;
        activePatrolPointIndex = pointIndex;
        InvalidatePatrolGeometryPreview(pointIndex);
    }

    private void CompleteOrCancelPatrolPointDrag(DirectedEnemySubWave wave)
    {
        if (!patrolPointDragActive)
            return;

        Event current = Event.current;
        if (current.rawType == EventType.KeyDown
            && current.keyCode == KeyCode.Escape)
        {
            CancelPatrolPointDrag();
            return;
        }

        bool mouseReleased = current.rawType == EventType.MouseUp;
        bool lostHotControl = GUIUtility.hotControl == 0
            && current.type == EventType.Layout;
        if (mouseReleased || lostHotControl)
            CommitPatrolPointDrag(wave);
    }

    private void CommitPatrolPointDrag(DirectedEnemySubWave wave)
    {
        int pointIndex = patrolPointDragIndex;
        if (wave == null
            || patrolPoints == null
            || pointIndex < 0
            || pointIndex >= patrolPoints.arraySize)
        {
            CancelPatrolPointDrag();
            return;
        }

        Undo.RecordObject(target, "Move Patrol Point");
        SerializedProperty point = patrolPoints.GetArrayElementAtIndex(pointIndex);
        point.FindPropertyRelative("offset").vector3Value = FromWorld(
            wave,
            patrolPointDragWorldPosition,
            (DirectedWaveCoordinateSpace)patrolCoordinateSpace.enumValueIndex);
        SyncPatrolSegmentsAffectedByPoint(pointIndex);

        patrolPointDragActive = false;
        patrolPointDragIndex = -1;
        patrolSceneCommitThisEvent = true;
    }

    private void CancelPatrolPointDrag()
    {
        if (!patrolPointDragActive)
            return;

        int cancelledPointIndex = patrolPointDragIndex;
        patrolPointDragActive = false;
        patrolPointDragIndex = -1;
        InvalidatePatrolGeometryPreview(cancelledPointIndex);
    }

    private void DrawPatrolFormationPreview(
        DirectedEnemySubWave wave,
        Dictionary<int, Vector3> basePositions,
        Vector3 patrolOffset)
    {
        if (Event.current.type != EventType.Repaint)
            return;

        Handles.color = new Color(0.2f, 1f, 0.55f, 0.95f);
        if (basePositions != null && basePositions.Count > 0)
        {
            foreach (KeyValuePair<int, Vector3> pair in basePositions)
                DrawPatrolFormationEndpoint(pair.Value + patrolOffset);

            return;
        }

        int count = GetEditorEffectiveEnemyCount();
        if (count <= 0)
            return;

        Transform frozenRoot = formationFrozen.boolValue
            ? formationPointsRoot.objectReferenceValue as Transform
            : null;
        for (int i = 0; i < count; i++)
        {
            Vector3 basePosition = frozenRoot != null && i < frozenRoot.childCount
                ? frozenRoot.GetChild(i).position
                : GetFormationWorldPosition(i, wave);
            DrawPatrolFormationEndpoint(basePosition + patrolOffset);
        }
    }

    private static void DrawPatrolFormationEndpoint(Vector3 endpoint)
    {
        Handles.DrawSolidDisc(endpoint, Vector3.forward, 0.07f);
        Handles.DrawWireDisc(endpoint, Vector3.forward, 0.14f);
    }

    private bool TryGetPatrolPreviewBasePositions(
        DirectedEnemySubWave wave,
        out Dictionary<int, Vector3> basePositions)
    {
        basePositions = null;
        int commandIndex = GetPatrolPreviewCommandIndex();
        if (commandIndex < 0)
            return false;

        if (cachedPatrolPreviewCommandIndex == commandIndex
            && cachedPatrolPreviewBasePositions != null
            && cachedPatrolPreviewBasePositions.Count > 0)
        {
            basePositions = cachedPatrolPreviewBasePositions;
            return true;
        }

        if (!TryGetCachedCommandPreview(wave, commandIndex))
            return false;

        cachedPatrolPreviewCommandIndex = commandIndex;
        cachedPatrolPreviewBasePositions = cachedCommandPreviewBefore;
        basePositions = cachedPatrolPreviewBasePositions;
        return basePositions != null && basePositions.Count > 0;
    }

    private static Vector3 GetPreviewPositionsCenter(
        Dictionary<int, Vector3> positions,
        Vector3 fallback)
    {
        if (positions == null || positions.Count == 0)
            return fallback;

        Vector3 center = Vector3.zero;
        foreach (KeyValuePair<int, Vector3> pair in positions)
            center += pair.Value;

        return center / positions.Count;
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
        if (Event.current.type != EventType.Repaint
            || patrolPoints == null
            || patrolPoints.arraySize < 2)
        {
            return;
        }

        RefreshPatrolRouteCache(centerWorld);
        if (cachedPatrolRoutePoints.Length < 2)
            return;

        Color routeColor = new Color(0.35f, 1f, 0.9f, 0.85f);
        Color inactiveRouteColor = new Color(0.35f, 1f, 0.9f, 0.35f);
        Handles.color = cachedPatrolCommandActive
            ? routeColor
            : inactiveRouteColor;
        Handles.DrawAAPolyLine(2.5f, cachedPatrolRoutePoints);

        if (!cachedPatrolCommandActive)
        {
            Handles.color = Color.white;
            Handles.Label(
                centerWorld + Vector3.up * 0.6f,
                "Patrol points exist, but Post Behavior is not Patrol/WobbleAndPatrol.");
        }
    }

    private void RefreshPatrolRouteCache(Vector3 centerWorld)
    {
        DirectedEnemySubWave wave = target as DirectedEnemySubWave;
        Matrix4x4 offsetMatrix = GetPatrolOffsetMatrix(wave);
        bool offsetTransformChanged = !hasCachedPatrolRouteOffsetMatrix
            || !cachedPatrolRouteOffsetMatrix.Equals(offsetMatrix);
        if (cachedPatrolRouteVersion == patrolGeometryVersion
            && !offsetTransformChanged)
        {
            cachedPatrolRouteCenter = centerWorld;
            return;
        }

        int segmentCount = 0;
        for (int segment = 0; segment < patrolPoints.arraySize; segment++)
        {
            if (PatrolPointHasNextSegment(segment))
                segmentCount++;
        }

        int pointCount = segmentCount > 0
            ? segmentCount * PatrolRouteSamplesPerSegment + 1
            : 0;
        bool structureChanged = cachedPatrolRoutePoints.Length != pointCount;
        if (structureChanged)
            cachedPatrolRoutePoints = new Vector3[pointCount];

        bool rebuildEntireRoute = patrolRouteCacheRequiresFullRebuild
            || structureChanged
            || offsetTransformChanged
            || patrolRouteDirtyPointIndex < 0;
        if (rebuildEntireRoute)
        {
            RebuildEntirePatrolRouteCache();
        }
        else
        {
            RefreshPatrolRouteSegmentsAffectedByPoint(
                patrolRouteDirtyPointIndex);
        }

        cachedPatrolCommandActive = wave != null && wave.SimulationUsesCommand(
            DirectedWavePostCommandType.Patrol);
        cachedPatrolRouteCenter = centerWorld;
        cachedPatrolRouteOffsetMatrix = offsetMatrix;
        hasCachedPatrolRouteOffsetMatrix = true;
        patrolRouteCacheRequiresFullRebuild = false;
        patrolRouteDirtyPointIndex = -1;
        cachedPatrolRouteVersion = patrolGeometryVersion;
    }

    private void RebuildEntirePatrolRouteCache()
    {
        int pointIndex = 0;
        for (int segment = 0; segment < patrolPoints.arraySize; segment++)
        {
            if (!PatrolPointHasNextSegment(segment))
                continue;

            if (pointIndex == 0)
            {
                cachedPatrolRoutePoints[pointIndex++] =
                    EvaluatePatrolSegment(segment, 0f);
            }

            for (int sample = 1;
                sample <= PatrolRouteSamplesPerSegment;
                sample++)
            {
                cachedPatrolRoutePoints[pointIndex++] =
                    EvaluatePatrolSegment(
                        segment,
                        sample / (float)PatrolRouteSamplesPerSegment);
            }
        }
    }

    private void RefreshPatrolRouteSegmentsAffectedByPoint(int pointIndex)
    {
        int affectedCount = CollectPatrolSegmentsAffectedByPoint(pointIndex);
        for (int i = 0; i < affectedCount; i++)
            RefreshPatrolRouteSegment(patrolAffectedSegmentIndices[i]);
    }

    private void RefreshPatrolRouteSegment(int segmentIndex)
    {
        int firstPointIndex = segmentIndex * PatrolRouteSamplesPerSegment;
        for (int sample = 0;
            sample <= PatrolRouteSamplesPerSegment;
            sample++)
        {
            int routePointIndex = firstPointIndex + sample;
            if (routePointIndex < 0
                || routePointIndex >= cachedPatrolRoutePoints.Length)
            {
                continue;
            }

            cachedPatrolRoutePoints[routePointIndex] = EvaluatePatrolSegment(
                segmentIndex,
                sample / (float)PatrolRouteSamplesPerSegment);
        }
    }

    private void DrawMobileScreenBounds()
    {
        if (!showMobileBounds || Event.current.type != EventType.Repaint)
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
        mobileBoundsLinePoints[0] = leftTop;
        mobileBoundsLinePoints[1] = rightTop;
        mobileBoundsLinePoints[2] = rightBottom;
        mobileBoundsLinePoints[3] = leftBottom;
        mobileBoundsLinePoints[4] = leftTop;
        Handles.DrawAAPolyLine(3f, mobileBoundsLinePoints);

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

    private void DrawEnemySlotSelectionHandles(DirectedEnemySubWave wave)
    {
        ClampSelectedEnemySlot();

        int slotCount = GetEditorEnemySlotCount();
        if (slotCount <= 0)
            return;

        Color previousColor = Handles.color;

        for (int i = 0; i < slotCount; i++)
        {
            if (!TryGetEditorEnemySlotWorldPosition(
                    wave,
                    i,
                    out Vector3 worldPosition))
            {
                continue;
            }

            Enemy enemyOverride = GetEditorEnemySlotOverride(i);
            bool selected = selectedEnemySlotIndex == i;
            Color slotColor = selected
                ? new Color(1f, 0.68f, 0.1f, 1f)
                : enemyOverride != null
                    ? new Color(0.35f, 1f, 0.55f, 1f)
                    : new Color(0.25f, 0.75f, 1f, 1f);
            float handleSize = Mathf.Max(
                0.08f,
                HandleUtility.GetHandleSize(worldPosition) * 0.1f);

            if (Event.current.type == EventType.Repaint)
            {
                Handles.color = new Color(
                    slotColor.r,
                    slotColor.g,
                    slotColor.b,
                    0.2f);
                Handles.DrawSolidDisc(
                    worldPosition,
                    Vector3.forward,
                    handleSize * 1.35f);
            }

            Handles.color = slotColor;
            if (Handles.Button(
                    worldPosition,
                    Quaternion.identity,
                    handleSize,
                    handleSize,
                    Handles.CircleHandleCap))
            {
                selectedEnemySlotIndex = i;
                Repaint();
                SceneView.RepaintAll();
            }

            if (Event.current.type == EventType.Repaint)
            {
                Handles.color = Color.white;
                Handles.Label(
                    worldPosition + Vector3.up * handleSize * 1.7f,
                    $"Slot {i}\n{GetEditorEnemySlotPrefabLabel(enemyOverride)}");
            }
        }

        Handles.color = previousColor;
    }

    private bool TryGetEditorEnemySlotWorldPosition(
        DirectedEnemySubWave wave,
        int index,
        out Vector3 worldPosition)
    {
        worldPosition = Vector3.zero;
        if (wave == null || index < 0)
            return false;

        if (UsesTransformEnemySlots())
        {
            Transform root = formationPointsRoot.objectReferenceValue as Transform;
            if (root == null || index >= root.childCount)
                return false;

            Transform point = root.GetChild(index);
            if (point == null)
                return false;

            worldPosition = point.position;
            return true;
        }

        DirectedWaveCoordinateSpace coordinateSpace =
            (DirectedWaveCoordinateSpace)formationCoordinateSpace.enumValueIndex;
        worldPosition = ToWorld(
            wave,
            GetFormationLocalPosition(index, wave),
            coordinateSpace);
        return true;
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

        if (activePatrolPointIndex >= 0)
        {
            Handles.DrawWireDisc(
                worldCenter,
                Vector3.forward,
                0.18f);
            Handles.Label(
                worldCenter + Vector3.up * 0.2f,
                "Formation Center (Patrol edit locked)");
        }
        else
        {
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
        if (Event.current.type != EventType.Repaint)
            return;

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
        if (Event.current.type != EventType.Repaint)
            return;

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
        return Mathf.Max(0.1f, previewDuration);
    }

    private float GetPreviewBaseRouteDuration()
    {
        DirectedEnemySubWave wave = target as DirectedEnemySubWave;
        return wave != null
            ? wave.GetSimulationBaseRouteDuration()
            : 0f;
    }

    private void LoadPreviewDuration()
    {
        string key = GetPreviewDurationPreferenceKey();
        previewDurationOverridden = EditorPrefs.HasKey(key);
        previewDuration = previewDurationOverridden
            ? Mathf.Max(0.1f, EditorPrefs.GetFloat(key, 0.1f))
            : Mathf.Max(0.1f, GetPreviewBaseRouteDuration());
    }

    private void SavePreviewDuration()
    {
        EditorPrefs.SetFloat(
            GetPreviewDurationPreferenceKey(),
            Mathf.Max(0.1f, previewDuration));
    }

    private string GetPreviewDurationPreferenceKey()
    {
        GlobalObjectId objectId = GlobalObjectId.GetGlobalObjectIdSlow(target);
        return PreviewDurationKeyPrefix + objectId;
    }

    private float GetPreviewPostBehaviorStartTime()
    {
        DirectedEnemySubWave wave = target as DirectedEnemySubWave;
        return wave != null
            ? wave.GetSimulationPreviewPostStartTime()
            : 0f;
    }

    private float GetPreviewPostBehaviorDuration()
    {
        DirectedEnemySubWave wave = target as DirectedEnemySubWave;
        if (wave == null)
            return 0f;

        int count = wave.GetSimulationEnemyCount();
        if (count <= 0)
            return 0f;

        float entranceDuration = (count - 1) * Mathf.Max(0f, spawnInterval.floatValue)
            + GetCheckpointPathDuration()
            + Mathf.Max(0f, settleDuration.floatValue);
        return Mathf.Max(
            0f,
            wave.GetSimulationPreviewTotalDuration(
                InfiniteParallelPreviewExtraDuration)
            - entranceDuration
            - 0.25f);
    }

    private int GetEditorEffectiveEnemyCount()
    {
        if (UsesTransformEnemySlots())
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
        if (UsesTransformEnemySlots())
            return HasEditorTransformFormationEnemyOverride();

        if (IsCustomPointsFormation())
            return HasEditorCustomFormationEnemyOverride();

        return HasEditorProceduralFormationEnemyOverride();
    }

    private bool HasEditorTransformFormationEnemyOverride()
    {
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

    private bool HasEditorProceduralFormationEnemyOverride()
    {
        if (proceduralFormationEnemyOverrides == null)
            return false;

        for (int i = 0; i < proceduralFormationEnemyOverrides.arraySize; i++)
        {
            if (proceduralFormationEnemyOverrides
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
        for (int i = 0; i < patrolPoints.arraySize; i++)
        {
            duration += Mathf.Max(
                0f,
                patrolPoints
                    .GetArrayElementAtIndex(i)
                    .FindPropertyRelative("wait")
                    .floatValue);
            if (i >= patrolPoints.arraySize - 1)
                continue;

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
            return GetPatrolPointPosition(0);

        float remaining = Mathf.Max(0f, postBehaviorTime);
        float totalDuration = GetPreviewPatrolPathDuration();
        if (patrolLoop.boolValue && totalDuration > 0f)
            remaining = Mathf.Repeat(remaining, totalDuration);
        else if (!patrolLoop.boolValue && remaining >= totalDuration)
            return GetPatrolPointPosition(patrolPoints.arraySize - 1);

        int lastSegment = patrolLoop.boolValue
            ? patrolPoints.arraySize - 1
            : patrolPoints.arraySize - 2;

        for (int i = 0; i < patrolPoints.arraySize; i++)
        {
            SerializedProperty point = patrolPoints.GetArrayElementAtIndex(i);
            float wait = Mathf.Max(
                0f,
                point.FindPropertyRelative("wait").floatValue);
            if (remaining <= wait)
                return GetPatrolPointPosition(i);

            remaining -= wait;
            if (i > lastSegment)
                break;

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
            ? GetPatrolPointPosition(0)
            : GetPatrolPointPosition(patrolPoints.arraySize - 1);
    }

    private float GetPreviewPatrolPathDuration()
    {
        if (patrolPoints == null || patrolPoints.arraySize < 2)
            return 0f;

        int lastSegment = patrolLoop.boolValue
            ? patrolPoints.arraySize - 1
            : patrolPoints.arraySize - 2;
        float duration = 0f;

        for (int i = 0; i < patrolPoints.arraySize; i++)
        {
            duration += Mathf.Max(
                0f,
                patrolPoints
                    .GetArrayElementAtIndex(i)
                    .FindPropertyRelative("wait")
                    .floatValue);
            if (i > lastSegment)
                continue;

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
                GetPatrolPointPosition(segmentIndex),
                GetPatrolPointPosition(GetNextPatrolPointIndex(segmentIndex)),
                time)
        };
    }

    private Vector3 EvaluatePatrolBezierSegment(int segmentIndex, float time)
    {
        Vector3 p0 = GetPatrolPointPosition(segmentIndex);
        Vector3 p3 = GetPatrolPointPosition(GetNextPatrolPointIndex(segmentIndex));
        Vector3 previous = GetPatrolPointPosition(GetPreviousPatrolPointIndex(segmentIndex));
        Vector3 following = GetPatrolPointPosition(
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
            2f * GetPatrolPointPosition(p1)
            + (-GetPatrolPointPosition(p0) + GetPatrolPointPosition(p2)) * t
            + (2f * GetPatrolPointPosition(p0) - 5f * GetPatrolPointPosition(p1)
                + 4f * GetPatrolPointPosition(p2) - GetPatrolPointPosition(p3))
            * t * t
            + (-GetPatrolPointPosition(p0) + 3f * GetPatrolPointPosition(p1)
                - 3f * GetPatrolPointPosition(p2) + GetPatrolPointPosition(p3))
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

    private Vector3 GetPatrolPointPosition(int index)
    {
        if (patrolPoints == null || patrolPoints.arraySize == 0)
            return Vector3.zero;

        int safeIndex = Mathf.Clamp(index, 0, patrolPoints.arraySize - 1);
        if (IsDraggingPatrolPoint(safeIndex))
            return patrolPointDragWorldPosition;

        Vector3 offset = patrolPoints
            .GetArrayElementAtIndex(safeIndex)
            .FindPropertyRelative("offset")
            .vector3Value;
        DirectedEnemySubWave wave = target as DirectedEnemySubWave;
        if (wave == null || patrolCoordinateSpace == null)
            return offset;

        return ToWorld(
            wave,
            offset,
            (DirectedWaveCoordinateSpace)patrolCoordinateSpace.enumValueIndex);
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


    private Matrix4x4 GetPatrolOffsetMatrix(DirectedEnemySubWave wave)
    {
        if (wave == null || patrolCoordinateSpace == null)
            return Matrix4x4.identity;

        DirectedWaveCoordinateSpace coordinateSpace =
            (DirectedWaveCoordinateSpace)patrolCoordinateSpace.enumValueIndex;
        if (coordinateSpace == DirectedWaveCoordinateSpace.LocalToSubWave)
            return wave.transform.localToWorldMatrix;

        if (coordinateSpace == DirectedWaveCoordinateSpace.LocalToSpawnPoint)
        {
            Transform spawn = GetSpawnPoint(wave);
            return spawn != null
                ? spawn.localToWorldMatrix
                : Matrix4x4.identity;
        }

        return Matrix4x4.identity;
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

    private enum IndividualEntranceShapePreset
    {
        Circle,
        Triangle,
        Rectangle,
        Diamond
    }
}
