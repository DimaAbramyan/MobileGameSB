using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class InspectWavePreviewReview
{
    public static object Stage()
    {
        var stage = UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage();
        if (stage == null) return "No prefab stage";
        var wave = stage.prefabContentsRoot.GetComponent<Wave>();
        var result = new List<object>();
        foreach (var sub in stage.prefabContentsRoot.GetComponentsInChildren<DirectedEnemySubWave>(true))
        {
            if (sub.name != "LeftBorder") continue;
            bool referenced = false;
            foreach (var cue in wave.ScheduledSubWaves) referenced |= cue.SubWavePrefab == sub.gameObject;
            result.Add(new { sub.name, referenced, sub.HasPostBehaviour, sub.HasPhaseEntry,
                post = JsonUtility.ToJson(sub.PostBehaviour), entry = JsonUtility.ToJson(sub.PhaseEntry),
                position = sub.GetSimulationFormationPosition(0).ToString("R") });
        }
        return result;
    }
    public static object Read()
    {
        var root = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Waves/2/2_2_extrime.prefab");
        var owner = root.GetComponent<Wave>();
        var results = new List<object>();
        foreach (var cue in owner.ScheduledSubWaves)
        {
            var sub = cue.SubWavePrefab.GetComponent<DirectedEnemySubWave>();
            if (!sub.name.Contains("Border")) continue;
            var flags = new List<object>();
            foreach (var component in sub.GetComponents<Behaviour>())
                flags.Add(new { type = component.GetType().Name, component.enabled, component.isActiveAndEnabled });
            var samples = new List<object>();
            foreach (float t in new[] {0f, 1f, 2f, 3f, 5f, 8f})
            {
                var positions = sub.EvaluateSimulationPreview(t, owner.transform, out string phase);
                var values = new List<object>();
                foreach (var item in positions) values.Add(new { slot = item.Key, x = item.Value.x, y = item.Value.y });
                samples.Add(new { t, phase, values });
            }
            results.Add(new { sub.name, path = AssetDatabase.GetAssetPath(sub), persistent = EditorUtility.IsPersistent(sub),
                sub.gameObject.activeSelf, sub.gameObject.activeInHierarchy, sub.HasPhaseEntry, sub.HasPostBehaviour,
                sub.UsesLegacyConfigurationCompatibility, flags, formation = JsonUtility.ToJson(sub.Formation),
                entry = JsonUtility.ToJson(sub.PhaseEntry), post = JsonUtility.ToJson(sub.PostBehaviour), samples });
        }
        return results;
    }
}
