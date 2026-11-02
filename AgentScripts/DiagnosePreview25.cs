using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

public static class DiagnosePreview25
{
    public static async Task<object> Run()
    {
        var wave = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Waves/2/2_5_extrime.prefab").GetComponent<Wave>();
        var rows = new List<object>();
        int starts = 0, shots = 0;
        using var sim = new WaveConductorShootingPreview(wave, new Vector3(0,-3), eventSink: e => {
            if(e.Kind == WavePreviewEventKind.AttackStart) starts++;
            if(e.Kind == WavePreviewEventKind.ProjectileSpawn) shots += e.Count;
        });
        for(int second=1; second<=60; second++)
        {
            int before=starts, oldShots=shots;
            int guard=0;
            while(!sim.AdvanceTo(second, 15, false, maxSteps: 60, deferPresentation:true))
            { if(++guard>1000) throw new Exception("Simulation stalled"); await Task.Delay(1); }
            var sessions = new List<object>();
            for(int i=0;i<sim.CueCount;i++) { var s=sim.GetSession(i); if(s!=null)sessions.Add(new{cue=i,status=s.CoordinatorStatus,phase=s.PhaseName}); }
            rows.Add(new{second,starts=starts-before,shots=shots-oldShots,live=sim.ProjectileCount,omitted=sim.Omitted,sessions,warning=sim.Warning});
            await Task.Delay(1);
        }
        if(sim.Omitted != 0) throw new Exception("Configured wave omitted projectiles: " + sim.Omitted);
        return rows;
    }
}
