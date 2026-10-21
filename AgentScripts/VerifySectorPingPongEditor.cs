using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Reflection;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class VerifySectorPingPongEditor
{
    public static async Task<object> Run()
    {
        var scene = EditorSceneManager.NewPreviewScene();
        var root = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefub/Enemies Prefab/Enemy.prefab"), scene);
        Editor editor = null; Action<SceneView> draw = null; var view = SceneView.lastActiveSceneView;
        try
        {
            var executor = root.GetComponent<SimpleEnemyAttackExecutor>(); using var serialized = new SerializedObject(executor);
            serialized.FindProperty("burstAttackSettings.attackPatterns").intValue = (int)EnemyAttackPatternsMode.Single;
            var layout = serialized.FindProperty("burstAttackSettings.shooting.emissionLayout");
            layout.FindPropertyRelative("type").intValue = (int)EnemyEmissionLayoutType.Sector;
            layout.FindPropertyRelative("launchMode").intValue = (int)EnemySectorLaunchMode.Sequential;
            layout.FindPropertyRelative("reverseOrder").boolValue = true;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Undo.IncrementCurrentGroup();
            layout.FindPropertyRelative("launchOrder").intValue = (int)EnemySectorLaunchOrder.PingPong;
            layout.FindPropertyRelative("reverseOrder").boolValue = false;
            layout.FindPropertyRelative("initialDirection").intValue = (int)EnemySectorInitialDirection.Reverse;
            layout.FindPropertyRelative("resetOrderOnAttackStart").boolValue = false;
            serialized.ApplyModifiedProperties(); Undo.FlushUndoRecordObjects();
            Undo.PerformUndo(); serialized.Update(); layout = serialized.FindProperty("burstAttackSettings.shooting.emissionLayout");
            if (!layout.FindPropertyRelative("reverseOrder").boolValue || layout.FindPropertyRelative("launchOrder").intValue == 2) throw new Exception("Undo failed");
            Undo.PerformRedo(); serialized.Update(); layout = serialized.FindProperty("burstAttackSettings.shooting.emissionLayout");
            if (layout.FindPropertyRelative("launchOrder").intValue != 2 || layout.FindPropertyRelative("resetOrderOnAttackStart").boolValue) throw new Exception("Redo failed");
            bool recorded = false; foreach (var change in PrefabUtility.GetPropertyModifications(root)) recorded |= change.propertyPath.EndsWith("launchOrder");
            if (!recorded) throw new Exception("Prefab order override not recorded");
            var method = typeof(EnemyEmissionLayoutSettingsDrawer).GetMethod("Fields", BindingFlags.Static | BindingFlags.NonPublic);
            HashSet<string> Fields() => new((IEnumerable<string>)method.Invoke(null, new object[] { layout }));
            foreach (string name in new[] { "launchOrder", "initialDirection", "resetOrderOnAttackStart" }) if (!Fields().Contains(name)) throw new Exception("Missing PingPong UI: " + name);
            layout.FindPropertyRelative("launchOrder").intValue = 0;
            if (Fields().Contains("initialDirection") || Fields().Contains("resetOrderOnAttackStart")) throw new Exception("PingPong options visible in Forward");
            layout.FindPropertyRelative("launchMode").intValue = 0;
            if (Fields().Contains("launchOrder")) throw new Exception("Order visible for Simultaneous");
            layout.FindPropertyRelative("type").intValue = (int)EnemyEmissionLayoutType.Circle;
            if (Fields().Contains("launchOrder")) throw new Exception("Order visible for Circle");
            layout.FindPropertyRelative("type").intValue = (int)EnemyEmissionLayoutType.Sector;
            layout.FindPropertyRelative("launchMode").intValue = 1; layout.FindPropertyRelative("launchOrder").intValue = 2;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            var json = EditorJsonUtility.ToJson(executor); EditorJsonUtility.FromJsonOverwrite(json, executor);
            if (executor.BurstAttackSettings.Shooting.EmissionLayout.InitialDirection != EnemySectorInitialDirection.Reverse) throw new Exception("Inspector settings did not serialize");
            editor = Editor.CreateEditor(executor); var rendered = new TaskCompletionSource<bool>(); int frames = 0;
            draw = current =>
            {
                if (current != view || Event.current.type != EventType.Layout && Event.current.type != EventType.Repaint) return;
                try
                {
                    Handles.BeginGUI(); GUILayout.BeginArea(new Rect(10, 20, 720, 6000)); editor.OnInspectorGUI(); GUILayout.EndArea(); Handles.EndGUI();
                    if (Event.current.type == EventType.Repaint && ++frames >= 2) rendered.TrySetResult(true);
                }
                catch (Exception error) { rendered.TrySetException(error); }
            };
            SceneView.duringSceneGui += draw; var timeout = Task.Delay(10000);
            while (!rendered.Task.IsCompleted && !timeout.IsCompleted) { view.Repaint(); await Task.Delay(50); }
            if (!rendered.Task.IsCompleted) throw new Exception("Inspector render timeout"); await rendered.Task;
            int before = CountWorlds();
            for (int i = 0; i < 3; i++)
            {
                using (var preview = new EnemyChildAttackScenePreview()) { preview.Simulate(executor, .3f, Vector3.up * 20); preview.Simulate(executor, 0, Vector3.up * 20); }
                if (CountWorlds() != before) throw new Exception("Preview world leak");
            }
            return new { passed = true, inspector = true, conditionalFields = true, undoRedo = true, prefabOverrides = true, serialization = true, previewRestartCleanup = true, compatibility = CheckWaves() };
        }
        finally
        {
            SceneView.duringSceneGui -= draw; if (editor != null) UnityEngine.Object.DestroyImmediate(editor);
            Undo.ClearUndo(root.GetComponent<SimpleEnemyAttackExecutor>()); UnityEngine.Object.DestroyImmediate(root); EditorSceneManager.ClosePreviewScene(scene);
        }
    }
    private static int CountWorlds() { int count = 0; foreach (var w in Unity.Entities.World.All) if (w.Name == "Scene shooting preview") count++; return count; }
    private static object CheckWaves()
    {
        string[] paths = { "Assets/Waves/1/1_Extreme/1_1_extrime.prefab", "Assets/Waves/1/1_Extreme/1_2_extrime.prefab", "Assets/Waves/1/1_Extreme/1_3_extrime.prefab", "Assets/Waves/1/1_Extreme/1_4_extrime.prefab", "Assets/Waves/2/2_1_extrime.prefab", "Assets/Waves/2/2_2_extrime.prefab", "Assets/Waves/2/2_3_extrime.prefab" };
        int subwaves = 0, slots = 0, patterns = 0;
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
                        if (wave.AttackPatternBehaviour == null || !wave.AttackPatternBehaviour.TryCopyResolvedAttackSettingsTo(i, false, settings)) continue;
                        var profile = settings.WaveBurstSettings;
                        for (int p = 0; p < profile.PatternCount; p++)
                        {
                            var layout = profile.GetPattern(p).Shooting.EmissionLayout; patterns++;
                            if (layout.UsesPingPong) throw new Exception("Old wave unexpectedly enabled PingPong");
                            bool legacy = (bool)typeof(EnemyEmissionLayoutSettings).GetField("reverseOrder", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(layout);
                            for (int order = 0; order < layout.EmissionCount; order++)
                                if (layout.GetLaunchIndex(order) != (legacy ? layout.EmissionCount - 1 - order : order)) throw new Exception("Legacy launch order changed");
                        }
                    }
                }
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            if (before != Convert.ToBase64String(sha.ComputeHash(File.ReadAllBytes(path)))) throw new Exception("Prefab modified: " + path);
        }
        return new { prefabs = paths.Length, subwaves, slots, patterns, noPrefabWrites = true, legacyOrderPreserved = true };
    }
}
