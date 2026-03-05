using UnityEngine;
[System.Serializable]
public class TeamSave : MonoBehaviour
{
    public static TeamSave Instance { get; private set; }

    public SaveData[] AllSavesThatLoaded;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }
}