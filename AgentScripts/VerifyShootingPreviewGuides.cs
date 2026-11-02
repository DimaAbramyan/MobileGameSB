using System;
using System.Collections;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class VerifyShootingPreviewGuides
{
    private static int checks;
    public static object Run()
    {
        checks = 0;
        foreach (int count in new[] { 1, 32, 64, 65, 128, 1024, 10000 })
        {
            int visible = 0;
            for (int slot = 0; slot < count; slot++) if (EnemyShootingPreview.IsOverviewSlot(slot, count)) visible++;
            Assert(visible == Math.Min(count, 64), "Overview stays bounded without duplicate samples.");
            Assert(EnemyShootingPreview.IsOverviewSlot(0, count) && EnemyShootingPreview.IsOverviewSlot(count - 1, count), "Both ends of the formation remain visible.");
            Assert(visible * EnemyShootingPreview.GetOverviewRayBudget(count) <= 512, "Ray budget covers the entire formation, not every enemy separately.");
        }
        var profile = new EnemyBurstAttackSettings(); profile.Validate(); profile.SetFacing(EnemyFacingMode.Rotation);
        var linear = AnimationCurve.Linear(0, 0, 1, 1);
        Quaternion initial = Quaternion.Euler(0, 0, 27), basis = Quaternion.Euler(0, 0, 61);
        foreach (var direction in new[] { FourWayEnemyRotationDirection.Clockwise, FourWayEnemyRotationDirection.CounterClockwise })
        {
            float sign = direction == FourWayEnemyRotationDirection.Clockwise ? -1 : 1;
            profile.FacingRotation.Configure(direction, FourWayEnemyRotationMode.Continuous, 90, linear, 360, 0, true);
            profile.Shooting.SetLayoutRotationEnabled(true);
            profile.Shooting.Rotation.Configure(FourWayEnemyRotationDirection.CounterClockwise, FourWayEnemyRotationMode.Continuous, 500, linear, 360, 0, true);
            var guide = EnemyShootingPreviewGeometry.GetFacingGuide(profile, initial, basis, .5f);
            Near(guide.Direction, Quaternion.Euler(0, 0, 27 + sign * 45) * Vector3.up, "Facing arrow uses the enemy's rotation, independent of layout rotation.");
            Assert(Math.Sign(guide.TurnAngle) == sign && !guide.PingPong, "Continuous arrow indicates the configured turn direction.");
            profile.FacingRotation.Configure(direction, FourWayEnemyRotationMode.ByAngle, 90, linear, 90, 0, true);
            Assert(Math.Abs(EnemyShootingPreviewGeometry.GetFacingGuide(profile, initial, basis, 2).TurnAngle) < .001f, "Completed By Angle stops its turn arrow.");
            profile.FacingRotation.Configure(direction, FourWayEnemyRotationMode.ByAngle, 90, linear, -90, 0, true);
            Assert(Math.Sign(EnemyShootingPreviewGeometry.GetFacingGuide(profile, initial, basis, .5f).TurnAngle) == -sign, "Negative angles reverse the indicator as they do in runtime.");
            profile.FacingRotation.Configure(direction, FourWayEnemyRotationMode.PingPongByAngle, 90, linear, 90, 0, true);
            guide = EnemyShootingPreviewGeometry.GetFacingGuide(profile, initial, basis, .5f);
            Assert(guide.PingPong && Math.Sign(guide.TurnAngle) == sign, "Ping Pong outward leg.");
            guide = EnemyShootingPreviewGeometry.GetFacingGuide(profile, initial, basis, 1.5f);
            Assert(guide.PingPong && Math.Sign(guide.TurnAngle) == -sign, "Ping Pong return leg changes the arrow direction.");
        }
        profile.SetFacing(EnemyFacingMode.FollowPattern, speed: 0);
        profile.Shooting.EmissionLayout.Configure(EnemyEmissionLayoutType.Point);
        var aimed = EnemyShootingPreviewGeometry.GetFacingGuide(profile, Quaternion.identity, Quaternion.identity, 0,
            lockedAim: Vector3.left, resolvedDirection: true);
        Near(aimed.Direction, profile.Shooting.GetFacingDirection(Vector3.left), "Follow Pattern displays the resolved aimed direction.");
        Assert(Math.Abs(aimed.TurnAngle) < .001f, "Instant follow does not show a spurious turn.");
        RouteSamples();
        return new { passed = true, checks, tested = "bounded overview with endpoints, independent facing direction, CW/CCW/negative angle/completed By Angle/Ping Pong return, aimed Follow Pattern, shared route samples and per-repaint invalidation" };
    }
    private static void RouteSamples()
    {
        var scene = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
        var root = new GameObject("Transient route sampling verification");
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, scene);
        try
        {
            var wave = root.AddComponent<DirectedEnemySubWave>();
            typeof(DirectedEnemySubWave).GetProperty("enemyCount", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(wave, 6);
            var order = wave.GetSimulationSpawnOrder();
            var current = wave.EvaluateSimulationPreview(1f);
            var captured = wave.EvaluateSimulationPreview(.2f);
            var method = typeof(EnemyShootingPreview).GetMethod("GetCapturedAimOrigin", BindingFlags.Static | BindingFlags.NonPublic);
            var cache = (IDictionary)typeof(EnemyShootingPreview).GetField("captureSamples", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            EnemyShootingPreview.BeginWavePreview(wave, 1f, current, order);
            foreach (var entry in current)
            {
                Vector3 expected = captured.TryGetValue(entry.Key, out var origin) ? origin : entry.Value;
                Vector3 actual = (Vector3)method.Invoke(null, new object[] { wave, entry.Key, entry.Value, 1f, .2f });
                Near(actual, expected, "Shared capture sample matches the original full simulation.");
            }
            Assert(cache.Count == 1, "All enemies reuse the same acquired-time simulation sample.");
            EnemyShootingPreview.EndWavePreview();
            EnemyShootingPreview.BeginWavePreview(wave, 1f, current, order);
            Assert(cache.Count == 0, "A new repaint invalidates samples so Inspector edits are immediately reflected.");
            Vector3 position = new Vector3(3, 4);
            Near((Vector3)method.Invoke(null, new object[] { wave, 0, position, 1f, 1f }), position, "Continuous aim uses the current origin without resimulating the wave.");
            Assert(cache.Count == 0, "Continuous aim does not build capture samples.");
        }
        finally
        {
            EnemyShootingPreview.EndWavePreview();
            UnityEngine.Object.DestroyImmediate(root);
            UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene);
        }
    }
    private static void Assert(bool success, string message) { checks++; if (!success) throw new Exception(message); }
    private static void Near(Vector3 actual, Vector3 expected, string message) => Assert((actual - expected).sqrMagnitude < .00001f, message + $" ({actual} != {expected})");
}
