using System;
using UnityEditor;
using UnityEngine;

public static class ConfigureBlackHoleRegionAbility
{
    public static object Run()
    {
        if (EditorApplication.isPlaying || EditorApplication.isCompiling || EditorApplication.isUpdating)
            throw new Exception("Idle Editor required.");
        const string path = "Assets/Prefub/Data/ShipMeta/BlackHoleMeta.asset";
        var meta = AssetDatabase.LoadAssetAtPath<ShipMetaConfig>(path);
        if (meta == null) throw new Exception("BlackHole Meta missing.");
        Undo.RecordObject(meta, "Replace BlackHole active ability");
        for (int i = 0; i < meta.LevelCount; i++)
        {
            var stats = meta.GetRuntimeStats(i);
            if (!stats.TryGetContract(out BlackHoleShipMetaContract contract))
                throw new Exception("BlackHole contract missing.");
            var before = contract.CreateProjectileField(default, new Unity.Mathematics.float2(0f, 1f));
            float cooldown = stats.TryGetContract(out AbilityRecoveryShipMetaContract recovery)
                ? recovery.Settings.Cooldown : 5f;
            contract.ConfigureActive(5f, cooldown, .2f, 2.5f, true, true);
            if (!before.Equals(contract.CreateProjectileField(default, new Unity.Mathematics.float2(0f, 1f))))
                throw new Exception("Passive field must not change.");
        }
        EditorUtility.SetDirty(meta);
        AssetDatabase.SaveAssetIfDirty(meta);
        foreach (string prefabPath in new[] { "Assets/GameContent/Hulls/Gameplay/BlackHoleCraft.prefab", "Assets/Prefub/Ships/BlackHole.prefab" })
        {
            var root = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath).GetComponent<ParentShip>();
            if (root.ActiveAbility is not BlackHoleActive || root.PassiveAbility is not BlackHolePassive)
                throw new Exception("BlackHole prefab wiring mismatch: " + prefabPath);
            if (root.ShipData.ShipMetaConfig != meta) throw new Exception("Meta reference mismatch: " + prefabPath);
        }
        return new { meta = path, levels = meta.LevelCount, duration = 5f, front = .2f, rear = 2.5f,
            passivePreserved = true, prefabReferencesVerified = 2 };
    }
}
