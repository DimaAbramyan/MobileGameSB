using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

public static class ObserveWaveAttackPhases
{
    private const BindingFlags Fields = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
    private static object Get(object owner, string name) => owner?.GetType().GetField(name, Fields)?.GetValue(owner);
    private static object Scalar(object owner, string name)
    {
        object value = Get(owner, name);
        if (value is Enemy enemy) return enemy != null ? new { id = enemy.GetInstanceID(), enemy.name, enemy.isDead, time = enemy.AttackTime } : null;
        if (value is ICollection collection) return collection.Count;
        if (value is IEnumerable sequence) { int count = 0; foreach (var item in sequence) count++; return count; }
        if (value is UnityEngine.Object unity) return unity != null ? unity.GetInstanceID() : null;
        return value;
    }
    private static object Controller(object value)
    {
        if (value == null) return null;
        var fields = new Dictionary<string, object>();
        foreach (string name in new[] { "isRunning", "completedCycles", "cycleLimit", "cycleCounted", "cyclePrepared", "nextQueueIndex", "nextAttackTime", "waitingForReady", "dispatchClockOwner", "pendingReadyEnemy", "attackQueue", "deferredAttackQueue", "activeFormationBurstEnemies", "activeMovementEnemies", "activeSequentialAttackEnemies", "nextAttackReadyTimes" })
            fields[name] = Scalar(value, name);
        var queue = Get(value, "attackQueue") as IEnumerable;
        var slots = new List<object>();
        if (queue != null) foreach (Enemy enemy in queue)
            slots.Add(enemy != null ? new { id = enemy.GetInstanceID(), enemy.isDead, active = enemy.isActiveAndEnabled, time = enemy.AttackTime } : null);
        fields["slots"] = slots;
        if (Get(value, "nextAttackReadyTimes") is IDictionary ready)
        {
            var timers = new List<object>();
            foreach (DictionaryEntry entry in ready) if (entry.Key is Enemy enemy && enemy != null)
                timers.Add(new { id = enemy.GetInstanceID(), enemy.isDead, now = enemy.AttackTime, ready = entry.Value });
            fields["readyTimes"] = timers;
        }
        return fields;
    }
    private static object Snapshot()
    {
        var coordinators = new List<object>(); var attacks = new List<object>();
        foreach (var coordinator in UnityEngine.Object.FindObjectsByType<WaveAttackCoordinator>(FindObjectsSortMode.None))
        {
            var groups = new List<object>();
            if (coordinator.Player != null) foreach (var group in coordinator.Player.ActiveGroups)
                groups.Add(new { group.Index, group.Status, group.StartedAt, group.Remaining, baselines = Get(group, "Baselines"),
                    requested = Get(group, "RequestedAt"), complete = Get(group, "CompleteAt"), done = Get(group, "Done"), draining = Get(group, "Draining") });
            var members = new List<object>();
            if (Get(coordinator.Player, "members") is IDictionary participants)
                foreach (DictionaryEntry participant in participants)
                    if (participant.Value is IWaveAttackCoordinatorParticipant member)
                        members.Add(new { index = participant.Key, state = member.State.ToString(), idle = member.IsIdle });
            coordinators.Add(new { coordinator.name, id = coordinator.GetInstanceID(), groups,
                next = coordinator.Player?.NextGroupIndex, nextAt = Get(coordinator.Player, "nextAt"), pass = Get(coordinator.Player, "pass"),
                exhausted = Get(coordinator.Player, "exhausted"), timeouts = coordinator.Player?.Timeouts, members });
        }
        foreach (var attack in UnityEngine.Object.FindObjectsByType<DirectedWaveAttackBehaviour>(FindObjectsSortMode.None))
        {
            var controllers = new List<object>();
            if (Get(attack, "attackPatternControllers") is IEnumerable multiple) foreach (var controller in multiple) controllers.Add(Controller(controller));
            attacks.Add(new { attack.name, id = attack.GetInstanceID(), permission = new { attack.AttackPermissions.AllowNewAttacks, attack.AttackPermissions.Interrupted },
                limit = Get(attack, "coordinatedCycleLimit"), clock = Get(attack, "externalClockOffset"),
                main = Controller(Get(attack, "attackController")), multiple = controllers });
        }
        return new { utc = DateTime.UtcNow, time = Time.time, scale = Time.timeScale, frame = Time.frameCount, playing = EditorApplication.isPlaying, coordinators, attacks };
    }
    public static object Now() => Snapshot();
    public static async Task<object> Observe(int seconds = 20)
    {
        string path = "AgentArtifacts/wave-phase-observation-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + ".jsonl";
        int samples = 0; var end = DateTime.UtcNow.AddSeconds(Mathf.Clamp(seconds, 1, 25));
        using (var output = new StreamWriter(path))
            while (EditorApplication.isPlaying && DateTime.UtcNow < end)
            {
                output.WriteLine(JsonConvert.SerializeObject(Snapshot())); output.Flush(); samples++;
                await Task.Delay(100);
            }
        return new { path, samples, final = Snapshot() };
    }
    public static object Configuration()
    {
        var values = new List<object>();
        foreach (var coordinator in UnityEngine.Object.FindObjectsByType<WaveAttackCoordinator>(FindObjectsSortMode.None))
            values.Add(new { type = "Coordinator", coordinator.name, json = JsonUtility.ToJson(coordinator.Settings) });
        foreach (var attack in UnityEngine.Object.FindObjectsByType<DirectedWaveAttackBehaviour>(FindObjectsSortMode.None))
            values.Add(new { type = "Attack", attack.name, json = JsonUtility.ToJson(attack) });
        File.WriteAllText("AgentArtifacts/wave-phase-active-configuration.json", JsonConvert.SerializeObject(values, Formatting.Indented));
        return new { configurations = values.Count };
    }
}
