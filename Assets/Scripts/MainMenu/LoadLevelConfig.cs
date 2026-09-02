using UnityEngine;
using UnityEngine.SceneManagement;
using Zenject;

public sealed class LoadLevelConfig : MonoBehaviour
{
    [SerializeField] private LevelConfig levelConfig;
    [SerializeField] private GameObject lockedWarning;
    [SerializeField] private NewMainMenuLevelSelectionController mainMenuDetailsWindow;
    [SerializeField] private LevelSelectionDetailsWindow detailsWindow;

    [InjectOptional] private LevelProgressService progressService;
    [InjectOptional] private BattleLaunchService battleLaunchService;

    public LevelConfig LevelConfig => levelConfig;

    private LevelProgressService Progress =>
        progressService ??= new LevelProgressService();

    public bool CanLoad()
    {
        return Progress.CanStartLevel(levelConfig);
    }

    public void Load()
    {
        if (TryShowDetailsWindow())
            return;

        StartLevel();
    }

    public void StartLevel()
    {
        if (levelConfig == null)
        {
            Debug.LogError(
                $"{nameof(LoadLevelConfig)} on {name} has no LevelConfig.",
                this);
            return;
        }

        if (!CanLoad())
        {
            Debug.LogWarning(
                $"Level {levelConfig.DisplayName} (ID: {levelConfig.Id}) is locked. "
                + $"Complete required level {levelConfig.RequiredLevel?.DisplayName} first.",
                this);

            if (lockedWarning != null)
                lockedWarning.SetActive(true);

            return;
        }

        if (!TryPrepareBattle())
            return;

        LevelLoader.SelectLevel(levelConfig);
        Time.timeScale = 1f;
        Debug.Log($"Loading level {levelConfig.DisplayName} (ID: {levelConfig.Id})");
        SceneManager.LoadScene(LevelLoader.FightingSceneName);
    }

    private bool TryShowDetailsWindow()
    {
        if (levelConfig == null)
            return false;

        if (mainMenuDetailsWindow == null)
            NewMainMenuLevelSelectionController.TryGetSceneController(
                out mainMenuDetailsWindow);

        if (mainMenuDetailsWindow != null)
        {
            mainMenuDetailsWindow.Show(levelConfig, this);
            return true;
        }

        if (detailsWindow == null)
            LevelSelectionDetailsWindow.TryGetSceneWindow(out detailsWindow);

        if (detailsWindow == null)
            return false;

        detailsWindow.Show(levelConfig, this);
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
