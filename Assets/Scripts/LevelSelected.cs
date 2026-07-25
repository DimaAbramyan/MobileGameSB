public static class LevelLoader
{
    public const int FirstFightingSceneBuildIndex = 5;
    public const string FightingSceneName = "Fighting";

    public static string LevelName;
    public static int LevelIndex;
    public static LevelConfig SelectedLevelConfig;

    public static int GetLevelIndex(int sceneBuildIndex)
    {
        return System.Math.Max(0, sceneBuildIndex - FirstFightingSceneBuildIndex);
    }

    public static void SelectLevel(LevelConfig config)
    {
        SelectedLevelConfig = config;
        if (config == null)
            return;

        LevelIndex = config.Id;
        LevelName = config.DisplayName;
    }

    public static LevelConfig GetSelectedLevel(LevelCatalog catalog)
    {
        if (SelectedLevelConfig != null
            && SelectedLevelConfig.Id == LevelIndex)
        {
            return SelectedLevelConfig;
        }

        LevelConfig config = catalog != null
            ? catalog.GetLevel(LevelIndex)
            : null;
        SelectedLevelConfig = config;
        return config;
    }
}
