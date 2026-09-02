using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Zenject;

public sealed class LevelSelectionDetailsWindow : MonoBehaviour
{
    private static readonly List<LevelSelectionDetailsWindow> SceneWindows = new();

    [Header("Root")]
    [SerializeField] private GameObject root;

    [Header("Level info")]
    [SerializeField] private TMP_Text levelNumberText;
    [SerializeField] private TMP_Text levelNameText;
    [SerializeField] private TMP_Text lockedText;

    [Header("Rewards")]
    [SerializeField] private TMP_Text rewardText;
    [FormerlySerializedAs("metalRewardText")]
    [SerializeField] private TMP_Text goldRewardText;
    [SerializeField] private TMP_Text coreRewardText;
    [SerializeField] private string rewardFormat = "Награда: золото {0}, ядра {1}";
    [SerializeField] private string repeatRewardFormat =
        "Повторная награда: золото {0}, ядра {1}";
    [FormerlySerializedAs("metalRewardFormat")]
    [SerializeField] private string goldRewardFormat = "Золото: {0}";
    [SerializeField] private string coreRewardFormat = "Ядра: {0}";

    [Header("Buttons")]
    [SerializeField] private Button startButton;
    [SerializeField] private Button collapseButton;

    [Header("Team")]
    [SerializeField] private TeamPreviewPanel teamPreviewPanel;

    [InjectOptional] private LevelProgressService progressService;
    [InjectOptional] private BattleLaunchService battleLaunchService;

    private LevelConfig selectedLevel;
    private LoadLevelConfig selectedLoader;

    private LevelProgressService Progress =>
        progressService ??= new LevelProgressService();

    public static bool TryGetSceneWindow(out LevelSelectionDetailsWindow window)
    {
        SceneWindows.RemoveAll(item => item == null);
        window = SceneWindows.Count > 0 ? SceneWindows[0] : null;
        return window != null;
    }

    private void Awake()
    {
        if (root == null)
            root = gameObject;

        startButton?.onClick.AddListener(StartSelectedLevel);
        collapseButton?.onClick.AddListener(Hide);

        Hide();
    }

    private void OnEnable()
    {
        if (!SceneWindows.Contains(this))
            SceneWindows.Add(this);
    }

    private void OnDisable()
    {
        SceneWindows.Remove(this);
    }

    private void OnDestroy()
    {
        startButton?.onClick.RemoveListener(StartSelectedLevel);
        collapseButton?.onClick.RemoveListener(Hide);
        SceneWindows.Remove(this);
    }

    public void Show(LevelConfig level, LoadLevelConfig loader = null)
    {
        selectedLevel = level;
        selectedLoader = loader;

        if (root != null)
            root.SetActive(true);

        RefreshLevelInfo();
        teamPreviewPanel?.Refresh();
    }

    public void Hide()
    {
        if (root != null)
            root.SetActive(false);
    }

    public void StartSelectedLevel()
    {
        if (selectedLevel == null)
            return;

        if (!Progress.CanStartLevel(selectedLevel))
        {
            RefreshLockedState();
            return;
        }

        if (!TryPrepareBattle())
        {
            RefreshLockedState();
            return;
        }

        LevelLoader.SelectLevel(selectedLevel);
        Time.timeScale = 1f;
        Debug.Log(
            $"Loading level {selectedLevel.DisplayName} "
            + $"(ID: {selectedLevel.Id})");

        SceneManager.LoadScene(LevelLoader.FightingSceneName);
    }

    private void RefreshLevelInfo()
    {
        if (selectedLevel == null)
            return;

        if (levelNumberText != null)
            levelNumberText.text = $"Уровень {selectedLevel.Id}";

        if (levelNameText != null)
            levelNameText.text = string.IsNullOrWhiteSpace(selectedLevel.DisplayName)
                ? selectedLevel.name
                : selectedLevel.DisplayName;

        RefreshLockedState();
        RefreshRewards();
    }

    private void RefreshRewards()
    {
        if (selectedLevel == null)
            return;

        bool alreadyCompleted = Progress.IsLevelCompleted(selectedLevel);
        int goldReward = selectedLevel.GoldReward;
        int coreReward = alreadyCompleted ? 0 : selectedLevel.CoreReward;

        if (rewardText != null)
        {
            rewardText.text = string.Format(
                alreadyCompleted ? repeatRewardFormat : rewardFormat,
                goldReward,
                coreReward);
        }

        if (goldRewardText != null)
            goldRewardText.text = string.Format(goldRewardFormat, goldReward);

        if (coreRewardText != null)
            coreRewardText.text = string.Format(coreRewardFormat, coreReward);
    }

    private void RefreshLockedState()
    {
        bool levelIsAvailable = Progress.CanStartLevel(selectedLevel);
        BattleLaunchService launchService = ResolveBattleLaunchService();
        bool shipsAreSelected = launchService != null
            && launchService.HasCompleteShipSelection;
        bool canStart = levelIsAvailable && shipsAreSelected;

        if (startButton != null)
            startButton.interactable = canStart;

        if (lockedText == null)
            return;

        lockedText.gameObject.SetActive(!canStart);
        if (canStart)
            return;

        if (!levelIsAvailable)
        {
            string requiredName = selectedLevel != null
                && selectedLevel.RequiredLevel != null
                ? selectedLevel.RequiredLevel.DisplayName
                : "предыдущий уровень";

            lockedText.text = $"Закрыто. Сначала пройди: {requiredName}";
            return;
        }

        lockedText.text = "Выбери два корабля в главном меню.";
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
