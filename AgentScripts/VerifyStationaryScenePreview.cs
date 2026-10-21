using System;
using System.Reflection;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

public sealed class StationaryGuiProbe : ScriptableObject { public EnemyStationaryFireSettings settings = new EnemyStationaryFireSettings(); }

public static class VerifyStationaryScenePreview
{
    public static async Task<object> Run()
    {
        if (EditorApplication.isPlaying || EditorApplication.isCompiling || EditorApplication.isUpdating) throw new Exception("Idle Editor required.");
        var view = SceneView.lastActiveSceneView;
        if (view == null) throw new Exception("Scene View required.");
        var root = PrefabUtility.LoadPrefabContents("Assets/Waves/1/1_Extreme/1_4_extrime.prefab");
        var probe = ScriptableObject.CreateInstance<StationaryGuiProbe>(); probe.hideFlags = HideFlags.HideAndDontSave;
        var waves = root.GetComponentsInChildren<DirectedEnemySubWave>(true);
        var editors = new UnityEditor.Editor[waves.Length];
        var done = new TaskCompletionSource<int>();
        int checks = 0;
        Action<SceneView> draw = current =>
        {
            if (current != view || Event.current.type != EventType.Repaint || done.Task.IsCompleted) return;
            try
            {
                foreach (var editor in editors)
                {
                    // Exercise the actual non-playing OnSceneGUI path, including static volley drawing.
                    editor.GetType().GetMethod("OnSceneGUI", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(editor, null);
                    checks++;
                }
                Handles.BeginGUI();
                try
                {
                    foreach (int mode in new[] { 0, 1, 2 })
                    {
                        if (mode == 1) probe.settings.ConfigureWorldPoint(new Vector2(3, -5));
                        else probe.settings.ConfigureAngle(120);
                        if (mode == 2) probe.settings.PreserveLegacyForward();
                        string before = JsonUtility.ToJson(probe.settings);
                        var property = new SerializedObject(probe).FindProperty("settings");
                        var drawer = new EnemyStationaryFireSettingsDrawer();
                        drawer.OnGUI(new Rect(-5000, 0, 500, drawer.GetPropertyHeight(property, GUIContent.none)), property, GUIContent.none);
                        if (JsonUtility.ToJson(probe.settings) != before) throw new Exception("Stationary drawing changed authored values.");
                        checks++;
                    }
                }
                finally { Handles.EndGUI(); }
                done.TrySetResult(checks);
            }
            catch (Exception error) { done.TrySetException(error); }
        };
        bool savedVisibility = EnemyShootingPreview.ShowFormationVolley;
        try
        {
            for (int i = 0; i < waves.Length; i++) editors[i] = UnityEditor.Editor.CreateEditor(waves[i]);
            EnemyShootingPreview.ShowFormationVolley = true;
            SceneView.duringSceneGui += draw; view.Repaint();
            if (await Task.WhenAny(done.Task, Task.Delay(5000)) != done.Task) throw new Exception("Scene GUI did not repaint in time.");
            checks = await done.Task;
            return new { passed = true, checks, waves = waves.Length, tested = "actual wave OnSceneGUI without Wave Preview; Stationary Angle/World Point/migrated Forward Inspector renders without changing settings" };
        }
        finally
        {
            SceneView.duringSceneGui -= draw;
            EnemyShootingPreview.ShowFormationVolley = savedVisibility;
            foreach (var editor in editors) if (editor != null) UnityEngine.Object.DestroyImmediate(editor);
            UnityEngine.Object.DestroyImmediate(probe); PrefabUtility.UnloadPrefabContents(root);
        }
    }
}
