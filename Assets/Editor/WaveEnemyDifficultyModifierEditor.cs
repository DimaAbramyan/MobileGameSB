using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(WaveEnemyDifficultyModifier))]
public sealed class WaveEnemyDifficultyModifierEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        WaveEnemyDifficultyModifier modifier =
            (WaveEnemyDifficultyModifier)target;
        List<LevelConfig> levels = FindReferencingLevels(modifier.gameObject);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Final Multipliers", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "For Repeat Burst attacks, fire rate increases Bursts Per Attack "
            + "with rounding up. Burst intervals remain unchanged.",
            MessageType.None);
        if (levels.Count == 0)
        {
            EditorGUILayout.HelpBox(
                "This wave is not assigned to a LevelConfig. At runtime, the "
                + "level multiplier will be 1 until the wave is loaded by a level.",
                MessageType.Info);
            DrawFormula("Hull health", modifier.HullHealthMultiplier, 1f);
            DrawFormula("Shield health", modifier.ShieldHealthMultiplier, 1f);
            DrawFormula("Damage", modifier.DamageMultiplier, 1f);
            DrawFormula("Fire rate", modifier.FireRateMultiplier, 1f);
            return;
        }

        for (int i = 0; i < levels.Count; i++)
        {
            LevelConfig level = levels[i];
            EditorGUILayout.LabelField(level.name, EditorStyles.miniBoldLabel);
            DrawFormula(
                "Hull health",
                modifier.HullHealthMultiplier,
                level.EnemyHullHealthMultiplier);
            DrawFormula(
                "Shield health",
                modifier.ShieldHealthMultiplier,
                level.EnemyShieldHealthMultiplier);
            DrawFormula(
                "Damage",
                modifier.DamageMultiplier,
                level.EnemyDamageMultiplier);
            DrawFormula(
                "Fire rate",
                modifier.FireRateMultiplier,
                level.EnemyFireRateMultiplier);
            EditorGUILayout.Space(2f);
        }
    }

    private static void DrawFormula(string label, float wave, float level)
    {
        EditorGUILayout.LabelField(
            label,
            $"{wave:0.###} (wave) × {level:0.###} (level) = {wave * level:0.###}");
    }

    private static List<LevelConfig> FindReferencingLevels(GameObject waveObject)
    {
        List<LevelConfig> levels = new();
        if (waveObject == null)
            return levels;

        string[] guids = AssetDatabase.FindAssets("t:LevelConfig");
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            LevelConfig level = AssetDatabase.LoadAssetAtPath<LevelConfig>(path);
            if (level == null || !ReferencesWave(level, waveObject))
                continue;

            levels.Add(level);
        }

        return levels;
    }

    private static bool ReferencesWave(LevelConfig level, GameObject waveObject)
    {
        IReadOnlyList<GameObject> waves = level.Waves;
        for (int i = 0; i < waves.Count; i++)
        {
            if (waves[i] == waveObject)
                return true;
        }

        return false;
    }
}
