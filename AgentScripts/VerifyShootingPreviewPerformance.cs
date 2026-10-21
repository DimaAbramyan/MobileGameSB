using System;
using System.Diagnostics;
using System.Reflection;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

public static class VerifyShootingPreviewPerformance
{
    public static async Task<object> Run()
    {
        var view = SceneView.lastActiveSceneView;
        if (view == null || EditorApplication.isPlaying || EditorApplication.isCompiling) throw new Exception("Idle Scene View required.");
        var scene = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
        var root = new GameObject("Transient shooting preview benchmark");
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, scene);
        var clone = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefub/Enemies Prefab/Enemy.prefab"));
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(clone, scene);
        var wave = root.AddComponent<DirectedEnemySubWave>();
        var attack = root.AddComponent<DirectedWaveAttackBehaviour>();
        Set(wave, "enemyPrefab", clone.GetComponent<Enemy>());
        Set(wave, "enemyCount", 32);
        Set(wave, "formationLayout", DirectedWaveFormationLayout.HorizontalLine);
        var settings = (DirectedWaveAttackSettings)Get(attack, "attackSettings");
        Set(settings, "fireMode", DirectedWaveAttackFireMode.Aimed);
        Set(settings, "burstSettingsSource", DirectedWaveBurstSettingsSource.WaveOverride);
        var shooting = settings.WaveBurstSettings.Shooting;
        shooting.EmissionLayout.ConfigureSector(350, 10, 3);
        shooting.ConfigureLegacyFan(5, -10, 10);
        Set(shooting, "projectileMovementPattern", EnemyProjectileMovementPattern.Orbit);
        Set(shooting, "projectileOrbitCenterMode", EnemyProjectileOrbitCenterMode.DnaLike);
        settings.Validate();
        var done = new TaskCompletionSource<object>();
        bool saved = EnemyShootingPreview.ShowFormationVolley;
        Action<SceneView> draw = current =>
        {
            if (current != view || Event.current.type != EventType.Repaint || done.Task.IsCompleted) return;
            try
            {
                var results = new System.Collections.Generic.List<object>();
                foreach (int count in new[] { 32, 128, 1024 })
                {
                    Set(wave, "enemyCount", count);
                    results.Add(Measure(wave, "overview"));
                }
                EnemyShootingPreview.ShowFormationVolley = false;
                results.Add(Measure(wave, "hidden"));
                EnemyShootingPreview.ShowFormationVolley = true;
                results.Add(Measure(wave, "selected slot", 0));
                shooting.EmissionLayout.Configure(EnemyEmissionLayoutType.Radial, 1000);
                shooting.ConfigureLegacyFan(1000, -30, 30);
                settings.Validate();
                results.Add(Measure(wave, "dense selected slot", 0));
                done.SetResult(new { passed = true, measurements = results, tested = "actual Scene GUI; bounded 1024-enemy overview, hidden fast path, detailed selected slot, million-ray volley sampled" });
            }
            catch (Exception error) { done.TrySetException(error); }
        };
        try
        {
            EnemyShootingPreview.ShowFormationVolley = true;
            SceneView.duringSceneGui += draw;
            view.Repaint();
            if (await Task.WhenAny(done.Task, Task.Delay(15000)) != done.Task) throw new Exception("Scene View did not repaint.");
            return await done.Task;
        }
        finally
        {
            SceneView.duringSceneGui -= draw;
            EnemyShootingPreview.ShowFormationVolley = saved;
            UnityEngine.Object.DestroyImmediate(root);
            UnityEngine.Object.DestroyImmediate(clone);
            UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene);
        }
    }
    private static object Measure(DirectedEnemySubWave wave, string mode, int slot = -1)
    {
        var watch = Stopwatch.StartNew();
        EnemyShootingPreview.DrawFormationVolley(wave, slot);
        watch.Stop();
        return new { mode, enemies = wave.GetConfiguredEnemySlotCount(), repaintMs = watch.Elapsed.TotalMilliseconds };
    }
    private static object Get(object obj, string name)
    {
        for (Type type = obj.GetType(); type != null; type = type.BaseType)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            var field = type.GetField(name, flags);
            if (field != null) return field.GetValue(obj);
            var property = type.GetProperty(name, flags);
            if (property != null) return property.GetValue(obj);
        }
        throw new Exception(name);
    }
    private static void Set(object obj, string name, object value)
    {
        for (Type type = obj.GetType(); type != null; type = type.BaseType)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            var field = type.GetField(name, flags);
            if (field != null) { field.SetValue(obj, value); return; }
            var property = type.GetProperty(name, flags);
            if (property != null) { property.SetValue(obj, value); return; }
        }
        throw new Exception(name);
    }
}
