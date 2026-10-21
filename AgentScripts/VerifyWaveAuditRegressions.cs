using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class VerifyWaveAuditRegressions
{
    private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    public static object Run()
    {
        var root = PrefabUtility.LoadPrefabContents("Assets/Waves/2/2_3_extrime.prefab");
        try
        {
            var wave = root.transform.Find("Main").GetComponent<DirectedEnemySubWave>();
            Set(wave.Formation, "customFormationPoints", new[] { Vector3.left, Vector3.right });
            var behaviour = wave.AttackPatternBehaviour;
            var first = new DirectedWaveAttackPattern();
            var second = new DirectedWaveAttackPattern();
            Set(first, "selectedEnemySlots", new List<int> { 0 });
            Set(second, "selectedEnemySlots", new List<int> { 1 });
            Set(behaviour, "attackPatterns", new List<DirectedWaveAttackPattern> { first, second });
            int groups;
            using (var session = new WaveShootingPreviewSession(wave, true, true, 0, Vector3.down * 4f))
                groups = ((IList)Get(session, "groups")).Count;

            Set(wave.PhaseEntry, "enabled", false);
            if (wave.PhaseEntryBehaviour != null) wave.PhaseEntryBehaviour.enabled = false;
            Set(wave.PostBehaviour, "enabled", true);
            if (wave.PostBehaviourComponent != null) wave.PostBehaviourComponent.enabled = true;
            Set(wave.Formation, "spawnInterval", 0f);
            Set(wave.PostBehaviour, "postStartDelay", 0f);
            Set(wave.PostBehaviour, "postCommandPipelineLoop", false);
            Set(wave.PostBehaviour, "postCommandPipelineFixedCount", 1);
            Set(wave.PostBehaviour, "wobblePhaseOffset", .7f);
            Set(wave.PostBehaviour, "patrolPoints", new[] { new DirectedWavePatrolPoint { offset = new Vector3(2f, 3f) } });
            Set(wave.PostBehaviour, "postCommands", new[] {
                new DirectedWavePostCommand { type = DirectedWavePostCommandType.Wait, duration = 1f },
                new DirectedWavePostCommand { type = DirectedWavePostCommandType.Parallel,
                    parallelExecutionMode = DirectedWaveParallelExecutionMode.Background, infiniteParallel = true,
                    parallelCommands = new[] { new DirectedWavePostCommand { type = DirectedWavePostCommandType.Patrol, duration = 4f } } },
                new DirectedWavePostCommand { type = DirectedWavePostCommandType.Wait, duration = 10f }
            });
            var order = wave.GetSimulationSpawnOrder();
            var positions = new Dictionary<int, Vector3>();
            wave.InvalidateSimulationPreviewCache();
            float post = wave.GetSimulationPreviewPostStartTime();
            wave.EvaluateSimulationPreviewNonAlloc(post + 3f, null, order, positions, out _);
            wave.EvaluateSimulationPreviewNonAlloc(post + .5f, null, order, positions, out _);
            var rewound = new Dictionary<int, Vector3>(positions);
            wave.InvalidateSimulationPreviewCache();
            wave.EvaluateSimulationPreviewNonAlloc(post + .5f, null, order, positions, out _);
            float rewindError = 0f;
            foreach (var pair in positions) rewindError = Mathf.Max(rewindError, Vector3.Distance(pair.Value, rewound[pair.Key]));
            bool passed = groups == 2 && rewindError < .00001f;
            if (!passed) throw new Exception("Attack group or rewind regression");
            return new { groups, expectedGroups = 2, rewindError, passed };
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
    private static object Get(object value, string name) => value.GetType().GetField(name, Fields).GetValue(value);
    private static void Set(object value, string name, object field) => value.GetType().GetField(name, Fields).SetValue(value, field);
}
