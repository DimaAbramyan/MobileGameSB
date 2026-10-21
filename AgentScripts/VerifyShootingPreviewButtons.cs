using System;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

public static class VerifyShootingPreviewButtons
{
    public static async Task<object> Run()
    {
        var view = SceneView.lastActiveSceneView;
        if (view == null || EditorApplication.isPlaying || EditorApplication.isCompiling) throw new Exception("Idle Scene View required.");
        var selected = Selection.activeObject;
        const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic;
        var controls = typeof(EnemyShootingPreview).GetMethod("DrawSceneControls", flags);
        var automaticDraw = (Action<SceneView>)Delegate.CreateDelegate(typeof(Action<SceneView>),
            typeof(EnemyShootingPreview).GetMethod("DrawSelectedFormationSlot", flags));
        bool savedVisibility = EnemyShootingPreview.ShowFormationVolley;
        string before = selected != null ? EditorJsonUtility.ToJson(selected) : null;
        var events = new System.Collections.Generic.List<string>();
        Vector2 clickPosition = default;
        Vector2 receivedProbe = default;
        bool probeReceived = false;
        Action<SceneView> trace = current =>
        {
            if (current != view) return;
            controls.Invoke(null, new object[] { view });
            Handles.BeginGUI();
            var rect = (Rect)typeof(EnemyShootingPreview).GetMethod("GetSceneControlsRect", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic).Invoke(null, new object[] { view });
            clickPosition = rect.center;
            if (Event.current.type == EventType.MouseMove) { receivedProbe = Event.current.mousePosition; probeReceived = true; }
            if (Event.current.type != EventType.Repaint && Event.current.type != EventType.Layout)
                events.Add($"{Event.current.type}: mouse={Event.current.mousePosition}, hot={GUIUtility.hotControl}, button={rect}");
            Handles.EndGUI();
        };
        try
        {
            SceneView.duringSceneGui -= automaticDraw;
            SceneView.duringSceneGui += trace;
            EnemyShootingPreview.ShowFormationVolley = true;
            await Repaint(view);
            Vector2 probePosition = new Vector2(200, 200);
            view.SendEvent(new Event { type = EventType.MouseMove, mousePosition = probePosition });
            await Task.Delay(30);
            if (!probeReceived) throw new Exception("Mouse probe did not reach Scene View.");
            Vector2 windowOffset = probePosition - receivedProbe;
            await Click(view, clickPosition + windowOffset);
            if (EnemyShootingPreview.ShowFormationVolley) throw new Exception("Scene button failed to hide Preview. " + string.Join("; ", events));
            await Repaint(view);
            await Click(view, clickPosition + windowOffset);
            if (!EnemyShootingPreview.ShowFormationVolley) throw new Exception("Scene button must remain clickable while Preview is hidden.");
            if (Selection.activeObject != selected || (selected != null && EditorJsonUtility.ToJson(selected) != before))
                throw new Exception("Visibility button changed selection or authored settings.");
            return new { passed = true, checks = 3, tested = "real mouse down/up on Scene View visibility button; hide/show including hidden state; no authored config changes" };
        }
        finally
        {
            SceneView.duringSceneGui -= trace;
            SceneView.duringSceneGui += automaticDraw;
            EnemyShootingPreview.ShowFormationVolley = savedVisibility;
        }
    }
    private static async Task Click(SceneView view, Vector2 position)
    {
        view.SendEvent(new Event { type = EventType.MouseDown, button = 0, mousePosition = position });
        await Task.Delay(30);
        view.SendEvent(new Event { type = EventType.MouseUp, button = 0, mousePosition = position });
        await Repaint(view);
    }
    private static async Task Repaint(SceneView view)
    {
        var done = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        Action<SceneView> handler = current =>
        {
            if (current == view && Event.current.type == EventType.Repaint) done.TrySetResult(true);
        };
        try
        {
            SceneView.duringSceneGui += handler;
            view.Repaint();
            if (await Task.WhenAny(done.Task, Task.Delay(5000)) != done.Task) throw new Exception("Scene View did not repaint.");
            await done.Task;
        }
        finally { SceneView.duringSceneGui -= handler; }
    }
}
