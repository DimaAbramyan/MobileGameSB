using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static class CaptureReusablePresets
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private const string Folder = "Assets/Configs/AttackPresets/Examples/";
    private static void Set(object value, string field, object data) => value.GetType().GetField(field, Private).SetValue(value, data);
    private static object Call(object value, string method) => value.GetType().GetMethod(method, Private).Invoke(value, null);

    public static async Task<object> Run()
    {
        var projectile = AssetDatabase.LoadAssetAtPath<EnemyProjectilePreset>(Folder + "OrbitBoundary_Projectile.asset");
        var pattern = AssetDatabase.LoadAssetAtPath<EnemyAttackPatternPreset>(Folder + "SequentialSector_AttackPattern.asset");
        var window = ScriptableObject.CreateInstance<EnemyPresetLibraryWindow>();
        var scene = EditorSceneManager.NewPreviewScene();
        var root = new GameObject("Reusable presets verification camera"); SceneManager.MoveGameObjectToScene(root, scene);
        var camera = root.AddComponent<Camera>(); camera.enabled = false; camera.scene = scene;
        camera.orthographic = true; camera.orthographicSize = 5f; camera.aspect = 1f;
        camera.transform.position = new Vector3(0f, 0f, -10f);
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.025f, .035f, .065f);
        var texture = new RenderTexture(768, 768, 24); camera.targetTexture = texture;
        var image = new Texture2D(768, 768, TextureFormat.RGB24, false);
        var previous = RenderTexture.active;
        try
        {
            window.position = new Rect(80, 80, 1050, 760); window.ShowUtility();
            Set(window, "category", 1); Set(window, "previewProjectile", projectile);
            window.GetType().GetMethod("Select", Private).Invoke(window, new object[] { pattern });
            Call(window, "Restart"); window.Repaint();
            await Task.Delay(400);
            if (window.GetType().GetField("preview", Private).GetValue(window) == null)
                throw new Exception("Library did not create preview.");
            var rect = window.position;
            var screen = new Texture2D((int)rect.width, (int)rect.height, TextureFormat.RGB24, false);
            try
            {
                screen.SetPixels(UnityEditorInternal.InternalEditorUtility.ReadScreenPixel(new Vector2(rect.x, rect.y), screen.width, screen.height));
                screen.Apply(); File.WriteAllBytes("AgentArtifacts/projectile-presets-library.png", screen.EncodeToPNG());
            }
            finally { Object.DestroyImmediate(screen); }
            int[] counts = new int[2];
            for (int i = 0; i < 2; i++)
            {
                using var preview = new EnemyPresetPreviewSession(i == 0 ? null : pattern, projectile, null, 12345);
                preview.Advance(i == 0 ? .6f : 1.2f);
                if (preview.Warning != null || preview.Count == 0) throw new Exception(preview.Warning ?? "No projectile render state.");
                counts[i] = preview.Count;
                Action<ScriptableRenderContext, Camera> draw = (context, view) => { if (view == camera) preview.World.Draw(view); };
                RenderPipelineManager.beginCameraRendering += draw;
                try { camera.Render(); }
                finally { RenderPipelineManager.beginCameraRendering -= draw; }
                RenderTexture.active = texture; image.ReadPixels(new Rect(0, 0, 768, 768), 0, 0); image.Apply();
                int lit = 0; foreach (var pixel in image.GetPixels32()) if (pixel.r > 80 || pixel.g > 80 || pixel.b > 80) lit++;
                if (lit < 5) throw new Exception("Projectile preset preview was blank.");
                File.WriteAllBytes(i == 0 ? "AgentArtifacts/projectile-presets-projectile-preview.png" : "AgentArtifacts/projectile-presets-sector-preview.png", image.EncodeToPNG());
            }
            Set(window, "category", 0); window.GetType().GetMethod("Select", Private).Invoke(window, new object[] { projectile });
            window.Repaint(); await Task.Delay(200);
            return new { passed = true, libraryAndAssetEditors = true, counts, captures = 3 };
        }
        finally
        {
            window.Close(); Object.DestroyImmediate(window);
            RenderTexture.active = previous; camera.targetTexture = null; texture.Release();
            Object.DestroyImmediate(texture); Object.DestroyImmediate(image); Object.DestroyImmediate(root); EditorSceneManager.ClosePreviewScene(scene);
        }
    }
}
