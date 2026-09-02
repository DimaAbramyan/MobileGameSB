using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class NewMainMenuLevelSelectionSetup
{
    [MenuItem("Tools/New Main Menu/Install SelectLevel")]
    public static void Install()
    {
        const string scenePath = "Assets/Scenes/NewMainMenu.unity";
        const string prefabPath = "Assets/Prefub/UI Prefab/SelectLevel.prefab";

        Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        if (FindExistingController(scene) != null)
        {
            Debug.Log("SelectLevel is already installed in NewMainMenu.");
            return;
        }

        Transform map = FindMapWindow();
        if (map == null)
            throw new System.InvalidOperationException("Could not find the Map window in NewMainMenu.");

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null)
            throw new System.InvalidOperationException("Could not load SelectLevel prefab.");

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, map);
        instance.name = prefab.name;
        instance.SetActive(false);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
    }

    private static NewMainMenuLevelSelectionController FindExistingController(Scene scene)
    {
        NewMainMenuLevelSelectionController[] controllers =
            Object.FindObjectsByType<NewMainMenuLevelSelectionController>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

        for (int index = 0; index < controllers.Length; index++)
        {
            NewMainMenuLevelSelectionController controller = controllers[index];
            if (controller.gameObject.scene == scene)
                return controller;
        }

        return null;
    }

    private static Transform FindMapWindow()
    {
        RectTransform[] candidates = Object.FindObjectsByType<RectTransform>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        for (int index = 0; index < candidates.Length; index++)
        {
            RectTransform candidate = candidates[index];
            if (candidate.name == "Map"
                && candidate.GetComponentInChildren<LoadLevelConfig>(true) != null)
            {
                return candidate;
            }
        }

        return null;
    }
}
