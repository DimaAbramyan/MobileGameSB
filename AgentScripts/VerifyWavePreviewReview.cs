using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Unity.Entities;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

public static class VerifyWavePreviewReview
{
    private const string Path = "Assets/Waves/2/2_2_extrime.prefab";
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    private static int checks;
    private static object Field(object value, string name) => value.GetType().GetField(name, Flags).GetValue(value);
    private static void Set(object value, string name, object data) => value.GetType().GetField(name, Flags).SetValue(value, data);
    private static void Check(bool condition, string message)
    { checks++; if (!condition) throw new Exception(message); }
    private static async Task Seek(WavePreviewPlayer player, float time)
    {
        player.Seek(time);
        for (int guard = 0; player.IsSeeking; guard++)
        {
            if (guard > 2000) throw new Exception("Seek did not finish");
            player.Update(0, 1000); await Task.Yield();
        }
    }
    private static void CheckVisibility(WaveConductorShootingPreview simulation, bool visible, string message)
    {
        Check((bool)Field(simulation.ProjectileWorld, "visible") == visible, message + " / projectiles");
        for (int i = 0; i < simulation.CueCount; i++)
        {
            var session = simulation.GetSession(i);
            if (session != null) Check(((GameObject)Field(session, "visualsRoot")).activeSelf == visible, message + " / cue " + i);
        }
    }
    public static async Task<object> Present()
    {
        var stage = PrefabStageUtility.GetCurrentPrefabStage();
        var source = stage != null && stage.assetPath == Path ? stage.prefabContentsRoot.GetComponent<Wave>()
            : AssetDatabase.LoadAssetAtPath<GameObject>(Path).GetComponent<Wave>();
        var window = WavePreviewPlayerWindow.Open(source);
        EnemyShootingPreview.ShowFormationVolley = true;
        var player = (WavePreviewPlayer)window.GetType().GetMethod("EnsurePlayer", Flags).Invoke(window, null);
        player.Pause(); await Seek(player, 4); player.Pause();
        Set(window, "showCoordinator", true); Set(window, "contentScroll", new Vector2(0, 100000));
        window.Repaint(); SceneView.RepaintAll(); await Task.Delay(100);
        var editor = Field(window, "coordinatorEditor") as Editor;
        Check(editor != null && editor.target == source.GetComponent<WaveAttackCoordinator>(), "Coordinator editor appears at the bottom of the player");
        var positions = new Dictionary<int, Vector3>(); player.Simulation.GetSession(3).CopyPreviewPositionsTo(positions);
        return new { presented = true, time = player.PresentedTime, leftBorder = positions[0].ToString("R"),
            coordinatorPanel = editor != null, source = source.name };
    }
    public static async Task<object> Run()
    {
        checks = 0;
        byte[] bytes = File.ReadAllBytes(Path);
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        bool dirty = scene.isDirty, show = EnemyShootingPreview.ShowFormationVolley;
        int worlds = World.All.Count;
        var wave = AssetDatabase.LoadAssetAtPath<GameObject>(Path).GetComponent<Wave>();
        var border = wave.ScheduledSubWaves[3].SubWavePrefab.GetComponent<DirectedEnemySubWave>();
        Check(border.name == "LeftBorder", "The regression targets LeftBorder");
        Vector3 expected = border.GetSimulationFormationPosition(0, wave.transform);
        Check(expected.sqrMagnitude > 1f, "Expected final formation point is away from the origin");
        foreach (float time in new[] {0f, .3f, 1f, 5f, 12f})
        {
            var frame = border.EvaluateSimulationPreview(time, wave.transform, out _);
            Check(frame.TryGetValue(0, out var position) && Vector3.Distance(position, expected) < .0001f,
                "Empty Patrol preserves LeftBorder formation at " + time);
        }
        try
        {
            EnemyShootingPreview.ShowFormationVolley = true;
            using (var player = new WavePreviewPlayer(wave, 30, new Vector3(0, -3)))
            {
                player.Play(); player.Pause(); await Seek(player, 4);
                var positions = new Dictionary<int, Vector3>();
                player.Simulation.GetSession(3).CopyPreviewPositionsTo(positions);
                Check(Vector3.Distance(positions[0], expected) < .0001f, "Player uses the correct LeftBorder position");
                Check(World.All.Count == worlds + 1, "A completed frame owns one world");
                player.Seek(20); player.Update(0, .1);
                Check(player.IsSeeking, "Forward seek exceeds the small work budget");
                CheckVisibility(player.Simulation, true, "Forward catch-up does not hide the scene");
                Check(World.All.Count == worlds + 1, "Forward catch-up reuses the simulation");

                var previous = player.Simulation; float previousTime = previous.Time;
                player.Seek(1); player.Update(0, .1);
                Check(player.IsSeeking, "Backward seek has intermediate work");
                Check(ReferenceEquals(Field(player, "presentation"), previous), "Backward seek retains the displayed frame");
                Check(!ReferenceEquals(previous, player.Simulation), "Replay uses a separate hidden world");
                CheckVisibility(previous, true, "Retained frame stays visible");
                CheckVisibility(player.Simulation, false, "Partial replay stays hidden");
                Check(World.All.Count == worlds + 2, "Only one retained presentation is owned");
                player.Update(0, .1);
                Check(previous.Time == previousTime, "Retained frame does not advance");
                Check(player.PresentedTime == previousTime, "Scene HUD reports the retained frame time");
                player.Seek(0); player.Update(0, .1);
                Check(!player.IsSeeking && Field(player, "presentation") == null, "A new rewind target publishes its finished frame");
                Check(World.All.Count == worlds + 1, "Publishing frees the old world");

                await Seek(player, 4);
                player.Seek(2); player.Update(0, .1);
                Check(player.IsSeeking, "Pause regression has a pending rewind");
                int paused = player.CurrentFrame;
                player.Pause(); player.Update(1, 1000);
                Check(player.CurrentFrame == paused && !player.IsSeeking, "Pause publishes and freezes the current replay frame");
                Check(Field(player, "presentation") == null && World.All.Count == worlds + 1, "Pause releases retained presentation");
                CheckVisibility(player.Simulation, true, "Paused replay remains visible");
                await Seek(player, 4);
                player.Seek(2); player.Update(0, .1); player.Stop();
                Check(World.All.Count == worlds, "Stop during replay frees both worlds");
            }

            var previewScene = EditorSceneManager.NewPreviewScene();
            var root = new GameObject("Patrol review regression");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, previewScene);
            try
            {
                var sub = root.AddComponent<DirectedEnemySubWave>();
                Set(sub.Formation, "enemyCount", 1); Set(sub.Formation, "formationCenter", new Vector3(-3, 2, 0));
                Set(sub.PhaseEntry, "enabled", false);
                var post = sub.PostBehaviour; Set(post, "enabled", true); Set(post, "postStartDelay", 0f); Set(post, "patrolLoop", false);
                Set(post, "postCommands", new[] { new DirectedWavePostCommand { type = DirectedWavePostCommandType.Patrol,
                    completionMode = DirectedWavePostCommandCompletionMode.CompleteRoute } });
                Set(post, "patrolCoordinateSpace", DirectedWaveCoordinateSpace.World);
                Set(post, "patrolPoints", new[] { new DirectedWavePatrolPoint { offset = new Vector3(-2, 3, 0), durationToNext = 2 },
                    new DirectedWavePatrolPoint { offset = new Vector3(2, 3, 0) } });
                Check(Vector3.Distance(sub.EvaluateSimulationPreview(0)[0], new Vector3(-2, 3, 0)) < .0001f, "Configured Patrol still starts at its first point");
                Check(Vector3.Distance(sub.EvaluateSimulationPreview(1)[0], new Vector3(0, 3, 0)) < .0001f, "Configured Patrol interpolation is unchanged");
                Check(Vector3.Distance(sub.EvaluateSimulationPreview(4)[0], new Vector3(2, 3, 0)) < .0001f, "Configured Patrol retains its last point");
            }
            finally { Object.DestroyImmediate(root); EditorSceneManager.ClosePreviewScene(previewScene); }
            Check(Convert.ToBase64String(bytes) == Convert.ToBase64String(File.ReadAllBytes(Path)), "Source prefab bytes unchanged");
            Check(scene.isDirty == dirty, "Real scene dirty flag unchanged");
            Check(World.All.Count == worlds, "No leaked ECS worlds");
            return new { passed = true, checks, leftBorder = expected.ToString("R"), forwardSeekVisible = true,
                backwardSeekFrozen = true, pauseStopCleanup = true, configuredPatrolUnchanged = true };
        }
        finally { EnemyShootingPreview.ShowFormationVolley = show; }
    }
}
