using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using UnityEditor;
using UnityEngine;

public static class VerifyWavePreviewNativeSnapshots
{
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private static object Call(object target, string name, params object[] args) => target.GetType().GetMethod(name, Flags).Invoke(target, args);
    private static async Task Advance(WaveConductorShootingPreview simulation, float time)
    { while (!simulation.AdvanceTo(time, 1000)) await Task.Yield(); }
    private static string State(WaveConductorShootingPreview simulation)
    {
        var values = new List<string>();
        for (int cue = 0; cue < simulation.CueCount; cue++)
        {
            var session = simulation.GetSession(cue); if (session == null) continue;
            var positions = new Dictionary<int, Vector3>(); session.CopyPreviewPositionsTo(positions);
            foreach (var p in positions) values.Add(cue + "/" + p.Key + ":" + p.Value.ToString("R"));
        }
        var manager = simulation.ProjectileWorld.SimulationWorld.EntityManager;
        using var query = manager.CreateEntityQuery(typeof(EnemyProjectileMovement), typeof(LocalTransform));
        using var entities = query.ToEntityArray(Allocator.Temp);
        foreach (var e in entities)
        {
            string value = JsonUtility.ToJson(manager.GetComponentData<LocalTransform>(e))
                + JsonUtility.ToJson(manager.GetComponentData<EnemyProjectileMovement>(e));
            if (manager.HasComponent<EnemyLaserState>(e)) value += JsonUtility.ToJson(manager.GetComponentData<EnemyLaserState>(e));
            values.Add(value);
        }
        values.Sort(StringComparer.Ordinal); return string.Join("\n", values);
    }
    public static async Task<object> Run()
    {
        int worlds = World.All.Count;
        var wave = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Waves/2/2_2_extrime.prefab").GetComponent<Wave>();
        string three, six;
        using (var simulation = new WaveConductorShootingPreview(wave, new Vector3(0, -3), spawnSeed: 0))
        {
            Call(simulation, "EnableSnapshots"); simulation.PrepareInitialState();
            var zero = Call(simulation, "CaptureSnapshot") as IDisposable;
            using (zero)
            {
                await Advance(simulation, 3); three = State(simulation);
                var snapshot = Call(simulation, "CaptureSnapshot") as IDisposable;
                using (snapshot)
                {
                    await Advance(simulation, 6); six = State(simulation);
                    Call(snapshot, "Restore");
                    if (State(simulation) != three) throw new Exception("Restored native/visual state differs at 3 seconds");
                    await Advance(simulation, 6);
                    if (State(simulation) != six) throw new Exception("Continued native state differs after restore");
                }
                Call(zero, "Restore");
                if (simulation.GetSession(0) != null) throw new Exception("Delayed cue exposed before its spawn after rollback");
                await Advance(simulation, 6);
                if (State(simulation) != six) throw new Exception("Replay across a delayed cue differs after rollback to zero");
            }
        }
        if (World.All.Count != worlds) throw new Exception("Snapshot worlds leaked");
        return new { passed = true, nativeRestore = true, continuation = true, delayedCueRollback = true, cleanup = true };
    }
}
