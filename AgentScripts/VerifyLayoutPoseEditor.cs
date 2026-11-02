using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

public static class VerifyLayoutPoseEditor
{
    private static int checks;
    private static void Check(bool value, string message) { checks++; if (!value) throw new Exception(message); }

    public static async Task<object> Run()
    {
        checks = 0;
        var view = SceneView.lastActiveSceneView;
        if (view == null || EditorApplication.isPlaying || EditorApplication.isCompiling) throw new Exception("Idle Scene View required.");
        var selection = Selection.objects;
        Selection.objects = Array.Empty<Object>();
        var scene = EditorSceneManager.NewPreviewScene();
        var pivot = view.pivot; var orientation = view.rotation; float size = view.size; bool ortho = view.orthographic;
        bool show = EnemyShootingPreview.ShowFormationVolley, trajectories = EnemyShootingPreview.ShowTrajectories;
        Action<SceneView> draw = null;
        var roots = new GameObject[4]; var sources = new SimpleEnemyAttackExecutor[4]; var before = new string[4];
        string[] names = { "Fan + By Angle", "Radial + Continuous", "Circle + PingPong", "Sector + Rotation" };
        try
        {
            for (int i = 0; i < sources.Length; i++)
            {
                roots[i] = new GameObject("Transient rotation coverage " + names[i]);
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(roots[i], scene);
                roots[i].transform.position = new Vector3(0, -.4f);
                sources[i] = roots[i].AddComponent<SimpleEnemyAttackExecutor>();
                var profile = sources[i].BurstAttackSettings; profile.Validate(); var shooting = profile.Shooting;
                if (i == 0)
                {
                    shooting.ConfigureLegacyFan(7, -35, 35);
                    JsonUtility.FromJsonOverwrite("{\"shotGunMode\":1,\"shotGunProjectileCount\":2,\"shotGunDirection\":{\"x\":0,\"y\":1}}", shooting);
                    shooting.EmissionLayout.Configure(EnemyEmissionLayoutType.Point, pointOffset: new Vector3(0, .3f));
                }
                if (i == 1) shooting.EmissionLayout.Configure(EnemyEmissionLayoutType.Radial, 6);
                if (i == 2) { shooting.ConfigureLegacyFan(3, -15, 15); shooting.EmissionLayout.Configure(EnemyEmissionLayoutType.Circle, 4, .75f); }
                if (i == 3) { shooting.ConfigureLegacyFan(3, -10, 10); shooting.EmissionLayout.ConfigureSector(-30, 30, 5); }
                shooting.SetLayoutRotationEnabled(true);
                shooting.Rotation.Configure(i == 2 ? FourWayEnemyRotationDirection.CounterClockwise : FourWayEnemyRotationDirection.Clockwise,
                    i == 1 ? FourWayEnemyRotationMode.Continuous : i == 2 ? FourWayEnemyRotationMode.PingPongByAngle : FourWayEnemyRotationMode.ByAngle,
                    90, AnimationCurve.Linear(0, 0, 1, 1), i == 1 ? 360 : i == 2 ? 60 : 90, i == 2 ? -45 : 0, true);
                var range = EnemyShootingPreviewGeometry.GetLayoutRotationRange(shooting);
                Check(Mathf.Abs(range.FromAngle - (i == 2 ? -45 : 0)) < .001f
                    && Mathf.Abs(range.ToAngle - (i == 1 ? -360 : i == 2 ? 60 : -90)) < .001f, names[i] + " extrema");
                foreach (float time in new[] { range.FromTime, range.ToTime })
                {
                    var frame = EnemyShootingPreview.CreateFrame(sources[i], shooting, roots[i].transform.position, time, null, out var basis);
                    var requests = new List<EnemyProjectileSpawnRequest>(); bool match = true;
                    for (int arm = 0; arm < shooting.DirectionCount; arm++)
                    {
                        var emitter = EnemyShootingPreviewGeometry.GetEmitter(shooting, arm, frame, basis, time);
                        int offset = requests.Count;
                        EnemyAttackProjectileBuilder.AppendEmitter(requests, null, shooting, emitter.Position, frame.EnemyPosition,
                            emitter.BaseDirection, 1, 1, 0, default);
                        for (int projectile = 0; projectile < shooting.ProjectilesPerDirection; projectile++)
                        {
                            var preview = EnemyShootingPreviewGeometry.GetProjectile(shooting, emitter, projectile);
                            match &= (preview.Position - requests[offset + projectile].Position).sqrMagnitude < .00001f
                                && (preview.Direction - requests[offset + projectile].Direction).sqrMagnitude < .00001f;
                        }
                    }
                    Check(requests.Count == shooting.ProjectilesPerVolley && match, names[i] + " complete endpoint volley matches runtime builder");
                }
                before[i] = EditorJsonUtility.ToJson(sources[i]);
            }
            var ordinary = new EnemyShootingSettings(); ordinary.Validate(); ordinary.SetLayoutRotationEnabled(false);
            var f = new EnemyEmissionPreviewFrame { EnemyPosition = Vector3.zero, ShootingPoint = Vector3.up, Scale = Vector3.one, EnemyBasis = Quaternion.identity, PointBasis = Quaternion.identity };
            var a = EnemyShootingPreviewGeometry.GetEmitter(ordinary, 0, f, Quaternion.identity, 0);
            var b = EnemyShootingPreviewGeometry.GetEmitter(ordinary, 0, f, Quaternion.identity, 10);
            Check(a.Position == b.Position && a.BaseDirection == b.BaseDirection, "Non-rotation geometry unchanged");
            EnemyShootingPreview.ShowFormationVolley = true; EnemyShootingPreview.ShowTrajectories = false;
            view.orthographic = true; view.LookAtDirect(Vector3.zero, Quaternion.identity, 3.5f);
            for (int sample = 0; sample < sources.Length; sample++)
            {
                int selected = sample, frames = 0; var done = new TaskCompletionSource<bool>();
                draw = current =>
                {
                    if (current != view || Event.current.type != EventType.Repaint) return;
                    try
                    {
                        EnemyShootingPreview.DrawExecutorPattern(sources[selected], sources[selected].BurstAttackSettings.Shooting,
                            roots[selected].transform.position, 0, staticLayoutPose: true);
                        Handles.BeginGUI(); GUI.Label(new Rect(12, 35, 600, 45), names[selected] + " — automatic From / To + coverage", EditorStyles.whiteLargeLabel); Handles.EndGUI();
                        if (++frames >= 3) done.TrySetResult(true);
                    }
                    catch (Exception error) { done.TrySetException(error); }
                };
                SceneView.duringSceneGui += draw;
                var deadline = System.Diagnostics.Stopwatch.StartNew();
                while (!done.Task.IsCompleted && deadline.ElapsedMilliseconds < 10000) { view.Repaint(); await Task.Delay(60); }
                if (!done.Task.IsCompleted) throw new Exception("Scene View repaint timeout");
                await done.Task;
                var target = view.camera.targetTexture;
                if (target == null) throw new Exception("Scene View render target unavailable");
                var capture = new Texture2D(target.width, target.height, TextureFormat.RGB24, false); var oldTarget = RenderTexture.active;
                try
                {
                    RenderTexture.active = target; capture.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); capture.Apply();
                    File.WriteAllBytes("AgentArtifacts/layout-rotation-coverage-" + selected + ".png", capture.EncodeToPNG());
                }
                finally { RenderTexture.active = oldTarget; Object.DestroyImmediate(capture); }
                Check(before[selected] == EditorJsonUtility.ToJson(sources[selected]), "Drawing does not alter authored configuration");
                SceneView.duringSceneGui -= draw; draw = null;
            }
            return new { passed = true, checks, combinations = names, sceneCaptures = 4, authoredDataUnchanged = true };
        }
        finally
        {
            if (draw != null) SceneView.duringSceneGui -= draw;
            EnemyShootingPreview.ShowFormationVolley = show; EnemyShootingPreview.ShowTrajectories = trajectories;
            view.orthographic = ortho; view.LookAtDirect(pivot, orientation, size);
            foreach (var root in roots) if (root != null) Object.DestroyImmediate(root);
            EditorSceneManager.ClosePreviewScene(scene);
            Selection.objects = selection; SceneView.RepaintAll();
        }
    }
}
