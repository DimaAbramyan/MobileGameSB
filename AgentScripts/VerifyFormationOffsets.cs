using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class VerifyFormationOffsets
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    public static object Run()
    {
        if (EditorApplication.isPlaying || EditorApplication.isCompiling || EditorApplication.isUpdating)
            throw new Exception("Editor must be idle in Edit Mode.");
        var root = new GameObject("Formation offset verification");
        var legacyRoot = new GameObject("Legacy formation offset verification");
        Editor editor = null;
        Editor legacyEditor = null;
        int checks = 0;
        try
        {
            root.transform.position = new Vector3(8, -3, 0);
            root.transform.rotation = Quaternion.Euler(0, 0, 25);
            root.transform.localScale = new Vector3(1.3f, 0.8f, 1);
            var wave = root.AddComponent<DirectedEnemySubWave>();
            var formation = root.AddComponent<DirectedWaveFormationBehaviour>();
            wave.SetFormationBehaviour(formation);
            var points = new GameObject("Points").transform; points.SetParent(root.transform, false);
            for (int i = 0; i < 5; i++)
            { var point = new GameObject("Slot" + i).transform; point.SetParent(points, false); point.localPosition = new Vector3(i, i * 0.3f, 0); }
            var config = new SerializedObject(formation);
            P(config, "enemyCount").intValue = 5;
            P(config, "columns").intValue = 1; P(config, "rows").intValue = 5;
            P(config, "shapePointCount").intValue = 5;
            P(config, "formationPointsRoot").objectReferenceValue = points;
            var cells = P(config, "gridMatrixCells"); cells.arraySize = 5;
            var custom = P(config, "customFormationPoints"); custom.arraySize = 5;
            for (int i = 0; i < 5; i++)
            { cells.GetArrayElementAtIndex(i).boolValue = true; custom.GetArrayElementAtIndex(i).vector3Value = new Vector3(2, i, 0); }
            config.ApplyModifiedProperties();
            editor = Editor.CreateEditor(wave);
            Vector2 step = new Vector2(0.7f, -0.4f);
            foreach (DirectedWaveFormationLayout layout in Enum.GetValues(typeof(DirectedWaveFormationLayout)))
            foreach (DirectedWaveCoordinateSpace space in new[] { DirectedWaveCoordinateSpace.World, DirectedWaveCoordinateSpace.LocalToSubWave })
            {
                config.Update(); P(config, "formationLayout").enumValueIndex = (int)layout;
                P(config, "formationCoordinateSpace").enumValueIndex = (int)space;
                P(config, "formationOffsetStep").vector2Value = Vector2.zero;
                config.ApplyModifiedProperties(); Refresh(editor);
                int count = wave.GetRewardEligibleEnemyCount();
                var baseline = new Vector3[count];
                for (int i = 0; i < count; i++) baseline[i] = Runtime(wave, i);
                for (int choice = 0; choice < 4; choice++)
                {
                    var origin = choice < 2 ? DirectedWaveFormationOffsetOrigin.Middle
                        : choice == 2 ? DirectedWaveFormationOffsetOrigin.FirstEnemy : DirectedWaveFormationOffsetOrigin.LastEnemy;
                    config.Update(); P(config, "formationOffsetStep").vector2Value = step;
                    P(config, "formationOffsetOrigin").enumValueIndex = (int)origin;
                    P(config, "formationCenterOffsetMode").enumValueIndex = choice == 1 ? 1 : 0;
                    config.ApplyModifiedProperties(); Refresh(editor);
                    for (int i = 0; i < count; i++)
                    {
                        float factor = choice == 2 ? i : choice == 3 ? count - 1 - i : i - (count - 1) * 0.5f;
                        if (choice == 1) factor = Mathf.Abs(factor);
                        Vector3 delta = new Vector3(step.x * factor, step.y * factor, 0);
                        if (space == DirectedWaveCoordinateSpace.LocalToSubWave) delta = root.transform.TransformVector(delta);
                        Vector3 actual = Runtime(wave, i);
                        Check(Vector3.Distance(actual, baseline[i] + delta) < 0.0001f, "Runtime offset " + layout, ref checks);
                        var preview = (Vector3)Invoke(editor, "GetFormationWorldPosition", i, wave);
                        Check(Vector3.Distance(actual, preview) < 0.0001f, "Preview parity " + layout, ref checks);
                    }
                }
            }
            // Bake keeps visible positions and clears the already-baked modifier.
            config.Update(); P(config, "formationLayout").enumValueIndex = (int)DirectedWaveFormationLayout.VerticalLine;
            P(config, "formationOffsetOrigin").enumValueIndex = (int)DirectedWaveFormationOffsetOrigin.Middle;
            P(config, "formationOffsetStep").vector2Value = step; config.ApplyModifiedProperties(); Refresh(editor);
            var beforeBake = new Vector3[5]; for (int i = 0; i < 5; i++) beforeBake[i] = Runtime(wave, i);
            Invoke(editor, "FreezeFormation");
            config.Update();
            Check(P(config, "formationOffsetStep").vector2Value == Vector2.zero, "Bake clears modifier", ref checks);
            for (int i = 0; i < 5; i++) Check(Vector3.Distance(Runtime(wave, i), beforeBake[i]) < 0.0001f, "Bake preserves position", ref checks);
            Invoke(editor, "UnfreezeFormation");
            for (int i = 0; i < 5; i++) Check(Vector3.Distance(Runtime(wave, i), beforeBake[i]) < 0.0001f, "Unfreeze does not double offset", ref checks);
            Check(DirectedWaveFormationOffset.Evaluate(0, 1, step, DirectedWaveFormationOffsetOrigin.Middle,
                DirectedWaveFormationCenterOffsetMode.Linear) == Vector3.zero, "Single slot stays fixed", ref checks);
            Check(DirectedWaveFormationOffset.Evaluate(0, 4, Vector2.right, DirectedWaveFormationOffsetOrigin.Middle,
                DirectedWaveFormationCenterOffsetMode.Linear).x == -1.5f, "Even count uses center between slots", ref checks);
            var legacy = legacyRoot.AddComponent<DirectedEnemySubWave>();
            var legacySo = new SerializedObject(legacy);
            legacySo.FindProperty("structuredConfigurationVersion").intValue = 0;
            legacySo.FindProperty("legacyEnemyCount").intValue = 5;
            legacySo.FindProperty("legacyFormationLayout").enumValueIndex = (int)DirectedWaveFormationLayout.VerticalLine;
            legacySo.FindProperty("legacyFormationCoordinateSpace").enumValueIndex = (int)DirectedWaveCoordinateSpace.World;
            legacySo.FindProperty("legacySpacing").vector2Value = Vector2.one;
            legacySo.FindProperty("legacyFormationCenter").vector3Value = Vector3.zero;
            legacySo.FindProperty("formation.formationOffsetStep").vector2Value = step;
            legacySo.ApplyModifiedProperties();
            legacyEditor = Editor.CreateEditor(legacy);
            for (int i = 0; i < 5; i++)
            {
                Vector3 expected = new Vector3(0, 2 - i, 0) + new Vector3(step.x, step.y, 0) * (i - 2);
                Check(Vector3.Distance(Runtime(legacy, i), expected) < 0.0001f, "Legacy runtime offsets without migration", ref checks);
                Check(Vector3.Distance((Vector3)Invoke(legacyEditor, "GetFormationWorldPosition", i, legacy), expected) < 0.0001f,
                    "Legacy preview offsets without migration", ref checks);
            }
        }
        finally
        {
            if (editor != null) UnityEngine.Object.DestroyImmediate(editor);
            if (legacyEditor != null) UnityEngine.Object.DestroyImmediate(legacyEditor);
            UnityEngine.Object.DestroyImmediate(root); UnityEngine.Object.DestroyImmediate(legacyRoot);
        }
        return new { passed = checks, layouts = 11, coordinateSpaces = 2, offsetChoices = 4 };
    }
    private static SerializedProperty P(SerializedObject so, string field) => so.FindProperty("configuration." + field);
    private static void Refresh(Editor editor) => Invoke(editor, "UpdatePropertySources");
    private static Vector3 Runtime(DirectedEnemySubWave wave, int index) => (Vector3)Invoke(wave, "CalculateFormationPosition", index);
    private static object Invoke(object target, string method, params object[] args) => target.GetType().GetMethod(method, Private).Invoke(target, args);
    private static void Check(bool value, string label, ref int count) { if (!value) throw new Exception(label); count++; }
}
