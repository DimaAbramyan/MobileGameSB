using UnityEngine;
using UnityEngine.SceneManagement;
using Zenject;

public class LoadLevel : MonoBehaviour
{
    [SerializeField]
    private bool IsNextLevel;
    [SerializeField]
    private bool IsThatRepat;
    [SerializeField]
    private int m_Level = 0;

    [InjectOptional] private LevelCatalog levelCatalog;
    [InjectOptional] private LevelProgressService progressService;

    private LevelProgressService Progress =>
        progressService ??= new LevelProgressService();

    public void LoadScene()
    {
        if (IsThatRepat)
        {
            LoadCurrentLevelAgain();
            return;
        }

        if (IsNextLevel
            && SceneManager.GetActiveScene().name
                == LevelLoader.FightingSceneName)
        {
            LevelLoader.LevelIndex++;
            LevelLoader.SelectedLevelConfig = null;
            LoadFightingScene();
            return;
        }

        int targetScene = IsNextLevel
            ? SceneManager.GetActiveScene().buildIndex + 1
            : m_Level;

        if (targetScene >= LevelLoader.FirstFightingSceneBuildIndex)
        {
            LevelLoader.LevelIndex =
                LevelLoader.GetLevelIndex(targetScene);
            LevelLoader.SelectedLevelConfig = null;

            LevelConfig targetLevel =
                levelCatalog != null
                    ? levelCatalog.GetLevel(LevelLoader.LevelIndex)
                    : null;

            if (targetLevel != null && !Progress.CanStartLevel(targetLevel))
            {
                Debug.LogWarning(
                    $"Level {targetLevel.DisplayName} (ID: {targetLevel.Id}) is locked. "
                    + $"Complete required level {targetLevel.RequiredLevel?.DisplayName} first.",
                    this);
                return;
            }

            LoadFightingScene();
            return;
        }

        LoadSceneByBuildIndex(targetScene);
    }

    private static void LoadCurrentLevelAgain()
    {
        if (SceneManager.GetActiveScene().name
            == LevelLoader.FightingSceneName)
        {
            LoadFightingScene();
            return;
        }

        LoadSceneByBuildIndex(
            SceneManager.GetActiveScene().buildIndex);
    }

    private static void LoadFightingScene()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(LevelLoader.FightingSceneName);
    }

    private static void LoadSceneByBuildIndex(int buildIndex)
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(buildIndex);
    }
}
