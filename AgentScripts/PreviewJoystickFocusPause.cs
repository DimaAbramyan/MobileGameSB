using System;
using System.IO;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class PreviewJoystickFocusPause
{
    public static object Run()
    {
        GameObject source = null;
        foreach (var root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
            if (root.name == "Pause") source = root;
        if (source == null) throw new Exception("Live pause missing.");
        var scene = EditorSceneManager.NewPreviewScene();
        RenderTexture target = null;
        Texture2D image = null;
        var previous = RenderTexture.active;
        try
        {
            var cameraObject = new GameObject("Pause preview camera", typeof(Camera));
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(cameraObject, scene);
            var camera = cameraObject.GetComponent<Camera>();
            camera.enabled = false;
            camera.orthographic = true;
            camera.orthographicSize = 10;
            camera.transform.position = new Vector3(1000, 1000, -100);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.09f, 0.12f, 0.18f);
            camera.cullingMask = 1 << 31;
            camera.overrideSceneCullingMask = EditorSceneManager.GetSceneCullingMask(scene);
            target = new RenderTexture(1080, 2340, 24);
            target.Create();
            camera.targetTexture = target;
            var instance = UnityEngine.Object.Instantiate(source);
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(instance, scene);
            foreach (var t in instance.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 31;
            var canvas = instance.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 10;
            instance.SetActive(true);
            Canvas.ForceUpdateCanvases();
            foreach (var text in instance.GetComponentsInChildren<TMPro.TMP_Text>(true)) text.ForceMeshUpdate();
            camera.Render();
            RenderTexture.active = target;
            image = new Texture2D(1080, 2340, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, 1080, 2340), 0, 0);
            image.Apply();
            string path = Path.GetFullPath("AgentArtifacts/PauseFocusPreview.png");
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, image.EncodeToPNG());
            return new {path};
        }
        finally
        {
            RenderTexture.active = previous;
            EditorSceneManager.ClosePreviewScene(scene);
            if (target != null) {target.Release(); UnityEngine.Object.DestroyImmediate(target);}
            if (image != null) UnityEngine.Object.DestroyImmediate(image);
        }
    }
}
