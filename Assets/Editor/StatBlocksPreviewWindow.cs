using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class StatBlocksPreviewWindow : EditorWindow
{
    public const string PanelPath = "Assets/Prefub/UI/Stats/StatBlocksPanel.prefab";
    private ScriptableObject meta;
    private Scene scene;
    private Camera previewCamera;
    private RenderTexture texture;
    private StatBlocksView view;
    private int metaLevel;
    public static void Open(ScriptableObject source)
    {
        var window = GetWindow<StatBlocksPreviewWindow>("Stat Blocks Preview");
        window.meta = source; window.Refresh(); window.Show();
    }
    private void OnGUI()
    {
        meta = (ScriptableObject)EditorGUILayout.ObjectField("Meta", meta, typeof(ScriptableObject), false);
        int count = meta is WeaponMetaConfig weapon ? weapon.LevelCount : meta is ShipMetaConfig ship ? ship.LevelCount : 1;
        metaLevel = EditorGUILayout.IntSlider("Preview Meta Level", metaLevel, 0, Mathf.Max(0, count - 1));
        EditorGUILayout.LabelField("Base configuration preview. Runtime values require Bind(Weapon/ParentShip).", EditorStyles.miniLabel);
        if (GUILayout.Button("Refresh Preview")) Refresh();
        if (texture == null) { EditorGUILayout.HelpBox("Select a Meta and refresh. Create UI prefabs first if missing.", MessageType.Info); return; }
        var rect = GUILayoutUtility.GetRect(100, 10000, 100, 10000);
        if (Event.current.type == EventType.Repaint)
        {
            Canvas.ForceUpdateCanvases(); previewCamera.Render();
            EditorGUI.DrawPreviewTexture(rect, texture, null, ScaleMode.ScaleToFit);
        }
    }
    private void Refresh()
    {
        Cleanup();
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PanelPath);
        if (prefab == null || !(meta is IStatDisplaySource)) return;
        scene = EditorSceneManager.NewPreviewScene();
        var root = new GameObject("Stat UI Preview", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        SceneManager.MoveGameObjectToScene(root, scene);
        var cameraObject = new GameObject("Stat UI Camera"); SceneManager.MoveGameObjectToScene(cameraObject, scene);
        previewCamera = cameraObject.AddComponent<Camera>(); previewCamera.scene = scene;
        previewCamera.enabled = false; previewCamera.orthographic = true; previewCamera.orthographicSize = 5;
        previewCamera.transform.position = new Vector3(0, 0, -10);
        previewCamera.clearFlags = CameraClearFlags.SolidColor; previewCamera.backgroundColor = new Color(.035f, .045f, .065f);
        texture = new RenderTexture(1000, 700, 24) { hideFlags = HideFlags.HideAndDontSave }; texture.Create();
        previewCamera.targetTexture = texture;
        var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = previewCamera;
        var panel = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene); panel.transform.SetParent(root.transform, false);
        var rect = panel.GetComponent<RectTransform>(); rect.anchorMin = rect.anchorMax = new Vector2(.5f, 1);
        rect.pivot = new Vector2(.5f, 1); rect.anchoredPosition = new Vector2(0, -24); rect.sizeDelta = new Vector2(940, 0);
        view = panel.GetComponent<StatBlocksView>(); view.Preview(meta, metaLevel); Repaint();
    }
    private void OnDisable() => Cleanup();
    private void Cleanup()
    {
        if (scene.IsValid()) EditorSceneManager.ClosePreviewScene(scene);
        if (texture != null) { texture.Release(); DestroyImmediate(texture); }
        texture = null; previewCamera = null; view = null;
    }
}
