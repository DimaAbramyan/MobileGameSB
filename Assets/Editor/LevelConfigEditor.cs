using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(LevelConfig))]
public sealed class LevelConfigEditor : Editor
{
    private readonly struct EnemyCount
    {
        public EnemyCount(Enemy enemy, int count)
        {
            Enemy = enemy;
            Count = count;
        }

        public Enemy Enemy { get; }
        public int Count { get; }
    }

    private sealed class EnemyComposition
    {
        public EnemyComposition(List<EnemyCount> enemies, int totalCount)
        {
            Enemies = enemies;
            TotalCount = totalCount;
        }

        public List<EnemyCount> Enemies { get; }
        public int TotalCount { get; }
    }

    private sealed class SubWaveEnemyComposition
    {
        public SubWaveEnemyComposition(string name, EnemyComposition composition)
        {
            Name = name;
            Composition = composition;
        }

        public string Name { get; }
        public EnemyComposition Composition { get; }
    }

    private sealed class WaveEnemyComposition
    {
        public WaveEnemyComposition(
            EnemyComposition composition,
            List<SubWaveEnemyComposition> subWaves)
        {
            Composition = composition;
            SubWaves = subWaves;
        }

        public EnemyComposition Composition { get; }
        public List<SubWaveEnemyComposition> SubWaves { get; }
    }

    private SerializedProperty waveMetalDrops;
    private readonly Dictionary<int, bool> subwaveCompositionExpanded = new();
    private readonly Dictionary<int, WaveEnemyComposition> waveCompositionCache = new();
    private EnemyComposition levelCompositionCache;

    private void OnEnable()
    {
        waveMetalDrops = serializedObject.FindProperty("waveMetalDrops");
        EditorApplication.projectChanged += InvalidateEnemyCompositionCache;
        Undo.undoRedoPerformed += InvalidateEnemyCompositionCache;
    }

    private void OnDisable()
    {
        EditorApplication.projectChanged -= InvalidateEnemyCompositionCache;
        Undo.undoRedoPerformed -= InvalidateEnemyCompositionCache;
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        EditorGUI.BeginChangeCheck();
        DrawPropertiesExcluding(serializedObject, "m_Script", "waveMetalDrops");
        bool propertiesChanged = EditorGUI.EndChangeCheck();
        if (serializedObject.ApplyModifiedProperties() || propertiesChanged)
            InvalidateEnemyCompositionCache();

        LevelConfig levelConfig = (LevelConfig)target;
        int waveCount = levelConfig.Waves?.Count ?? 0;
        if (waveMetalDrops == null || waveMetalDrops.arraySize != waveCount)
        {
            Undo.RecordObject(levelConfig, "Synchronize wave metal drops");
            levelConfig.EnsureWaveMetalDropSettings();
            EditorUtility.SetDirty(levelConfig);
            serializedObject.Update();
            InvalidateEnemyCompositionCache();
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

        WaveEnemyComposition composition = GetWaveEnemyComposition(wavePrefab);
        if (composition.SubWaves.Count == 0)
        {
            EditorGUILayout.HelpBox(
                "No configured subwaves were found in this wave prefab.",
                MessageType.Warning);
            return;
        }

        EditorGUI.indentLevel++;
        DrawEnemySummary("Wave Enemy Summary", composition.Composition);

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
        for (int i = 0; i < composition.SubWaves.Count; i++)
            DrawSubWaveEnemyComposition(i, composition.SubWaves[i]);
        EditorGUI.indentLevel--;
        EditorGUI.indentLevel--;
    }

    private void DrawLevelEnemySummary(LevelConfig levelConfig)
    {
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Level Enemy Summary", EditorStyles.boldLabel);
        if (GUILayout.Button("Refresh", GUILayout.Width(70f)))
            InvalidateEnemyCompositionCache();
        EditorGUILayout.EndHorizontal();

        DrawEnemySummaryContents(GetLevelEnemyComposition(levelConfig));
    }

    private static void DrawEnemySummary(
        string title,
        EnemyComposition composition)
    {
        EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
        DrawEnemySummaryContents(composition);
    }

    private static void DrawEnemySummaryContents(EnemyComposition composition)
    {
        EditorGUILayout.LabelField(
            $"Total enemies: {composition.TotalCount}",
            EditorStyles.miniBoldLabel);

        if (composition.Enemies.Count == 0)
        {
            EditorGUILayout.HelpBox(
                "No configured enemies were found.",
                MessageType.Info);
            return;
        }

        EditorGUI.indentLevel++;
        for (int i = 0; i < composition.Enemies.Count; i++)
        {
            EnemyCount pair = composition.Enemies[i];
            string enemyName = pair.Enemy != null ? pair.Enemy.name : "Missing Enemy";
            string eligibility = pair.Enemy != null && pair.Enemy.CanContainBuff()
                ? string.Empty
                : " (no metal)";
            EditorGUILayout.LabelField(
                $"{enemyName}: {pair.Count}{eligibility}",
                EditorStyles.miniLabel);
        }
        EditorGUI.indentLevel--;
    }

    private static void DrawSubWaveEnemyComposition(
        int subWaveIndex,
        SubWaveEnemyComposition subWave)
    {
        EnemyComposition composition = subWave.Composition;
        if (composition.Enemies.Count == 0)
        {
            EditorGUILayout.HelpBox(
                $"{subWaveIndex + 1}. {subWave.Name}: enemy composition is unavailable.",
                MessageType.Warning);
            return;
        }

        EditorGUILayout.LabelField(
            $"{subWaveIndex + 1}. {subWave.Name}",
            EditorStyles.miniBoldLabel);
        EditorGUI.indentLevel++;
        EditorGUILayout.LabelField(
            $"Total enemies: {composition.TotalCount}",
            EditorStyles.miniLabel);
        EditorGUI.indentLevel++;
        for (int i = 0; i < composition.Enemies.Count; i++)
        {
            EnemyCount pair = composition.Enemies[i];
            string enemyName = pair.Enemy != null ? pair.Enemy.name : "Missing Enemy";
            string eligibility = pair.Enemy != null && pair.Enemy.CanContainBuff()
                ? string.Empty
                : " (no metal)";
            EditorGUILayout.LabelField(
                $"{enemyName}: {pair.Count}{eligibility}",
                EditorStyles.wordWrappedMiniLabel);
        }
        EditorGUI.indentLevel--;
        EditorGUI.indentLevel--;
    }

    private EnemyComposition GetLevelEnemyComposition(LevelConfig levelConfig)
    {
        if (levelCompositionCache != null)
            return levelCompositionCache;

        Dictionary<Enemy, int> enemyCounts = new();
        IReadOnlyList<GameObject> waves = levelConfig.Waves;
        if (waves != null)
        {
            for (int i = 0; i < waves.Count; i++)
                MergeEnemyCounts(enemyCounts, GetWaveEnemyComposition(waves[i]).Composition);
        }

        levelCompositionCache = CreateEnemyComposition(enemyCounts);
        return levelCompositionCache;
    }

    private WaveEnemyComposition GetWaveEnemyComposition(GameObject wavePrefab)
    {
        if (wavePrefab == null)
            return new WaveEnemyComposition(
                CreateEnemyComposition(new Dictionary<Enemy, int>()),
                new List<SubWaveEnemyComposition>());

        int waveId = wavePrefab.GetInstanceID();
        if (waveCompositionCache.TryGetValue(waveId, out WaveEnemyComposition cached))
            return cached;

        InfoAboutSubWave[] subWaves =
            wavePrefab.GetComponentsInChildren<InfoAboutSubWave>(true);
        var subWaveCompositions = new List<SubWaveEnemyComposition>(subWaves.Length);
        var enemyCounts = new Dictionary<Enemy, int>();
        for (int i = 0; i < subWaves.Length; i++)
        {
            InfoAboutSubWave subWave = subWaves[i];
            var subWaveEnemyCounts = new Dictionary<Enemy, int>();
            CollectEnemyCounts(subWave, subWaveEnemyCounts);
            EnemyComposition subWaveComposition =
                CreateEnemyComposition(subWaveEnemyCounts);
            string subWaveName = string.IsNullOrWhiteSpace(subWave.name)
                ? subWave.GetType().Name
                : subWave.name;
            subWaveCompositions.Add(new SubWaveEnemyComposition(
                subWaveName,
                subWaveComposition));
            MergeEnemyCounts(enemyCounts, subWaveComposition);
        }

        var composition = new WaveEnemyComposition(
            CreateEnemyComposition(enemyCounts),
            subWaveCompositions);
        waveCompositionCache.Add(waveId, composition);
        return composition;
    }

    private static EnemyComposition CreateEnemyComposition(
        Dictionary<Enemy, int> enemyCounts)
    {
        var enemies = new List<EnemyCount>(enemyCounts.Count);
        int totalCount = 0;
        foreach (KeyValuePair<Enemy, int> pair in enemyCounts)
        {
            enemies.Add(new EnemyCount(pair.Key, pair.Value));
            totalCount += pair.Value;
        }

        enemies.Sort((left, right) => string.Compare(
            left.Enemy != null ? left.Enemy.name : "Missing Enemy",
            right.Enemy != null ? right.Enemy.name : "Missing Enemy",
            System.StringComparison.Ordinal));
        return new EnemyComposition(enemies, totalCount);
    }

    private static void MergeEnemyCounts(
        Dictionary<Enemy, int> destination,
        EnemyComposition source)
    {
        for (int i = 0; i < source.Enemies.Count; i++)
        {
            EnemyCount pair = source.Enemies[i];
            destination.TryGetValue(pair.Enemy, out int count);
            destination[pair.Enemy] = count + pair.Count;
        }
    }

    private void InvalidateEnemyCompositionCache()
    {
        waveCompositionCache.Clear();
        levelCompositionCache = null;
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
