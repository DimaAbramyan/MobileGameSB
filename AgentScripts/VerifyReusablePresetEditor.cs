using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

public static class VerifyReusablePresetEditor
{
    private const string Folder = "Assets/Configs/AttackPresets/Examples";
    private const string Manifest = "AgentArtifacts/projectile-presets-example-manifest.json";
    private static int checks;
    [Serializable] private sealed class Paths { public string projectile, child, fan, sector; }
    private static void Check(bool valid, string message) { checks++; if (!valid) throw new Exception(message); }
    private static void Patch(object value, string json) => JsonUtility.FromJsonOverwrite(json, value);
    private static int Worlds() { int count = 0; foreach (var world in World.All) if (world.Name == "Scene shooting preview") count++; return count; }
    private static void Set(object value, string name, object field) => value.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).SetValue(value, field);

    public static async Task<object> Run()
    {
        checks = 0;
        var scene = EditorSceneManager.NewPreviewScene();
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefub/Enemies Prefab/Enemy.prefab");
        var root = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
        Editor editor = null; Action<SceneView> draw = null;
        try
        {
            var executor = root.GetComponent<SimpleEnemyAttackExecutor>();
            using var serialized = new SerializedObject(executor);
            var fallback = serialized.FindProperty("defaultProjectilePrefab").objectReferenceValue as EnemyBullet;
            Check(fallback != null, "Existing enemy projectile available");
            var paths = CreateExamples(fallback);
            var projectile = AssetDatabase.LoadAssetAtPath<EnemyProjectilePreset>(paths.projectile);
            var fan = AssetDatabase.LoadAssetAtPath<EnemyAttackPatternPreset>(paths.fan);
            var sector = AssetDatabase.LoadAssetAtPath<EnemyAttackPatternPreset>(paths.sector);
            var timing = serialized.FindProperty("burstAttackSettings");
            var shooting = timing.FindPropertyRelative("shooting");
            string originalTiming = JsonUtility.ToJson(executor.BurstAttackSettings);
            Undo.IncrementCurrentGroup();
            shooting.FindPropertyRelative("projectilePreset").objectReferenceValue = projectile;
            EnemyPresetInspector.Apply(shooting, timing, true);
            Check(executor.BurstAttackSettings.Shooting.ChildAttack != null, "Inspector applied child tree");
            Check(executor.BurstAttackSettings.Shooting.ResolveProjectilePrefab(null) == fallback, "Inspector applied saved prefab reference");
            Check(executor.BurstAttackSettings.AuthoredShooting.ProjectilePreset == projectile, "Inspector binding persisted");
            float speed = executor.BurstAttackSettings.Shooting.InitialSpeed;
            shooting.FindPropertyRelative("attackPatternPreset").objectReferenceValue = fan;
            EnemyPresetInspector.Apply(shooting, timing, false);
            Check(Mathf.Approximately(executor.BurstAttackSettings.Shooting.InitialSpeed, speed), "Inspector pattern preserves projectile");
            Check(executor.BurstAttackSettings.Shooting.FanProjectileCount == 3, "Inspector pattern applied");
            Undo.FlushUndoRecordObjects(); Undo.PerformUndo(); serialized.Update();
            Check(executor.BurstAttackSettings.AuthoredShooting.ProjectilePreset == null, "Undo restored both bindings");
            Undo.PerformRedo(); serialized.Update();
            Check(executor.BurstAttackSettings.AuthoredShooting.ProjectilePreset == projectile, "Redo restored projectile link");
            Check(executor.BurstAttackSettings.AuthoredShooting.AttackPatternPreset == fan, "Redo restored pattern link");
            var modifications = PrefabUtility.GetPropertyModifications(root);
            bool projectileOverride = false, patternOverride = false;
            foreach (var modification in modifications)
            { projectileOverride |= modification.propertyPath.EndsWith(".projectilePreset"); patternOverride |= modification.propertyPath.EndsWith(".attackPatternPreset"); }
            Check(projectileOverride && patternOverride, "Native prefab overrides contain both references");
            shooting = serialized.FindProperty("burstAttackSettings.shooting"); timing = serialized.FindProperty("burstAttackSettings");
            var overrides = shooting.FindPropertyRelative("projectilePresetOverrides"); overrides.arraySize = 1; overrides.GetArrayElementAtIndex(0).stringValue = "projectileBaseSpeed";
            shooting.FindPropertyRelative("projectileBaseSpeed").floatValue = 9f; serialized.ApplyModifiedProperties();
            Check(Mathf.Approximately(executor.BurstAttackSettings.Shooting.InitialSpeed, 9f), "Serialized local override applied");
            var display = EnemyPresetInspector.DisplayShooting(shooting, timing);
            Check(Mathf.Approximately(display.FindPropertyRelative("projectileBaseSpeed").floatValue, 9f), "Inspector displays effective local value");
            var childShooting = shooting.FindPropertyRelative("childAttack.pattern.shooting");
            var childTiming = shooting.FindPropertyRelative("childAttack.pattern");
            var childDisplay = EnemyPresetInspector.DisplayShooting(childShooting, childTiming);
            Check(EnemyChildAttackSettingsDrawer.IsChildPattern(childDisplay), "Linked child display keeps child-only field rules");
            string beforeAsset = EditorJsonUtility.ToJson(projectile);
            shooting.FindPropertyRelative("projectileBaseSpeed").floatValue = 11f; serialized.ApplyModifiedProperties();
            Check(EditorJsonUtility.ToJson(projectile) == beforeAsset, "Local edit does not mutate asset");
            editor = Editor.CreateEditor(executor);
            var view = SceneView.lastActiveSceneView; Check(view != null, "Scene View available");
            var done = new TaskCompletionSource<bool>(); int repaints = 0;
            draw = current =>
            {
                if (current != view || Event.current.type != EventType.Layout && Event.current.type != EventType.Repaint) return;
                try
                {
                    Handles.BeginGUI(); GUILayout.BeginArea(new Rect(10, 30, 850, 20000)); editor.OnInspectorGUI(); GUILayout.EndArea(); Handles.EndGUI();
                    if (Event.current.type == EventType.Repaint && ++repaints >= 2) done.TrySetResult(true);
                }
                catch (Exception error) { done.TrySetException(error); }
            };
            SceneView.duringSceneGui += draw; var timeout = Task.Delay(10000);
            while (!done.Task.IsCompleted && !timeout.IsCompleted) { view.Repaint(); await Task.Delay(50); }
            Check(done.Task.IsCompleted, "Native custom Inspector rendered"); await done.Task; SceneView.duringSceneGui -= draw; draw = null;
            var preview = Preview(projectile, fan, sector, fallback);
            var integration = Integration(projectile, fan, sector);
            var compatibility = Waves();
            return new { passed = true, checks, undoRedo = true, prefabOverrides = true, inspectorRendered = true, preview, integration, compatibility, examples = paths };
        }
        finally
        {
            SceneView.duringSceneGui -= draw; if (editor != null) Object.DestroyImmediate(editor);
            Undo.ClearUndo(root.GetComponent<SimpleEnemyAttackExecutor>()); Object.DestroyImmediate(root); EditorSceneManager.ClosePreviewScene(scene);
        }
    }

    private static Paths CreateExamples(EnemyBullet prefab)
    {
        if (File.Exists(Manifest)) return JsonUtility.FromJson<Paths>(File.ReadAllText(Manifest));
        EnsureFolder(Folder);
        var paths = new Paths();
        var child = ScriptableObject.CreateInstance<EnemyProjectilePreset>();
        var childData = new EnemyShootingSettings(); childData.SetProjectileBaseSpeedOverride(true, 2.5f);
        Patch(childData, "{\"projectilePrefab\":{\"instanceID\":" + prefab.GetInstanceID() + "},\"overrideProjectileLifetime\":true,\"projectileLifetime\":2}"); child.SetSettings(childData);
        paths.child = Save(child, "BoundaryChild_Projectile");
        var fan = ScriptableObject.CreateInstance<EnemyAttackPatternPreset>();
        var fanData = new EnemyBurstAttackSettings(); fanData.ConfigureLegacySingleAttack(2, 1f, .25f); fanData.Shooting.ConfigureLegacyFan(3, -25f, 25f);
        fan.SetSettings(fanData, new EnemyPresetFiringSettings { enabled = true, fireMode = DirectedWaveAttackFireMode.Default });
        paths.fan = Save(fan, "Fan_AttackPattern");
        var sector = ScriptableObject.CreateInstance<EnemyAttackPatternPreset>();
        var sectorData = new EnemyBurstAttackSettings(); sectorData.ConfigureLegacyRepeatedBurst(2, 1.5f, .2f);
        sectorData.Shooting.EmissionLayout.ConfigureSector(-50f, 50f, 5, EnemySectorLaunchMode.Sequential, .08f, .05f);
        Patch(sectorData.Shooting.EmissionLayout, "{\"launchOrder\":2,\"resetOrderOnAttackStart\":false}");
        sector.SetSettings(sectorData, new EnemyPresetFiringSettings { enabled = true, fireMode = DirectedWaveAttackFireMode.Default });
        paths.sector = Save(sector, "SequentialSector_AttackPattern");
        var projectile = ScriptableObject.CreateInstance<EnemyProjectilePreset>();
        var data = new EnemyShootingSettings(); data.SetProjectileBaseSpeedOverride(true, 3f);
        Patch(data, "{\"projectilePrefab\":{\"instanceID\":" + prefab.GetInstanceID() + "},\"projectileMovementPattern\":3,\"projectileOrbitRadius\":0.2,\"projectileOrbitRadialSpeed\":2,\"projectileAngularSpeed\":45,\"overrideProjectileLifetime\":true,\"projectileLifetime\":8}");
        var children = new EnemyChildAttackSettings();
        Patch(children, "{\"triggerMode\":2,\"eventType\":5,\"triggerBoundaries\":15,\"timeOffset\":0.2,\"repeatCount\":1}");
        children.Pattern.AuthoredShooting.LinkProjectile(child);
        children.Pattern.AuthoredShooting.LinkPattern(fan);
        data.SetChildAttack(children); projectile.SetSettings(data);
        paths.projectile = Save(projectile, "OrbitBoundary_Projectile");
        foreach (var preset in new EnemyReusablePreset[] { child, fan, sector, projectile })
        {
            using var serialized = new SerializedObject(preset);
            serialized.FindProperty("category").stringValue = "Examples";
            serialized.FindProperty("description").stringValue = "Reusable preset example; existing waves and prefabs are unchanged.";
            serialized.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(preset); AssetDatabase.SaveAssetIfDirty(preset);
        }
        File.WriteAllText(Manifest, JsonUtility.ToJson(paths, true));
        return paths;
    }
    private static string Save(EnemyReusablePreset asset, string name)
    {
        string path = AssetDatabase.GenerateUniqueAssetPath(Folder + "/" + name + ".asset");
        AssetDatabase.CreateAsset(asset, path); AssetDatabase.SaveAssetIfDirty(asset); return path;
    }
    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = path.Substring(0, path.LastIndexOf('/')); EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, path.Substring(path.LastIndexOf('/') + 1));
    }
    private static object Preview(EnemyProjectilePreset projectile, EnemyAttackPatternPreset fan, EnemyAttackPatternPreset sector, EnemyBullet fallback)
    {
        int initialWorlds = Worlds(), maximum = 0;
        string first = null;
        for (int iteration = 0; iteration < 3; iteration++)
        {
            using (var session = new EnemyPresetPreviewSession(fan, projectile, fallback, 12345))
            {
                for (int frame = 0; frame <= 300; frame++) { session.Advance(frame / 60f); maximum = Mathf.Max(maximum, session.Count); }
                Check(session.Warning == null, "Library preview configured"); Check(session.Count > 0, "Library simulates projectile/child patterns");
                string fingerprint = Fingerprint(session.World.SimulationWorld);
                if (first == null) first = fingerprint; else Check(first == fingerprint, "Repeat same seed deterministic");
            }
            Check(Worlds() == initialWorlds, "Preview stopped/cleaned up");
        }
        using (var session = new EnemyPresetPreviewSession(sector, projectile, fallback, 12345))
        { session.Advance(2f); Check(session.Count > 0, "Sequential Sector preset uses same projectile"); }
        Check(Worlds() == initialWorlds, "Sequential preview cleaned up");
        var randomized = ScriptableObject.CreateInstance<EnemyProjectilePreset>();
        try
        {
            var data = new EnemyShootingSettings(); data.CopyFrom(projectile.Settings);
            Patch(data, "{\"projectileMovementPattern\":0,\"useChildAttackPattern\":false}");
            data.SetProjectileBaseSpeedRange(new RandomFloatRange { enabled = true, min = 1f, max = 5f, initialized = true }, EnemySpeedRandomizeOn.PerProjectile);
            randomized.SetSettings(data);
            string previous = null;
            for (int i = 0; i < 3; i++)
            {
                using var session = new EnemyPresetPreviewSession(fan, randomized, fallback, i < 2 ? 77 : 78);
                session.Advance(.5f); string signature = Fingerprint(session.World.SimulationWorld);
                if (i == 1) Check(signature == previous, "Random speed same Seed repeats exactly");
                if (i == 2) Check(signature != previous, "New Seed changes random speed result");
                previous = signature;
            }
        }
        finally { Object.DestroyImmediate(randomized); }
        Check(Worlds() == initialWorlds, "Random preview cleaned up");
        return new { maximumProjectiles = maximum, deterministic = true, cleanup = true };
    }
    private static object Integration(EnemyProjectilePreset projectile, EnemyAttackPatternPreset fan, EnemyAttackPatternPreset sector)
    {
        int nativeCount;
        var scene = EditorSceneManager.NewPreviewScene(); var world = new World("Reusable preset native verification", WorldFlags.Editor);
        var root = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefub/Enemies Prefab/Enemy.prefab"), scene);
        var spawner = EnemyProjectileEcsSpawner.CreateEditorPreview(world);
        try
        {
            var executor = root.GetComponent<SimpleEnemyAttackExecutor>(); Set(executor, "projectileSpawner", spawner);
            typeof(SimpleEnemyAttackExecutor).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(executor, null);
            var profile = new EnemyBurstAttackSettings(); profile.ConfigureLegacySingleAttack(1, 1f, .1f);
            profile.ApplyPatternPreset(fan, EnemyPresetUseMode.Linked); profile.AuthoredShooting.LinkProjectile(projectile);
            // The projectile preset supplies the visual/authoring prefab even if the executor has none.
            Set(executor, "defaultProjectilePrefab", null); executor.SetWaveAttackSettings(profile);
            executor.BeginAttackSequence(profile); executor.BeginEmissionOriginAttack(profile);
            Check(executor.CanPerformWaveAttack && executor.TryFireInDirection(Vector3.up, profile), "Native executor fires linked projectile/pattern without enemy prefab fallback");
            using var query = world.EntityManager.CreateEntityQuery(typeof(EnemyProjectileMovement), typeof(LocalTransform)); nativeCount = query.CalculateEntityCount();
            Check(nativeCount == 3, "Native linked Fan creates expected projectile count"); executor.EndAttackSequence(profile);
        }
        finally { Object.DestroyImmediate(root); spawner.Dispose(); world.Dispose(); EditorSceneManager.ClosePreviewScene(scene); }
        int total = 0;
        foreach (bool sequence in new[] { false, true })
        {
            var waveRoot = PrefabUtility.LoadPrefabContents("Assets/Waves/2/2_3_extrime.prefab");
            try
            {
                var wave = waveRoot.transform.Find("Main").GetComponent<DirectedEnemySubWave>(); var behaviour = wave.AttackPatternBehaviour;
                Set(behaviour, "attackPreset", null); ((System.Collections.IList)behaviour.GetType().GetField("attackPatterns", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(behaviour)).Clear();
                Set(behaviour, "executionMode", sequence ? DirectedWaveAttackExecutionMode.Sequence : DirectedWaveAttackExecutionMode.Patterns);
                var settings = (DirectedWaveAttackSettings)behaviour.GetType().GetField("attackSettings", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(behaviour);
                if (sequence)
                {
                    var step = new DirectedWaveAttackSequenceStep(); var pattern = new DirectedWaveAttackPattern(); Set(step, "pattern", pattern);
                    settings = (DirectedWaveAttackSettings)pattern.GetType().GetField("attackSettings", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(pattern);
                    behaviour.AttackSequence.steps = new() { step }; behaviour.AttackSequence.sequenceMode = DirectedWaveAttackSequenceMode.Loop; behaviour.AttackSequence.loopCount = 0;
                }
                Set(settings, "burstSettingsSource", DirectedWaveBurstSettingsSource.WaveOverride); Set(settings, "fireMode", DirectedWaveAttackFireMode.Default); Set(settings, "attacksPerSecond", 10f);
                settings.WaveBurstSettings.ApplyPatternPreset(sequence ? sector : fan, EnemyPresetUseMode.Linked);
                settings.WaveBurstSettings.AuthoredShooting.LinkProjectile(projectile);
                using var preview = new WaveShootingPreviewSession(wave, true, false, 0, Vector3.up * 100f);
                for (int frame = 0; frame < 100; frame++) preview.AdvanceTo(frame / 30f);
                Check(preview.ProjectileCount > 0, "Linked presets in " + (sequence ? "Sequence" : "Patterns") + " Preview Wave"); total += preview.ProjectileCount;
            }
            finally { PrefabUtility.UnloadPrefabContents(waveRoot); }
        }
        return new { nativeProjectiles = nativeCount, wavePreviewProjectiles = total, sequence = true };
    }
    private static string Fingerprint(World world)
    {
        using var query = world.EntityManager.CreateEntityQuery(typeof(LocalTransform), typeof(EnemyProjectileResolution));
        using var transforms = query.ToComponentDataArray<LocalTransform>(Allocator.Temp);
        var values = new List<string>(); foreach (var transform in transforms) values.Add(transform.Position.ToString());
        values.Sort(StringComparer.Ordinal); return string.Join(";", values);
    }
    public static object ReloadSaved()
    {
        checks = 0; var paths = JsonUtility.FromJson<Paths>(File.ReadAllText(Manifest));
        foreach (string path in new[] { paths.projectile, paths.child, paths.fan, paths.sector }) AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        var root = AssetDatabase.LoadAssetAtPath<EnemyProjectilePreset>(paths.projectile);
        var child = root.Settings.ChildAttack;
        Check(child != null && child.UsesScreenBoundary && child.TriggerBoundaries == EnemyChildAttackBoundaries.All, "Boundary after reload");
        Check(AssetDatabase.GetAssetPath(child.Pattern.AuthoredShooting.ProjectilePreset) == paths.child, "Nested projectile reference after reload");
        Check(AssetDatabase.GetAssetPath(child.Pattern.AuthoredShooting.AttackPatternPreset) == paths.fan, "Nested attack reference after reload");
        Check(root.Settings.ResolveProjectilePrefab(null) != null && child.Pattern.Shooting.ResolveProjectilePrefab(null) != null, "Native prefab references after reload");
        Check(!EnemyPresetDependencies.HasCycle(root), "Reloaded graph valid");
        Check(Mathf.Approximately(root.Settings.InitialSpeed, 3f) && Mathf.Approximately(child.TimeOffset, .2f), "Values after reload");
        return new { passed = true, checks, domainReload = true, assets = paths };
    }
    private static object Waves()
    {
        int waves = 0, subwaves = 0, slots = 0;
        var paths = new[] { "Assets/Waves/1/1_Extreme/1_1_extrime.prefab", "Assets/Waves/1/1_Extreme/1_2_extrime.prefab", "Assets/Waves/1/1_Extreme/1_3_extrime.prefab", "Assets/Waves/1/1_Extreme/1_4_extrime.prefab", "Assets/Waves/2/2_1_extrime.prefab", "Assets/Waves/2/2_2_extrime.prefab", "Assets/Waves/2/2_3_extrime.prefab" };
        foreach (string path in paths)
        {
            byte[] before = File.ReadAllBytes(path); var prefab = PrefabUtility.LoadPrefabContents(path); Check(prefab != null, "Wave loaded " + path); waves++;
            try
            {
            foreach (var wave in prefab.GetComponentsInChildren<DirectedEnemySubWave>(true))
            {
                subwaves++;
                for (int i = 0; i < wave.GetConfiguredEnemySlotCount(); i++)
                {
                    slots++; Check(wave.GetConfiguredEnemyPrefabForSlot(i) != null, "Enemy slot reference valid: " + path + " / " + wave.name + " / " + i);
                    if (wave.AttackPatternBehaviour == null) continue;
                    var resolved = new DirectedWaveAttackSettings();
                    if (!wave.AttackPatternBehaviour.TryCopyResolvedAttackSettingsTo(i, false, resolved)) continue;
                    resolved.Validate();
                    for (int p = 0; p < resolved.WaveBurstSettings.PatternCount; p++)
                    {
                        var profile = resolved.WaveBurstSettings.GetPattern(p);
                        Check(ReferenceEquals(profile.ResolvePresets(), profile), "Legacy profile has no new linked dependency");
                    }
                }
            }
            }
            finally { PrefabUtility.UnloadPrefabContents(prefab); }
            Check(Convert.ToBase64String(before) == Convert.ToBase64String(File.ReadAllBytes(path)), "Wave prefab bytes unchanged");
        }
        return new { waves, subwaves, slots, filesUnchanged = true };
    }
}
