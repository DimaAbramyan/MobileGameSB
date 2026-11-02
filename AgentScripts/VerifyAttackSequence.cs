using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class VerifyAttackSequence
{
    private static int checks;
    private sealed class Host : IDirectedWaveAttackSequenceHost
    {
        public float Time, Duration = .1f, ChildDuration;
        public readonly List<string> Starts = new();
        public bool TryStart(DirectedWaveAttackSequenceStep step, out IDirectedWaveAttackSequenceRun run)
        { Starts.Add(step.name); run = new Run(this, Time + Duration, Time + Duration + ChildDuration); return true; }
    }
    private sealed class Run : IDirectedWaveAttackSequenceRun
    {
        private readonly Host host;
        private readonly float end, childEnd;
        public Run(Host host, float end, float childEnd) { this.host = host; this.end = end; this.childEnd = childEnd; }
        public bool RootComplete => host.Time >= end;
        public bool ChildrenComplete => host.Time >= childEnd;
        public void Tick() { }
        public void Stop() { }
    }
    public static object RunAll()
    {
        checks = 0;
        VerifyOrder(); VerifyPlayer(); VerifyCopies(); VerifyPreview(); VerifyExample();
        return new { passed = true, checks, tested = "shared sequence order, nested blocks, delays, overlap, child gates/timeouts, reentry, presets, old defaults, real ECS Preview" };
    }
    private static DirectedWaveAttackSequenceStep Step(string name) => new DirectedWaveAttackSequenceStep { name = name };
    private static string Order(DirectedWaveAttackSequenceSettings settings, int maximum = 100)
    {
        var cursor = new DirectedWaveAttackSequenceCursor(settings); var values = new List<string>();
        for (int i = 0; i < maximum && cursor.TryNext(out var step); i++) values.Add(step.name);
        return string.Join(",", values);
    }
    private static void VerifyOrder()
    {
        var settings = new DirectedWaveAttackSequenceSettings { steps = new() { Step("A"), Step("B"), Step("C") } };
        Check(Order(settings) == "A,B,C", "Once order");
        settings.startStep = 1; Check(Order(settings) == "B,C", "Start step");
        settings.sequenceMode = DirectedWaveAttackSequenceMode.Loop; settings.loopCount = 2;
        Check(Order(settings) == "B,C,A,B,C", "Loop and start step");
        settings.startStep = 0; settings.sequenceMode = DirectedWaveAttackSequenceMode.PingPong;
        Check(Order(settings) == "A,B,C,B,A,B,C,B,A", "PingPong avoids duplicate endpoints");
        settings.sequenceMode = DirectedWaveAttackSequenceMode.Random; settings.loopCount = 5;
        string first = Order(settings); Check(first == Order(settings), "Random seed deterministic");
        settings.seed++; Check(first != Order(settings), "Random seed changes order");
        settings.sequenceMode = DirectedWaveAttackSequenceMode.Once;
        settings.steps = new() { new DirectedWaveAttackSequenceStep { type = DirectedWaveAttackSequenceStepType.RepeatBlock, repeatCount = 2,
            steps = new() { Step("A"), new DirectedWaveAttackSequenceStep { type = DirectedWaveAttackSequenceStepType.RepeatBlock,
                repeatCount = 3, steps = new() { Step("B") } } } }, Step("C") };
        Check(Order(settings) == "A,B,B,B,A,B,B,B,C", "Nested repeat blocks");
        settings.steps[1].enabled = false; Check(!Order(settings).Contains("C"), "Disabled steps");
    }
    private static void Tick(DirectedWaveAttackSequencePlayer player, Host host, float time) { host.Time = time; player.Tick(time); }
    private static void VerifyPlayer()
    {
        var a = Step("A"); a.repeatCount = 3; a.startDelay = .2f; a.delayAfterStep = .3f;
        var pause = Step("pause"); pause.type = DirectedWaveAttackSequenceStepType.Pause; pause.pauseDuration = .5f;
        var b = Step("B");
        var settings = new DirectedWaveAttackSequenceSettings { steps = new() { a, pause, b } };
        var host = new Host(); var player = new DirectedWaveAttackSequencePlayer(settings, host); player.Begin(0f, true);
        Tick(player, host, 0f); Tick(player, host, .19f); Check(host.Starts.Count == 0, "Start delay");
        for (int i = 20; i <= 150; i++) Tick(player, host, i / 100f);
        Check(string.Join(",", host.Starts) == "A,A,A,B" && player.IsComplete, "Repeat, completion, pause and after delay");

        a = Step("A"); a.waitForCompletion = false;
        settings = new DirectedWaveAttackSequenceSettings { waitForPreviousAttack = false, steps = new() { a, Step("B") } };
        host = new Host { Duration = 2f }; player = new DirectedWaveAttackSequencePlayer(settings, host); player.Begin(0f, true);
        Tick(player, host, 0f); Check(host.Starts.Count == 2, "No-wait overlap");
        settings.waitForPreviousAttack = true; host = new Host { Duration = 2f };
        player = new DirectedWaveAttackSequencePlayer(settings, host); player.Begin(0f, true); Tick(player, host, 0f);
        Check(host.Starts.Count == 1, "Global previous-attack gate");
        Tick(player, host, 2f); Check(host.Starts.Count == 2, "Global gate releases");

        a = Step("A"); a.waitForChildAttacks = true; a.childWaitTimeout = .2f;
        settings = new DirectedWaveAttackSequenceSettings { steps = new() { a, Step("B") } };
        host = new Host { ChildDuration = 10000f }; player = new DirectedWaveAttackSequencePlayer(settings, host); player.Begin(0f, true);
        Tick(player, host, 0f); Tick(player, host, .1f); Tick(player, host, .29f);
        Check(host.Starts.Count == 1, "Explicit child wait");
        Tick(player, host, .31f); Check(host.Starts.Count == 2 && player.ChildWaitTimeouts == 1, "Child timeout does not stall sequence");
        a.waitForChildAttacks = false; host = new Host { ChildDuration = 10000f };
        player = new DirectedWaveAttackSequencePlayer(settings, host); player.Begin(0f, true); Tick(player, host, 0f); Tick(player, host, .1f);
        Check(host.Starts.Count == 2, "Long children ignored by default");
        a.waitForChildAttacks = true; a.unlimitedChildWait = true;
        host = new Host { ChildDuration = 10000f }; player = new DirectedWaveAttackSequencePlayer(settings, host);
        player.Begin(0f, true); Tick(player, host, 0f); Tick(player, host, 100f);
        Check(host.Starts.Count == 1, "Unlimited child wait is explicit");

        settings = new DirectedWaveAttackSequenceSettings { steps = new() { Step("A"), Step("B") } };
        host = new Host(); player = new DirectedWaveAttackSequencePlayer(settings, host); player.Begin(0f, true);
        Tick(player, host, 0f); Tick(player, host, .1f); player.Pause(.15f); player.Begin(5f, false); Tick(player, host, 5f);
        Check(string.Join(",", host.Starts) == "A,B,B", "Reentry resumes interrupted step");
        player.Pause(5f); player.Begin(6f, true); Tick(player, host, 6f);
        Check(host.Starts[host.Starts.Count - 1] == "A", "Restart on reentry");
        settings.sequenceMode = DirectedWaveAttackSequenceMode.Loop; settings.loopCount = 0;
        settings.steps = new() { new DirectedWaveAttackSequenceStep { type = DirectedWaveAttackSequenceStepType.Pause, pauseDuration = 0f } };
        player = new DirectedWaveAttackSequencePlayer(settings, host); player.Begin(0f, true); Tick(player, host, 0f);
        Check(!player.IsComplete, "Zero-duration infinite loop bounded per frame");
    }
    private static void VerifyCopies()
    {
        var profile = new EnemyBurstAttackSettings(); profile.ConfigureLegacySingleAttack(3, 2f, .4f);
        profile.Shooting.EmissionLayout.Configure(EnemyEmissionLayoutType.Sector, 4);
        Set(profile.Shooting.EmissionLayout, "launchInterval", .2f);
        var copy = EnemyAttackSettingsResolver.CreateRuntimeCopy(profile, null, null); copy.ScaleAttackTiming(2f);
        Check(copy.AttackShotCount == 3 && copy.AttackShotInterval == .2f && copy.AttackCooldown == 1f, "Speed scales timing, not shot count");
        Check(copy.Shooting.EmissionLayout.LaunchInterval == .1f && profile.Shooting.EmissionLayout.LaunchInterval == .2f,
            "Sector timing scales without mutating source");
        var step = Step("Nested"); step.steps.Add(Step("Child"));
        var clone = JsonUtility.FromJson<DirectedWaveAttackSequenceStep>(JsonUtility.ToJson(step));
        clone.steps[0].name = "Changed";
        Check(step.steps[0].name == "Child", "Deep duplicate of managed repeat blocks");
        var preset = ScriptableObject.CreateInstance<DirectedWaveAttackPreset>();
        try
        {
            var source = new DirectedWaveAttackSettings(); preset.SetAttackSettings(source, true, 3f);
            Set(step.pattern, "attackPreset", preset); step.attackSpeedMultiplier = 2f;
            string before = JsonUtility.ToJson(preset);
            var resolved = new DirectedWaveAttackSettings(); step.CopySettingsTo(resolved);
            Check(resolved.AttacksPerSecond == 2f && resolved.RuntimeAttackSpeedMultiplier == 2f
                && resolved.HasRuntimeProjectileBaseSpeedOverride && resolved.RuntimeProjectileBaseSpeedOverride == 3f,
                "Step preset, cadence and projectile speed priorities");
            Check(before == JsonUtility.ToJson(preset), "Step speed does not mutate preset");
        }
        finally { UnityEngine.Object.DestroyImmediate(preset); }
    }
    private static void VerifyPreview()
    {
        var root = PrefabUtility.LoadPrefabContents("Assets/Waves/2/2_3_extrime.prefab");
        try
        {
            var wave = root.transform.Find("Main").GetComponent<DirectedEnemySubWave>();
            var behaviour = wave.AttackPatternBehaviour;
            Check(!behaviour.UsesAttackSequence, "Old wave defaults to existing patterns");
            Set(behaviour, "executionMode", DirectedWaveAttackExecutionMode.Sequence);
            var first = Attack("Point", EnemyEmissionLayoutType.Point, 1);
            var second = Attack("Sector", EnemyEmissionLayoutType.Sector, 3);
            Set(wave.Formation, "customFormationPoints", new[] { Vector3.zero });
            var settings = behaviour.AttackSequence; settings.steps.Clear(); settings.steps.Add(first); settings.steps.Add(second);
            using var session = new WaveShootingPreviewSession(wave, true, true, 0, Vector3.down * 4f);
            while (!session.AdvanceTo(.1f, 1000d)) { }
            Check(session.ProjectileCount == 1 && session.SequenceStatus == "Point", "First sequence step emits once");
            while (!session.AdvanceTo(2f, 1000d)) { }
            Check(session.ProjectileCount == 4 && session.SequenceStatus == "Complete", "Sequence Preview advances to distinct Sector then stops");
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
    private static DirectedWaveAttackSequenceStep Attack(string name, EnemyEmissionLayoutType layout, int count)
    {
        var step = Step(name);
        var settings = (DirectedWaveAttackSettings)Get(step.pattern, "attackSettings");
        Set(settings, "fireMode", DirectedWaveAttackFireMode.Default);
        Set(settings, "burstSettingsSource", DirectedWaveBurstSettingsSource.WaveOverride);
        settings.WaveBurstSettings.ConfigureLegacySingleAttack(1, .2f, 0f);
        settings.WaveBurstSettings.Shooting.EmissionLayout.Configure(layout, count);
        // This fixture counts all emitted bullets at 8s, independently of screen cleanup.
        settings.WaveBurstSettings.Shooting.SetScreenBoundary(new EnemyProjectileScreenSettings(false));
        Set(settings.WaveBurstSettings.Shooting, "overrideProjectileLifetime", true);
        Set(settings.WaveBurstSettings.Shooting, "projectileLifetime", 10f);
        return step;
    }
    private static void VerifyExample()
    {
        var root = PrefabUtility.LoadPrefabContents("Assets/Waves/2/2_3_extrime.prefab");
        try
        {
            var wave = root.transform.Find("Main").GetComponent<DirectedEnemySubWave>();
            Set(wave.Formation, "customFormationPoints", new[] { Vector3.left, Vector3.right });
            var behaviour = wave.AttackPatternBehaviour;
            Set(behaviour, "executionMode", DirectedWaveAttackExecutionMode.Sequence);
            var settings = behaviour.AttackSequence;
            var aimed = Attack("Aimed x3", EnemyEmissionLayoutType.Point, 1); aimed.repeatCount = 3;
            Set(Get(aimed.pattern, "attackSettings"), "fireMode", DirectedWaveAttackFireMode.Aimed);
            var radial = Attack("Radial", EnemyEmissionLayoutType.Radial, 8);
            var sector = Attack("Rotating Sector", EnemyEmissionLayoutType.Sector, 4);
            var shooting = ((DirectedWaveAttackSettings)Get(sector.pattern, "attackSettings")).WaveBurstSettings.Shooting;
            shooting.EmissionLayout.ConfigureSector(330f, 30f, 4, EnemySectorLaunchMode.Sequential, .2f, .15f);
            shooting.SetLayoutRotationEnabled(true);
            settings.sequenceMode = DirectedWaveAttackSequenceMode.Loop; settings.loopCount = 2;
            settings.steps = new() { aimed, radial, new DirectedWaveAttackSequenceStep {
                type = DirectedWaveAttackSequenceStepType.Pause, pauseDuration = .2f }, sector };
            using (var preview = new WaveShootingPreviewSession(wave, true, false, 0, Vector3.down * 4f))
            {
                while (!preview.AdvanceTo(8f, 1000d)) { }
                Check(preview.ProjectileCount == 30 && preview.SequenceStatus == "Complete",
                    $"Requested example: 3 aimed, radial, pause, rotating sequential Sector, two loops; actual={preview.ProjectileCount}, status={preview.SequenceStatus}");
            }

            var first = Attack("Slot 0", EnemyEmissionLayoutType.Point, 1); first.overrideTargetSlots = true;
            first.targetSlots.Add(0); first.waitForCompletion = false;
            var second = Attack("Slot 1", EnemyEmissionLayoutType.Point, 1); second.overrideTargetSlots = true; second.targetSlots.Add(1);
            settings.sequenceMode = DirectedWaveAttackSequenceMode.Once; settings.waitForPreviousAttack = false;
            settings.steps = new() { first, second };
            using (var preview = new WaveShootingPreviewSession(wave, true, true, 0, Vector3.down * 4f))
            {
                while (!preview.AdvanceTo(.1f, 1000d)) { }
                Check(preview.ProjectileCount == 2, "Disjoint target overrides overlap");
            }
            second.targetSlots[0] = 0;
            using (var preview = new WaveShootingPreviewSession(wave, true, true, 0, Vector3.down * 4f))
            {
                while (!preview.AdvanceTo(.1f, 1000d)) { }
                Check(preview.ProjectileCount == 1, "Same-slot steps cannot replace an active attack");
                while (!preview.AdvanceTo(2f, 1000d)) { }
                Check(preview.ProjectileCount == 2 && preview.SequenceStatus == "Complete", "Same-slot step continues after release");
            }
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
    private static void Check(bool value, string message) { checks++; if (!value) throw new Exception(message); }
    private static object Get(object value, string name) => value.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).GetValue(value);
    private static void Set(object value, string name, object field) => value.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).SetValue(value, field);
}
