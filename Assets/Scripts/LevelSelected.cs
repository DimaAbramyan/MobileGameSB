public static class LevelLoader
{
    public const int FirstFightingSceneBuildIndex = 5;

    public static string LevelName;
    public static int LevelIndex;

    public static int GetLevelIndex(int sceneBuildIndex)
    {
        return System.Math.Max(0, sceneBuildIndex - FirstFightingSceneBuildIndex);
    }
}
