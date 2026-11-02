using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class VerifyAttackOrder
{
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    private static int checks;
    private static void Check(bool value, string message) { checks++; if (!value) throw new Exception(message); }
    private sealed class Executor : IEnemyBurstAttackExecutor
    {
        public int Shots;
        public EnemyBurstAttackSettings BurstAttackSettings { get; } = new();
        public bool CanPerformWaveAttack => true;
        public void SetWaveAttackControl(bool controlled) { }
        public bool TryFireAt(Vector3 point, EnemyBurstAttackSettings settings) { Shots++; return true; }
        public bool TryFireInDirection(Vector3 direction, EnemyBurstAttackSettings settings) { Shots++; return true; }
    }
    public static object Run()
    {
        checks = 0;
        var slots = new List<DirectedWaveAttackOrderSlot> {
            new(4, new Vector2(2, 0)), new(1, new Vector2(-1, 0)), new(3, new Vector2(1, 0)),
            new(0, new Vector2(-2, 0)), new(2, Vector2.zero) };
        var config = new DirectedWaveAttackOrderSettings { enabled = true };
        var plan = new DirectedWaveAttackOrderPlan();
        Test(plan, slots, config, DirectedWaveAttackOrderMode.Sequential, "0,1,2,3,4");
        Test(plan, slots, config, DirectedWaveAttackOrderMode.LeftToRight, "0,1,2,3,4");
        Test(plan, slots, config, DirectedWaveAttackOrderMode.RightToLeft, "4,3,2,1,0");
        Test(plan, slots, config, DirectedWaveAttackOrderMode.CenterToOutside, "2,1,3,0,4");
        Test(plan, slots, config, DirectedWaveAttackOrderMode.OutsideToCenter, "0,4,1,3,2");
        Test(plan, slots, config, DirectedWaveAttackOrderMode.PingPong, "0,1,2,3,4,3,2,1");
        Test(plan, slots, config, DirectedWaveAttackOrderMode.SymmetricalPairs, "0,4,1,3,2");
        Check(Batches(plan) == "0,0,1,1,2", "Atomic pairs with odd center");
        config.reverseOrder = true;
        Test(plan, slots, config, DirectedWaveAttackOrderMode.SymmetricalPairs, "2,1,3,0,4");
        Check(Batches(plan) == "0,1,1,2,2", "Reversed odd center must not split a pair");
        config.startSlot = 3;
        Test(plan, slots, config, DirectedWaveAttackOrderMode.SymmetricalPairs, "1,3,0,4,2");
        Check(Batches(plan) == "0,0,1,1,2", "Pair Start Slot preserves membership");
        config.reverseOrder = false; config.startSlot = -1;
        var grid = new List<DirectedWaveAttackOrderSlot> { new(0, new Vector2(-2, 1)), new(1, new Vector2(-2, -1)),
            new(2, new Vector2(2, 1)), new(3, new Vector2(2, -1)) };
        Test(plan, grid, config, DirectedWaveAttackOrderMode.SymmetricalPairs, "0,2,1,3");
        Check(Batches(plan) == "0,0,1,1", "Mirror pairs preserve Y instead of pairing opposite corners");
        config.orderSource = DirectedWaveAttackOrderSource.FormationSlotOrder;
        Test(plan, grid, config, DirectedWaveAttackOrderMode.Sequential, "0,1,2,3");
        config.orderSource = DirectedWaveAttackOrderSource.SpatialOrder;
        var crossed = new List<DirectedWaveAttackOrderSlot> { new(0, new Vector2(2, 1)), new(1, new Vector2(-2, 1)),
            new(2, new Vector2(1, 1)), new(3, new Vector2(-1, 1)) };
        Test(plan, crossed, config, DirectedWaveAttackOrderMode.Sequential, "1,3,2,0");
        config.resetOrderEachCycle = false;
        plan.BuildCycle(grid, config, 1); Check(Values(plan) == "1,3,2,0", "No reset retains first geometry snapshot");
        config.resetOrderEachCycle = true;
        plan.BuildCycle(grid, config, 1); Check(Values(plan) == "0,1,2,3", "Reset captures updated geometry");
        config.orderSource = DirectedWaveAttackOrderSource.FormationSlotOrder;
        var circle = new List<DirectedWaveAttackOrderSlot> { new(0, Vector2.up), new(1, Vector2.right), new(2, Vector2.down), new(3, Vector2.left) };
        Test(plan, circle, config, DirectedWaveAttackOrderMode.Clockwise, "0,1,2,3");
        Test(plan, circle, config, DirectedWaveAttackOrderMode.CounterClockwise, "0,3,2,1");
        config.enemiesPerBatch = 2;
        Test(plan, slots, config, DirectedWaveAttackOrderMode.Sequential, "0,1,2,3,4");
        Check(Batches(plan) == "0,0,1,1,2", "Batch size two");
        Test(plan, slots, config, DirectedWaveAttackOrderMode.Simultaneous, "0,1,2,3,4");
        Check(Batches(plan) == "0,0,0,0,0", "Simultaneous ignores batch size");
        config.enemiesPerBatch = 1; config.manualSlots = new() { 3, 1, 3, 900, 0 };
        Test(plan, slots, config, DirectedWaveAttackOrderMode.Manual, "3,1,3,0");
        config.startSlot = 1;
        Test(plan, slots, config, DirectedWaveAttackOrderMode.Manual, "1,3,0,3");
        config.startSlot = -1; config.mode = DirectedWaveAttackOrderMode.Random;
        var unityRandom = JsonUtility.ToJson(UnityEngine.Random.state);
        plan.Reset(); plan.BuildCycle(slots, config, 1); string first = Values(plan);
        slots.Reverse(); plan.Reset(); plan.BuildCycle(slots, config, 1);
        Check(Values(plan) == first, "Spawn/enumeration order cannot affect seeded random order");
        Check(JsonUtility.ToJson(UnityEngine.Random.state) == unityRandom, "Does not consume global Unity Random");
        plan.BuildCycle(slots, config, 1); Check(Values(plan) == first, "Reset Each Cycle resets random seed");
        config.resetOrderEachCycle = false; bool different = false;
        for (int i = 0; i < 5; i++) { plan.BuildCycle(slots, config, 1); different |= Values(plan) != first; }
        Check(different, "Continuous random stream without resetting");
        config.loopOrder = false; Check(!plan.BuildCycle(slots, config, 1), "Loop Order false stops further cycles");
        config.loopOrder = true; config.resetOrderEachCycle = true; config.mode = DirectedWaveAttackOrderMode.Sequential;
        plan.Reset(); plan.BuildCycle(slots, config, 2); Check(plan.Entries.Count == 10, "Existing attacks-per-enemy rounds preserved");
        var copy = new DirectedWaveAttackSettings(); copy.AttackOrder.CopyFrom(config);
        var resolved = new DirectedWaveAttackSettings(); resolved.CopyFrom(copy);
        resolved.AttackOrder.manualSlots.Add(99); Check(!copy.AttackOrder.manualSlots.Contains(99), "Deep copy of manual order");
        resolved.ScaleSequenceScheduling(2); Check(Mathf.Abs(resolved.AttackOrder.attackIntervalBetweenEnemies - .075f) < .0001f, "Sequence speed scales group interval");
        Check(!new DirectedWaveAttackSettings().UsesAttackOrder, "Old defaults remain disabled");
        Preview();
        RuntimeSkipping();
        return new { passed = true, checks, modes = 12, seedIndependentOfSpawn = true, runtimeDeathAndCooldown = true,
            realPreview = true, sequenceIntegration = true };
    }

    private static void Preview()
    {
        var root = PrefabUtility.LoadPrefabContents("Assets/Waves/2/2_3_extrime.prefab");
        try
        {
            var wave = root.transform.Find("Main").GetComponent<DirectedEnemySubWave>();
            var behaviour = wave.AttackPatternBehaviour;
            Set(behaviour, "executionMode", DirectedWaveAttackExecutionMode.Patterns);
            Set(behaviour, "attackPatterns", new List<DirectedWaveAttackPattern>());
            Set(behaviour, "attackPreset", null); Set(behaviour, "useSelectedEnemySlots", true);
            Set(behaviour, "selectedEnemySlots", new List<int> { 0, 1, 2, 3 });
            // Test order scheduling independently of this editable wave's Sector/child/random overrides.
            var settings = new DirectedWaveAttackSettings();
            Set(behaviour, "attackSettings", settings);
            Set(settings, "fireMode", DirectedWaveAttackFireMode.Default);
            Set(settings, "burstSettingsSource", DirectedWaveBurstSettingsSource.WaveOverride);
            Set(settings, "useAttackStartDelay", false);
            settings.WaveBurstSettings.ConfigureLegacySingleAttack(1, 0f, 0f);
            var order = settings.AttackOrder; order.enabled = true; order.mode = DirectedWaveAttackOrderMode.Manual;
            order.manualSlots = new() { 2, 0, 3 }; order.loopOrder = false;
            order.attackIntervalBetweenEnemies = .2f;
            using (var preview = new WaveShootingPreviewSession(wave, true, true, 0, Vector3.down * 4))
            {
                Advance(preview, .1f); var group = ((System.Collections.IList)Get(preview, "groups"))[0];
                var plan = (DirectedWaveAttackOrderPlan)Get(group, "OrderPlan");
                Check(plan.Cursor == 1 && Values(plan) == "2,0,3", "Manual Preview starts slot 2, independent of spawn order");
                Advance(preview, .27f); Check(plan.Cursor == 2, "Preview interval before second enemy");
                Advance(preview, 1f); Check(plan.Cursor == 3 && plan.CycleCount == 1, "Preview non-looping completion");
            }
            order.mode = DirectedWaveAttackOrderMode.Simultaneous;
            using (var preview = new WaveShootingPreviewSession(wave, true, true, 0, Vector3.down * 4))
            {
                Advance(preview, .1f); var group = ((System.Collections.IList)Get(preview, "groups"))[0];
                Check(((DirectedWaveAttackOrderPlan)Get(group, "OrderPlan")).Cursor == 4, "All selected enemies start together");
            }
            order.mode = DirectedWaveAttackOrderMode.Sequential; order.waitForPreviousEnemy = true;
            settings.WaveBurstSettings.ConfigureLegacySingleAttack(3, .4f, .2f);
            using (var preview = new WaveShootingPreviewSession(wave, true, true, 0, Vector3.down * 4))
            {
                Advance(preview, .7f); var group = ((System.Collections.IList)Get(preview, "groups"))[0];
                var plan = (DirectedWaveAttackOrderPlan)Get(group, "OrderPlan");
                Check(plan.Cursor == 1, "Previous batch waits for all shots and cooldown");
                Advance(preview, 1f); Check(plan.Cursor >= 2, "Next enemy starts after previous cooldown; cursor=" + plan.Cursor
                    + "; slots=" + DescribeSlots(group) + "; override=" + settings.OverridesEnemyAttackCooldown
                    + "; cooldown=" + settings.EnemyAttackCooldown);
            }
            Set(behaviour, "executionMode", DirectedWaveAttackExecutionMode.Sequence);
            var step = new DirectedWaveAttackSequenceStep { name = "Ordered slots" };
            var sequenceSettings = new DirectedWaveAttackSettings(); sequenceSettings.CopyFrom(settings);
            sequenceSettings.AttackOrder.waitForPreviousEnemy = false;
            sequenceSettings.AttackOrder.mode = DirectedWaveAttackOrderMode.Manual;
            sequenceSettings.AttackOrder.manualSlots = new() { 2 };
            sequenceSettings.WaveBurstSettings.ConfigureLegacySingleAttack(1, 0f, 0f);
            Set(step.pattern, "attackSettings", sequenceSettings);
            behaviour.AttackSequence.steps = new() { step, new DirectedWaveAttackSequenceStep { type = DirectedWaveAttackSequenceStepType.Pause, pauseDuration = .1f } };
            using (var preview = new WaveShootingPreviewSession(wave, true, true, 0, Vector3.down * 4))
            {
                Advance(preview, 1f);
                Check(((DirectedWaveAttackSequencePlayer)Get(preview, "sequencePlayer")).IsComplete,
                    "Sequence completion ignores slots intentionally omitted from Manual order");
            }
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
    private static void RuntimeSkipping()
    {
        var root = PrefabUtility.LoadPrefabContents("Assets/Waves/2/2_3_extrime.prefab");
        var created = new List<GameObject>();
        try
        {
            var wave = root.transform.Find("Main").GetComponent<DirectedEnemySubWave>();
            var alive = Get(wave, "aliveEnemies");
            var indices = (Dictionary<Enemy, int>)Get(wave, "formationIndices");
            var enemies = new List<Enemy>();
            for (int i = 0; i < 3; i++)
            {
                var obj = new GameObject("Attack Order test " + i); obj.SetActive(false); created.Add(obj);
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(obj, root.scene);
                var enemy = obj.AddComponent<Enemy>(); obj.SetActive(true);
                Invoke(alive, "Add", enemy); indices[enemy] = i; enemies.Add(enemy);
            }
            var settings = new DirectedWaveAttackSettings(); Set(settings, "fireMode", DirectedWaveAttackFireMode.Default);
            settings.AttackOrder.enabled = true; settings.AttackOrder.loopOrder = false;
            var type = typeof(DirectedEnemySubWave).Assembly.GetType("DirectedWaveAttackController", true);
            var controller = Activator.CreateInstance(type, new object[] { wave, settings, null });
            Invoke(controller, "BeginSingleCycle", 1f, new EnemyAttackCompletionTracker());
            var executors = (Dictionary<Enemy, IWaveAttackExecutor>)Get(controller, "executors");
            for (int i = 0; i < enemies.Count; i++) executors[enemies[i]] = new Executor();
            Check((bool)Invoke(controller, "BuildOrderedAttackCycle"), "Runtime builds finite order");
            var plan = (DirectedWaveAttackOrderPlan)Get(controller, "orderPlan");
            Check(Values(plan) == "0,1,2", "Runtime order uses formation IDs, not HashSet insertion");
            Set(enemies[0], "isDead", true); Invoke(controller, "NotifyEnemyDestroyed", enemies[0]);
            var cooldowns = (Dictionary<Enemy, float>)Get(controller, "nextAttackReadyTimes");
            cooldowns[enemies[1]] = enemies[1].AttackTime + 100f;
            Invoke(controller, "Tick");
            Check(plan.Cursor == 1, "Dead head skipped, next cooldown keeps its place without overtaking");
            Set(enemies[1], "isDead", true); Invoke(controller, "NotifyEnemyDestroyed", enemies[1]);
            cooldowns[enemies[2]] = enemies[2].AttackTime + 100f; Invoke(controller, "Tick");
            Check(plan.Cursor == 2, "Second destroyed enemy skipped without corrupting queue");
            Set(enemies[2], "isDead", true); Invoke(controller, "NotifyEnemyDestroyed", enemies[2]);
            Invoke(controller, "Tick"); Check((bool)type.GetProperty("HasCompletedSingleCycle").GetValue(controller), "All dead targets complete finite Sequence cycle");
            Invoke(controller, "Stop"); Check(plan.CycleCount == 0, "Restart clears order state");
            settings.AttackOrder.mode = DirectedWaveAttackOrderMode.Simultaneous;
            Invoke(controller, "BeginSingleCycle", 1f, new EnemyAttackCompletionTracker());
            var recordings = new List<Executor>();
            for (int i = 0; i < enemies.Count; i++)
            { Set(enemies[i], "isDead", false); var recording = new Executor(); recordings.Add(recording); executors[enemies[i]] = recording; }
            Invoke(controller, "Tick");
            Check(plan.Cursor == 3, "Runtime dispatches a simultaneous batch in one Tick");
            for (int i = 0; i < recordings.Count; i++) Check(recordings[i].Shots == 1, "Runtime shot in simultaneous batch " + i);
            Invoke(controller, "Stop");
        }
        finally
        { foreach (var obj in created) UnityEngine.Object.DestroyImmediate(obj); PrefabUtility.UnloadPrefabContents(root); }
    }
    private static string DescribeSlots(object group)
    {
        var values = new List<string>();
        foreach (var slot in (System.Collections.IEnumerable)Get(group, "Slots"))
            values.Add(Get(slot, "Index") + ":active=" + Get(slot, "Active") + ",ready=" + Get(slot, "ReadyTime"));
        return string.Join(";", values);
    }
    private static void Advance(WaveShootingPreviewSession preview, float time) { while (!preview.AdvanceTo(time, 1000d)) { } }
    private static void Test(DirectedWaveAttackOrderPlan plan, List<DirectedWaveAttackOrderSlot> slots,
        DirectedWaveAttackOrderSettings config, DirectedWaveAttackOrderMode mode, string expected)
    { config.mode = mode; plan.Reset(); plan.BuildCycle(slots, config, 1); Check(Values(plan) == expected, mode + ": " + Values(plan) + " != " + expected); }
    private static string Values(DirectedWaveAttackOrderPlan plan)
    { var result = new List<int>(); foreach (var entry in plan.Entries) result.Add(entry.Slot); return string.Join(",", result); }
    private static string Batches(DirectedWaveAttackOrderPlan plan)
    { var result = new List<int>(); foreach (var entry in plan.Entries) result.Add(entry.Batch); return string.Join(",", result); }
    private static object Get(object value, string name) => value.GetType().GetField(name, Flags).GetValue(value);
    private static void Set(object value, string name, object field) => value.GetType().GetField(name, Flags).SetValue(value, field);
    private static object Invoke(object value, string name, params object[] args) => value.GetType().GetMethod(name, Flags).Invoke(value, args);
}
