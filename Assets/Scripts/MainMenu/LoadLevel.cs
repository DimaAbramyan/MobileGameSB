using UnityEngine;
using UnityEngine.SceneManagement;

public class LoadLevel : MonoBehaviour
{
    [SerializeField]
    private bool IsNextLevel;
    [SerializeField]
    private bool IsThatRepat;
    [SerializeField]
    private int m_Level = 0;

    public void LoadScene()
    {
        int targetLevel;

        if (IsThatRepat)
            targetLevel = SceneManager.GetActiveScene().buildIndex;
        else if (IsNextLevel)
            targetLevel = SceneManager.GetActiveScene().buildIndex + 1;
        else
            targetLevel = m_Level;

        if (targetLevel >= LevelLoader.FirstFightingSceneBuildIndex)
            LevelLoader.LevelIndex = LevelLoader.GetLevelIndex(targetLevel);

        Time.timeScale = 1f;
        SceneManager.LoadScene(targetLevel);
    }
}
