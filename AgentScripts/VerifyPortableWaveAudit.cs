using System;
using System.Collections.Generic;
using UnityEngine;

// Also usable inside Unity. No scenes, GameObjects, assets, or persistent player data are changed.
public static class VerifyPortableWaveAudit
{
    public sealed class Result
    {
        public int checks;
        public int existingCoordinatorChecks;
        public List<string> passed = new();
        public List<string> failures = new();
    }
    private sealed class Participant : IWaveAttackCoordinatorParticipant
    {
        public WaveAttackParticipantState State { get; set; } = WaveAttackParticipantState.Ready;
        public int CompletedCycles { get; set; }
        public bool IsIdle { get; set; } = true;
        public WaveAttackPermissions Permissions = new();
        public int Starts;
        public void SetPermission(object owner, WaveAttackPermission value) => Permissions.Set(owner, value);
        public void ReleasePermission(object owner) => Permissions.Release(owner);
        public void Reactivate(WaveAttackReactivation mode) { Starts++; if (mode == WaveAttackReactivation.Restart) CompletedCycles = 0; }
        public void SetCycleLimit(int limit) { }
    }
    private static void Check(bool value, string message, Result result)
    { result.checks++; if (!value) throw new InvalidOperationException(message); }
    private static void Scenario(string name, Action action, Result result)
    { try { action(); result.passed.Add(name); } catch (Exception error) { result.failures.Add(name + ": " + error.GetType().Name + ": " + error.Message); } }
    private static WaveAttackCoordinatorGroup Group(int member, float duration = .2f)
        => new() { duration = duration, completionMode = WaveAttackGroupCompletion.Timed,
            controlledSubWaves = new() { new() { scheduleIndex = member } } };
    public static Result Run()
    {
        var result = new Result();
        Scenario("Existing coordinator managed scenarios", () => {
            // Reflection keeps this file compatible with the full existing Editor suite.
            var method = typeof(VerifyWaveAttackCoordinator).GetMethod("RunPure", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            result.existingCoordinatorChecks = method != null ? (int)method.Invoke(null, null) : 0;
            Check(result.existingCoordinatorChecks > 0, "Run via RunPortableWaveAudit.ps1 to include existing pure checks", result);
        }, result);
        Scenario("Empty coordinator and repeated disposal", () => {
            var settings = new WaveAttackCoordinatorSettings();
            var player = new WaveAttackCoordinatorPlayer(settings, _ => null);
            player.Begin(0); player.Tick(100); Check(player.IsComplete, "Empty sequence completes", result);
            player.Dispose(); player.Dispose(); player.Tick(200);
        }, result);
        Scenario("A-B-A delays and permission cleanup", () => {
            var members = new[] { new Participant(), new Participant() };
            var settings = new WaveAttackCoordinatorSettings { switchMode = WaveAttackSwitchMode.Interrupt, globalCooldown = .1f,
                groups = new() { Group(0), Group(1) } };
            settings.groups[0].delayAfterGroup = .3f;
            using (var player = new WaveAttackCoordinatorPlayer(settings, i => members[i])) {
                player.Begin(0); player.Tick(0);
                Check(members[0].Permissions.AllowNewAttacks && !members[1].Permissions.AllowNewAttacks, "A first", result);
                player.Tick(.201f);
                Check(!members[0].Permissions.AllowNewAttacks && !members[1].Permissions.AllowNewAttacks, "Deactivation without immediate successor", result);
                player.Tick(.600f); Check(members[1].Starts == 0, "Delay retained", result);
                player.Tick(.602f); Check(members[1].Starts == 1, "B after delay", result);
                player.Tick(.803f); player.Tick(.904f);
                Check(members[0].Starts == 2, "A reactivated", result);
            }
            Check(members[0].Permissions.AllowNewAttacks && members[1].Permissions.AllowNewAttacks, "All leases released", result);
        }, result);
        Scenario("Null participant collection", () => {
            var settings = new WaveAttackCoordinatorSettings { loopSequence = false, groups = new() { new() { controlledSubWaves = null } } };
            using var player = new WaveAttackCoordinatorPlayer(settings, _ => null);
            player.Begin(0); player.Tick(0); player.Tick(.01f);
            Check(player.IsComplete, "Unconfigured group follows the existing empty-group drain", result);
        }, result);
        Scenario("Null group collection", () => {
            var settings = new WaveAttackCoordinatorSettings { groups = null };
            using var player = new WaveAttackCoordinatorPlayer(settings, _ => null);
            player.Begin(0); player.Tick(0); Check(player.IsComplete, "Missing group list is empty", result);
        }, result);
        Scenario("Begin after Dispose cannot leave a permission lease", () => {
            var member = new Participant();
            var settings = new WaveAttackCoordinatorSettings { groups = new() { Group(0) } };
            var player = new WaveAttackCoordinatorPlayer(settings, _ => member);
            player.Dispose(); bool rejected = false;
            try { player.Begin(0); } catch (ObjectDisposedException) { rejected = true; }
            player.Dispose();
            Check(rejected && member.Permissions.AllowNewAttacks, "Disposed scheduler must reject Begin before acquiring permissions", result);
        }, result);
        Scenario("Zero-duration infinite loop is bounded per tick", () => {
            var settings = new WaveAttackCoordinatorSettings { groups = new() { Group(0, 0f) }, switchMode = WaveAttackSwitchMode.Interrupt };
            var member = new Participant();
            using var player = new WaveAttackCoordinatorPlayer(settings, _ => member);
            player.Begin(0); player.Tick(0);
            Check(member.Starts > 0 && member.Starts <= 64, "Scheduling guard bounds zero-duration loop", result);
        }, result);
        Scenario("Boundary masks, high speeds, outside spawns and corner crossing", () => {
            var bounds = Rect.MinMaxRect(-3f, -5f, 3f, 5f);
            for (int mask = 0; mask < 16; mask++) {
                bool cross = EnemyChildAttackBoundaryIntersection.TryCross(new Vector3(-100, 0), new Vector3(100, 0), bounds,
                    (EnemyChildAttackBoundaries)mask, 0, out float fraction, out var reached);
                bool left = (mask & 1) != 0, right = (mask & 2) != 0;
                Check(cross == (left || right), "Only selected vertical edges trigger: " + mask, result);
                if (cross) Check(reached == (left ? EnemyChildAttackBoundary.Left : EnemyChildAttackBoundary.Right)
                    && Mathf.Abs(fraction - (left ? .485f : .515f)) < .00001f, "First selected crossing", result);
                Check(!EnemyChildAttackBoundaryIntersection.TryCross(new Vector3(10, 10), new Vector3(11, 11), bounds,
                    (EnemyChildAttackBoundaries)mask, 0, out _, out _), "Outside spawn does not trigger immediately", result);
            }
            Check(EnemyChildAttackBoundaryIntersection.TryCross(new Vector3(0, 0), new Vector3(6, 10), bounds,
                EnemyChildAttackBoundaries.All, 0, out float corner, out _) && Mathf.Abs(corner - .5f) < .00001f, "Single first event at a corner", result);
            Check(EnemyChildAttackBoundaryIntersection.TryCross(new Vector3(0, 10), new Vector3(4, 10), bounds,
                EnemyChildAttackBoundaries.Right, -1, out _, out _), "Ignored top does not clip later right crossing", result);
        }, result);
        return result;
    }
}
