using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Unity.Entities;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class VerifyNumericRandomizationEditor
{
    private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    public static async Task<object> Run()
    {
        var view = SceneView.lastActiveSceneView;
        if (view == null) throw new Exception("Scene View required");
        bool visible = EnemyShootingPreview.ShowFormationVolley;
        int worlds = World.All.Count;
        int undoGroup = Undo.GetCurrentGroup(); Undo.IncrementCurrentGroup();
        var scene = EditorSceneManager.NewPreviewScene();
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Waves/2/2_3_extrime.prefab");
        var root = (GameObject)PrefabUtility.InstantiatePrefab(asset, scene);
        Editor editor = null;
        Action<SceneView> draw = null;
        try
        {
            var wave = root.transform.Find("Main").GetComponent<DirectedEnemySubWave>();
            var behaviour = wave.AttackPatternBehaviour;
            using var serialized = new SerializedObject(behaviour);
            serialized.FindProperty("executionMode").enumValueIndex = 1;
            var steps = serialized.FindProperty("attackSequence.steps"); steps.arraySize = 1;
            steps.GetArrayElementAtIndex(0).managedReferenceValue = new DirectedWaveAttackSequenceStep {
                name = "Repeat", type = DirectedWaveAttackSequenceStepType.RepeatBlock, repeatCount = 3,
                steps = new() { new DirectedWaveAttackSequenceStep { name = "Attack" }, new DirectedWaveAttackSequenceStep {
                    name = "Pause", type = DirectedWaveAttackSequenceStepType.Pause } } };
            var order = steps.GetArrayElementAtIndex(0).FindPropertyRelative("steps").GetArrayElementAtIndex(0)
                .FindPropertyRelative("pattern.attackSettings.attackOrder");
            order.FindPropertyRelative("enabled").boolValue = true;
            order.FindPropertyRelative("mode").enumValueIndex = (int)DirectedWaveAttackOrderMode.Manual;
            var manual = order.FindPropertyRelative("manualSlots"); manual.arraySize = 3;
            manual.GetArrayElementAtIndex(0).intValue = 2; manual.GetArrayElementAtIndex(1).intValue = 0; manual.GetArrayElementAtIndex(2).intValue = 1;
            var numericPattern = steps.GetArrayElementAtIndex(0).FindPropertyRelative("steps").GetArrayElementAtIndex(0)
                .FindPropertyRelative("pattern.attackSettings.waveBurstSettings");
            var shooting = numericPattern.FindPropertyRelative("shooting");
            shooting.FindPropertyRelative("overrideProjectileBaseSpeed").boolValue = true;
            shooting.FindPropertyRelative("projectileBaseSpeed").floatValue = 7f;
            numericPattern.FindPropertyRelative("attackCooldown").floatValue = 5f;
            shooting.FindPropertyRelative("emissionOrigin.x.offset").floatValue = 9f;
            foreach (var path in new[] { "attackCooldownRandom", "shooting.projectileBaseSpeedRandom", "shooting.emissionOrigin.x.offsetRandom" })
            {
                var random = numericPattern.FindPropertyRelative(path);
                random.FindPropertyRelative("enabled").boolValue = true;
                random.FindPropertyRelative("initialized").boolValue = true;
                random.FindPropertyRelative("min").floatValue = 1f;
                random.FindPropertyRelative("max").floatValue = 3f;
            }
            serialized.ApplyModifiedProperties(); Undo.FlushUndoRecordObjects();
            if (!behaviour.UsesAttackSequence || behaviour.AttackSequence.steps[0].steps.Count != 2) throw new Exception("Managed sequence did not serialize");
            bool recorded = false;
            foreach (var modification in PrefabUtility.GetPropertyModifications(root))
                if (modification.propertyPath.Contains("executionMode") || modification.propertyPath.Contains("attackSequence")) recorded = true;
            if (!recorded) throw new Exception("Sequence prefab overrides not recorded");
            Undo.PerformUndo(); if (behaviour.UsesAttackSequence) throw new Exception("Sequence Undo failed");
            Undo.PerformRedo(); if (!behaviour.UsesAttackSequence || behaviour.AttackSequence.steps[0].steps.Count != 2) throw new Exception("Sequence Redo failed");
            var roundTrip = new DirectedWaveAttackSettings(); behaviour.AttackSequence.steps[0].steps[0].CopySettingsTo(roundTrip);
            if (!roundTrip.UsesAttackOrder || roundTrip.AttackOrder.manualSlots[0] != 2)
                throw new Exception("Attack Order did not survive Undo/Redo");
            var afterNumericUndo = new DirectedWaveAttackSettings(); behaviour.AttackSequence.steps[0].steps[0].CopySettingsTo(afterNumericUndo);
            if (!afterNumericUndo.WaveBurstSettings.AttackCooldownRandom.enabled || !afterNumericUndo.WaveBurstSettings.Shooting.ProjectileBaseSpeedRandom.enabled)
                throw new Exception("Random ranges did not survive Undo/Redo");
            var fixedPath = serialized.FindProperty("attackSequence.steps").GetArrayElementAtIndex(0).FindPropertyRelative("steps").GetArrayElementAtIndex(0)
                .FindPropertyRelative("pattern.attackSettings.waveBurstSettings.shooting.projectileBaseSpeedRandom.enabled");
            fixedPath.boolValue = false; serialized.ApplyModifiedProperties();
            fixedPath.boolValue = true; serialized.ApplyModifiedProperties();
            var restoredNumeric = new DirectedWaveAttackSettings(); behaviour.AttackSequence.steps[0].steps[0].CopySettingsTo(restoredNumeric);
            if (restoredNumeric.WaveBurstSettings.Shooting.ProjectileBaseSpeedRandom.min != 1f || restoredNumeric.WaveBurstSettings.Shooting.InitialSpeed != 7f)
                throw new Exception("Toggle lost fixed value or range");
            string before = EditorJsonUtility.ToJson(behaviour);
            EditorJsonUtility.FromJsonOverwrite(before, behaviour);
            if (behaviour.AttackSequence.steps[0].steps[1].name != "Pause") throw new Exception("Nested sequence round-trip failed");

            editor = Editor.CreateEditor(behaviour);
            var serializedSteps = editor.serializedObject.FindProperty("attackSequence.steps");
            serializedSteps.GetArrayElementAtIndex(0).isExpanded = true;
            var nested = serializedSteps.GetArrayElementAtIndex(0).FindPropertyRelative("steps");
            nested.GetArrayElementAtIndex(0).isExpanded = true; nested.GetArrayElementAtIndex(1).isExpanded = true;
            var rendered = new TaskCompletionSource<bool>();
            draw = current =>
            {
                if (current != view || Event.current.type != EventType.Layout && Event.current.type != EventType.Repaint) return;
                try
                {
                    Handles.BeginGUI();
                    GUILayout.BeginArea(new Rect(10, 10, 600, 900));
                    try { editor.OnInspectorGUI(); }
                    finally { GUILayout.EndArea(); Handles.EndGUI(); }
                    if (Event.current.type == EventType.Repaint)
                    {
                        var sceneGuides = editor.GetType().GetField("attackOrderScenePreview", Fields).GetValue(editor);
                        sceneGuides.GetType().GetMethod("Draw").Invoke(sceneGuides, new object[] { wave, behaviour });
                        using (var orderPreview = new WaveShootingPreviewSession(wave, true, true, 0, Vector3.down * 4f))
                        {
                            while (!orderPreview.AdvanceTo(.1f, 1000d)) { }
                            typeof(WaveShootingPreviewSession).GetMethod("DrawAttackOrders", Fields).Invoke(orderPreview, null);
                        }
                        rendered.TrySetResult(true);
                    }
                }
                catch (Exception error) { rendered.TrySetException(error); }
            };
            SceneView.duringSceneGui += draw; view.Repaint();
            if (await Task.WhenAny(rendered.Task, Task.Delay(10000)) != rendered.Task) throw new Exception("Sequence inspector repaint timed out");
            await rendered.Task;
            SceneView.duringSceneGui -= draw; draw = null;
            UnityEngine.Object.DestroyImmediate(editor); editor = null;

            int count = behaviour.AttackSequence.steps[0].steps.Count;
            var copy = JsonUtility.FromJson<DirectedWaveAttackSequenceStep>(JsonUtility.ToJson(behaviour.AttackSequence.steps[0]));
            copy.steps[0].name = "Independent copy";
            if (behaviour.AttackSequence.steps[0].steps[0].name == copy.steps[0].name) throw new Exception("Duplicate shares nested steps");
            for (int i = 0; i < 3; i++)
            {
                using var preview = new WaveShootingPreviewSession(wave, true, true, 0, Vector3.down * 4f);
                while (!preview.AdvanceTo(.1f, 1000d)) { }
            }
            if (World.All.Count != worlds) throw new Exception("Sequence preview world leaked");
            editor = Editor.CreateEditor(wave);
            typeof(DirectedEnemySubWaveEditor).GetField("simulateShooting", Fields).SetValue(editor, true);
            typeof(DirectedEnemySubWaveEditor).GetMethod("SeekPreview", Fields).Invoke(editor, new object[] { 0f });
            var sessionField = typeof(DirectedEnemySubWaveEditor).GetField("shootingSession", Fields);
            var oldSession = (WaveShootingPreviewSession)sessionField.GetValue(editor);
            behaviour.InvalidateSequenceGuides();
            typeof(DirectedEnemySubWaveEditor).GetMethod("UpdatePreview", Fields).Invoke(editor, null);
            if (ReferenceEquals(oldSession, sessionField.GetValue(editor)) || oldSession.ProjectileWorld.SimulationWorld.IsCreated)
                throw new Exception("Inspector edit did not restart and clean up Preview");
            UnityEngine.Object.DestroyImmediate(editor); editor = null;
            var preset = ScriptableObject.CreateInstance<DirectedWaveAttackPreset>();
            try
            {
                var pattern = behaviour.AttackSequence.steps[0].steps[0].pattern;
                typeof(DirectedWaveAttackPattern).GetField("attackPreset", Fields).SetValue(pattern, preset);
                behaviour.InvalidateSequenceGuides();
                using var preview = new WaveShootingPreviewSession(wave, true, true, 0, Vector3.down * 4f);
                preset.SetAttackSettings(new DirectedWaveAttackSettings());
                if (preview.IsConfigurationCurrent) throw new Exception("Preset edit did not invalidate Sequence Preview");
            }
            finally { UnityEngine.Object.DestroyImmediate(preset); }
            if (World.All.Count != worlds) throw new Exception("Restarted preview leaked world");
            return new { passed = true, inspectorRendered = true, nestedSerialization = true, prefabOverrides = true,
                undoRedo = true, deepCopy = true, previewRestarts = 3, liveInspectorAndPresetRefresh = true, sceneAndAnimatedGuidesRendered = true, leakedWorlds = 0 };
        }
        finally
        {
            if (draw != null) SceneView.duringSceneGui -= draw;
            if (editor != null) UnityEngine.Object.DestroyImmediate(editor);
            Undo.RevertAllDownToGroup(undoGroup + 1);
            UnityEngine.Object.DestroyImmediate(root); EditorSceneManager.ClosePreviewScene(scene);
            EnemyShootingPreview.ShowFormationVolley = visible;
        }
    }
}
