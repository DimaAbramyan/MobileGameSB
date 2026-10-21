using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor.SceneManagement;

public static class VerifyMotionCurves
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static int checks;
    public static object Run()
    {
        if (EditorApplication.isPlaying || EditorApplication.isCompiling || EditorApplication.isUpdating)
            throw new Exception("Idle Editor required.");
        checks = 0;
        VerifyLegacy(); VerifyShapes(); VerifyHandles(); VerifyIntegration();
        return new { passed = checks, legacyDefaultGeometryExact = true, phaseEntryPatrolPreview = true };
    }
    private static void VerifyLegacy()
    {
        var random = new System.Random(1701);
        for (int route = 0; route < 20; route++)
        {
            Vector3 a = RandomPoint(random), b = RandomPoint(random), c = RandomPoint(random), d = RandomPoint(random);
            foreach (var mode in new[] { DirectedWaveSegmentMotion.Linear, DirectedWaveSegmentMotion.Bezier, DirectedWaveSegmentMotion.CatmullRom })
                for (int i = 0; i <= 40; i++)
                {
                    float t = i / 40f;
                    Vector3 expected = Legacy(a, b, c, d, mode, t);
                    Vector3 actual = DirectedWavePathEvaluator.EvaluateSegment(a, b, c, d, mode, t);
                    Check(actual.x == expected.x && actual.y == expected.y && actual.z == expected.z, "Exact legacy default: " + mode);
                }
        }
        var old = JsonUtility.FromJson<DirectedWavePathCheckpoint>("{\"position\":{\"x\":2,\"y\":3,\"z\":0},\"durationToNext\":0.8,\"speedToNext\":4,\"motionToNext\":2}");
        Check(old.curveSettings.handleMode == DirectedWaveBezierHandleMode.Auto && old.curveSettings.tension == 0f
            && old.curveSettings.parameterization == DirectedWaveCurveParameterization.Uniform, "Old serialized defaults");
        Check(old.durationToNext == .8f && old.speedToNext == 4f && old.motionToNext == DirectedWaveSegmentMotion.CatmullRom, "Old timing unchanged");
    }
    private static void VerifyShapes()
    {
        Vector3 a = new Vector3(-3, 2), b = Vector3.zero, c = new Vector3(2, 0), d = new Vector3(3, 4);
        foreach (bool major in new[] { false, true })
            foreach (float bulge in new[] { -.8f, -.5f, .5f, .8f, 1f })
            {
                Check(DirectedWavePathEvaluator.EvaluateArc(b, c, 0f, bulge, major) == b, "Arc start");
                Check(DirectedWavePathEvaluator.EvaluateArc(b, c, 1f, bulge, major) == c, "Arc end");
                float expectedHeight = major ? -1f / bulge : bulge;
                Near(DirectedWavePathEvaluator.EvaluateArc(b, c, .5f, bulge, major), new Vector3(1, expectedHeight), 2e-5f, "Arc midpoint and side");
                float centerY = -(1f - bulge * bulge) / (2f * bulge);
                var center = new Vector3(1, centerY);
                float radius = Vector3.Distance(b, center);
                for (int i = 0; i <= 20; i++)
                    Check(Mathf.Abs(Vector3.Distance(DirectedWavePathEvaluator.EvaluateArc(b, c, i / 20f, bulge, major), center) - radius) < 2e-5f, "Circular arc radius");
            }
        Near(DirectedWavePathEvaluator.EvaluateArc(b, c, .25f, 0f, true), new Vector3(.5f, 0), 0f, "Zero bulge is straight");
        Near(DirectedWavePathEvaluator.EvaluateArc(b, b, .5f, .5f, true), b, 0f, "Coincident arc endpoints");
        var free = new DirectedWaveCurveSettings { handleMode = DirectedWaveBezierHandleMode.Free, outHandle = new Vector3(0, 2) };
        var end = new DirectedWaveCurveSettings { handleMode = DirectedWaveBezierHandleMode.Free, inHandle = new Vector3(0, 2) };
        Near(DirectedWavePathEvaluator.EvaluateSegment(a, b, c, d, DirectedWaveSegmentMotion.Bezier, .5f, free, end), new Vector3(1, 1.5f), 1e-6f, "Manual cubic control points");
        foreach (var parameterization in new[] { DirectedWaveCurveParameterization.Uniform, DirectedWaveCurveParameterization.Centripetal, DirectedWaveCurveParameterization.Chordal })
            foreach (float tension in new[] { 0f, .5f, 1f })
            {
                var settings = new DirectedWaveCurveSettings { parameterization = parameterization, tension = tension };
                Near(DirectedWavePathEvaluator.EvaluateSegment(a, b, c, d, DirectedWaveSegmentMotion.CatmullRom, 0f, settings), b, 1e-6f, "Catmull start");
                Near(DirectedWavePathEvaluator.EvaluateSegment(a, b, c, d, DirectedWaveSegmentMotion.CatmullRom, 1f, settings), c, 1e-6f, "Catmull end");
                for (int i = 0; i <= 20; i++)
                {
                    var point = DirectedWavePathEvaluator.EvaluateSegment(b, b, c, c, DirectedWaveSegmentMotion.CatmullRom, i / 20f, settings);
                    Check(float.IsFinite(point.x) && float.IsFinite(point.y), "Repeated endpoint knots stay finite");
                }
            }
        var zero = DirectedWavePathEvaluator.EvaluateSegment(a, b, c, d, DirectedWaveSegmentMotion.CatmullRom, .3f);
        var tight = DirectedWavePathEvaluator.EvaluateSegment(a, b, c, d, DirectedWaveSegmentMotion.CatmullRom, .3f,
            new DirectedWaveCurveSettings { tension = 1f });
        Check(Vector3.Distance(zero, tight) > .01f, "Tension affects shape");
        var uniform = DirectedWavePathEvaluator.EvaluateSegment(a, b, c, d, DirectedWaveSegmentMotion.CatmullRom, .3f);
        var centripetal = DirectedWavePathEvaluator.EvaluateSegment(a, b, c, d, DirectedWaveSegmentMotion.CatmullRom, .3f,
            new DirectedWaveCurveSettings { parameterization = DirectedWaveCurveParameterization.Centripetal });
        var chordal = DirectedWavePathEvaluator.EvaluateSegment(a, b, c, d, DirectedWaveSegmentMotion.CatmullRom, .3f,
            new DirectedWaveCurveSettings { parameterization = DirectedWaveCurveParameterization.Chordal });
        Check(Vector3.Distance(uniform, centripetal) > .001f && Vector3.Distance(centripetal, chordal) > .001f, "Parameterization affects shape");
    }
    private static void VerifyHandles()
    {
        var settings = new DirectedWaveCurveSettings { handleMode = DirectedWaveBezierHandleMode.Free, inHandle = new Vector3(-1, 0), outHandle = new Vector3(2, 0) };
        settings.SetHandle(false, new Vector3(0, 3));
        Check(settings.inHandle == new Vector3(-1, 0), "Free keeps other handle");
        settings.handleMode = DirectedWaveBezierHandleMode.Aligned;
        settings.SetHandle(false, new Vector3(0, 2));
        Near(settings.inHandle, new Vector3(0, -1), 1e-6f, "Aligned preserves opposite length");
        settings.handleMode = DirectedWaveBezierHandleMode.Mirrored;
        settings.SetHandle(true, new Vector3(-3, 1));
        Check(settings.outHandle == new Vector3(3, -1), "Mirrored matches opposite length");
        var restored = JsonUtility.FromJson<DirectedWavePathCheckpoint>(JsonUtility.ToJson(new DirectedWavePathCheckpoint { curveSettings = settings }));
        Check(restored.curveSettings.inHandle == settings.inHandle && restored.curveSettings.handleMode == settings.handleMode, "Preset serialization retains settings");
        var matrix = Matrix4x4.TRS(new Vector3(99, 88), Quaternion.Euler(0, 0, 90), new Vector3(2, 3, 1));
        var transformed = settings.TransformHandles(matrix);
        Near(transformed.outHandle, matrix.MultiplyVector(settings.outHandle), 1e-6f, "Handle transform excludes translation");
    }
    private static void VerifyIntegration()
    {
        var root = new GameObject("Motion curve integration verification");
        var scene = EditorSceneManager.NewPreviewScene();
        SceneManager.MoveGameObjectToScene(root, scene);
        Editor editor = null;
        try
        {
            root.AddComponent<DirectedWavePhaseEntryBehaviour>(); root.AddComponent<DirectedWavePostBehaviour>();
            var wave = root.AddComponent<DirectedEnemySubWave>();
            root.transform.SetPositionAndRotation(new Vector3(3, 4), Quaternion.Euler(0, 0, 30));
            root.transform.localScale = new Vector3(2, 1.5f, 1);
            var points = new[]
            {
                new DirectedWavePathCheckpoint { position = new Vector3(-2, 0), durationToNext = .8f },
                new DirectedWavePathCheckpoint { position = new Vector3(0, 1), durationToNext = 1.3f },
                new DirectedWavePathCheckpoint { position = new Vector3(3, 0), durationToNext = .7f },
                new DirectedWavePathCheckpoint { position = new Vector3(4, 2), durationToNext = 1.1f }
            };
            Set(wave, "pathCheckpoints", points); Set(wave, "pathCoordinateSpace", DirectedWaveCoordinateSpace.LocalToSubWave);
            var patrol = new DirectedWavePatrolPoint[points.Length];
            for (int i = 0; i < points.Length; i++) patrol[i] = new DirectedWavePatrolPoint { offset = points[i].position };
            Set(wave, "patrolPoints", patrol); Set(wave, "patrolCoordinateSpace", DirectedWaveCoordinateSpace.LocalToSubWave);
            Set(wave, "patrolLoop", false);
            editor = Editor.CreateEditor(wave, typeof(DirectedEnemySubWaveEditor));
            foreach (var mode in new[] { DirectedWaveSegmentMotion.Bezier, DirectedWaveSegmentMotion.CatmullRom, DirectedWaveSegmentMotion.Arc })
            {
                for (int i = 0; i < points.Length; i++)
                {
                    points[i].motionToNext = mode; patrol[i].motionToNext = mode;
                    var settings = new DirectedWaveCurveSettings { handleMode = DirectedWaveBezierHandleMode.Free,
                        inHandle = new Vector3(-.4f, -.8f), outHandle = new Vector3(.5f, 1f), handlesInitialized = true,
                        tension = .35f, parameterization = DirectedWaveCurveParameterization.Centripetal, bulge = .45f, longArc = i == 1 };
                    points[i].curveSettings = settings; patrol[i].curveSettings = settings;
                }
                Invoke(editor, "UpdatePropertySources");
                var runtime = (DirectedWaveRuntimeCheckpoint[])Invoke(wave, "BuildWorldPathCheckpoints");
                var preview = (DirectedWaveRuntimeCheckpoint[])Invoke(editor, "GetWorldPathCheckpoints", wave);
                Check(runtime.Length == points.Length && preview.Length == points.Length, "Checkpoint model shared");
                for (int segment = 0; segment < points.Length - 1; segment++)
                    for (int sample = 0; sample <= 10; sample++)
                    {
                        float t = sample / 10f;
                        Near(DirectedWavePathEvaluator.EvaluateSegment(runtime, segment, t),
                            DirectedWavePathEvaluator.EvaluateSegment(preview, segment, t), 1e-5f, "Entry preview geometry: " + mode);
                        Near((Vector3)Invoke(wave, "EvaluatePatrolSegment", segment, t),
                            (Vector3)Invoke(editor, "EvaluatePatrolSegment", segment, t), 1e-5f, "Patrol preview geometry: " + mode);
                        Near((Vector3)Invoke(wave, "EvaluatePatrolSegment", segment, t),
                            DirectedWavePathEvaluator.EvaluateSegment(runtime, segment, t), 1e-5f, "Entry and patrol geometry: " + mode);
                    }
                var simulated = (Vector3)Invoke(wave, "EvaluateSimulationCheckpointPath", runtime, .4f);
                float parameter = DirectedWavePathEvaluator.GetParameterAtNormalizedDistance(runtime, 0, .5f);
                Near(simulated, DirectedWavePathEvaluator.EvaluateSegment(runtime, 0, parameter), 1e-5f, "Preview timing and speed preserved");
                // Loop evaluator is internal; use its actual implementation through reflection.
                var loopType = typeof(DirectedWavePathEvaluator).Assembly.GetType("DirectedWaveEntranceLoopEvaluator");
                var loop = loopType.GetMethod("EvaluateLoopSegment", BindingFlags.Static | BindingFlags.Public);
                var actual = (Vector3)loop.Invoke(null, new object[] { runtime, 2, 3, 1, 2, .4f });
                var expected = DirectedWavePathEvaluator.EvaluateSegment(runtime[2].position, runtime[3].position,
                    runtime[1].position, runtime[2].position, mode, .4f, runtime[3].curveSettings, runtime[1].curveSettings);
                Near(actual, expected, 1e-5f, "Closed entry segment carries settings");
            }
            var savedPatrolPoint = patrol[2]; patrol[2] = null;
            var nullableEnd = (Vector3)Invoke(wave, "EvaluatePatrolSegment", 1, .5f);
            Check(float.IsFinite(nullableEnd.x) && float.IsFinite(nullableEnd.y), "Missing patrol endpoint remains safe");
            patrol[2] = savedPatrolPoint;
            VerifySerializedEditing(wave, editor);
        }
        finally { if (editor != null) UnityEngine.Object.DestroyImmediate(editor); EditorSceneManager.ClosePreviewScene(scene); }
    }

    private static void VerifySerializedEditing(DirectedEnemySubWave wave, Editor editor)
    {
        Invoke(editor, "UpdatePropertySources");
        var points = (SerializedProperty)editor.GetType().GetField("pathCheckpoints", Private).GetValue(editor);
        var point = points.GetArrayElementAtIndex(1);
        var read = editor.GetType().GetMethod("ReadCurveSettings", BindingFlags.Static | BindingFlags.NonPublic);
        var write = editor.GetType().GetMethod("WriteCurveSettings", BindingFlags.Static | BindingFlags.NonPublic);
        var before = (DirectedWaveCurveSettings)read.Invoke(null, new object[] { point });
        var changed = before;
        changed.tension = .87f; changed.parameterization = DirectedWaveCurveParameterization.Chordal;
        changed.bulge = -.35f; changed.longArc = true;
        changed.handleMode = DirectedWaveBezierHandleMode.Mirrored;
        changed.SetHandle(false, new Vector3(.7f, 1.9f));
        UnityEngine.Object owner = point.serializedObject.targetObject;
        int group = Undo.GetCurrentGroup(); Undo.IncrementCurrentGroup();
        try
        {
            Undo.RecordObject(owner, "Verify route parameter undo");
            write.Invoke(null, new object[] { point, changed });
            point.serializedObject.ApplyModifiedProperties(); Undo.FlushUndoRecordObjects();
            var live = (DirectedWavePathCheckpoint[])wave.GetType().GetProperty("pathCheckpoints", Private).GetValue(wave);
            Check(live[1].curveSettings.bulge == -.35f && live[1].curveSettings.inHandle == -changed.outHandle,
                "Numeric settings update serialized model");
            float duration = live[1].durationToNext;
            float speed = live[1].speedToNext;
            foreach (var mode in new[] { DirectedWaveSegmentMotion.Linear, DirectedWaveSegmentMotion.Bezier, DirectedWaveSegmentMotion.CatmullRom, DirectedWaveSegmentMotion.Arc })
            {
                point.FindPropertyRelative("motionToNext").enumValueIndex = (int)mode;
                point.serializedObject.ApplyModifiedProperties();
                var stored = (DirectedWaveCurveSettings)read.Invoke(null, new object[] { point });
                Check(stored.bulge == changed.bulge && stored.tension == changed.tension
                    && stored.inHandle == changed.inHandle && stored.outHandle == changed.outHandle,
                    "Switching curve type retains parameters");
            }
            var saved = EditorJsonUtility.ToJson(owner);
            var cleared = changed; cleared.bulge = 0f; cleared.inHandle = Vector3.zero;
            write.Invoke(null, new object[] { point, cleared }); point.serializedObject.ApplyModifiedProperties();
            EditorJsonUtility.FromJsonOverwrite(saved, owner); point.serializedObject.Update();
            var loaded = (DirectedWaveCurveSettings)read.Invoke(null, new object[] { point });
            Check(loaded.bulge == changed.bulge && loaded.inHandle == changed.inHandle, "Component save/load preserves parameters");
            live = (DirectedWavePathCheckpoint[])wave.GetType().GetProperty("pathCheckpoints", Private).GetValue(wave);
            Check(live[1].durationToNext == duration && live[1].speedToNext == speed, "Editing curve data preserves timing fields");
            Undo.FlushUndoRecordObjects();
            Undo.CollapseUndoOperations(group + 1);
            Undo.PerformUndo(); point.serializedObject.Update();
            var undone = (DirectedWaveCurveSettings)read.Invoke(null, new object[] { point });
            Check(undone.bulge == before.bulge && undone.tension == before.tension && undone.inHandle == before.inHandle,
                "Undo restores curve parameters");
        }
        finally { Undo.ClearUndo(owner); Undo.IncrementCurrentGroup(); }
    }
    private static Vector3 RandomPoint(System.Random r) => new Vector3((float)r.NextDouble() * 10, (float)r.NextDouble() * 10, (float)r.NextDouble());
    private static object Invoke(object target, string method, params object[] args) => target.GetType().GetMethod(method, Private).Invoke(target, args);
    private static void Set(object target, string property, object value) => target.GetType().GetProperty(property, Private).SetValue(target, value);
    private static void Near(Vector3 a, Vector3 b, float tolerance, string message) => Check(Vector3.Distance(a, b) <= tolerance, message + " " + a + " vs " + b);
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); checks++; }
    private static Vector3 Legacy(Vector3 previous, Vector3 current, Vector3 next, Vector3 following, DirectedWaveSegmentMotion motion, float time)
    {
        float t = Mathf.Clamp01(time);
        if (motion == DirectedWaveSegmentMotion.Linear) return Vector3.LerpUnclamped(current, next, time);
        if (motion == DirectedWaveSegmentMotion.Bezier)
        {
            Vector3 p1 = current + (next - previous) / 6f, p2 = next - (following - current) / 6f;
            float u = 1f - t;
            return u * u * u * current + 3f * u * u * t * p1 + 3f * u * t * t * p2 + t * t * t * next;
        }
        return .5f * (2f * current + (-previous + next) * t + (2f * previous - 5f * current + 4f * next - following) * t * t
            + (-previous + 3f * current - 3f * next + following) * t * t * t);
    }
}
