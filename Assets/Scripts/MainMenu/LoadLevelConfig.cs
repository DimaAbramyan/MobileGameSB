using UnityEngine;
using UnityEngine.SceneManagement;
using Zenject;

public sealed class LoadLevelConfig : MonoBehaviour
{
    [SerializeField] private LevelConfig levelConfig;
    [SerializeField] private GameObject lockedWarning;
    [SerializeField] private LevelSelectionDetailsWindow detailsWindow;

    [InjectOptional] private LevelProgressService progressService;

    public LevelConfig LevelConfig => levelConfig;
    public int BattleSceneIndex => 5;

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

        LevelLoader.SelectLevel(levelConfig);
        Time.timeScale = 1f;
        Debug.Log($"Loading level {levelConfig.DisplayName} (ID: {levelConfig.Id})");
        SceneManager.LoadScene(5);
    }

    private bool TryShowDetailsWindow()
    {
        if (levelConfig == null)
            return false;

        if (detailsWindow == null)
            LevelSelectionDetailsWindow.TryGetSceneWindow(out detailsWindow);

        if (detailsWindow == null)
            return false;

        detailsWindow.Show(levelConfig, this);
        return true;
    }
}
