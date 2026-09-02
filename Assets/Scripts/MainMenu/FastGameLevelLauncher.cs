using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Zenject;

public sealed class FastGameLevelLauncher : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private TMP_Text label;
    [SerializeField] private string labelFormat = "FastGame\nLevel ID: {0}";
    [SerializeField] private string unavailableLabel = "FastGame\nNo available level";

    [Header("Navigation")]
    [SerializeField] private NewMainMenuTabsController tabsController;
    [SerializeField, Min(0)] private int mapTabIndex = 1;
    [InjectOptional] private LevelCatalog levelCatalog;
    [InjectOptional] private LevelProgressService progressService;

    private void Awake()
    {
        if (button == null)
        {
            Debug.LogError(
                $"{nameof(FastGameLevelLauncher)} on {name} has no Button reference.",
                this);
            return;
        }

        button.onClick.AddListener(StartFastGame);
    }

    private void OnEnable()
    {
        Refresh();
    }

    private void OnDestroy()
    {
        if (button != null)
            button.onClick.RemoveListener(StartFastGame);
    }

    public void Refresh()
    {
        LevelConfig level = FindLastAvailableLevel();

        if (label != null)
        {
            label.text = level == null
                ? unavailableLabel
                : string.Format(labelFormat, level.Id);
        }

        if (button != null)
            button.interactable = level != null;
    }

    public void StartFastGame()
    {
        LevelConfig level = FindLastAvailableLevel();
        if (level == null)
        {
            Debug.LogWarning("FastGame cannot find an available level.", this);
            Refresh();
            return;
        }

        LoadLevelConfig levelLoader = FindLevelLoader(level);
        if (levelLoader == null)
        {
            Debug.LogError(
                $"FastGame could not find {nameof(LoadLevelConfig)} for "
                + $"{level.DisplayName} (ID: {level.Id}).",
                this);
            return;
        }

        if (tabsController == null)
        {
            Debug.LogError(
                $"{nameof(FastGameLevelLauncher)} on {name} has no "
                + $"{nameof(NewMainMenuTabsController)} reference.",
                this);
            return;
        }

        tabsController.NavigateToTab(mapTabIndex);
        levelLoader.Load();
    }

    private LevelConfig FindLastAvailableLevel()
    {
        LevelCatalog catalog = ResolveLevelCatalog();
        LevelProgressService progress = ResolveProgressService();
        if (catalog == null || progress == null)
            return null;

        LevelConfig lastAvailableLevel = null;
        for (int i = 0; i < catalog.Levels.Count; i++)
        {
            LevelConfig level = catalog.Levels[i];
            if (level == null || !progress.CanStartLevel(level))
                continue;

            if (lastAvailableLevel == null || level.Id > lastAvailableLevel.Id)
                lastAvailableLevel = level;
        }

        return lastAvailableLevel;
    }

    private LevelCatalog ResolveLevelCatalog()
    {
        if (levelCatalog != null)
            return levelCatalog;

        ProjectContext projectContext = ProjectContext.Instance;
        if (projectContext == null)
            return null;

        DiContainer container = projectContext.Container;
        if (container.HasBinding<LevelCatalog>())
            levelCatalog = container.Resolve<LevelCatalog>();

        return levelCatalog;
    }

    private LevelProgressService ResolveProgressService()
    {
        if (progressService != null)
            return progressService;

        ProjectContext projectContext = ProjectContext.Instance;
        if (projectContext == null)
            return null;

        DiContainer container = projectContext.Container;
        if (container.HasBinding<LevelProgressService>())
            progressService = container.Resolve<LevelProgressService>();

        return progressService;
    }

    private LoadLevelConfig FindLevelLoader(LevelConfig level)
    {
        LoadLevelConfig[] loaders =
            Resources.FindObjectsOfTypeAll<LoadLevelConfig>();
        for (int index = 0; index < loaders.Length; index++)
        {
            LoadLevelConfig loader = loaders[index];
            if (loader != null
                && loader.gameObject.scene == gameObject.scene
                && loader.LevelConfig == level)
            {
                return loader;
            }
        }

        return null;
    }
}
