using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class VerifyAttackSequenceRuntime
{
    private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    private static int checks;
    private sealed class Executor : IEnemyBurstAttackExecutor, IEnemyPendingEmissionExecutor
    {
        public EnemyBurstAttackSettings BurstAttackSettings { get; set; }
        public bool CanPerformWaveAttack => true;
        public int Shots;
        public bool Pending = true;
        public void SetWaveAttackControl(bool controlled) { }
        public bool TryFireAt(Vector3 point, EnemyBurstAttackSettings settings) { Shots++; return true; }
        public bool TryFireInDirection(Vector3 direction, EnemyBurstAttackSettings settings) { Shots++; return true; }
        public bool HasPendingEmissions(EnemyBurstAttackSettings settings) => Pending;
    }
    public static object Run()
    {
        checks = 0;
        var root = PrefabUtility.LoadPrefabContents("Assets/Waves/2/2_3_extrime.prefab");
        var enemyObject = new GameObject("Sequence runtime verification"); enemyObject.SetActive(false);
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(enemyObject, root.scene);
        try
        {
            var wave = root.transform.Find("Main").GetComponent<DirectedEnemySubWave>();
            var enemy = enemyObject.AddComponent<Enemy>();
            var settings = new DirectedWaveAttackSettings();
            Set(settings, "fireMode", DirectedWaveAttackFireMode.Default);
            Set(settings, "burstSettingsSource", DirectedWaveBurstSettingsSource.WaveOverride);
            var profile = settings.WaveBurstSettings;
            profile.ConfigureLegacyRepeatedBurst(3, .4f, .2f);
            Set(profile, "burstAttack", EnemyBurstAttackMode.Enabled);
            Set(profile, "attackShotCount", 2); Set(profile, "attackShotInterval", .5f);
            Set(profile, "burstShotCount", 3); Set(profile, "burstShotInterval", .2f); Set(profile, "attackCooldown", .4f);
            var executor = new Executor { BurstAttackSettings = profile };
            var type = typeof(DirectedEnemySubWave).Assembly.GetType("DirectedWaveAttackController", true);
            var controller = Activator.CreateInstance(type, new object[] { wave, settings, null });
            Invoke(controller, "BeginSingleCycle", 2f, new EnemyAttackCompletionTracker());
            var resolved = (EnemyBurstAttackSettings)Invoke(controller, "GetBurstSettings", enemy, executor);
            Check(resolved != profile && resolved.AttackShotCount == 2 && resolved.BurstShotCount == 3,
                "Finite controller scales a private copy without changing counts");
            var routine = (IEnumerator)Invoke(controller, "FireFullAttack", enemy, executor, resolved);
            var stack = new Stack<IEnumerator>(); stack.Push(routine);
            var times = new[] { 0f, .1001f, .2002f, .4503f, .5504f, .6505f };
            for (int i = 0; i < times.Length; i++)
            {
                Clock(enemy, times[i]); Advance(stack);
                Check(executor.Shots == i + 1, "Runtime Burst timing at shot " + i);
            }
            Check(stack.Count > 0, "Sequential Sector pending emissions block completion");
            executor.Pending = false; Advance(stack);
            Check(stack.Count == 0, "Emission completion releases root attack");
            var cooldowns = (Dictionary<Enemy, float>)Get(controller, "nextAttackReadyTimes");
            Check(Mathf.Abs(cooldowns[enemy] - .8505f) < .001f, "Scaled Attack Cooldown preserved");
            Invoke(controller, "Stop");
            Invoke(controller, "BeginSingleCycle", 1f, new EnemyAttackCompletionTracker());
            Invoke(controller, "BuildAttackCycle");
            Check((bool)type.GetProperty("HasCompletedSingleCycle").GetValue(controller), "Empty/dead target cycle finishes");
            Check(!(bool)Invoke(controller, "BuildAttackCycle"), "Single cycle cannot refill forever");
            Invoke(controller, "Stop");

            var simple = enemyObject.AddComponent<SimpleEnemyAttackExecutor>();
            Invoke(simple, "Awake"); enemyObject.SetActive(true);
            var alive = Get(wave, "aliveEnemies"); Invoke(alive, "Add", enemy);
            ((Dictionary<Enemy, int>)Get(wave, "formationIndices"))[enemy] = 0;
            var behaviour = wave.AttackPatternBehaviour; Set(behaviour, "executionMode", DirectedWaveAttackExecutionMode.Sequence);
            behaviour.AttackSequence.steps = new() { new DirectedWaveAttackSequenceStep {
                name = "Long pause", type = DirectedWaveAttackSequenceStepType.Pause, pauseDuration = 10f } };
            behaviour.AttackSequence.restartOnReentry = false;
            Set(wave, "runtimeTimelineRunning", true);
            Invoke(behaviour, "IDirectedWavePostTimelineBehaviour.OnPostTimelineStarted", wave);
            Invoke(behaviour, "IDirectedWavePostTimelineBehaviour.TickPostTimeline");
            Check((bool)Get(simple, "isWaveAttackControlled"), "Sequence reserves control during a pause");
            Invoke(behaviour, "OnDisable");
            Check(!(bool)Get(simple, "isWaveAttackControlled"), "Disabling releases autonomous control");
            Invoke(behaviour, "OnEnable");
            var player = Get(behaviour, "sequencePlayer");
            Check(!(bool)Get(player, "paused") && (bool)Get(simple, "isWaveAttackControlled"), "Reentry resumes execution and reservations");
            behaviour.AttackSequence.restartOnReentry = true;
            Invoke(behaviour, "OnDisable"); Invoke(behaviour, "OnEnable");
            Check(Get(player, "current") == null, "Restart On Reentry resets cursor");
            Invoke(behaviour, "OnDisable");
            Set(wave, "runtimeTimelineRunning", false);
            return new { passed = true, checks, finiteRuntimeController = true, sectorCompletion = true, reentry = true, pauseReservesSlots = true };
        }
        finally { UnityEngine.Object.DestroyImmediate(enemyObject); PrefabUtility.UnloadPrefabContents(root); }
    }
    private static void Advance(Stack<IEnumerator> stack)
    {
        for (int guard = 0; guard < 100 && stack.Count > 0; guard++)
        {
            var iterator = stack.Peek();
            if (!iterator.MoveNext()) { stack.Pop(); continue; }
            if (iterator.Current is IEnumerator nested) stack.Push(nested); else return;
        }
    }
    private static void Clock(Enemy enemy, float time)
    {
        object clock = new EnemyAttackClock(); Set(clock, "initialized", true);
        Set(clock, "previousTime", Time.time); Set(clock, "attackTime", time); Set(enemy, "attackClock", clock);
    }
    private static void Check(bool value, string message) { checks++; if (!value) throw new Exception(message); }
    private static object Invoke(object value, string name, params object[] args) => value.GetType().GetMethod(name, Fields).Invoke(value, args);
    private static object Get(object value, string name) => value.GetType().GetField(name, Fields).GetValue(value);
    private static void Set(object value, string name, object field) => value.GetType().GetField(name, Fields).SetValue(value, field);
}
