using UnityEditor;
using UnityEngine;

public sealed partial class DirectedEnemySubWaveEditor
{
    private static DirectedWaveCurveSettings ReadCurveSettings(SerializedProperty point)
    {
        var p = point.FindPropertyRelative("curveSettings");
        return new DirectedWaveCurveSettings
        {
            handleMode = (DirectedWaveBezierHandleMode)p.FindPropertyRelative("handleMode").enumValueIndex,
            inHandle = p.FindPropertyRelative("inHandle").vector3Value,
            outHandle = p.FindPropertyRelative("outHandle").vector3Value,
            handlesInitialized = p.FindPropertyRelative("handlesInitialized").boolValue,
            tension = p.FindPropertyRelative("tension").floatValue,
            parameterization = (DirectedWaveCurveParameterization)p.FindPropertyRelative("parameterization").enumValueIndex,
            bulge = p.FindPropertyRelative("bulge").floatValue,
            longArc = p.FindPropertyRelative("longArc").boolValue
        };
    }

    private static void WriteCurveSettings(SerializedProperty point, DirectedWaveCurveSettings value)
    {
        var p = point.FindPropertyRelative("curveSettings");
        p.FindPropertyRelative("handleMode").enumValueIndex = (int)value.handleMode;
        p.FindPropertyRelative("inHandle").vector3Value = value.inHandle;
        p.FindPropertyRelative("outHandle").vector3Value = value.outHandle;
        p.FindPropertyRelative("handlesInitialized").boolValue = value.handlesInitialized;
        p.FindPropertyRelative("tension").floatValue = value.tension;
        p.FindPropertyRelative("parameterization").enumValueIndex = (int)value.parameterization;
        p.FindPropertyRelative("bulge").floatValue = value.bulge;
        p.FindPropertyRelative("longArc").boolValue = value.longArc;
    }

    private static DirectedWaveCurveSettings ReadWorldCurveSettings(SerializedProperty point,
        DirectedEnemySubWave wave, DirectedWaveCoordinateSpace space)
    {
        var value = ReadCurveSettings(point);
        if (wave == null) return value;
        Transform spawn = GetSpawnPoint(wave);
        return space switch
        {
            DirectedWaveCoordinateSpace.LocalToSpawnPoint when spawn != null => value.TransformHandles(spawn.localToWorldMatrix),
            DirectedWaveCoordinateSpace.LocalToSubWave => value.TransformHandles(wave.transform.localToWorldMatrix),
            _ => value
        };
    }

    private int CurveNextIndex(SerializedProperty points, int index, bool patrol)
    {
        if (index + 1 < points.arraySize) return index + 1;
        if (patrol) return patrolLoop.boolValue ? 0 : index;
        return UsesEntrancePathLoop() && !entranceLoopTeleportToStart.boolValue
            ? Mathf.Clamp(entranceLoopStartCheckpointIndex.intValue, 0, points.arraySize - 2) : index;
    }

    private Vector3 CurvePointPosition(SerializedProperty points, int index, bool patrol)
    {
        return points.GetArrayElementAtIndex(index).FindPropertyRelative(patrol ? "offset" : "position").vector3Value;
    }

    private void GetAutoHandles(SerializedProperty points, int index, bool patrol, out Vector3 incoming, out Vector3 outgoing)
    {
        int previous = patrol && patrolLoop.boolValue
            ? (index - 1 + points.arraySize) % points.arraySize : Mathf.Max(0, index - 1);
        int next = CurveNextIndex(points, index, patrol);
        Vector3 tangent = (CurvePointPosition(points, next, patrol) - CurvePointPosition(points, previous, patrol)) / 6f;
        incoming = -tangent; outgoing = tangent;
    }

    private void DrawSegmentCurveInspector(SerializedProperty points, int index, bool patrol)
    {
        var point = points.GetArrayElementAtIndex(index);
        var motion = (DirectedWaveSegmentMotion)point.FindPropertyRelative("motionToNext").enumValueIndex;
        var settings = point.FindPropertyRelative("curveSettings");
        EditorGUI.BeginChangeCheck();
        switch (motion)
        {
            case DirectedWaveSegmentMotion.Bezier:
                DrawBezierPointInspector(points, index, patrol, "Start Checkpoint Tangents");
                int next = CurveNextIndex(points, index, patrol);
                if (next != index) DrawBezierPointInspector(points, next, patrol, "End Checkpoint Tangents");
                break;
            case DirectedWaveSegmentMotion.CatmullRom:
                EditorGUILayout.PropertyField(settings.FindPropertyRelative("tension"), new GUIContent("Tension", "0 preserves full tangents; 1 removes endpoint tangents."));
                EditorGUILayout.PropertyField(settings.FindPropertyRelative("parameterization"), new GUIContent("Parameterization"));
                break;
            case DirectedWaveSegmentMotion.Arc:
                EditorGUILayout.PropertyField(settings.FindPropertyRelative("bulge"), new GUIContent("Bulge", "-1..1; positive bends left of the route. Zero is straight; |1| is a semicircle."));
                EditorGUILayout.PropertyField(settings.FindPropertyRelative("longArc"), new GUIContent("Long Arc", "Use the complementary major arc of the same circle, on the other side of the chord."));
                break;
        }
        if (EditorGUI.EndChangeCheck())
        {
            if (patrol) InvalidatePatrolGeometryPreview(index);
            SceneView.RepaintAll();
        }
    }

    private void DrawBezierPointInspector(SerializedProperty points, int index, bool patrol, string label)
    {
        EditorGUI.BeginChangeCheck();
        var point = points.GetArrayElementAtIndex(index);
        var value = ReadCurveSettings(point);
        GetAutoHandles(points, index, patrol, out var autoIn, out var autoOut);
        EditorGUILayout.LabelField(label + " (" + index + ")", EditorStyles.boldLabel);
        var mode = (DirectedWaveBezierHandleMode)EditorGUILayout.EnumPopup("Handle Mode", value.handleMode);
        if (mode != value.handleMode)
        {
            if (mode != DirectedWaveBezierHandleMode.Auto && !value.handlesInitialized)
            {
                value.inHandle = autoIn; value.outHandle = autoOut; value.handlesInitialized = true;
            }
            value.handleMode = mode;
            if (mode == DirectedWaveBezierHandleMode.Aligned || mode == DirectedWaveBezierHandleMode.Mirrored)
                value.SetHandle(false, value.outHandle);
            WriteCurveSettings(point, value);
        }
        using (new EditorGUI.DisabledScope(mode == DirectedWaveBezierHandleMode.Auto))
        {
            EditorGUI.BeginChangeCheck();
            Vector3 incoming = EditorGUILayout.Vector3Field("In Handle", mode == DirectedWaveBezierHandleMode.Auto ? autoIn : value.inHandle);
            if (EditorGUI.EndChangeCheck()) { value.SetHandle(true, incoming); WriteCurveSettings(point, value); }
            EditorGUI.BeginChangeCheck();
            Vector3 outgoing = EditorGUILayout.Vector3Field("Out Handle", mode == DirectedWaveBezierHandleMode.Auto ? autoOut : value.outHandle);
            if (EditorGUI.EndChangeCheck()) { value.SetHandle(false, outgoing); WriteCurveSettings(point, value); }
        }
        if (mode == DirectedWaveBezierHandleMode.Aligned)
            EditorGUILayout.HelpBox("Handles share a tangent direction and keep independent lengths.", MessageType.None);
        else if (mode == DirectedWaveBezierHandleMode.Mirrored)
            EditorGUILayout.HelpBox("Handles have opposite directions and equal lengths.", MessageType.None);
        if (EditorGUI.EndChangeCheck()) CurveSceneChanged(index, patrol);
    }

    private void DrawCurveSceneHandles(DirectedEnemySubWave wave, SerializedProperty points, bool patrol)
    {
        if (points == null || points.arraySize < 2) return;
        int active = patrol ? activePatrolPointIndex : activePathCheckpointIndex;
        var space = (DirectedWaveCoordinateSpace)(patrol ? patrolCoordinateSpace : pathCoordinateSpace).enumValueIndex;
        for (int i = 0; i < points.arraySize; i++)
        {
            int next = CurveNextIndex(points, i, patrol);
            if (next == i) continue;
            var point = points.GetArrayElementAtIndex(i);
            var end = points.GetArrayElementAtIndex(next);
            var motion = (DirectedWaveSegmentMotion)point.FindPropertyRelative("motionToNext").enumValueIndex;
            Vector3 a = ToWorld(wave, CurvePointPosition(points, i, patrol), space);
            Vector3 b = ToWorld(wave, CurvePointPosition(points, next, patrol), space);
            int previous = patrol && patrolLoop.boolValue ? (i - 1 + points.arraySize) % points.arraySize : Mathf.Max(0, i - 1);
            int following = patrol ? CurveNextIndex(points, next, true) : Mathf.Min(next + 1, points.arraySize - 1);
            Vector3 before = ToWorld(wave, CurvePointPosition(points, previous, patrol), space);
            Vector3 after = ToWorld(wave, CurvePointPosition(points, following, patrol), space);
            var startSettings = ReadWorldCurveSettings(point, wave, space);
            var endSettings = ReadWorldCurveSettings(end, wave, space);
            Handles.color = patrol ? new Color(.35f, 1f, .9f, .7f) : new Color(0f, 1f, 1f, .7f);
            Vector3 last = a;
            for (int sample = 1; sample <= 32; sample++)
            {
                Vector3 current = DirectedWavePathEvaluator.EvaluateSegment(before, a, b, after, motion,
                    sample / 32f, startSettings, endSettings);
                Handles.DrawLine(last, current); last = current;
            }
            if (active != i && active != next && !(active < 0 && i == 0)) continue;
            if (motion == DirectedWaveSegmentMotion.Bezier)
            {
                DrawBezierHandle(wave, points, i, patrol, false, a, (b - before) / 6f, space);
                DrawBezierHandle(wave, points, next, patrol, true, b, -(after - a) / 6f, space);
            }
            else if (motion == DirectedWaveSegmentMotion.Arc)
            {
                var value = ReadCurveSettings(point);
                Vector3 midpoint = DirectedWavePathEvaluator.EvaluateArc(a, b, .5f, value.bulge, value.longArc);
                Vector3 chord = b - a; chord.z = 0f;
                float length = chord.magnitude;
                if (length < 1e-6f) continue;
                Vector3 normal = new Vector3(-chord.y, chord.x, 0f) / length;
                Handles.color = new Color(1f, .7f, .15f);
                Handles.DrawDottedLine((a + b) * .5f, midpoint, 4f);
                EditorGUI.BeginChangeCheck();
                Vector3 changed = Handles.Slider(midpoint, normal, HandleUtility.GetHandleSize(midpoint) * .08f, Handles.DotHandleCap, 0f);
                Handles.Label(midpoint, "Arc " + i);
                if (!EditorGUI.EndChangeCheck()) continue;
                float height = Vector3.Dot(changed - (a + b) * .5f, normal);
                value.bulge = value.longArc
                    ? (Mathf.Abs(height) < 1e-5f ? Mathf.Sign(value.bulge) : Mathf.Clamp(-length / (2f * height), -1f, 1f))
                    : Mathf.Clamp(2f * height / length, -1f, 1f);
                Undo.RecordObject(point.serializedObject.targetObject, "Change arc curvature");
                WriteCurveSettings(point, value);
                CurveSceneChanged(i, patrol);
            }
        }
    }

    private void DrawBezierHandle(DirectedEnemySubWave wave, SerializedProperty points, int index, bool patrol,
        bool incoming, Vector3 origin, Vector3 autoOffset, DirectedWaveCoordinateSpace space)
    {
        var point = points.GetArrayElementAtIndex(index);
        var value = ReadCurveSettings(point);
        var worldValue = ReadWorldCurveSettings(point, wave, space);
        Vector3 offset = value.handleMode == DirectedWaveBezierHandleMode.Auto ? autoOffset
            : incoming ? worldValue.inHandle : worldValue.outHandle;
        Vector3 position = origin + offset;
        Handles.color = incoming ? new Color(1f, .5f, .4f) : new Color(.4f, 1f, .5f);
        Handles.DrawLine(origin, position);
        Handles.Label(position, (incoming ? "In " : "Out ") + index);
        if (value.handleMode == DirectedWaveBezierHandleMode.Auto)
        {
            if (Event.current.type == EventType.Repaint)
                Handles.DotHandleCap(0, position, Quaternion.identity, HandleUtility.GetHandleSize(position) * .04f, EventType.Repaint);
            return;
        }
        EditorGUI.BeginChangeCheck();
        Vector3 changed = Handles.PositionHandle(position, Quaternion.identity);
        if (!EditorGUI.EndChangeCheck()) return;
        Undo.RecordObject(point.serializedObject.targetObject, "Move Bezier tangent");
        Vector3 localOffset = FromWorld(wave, changed, space) - FromWorld(wave, origin, space);
        value.SetHandle(incoming, localOffset);
        WriteCurveSettings(point, value);
        CurveSceneChanged(index, patrol);
    }

    private void CurveSceneChanged(int index, bool patrol)
    {
        if (patrol)
        {
            InvalidatePatrolGeometryPreview(index);
            QueuePatrolPointMetricRecalculation(index);
        }
        else
        {
            if (index < pathCheckpoints.arraySize - 1)
                SyncCheckpointDurationAndSpeed(index, false, false, true);
            if (index > 0) SyncCheckpointDurationAndSpeed(index - 1, false, false, true);
        }
        Repaint(); SceneView.RepaintAll();
    }
}
