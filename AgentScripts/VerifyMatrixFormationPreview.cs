using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class VerifyMatrixFormationPreview
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    public static object Run()
    {
        var prefab = PrefabUtility.LoadPrefabContents("Assets/Waves/2/2_3_extrime.prefab");
        try
        {
            var original = Verify(prefab);
            var main = prefab.transform.Find("Main").GetComponent<DirectedEnemySubWave>();
            using var serialized = new SerializedObject(main);
            var points = serialized.GetIterator();
            while (points.Next(true) && points.name != "customFormationPoints") { }
            if (points.name != "customFormationPoints") throw new Exception("Missing custom points");
            int count = points.arraySize;
            points.arraySize = count * 2;
            for (int i = 0; i < count; i++)
                points.GetArrayElementAtIndex(i + count).vector3Value =
                    points.GetArrayElementAtIndex(i).vector3Value + Vector3.right * .01f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            main.InvalidateSimulationPreviewCache();
            return Newtonsoft.Json.JsonConvert.SerializeObject(new { original, dense = Verify(prefab) });
        }
        finally { PrefabUtility.UnloadPrefabContents(prefab); }
    }

    private static object Verify(GameObject prefab)
    {
        var owner = prefab.GetComponent<Wave>();
        var main = prefab.transform.Find("Main").GetComponent<DirectedEnemySubWave>();
        var expected = new Dictionary<int, Vector3>();
        using var standalone = new WaveShootingPreviewSession(main, true, true, 0, Vector3.down * 4f);
        var standalonePositions = new Dictionary<int, Vector3>();
        standalone.CopyPreviewPositionsTo(standalonePositions);
        int standaloneMisplaced = 0;
        foreach (var pair in standalonePositions)
            if (Vector3.Distance(pair.Value, main.GetSimulationFormationPosition(pair.Key)) > .001f)
                standaloneMisplaced++;
        float time = main.GetSimulationPreviewPostStartTime() + 2f;
        int cueIndex = -1;
        for (int i = 0; i < owner.ScheduledSubWaves.Count; i++)
            if (owner.ScheduledSubWaves[i].SubWavePrefab == main.gameObject) cueIndex = i;
        if (cueIndex < 0) throw new Exception("Main missing from schedule");
        var editor = Editor.CreateEditor(owner);
        using var shooting = new WaveConductorShootingPreview(owner, Vector3.down * 4f);
        try
        {
            float global = time + owner.ScheduledSubWaves[cueIndex].StartDelay;
            var warning = owner.GetComponent<WaveDangerWarningController>();
            if (warning != null && warning.ShouldPlayWarning) global += warning.WarningDuration;
            int steps = 0;
            while (!shooting.AdvanceTo(global, 1000d)) if (++steps > 100) throw new Exception("Preview timeout");
            typeof(WaveEditor).GetField("shootingPreview", Private).SetValue(editor, shooting);
            var cache = typeof(WaveEditor).GetMethod("GetCuePreviewCache", Private)
                .Invoke(editor, new object[] { cueIndex, main.gameObject });
            typeof(WaveEditor).GetMethod("RefreshCuePreviewCache", Private)
                .Invoke(editor, new object[] { cache, cueIndex, owner, time, main.GetSimulationEnemyCount() });
            var actual = (Dictionary<int, Vector3>)cache.GetType().GetField("positions").GetValue(cache);
            var order = shooting.GetSpawnOrder(cueIndex);
            main.EvaluateSimulationPreviewNonAlloc(time, owner.transform, order, expected, out _);
            int missing = 0, misplaced = 0;
            foreach (var pair in expected)
            {
                if (!actual.TryGetValue(pair.Key, out var p)) missing++;
                else if (Vector3.Distance(p, pair.Value) > .001f) misplaced++;
            }
            var session = shooting.GetSession(cueIndex);
            var visualPositions = new Dictionary<int, Vector3>();
            session.CopyPreviewPositionsTo(visualPositions);
            int configured = main.GetConfiguredEnemySlotCount();
            bool sampled = session.Warning != null && session.Warning.Contains("samples");
            bool passed = actual.Count == expected.Count && missing == 0 && misplaced == 0
                && session.EnemyCount == configured && visualPositions.Count == configured
                && standalone.EnemyCount == configured && standalonePositions.Count == configured
                && standaloneMisplaced == 0 && !sampled;
            if (!passed) throw new Exception($"Incomplete preview: configured {configured}, standalone {standalone.EnemyCount}, schedule {session.EnemyCount}, visible {visualPositions.Count}");
            return new { expected = expected.Count, actual = actual.Count, missing, misplaced,
                standaloneEnemies = standalone.EnemyCount, standaloneMisplaced,
                simulatedShooters = session.EnemyCount, visibleEnemies = visualPositions.Count, sampled, passed };
        }
        finally
        {
            typeof(WaveEditor).GetField("shootingPreview", Private).SetValue(editor, null);
            UnityEngine.Object.DestroyImmediate(editor);
        }
    }
}
