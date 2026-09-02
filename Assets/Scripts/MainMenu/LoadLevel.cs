using UnityEngine;
using UnityEngine.SceneManagement;
using Zenject;

public class LoadLevel : MonoBehaviour
{
    [SerializeField]
    private bool IsNextLevel;
    [SerializeField]
    private bool IsThatRepat;

    [InjectOptional] private AudioVolumeService audioVolumeService;
    public void LoadScene()
    {
        if (IsThatRepat)
        {
            LoadCurrentLevelAgain();
            return;
        }

        if (IsNextLevel)
        {
            LevelLoader.LevelIndex++;
            LevelLoader.SelectedLevelConfig = null;
            LoadFightingScene();
            return;
        }

        LoadMapScene();
    }

    private static void LoadCurrentLevelAgain()
    {
        LoadFightingScene();
    }

    private static void LoadFightingScene()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(LevelLoader.FightingSceneName);
    }

    private void LoadMapScene()
    {
        StopBattleAudio();
        LevelLoader.RequestMapOnMainMenuLoad();
        Time.timeScale = 1f;
        SceneManager.LoadScene(LevelLoader.MainMenuSceneName);
    }

    private void StopBattleAudio()
    {
        if (audioVolumeService == null)
        {
            ProjectContext projectContext = ProjectContext.Instance;
            if (projectContext != null
                && projectContext.Container.HasBinding<AudioVolumeService>())
            {
                audioVolumeService = projectContext.Container
                    .Resolve<AudioVolumeService>();
            }
        }

        if (audioVolumeService == null)
        {
            Debug.LogError(
                "Could not resolve AudioVolumeService while leaving the battle.",
                this);
            return;
        }

        audioVolumeService.StopAllPlayback();
    }
}
