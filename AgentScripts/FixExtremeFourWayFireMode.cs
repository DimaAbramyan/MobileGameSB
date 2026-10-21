using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class FixExtremeFourWayFireMode
{
    private const string Path = "Assets/Waves/1_Extreme/1_4_extrime.prefab";
    public static object Verify()
    {
        var scene = EditorSceneManager.NewPreviewScene();
        var container = new GameObject("FourWay override verification");
        container.SetActive(false);
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(container, scene);
        try
        {
            var root = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Path), container.transform);
            DirectedEnemySubWave wave = null;
            foreach (var item in root.GetComponentsInChildren<DirectedEnemySubWave>(true))
                if (item.name == "FourWay") wave = item;
            var enemy = UnityEngine.Object.Instantiate(wave.GetConfiguredEnemyPrefabForSlot(0), container.transform);
            var executor = enemy.GetComponent<SimpleEnemyAttackExecutor>();
            var settings = new DirectedWaveAttackSettings();
            wave.GetComponent<DirectedWaveAttackBehaviour>().TryCopyResolvedAttackSettingsTo(0, false, settings);
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            typeof(DirectedEnemySubWave).GetProperty("formationFrozen", flags).SetValue(wave, true);
            var slots = (System.Collections.IDictionary)typeof(DirectedEnemySubWave).GetField("formationIndices", flags).GetValue(wave);
            slots[enemy] = 0;
            var type = typeof(DirectedEnemySubWave).Assembly.GetType("DirectedWaveAttackController", true);
            var controller = Activator.CreateInstance(type, new object[] { wave, settings, null });
            object[] args = { enemy, executor, false };
            var profile = (EnemyBurstAttackSettings)type.GetMethod("ResolveAttackSettings", flags).Invoke(controller, args);
            var mode = (DirectedWaveAttackFireMode)type.GetMethod("GetFireMode", flags).Invoke(controller, new object[] { enemy });
            executor.SetWaveAttackSettings(profile);
            var active = (EnemyBurstAttackSettings)typeof(SimpleEnemyAttackExecutor).GetProperty("ActivePatterns", flags).GetValue(executor);
            var data = JsonUtility.ToJson(active.Shooting);
            if (mode != DirectedWaveAttackFireMode.Default || !(bool)args[2]
                || !data.Contains("\"fanMode\":1") || !data.Contains("\"fanProjectileCount\":5")
                || active.Shooting.MovementPattern != EnemyProjectileMovementPattern.Default)
                throw new Exception("FourWay runtime override did not reach its executor.");
            return new { passed = true, effectiveMode = mode.ToString(), localProfileReachedExecutor = ReferenceEquals(active, profile),
                fan = "Fan", projectilesPerFan = 5, movement = active.Shooting.MovementPattern.ToString(), sourcePrefabUnchanged = true };
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(container);
            EditorSceneManager.ClosePreviewScene(scene);
        }
    }
    public static object Run()
    {
        if (EditorApplication.isPlaying || EditorApplication.isCompiling || EditorApplication.isUpdating)
            throw new Exception("Editor must be idle in Edit Mode.");
        var stage = PrefabStageUtility.GetCurrentPrefabStage();
        bool useStage = stage != null && stage.assetPath == Path;
        var root = useStage ? stage.prefabContentsRoot : AssetDatabase.LoadAssetAtPath<GameObject>(Path);
        DirectedEnemySubWave wave = null;
        foreach (var item in root.GetComponentsInChildren<DirectedEnemySubWave>(true))
            if (item.name == "FourWay") wave = item;
        if (wave == null) throw new Exception("FourWay subwave missing.");
        var local = wave.GetConfiguredEnemyOverrideForSlot(0);
        var before = new SerializedObject(local).FindProperty("fireMode").enumValueIndex;
        Undo.RecordObject(local, "Use Default firing for FourWay slot");
        var so = new SerializedObject(local);
        so.FindProperty("fireMode").enumValueIndex = (int)DirectedWaveAttackFireMode.Default;
        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(local);
        if (useStage)
        {
            EditorSceneManager.MarkSceneDirty(stage.scene);
            PrefabUtility.SaveAsPrefabAsset(root, Path);
        }
        else AssetDatabase.SaveAssetIfDirty(local);
        var settings = new DirectedWaveAttackSettings();
        if (!wave.GetComponent<DirectedWaveAttackBehaviour>().TryCopyResolvedAttackSettingsTo(0, false, settings))
            throw new Exception("Missing effective attack settings.");
        var profile = new EnemyBurstAttackSettings();
        profile.CopyFrom(settings.WaveBurstSettings);
        local.ApplyAttackPatternOverride(profile);
        if (local.ResolveFireMode(settings.FireMode) != DirectedWaveAttackFireMode.Default)
            throw new Exception("Effective fire mode is not Default.");
        return new { asset = Path, slot = local.name, before = (DirectedWaveAttackFireMode)before,
            after = local.ResolveFireMode(settings.FireMode).ToString(),
            shootingType = profile.Shooting.ShootingType.ToString(),
            profile = JsonUtility.ToJson(profile), usedPrefabStage = useStage };
    }
}
