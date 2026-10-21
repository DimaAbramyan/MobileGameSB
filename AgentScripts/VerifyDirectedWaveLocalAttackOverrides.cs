using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

// Runs against temporary objects in the connected Editor; no assets are saved.
public static class VerifyDirectedWaveLocalAttackOverrides
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static int checks;

    public static object Run()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlaying)
            throw new Exception("Editor must be idle in Edit Mode.");
        checks = 0;
        var previewScene = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
        var root = new GameObject("Local attack override verification");
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, previewScene);
        root.SetActive(false);
        root.hideFlags = HideFlags.HideAndDontSave;
        Editor editor = null;
        try
        {
            var wave = root.AddComponent<DirectedEnemySubWave>();
            var behaviour = root.AddComponent<DirectedWaveAttackBehaviour>();
            var points = Child(root.transform, "Points");
            var local = Child(points, "Slot 0").gameObject.AddComponent<DirectedWaveEnemyOverride>();
            Set(wave, "formationFrozen", true);
            Set(wave, "formationPointsRoot", points);
            wave.SetAttackPatternBehaviour(behaviour);
            var enemyObject = Child(root.transform, "Enemy").gameObject;
            var enemy = enemyObject.AddComponent<Enemy>();
            var executor = enemyObject.AddComponent<SimpleEnemyAttackExecutor>();
            Set(wave, "enemyPrefab", enemy);
            ((IDictionary)Get(wave, "formationIndices"))[enemy] = 0;

            var shared = Profile(3, 0.35f, 4f, 2);
            var shooting = Profile(11, 2f, 19f, 7);
            var settings = new DirectedWaveAttackSettings();
            Set(settings, "serializedVersion", 11);
            Set(settings, "burstSettingsSource", DirectedWaveBurstSettingsSource.WaveOverride);
            Set(settings, "fireMode", DirectedWaveAttackFireMode.Stationary);
            Set(settings, "waveBurstSettings", shared);
            settings.SetRuntimeProjectileBaseSpeedOverride(8f);
            local.InitializeAttackPatternOverride(settings, shared);
            Assert(local.ResolveFireMode(DirectedWaveAttackFireMode.None) == DirectedWaveAttackFireMode.Stationary,
                "Initial Fire Mode must come from the behaviour.");
            var initial = new EnemyBurstAttackSettings();
            initial.CopyFrom(shared);
            local.ApplyAttackPatternOverride(initial);
            Assert(initial.Shooting.InitialSpeed == 8f, "Initial base speed must come from the behaviour.");
            Set(local, "attackPattern", shooting);
            Set(local, "overrideProjectileBaseSpeed", false);
            VerifyControllers(wave, enemy, executor, local, settings, shared, 7, 8f);
            Set(local, "overrideProjectileBaseSpeed", true);
            Set(local, "projectileBaseSpeed", 13f);
            VerifyControllers(wave, enemy, executor, local, settings, shared, 7, 13f);
            Assert(shared.Shooting.FanProjectileCount == 2 && shared.Shooting.InitialSpeed == 0f,
                "Shared configuration must stay unchanged.");

            VerifyMultiplePatterns(shared);
            VerifyMigration(wave, enemy, executor, local, settings);
            VerifyFireModes(wave, local, settings, enemy, executor);

            Set(behaviour, "attackSettings", settings);
            Set(behaviour, "overrideBaseProjectileSpeed", true);
            Set(behaviour, "baseProjectileSpeed", 8f);
            editor = Editor.CreateEditor(behaviour);
            Invoke(editor, "InitializeLocalAttackOverride", wave, local, 0);
            Assert(!local.UsesLegacyFullAttackSettings, "Reset must switch to shared cadence.");
            Assert(!ReferenceEquals(Get(local, "attackPattern"), shared), "Initialization must deep-copy the profile.");
            Assert(((EnemyBurstAttackSettings)Get(local, "attackPattern")).Shooting.FanProjectileCount == 2,
                "Inspector initialization must copy the resolved behaviour profile.");
            VerifyResolvedSources(wave, behaviour, local, executor, editor, settings);

            foreach (string field in new[] { "overrideAttackPattern", "fireMode", "attackPattern", "projectileBaseSpeed" })
                Assert(typeof(DirectedWaveEnemyOverride).GetField(field, Private).IsDefined(typeof(HideInInspector), true),
                    "Local shooting is edited only through Attack Pattern: " + field);

            var prefabReport = InspectPrefabs();
            return new { passed = true, checks, controllers = new[] { "Post-Formation", "Entrance" },
                migration = "v0/v1, legacy cadence, projectile type, disable, JSON round-trip", prefabs = prefabReport };
        }
        finally
        {
            if (editor != null) UnityEngine.Object.DestroyImmediate(editor);
            UnityEngine.Object.DestroyImmediate(root);
            UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(previewScene);
        }
    }

    private static void VerifyControllers(DirectedEnemySubWave wave, Enemy enemy,
        SimpleEnemyAttackExecutor executor, DirectedWaveEnemyOverride local,
        DirectedWaveAttackSettings settings, EnemyBurstAttackSettings timing, int fanCount, float speed)
    {
        foreach (string name in new[] { "DirectedWaveAttackController", "DirectedWaveEntranceAttackController" })
        {
            object controller = Controller(name, wave, settings);
            var result = (EnemyBurstAttackSettings)Invoke(controller, "GetBurstSettings", enemy, executor);
            Assert(result.Shooting.FanProjectileCount == fanCount, name + ": local shooting.");
            Assert(result.AttackShotCount == timing.AttackShotCount
                && result.AttackShotInterval == timing.AttackShotInterval
                && result.AttackCooldown == timing.AttackCooldown, name + ": cadence is preserved.");
            Assert(Mathf.Approximately(result.Shooting.InitialSpeed, speed), name + ": speed priority.");
            Assert((DirectedWaveAttackFireMode)Invoke(controller, "GetFireMode", enemy)
                == local.ResolveFireMode(settings.FireMode), name + ": effective Fire Mode.");
            controller.GetType().GetMethod("Stop").Invoke(controller, null);
        }
    }

    private static void VerifyMultiplePatterns(EnemyBurstAttackSettings shared)
    {
        var local = Profile(15, 3f, 20f, 4);
        Set(local, "attackPatterns", EnemyAttackPatternsMode.Multiple);
        var first = new EnemyAttackPatternSettings();
        first.CopyFrom(local);
        var second = new EnemyAttackPatternSettings();
        second.CopyFrom(local);
        Set(second.Shooting, "fanProjectileCount", 6);
        Set(local, "patterns", new List<EnemyAttackPatternSettings> { first, second });
        local.Validate();
        var result = new EnemyBurstAttackSettings();
        result.CopyFrom(shared);
        result.ApplyShootingOverrideFrom(local);
        Assert(result.PatternCount == 2, "Local multiple shooting patterns.");
        Assert(result.GetPattern(0).Shooting.FanProjectileCount == 4
            && result.GetPattern(1).Shooting.FanProjectileCount == 6, "Each local pattern has its shooting profile.");
        for (int i = 0; i < result.PatternCount; i++)
            Assert(result.GetPattern(i).AttackCooldown == shared.AttackCooldown
                && result.GetPattern(i).AttackShotCount == shared.AttackShotCount,
                "Added patterns inherit shared cadence.");
    }

    private static void VerifyResolvedSources(DirectedEnemySubWave wave, DirectedWaveAttackBehaviour behaviour,
        DirectedWaveEnemyOverride local, SimpleEnemyAttackExecutor executor, Editor editor,
        DirectedWaveAttackSettings settings)
    {
        var preset = ScriptableObject.CreateInstance<DirectedWaveAttackPreset>();
        try
        {
            Set(settings, "fireMode", DirectedWaveAttackFireMode.None);
            preset.SetAttackSettings(settings, true, 17f);
            Set(behaviour, "attackPreset", preset);
            Set(behaviour, "overrideBaseProjectileSpeed", false);
            editor.serializedObject.Update();
            Invoke(editor, "InitializeLocalAttackOverride", wave, local, 0);
            Assert(local.ResolveFireMode(DirectedWaveAttackFireMode.Stationary) == DirectedWaveAttackFireMode.None,
                "Initialization copies preset Fire Mode, including None.");
            Assert(((EnemyBurstAttackSettings)Get(local, "attackPattern")).Shooting.InitialSpeed == 17f,
                "Initialization copies preset speed: actual=" + Get(local, "projectileBaseSpeed")
                + ", presetOverride=" + preset.OverridesBaseProjectileSpeed
                + ", behaviourOverride=" + Get(behaviour, "overrideBaseProjectileSpeed"));

            var selectedPattern = new DirectedWaveAttackPattern();
            Set(selectedPattern, "selectedEnemySlots", new List<int> { 0 });
            Set(selectedPattern, "attackPreset", preset);
            Set(behaviour, "attackPatterns", new List<DirectedWaveAttackPattern> { selectedPattern });
            Set(behaviour, "attackPreset", null);
            editor.serializedObject.Update();
            Invoke(editor, "InitializeLocalAttackOverride", wave, local, 0);
            Assert(((EnemyBurstAttackSettings)Get(local, "attackPattern")).Shooting.InitialSpeed == 17f
                && local.ResolveFireMode(DirectedWaveAttackFireMode.Stationary) == DirectedWaveAttackFireMode.None,
                "Initialization resolves the pattern assigned to this slot.");

            Set(behaviour, "attackPatterns", new List<DirectedWaveAttackPattern>());
            Set(settings, "burstSettingsSource", DirectedWaveBurstSettingsSource.EnemySettings);
            Set(executor, "burstAttackSettings", Profile(2, 0.2f, 3f, 12));
            editor.serializedObject.Update();
            Invoke(editor, "InitializeLocalAttackOverride", wave, local, 0);
            Assert(((EnemyBurstAttackSettings)Get(local, "attackPattern")).Shooting.FanProjectileCount == 12,
                "Enemy Settings initializes shooting from the configured enemy prefab.");
        }
        finally { UnityEngine.Object.DestroyImmediate(preset); }
    }

    private static void VerifyMigration(DirectedEnemySubWave wave, Enemy enemy,
        SimpleEnemyAttackExecutor executor, DirectedWaveEnemyOverride local, DirectedWaveAttackSettings settings)
    {
        var legacy = Profile(5, 0.75f, 12f, 3);
        Set(local, "burstAttackSettings", legacy);
        Set(local, "overrideBurstAttackSettings", true);
        Set(local, "overrideProjectileType", true);
        Set(local, "projectileType", EnemyProjectileType.Homing);
        Set(local, "attackOverrideVersion", 0);
        Assert(local.EnsureAttackOverrideMigration(), "Migrate v0.");
        Assert(!local.EnsureAttackOverrideMigration(), "Migration is idempotent.");
        Assert(local.UsesLegacyFullAttackSettings && local.ResolveFireMode(DirectedWaveAttackFireMode.Stationary)
            == DirectedWaveAttackFireMode.Stationary, "Legacy cadence and inherited Fire Mode.");
        var copied = new EnemyBurstAttackSettings();
        Assert(local.TryCopyLegacyFullAttackSettings(copied), "Legacy profile is retained.");
        local.ApplyAttackPatternOverride(copied);
        Assert(copied.Shooting.ProjectileType == EnemyProjectileType.Homing, "Combined legacy projectile type is retained.");
        var edited = (EnemyBurstAttackSettings)Get(local, "attackPattern");
        Set(edited.Shooting, "fanProjectileCount", 9);
        Set(local, "overrideProjectileBaseSpeed", false);
        VerifyControllers(wave, enemy, executor, local, settings, legacy, 9, 8f);
        local.ApplyTo(enemy);
        Assert(executor.HasAttackSettingsOverride && executor.BurstAttackSettings.Shooting.FanProjectileCount == 9,
            "Legacy profile also survives autonomous entrance.");

        string json = EditorJsonUtility.ToJson(local);
        var restored = Child(local.transform.parent, "Serialized slot").gameObject.AddComponent<DirectedWaveEnemyOverride>();
        EditorJsonUtility.FromJsonOverwrite(json, restored);
        Assert(restored.UsesLegacyFullAttackSettings && !restored.EnsureAttackOverrideMigration(),
            "Serialized migration does not reset the profile.");
        var restoredProfile = new EnemyBurstAttackSettings();
        restored.TryCopyLegacyFullAttackSettings(restoredProfile);
        restored.ApplyAttackPatternOverride(restoredProfile);
        Assert(restoredProfile.Shooting.FanProjectileCount == 9, "Edited shooting survives serialization.");
        UnityEngine.Object.DestroyImmediate(restored.gameObject);

        // Keep the test executor neutral after verifying autonomous legacy behaviour.
        typeof(SimpleEnemyAttackExecutor).GetProperty("HasAttackSettingsOverride")
            .GetSetMethod(true).Invoke(executor, new object[] { false });
        Set(local, "overrideAttackPattern", false);
        local.ApplyTo(enemy);
        Assert(!local.UsesLegacyFullAttackSettings && local.AttackSettingsOverride == null
            && !local.TryCopyLegacyFullAttackSettings(copied) && !executor.HasAttackSettingsOverride,
            "Disabled migrated override must not revive legacy settings.");
        VerifyControllers(wave, enemy, executor, local, settings, settings.WaveBurstSettings, 2, 8f);
        Set(local, "overrideAttackPattern", true);
        Set(local, "inheritWaveFireMode", true);
        Set(local, "useLegacyFullAttackSettings", false);
        Set(local, "attackOverrideVersion", 1);
        Assert(local.EnsureAttackOverrideMigration() && local.UsesLegacyFullAttackSettings, "Migrate v1 cadence.");
    }

    private static void VerifyFireModes(DirectedEnemySubWave wave, DirectedWaveEnemyOverride local,
        DirectedWaveAttackSettings settings, Enemy enemy, SimpleEnemyAttackExecutor executor)
    {
        Set(local, "inheritWaveFireMode", false);
        var preview = typeof(EnemyShootingPreview).GetMethod("TryGetWaveSlotFireDirection",
            BindingFlags.Static | BindingFlags.NonPublic);
        foreach (DirectedWaveAttackFireMode mode in Enum.GetValues(typeof(DirectedWaveAttackFireMode)))
        {
            Set(local, "fireMode", mode);
            Assert(local.ResolveFireMode(settings.FireMode) == mode, "Local Fire Mode: " + mode);
            foreach (string name in new[] { "DirectedWaveAttackController", "DirectedWaveEntranceAttackController" })
            {
                object controller = Controller(name, wave, settings);
                Assert((DirectedWaveAttackFireMode)Invoke(controller, "GetFireMode", enemy) == mode,
                    name + ": Fire Mode " + mode);
            }
            object[] args = { wave, 0, settings, Vector3.zero, Vector3.down, Vector3.zero, false };
            Assert((bool)preview.Invoke(null, args) == (mode != DirectedWaveAttackFireMode.None),
                "Preview respects Fire Mode: " + mode);
            Assert((bool)args[6] == (mode == DirectedWaveAttackFireMode.Aimed), "Preview ShotGun aiming: " + mode);
        }
        Set(local, "forwardFireHalfAngle", 25f);
        Set(local, "fireMode", DirectedWaveAttackFireMode.ForwardWhenPlayerAhead);
        Assert(local.ResolveForwardFireHalfAngle(15f) == 25f, "Local forward sector.");
        object[] outside = { wave, 0, settings, Vector3.zero, Vector3.up, Vector3.zero, false };
        Assert(!(bool)preview.Invoke(null, outside), "Preview rejects a target outside the forward sector.");
    }

    private static object InspectPrefabs()
    {
        int prefabs = 0, slots = 0, legacy = 0, controlledSlots = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Waves" }))
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
            if (asset == null) continue;
            prefabs++;
            foreach (var wave in asset.GetComponentsInChildren<DirectedEnemySubWave>(true))
            {
                for (int i = 0; i < wave.GetConfiguredEnemySlotCount(); i++)
                {
                    var point = wave.GetConfiguredEnemyOverrideForSlot(i);
                    if (point == null) continue;
                    controlledSlots++;
                    Assert(wave.GetConfiguredEnemyOverrideHostForSlot(i) == point.transform,
                        "Persisted wave resolves the same slot host.");
                }
            }
            foreach (var point in asset.GetComponentsInChildren<DirectedWaveEnemyOverride>(true))
            {
                slots++;
                if (point.UsesLegacyFullAttackSettings) legacy++;
            }
        }
        return new { prefabs, slots, legacy, controlledSlots };
    }

    private static EnemyBurstAttackSettings Profile(int shots, float interval, float cooldown, int fan)
    {
        var profile = new EnemyBurstAttackSettings();
        profile.Validate();
        Set(profile, "attackShotCount", shots);
        Set(profile, "attackShotInterval", interval);
        Set(profile, "attackCooldown", cooldown);
        profile.Shooting.ConfigureLegacyFan(fan, -30f, 30f);
        profile.Validate();
        return profile;
    }

    private static object Controller(string name, DirectedEnemySubWave wave, DirectedWaveAttackSettings settings)
    {
        Type type = typeof(DirectedEnemySubWave).Assembly.GetType(name, true);
        return Activator.CreateInstance(type, name == "DirectedWaveAttackController"
            ? new object[] { wave, settings, null }
            : new object[] { wave, settings, new DirectedWaveEntranceAttackSettings() });
    }
    private static Transform Child(Transform parent, string name)
    {
        var child = new GameObject(name);
        child.transform.SetParent(parent, false);
        return child.transform;
    }
    private static void Set(object target, string name, object value)
    {
        var field = target.GetType().GetField(name, Private);
        if (field != null) { field.SetValue(target, value); return; }
        var property = target.GetType().GetProperty(name, Private);
        if (property == null || !property.CanWrite)
            throw new Exception("Missing verification member: " + target.GetType().Name + "." + name);
        property.SetValue(target, value);
    }
    private static object Get(object target, string name) => target.GetType().GetField(name, Private).GetValue(target);
    private static object Invoke(object target, string name, params object[] args) => target.GetType().GetMethod(name, Private).Invoke(target, args);
    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
        checks++;
    }
}
