using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public static class VerifyPreviewPresentation
{
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
    public static async Task<object> Run()
    {
        var view = SceneView.lastActiveSceneView;
        if (view == null) throw new Exception("Scene View required");
        var pivot = view.pivot; var rotation = view.rotation; float size = view.size; bool ortho = view.orthographic;
        bool shown = EnemyShootingPreview.ShowFormationVolley;
        var scene = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
        var root = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Waves/2/2_1_extrime.prefab"));
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, scene);
        WaveShootingPreviewSession session = null;
        DirectedEnemySubWaveEditor editor = null;
        Texture2D pixels = null;
        Action<ScriptableRenderContext, Camera> read = null;
        EditorApplication.CallbackFunction tick = null;
        try
        {
            DirectedEnemySubWave wave = null;
            foreach (var candidate in root.GetComponentsInChildren<DirectedEnemySubWave>(true))
                if (candidate.name.Trim().EndsWith("right")) { wave = candidate; break; }
            if (wave == null) throw new Exception("Wave missing");
            EnemyShootingPreview.ShowFormationVolley = true;
            session = new WaveShootingPreviewSession(wave, true, false, 0, new Vector3(0, -3));
            while (!session.AdvanceTo(.6f, 4)) { }
            using (var query = session.ProjectileWorld.SimulationWorld.EntityManager.CreateEntityQuery(typeof(LocalTransform), typeof(EnemyProjectileMovement)))
            using (var poses = query.ToComponentDataArray<LocalTransform>(Allocator.Temp))
            {
                if (poses.Length == 0) throw new Exception("No projectile to inspect");
                view.orthographic = true;
                view.LookAtDirect(poses[0].Position, Quaternion.identity, .3f);
            }
            // Intentionally do not call Draw/OnSceneGUI: presentation must persist independently.
            session.ProjectileWorld.RegisterCamera(view.camera);
            session.SetVisible(false);
            pixels = new Texture2D(64, 64, TextureFormat.RGBA32, false);
            Color32[] baseline = null;
            var changedPixels = new List<int>();
            var hiddenPixels = new List<int>();
            int frames = 0;
            var rendered = new TaskCompletionSource<bool>();
            read = (context, camera) =>
            {
                if (camera != view.camera || rendered.Task.IsCompleted || camera.targetTexture == null) return;
                try
                {
                    var saved = RenderTexture.active;
                    try
                    {
                        var texture = camera.targetTexture;
                        RenderTexture.active = texture;
                        pixels.ReadPixels(new Rect(texture.width / 2 - 32, texture.height / 2 - 32, 64, 64), 0, 0);
                        pixels.Apply();
                    }
                    finally { RenderTexture.active = saved; }
                    var colors = pixels.GetPixels32();
                    if (++frames < 3) return;
                    if (baseline == null) { baseline = colors; session.SetVisible(true); return; }
                    int changed = 0;
                    for (int i = 0; i < colors.Length; i++)
                        if (Math.Abs(colors[i].r - baseline[i].r) + Math.Abs(colors[i].g - baseline[i].g)
                            + Math.Abs(colors[i].b - baseline[i].b) > 30) changed++;
                    if (changedPixels.Count < 20)
                    {
                        changedPixels.Add(changed);
                        if (changedPixels.Count == 20) session.SetVisible(false);
                    }
                    else
                    {
                        hiddenPixels.Add(changed);
                        if (hiddenPixels.Count == 5) rendered.TrySetResult(true);
                    }
                }
                catch (Exception error) { rendered.TrySetException(error); }
            };
            RenderPipelineManager.endCameraRendering += read;
            tick = () => view.Repaint();
            EditorApplication.update += tick;
            if (await Task.WhenAny(rendered.Task, Task.Delay(15000)) != rendered.Task) throw new Exception("Render timeout");
            await rendered.Task;
            EditorApplication.update -= tick; tick = null;
            RenderPipelineManager.endCameraRendering -= read; read = null;
            foreach (int count in changedPixels) if (count < 10) throw new Exception("Missing projectile pixels: " + string.Join(",", changedPixels));
            foreach (int count in hiddenPixels) if (count > 5) throw new Exception("Hidden projectiles remain visible: " + string.Join(",", hiddenPixels));
            session.Dispose(); session = null;

            editor = (DirectedEnemySubWaveEditor)Editor.CreateEditor(wave);
            var type = editor.GetType();
            type.GetField("previewFormationOnly", Flags).SetValue(editor, true);
            type.GetField("previewDuration", Flags).SetValue(editor, 10f);
            type.GetMethod("StartPreview", Flags).Invoke(editor, null);
            double start = EditorApplication.timeSinceStartup;
            await Task.Delay(3500);
            double wallSeconds = EditorApplication.timeSinceStartup - start;
            var running = (WaveShootingPreviewSession)type.GetField("shootingSession", Flags).GetValue(editor);
            float simulatedSeconds = running.Time;
            double lagSeconds = wallSeconds - simulatedSeconds;
            if (lagSeconds > .15) throw new Exception("Preview falls behind wall clock by " + lagSeconds + " seconds");
            return new { passed = true, renderedFrames = changedPixels.Count, changedPixels, hiddenPixels, wallSeconds, simulatedSeconds, lagSeconds };
        }
        finally
        {
            if (tick != null) EditorApplication.update -= tick;
            if (read != null) RenderPipelineManager.endCameraRendering -= read;
            if (editor != null) UnityEngine.Object.DestroyImmediate(editor);
            session?.Dispose();
            if (pixels != null) UnityEngine.Object.DestroyImmediate(pixels);
            UnityEngine.Object.DestroyImmediate(root);
            UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene);
            EnemyShootingPreview.ShowFormationVolley = shown;
            view.orthographic = ortho;
            view.LookAtDirect(pivot, rotation, size);
        }
    }
}
