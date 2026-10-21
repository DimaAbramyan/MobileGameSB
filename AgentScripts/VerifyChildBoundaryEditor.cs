using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Reflection;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class VerifyChildBoundaryEditor
{
    public static async Task<object> Run()
    {
        var scene = EditorSceneManager.NewPreviewScene();
        var root = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefub/Enemies Prefab/Enemy.prefab"), scene);
        Editor editor = null; Action<SceneView> draw = null;
        var view = SceneView.lastActiveSceneView;
        if (view == null) throw new Exception("Scene View required");
        try
        {
            var executor = root.GetComponent<SimpleEnemyAttackExecutor>();
            using var serialized = new SerializedObject(executor);
            serialized.FindProperty("burstAttackSettings.attackPatterns").intValue = (int)EnemyAttackPatternsMode.Single;
            serialized.FindProperty("burstAttackSettings.shooting.useChildAttackPattern").boolValue = true;
            var child = serialized.FindProperty("burstAttackSettings.shooting.childAttack");
            child.managedReferenceValue = new EnemyChildAttackSettings();
            child.FindPropertyRelative("triggerMode").intValue = (int)EnemyChildAttackTriggerMode.OnEvent;
            child.FindPropertyRelative("eventType").intValue = (int)EnemyChildAttackEvent.OnScreenBoundaryReached;
            child.FindPropertyRelative("boundary").intValue = (int)EnemyChildAttackBoundary.Left;
            child.FindPropertyRelative("boundaryOffset").floatValue = -.75f;
            child.FindPropertyRelative("timeOffset").floatValue = .5f;
            child.FindPropertyRelative("triggerOnce").boolValue = false;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Undo.IncrementCurrentGroup(); Undo.RecordObject(executor, "Verify boundary event Undo");
            child = serialized.FindProperty("burstAttackSettings.shooting.childAttack");
            child.FindPropertyRelative("boundaryOffset").floatValue = 2;
            serialized.ApplyModifiedProperties(); Undo.FlushUndoRecordObjects();
            Undo.PerformUndo(); serialized.Update(); child = serialized.FindProperty("burstAttackSettings.shooting.childAttack");
            if (child.FindPropertyRelative("boundaryOffset").floatValue != -.75f) throw new Exception("Undo failed");
            Undo.PerformRedo(); serialized.Update(); child = serialized.FindProperty("burstAttackSettings.shooting.childAttack");
            if (child.FindPropertyRelative("boundaryOffset").floatValue != 2) throw new Exception("Redo failed");
            bool overridden = false;
            foreach (var change in PrefabUtility.GetPropertyModifications(root)) overridden |= change.propertyPath.Contains("childAttack");
            if (!overridden) throw new Exception("Prefab overrides missing");
            var fieldsMethod = typeof(EnemyChildAttackSettingsDrawer).GetMethod("Fields", BindingFlags.NonPublic | BindingFlags.Static);
            var fields = (IEnumerable<string>)fieldsMethod.Invoke(null, new object[] { child });
            var visible = new HashSet<string>(fields);
            foreach (var name in new[] { "boundary", "boundaryOffset", "triggerOnce", "timeOffset" }) if (!visible.Contains(name)) throw new Exception("Hidden event field: " + name);
            child.FindPropertyRelative("eventType").intValue = (int)EnemyChildAttackEvent.OnSpawn;
            visible = new HashSet<string>((IEnumerable<string>)fieldsMethod.Invoke(null, new object[] { child }));
            if (visible.Contains("boundary") || visible.Contains("timeOffset")) throw new Exception("Boundary settings shown for other event");
            child.FindPropertyRelative("eventType").intValue = (int)EnemyChildAttackEvent.OnScreenBoundaryReached;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            editor = Editor.CreateEditor(executor);
            var rendered = new TaskCompletionSource<bool>(); int frames = 0;
            draw = current =>
            {
                if (current != view || Event.current.type != EventType.Layout && Event.current.type != EventType.Repaint) return;
                try
                {
                    Handles.BeginGUI(); GUILayout.BeginArea(new Rect(10, 20, 700, 6000));
                    editor.OnInspectorGUI(); GUILayout.EndArea(); Handles.EndGUI();
                    if (Event.current.type == EventType.Repaint && ++frames >= 2) rendered.TrySetResult(true);
                }
                catch (Exception error) { rendered.TrySetException(error); }
            };
            SceneView.duringSceneGui += draw; var timeout = Task.Delay(10000);
            while (!rendered.Task.IsCompleted && !timeout.IsCompleted) { view.Repaint(); await Task.Delay(50); }
            if (!rendered.Task.IsCompleted) throw new Exception("Inspector render timeout");
            await rendered.Task;
            int worlds = CountWorlds();
            for (int i = 0; i < 3; i++)
            {
                using (var preview = new EnemyChildAttackScenePreview()) { preview.Simulate(executor, .4f, Vector3.up * 100); preview.Simulate(executor, 0, Vector3.up * 100); }
                if (CountWorlds() != worlds) throw new Exception("Preview world leak");
            }
            return new { passed = true, inspectorRendered = true, conditionalFields = true, undoRedo = true, prefabOverrides = true, previewRestartAndCleanup = true, compatibility = CheckWaves() };
        }
        finally
        {
            SceneView.duringSceneGui -= draw;
            if (editor != null) UnityEngine.Object.DestroyImmediate(editor);
            Undo.ClearUndo(root.GetComponent<SimpleEnemyAttackExecutor>());
            UnityEngine.Object.DestroyImmediate(root); EditorSceneManager.ClosePreviewScene(scene);
        }
    }
    private static int CountWorlds() { int count = 0; foreach (var world in Unity.Entities.World.All) if (world.Name == "Scene shooting preview") count++; return count; }
    private static object CheckWaves()
    {
        string[] paths = { "Assets/Waves/1/1_Extreme/1_1_extrime.prefab", "Assets/Waves/1/1_Extreme/1_2_extrime.prefab",
            "Assets/Waves/1/1_Extreme/1_3_extrime.prefab", "Assets/Waves/1/1_Extreme/1_4_extrime.prefab",
            "Assets/Waves/2/2_1_extrime.prefab", "Assets/Waves/2/2_2_extrime.prefab", "Assets/Waves/2/2_3_extrime.prefab" };
        int subwaves = 0, slots = 0;
        foreach (string path in paths)
        {
            using var sha = SHA256.Create(); string before = Convert.ToBase64String(sha.ComputeHash(File.ReadAllBytes(path)));
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                foreach (var t in root.GetComponentsInChildren<Transform>(true)) if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject) > 0) throw new Exception("Missing script: " + path);
                foreach (var wave in root.GetComponentsInChildren<DirectedEnemySubWave>(true))
                {
                    subwaves++; int count = wave.GetConfiguredEnemySlotCount(); slots += count;
                    for (int i = 0; i < count; i++)
                    {
                        var settings = new DirectedWaveAttackSettings();
                        if (wave.AttackPatternBehaviour != null && wave.AttackPatternBehaviour.TryCopyResolvedAttackSettingsTo(i, false, settings))
                            CheckProfile(settings.WaveBurstSettings);
                    }
                }
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            if (before != Convert.ToBase64String(sha.ComputeHash(File.ReadAllBytes(path)))) throw new Exception("Prefab changed: " + path);
        }
        return new { prefabs = paths.Length, subwaves, slots, missingScripts = 0, noPrefabWrites = true };
    }
    private static void CheckProfile(EnemyBurstAttackSettings profile)
    {
        for (int i = 0; i < profile.PatternCount; i++)
        {
            var child = profile.GetPattern(i).Shooting.ChildAttack;
            for (int depth = 0; child != null && depth < EnemyChildAttackSettings.MaximumDepth; depth++, child = child.Pattern.Shooting.ChildAttack)
                if (child.UsesScreenBoundary) throw new Exception("Old prefab enabled the new event unexpectedly");
        }
    }
}
