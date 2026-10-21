using System;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class VerifyDiveMotionEditor
{
    public static async Task<object> Run()
    {
        var view = SceneView.lastActiveSceneView;
        if (view == null) throw new Exception("Scene View required");
        var scene = EditorSceneManager.NewPreviewScene();
        var root = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Waves/2/2_3_extrime.prefab"), scene);
        Editor editor = null; Action<SceneView> draw = null;
        try
        {
            var attack = root.transform.Find("Main").GetComponent<DirectedWaveAttackBehaviour>();
            Undo.IncrementCurrentGroup();
            using var serialized = new SerializedObject(attack);
            serialized.FindProperty("executionMode").enumValueIndex = 0;
            serialized.FindProperty("attackPatterns").arraySize = 0;
            serialized.FindProperty("attackPreset").objectReferenceValue = null;
            serialized.FindProperty("attackSettings.movementMode").enumValueIndex = (int)DirectedWaveAttackMovementMode.MoveToPlayer;
            serialized.FindProperty("attackSettings.minimumDiveDuration").floatValue = .5f;
            serialized.FindProperty("attackSettings.maximumDiveSpeed").floatValue = 3f;
            serialized.ApplyModifiedProperties(); Undo.FlushUndoRecordObjects();
            Undo.PerformUndo(); serialized.Update();
            if (serialized.FindProperty("attackSettings.minimumDiveDuration").floatValue != 0) throw new Exception("Undo failed");
            Undo.PerformRedo(); serialized.Update();
            if (serialized.FindProperty("attackSettings.maximumDiveSpeed").floatValue != 3) throw new Exception("Redo failed");
            bool recorded = false;
            foreach (var modification in PrefabUtility.GetPropertyModifications(root))
                recorded |= modification.propertyPath == "attackSettings.minimumDiveDuration" || modification.propertyPath == "attackSettings.maximumDiveSpeed";
            if (!recorded) throw new Exception("Prefab overrides missing");
            var json = EditorJsonUtility.ToJson(attack); EditorJsonUtility.FromJsonOverwrite(json, attack);
            editor = Editor.CreateEditor(attack); var rendered = new TaskCompletionSource<bool>(); int frames = 0;
            draw = current =>
            {
                if (current != view || Event.current.type != EventType.Layout && Event.current.type != EventType.Repaint) return;
                try
                {
                    Handles.BeginGUI(); GUILayout.BeginArea(new Rect(15, 30, 680, 2900));
                    editor.OnInspectorGUI(); GUILayout.EndArea(); Handles.EndGUI();
                    if (Event.current.type == EventType.Repaint && ++frames >= 2) rendered.TrySetResult(true);
                }
                catch (Exception exception) { rendered.TrySetException(exception); }
            };
            SceneView.duringSceneGui += draw; var timeout = Task.Delay(10000);
            while (!rendered.Task.IsCompleted && !timeout.IsCompleted) { view.Repaint(); await Task.Delay(50); }
            if (!rendered.Task.IsCompleted) throw new Exception("Inspector render timeout");
            await rendered.Task;
            return new { passed = true, inspector = true, undoRedo = true, prefabOverrides = true, serialization = true };
        }
        finally
        {
            SceneView.duringSceneGui -= draw;
            if (editor != null) UnityEngine.Object.DestroyImmediate(editor);
            Undo.ClearUndo(root); UnityEngine.Object.DestroyImmediate(root); EditorSceneManager.ClosePreviewScene(scene);
        }
    }
}
