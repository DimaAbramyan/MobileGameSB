using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Read the real battle scene in an isolated, non-rendering Editor preview scene.
// The menu camera must never define wave geometry or projectile screen boundaries.
[InitializeOnLoad]
internal static class WavePreviewGameplayCamera
{
    private static Scene previewScene;
    private static BattleCameraViewport viewport;
    private static Camera previewCamera;
    private static readonly List<Canvas> canvases = new();
    private static readonly List<CanvasScaler> scalers = new();
    private static readonly List<SafeArea> safeAreas = new();
    private static readonly List<BattleTopHudLayout> hudLayouts = new();
    private static Camera liveCamera;
    private static double nextLiveCheck;
    private static int width, height;
    private static WavePreviewDisplay.Sample display;
    private static bool loading;
    private static int revision;
    private static Camera trackedCamera;
    private static Matrix4x4 trackedProjection, trackedTransform;
    private static Rect trackedPixelRect;
    internal static int Revision
    {
        get
        {
            var camera = GameplayCamera;
            if (camera != null && (trackedCamera != camera || trackedProjection != camera.projectionMatrix
                || trackedTransform != camera.worldToCameraMatrix || trackedPixelRect != camera.rect))
            {
                trackedCamera = camera; trackedProjection = camera.projectionMatrix;
                trackedTransform = camera.worldToCameraMatrix; trackedPixelRect = camera.rect; revision++;
            }
            return revision;
        }
    }
    internal static string Error { get; private set; }
    internal static string ScenePath { get; private set; }

    static WavePreviewGameplayCamera()
    {
        AssemblyReloadEvents.beforeAssemblyReload += Release;
        EditorApplication.quitting += Release;
        EditorApplication.update += Update;
        EditorApplication.projectChanged += Release;
        EditorApplication.playModeStateChanged += state =>
        { if (state == PlayModeStateChange.ExitingEditMode) Release(); };
    }
    internal static Camera GameplayCamera
    {
        get
        {
            if (Application.isPlaying) return Camera.main;
            if (TryLiveCamera(out var live)) return live;
            if (loading || Error != null && previewCamera == null) return null;
            if (previewCamera == null && !Load()) return null;
            RefreshLayout();
            return width > 0 ? previewCamera : null;
        }
    }
    private static bool TryLiveCamera(out Camera camera)
    {
        camera = liveCamera;
        if (camera != null && camera.gameObject.scene.isLoaded) return true;
        if (EditorApplication.timeSinceStartup < nextLiveCheck) return false;
        nextLiveCheck = EditorApplication.timeSinceStartup + .2d;
        foreach (var candidate in UnityEngine.Object.FindObjectsByType<BattleCameraViewport>(FindObjectsSortMode.None))
        {
            if (EditorSceneManager.IsPreviewScene(candidate.gameObject.scene)) continue;
            using var serialized = new SerializedObject(candidate);
            camera = serialized.FindProperty("targetCamera").objectReferenceValue as Camera;
            if (camera != null) { liveCamera = camera; return true; }
        }
        return false;
    }
    private static bool Load()
    {
        loading = true;
        try
        {
            ScenePath = FindBattleScene();
            if (string.IsNullOrEmpty(ScenePath)) throw new InvalidOperationException("Battle scene is missing: " + LevelLoader.FightingSceneName);
            previewScene = EditorSceneManager.OpenPreviewScene(ScenePath);
            foreach (var root in previewScene.GetRootGameObjects())
            {
                foreach (var camera in root.GetComponentsInChildren<Camera>(true)) camera.enabled = false;
                foreach (var brain in root.GetComponentsInChildren<Unity.Cinemachine.CinemachineBrain>(true)) brain.enabled = false;
                foreach (var virtualCamera in root.GetComponentsInChildren<Unity.Cinemachine.CinemachineCamera>(true)) virtualCamera.enabled = false;
                foreach (var item in root.GetComponentsInChildren<BattleCameraViewport>(true))
                { item.enabled = false; if (viewport == null) viewport = item; }
                foreach (var layout in root.GetComponentsInChildren<BattleTopHudLayout>(true))
                { layout.enabled = false; hudLayouts.Add(layout); }
                foreach (var canvas in root.GetComponentsInChildren<Canvas>(true))
                { canvas.enabled = false; if (canvas.isRootCanvas) canvases.Add(canvas); }
                scalers.AddRange(root.GetComponentsInChildren<CanvasScaler>(true));
                safeAreas.AddRange(root.GetComponentsInChildren<SafeArea>(true));
            }
            if (viewport == null) throw new InvalidOperationException("Battle scene has no BattleCameraViewport.");
            using var serialized = new SerializedObject(viewport);
            previewCamera = serialized.FindProperty("targetCamera").objectReferenceValue as Camera;
            if (previewCamera == null) throw new InvalidOperationException("BattleCameraViewport has no Target Camera.");
            width = height = 0; Error = null;
            return true;
        }
        catch (Exception error)
        {
            Release(); Error = error.Message;
            Debug.LogWarning("Wave Preview gameplay camera: " + Error);
            return false;
        }
        finally { loading = false; }
    }
    private static string FindBattleScene()
    {
        foreach (var scene in EditorBuildSettings.scenes)
            if (Path.GetFileNameWithoutExtension(scene.path) == LevelLoader.FightingSceneName) return scene.path;
        foreach (string guid in AssetDatabase.FindAssets(LevelLoader.FightingSceneName + " t:Scene"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (Path.GetFileNameWithoutExtension(path) == LevelLoader.FightingSceneName) return path;
        }
        return null;
    }
    private static void Update()
    { if (previewCamera != null && !Application.isPlaying) RefreshLayout(); }

    private static void RefreshLayout()
    {
        WavePreviewDisplay.Sample current;
        try { current = WavePreviewDisplay.Read(); }
        catch (Exception error)
        {
            if (Error != error.Message) Debug.LogWarning("Wave Preview gameplay display: " + error.Message);
            Error = error.Message;
            return;
        }
        if (width > 0 && display.Equals(current)) return;
        display = current; Error = null;
        width = display.Width; height = display.Height;
        if (width <= 0 || height <= 0) return;
        // Overlay canvases in a preview scene have no rendering display, hence zero root rects.
        // Let the official CanvasScaler compute scale, then supply that display's pixel rect.
        // Child anchoring/layout and BattleCameraViewport remain the actual runtime implementation.
        foreach (var scaler in scalers) WavePreviewDisplay.ApplyScaler(scaler, display);
        foreach (var canvas in canvases)
        {
            var rect = (RectTransform)canvas.transform;
            float scale = Mathf.Max(.0001f, canvas.scaleFactor);
            rect.sizeDelta = new Vector2(width / scale, height / scale);
            rect.localScale = Vector3.one * scale;
            rect.position = new Vector3(width * rect.pivot.x, height * rect.pivot.y, 0);
        }
        foreach (var area in safeAreas) area.Apply(width, height, display.SafeArea);
        foreach (var canvas in canvases) LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)canvas.transform);
        Rect gameplayPixels = viewport.ApplyForDisplay(width, height, display.SafeArea);
        foreach (var layout in hudLayouts) layout.Apply(gameplayPixels);
        foreach (var canvas in canvases) LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)canvas.transform);
        viewport.ApplyForDisplay(width, height, display.SafeArea);
        // A disabled Editor camera otherwise picks up the Scene View render target's aspect
        // while drawing gizmos. Freeze it to the Game View's gameplay pixel rectangle.
        Rect gameplayRect = previewCamera.rect;
        previewCamera.aspect = width * gameplayRect.width / Mathf.Max(1f, height * gameplayRect.height);
    }
    internal static void Release()
    {
        previewCamera = liveCamera = trackedCamera = null; viewport = null; Error = null; revision++;
        canvases.Clear(); scalers.Clear(); safeAreas.Clear(); hudLayouts.Clear(); width = height = 0;
        if (previewScene.IsValid()) EditorSceneManager.ClosePreviewScene(previewScene);
        previewScene = default;
    }
}

