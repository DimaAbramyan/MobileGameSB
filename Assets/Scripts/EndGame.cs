using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Zenject;

public class EndGame : MonoBehaviour
{
    [SerializeField] Text Points;
    [SerializeField] Text Bonuses;

    [InjectOptional] private LevelProgressService progressService;
    [InjectOptional] private PlayerResourceWallet resourceWallet;

    private LevelProgressService Progress =>
        progressService ??= new LevelProgressService();
    private PlayerResourceWallet Resources =>
        resourceWallet ??= new PlayerResourceWallet();

    // Start is called before the first frame update
    private void Awake()
    {
        LevelConfig selectedLevel = LevelLoader.SelectedLevelConfig;
        if (selectedLevel != null)
        {
            bool alreadyCompleted = Progress.IsLevelCompleted(selectedLevel);
            GrantLevelRewards(selectedLevel, alreadyCompleted);
            Progress.MarkLevelCompleted(selectedLevel);
            return;
        }

        Progress.MarkLevelCompleted(LevelLoader.LevelIndex);
    }
    void Start()
    {
        GetComponent<Text>().text += $"{LevelLoader.LevelIndex}";
        Points.text += PointsCollector.Points.ToString() + " / "+PointsCollector.MaxPoints.ToString();
        Bonuses.text += PointsCollector.Bonuses.ToString() + " / " + PointsCollector.MaxBonuses.ToString();
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    private void GrantLevelRewards(LevelConfig level, bool alreadyCompleted)
    {
        if (level == null)
            return;

        int metal = alreadyCompleted
            ? Mathf.FloorToInt(level.MetalReward * 0.2f)
            : level.MetalReward;
        int cores = alreadyCompleted ? 0 : level.CoreReward;

        Resources.Add(metal, cores);
    }
}
