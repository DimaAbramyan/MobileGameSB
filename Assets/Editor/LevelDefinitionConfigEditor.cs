using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(LevelDefinitionConfig))]
public sealed class LevelDefinitionConfigEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        var definition = (LevelDefinitionConfig)target;
        EditorGUILayout.Space();
        EditorGUILayout.HelpBox("Each difficulty has its own waves, rewards, background and enemy multipliers. Level identity comes from Normal.", MessageType.Info);
        DrawDifficulty(definition, LevelDifficulty.Normal);
        DrawDifficulty(definition, LevelDifficulty.Hard);
        DrawDifficulty(definition, LevelDifficulty.Extreme);
    }

    private static void DrawDifficulty(LevelDefinitionConfig definition, LevelDifficulty difficulty)
    {
        LevelConfig config = definition.GetDifficulty(difficulty);
        if (config == null)
        {
            EditorGUILayout.HelpBox($"Assign {difficulty} LevelConfig.", MessageType.Warning);
            return;
        }

        if (config.Id != definition.Id || config.Difficulty != difficulty)
            EditorGUILayout.HelpBox($"{difficulty}: use level ID {definition.Id} and difficulty {difficulty} in {config.name}.", MessageType.Error);

        if (config.Waves.Count == 0)
            EditorGUILayout.HelpBox($"{difficulty}: no waves configured.", MessageType.Warning);

        EditorGUILayout.LabelField(difficulty.ToString(), EditorStyles.boldLabel);
        EditorGUILayout.LabelField($"Waves: {config.Waves.Count} | Gold: {config.GoldReward} | Chips: {config.CoreReward}");
        if (GUILayout.Button($"Edit {difficulty}"))
            Selection.activeObject = config;
    }
}
