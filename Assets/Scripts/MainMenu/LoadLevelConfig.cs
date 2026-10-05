using UnityEngine;
using UnityEngine.SceneManagement;
using Zenject;

public sealed class LoadLevelConfig : MonoBehaviour
{
    [SerializeField] private LevelDefinitionConfig levelDefinition;
    [SerializeField, HideInInspector] private LevelConfig levelConfig;
    [SerializeField] private GameObject lockedWarning;
    [SerializeField] private NewMainMenuLevelSelectionController mainMenuDetailsWindow;
    [SerializeField] private LevelSelectionDetailsWindow detailsWindow;

    [InjectOptional] private LevelProgressService progressService;
    [InjectOptional] private BattleLaunchService battleLaunchService;

    public LevelDefinitionConfig LevelDefinition => levelDefinition;
    public LevelConfig LevelConfig => levelDefinition != null
        ? levelDefinition.Normal
        : levelConfig;

    private LevelProgressService Progress =>
        progressService ??= new LevelProgressService();

    public bool CanLoad()
    {
        return CanLoad(LevelConfig);
    }

    public bool CanLoad(LevelConfig config)
    {
        return (levelDefinition != null
                ? levelDefinition.Contains(config)
                : config != null && config == levelConfig)
            && Progress.CanStartLevel(config);
    }

    public void Load()
    {
        if (TryShowDetailsWindow())
            return;

        StartLevel();
    }

    public void StartLevel()
    {
        StartLevel(LevelConfig);
    }

    public void StartLevel(LevelConfig config)
    {
        if (config == null)
        {
            Debug.LogError(
                $"{nameof(LoadLevelConfig)} on {name} has no LevelConfig.",
                this);
            return;
        }

        if (!CanLoad(config))
        {
            Debug.LogWarning(
                $"Level {config.DisplayName} (ID: {config.Id}) is locked. "
                + $"Complete required level {config.RequiredLevel?.DisplayName} first.",
                this);

            if (lockedWarning != null)
                lockedWarning.SetActive(true);

            return;
        }

        if (!TryPrepareBattle())
            return;

        LevelLoader.SelectLevel(config);
        Time.timeScale = 1f;
        Debug.Log($"Loading level {config.DisplayName} (ID: {config.Id})");
        SceneManager.LoadScene(LevelLoader.FightingSceneName);
    }

    private bool TryShowDetailsWindow()
    {
        LevelConfig config = LevelConfig;
        if (config == null)
            return false;

        if (mainMenuDetailsWindow == null)
            NewMainMenuLevelSelectionController.TryGetSceneController(
                out mainMenuDetailsWindow);

        if (mainMenuDetailsWindow != null)
        {
            mainMenuDetailsWindow.Show(config, this);
            return true;
        }

        if (detailsWindow == null)
            LevelSelectionDetailsWindow.TryGetSceneWindow(out detailsWindow);

        if (detailsWindow == null)
            return false;

        detailsWindow.Show(config, this);
        return true;
    }

    private bool TryPrepareBattle()
    {
        BattleLaunchService launchService = ResolveBattleLaunchService();
        if (launchService == null)
        {
            Debug.LogError(
                "Could not resolve BattleLaunchService from ProjectContext.",
                this);
            return false;
        }

        if (launchService.TryPrepareBattle(out string failureReason))
            return true;

        Debug.LogWarning(failureReason, this);
        return false;
    }

    private BattleLaunchService ResolveBattleLaunchService()
    {
        if (battleLaunchService != null)
            return battleLaunchService;

        ProjectContext projectContext = ProjectContext.Instance;
        if (projectContext == null)
            return null;

        DiContainer container = projectContext.Container;
        if (container.HasBinding<BattleLaunchService>())
            battleLaunchService = container.Resolve<BattleLaunchService>();

        return battleLaunchService;
    }
}
