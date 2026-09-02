using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(LevelConfig))]
public sealed class LevelConfigEditor : Editor
{
    private SerializedProperty waveMetalDrops;
    private readonly Dictionary<int, bool> subwaveCompositionExpanded = new();

    private void OnEnable()
    {
        waveMetalDrops = serializedObject.FindProperty("waveMetalDrops");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        DrawPropertiesExcluding(serializedObject, "m_Script", "waveMetalDrops");
        serializedObject.ApplyModifiedProperties();

        LevelConfig levelConfig = (LevelConfig)target;
        int waveCount = levelConfig.Waves?.Count ?? 0;
        if (waveMetalDrops == null || waveMetalDrops.arraySize != waveCount)
        {
            Undo.RecordObject(levelConfig, "Synchronize wave metal drops");
            levelConfig.EnsureWaveMetalDropSettings();
            EditorUtility.SetDirty(levelConfig);
            serializedObject.Update();
        }

        EditorGUILayout.Space();
        DrawLevelEnemySummary(levelConfig);
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Metal Drops Per Wave", EditorStyles.boldLabel);
        for (int i = 0; i < waveCount; i++)
        {
            GameObject wavePrefab = levelConfig.Waves[i];
            string waveName = wavePrefab != null ? wavePrefab.name : "Missing Wave";
            SerializedProperty settings = waveMetalDrops.GetArrayElementAtIndex(i);
            EditorGUILayout.PropertyField(
                settings,
                new GUIContent($"Wave {i + 1}: {waveName}"),
                includeChildren: true);

            DrawWaveEnemyComposition(levelConfig, i, wavePrefab);
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Metal Drop Debug", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            $"Drops: {levelConfig.MetalDropMinimum}–{levelConfig.MetalDropMaximum}\n"
            + $"Completion gold: {levelConfig.GoldReward}",
            MessageType.Info);

        if (levelConfig.MetalDropMaximum > 0
            && levelConfig.MetalPickupPrefab == null)
        {
            EditorGUILayout.HelpBox(
                "Assign Metal Pickup Prefab to enable physical metal drops.",
                MessageType.Error);
        }

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawWaveEnemyComposition(
        LevelConfig levelConfig,
        int waveIndex,
        GameObject wavePrefab)
    {
        if (wavePrefab == null)
            return;

        InfoAboutSubWave[] subWaves =
            wavePrefab.GetComponentsInChildren<InfoAboutSubWave>(true);
        if (subWaves.Length == 0)
        {
            EditorGUILayout.HelpBox(
                "No configured subwaves were found in this wave prefab.",
                MessageType.Warning);
            return;
        }

        Dictionary<Enemy, int> enemyCounts = new();
        CollectWaveEnemyCounts(wavePrefab, enemyCounts);

        EditorGUI.indentLevel++;
        DrawEnemySummary("Wave Enemy Summary", enemyCounts);

        int expansionKey = levelConfig.GetInstanceID() * 397 ^ waveIndex;
        bool expanded = subwaveCompositionExpanded.TryGetValue(
            expansionKey,
            out bool storedExpanded)
            && storedExpanded;
        bool nextExpanded = EditorGUILayout.Foldout(
            expanded,
            "Subwave composition",
            true);
        subwaveCompositionExpanded[expansionKey] = nextExpanded;
        if (!nextExpanded)
        {
            EditorGUI.indentLevel--;
            return;
        }

        EditorGUI.indentLevel++;
        for (int i = 0; i < subWaves.Length; i++)
            DrawSubWaveEnemyComposition(i, subWaves[i]);
        EditorGUI.indentLevel--;
        EditorGUI.indentLevel--;
    }

    private static void DrawLevelEnemySummary(LevelConfig levelConfig)
    {
        Dictionary<Enemy, int> enemyCounts = new();
        IReadOnlyList<GameObject> waves = levelConfig.Waves;
        if (waves != null)
        {
            for (int i = 0; i < waves.Count; i++)
                CollectWaveEnemyCounts(waves[i], enemyCounts);
        }

        DrawEnemySummary("Level Enemy Summary", enemyCounts);
    }

    private static void DrawEnemySummary(
        string title,
        Dictionary<Enemy, int> enemyCounts)
    {
        EditorGUILayout.LabelField(title, EditorStyles.boldLabel);

        int totalCount = 0;
        foreach (int count in enemyCounts.Values)
            totalCount += count;

        EditorGUILayout.LabelField(
            $"Total enemies: {totalCount}",
            EditorStyles.miniBoldLabel);

        if (enemyCounts.Count == 0)
        {
            EditorGUILayout.HelpBox(
                "No configured enemies were found.",
                MessageType.Info);
            return;
        }

        List<KeyValuePair<Enemy, int>> types = new(enemyCounts);
        types.Sort((left, right) => string.Compare(
            left.Key != null ? left.Key.name : "Missing Enemy",
            right.Key != null ? right.Key.name : "Missing Enemy",
            System.StringComparison.Ordinal));

        EditorGUI.indentLevel++;
        for (int i = 0; i < types.Count; i++)
        {
            KeyValuePair<Enemy, int> pair = types[i];
            string enemyName = pair.Key != null ? pair.Key.name : "Missing Enemy";
            string eligibility = pair.Key != null && pair.Key.CanContainBuff()
                ? string.Empty
                : " (no metal)";
            EditorGUILayout.LabelField(
                $"{enemyName}: {pair.Value}{eligibility}",
                EditorStyles.miniLabel);
        }
        EditorGUI.indentLevel--;
    }

    private static void CollectWaveEnemyCounts(
        GameObject wavePrefab,
        Dictionary<Enemy, int> enemyCounts)
    {
        if (wavePrefab == null)
            return;

        InfoAboutSubWave[] subWaves =
            wavePrefab.GetComponentsInChildren<InfoAboutSubWave>(true);
        for (int i = 0; i < subWaves.Length; i++)
            CollectEnemyCounts(subWaves[i], enemyCounts);
    }

    private static void DrawSubWaveEnemyComposition(
        int subWaveIndex,
        InfoAboutSubWave subWave)
    {
        Dictionary<Enemy, int> enemyCounts = new();
        CollectEnemyCounts(subWave, enemyCounts);

        string subWaveName = string.IsNullOrWhiteSpace(subWave.name)
            ? subWave.GetType().Name
            : subWave.name;

        if (enemyCounts.Count == 0)
        {
            EditorGUILayout.HelpBox(
                $"{subWaveIndex + 1}. {subWaveName}: enemy composition is unavailable.",
                MessageType.Warning);
            return;
        }

        int totalCount = 0;
        List<KeyValuePair<Enemy, int>> types = new(enemyCounts);
        foreach (KeyValuePair<Enemy, int> pair in enemyCounts)
            totalCount += pair.Value;

        types.Sort((left, right) => string.Compare(
            left.Key != null ? left.Key.name : "Missing Enemy",
            right.Key != null ? right.Key.name : "Missing Enemy",
            System.StringComparison.Ordinal));

        EditorGUILayout.LabelField(
            $"{subWaveIndex + 1}. {subWaveName}",
            EditorStyles.miniBoldLabel);
        EditorGUI.indentLevel++;
        EditorGUILayout.LabelField(
            $"Total enemies: {totalCount}",
            EditorStyles.miniLabel);
        EditorGUI.indentLevel++;
        for (int i = 0; i < types.Count; i++)
        {
            KeyValuePair<Enemy, int> pair = types[i];
            string enemyName = pair.Key != null ? pair.Key.name : "Missing Enemy";
            string eligibility = pair.Key != null && pair.Key.CanContainBuff()
                ? string.Empty
                : " (no metal)";
            EditorGUILayout.LabelField(
                $"{enemyName}: {pair.Value}{eligibility}",
                EditorStyles.wordWrappedMiniLabel);
        }
        EditorGUI.indentLevel--;
        EditorGUI.indentLevel--;
    }

    private static void CollectEnemyCounts(
        InfoAboutSubWave subWave,
        Dictionary<Enemy, int> enemyCounts)
    {
        if (subWave is DirectedEnemySubWave directedSubWave)
        {
            int slotCount = directedSubWave.GetConfiguredEnemySlotCount();
            for (int i = 0; i < slotCount; i++)
                AddEnemy(enemyCounts, directedSubWave.GetConfiguredEnemyPrefabForSlot(i));
            return;
        }

        if (subWave is TrajectoryEnemySubWave trajectorySubWave)
        {
            Enemy enemyPrefab = trajectorySubWave.GetConfiguredEnemyPrefab();
            int enemyCount = trajectorySubWave.GetRewardEligibleEnemyCount();
            for (int i = 0; i < enemyCount; i++)
                AddEnemy(enemyCounts, enemyPrefab);
        }
    }

    private static void AddEnemy(Dictionary<Enemy, int> enemyCounts, Enemy enemy)
    {
        if (enemy == null)
            return;

        enemyCounts.TryGetValue(enemy, out int count);
        enemyCounts[enemy] = count + 1;
    }
}
