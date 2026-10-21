using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static class VerifyWavePhaseDeathRegression
{
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private static object Get(object owner, string field) => owner.GetType().GetField(field, Flags).GetValue(owner);
    private static void Set(object owner, string field, object value) => owner.GetType().GetField(field, Flags).SetValue(owner, value);
    private sealed class Member : IWaveAttackCoordinatorParticipant
    {
        public WaveAttackParticipantState State { get; set; } = WaveAttackParticipantState.Ready;
        public int CompletedCycles => 0;
        public bool IsIdle => true;
        public WaveAttackPermissions Permissions = new();
        public int Reactivations;
        public void SetPermission(object owner, WaveAttackPermission permission) => Permissions.Set(owner, permission);
        public void ReleasePermission(object owner) => Permissions.Release(owner);
        public void Reactivate(WaveAttackReactivation mode) => Reactivations++;
        public void SetCycleLimit(int limit) { }
    }
    private static WaveAttackCoordinatorGroup Group(params int[] members)
    {
        var group = new WaveAttackCoordinatorGroup { completionMode = WaveAttackGroupCompletion.Timed, duration = 1f, maxWaitDuration = 0f, onEnemyDeath = WaveAttackEnemyDeath.CompleteGroup };
        foreach (int index in members) group.controlledSubWaves.Add(new WaveAttackSubWaveReference { scheduleIndex = index });
        return group;
    }
    public static object Run()
    {
        var results = new Dictionary<string, bool>();
        var members = new[] { new Member(), new Member(), new Member() };
        var settings = new WaveAttackCoordinatorSettings { loopSequence = true, switchMode = WaveAttackSwitchMode.Interrupt };
        settings.groups = new() { Group(0, 1), Group(2) };
        using (var player = new WaveAttackCoordinatorPlayer(settings, i => members[i]))
        {
            player.Begin(0f); player.Tick(0f);
            members[1].State = WaveAttackParticipantState.Gone; player.Tick(.2f);
            results["newDeathStillHonorsCompleteGroup"] = !members[0].Permissions.AllowNewAttacks && members[2].Permissions.AllowNewAttacks;
            player.Tick(1.21f);
            results["survivorsAttackOnNextPass"] = members[0].Permissions.AllowNewAttacks;
            results["deadParticipantNotReactivated"] = members[1].Reactivations == 1;
            player.Tick(1.3f);
            results["oldDeathDoesNotShortenNextTimedWindow"] = members[0].Permissions.AllowNewAttacks;
        }
        members = new[] { new Member(), new Member() };
        settings.groups = new() { Group(0), Group(1) }; settings.groups[1].delayAfterGroup = 2f;
        settings.switchMode = WaveAttackSwitchMode.FinishCurrentAttack;
        using (var player = new WaveAttackCoordinatorPlayer(settings, i => members[i]))
        {
            player.Begin(0f); player.Tick(0f); player.Tick(1f); player.Tick(1.01f);
            members[1].State = WaveAttackParticipantState.Gone; player.Tick(1.2f); player.Tick(1.21f);
            player.Tick(3.20f); results["deathPreservesDelayAfterGroup"] = !members[0].Permissions.AllowNewAttacks;
            player.Tick(3.22f); results["nextGroupStartsAfterDelay"] = members[0].Permissions.AllowNewAttacks;
        }
        var root = PrefabUtility.LoadPrefabContents("Assets/Waves/2/2_2_extrime.prefab");
        var actor = new GameObject("Phase cooldown regression"); actor.SetActive(false);
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(actor, root.scene);
        try
        {
            var wave = root.GetComponentInChildren<DirectedEnemySubWave>(true); var attack = wave.AttackPatternBehaviour;
            var enemy = actor.AddComponent<Enemy>(); actor.SetActive(true);
            Set(wave, "runtimeTimelineRunning", true);
            ((IDirectedWavePostTimelineBehaviour)attack).OnPostTimelineStarted(wave);
            var controller = Get(attack, "attackController");
            var cooldowns = (Dictionary<Enemy, float>)Get(controller, "nextAttackReadyTimes");
            float readyAt = enemy.AttackTime + 6.8f; cooldowns[enemy] = readyAt;
            attack.ReactivateCoordinatedAttack(WaveAttackReactivation.Restart);
            var current = (Dictionary<Enemy, float>)Get(Get(attack, "attackController"), "nextAttackReadyTimes");
            results["restartPreservesAuthoredCooldown"] = current.TryGetValue(enemy, out float restored) && Mathf.Approximately(restored, readyAt);
            var method = Get(attack, "attackController").GetType().GetMethod("IsAttackReady", Flags);
            results["restartCannotFireBeforeCooldownExpires"] = !(bool)method.Invoke(Get(attack, "attackController"), new object[] { enemy });
            current[enemy] = enemy.AttackTime - 1f;
            attack.ReactivateCoordinatedAttack(WaveAttackReactivation.Restart);
            results["expiredCooldownDoesNotAddExtraWait"] = (bool)method.Invoke(Get(attack, "attackController"), new object[] { enemy });

            var first = new DirectedWaveAttackPattern(); var second = new DirectedWaveAttackPattern();
            ((List<int>)Get(first, "selectedEnemySlots")).Add(0); ((List<int>)Get(second, "selectedEnemySlots")).Add(1);
            Set(attack, "attackPatterns", new List<DirectedWaveAttackPattern> { first, second });
            Set(attack, "executionMode", DirectedWaveAttackExecutionMode.Patterns);
            ((IDirectedWavePostTimelineBehaviour)attack).OnPostTimelineStarted(wave);
            var controllers = (IList)Get(attack, "attackPatternControllers");
            results["multipleControllersAvailable"] = controllers.Count == 2;
            ((Dictionary<Enemy, float>)Get(controllers[0], "nextAttackReadyTimes"))[enemy] = readyAt;
            ((Dictionary<Enemy, float>)Get(controllers[1], "nextAttackReadyTimes"))[enemy] = readyAt + 3f;
            attack.ReactivateCoordinatedAttack(WaveAttackReactivation.Restart);
            controllers = (IList)Get(attack, "attackPatternControllers");
            results["multipleCooldownsRemainIndependent"] = ((Dictionary<Enemy, float>)Get(controllers[0], "nextAttackReadyTimes")).TryGetValue(enemy, out float firstTime)
                && ((Dictionary<Enemy, float>)Get(controllers[1], "nextAttackReadyTimes")).TryGetValue(enemy, out float secondTime)
                && Mathf.Approximately(firstTime, readyAt) && Mathf.Approximately(secondTime, readyAt + 3f);
            using (var preview = new WaveShootingPreviewSession(wave, true, true, 0, Vector3.down * 4f))
            {
                preview.SetCycleLimit(-1);
                var slots = (IList)Get(preview, "slots");
                if (slots.Count == 0) throw new Exception("No slots in the configured wave preview.");
                object slot = slots[0];
                Set(slot, "ReadyTime", 6.8f);
                preview.Reactivate(WaveAttackReactivation.Restart);
                results["previewRestartPreservesCooldown"] = Mathf.Approximately((float)Get(slot, "ReadyTime"), 6.8f);
            }
        }
        finally { Object.DestroyImmediate(actor); PrefabUtility.UnloadPrefabContents(root); }
        bool passed = true; foreach (bool result in results.Values) passed &= result;
        return new { passed, results };
    }
}
