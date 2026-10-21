using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class VerifyDirectedWaveAimModes
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static int checks;

    private sealed class RecordingExecutor : IEnemyBurstAttackExecutor, IEnemyLockedAimAttackExecutor
    {
        public readonly List<Vector3> Directions = new List<Vector3>();
        public readonly List<EnemyBurstAttackSettings> Patterns = new List<EnemyBurstAttackSettings>();
        public Enemy Enemy;
        public Transform Player;
        public EnemyBurstAttackSettings BurstAttackSettings { get; set; }
        public bool CanPerformWaveAttack => true;
        public void SetWaveAttackControl(bool enabled) { }
        public Vector3 GetWaveAimOrigin(EnemyBurstAttackSettings settings) => Enemy.transform.position;
        public bool TryFireAt(Vector3 target, EnemyBurstAttackSettings settings)
            => Record((target - Enemy.transform.position).normalized, settings);
        public bool TryFireInDirection(Vector3 direction, EnemyBurstAttackSettings settings) => Record(direction, settings);
        public bool TryFireInAimedDirection(Vector3 direction, EnemyBurstAttackSettings settings) => Record(direction, settings);
        private bool Record(Vector3 direction, EnemyBurstAttackSettings settings)
        {
            Directions.Add(direction);
            Patterns.Add(settings);
            Player.position += new Vector3(2f, 0f, 0f);
            Enemy.transform.position += new Vector3(0f, 0.25f, 0f);
            return true;
        }
    }

    public static object Run()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlaying)
            throw new Exception("Editor must be idle in Edit Mode.");
        checks = 0;
        var scene = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
        var root = new GameObject("Aim mode verification");
        root.SetActive(false);
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, scene);
        try
        {
            var wave = root.AddComponent<DirectedEnemySubWave>();
            var player = Child(root.transform, "Player").gameObject.AddComponent<PlayerController>();
            var enemy = Child(root.transform, "Enemy").gameObject.AddComponent<Enemy>();
            Set(wave, "playerController", player);
            var points = Child(root.transform, "Points");
            var local = Child(points, "Slot").gameObject.AddComponent<DirectedWaveEnemyOverride>();
            Set(wave, "formationFrozen", true);
            Set(wave, "formationPointsRoot", points);
            ((IDictionary)Get(wave, "formationIndices"))[enemy] = 0;
            root.SetActive(true);
            var executor = new RecordingExecutor { Enemy = enemy, Player = player.transform };
            var settings = new DirectedWaveAttackSettings();
            Set(settings, "burstSettingsSource", DirectedWaveBurstSettingsSource.WaveOverride);
            foreach (DirectedWaveAttackAimMode mode in Enum.GetValues(typeof(DirectedWaveAttackAimMode)))
            {
                Set(settings, "aimMode", mode);
                var profile = Profile(true, 2, 2);
                Set(settings, "waveBurstSettings", profile);
                Reset(executor);
                object controller = Controller("DirectedWaveAttackController", wave, settings);
                RunIterator((IEnumerator)Invoke(controller, "FireFullAttack", enemy, executor, profile));
                Assert(executor.Directions.Count == 4, "Four shots in two bursts.");
                VerifyWithinBurst(executor.Directions[0], executor.Directions[1], mode);
                VerifyWithinBurst(executor.Directions[2], executor.Directions[3], mode);
                Assert((Vector3.Dot(executor.Directions[0], executor.Directions[2]) > .99999f)
                    == (mode == DirectedWaveAttackAimMode.AimOnStart), "Only Aim On Burst reacquires between bursts.");
                Vector3 previous = executor.Directions[3];
                RunIterator((IEnumerator)Invoke(controller, "FireFullAttack", enemy, executor, profile));
                Assert(Vector3.Dot(previous, executor.Directions[4]) < .99999f, "New attack reacquires aim.");

                Reset(executor);
                object entrance = Controller("DirectedWaveEntranceAttackController", wave, settings);
                ((IDictionary)Get(entrance, "executors"))[enemy] = executor;
                var aim = new DirectedWaveAttackAimState();
                for (int i = 0; i < 4; i++)
                {
                    object[] args = { enemy, aim, i };
                    Assert((bool)Invoke(entrance, "TryFireSequence", args), "Entrance shot succeeds.");
                    aim = (DirectedWaveAttackAimState)args[1];
                }
                VerifyWithinBurst(executor.Directions[0], executor.Directions[1], mode);
                VerifyWithinBurst(executor.Directions[2], executor.Directions[3], mode);
                Assert((Vector3.Dot(executor.Directions[0], executor.Directions[2]) > .99999f)
                    == (mode == DirectedWaveAttackAimMode.AimOnStart), "Entrance respects acquisition mode.");
                VerifyMultiple(wave, enemy, executor, settings, mode);
            }

            var noBurst = Profile(false, 4, 1);
            Set(settings, "waveBurstSettings", noBurst);
            Set(settings, "aimMode", DirectedWaveAttackAimMode.AimOnBurst);
            Reset(executor);
            var fallback = Controller("DirectedWaveAttackController", wave, settings);
            RunIterator((IEnumerator)Invoke(fallback, "FireFullAttack", enemy, executor, noBurst));
            foreach (Vector3 shot in executor.Directions) Same(shot, executor.Directions[0], "No-burst profile keeps attack aim.");
            Assert(DirectedWaveAttackAimState.RequiresBurstWarning(DirectedWaveAttackAimMode.AimOnBurst, noBurst),
                "Inspector warns when burst mode is disabled.");
            Assert(!DirectedWaveAttackAimState.RequiresBurstWarning(DirectedWaveAttackAimMode.AimOnStart, noBurst),
                "Aim On Start does not require bursts.");
            Set(settings, "aimMode", DirectedWaveAttackAimMode.AimContinuous);
            Reset(executor);
            RunIterator((IEnumerator)Invoke(Controller("DirectedWaveAttackController", wave, settings),
                "FireFullAttack", enemy, executor, noBurst));
            for (int i = 1; i < executor.Directions.Count; i++)
                VerifyWithinBurst(executor.Directions[i - 1], executor.Directions[i], DirectedWaveAttackAimMode.AimContinuous);
            Assert(!DirectedWaveAttackAimState.RequiresBurstWarning(DirectedWaveAttackAimMode.AimContinuous, noBurst),
                "Continuous aim does not require bursts.");
            Set(settings, "aimMode", DirectedWaveAttackAimMode.AimOnBurst);
            var burst = Profile(true, 3, 3);
            Set(settings, "fireMode", DirectedWaveAttackFireMode.Default);
            Assert(settings.HasFireMode && !settings.RequiresPlayerTarget,
                "Default starts configured wave attacks without requiring a player target.");
            Reset(executor);
            RunIterator((IEnumerator)Invoke(Controller("DirectedWaveAttackController", wave, settings),
                "FireFullAttack", enemy, executor, noBurst));
            Assert(executor.Directions.Count == 4, "Default fires the complete configured attack.");
            foreach (Vector3 shot in executor.Directions)
                Same(shot, Vector3.up, "Default uses pattern direction rather than aiming at the moving player.");
            object defaultEntrance = Controller("DirectedWaveEntranceAttackController", wave, settings);
            ((IDictionary)Get(defaultEntrance, "executors"))[enemy] = executor;
            Assert((bool)Invoke(defaultEntrance, "TryFire", enemy), "Default also fires during entrance.");
            Same(executor.Directions[4], Vector3.up, "Entrance Default uses pattern direction.");
            local.InitializeAttackPatternOverride(settings, noBurst);
            Assert(local.ResolveFireMode(DirectedWaveAttackFireMode.Aimed) == DirectedWaveAttackFireMode.Default,
                "Local overrides preserve Default.");
            Set(local, "overrideAttackPattern", false);
            Set(settings, "fireMode", DirectedWaveAttackFireMode.Aimed);
            Set(burst, "burstShotInterval", .2f);
            Set(burst, "attackShotInterval", .5f);
            burst.Validate();
            Assert(!DirectedWaveAttackAimState.RequiresBurstWarning(DirectedWaveAttackAimMode.AimOnBurst, burst),
                "Enabled bursts do not produce a warning.");
            float first = EnemyShootingPreview.GetAimCaptureTime(burst, 0, DirectedWaveAttackAimMode.AimOnBurst, .1f);
            float within = EnemyShootingPreview.GetAimCaptureTime(burst, 0, DirectedWaveAttackAimMode.AimOnBurst, .3f);
            SameScalar(first, within, "Preview holds aim inside burst.");
            Assert(EnemyShootingPreview.GetAimCaptureTime(burst, 0, DirectedWaveAttackAimMode.AimOnBurst, 1f) > first,
                "Preview updates aim at the next burst.");
            SameScalar(EnemyShootingPreview.GetAimCaptureTime(burst, 0, DirectedWaveAttackAimMode.AimOnStart, 1f),
                0f, "Preview holds Aim On Start for the entire attack.");
            SameScalar(EnemyShootingPreview.GetAimCaptureTime(burst, 0, DirectedWaveAttackAimMode.AimContinuous, .3f),
                .3f, "Preview tracks the current position for continuous aim.");

            local.InitializeAttackPatternOverride(settings, burst);
            Assert(local.ResolveAimMode(DirectedWaveAttackAimMode.AimOnStart) == DirectedWaveAttackAimMode.AimOnBurst,
                "Local override inherits mode on initialization.");
            Set(local, "inheritWaveFireMode", true);
            Assert(local.ResolveAimMode(DirectedWaveAttackAimMode.AimOnStart) == DirectedWaveAttackAimMode.AimOnStart,
                "Migrated local override can inherit shared aim mode.");
            Set(local, "inheritWaveFireMode", false);
            Set(local, "overrideAttackPattern", false);
            Assert(local.ResolveAimMode(DirectedWaveAttackAimMode.AimOnStart) == DirectedWaveAttackAimMode.AimOnStart,
                "Disabled local override leaves shared aim mode intact.");
            var copy = new DirectedWaveAttackSettings();
            copy.CopyFrom(settings);
            Assert(copy.AimMode == settings.AimMode, "Preset/profile copying preserves mode.");
            Assert(new DirectedWaveAttackSettings().AimMode == DirectedWaveAttackAimMode.AimOnStart,
                "Old serialized configurations get Aim On Start without renaming fields.");
            Assert(new SerializedObject(local).FindProperty("aimMode") != null, "Local mode is serialized.");
            return new { passed = true, checks, tested = "single/multiple, post/entrance, moving player/enemy, burst boundaries, resets, warning, preview, local overrides, copying" };
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
            UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene);
        }
    }

    private static void VerifyMultiple(DirectedEnemySubWave wave, Enemy enemy, RecordingExecutor executor,
        DirectedWaveAttackSettings settings, DirectedWaveAttackAimMode mode)
    {
        var profile = Profile(true, 2, 2);
        profile.EnableMultiplePatterns();
        var entries = (IList)Get(profile, "patterns");
        var other = new EnemyAttackPatternSettings();
        other.CopyFrom(Profile(true, 2, 3));
        entries.Add(other);
        Set(settings, "waveBurstSettings", profile);
        Reset(executor);
        var controller = Controller("DirectedWaveAttackController", wave, settings);
        RunIterator((IEnumerator)Invoke(controller, "FireFullAttack", enemy, executor, profile));
        Assert(executor.Directions.Count == 10, "Parallel patterns fire their configured shot counts.");
        if (mode == DirectedWaveAttackAimMode.AimContinuous)
        {
            for (int i = 1; i < executor.Directions.Count; i++)
                VerifyWithinBurst(executor.Directions[i - 1], executor.Directions[i], mode);
            return;
        }
        if (mode == DirectedWaveAttackAimMode.AimOnStart)
        {
            foreach (Vector3 shot in executor.Directions) Same(shot, executor.Directions[0], "All patterns share attack aim.");
            return;
        }
        for (int p = 0; p < profile.PatternCount; p++)
        {
            var shots = new List<Vector3>();
            for (int i = 0; i < executor.Patterns.Count; i++)
                if (ReferenceEquals(executor.Patterns[i], profile.GetPattern(p))) shots.Add(executor.Directions[i]);
            int count = profile.GetPattern(p).BurstShotCount;
            for (int i = 0; i < shots.Count; i++) Same(shots[i], shots[(i / count) * count], "Each stream keeps its own burst aim.");
            Assert(Vector3.Dot(shots[0], shots[count]) < .99999f, "Each stream reacquires for next burst.");
        }
    }

    private static EnemyBurstAttackSettings Profile(bool burst, int attacks, int shots)
    {
        var profile = new EnemyBurstAttackSettings();
        profile.Validate();
        Set(profile, "burstAttack", burst ? EnemyBurstAttackMode.Enabled : EnemyBurstAttackMode.None);
        Set(profile, "attackShotCount", attacks);
        Set(profile, "burstShotCount", shots);
        Set(profile, "attackShotInterval", 0f);
        Set(profile, "burstShotInterval", 0f);
        Set(profile, "attackCooldown", 3f);
        profile.Validate();
        return profile;
    }
    private static void Reset(RecordingExecutor executor)
    {
        executor.Directions.Clear(); executor.Patterns.Clear();
        executor.Enemy.transform.position = Vector3.zero;
        executor.Player.position = new Vector3(0f, -5f, 0f);
    }
    private static object Controller(string name, DirectedEnemySubWave wave, DirectedWaveAttackSettings settings)
        => Activator.CreateInstance(typeof(DirectedEnemySubWave).Assembly.GetType(name, true),
            name == "DirectedWaveAttackController" ? new object[] { wave, settings, null }
                : new object[] { wave, settings, new DirectedWaveEntranceAttackSettings() });
    private static Transform Child(Transform parent, string name)
    {
        var go = new GameObject(name); go.transform.SetParent(parent, false); return go.transform;
    }
    private static void RunIterator(IEnumerator routine)
    {
        int guard = 0;
        while (routine.MoveNext())
        {
            if (++guard > 100) throw new Exception("Unexpected timed wait in zero-interval test.");
            if (routine.Current is IEnumerator nested) RunIterator(nested);
        }
    }
    private static void Set(object target, string name, object value)
    {
        var field = target.GetType().GetField(name, Private);
        if (field != null) field.SetValue(target, value);
        else target.GetType().GetProperty(name, Private).SetValue(target, value);
    }
    private static object Get(object target, string name) => target.GetType().GetField(name, Private).GetValue(target);
    private static object Invoke(object target, string name, params object[] args) => target.GetType().GetMethod(name, Private).Invoke(target, args);
    private static void Same(Vector3 a, Vector3 b, string message) => Assert((a - b).sqrMagnitude < .00001f, message);
    private static void VerifyWithinBurst(Vector3 a, Vector3 b, DirectedWaveAttackAimMode mode)
    {
        if (mode == DirectedWaveAttackAimMode.AimContinuous)
            Assert(Vector3.Dot(a, b) < .99999f, "Continuous aim tracks movement on each shot.");
        else
            Same(a, b, "Locked modes keep direction inside the burst.");
    }
    private static void SameScalar(float a, float b, string message) => Assert(Mathf.Abs(a - b) < .00001f, message);
    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
        checks++;
    }
}
