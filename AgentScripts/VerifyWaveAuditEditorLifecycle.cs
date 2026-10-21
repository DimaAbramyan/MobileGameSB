using System;
using System.Collections;
using System.Reflection;
using Unity.Entities;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class VerifyWaveAuditEditorLifecycle
{
    private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    public static object Run()
    {
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Waves/2/2_3_extrime.prefab");
        bool visible = EnemyShootingPreview.ShowFormationVolley;
        int worlds = World.All.Count;
        var scene = EditorSceneManager.NewPreviewScene();
        var view = SceneView.lastActiveSceneView;
        Vector3 pivot = view != null ? view.pivot : default;
        Quaternion rotation = view != null ? view.rotation : default;
        float size = view != null ? view.size : 0f;
        GameObject root = null;
        Editor editor = null;
        int undoGroup = Undo.GetCurrentGroup();
        Undo.IncrementCurrentGroup();
        try
        {
            root = (GameObject)PrefabUtility.InstantiatePrefab(asset, scene);
            var wave = root.transform.Find("Main").GetComponent<DirectedEnemySubWave>();
            var point = wave.transform.GetChild(0);
            Vector3 before = point.position, after = before + Vector3.right * .123f;
            var move = typeof(DirectedEnemySubWaveEditor).GetMethod("SetFormationPointWorldPosition", BindingFlags.Static | BindingFlags.NonPublic);
            move.Invoke(null, new object[] { point, after, "Audit temporary point" });
            Undo.FlushUndoRecordObjects();
            bool recorded = false;
            var modifications = PrefabUtility.GetPropertyModifications(root);
            if (modifications != null)
                foreach (var modification in modifications)
                    if (modification.target is Transform && modification.propertyPath.StartsWith("m_LocalPosition")) recorded = true;
            Check(recorded, "Prefab transform modification not recorded");
            Undo.PerformUndo(); Check(Vector3.Distance(point.position, before) < .00001f, "Undo failed");
            Undo.PerformRedo(); Check(Vector3.Distance(point.position, after) < .00001f, "Redo failed");

            editor = Editor.CreateEditor(wave);
            Set(editor, "previewFormationOnly", true);
            Set(editor, "previewAllEnemies", true);
            Set(editor, "simulateShooting", true);
            for (int i = 0; i < 3; i++)
            {
                Call(editor, "StartPreview");
                Call(editor, "EnsureShootingSession");
                Check(Get(editor, "shootingSession") != null && World.All.Count == worlds + 1, "Preview did not create its world");
                Call(editor, "StopPreview");
                Check(Get(editor, "shootingSession") == null, "Stopped preview retained session");
            }
            UnityEngine.Object.DestroyImmediate(editor); editor = null;
            Check(World.All.Count == worlds, "Preview world leaked after stop/restart");

            editor = Editor.CreateEditor(root.GetComponent<Wave>());
            Set(editor, "showAttackPatterns", true);
            for (int i = 0; i < 3; i++)
            {
                Call(editor, "StartPreview");
                Check(Get(editor, "shootingPreview") != null && World.All.Count == worlds + 1, "Conductor preview did not create its world");
                Call(editor, "StopPreview");
                Check(Get(editor, "shootingPreview") == null && World.All.Count == worlds, "Conductor preview leaked after stop");
            }
            UnityEngine.Object.DestroyImmediate(editor); editor = null;

            // A persistent prefab parent has no valid scene; preview must use a valid fallback.
            var assetWave = asset.transform.Find("Main").GetComponent<DirectedEnemySubWave>();
            using (var preview = new WaveShootingPreviewSession(assetWave, true, true, 0, Vector3.down * 4f,
                previewParent: asset.transform)) Check(preview.EnemyCount == 38, "Asset preview omitted enemies");
            Check(World.All.Count == worlds, "Persistent asset preview leaked world");
            return new { passed = true, restartStopCycles = 6, prefabOverrides = true, undoRedo = true, leakedWorlds = 0 };
        }
        finally
        {
            if (editor != null) UnityEngine.Object.DestroyImmediate(editor);
            Undo.RevertAllDownToGroup(undoGroup + 1);
            if (root != null) UnityEngine.Object.DestroyImmediate(root);
            EditorSceneManager.ClosePreviewScene(scene);
            EnemyShootingPreview.ShowFormationVolley = visible;
            if (view != null) { view.LookAtDirect(pivot, rotation, size); view.Repaint(); }
        }
    }
    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    private static object Get(object value, string name) => value.GetType().GetField(name, Fields).GetValue(value);
    private static void Set(object value, string name, object field) => value.GetType().GetField(name, Fields)?.SetValue(value, field);
    private static void Call(object value, string name) => value.GetType().GetMethod(name, Fields).Invoke(value, null);
}
