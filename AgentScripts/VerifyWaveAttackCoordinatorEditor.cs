using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Unity.Entities;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class VerifyWaveAttackCoordinatorEditor
{
    public static async Task<object> Run()
    {
        var view = SceneView.lastActiveSceneView;
        if (view == null) throw new Exception("Scene View required");
        int worlds = World.All.Count, undo = Undo.GetCurrentGroup();
        var scene = EditorSceneManager.NewPreviewScene();
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Waves/2/2_3_extrime.prefab");
        var root = (GameObject)PrefabUtility.InstantiatePrefab(asset, scene);
        Editor editor = null; Action<SceneView> draw = null;
        WaveConductorShootingPreview livePreview = null;
        bool visible = EnemyShootingPreview.ShowFormationVolley;
        try
        {
            Undo.IncrementCurrentGroup();
            var coordinator = Undo.AddComponent<WaveAttackCoordinator>(root);
            if (!PrefabUtility.IsAddedComponentOverride(coordinator)) throw new Exception("Added component override missing");
            Undo.IncrementCurrentGroup();
            using var serialized = new SerializedObject(coordinator);
            var groups = serialized.FindProperty("settings.groups"); groups.arraySize = 1;
            var group = groups.GetArrayElementAtIndex(0); group.FindPropertyRelative("groupName").stringValue = "Editor group";
            group.FindPropertyRelative("completionMode").enumValueIndex = (int)WaveAttackGroupCompletion.Timed;
            group.FindPropertyRelative("duration").floatValue = .2f;
            var members = group.FindPropertyRelative("controlledSubWaves"); members.arraySize = 1;
            members.GetArrayElementAtIndex(0).FindPropertyRelative("scheduleIndex").intValue = 0;
            serialized.ApplyModifiedProperties(); Undo.FlushUndoRecordObjects();
            if (coordinator.Settings.groups.Count != 1) throw new Exception("Serialized group did not apply");
            Undo.PerformUndo(); if (coordinator.Settings.groups.Count != 0) throw new Exception("Coordinator Undo failed");
            Undo.PerformRedo(); if (coordinator.Settings.groups[0].groupName != "Editor group") throw new Exception("Coordinator Redo failed");
            serialized.Update(); groups = serialized.FindProperty("settings.groups");
            groups.InsertArrayElementAtIndex(0); serialized.ApplyModifiedProperties();
            coordinator.Settings.groups[1].controlledSubWaves[0].scheduleIndex = 1;
            if (coordinator.Settings.groups[0].controlledSubWaves[0].scheduleIndex != 0) throw new Exception("Group copy shares nested mutable data");
            serialized.Update(); groups.MoveArrayElement(0, 1); serialized.ApplyModifiedProperties();
            if (coordinator.Settings.groups[0].controlledSubWaves[0].scheduleIndex != 1) throw new Exception("Group reordering failed");
            string json = EditorJsonUtility.ToJson(coordinator); EditorJsonUtility.FromJsonOverwrite(json, coordinator);
            if (coordinator.Settings.groups.Count != 2) throw new Exception("Coordinator round trip failed");
            coordinator.Settings.loopSequence = false;
            foreach (var item in coordinator.Settings.groups) { item.waitUntilReady = false; item.maxWaitDuration = .5f; }
            editor = Editor.CreateEditor(coordinator);
            var editorGroups = editor.serializedObject.FindProperty("settings.groups");
            editorGroups.GetArrayElementAtIndex(0).isExpanded = true;
            editorGroups.GetArrayElementAtIndex(0).FindPropertyRelative("controlledSubWaves").isExpanded = true;
            var rendered = new TaskCompletionSource<bool>();
            livePreview = new WaveConductorShootingPreview(root.GetComponent<Wave>(), Vector3.down * 4);
            while (!livePreview.AdvanceTo(.2f, 1000d)) { }
            int frames = 0;
            EnemyShootingPreview.ShowFormationVolley = true;
            draw = current =>
            {
                if (current != view || Event.current.type != EventType.Layout && Event.current.type != EventType.Repaint) return;
                try
                {
                    Handles.BeginGUI();
                    GUILayout.BeginArea(new Rect(20, 280, 620, 620));
                    editor.OnInspectorGUI();
                    GUILayout.EndArea(); Handles.EndGUI();
                    livePreview?.Draw(current);
                    if (Event.current.type != EventType.Repaint || ++frames < 2) return;
                    livePreview.Dispose(); livePreview = null;
                    for (int i = 0; i < 3; i++)
                    {
                        using (var preview = new WaveConductorShootingPreview(root.GetComponent<Wave>(), Vector3.down * 4))
                        {
                            while (!preview.AdvanceTo(.2f, 1000d)) { }
                            if (preview.CoordinatorPlayer == null) throw new Exception("Conductor has no coordinator Preview");
                        }
                    }
                    rendered.TrySetResult(true);
                }
                catch (Exception exception) { rendered.TrySetException(exception); }
            };
            SceneView.duringSceneGui += draw; view.Repaint();
            var timeout = Task.Delay(10000);
            while (!rendered.Task.IsCompleted && !timeout.IsCompleted) { view.Repaint(); await Task.Delay(50); }
            if (!rendered.Task.IsCompleted) throw new Exception("Inspector rendering timeout");
            await rendered.Task;
            if (World.All.Count != worlds) throw new Exception("Preview restart leaked ECS worlds");
            return new { passed = true, inspectorAndSceneView = true, undoRedo = true, prefabOverrides = true, duplicationAndReorder = true, threePreviewRestarts = true };
        }
        finally
        {
            SceneView.duringSceneGui -= draw;
            livePreview?.Dispose();
            EnemyShootingPreview.ShowFormationVolley = visible;
            if (editor != null) UnityEngine.Object.DestroyImmediate(editor);
            Undo.ClearUndo(root);
            UnityEngine.Object.DestroyImmediate(root); EditorSceneManager.ClosePreviewScene(scene);
            Undo.SetCurrentGroupName("Coordinator verification completed");
        }
    }
}
