using System;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

public sealed class BounceLimitInspectorHost : ScriptableObject
{
    public EnemyProjectileScreenSettings settings;
}
public sealed class BounceLimitInspectorWindow : EditorWindow
{
    public Action Draw;
    public Exception Error;
    public int Repaints;
    void OnGUI()
    {
        try { Draw?.Invoke(); } catch (Exception e) { Error = e; }
        if (Event.current.type == EventType.Repaint) Repaints++;
    }
}
public static class VerifyBounceLimitInspector
{
    public static async Task<object> Run()
    {
        int checks = 0;
        void Check(bool b, string message) { checks++; if (!b) throw new Exception(message); }
        var host = ScriptableObject.CreateInstance<BounceLimitInspectorHost>();
        host.hideFlags = HideFlags.HideAndDontSave;
        var window = ScriptableObject.CreateInstance<BounceLimitInspectorWindow>();
        using var so = new SerializedObject(host);
        int group = -1;
        try
        {
            window.position = new Rect(100, 100, 650, 240); window.ShowUtility();
            for (int i = 0; i < 4; i++)
            {
                host.settings = new EnemyProjectileScreenSettings(i != 3, bounce: i > 0, overrideBounceTime: i > 1);
                so.Update();
                var p = so.FindProperty("settings");
                float line = EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
                int expected = i == 0 ? 3 : i == 1 ? 5 : i == 2 ? 6 : 5;
                Check(Mathf.Abs(EditorGUI.GetPropertyHeight(p) - expected * line) < .01f, "Drawer conditional height " + i);
                string before = JsonUtility.ToJson(host);
                int start = window.Repaints;
                window.Draw = () => EditorGUI.PropertyField(new Rect(5, 5, 630, EditorGUI.GetPropertyHeight(p)), p);
                double until = EditorApplication.timeSinceStartup + 5;
                while (window.Repaints < start + 2 && EditorApplication.timeSinceStartup < until)
                { window.Repaint(); await Task.Delay(25); if (window.Error != null) throw window.Error; }
                Check(window.Repaints >= start + 2, "Real Layout/Repaint observed " + i);
                Check(JsonUtility.ToJson(host) == before, "Drawing does not mutate configuration " + i);
            }
            window.Draw = null;
            Undo.IncrementCurrentGroup(); group = Undo.GetCurrentGroup();
            so.Update();
            var count = so.FindProperty("settings.extraBounceCount"); count.intValue = 3;
            so.ApplyModifiedProperties(); Undo.FlushUndoRecordObjects();
            Check(host.settings.MaximumBounces == 4, "Serialized edit updates max bounces");
            Undo.PerformUndo(); so.Update(); Check(host.settings.MaximumBounces == 1, "Undo restores count");
            Undo.PerformRedo(); so.Update(); Check(host.settings.MaximumBounces == 4, "Redo restores count");
            return new { passed = true, checks, inspector = true, undoRedo = true };
        }
        finally
        {
            window.Draw = null; window.Close();
            if (group >= 0) Undo.RevertAllDownToGroup(group);
            UnityEngine.Object.DestroyImmediate(host);
        }
    }
}
