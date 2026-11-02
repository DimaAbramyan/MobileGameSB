using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Zenject;

public sealed class BossInspectorProbeWindow : EditorWindow
{
    public Action Draw;
    public Exception Error;
    public int Repaints;
    Vector2 scroll;
    void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);
        try { Draw?.Invoke(); }
        catch (Exception e) { Error = e; }
        finally { EditorGUILayout.EndScrollView(); }
        if (Event.current.type == EventType.Repaint) Repaints++;
    }
}

// Focused, isolated production-executor tests. No wave, prefab or save is written.
public static class VerifyBossSharedAttacks
{
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    static int checks;
    static void Check(bool value, string message) { checks++; if (!value) throw new Exception(message); }
    static FieldInfo Field(object o, string name)
    {
        for (var t = o.GetType(); t != null; t = t.BaseType)
        {
            var f = t.GetField(name, Flags); if (f != null) return f;
        }
        throw new Exception("Missing field " + name);
    }
    static void Set(object o, string name, object value) => Field(o, name).SetValue(o, value);
    static T Get<T>(object o, string name) => (T)Field(o, name).GetValue(o);
    static void Call(object o, string method, params object[] args) => o.GetType().GetMethod(method, Flags).Invoke(o, args);

    sealed class Target : IEnemyEmissionOriginContext
    {
        public Vector3 AttackTargetPosition { get; set; } = new(0, -3, 0);
        public bool HasEmissionOriginPlayer => true;
        public Camera EmissionOriginCamera => null;
    }
    sealed class Context : IDisposable
    {
        public readonly EnemyProjectilePreviewWorld World = new();
        public readonly GameObject Root;
        public readonly BossController Boss;
        public readonly SimpleEnemyAttackExecutor Executor;
        public readonly EnemyProjectileEcsSpawner Spawner;
        public readonly DiContainer Container = new();
        public readonly Target Aim = new();
        public readonly BossAttackDriver Driver;
        public EntityManager Manager => World.SimulationWorld.EntityManager;
        public Context()
        {
            World.SetVisible(false);
            Spawner = Get<EnemyProjectileEcsSpawner>(World, "spawner");
            Container.Bind<EnemyManager>().FromInstance(new EnemyManager());
            Container.Bind<EnemyProjectileEcsSpawner>().FromInstance(Spawner);
            Root = new GameObject("Boss integration probe") { hideFlags = HideFlags.HideAndDontSave };
            Root.SetActive(false);
            Boss = Root.AddComponent<BossController>();
            Check(Root.GetComponents<Enemy>().Length == 1, "RequireComponent must not create a second Enemy");
            Set(Boss, "DoHaveBuff", false); Set(Boss, "_maxHealth", 100f);
            Set(Boss, "phases", new[] { Phase(Pattern()) });
            Executor = Root.GetComponent<SimpleEnemyAttackExecutor>();
            var bullet = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefub/Enemy Projectiles/Bullet.prefab").GetComponent<EnemyBullet>();
            Set(Executor, "defaultProjectilePrefab", bullet);
            Set(Executor, "homingProjectilePrefab", bullet);
            Set(Executor, "burstAtPointProjectilePrefab", bullet);
            Container.InjectGameObject(Root);
            Root.SetActive(true);
            if (!Application.isPlaying) { Call(Executor, "Awake"); Boss.Awake(); }
            Driver = new BossAttackDriver(Boss, Executor, Aim);
        }
        public void Tick(float time) => Driver.Tick(time);
        public void Sectors(float time) => Call(Executor, "TickPendingSectors", time);
        public int Count => World.Count;
        public void Dispose()
        {
            Driver.Stop();
            if (Root != null) UnityEngine.Object.DestroyImmediate(Root);
            World.Dispose();
        }
    }

    static DirectedWaveAttackPattern Pattern(int count = 1, EnemyProjectileType type = EnemyProjectileType.Default)
    {
        var p = new DirectedWaveAttackPattern();
        var s = Get<DirectedWaveAttackSettings>(p, "attackSettings");
        Set(s, "burstSettingsSource", DirectedWaveBurstSettingsSource.WaveOverride);
        Set(s, "fireMode", DirectedWaveAttackFireMode.Aimed);
        var profile = s.WaveBurstSettings;
        Set(profile, "attackCooldown", 0f);
        profile.Shooting.ConfigureLegacyFan(count, -30, 30);
        profile.Shooting.SetProjectileType(type);
        profile.Shooting.SetScreenBoundary(new EnemyProjectileScreenSettings(false));
        profile.Shooting.SetProjectileBaseSpeedOverride(true, 1f);
        Set(profile.Shooting, "overrideProjectileLifetime", true);
        Set(profile.Shooting, "projectileLifetime", 30f);
        profile.Validate();
        return p;
    }
    static EnemyBurstAttackSettings Profile(DirectedWaveAttackPattern pattern)
        => Get<DirectedWaveAttackSettings>(pattern, "attackSettings").WaveBurstSettings;
    static BossPhaseDefinition Phase(params DirectedWaveAttackPattern[] patterns)
    {
        var p = new BossPhaseDefinition(); Set(p, "startDelay", 0f); Set(p, "delayBetweenAttacks", 0f); Set(p, "attacks", patterns); return p;
    }
    static DirectedWaveAttackSequenceStep Step(DirectedWaveAttackPattern p)
        => new() { pattern = p, name = "Shared attack" };

    public static object Run()
    {
        checks = 0; int worlds = Unity.Entities.World.All.Count;
        using (var c = new Context())
        {
            c.Driver.Begin(Phase(Pattern(3), Pattern(5)), 0f);
            c.Tick(0); c.Tick(.01f); Check(c.Count == 3, "Sequential first Fan uses ECS");
            c.Tick(.02f); Check(c.Count == 8, "Sequential next attack");
            c.Tick(.03f); Check(c.Count == 11, "Sequential wraps to first attack");
            var snapshot = JsonUtility.ToJson(c.Boss.Phases[0]);
            Check(snapshot.Contains("attackSettings") && !snapshot.Contains("BossRadialAttackPattern"), "Phases serialize shared attack data");
        }
        var order = new List<int>();
        for (int run = 0; run < 2; run++)
        {
            using var c = new Context();
            var phase = Phase(Pattern(1), Pattern(2), Pattern(3)); Set(phase, "selectionMode", BossAttackSelectionMode.Random);
            c.Driver.Begin(phase, 0); c.Tick(0); int previous = 0;
            for (int i = 0; i < 8; i++)
            {
                c.Tick((i + 1) * .02f); int emitted = c.Count - previous; previous = c.Count;
                if (run == 0) order.Add(emitted); else Check(order[i] == emitted, "Random phase selection repeats for the same seed");
            }
            Check(previous > 8, "Random phase actually emits different configured patterns");
        }
        using (var c = new Context())
        {
            var p = Pattern(); Set(Profile(p), "burstAttack", EnemyBurstAttackMode.Enabled);
            Set(Profile(p), "burstShotCount", 3); Set(Profile(p), "burstShotInterval", .2f);
            var phase = Phase(p); Set(phase, "startDelay", .5f);
            c.Driver.Begin(phase, 0); c.Tick(.4f); Check(c.Count == 0, "Phase start delay");
            c.Tick(.5f); c.Tick(.51f); Check(c.Count == 1, "Burst begins after phase delay");
            c.Tick(.72f); Check(c.Count == 2, "Shared Burst interval");
            c.Driver.Stop(); c.Tick(5); Check(c.Count == 2, "Stopping cancels remaining Burst shots");
        }
        using (var c = new Context())
        {
            var sector = Pattern(); Profile(sector).Shooting.EmissionLayout.ConfigureSector(-40, 40, 5,
                EnemySectorLaunchMode.Sequential, 0, .3f);
            c.Driver.Begin(Phase(sector), 0); c.Tick(0); c.Tick(.01f);
            var resolved = Get<EnemyBurstAttackSettings>(Get<object>(Get<object>(c.Driver, "active"), "attack"), "Profile");
            Check(c.Executor.HasPendingEmissions(resolved), "Sequential Sector is awaited by the boss run");
            int count = c.Count;
            c.Driver.Begin(Phase(Pattern(2)), .02f);
            c.Sectors(c.Boss.AttackTime + 10f);
            Check(c.Count == count, "Phase switch cancels old deferred Sectors");
            c.Tick(.02f); c.Tick(.03f);
            Check(c.Count == count + 2, "New phase fires while old projectiles remain");
        }
        using (var c = new Context())
        {
            var attack = Pattern();
            var other = new EnemyAttackPatternSettings(); other.CopyFrom(Profile(Pattern(2)));
            Profile(attack).EnableMultiplePatterns(); Profile(attack).AddPattern(other);
            c.Driver.Begin(Phase(attack), 0); c.Tick(0); c.Tick(.01f);
            Check(c.Count == 3, "Single/Multiple shared profiles both work on a boss");
        }
        using (var c = new Context())
        {
            var phase = Phase(); Set(phase, "executionMode", DirectedWaveAttackExecutionMode.Sequence);
            phase.Sequence.steps.Add(Step(Pattern(2)));
            phase.Sequence.steps.Add(new() { type = DirectedWaveAttackSequenceStepType.Pause, pauseDuration = .2f });
            var block = new DirectedWaveAttackSequenceStep { type = DirectedWaveAttackSequenceStepType.RepeatBlock, repeatCount = 2 };
            block.steps.Add(Step(Pattern())); phase.Sequence.steps.Add(block);
            c.Driver.Begin(phase, 0);
            for (int i = 0; i < 100; i++) c.Tick(i * .01f);
            Check(c.Driver.SequenceComplete && c.Count == 4, "Shared Sequence Once + Pause + nested Repeat Block");
            c.Tick(10); Check(c.Count == 4, "Completed Sequence does not restart implicitly");
        }
        using (var c = new Context())
        {
            var p = Pattern();
            var child = new EnemyChildAttackSettings(); Set(child, "triggerValue", .05f); Set(child, "repeatInterval", .1f);
            child.Pattern.Shooting.SetScreenBoundary(new EnemyProjectileScreenSettings(false));
            Profile(p).Shooting.SetChildAttack(child);
            c.Driver.Begin(Phase(p), 0); c.Tick(0); c.Tick(.01f); c.Driver.Stop();
            Check(c.Count == 1, "Stopping the boss leaves an emitted Child Attack parent alive");
            c.World.Step(.06f, .06f, new Vector3(1000, 1000));
            Check(c.Count > 1, "Already spawned parents continue their own Child Attack after phase stop");
        }
        using (var c = new Context())
        {
            var p = Pattern(1, EnemyProjectileType.CompositeCircle);
            Set(Profile(p).Shooting.Composite, "componentCount", 4);
            c.Driver.Begin(Phase(p), 0); c.Tick(0); c.Tick(.01f);
            using var q = c.Manager.CreateEntityQuery(typeof(EnemyCompositePart));
            Check(q.CalculateEntityCount() == 4, "Boss uses existing Composite ECS parts");
        }
        using (var c = new Context())
        {
            var p = Pattern(1, EnemyProjectileType.Laser);
            var laser = Profile(p).Shooting.Laser;
            Set(laser.Geometry, "lengthMode", EnemyLaserLengthMode.Custom);
            c.Driver.Begin(Phase(p), 0); c.Tick(0); c.Tick(.01f);
            Check(c.Spawner.ActiveLasers == 1, "Boss launches existing Laser/Telegraph lifecycle");
        }
        var attackPreset = ScriptableObject.CreateInstance<EnemyAttackPatternPreset>();
        var projectilePreset = ScriptableObject.CreateInstance<EnemyProjectilePreset>();
        try
        {
            attackPreset.SetSettings(Profile(Pattern(3)), new EnemyPresetFiringSettings
                { enabled = true, fireMode = DirectedWaveAttackFireMode.Default });
            var projectile = Profile(Pattern()).Shooting;
            Set(projectile, "overrideBaseSize", true); Set(projectile, "baseSize", 2f);
            projectile.ProjectileBaseSpeedRandom.enabled = true;
            projectile.ProjectileBaseSpeedRandom.min = projectile.ProjectileBaseSpeedRandom.max = 2.75f;
            projectilePreset.SetSettings(projectile);
            using var c = new Context();
            var p = Pattern(); Profile(p).AuthoredShooting.LinkPattern(attackPreset); Profile(p).AuthoredShooting.LinkProjectile(projectilePreset);
            c.Driver.Begin(Phase(p), 0); c.Tick(0); c.Tick(.01f);
            Check(c.Count == 3, "Linked Attack Pattern preset supplies Fan and firing policy");
            using var q = c.Manager.CreateEntityQuery(typeof(EnemyProjectileMovement), typeof(LocalTransform));
            using var bullets = q.ToEntityArray(Allocator.Temp);
            foreach (var e in bullets)
            {
                Check(Mathf.Approximately(c.Manager.GetComponentData<LocalTransform>(e).Scale, 2f), "Linked Projectile preset supplies base size");
                float speed = Unity.Mathematics.math.length(c.Manager.GetComponentData<EnemyProjectileVelocity>(e).Value);
                Check(Mathf.Approximately(speed, 2.75f), "Shared numeric speed sampling uses the configured range, actual=" + speed);
            }
        }
        finally { UnityEngine.Object.DestroyImmediate(attackPreset); UnityEngine.Object.DestroyImmediate(projectilePreset); }
        using (var c = new Context())
        {
            var p = Pattern(); var child = new EnemyChildAttackSettings();
            Set(child, "triggerValue", 10f); Profile(p).Shooting.SetChildAttack(child);
            var phase = Phase(); Set(phase, "executionMode", DirectedWaveAttackExecutionMode.Sequence);
            var first = Step(p); first.waitForChildAttacks = true; first.childWaitTimeout = .15f;
            phase.Sequence.steps.Add(first); phase.Sequence.steps.Add(Step(Pattern()));
            c.Driver.Begin(phase, 0); c.Tick(0); c.Tick(.01f); c.Tick(.1f);
            Check(c.Count == 1, "Explicit child completion wait holds next Sequence attack");
            for (int i = 20; i < 40; i++) c.Tick(i * .01f);
            Check(c.Count == 2 && c.Driver.SequenceComplete, "Finite shared child timeout prevents an infinite child effect blocking the boss");
        }
        if (Application.isPlaying) CheckBossLifecycle();
        Check(Unity.Entities.World.All.Count == worlds, "All isolated ECS worlds are disposed");
        return new { passed = true, checks, playMode = Application.isPlaying, ecsExecutor = true,
            sequence = true, composite = true, child = true, laser = true };
    }

    static void CheckBossLifecycle()
    {
        using var c = new Context();
        var first = Phase(Pattern()); Set(first, "nextPhaseHealthThresholdPercent", 70f);
        var second = Phase(Pattern(2)); Set(second, "nextPhaseHealthThresholdPercent", 30f);
        var third = Phase(Pattern(3));
        Set(c.Boss, "phases", new[] { first, second, third });
        int changes = 0, defeated = 0;
        c.Boss.PhaseChanged += (_, _) => changes++;
        c.Boss.Defeated += _ => defeated++;
        Call(c.Boss, "Start");
        c.Boss.TakeDamageIgnoringShield(40);
        Check(c.Boss.CurrentPhaseIndex == 1 && changes == 2, "Real hull damage triggers HP phases");
        c.Boss.TakeDamageIgnoringShield(35);
        Check(c.Boss.CurrentPhaseIndex == 2, "Next HP threshold");
        var bossDriver = Get<BossAttackDriver>(c.Boss, "attacks");
        float time = c.Boss.AttackTime;
        bossDriver.Tick(time); bossDriver.Tick(time + .01f);
        int before = c.Count;
        c.Boss.Dying(); c.Boss.Dying();
        bossDriver.Tick(time + 100f);
        Check(defeated == 1 && c.Boss.isDead && c.Count == before, "Boss death stops schedules and notifies once");

        // Exercise the actual encounter factory and its Zenject injection path.
        var fightObject = new GameObject("BossFight probe") { hideFlags = HideFlags.HideAndDontSave };
        var template = new GameObject("Boss template probe") { hideFlags = HideFlags.HideAndDontSave };
        template.SetActive(false);
        try
        {
            var prefab = template.AddComponent<BossController>(); Set(prefab, "DoHaveBuff", false); Set(prefab, "_maxHealth", 100f);
            Set(prefab, "phases", new[] { Phase(Pattern()), Phase(Pattern(2)) });
            var executor = template.GetComponent<SimpleEnemyAttackExecutor>();
            Set(executor, "defaultProjectilePrefab", Get<EnemyBullet>(c.Executor, "defaultProjectilePrefab"));
            var fight = fightObject.AddComponent<BossFight>();
            Set(fight, "bossPrefab", prefab); Set(fight, "destroyEncounterOnDefeat", false);
            c.Container.Inject(fight); int spawns = 0, completions = 0;
            fight.BossSpawned += _ => spawns++; fight.BossDefeated += () => completions++;
            fight.Init(null); var spawned = fight.ActiveBoss; fight.Init(null);
            Check(spawns == 1 && spawned != null, "BossFight repeated Init cannot create a second boss");
            spawned.gameObject.SetActive(true); spawned.Dying();
            Check(completions == 1 && Get<bool>(fight, "completed"), "Injected BossFight completes on death");
            UnityEngine.Object.DestroyImmediate(spawned.gameObject);
        }
        finally { UnityEngine.Object.DestroyImmediate(fightObject); UnityEngine.Object.DestroyImmediate(template); }
    }

    public static object Inspector()
    {
        checks = 0;
        var scene = EditorSceneManager.NewPreviewScene();
        var root = new GameObject("Boss Inspector probe");
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, scene);
        root.SetActive(false);
        try
        {
            var boss = root.AddComponent<BossController>();
            using var serialized = new SerializedObject(boss);
            var phases = serialized.FindProperty("phases"); phases.arraySize = 2;
            phases.GetArrayElementAtIndex(0).boxedValue = Phase(Pattern(3));
            phases.GetArrayElementAtIndex(1).boxedValue = Phase(Pattern(5));
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Check(boss.Phases[0].Attacks.Length == 1, "Serialized phase add and boxed assignment");
            var sequence = phases.GetArrayElementAtIndex(0).FindPropertyRelative("sequence.steps");
            sequence.arraySize = 1; sequence.GetArrayElementAtIndex(0).managedReferenceValue = Step(Pattern());
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Check(boss.Phases[0].Sequence.steps.Count == 1, "Managed Sequence steps editable through Inspector");
            Undo.IncrementCurrentGroup();
            phases.MoveArrayElement(0, 1); serialized.ApplyModifiedProperties(); Undo.FlushUndoRecordObjects();
            Check(boss.Phases[1].Sequence.steps.Count == 1, "Phase reordering retains nested data");
            Undo.PerformUndo(); Check(boss.Phases[0].Sequence.steps.Count == 1, "Undo restores phase order");
            Undo.PerformRedo(); Check(boss.Phases[1].Sequence.steps.Count == 1, "Redo restores phase order");
            var editor = UnityEditor.Editor.CreateEditor(boss);
            Check(editor is BossControllerEditor, "Boss uses custom shared-pattern Inspector");
            UnityEngine.Object.DestroyImmediate(editor);
            return new { passed = true, checks, undo = true, phases = true, sharedSequence = true };
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
    }

    public static async Task<object> Gui()
    {
        if (Application.isPlaying) throw new Exception("Stop Play Mode for Scene View rendering");
        checks = 0;
        var scene = EditorSceneManager.NewPreviewScene();
        var root = new GameObject("Boss GUI probe");
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, scene); root.SetActive(false);
        var boss = root.AddComponent<BossController>();
        var patterns = new[] { Pattern(7), Pattern(), Pattern(3), Pattern(), Pattern(1, EnemyProjectileType.CompositeCircle), Pattern(1, EnemyProjectileType.Laser) };
        Profile(patterns[1]).Shooting.EmissionLayout.Configure(EnemyEmissionLayoutType.Radial, 6);
        Profile(patterns[2]).Shooting.EmissionLayout.Configure(EnemyEmissionLayoutType.Circle, 5, 1f);
        Profile(patterns[3]).Shooting.EmissionLayout.ConfigureSector(-40, 40, 5, EnemySectorLaunchMode.Sequential);
        Set(Profile(patterns[0]).Shooting, "layoutRotationEnabled", true);
        Set(boss, "phases", new[] { Phase(patterns), Phase() });
        Set(boss.Phases[1], "executionMode", DirectedWaveAttackExecutionMode.Sequence);
        var block = new DirectedWaveAttackSequenceStep { type = DirectedWaveAttackSequenceStepType.RepeatBlock, repeatCount = 2 };
        block.steps.Add(Step(Pattern())); boss.Phases[1].Sequence.steps.Add(block);
        boss.Phases[1].Sequence.steps.Add(new() { type = DirectedWaveAttackSequenceStepType.Pause });
        var executor = root.GetComponent<SimpleEnemyAttackExecutor>();
        Set(executor, "defaultProjectilePrefab", AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefub/Enemy Projectiles/Bullet.prefab").GetComponent<EnemyBullet>());
        var editor = UnityEditor.Editor.CreateEditor(boss);
        var window = ScriptableObject.CreateInstance<BossInspectorProbeWindow>();
        var view = SceneView.lastActiveSceneView;
        bool previous = EnemyShootingPreview.ShowFormationVolley;
        Exception sceneError = null; int guides = 0;
        Action<SceneView> draw = v =>
        {
            if (v != view || Event.current.type != EventType.Repaint || guides >= patterns.Length) return;
            try
            {
                Set(editor, "previewPath", $"phases.Array.data[0].attacks.Array.data[{guides}]");
                Call(editor, "OnSceneGUI"); guides++;
            }
            catch (Exception e) { sceneError = e; }
        };
        try
        {
            using var serialized = new SerializedObject(boss);
            serialized.FindProperty("phases.Array.data[0]").isExpanded = true;
            serialized.FindProperty("phases.Array.data[0].attacks.Array.data[0]").isExpanded = true;
            serialized.FindProperty("phases.Array.data[1]").isExpanded = true;
            serialized.FindProperty("phases.Array.data[1].sequence.steps.Array.data[0]").isExpanded = true;
            serialized.FindProperty("phases.Array.data[1].sequence.steps.Array.data[0].steps.Array.data[0]").isExpanded = true;
            Set(editor, "emitterSettings", true);
            EnemyShootingPreview.ShowFormationVolley = true;
            SceneView.duringSceneGui += draw;
            window.Draw = editor.OnInspectorGUI;
            window.position = new Rect(120, 120, 620, 740); window.ShowUtility();
            for (int i = 0; i < 80 && (window.Repaints < 2 || guides < patterns.Length); i++)
            { window.Repaint(); view?.Repaint(); await Task.Delay(50); }
            if (window.Error != null) throw window.Error;
            if (sceneError != null) throw sceneError;
            Check(window.Repaints >= 2, "Actual IMGUI renders expanded phases, shared profile and Repeat/Sequence controls");
            Check(guides == patterns.Length, "Actual Scene View renders Fan, Radial, Circle, Sector, Composite and Laser using shared preview");
            return new { passed = true, checks, inspectorRepaints = window.Repaints, previewLayouts = guides };
        }
        finally
        {
            SceneView.duringSceneGui -= draw;
            EnemyShootingPreview.ShowFormationVolley = previous;
            window.Draw = null; window.Close(); UnityEngine.Object.DestroyImmediate(editor);
            EditorSceneManager.ClosePreviewScene(scene);
        }
    }
}
