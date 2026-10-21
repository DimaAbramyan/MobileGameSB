using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class VerifyDiveMotionLimits
{
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private static int checks, scenarios;
    private static void Check(bool value, string message) { checks++; if (!value) throw new Exception(message); }
    public static object Run()
    {
        checks = scenarios = 0;
        var curves = new[] { AnimationCurve.Linear(0, 0, 1, 1), AnimationCurve.EaseInOut(0, 0, 1, 1),
            new AnimationCurve(new Keyframe(0, 0), new Keyframe(.02f, .95f), new Keyframe(1, 1)),
            new AnimationCurve(new Keyframe(0, 0), new Keyframe(.25f, 1), new Keyframe(.7f, .1f), new Keyframe(1, 1)),
            AnimationCurve.Linear(0, 0, 1, .25f),
            new AnimationCurve(new Keyframe(0, 0, 0, float.PositiveInfinity), new Keyframe(.5f, 1), new Keyframe(1, 1)) };
        foreach (var curve in curves)
            foreach (float distance in new[] { .000001f, .03f, 1f, 12f })
            {
                Simulate(distance, 30, .3f, 2, 1, curve, false);
                Simulate(distance, 30, .3f, 2, 8, curve, true);
            }
        Simulate(.05f, 100, .4f, 0, 10, curves[1], false);
        Simulate(12, 4, 0, 0, .25f, curves[0], false);
        Simulate(.000001f, 10, 0, .000005f, 1, curves[2], true);
        var zero = new DirectedWaveDiveMotionProgress(Vector3.one, 0, 10, .5f);
        Check(zero.IsComplete && zero.Duration == 0 && zero.Position == Vector3.one, "Zero distance is already complete without a bogus wait");
        Data(); Native(curves); Physics(curves[2]);
        return new { passed = true, checks, scenarios, shortAndLongDistances = true, curveReversalsAndSteps = true,
            minimumWithSpeedBonuses = true, physicalTickBudget = true, actualPhysics2DSimulation = true, stageIsolation = true, legacyDefaults = true };
    }
    private static void Simulate(float distance, float speed, float minimum, float maximum, float multiplier, AnimationCurve curve, bool variableDelta)
    {
        scenarios++;
        var from = distance < .0001f ? Vector3.zero : new Vector3(.3f, -.5f, 0); var end = from + new Vector3(distance * .6f, distance * .8f, 0);
        float actualDistance = Vector3.Distance(from, end);
        var progress = new DirectedWaveDiveMotionProgress(from, actualDistance, speed, minimum);
        float time = 0, lastPhase = 0; int step = 0; Vector3 previous = from;
        var deltas = new[] { 1f / 120f, 1f / 30f, .13f, .007f };
        while (!progress.IsComplete && step < 100000)
        {
            float delta = variableDelta ? deltas[step % deltas.Length] : 1f / 60f;
            float phase = progress.PrepareStep(delta, multiplier);
            Check(phase >= lastPhase && phase <= 1f, "Curve time moves forward while retaining pending samples");
            Vector3 desired = Vector3.LerpUnclamped(from, end, Mathf.Clamp01(curve.Evaluate(phase)));
            var position = progress.ApplyStep(desired, delta, maximum);
            if (maximum > 0) Check(Vector3.Distance(previous, position) <= maximum * delta + (actualDistance < .0001f ? maximum * delta * .0001f + 1e-12f : .000002f), "Actual displacement respects speed cap");
            Check(!float.IsNaN(position.x) && !float.IsInfinity(position.y), "Finite coordinates");
            Check(Vector3.Cross(end - from, position - from).sqrMagnitude < .000001f, "Path stays on the authored segment");
            previous = position; lastPhase = phase; time += delta; step++;
        }
        Check(progress.IsComplete, "Curve terminates at finite duration");
        Check(time + .00002f >= minimum, "Minimum duration also holds with movement speed bonuses");
        int finishSteps = 0;
        while (!progress.Position.Equals(end) && finishSteps++ < 100000)
        {
            float delta = 1f / 60f;
            float cap = maximum > 0 ? maximum : speed * multiplier;
            var position = progress.MovePosition(end, delta, cap);
            Check(Vector3.Distance(previous, position) <= cap * delta + (actualDistance < .0001f ? cap * delta * .0001f + 1e-12f : .000002f), "Final correction cannot teleport");
            previous = position; time += delta;
        }
        Check(progress.Position.Equals(end), "Exact final endpoint without tolerance snap");
        if (maximum > 0) Check(time + .00002f >= actualDistance / maximum, "Speed cap extends duration when necessary");
    }
    private static void Data()
    {
        var old = JsonUtility.FromJson<DirectedWaveAttackSettings>("{\"serializedVersion\":12,\"movementMode\":1,\"minDiveSpeed\":7,\"maxDiveSpeed\":9}");
        old.Validate(); Check(old.MinimumDiveDuration == 0 && old.MaximumDiveSpeed == 0, "Missing serialized fields stay disabled");
        Check((float)Get(old, "minDiveSpeed") == 7 && (float)Get(old, "maxDiveSpeed") == 9, "Authored speed range remains intact");
        Set(old, "minimumDiveDuration", .4f); Set(old, "maximumDiveSpeed", 2f);
        var copy = new DirectedWaveAttackSettings(); copy.CopyFrom(old);
        Check(copy.MinimumDiveDuration == .4f && copy.MaximumDiveSpeed == 2f, "Settings copy limits");
        var restored = JsonUtility.FromJson<DirectedWaveAttackSettings>(JsonUtility.ToJson(old));
        Check(restored.MinimumDiveDuration == .4f && restored.MaximumDiveSpeed == 2f, "Serialization round trip");
        var preset = ScriptableObject.CreateInstance<DirectedWaveAttackPreset>();
        try
        {
            preset.SetAttackSettings(old, false, 1f); Check(preset.AttackSettings.MaximumDiveSpeed == 2f, "Preset preserves limits");
            var pattern = new DirectedWaveAttackPattern(); Set(pattern, "attackSettings", old);
            pattern.CopyResolvedSettingsTo(copy); Check(copy.MinimumDiveDuration == .4f, "Multiple/local pattern resolution copies limits");
            var step = new DirectedWaveAttackSequenceStep { pattern = pattern, attackSpeedMultiplier = 5 };
            step.CopySettingsTo(copy); Check(copy.MaximumDiveSpeed == 2f && copy.MinimumDiveDuration == .4f, "Sequence cannot scale away physical limits");
        }
        finally { UnityEngine.Object.DestroyImmediate(preset); }
        Set(old, "minimumDiveDuration", -1f); Set(old, "maximumDiveSpeed", -2f); old.Validate();
        Check(old.MinimumDiveDuration == 0 && old.MaximumDiveSpeed == 0, "Negative values disable limits");
    }
    private static void Native(AnimationCurve[] curves)
    {
        var scene = EditorSceneManager.NewPreviewScene(); var root = new GameObject("Dive verification");
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, scene);
        var actor = new GameObject("Dive enemy"); actor.SetActive(false); actor.transform.SetParent(root.transform);
        try
        {
            var wave = root.AddComponent<DirectedEnemySubWave>(); var enemy = actor.AddComponent<Enemy>();
            var body = actor.AddComponent<Rigidbody2D>(); body.bodyType = RigidbodyType2D.Kinematic;
            var settings = new DirectedWaveAttackSettings(); Set(settings, "movementMode", DirectedWaveAttackMovementMode.MoveToPlayer);
            Set(settings, "minimumDiveDuration", .3f); Set(settings, "maximumDiveSpeed", .7f);
            var type = typeof(Wave).Assembly.GetType("DirectedWaveAttackController", true);
            var controller = Activator.CreateInstance(type, new object[] { wave, settings, null });
            var positions = (Dictionary<Enemy, Vector3>)Get(wave, "attackMotionPositions");
            foreach (var curve in curves)
            {
                scenarios++;
                positions.Clear(); actor.transform.position = Vector3.zero;
                Vector3 target = new Vector3(.03f, 0, 0), previous = Vector3.zero;
                var iterator = Move(controller, enemy, target, curve, true);
                Check(iterator.MoveNext() && iterator.Current is WaitForFixedUpdate, "Physics bodies start on a fixed step");
                int frames = 0;
                while (iterator.MoveNext() && frames++ < 2000)
                {
                    Check(iterator.Current is WaitForFixedUpdate, "Physical approach consistently uses fixed steps");
                    if (!positions.TryGetValue(enemy, out var position)) continue;
                    Check(Vector3.Distance(previous, position) <= .7f * Time.fixedDeltaTime + .000001f, "Native movement command respects physical speed budget");
                    previous = position;
                }
                Check(frames < 2000 && positions[enemy].Equals(target), "Native curve and final tail terminate exactly");
                Check(frames * Time.fixedDeltaTime + .00001f >= .3f, "Native minimum duration");
            }
            positions.Clear(); actor.transform.position = Vector3.zero;
            var zero = Move(controller, enemy, Vector3.zero, curves[0], true);
            Check(!zero.MoveNext(), "Native zero distance does not produce an artificial physical step");
            var baseline = Collect(controller, enemy, positions, Vector3.right, curves[1], false);
            Set(settings, "movementMode", DirectedWaveAttackMovementMode.FlyThroughDive);
            var flyThrough = Collect(controller, enemy, positions, Vector3.right, curves[1], true);
            Check(Equal(baseline, flyThrough), "Fly Through ignores these Move To Player limits");
            Set(settings, "movementMode", DirectedWaveAttackMovementMode.MoveToPlayer);
            Set(settings, "minimumDiveDuration", 0f); Set(settings, "maximumDiveSpeed", 0f);
            var disabled = Collect(controller, enemy, positions, Vector3.right, curves[1], true);
            Check(Equal(baseline, disabled), "Legacy duration/positions and final endpoint are unchanged when disabled");
            Set(settings, "minimumDiveDuration", .3f); Set(settings, "maximumDiveSpeed", .7f);
            Set(settings, "playerStandoffRadius", .4f); actor.transform.position = Vector3.up;
            var standoff = (IEnumerator)Invoke(controller, "MoveEnemy", enemy, Vector3.up, Vector3.up * .4f, 100f, curves[2], false, true, Vector3.zero, true);
            int count = 0;
            while (standoff.MoveNext() && count++ < 3000)
                if (positions.TryGetValue(enemy, out var position)) Check(position.magnitude >= .4f - .00001f, "Standoff radius remains intact");
            Check(count < 3000 && positions[enemy].Equals(Vector3.up * .4f), "Standoff endpoint exact");
            Set(settings, "useDivePreparation", false); Set(settings, "fireMode", DirectedWaveAttackFireMode.None);
            Set(settings, "minDiveDepth", 2f); Set(settings, "maxDiveDepth", 2f);
            actor.transform.position = Vector3.zero;
            var dive = (IEnumerator)Invoke(controller, "RunMoveToPlayerAndShoot", enemy, null);
            Check(dive.MoveNext(), "Dive starts with existing preparation stage");
            Check(dive.MoveNext() && (bool)Get(dive.Current, "applyDiveLimits"), "Only the approach opts into limits");
            Check(dive.MoveNext() && !(bool)Get(dive.Current, "applyDiveLimits"), "Actual return stage does not opt into limits");
            Set(settings, "useDivePreparation", true);
            var preparation = (IEnumerator)Invoke(controller, "RunDivePreparation", enemy, Vector3.down);
            Check(preparation.MoveNext() && !(bool)Get(preparation.Current, "applyDiveLimits"), "Actual preparation stage remains unchanged");
            Set(enemy, "isDead", true);
            Invoke(controller, "Stop");
        }
        finally { UnityEngine.Object.DestroyImmediate(root); EditorSceneManager.ClosePreviewScene(scene); }
    }
    private static IEnumerator Move(object controller, Enemy enemy, Vector3 target, AnimationCurve curve, bool apply)
        => (IEnumerator)Invoke(controller, "MoveEnemy", enemy, Vector3.zero, target, 100f, curve, false, false, Vector3.zero, apply);
    private static void Physics(AnimationCurve curve)
    {
        var scene = EditorSceneManager.NewPreviewScene();
        var physics = PhysicsSceneExtensions2D.GetPhysicsScene2D(scene);
        Check(physics.IsValid() && !physics.Equals(Physics2D.defaultPhysicsScene), "Physics test is isolated from the user's scene");
        var root = new GameObject("Dive physical simulation"); UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, scene);
        var actor = new GameObject("Physical dive enemy"); actor.SetActive(false); actor.transform.SetParent(root.transform);
        try
        {
            var wave = root.AddComponent<DirectedEnemySubWave>(); var enemy = actor.AddComponent<Enemy>();
            var body = actor.AddComponent<Rigidbody2D>(); body.bodyType = RigidbodyType2D.Kinematic; body.interpolation = RigidbodyInterpolation2D.None;
            var settings = new DirectedWaveAttackSettings(); Set(settings, "movementMode", DirectedWaveAttackMovementMode.MoveToPlayer);
            Set(settings, "minimumDiveDuration", .3f); Set(settings, "maximumDiveSpeed", 2f);
            var type = typeof(Wave).Assembly.GetType("DirectedWaveAttackController");
            var controller = Activator.CreateInstance(type, new object[] { wave, settings, null }); actor.SetActive(true);
            foreach (float distance in new[] { .03f, 12f })
            {
                scenarios++; body.position = Vector2.zero; actor.transform.position = Vector3.zero;
                Vector3 target = Vector3.down * distance; var iterator = Move(controller, enemy, target, curve, true);
                Vector2 previous = body.position; int frames = 0;
                while (iterator.MoveNext() && frames++ < 5000)
                {
                    Check(iterator.Current is WaitForFixedUpdate, "Physics simulation and coroutine share a clock");
                    Check(physics.Simulate(Time.fixedDeltaTime), "Local physics scene simulates successfully");
                    Check(Vector2.Distance(body.position, previous) <= 2f * Time.fixedDeltaTime + .000003f, "Actual Rigidbody2D speed, not only requested positions, is capped");
                    previous = body.position;
                }
                Check(frames < 5000 && Vector2.Distance(body.position, target) < .000001f, "Physical body reaches the final point before attack continues");
                Check(frames * Time.fixedDeltaTime >= .3f, "Actual physical movement observes minimum duration");
            }
            Invoke(controller, "Stop");
        }
        finally { UnityEngine.Object.DestroyImmediate(root); EditorSceneManager.ClosePreviewScene(scene); }
    }
    private static List<Vector3> Collect(object controller, Enemy enemy, Dictionary<Enemy, Vector3> positions, Vector3 target, AnimationCurve curve, bool apply)
    {
        positions.Clear(); enemy.transform.position = Vector3.zero;
        var result = new List<Vector3>(); var iterator = Move(controller, enemy, target, curve, apply); int frames = 0;
        while (iterator.MoveNext() && frames++ < 10000)
        { Check(!(iterator.Current is WaitForFixedUpdate), "Other stages retain the old update clock"); if (positions.TryGetValue(enemy, out var p)) result.Add(p); }
        Check(frames < 10000, "Legacy native movement terminates"); if (positions.TryGetValue(enemy, out var end)) result.Add(end);
        return result;
    }
    private static bool Equal(List<Vector3> a, List<Vector3> b)
    { if (a.Count != b.Count) return false; for (int i = 0; i < a.Count; i++) if (!a[i].Equals(b[i])) return false; return true; }
    private static object Get(object value, string name) { for (var t = value.GetType(); t != null; t = t.BaseType) { var f = t.GetField(name, Flags); if (f != null) return f.GetValue(value); } throw new Exception(name); }
    private static void Set(object value, string name, object data) { for (var t = value.GetType(); t != null; t = t.BaseType) { var f = t.GetField(name, Flags); if (f != null) { f.SetValue(value, data); return; } } throw new Exception(name); }
    private static object Invoke(object value, string name, params object[] args) => value.GetType().GetMethod(name, Flags).Invoke(value, args);
}
