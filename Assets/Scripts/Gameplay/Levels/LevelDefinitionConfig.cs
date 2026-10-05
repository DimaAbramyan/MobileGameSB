using UnityEngine;

[CreateAssetMenu(fileName = "LevelDefinition", menuName = "Game/Levels/Level Definition")]
public sealed class LevelDefinitionConfig : ScriptableObject
{
    [SerializeField] private LevelConfig normal;
    [SerializeField] private LevelConfig hard;
    [SerializeField] private LevelConfig extreme;

    public int Id => normal != null ? normal.Id : -1;
    public string DisplayName => normal != null ? normal.DisplayName : name;
    public LevelConfig Normal => normal;
    public LevelConfig Hard => hard;
    public LevelConfig Extreme => extreme;

    public LevelConfig GetDifficulty(LevelDifficulty difficulty)
    {
        return difficulty switch
        {
            LevelDifficulty.Normal => normal,
            LevelDifficulty.Hard => hard,
            LevelDifficulty.Extreme => extreme,
            _ => null
        };
    }

    public bool Contains(LevelConfig config)
    {
        return config != null && (config == normal || config == hard || config == extreme);
    }
}
