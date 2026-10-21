using System;
using System.Diagnostics;
using System.Reflection;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEditor;
using UnityEngine;

public static class VerifyWaveShootingSimulation
{
    static int checks;
    static void Assert(bool condition, string message) { checks++; if (!condition) throw new Exception(message); }
    public static object Run()
    {
        if (EditorApplication.isPlaying || EditorApplication.isCompiling || EditorApplication.isUpdating) throw new Exception("Editor must be idle");
        checks = 0;
        var scene = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
        var root = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Waves/2/2_1_extrime.prefab"));
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, scene);
        var results = new System.Collections.Generic.List<object>();
        try
        {
            foreach (var wave in root.GetComponentsInChildren<DirectedEnemySubWave>(true))
            {
                if (!wave.name.Trim().EndsWith("left") && !wave.name.Trim().EndsWith("right")) continue;
                string before = EditorJsonUtility.ToJson(wave);
                var resolved = new DirectedWaveAttackSettings();
                wave.AttackPatternBehaviour.TryCopyResolvedAttackSettingsTo(0, false, resolved);
                int expected = resolved.WaveBurstSettings.Shooting.ProjectilesPerDirection;
                using (var session = new WaveShootingPreviewSession(wave, true, false, 0, new Vector3(0, -3)))
                {
                    Assert(session.TargetObject.hideFlags == HideFlags.HideAndDontSave, "Target must never save in scene");
                    Assert(session.EnemyCount == 1, "Default preview samples one enemy");
                    session.AdvanceTo(1f / 30, 1000);
                    Assert(session.ProjectileCount == expected, "Actual authored Sector creates the configured DNA Fan: " + session.ProjectileCount + "; expected " + expected);
                    using var query = session.ProjectileWorld.SimulationWorld.EntityManager.CreateEntityQuery(typeof(EnemyProjectileMovement), typeof(LocalTransform));
                    using var movements = query.ToComponentDataArray<EnemyProjectileMovement>(Allocator.Temp);
                    Assert(movements[0].OrbitCenterMode == EnemyProjectileOrbitCenterMode.DnaLike, "Actual movement stays DNA");
                    Assert(math.abs(movements[0].DnaElapsedTime - 1f / 30) < .001f, "Runtime ECS motion updates in editor world");
                    session.TargetPosition = new Vector3(3, -5, 0);
                    Assert(session.TargetObject.transform.position == session.TargetPosition, "Target can move");
                    var timer = Stopwatch.StartNew();
                    int chunks = 0;
                    while (!session.AdvanceTo(8, 4)) { if (++chunks > 1000) throw new Exception("Seek failed to finish"); }
                    results.Add(new { wave=wave.name, seek8SecondsMs=timer.Elapsed.TotalMilliseconds, chunks, active=session.ProjectileCount });
                    Assert(session.Time >= 7.96f && session.Time <= 8.04f, "Seek reaches requested time");
                    Assert(session.ProjectileCount <= EnemyProjectilePreviewWorld.MaximumProjectiles, "Projectile count bounded");
                }
                Assert(before == EditorJsonUtility.ToJson(wave), "Preview does not mutate authored wave");
            }
            var selectedWave = root.GetComponentsInChildren<DirectedEnemySubWave>(true)[1];
            var editor = (DirectedEnemySubWaveEditor)Editor.CreateEditor(selectedWave);
            try
            {
                var flags = BindingFlags.Instance | BindingFlags.NonPublic;
                var formation = editor.GetType().GetField("previewFormationOnly", flags);
                formation.SetValue(editor, true);
                editor.GetType().GetMethod("SeekPreview", flags).Invoke(editor, new object[] { 2f });
                for (int i=0; i<100; i++) editor.GetType().GetMethod("UpdatePreview", flags).Invoke(editor, null);
                editor.GetType().GetMethod("SeekPreview", flags).Invoke(editor, new object[] { .2f });
                var session = (WaveShootingPreviewSession)editor.GetType().GetField("shootingSession", flags).GetValue(editor);
                Assert(session.Time == 0, "Backward seek resets firing history");
                Assert((bool)editor.GetType().GetField("previewVisible", flags).GetValue(editor), "Paused preview remains visible");
                editor.GetType().GetMethod("StopPreview", flags).Invoke(editor, null);
                Assert(editor.GetType().GetField("shootingSession", flags).GetValue(editor) == null, "Stop disposes session");
            }
            finally { UnityEngine.Object.DestroyImmediate(editor); }
        }
        finally { UnityEngine.Object.DestroyImmediate(root); UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene); }
        return new { passed=true, checks, results };
    }
}
