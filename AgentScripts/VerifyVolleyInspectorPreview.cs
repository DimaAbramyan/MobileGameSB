using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public sealed class VolleyPreviewGuiHost : ScriptableObject { public EnemyBurstAttackSettings profile = new EnemyBurstAttackSettings(); }

public static class VerifyVolleyInspectorPreview
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static int checks;
    private static SceneView view;
    private static Vector3 savedPivot;
    private static Quaternion savedRotation;
    private static float savedSize;
    private static bool savedOrtho, saved2D;
    private static VolleyPreviewGuiHost host;
    private static EnemyShootingSettings[] examples;
    private static string guiError;
    private static bool guiVerified;

    public static object Begin()
    {
        if (EditorApplication.isPlaying || EditorApplication.isCompiling || EditorApplication.isUpdating) throw new Exception("Editor must be idle.");
        checks = 0; guiVerified = false; guiError = null;
        VerifyFieldsAndGeometry();
        VerifyInitializers();
        host = ScriptableObject.CreateInstance<VolleyPreviewGuiHost>(); host.hideFlags = HideFlags.HideAndDontSave;
        host.profile.ConfigureLegacyRepeatedBurst(3, 1, .1f);
        host.profile.Shooting.EmissionLayout.ConfigureSector(330, 30, 5, EnemySectorLaunchMode.Sequential, .2f, .1f, reverse: true);
        host.profile.Shooting.ConfigureLegacyFan(3, -12, 12);
        examples = new EnemyShootingSettings[4];
        for (int i = 0; i < 4; i++) { examples[i] = new EnemyShootingSettings(); examples[i].Validate(); }
        examples[0].EmissionLayout.Configure(EnemyEmissionLayoutType.Point, pointOffset: new Vector3(.25f, .15f)); examples[0].ConfigureLegacyFan(5, -35, 35);
        examples[1].EmissionLayout.ConfigureSector(330, 30, 5, EnemySectorLaunchMode.Sequential, .2f, .1f); examples[1].ConfigureLegacyFan(3, -10, 10);
        examples[2].EmissionLayout.Configure(EnemyEmissionLayoutType.Radial, 8); Set(examples[2], "shotGunMode", EnemyProjectileShotGunMode.ShotGun);
        Set(examples[2], "shotGunProjectileCount", 3); Set(examples[2], "shotGunDirection", Vector2.up);
        examples[3].EmissionLayout.Configure(EnemyEmissionLayoutType.Circle, 4, .65f); examples[3].ConfigureLegacyFan(3, -20, 20);
        Set(examples[3], "shotGunMode", EnemyProjectileShotGunMode.ShotGun); Set(examples[3], "shotGunProjectileCount", 2); Set(examples[3], "shotGunDirection", Vector2.up);
        view = SceneView.lastActiveSceneView;
        if (view == null) throw new Exception("Open a Scene View to verify rendering.");
        savedPivot = view.pivot; savedRotation = view.rotation; savedSize = view.size; savedOrtho = view.orthographic; saved2D = view.in2DMode;
        view.in2DMode = true; view.pivot = new Vector3(200, 200, 0); view.rotation = Quaternion.identity; view.size = 5.5f; view.orthographic = true;
        SceneView.duringSceneGui += Draw;
        view.Repaint();
        return new { ready = true, checks, capture = "Scene View examples: Point+Fan, Sector+Fan, Radial+Stack, Circle+Fan+Stack" };
    }

    public static object Finish()
    {
        // Each run_script call loads a new ephemeral assembly. Locate the installed callback's owner.
        if (host == null)
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var owner = assembly.GetType("VerifyVolleyInspectorPreview");
                if (owner == null || owner == typeof(VerifyVolleyInspectorPreview)) continue;
                var installedHost = owner.GetField("host", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null) as UnityEngine.Object;
                if (installedHost != null) return owner.GetMethod("Finish").Invoke(null, null);
            }
        SceneView.duringSceneGui -= Draw;
        if (view != null)
        {
            view.in2DMode = saved2D; view.pivot = savedPivot; view.rotation = savedRotation; view.size = savedSize; view.orthographic = savedOrtho; view.Repaint();
        }
        if (host != null) UnityEngine.Object.DestroyImmediate(host);
        host = null;
        examples = null;
        if (guiError != null) throw new Exception(guiError);
        Assert(guiVerified, "Scene GUI drew and verified the Inspector.");
        return new { passed = true, checks, tested = "unique Inspector fields, group separation/folding, source priority, unchanged serialized settings, curve/burst heights, speed/lifetime initializers, sector/fan boundaries and actual Scene GUI rendering" };
    }

    private static void Draw(SceneView current)
    {
        if (current != view || Event.current.type != EventType.Repaint) return;
        Vector3 center = new Vector3(200, 200, 0);
        Vector3[] offsets = { new Vector3(-2.4f, 1.6f), new Vector3(2.4f, 1.6f), new Vector3(-2.4f, -1.8f), new Vector3(2.4f, -1.8f) };
        string[] labels = { "Point + Fan (5)", "Sector + Fan (5 × 3)", "Radial + Stack (8 × 3)", "Circle + Fan + Stack (4 × 3 × 2)" };
        Handles.Label(center + new Vector3(-4, 4), "VOLLEY STRUCTURE · cyan emitters / blue base / pink Sector / yellow Fan / orange projectiles");
        for (int i = 0; i < examples.Length; i++)
        {
            Handles.Label(center + offsets[i] + Vector3.down * .6f, labels[i]);
            EnemyShootingPreview.Draw(examples[i], center + offsets[i], Quaternion.identity, 0);
        }
        if (guiVerified || guiError != null) return;
        Handles.BeginGUI();
        try
        {
            var serialized = new SerializedObject(host);
            var timing = serialized.FindProperty("profile"); var shooting = timing.FindPropertyRelative("shooting");
            string unchanged = JsonUtility.ToJson(host.profile);
            foreach (bool behaviour in new[] { true, false })
                foreach (bool projectile in new[] { true, false })
                {
                    EnemyShootingSettingsDrawer.SetGroupExpanded(shooting, false, behaviour);
                    EnemyShootingSettingsDrawer.SetGroupExpanded(shooting, true, projectile);
                    float expected = EnemyShootingSettingsDrawer.SettingsHeight(shooting, timing, true);
                    var rect = new Rect(-5000, 0, 500, expected);
                    EnemyShootingSettingsDrawer.DrawSettings(ref rect, shooting, timing, true);
                    Near(rect.y, expected, "Rendered Inspector height matches reserved height for both groups.");
                }
            serialized.ApplyModifiedProperties();
            Assert(JsonUtility.ToJson(host.profile) == unchanged, "Drawing and collapsing groups never changes authored data.");
            guiVerified = true;
        }
        catch (Exception error) { guiError = error.ToString(); }
        finally { Handles.EndGUI(); }
    }

    private static void VerifyFieldsAndGeometry()
    {
        var testHost = ScriptableObject.CreateInstance<VolleyPreviewGuiHost>();
        var localObject = new GameObject("Inspector source verification"); localObject.hideFlags = HideFlags.HideAndDontSave; localObject.SetActive(false);
        try
        {
            foreach (EnemyEmissionLayoutType type in Enum.GetValues(typeof(EnemyEmissionLayoutType)))
                foreach (EnemyProjectileInitialSpeedMode speed in Enum.GetValues(typeof(EnemyProjectileInitialSpeedMode)))
                {
                    var shooting = testHost.profile.Shooting;
                    shooting.EmissionLayout.Configure(type, 5); shooting.ConfigureLegacyFan(3, -20, 35);
                    Set(shooting, "shotGunMode", EnemyProjectileShotGunMode.ShotGun); Set(shooting, "initialSpeedMode", speed);
                    Set(shooting, "projectileMovementPattern", EnemyProjectileMovementPattern.Orbit);
                    Set(shooting, "projectileOrbitCenterMode", EnemyProjectileOrbitCenterMode.DnaLike);
                    var property = new SerializedObject(testHost).FindProperty("profile").FindPropertyRelative("shooting");
                    var behaviour = Fields("BehaviourFields", property); var projectiles = Fields("ProjectileFields", property);
                    var unique = new HashSet<string>();
                    foreach (string field in behaviour) Assert(unique.Add(field) && property.FindPropertyRelative(field) != null, "Behaviour field exists once: " + field);
                    foreach (string field in projectiles) Assert(unique.Add(field) && property.FindPropertyRelative(field) != null, "Projectile field exists once: " + field);
                    Assert(behaviour.Contains("emissionLayout") && behaviour.Contains("shotGunMode") && behaviour.Contains("fanMode") && behaviour.Contains("layoutRotationEnabled"), "Formation belongs to Shooting Behaviour.");
                    Assert(projectiles.Contains("projectileType") && projectiles.Contains("projectileMovementPattern") && projectiles.Contains("overrideProjectileLifetime") && projectiles.Contains("projectileAngularSpeed"), "Created projectile settings have one group.");
                    Assert(!behaviour.Contains("shotGunInitialSpeed") && !projectiles.Contains("shotGunDirection"), "Stack speed and direction have distinct groups.");
                    Assert(projectiles.Contains("shotGunInitialSpeed") == (speed != EnemyProjectileInitialSpeedMode.Random), "Unused base speed is hidden for Random mode.");
                    EnemyShootingSettingsDrawer.SetGroupExpanded(property, false, true); EnemyShootingSettingsDrawer.SetGroupExpanded(property, true, true);
                    float full = EnemyShootingSettingsDrawer.SettingsHeight(property);
                    EnemyShootingSettingsDrawer.SetGroupExpanded(property, true, false);
                    Assert(EnemyShootingSettingsDrawer.SettingsHeight(property) < full, "Foldout makes Inspector smaller.");
                    EnemyShootingSettingsDrawer.SetGroupExpanded(property, true, true);
                }
            var sector = testHost.profile.Shooting; sector.EmissionLayout.ConfigureSector(350, 10, 1);
            Set(sector, "shotGunDirection", Vector2.up);
            var frame = new EnemyEmissionPreviewFrame { EnemyPosition = new Vector3(2, 3), ShootingPoint = new Vector3(2, 3), Scale = Vector3.one, EnemyBasis = Quaternion.identity, PointBasis = Quaternion.identity };
            var emitter = EnemyShootingPreviewGeometry.GetEmitter(sector, 0, frame, Quaternion.identity, 0);
            Near(emitter.BaseDirection, Vector3.up, "Single Sector ray uses the midpoint across zero.");
            Near(EnemyShootingPreviewGeometry.SectorStartDirection(sector, emitter), Quaternion.Euler(0, 0, 350) * Vector3.up, "Sector boundary remains Start Angle with count one.");
            Near(EnemyShootingPreviewGeometry.SectorEndDirection(sector, emitter), Quaternion.Euler(0, 0, 370) * Vector3.up, "Sector boundary remains End Angle with count one.");
            var local = localObject.AddComponent<DirectedWaveEnemyOverride>();
            VerifySpeedMigration(local);
            var localSerialized = new SerializedObject(local);
            var localShooting = localSerialized.FindProperty("attackPattern").FindPropertyRelative("shooting");
            var localFields = Fields("ProjectileFields", localShooting);
            Assert(localFields.Contains("overrideProjectileBaseSpeed") && localFields.Contains("projectileBaseSpeed") && !localFields.Contains("shotGunInitialSpeed"), "Only the pattern's effective base speed is editable.");
            Assert(EnemyShootingSettingsDrawer.SpeedSource(localShooting).Contains("This pattern"), "Inspector states the single editable source.");
        }
        finally { UnityEngine.Object.DestroyImmediate(testHost); UnityEngine.Object.DestroyImmediate(localObject); }
    }

    private static void VerifySpeedMigration(DirectedWaveEnemyOverride local)
    {
        var profile = new EnemyBurstAttackSettings(); profile.Validate();
        profile.Shooting.ConfigureLegacyFan(5, -25, 35);
        profile.EnableMultiplePatterns();
        var second = new EnemyAttackPatternSettings(); second.CopyFrom(profile.GetPattern(0));
        second.Shooting.SetProjectileBaseSpeedOverride(true, 3); profile.AddPattern(second);
        Set(local, "attackPattern", profile); Set(local, "attackOverrideVersion", 2);
        Set(local, "overrideAttackPattern", true); Set(local, "overrideProjectileBaseSpeed", true); Set(local, "projectileBaseSpeed", 7f);
        Assert(local.EnsureAttackOverrideMigration(), "Version 2 common speed migrates.");
        Assert(!(bool)typeof(DirectedWaveEnemyOverride).GetField("overrideProjectileBaseSpeed", Private).GetValue(local), "Legacy common flag is retired.");
        for (int p = 0; p < profile.PatternCount; p++)
        {
            Assert(profile.GetPattern(p).Shooting.OverridesProjectileBaseSpeed, "Every pattern owns its migrated override.");
            Near(profile.GetPattern(p).Shooting.InitialSpeed, 7, "Migrated speed replaces previously shadowed values.");
        }
        Near(profile.Shooting.InitialSpeed, 7, "Inactive single-pattern configuration migrates too.");
        string migrated = EditorJsonUtility.ToJson(local);
        Assert(!local.EnsureAttackOverrideMigration() && EditorJsonUtility.ToJson(local) == migrated, "Migration is byte-stable when repeated.");
        var output = new EnemyBurstAttackSettings(); output.Validate(); local.ApplyAttackPatternOverride(output, 11);
        for (int p = 0; p < output.PatternCount; p++) Near(output.GetPattern(p).Shooting.InitialSpeed, 7, "Old common speed still wins over shared wave speed.");
        Near(profile.GetPattern(0).Shooting.FanMinAngle, -25, "Migration preserves Fan geometry.");
        EditorJsonUtility.FromJsonOverwrite(migrated, local);
        Assert(!local.EnsureAttackOverrideMigration(), "Serialized round-trip keeps the migration version.");
        local.ApplyAttackPatternOverride(output, 11);
        Near(output.GetPattern(1).Shooting.InitialSpeed, 7, "Serialized multiple patterns retain migrated speed.");
        profile = new EnemyBurstAttackSettings(); profile.Validate(); profile.EnableMultiplePatterns();
        var first = new EnemyAttackPatternSettings(); first.CopyFrom(profile); first.Shooting.SetProjectileBaseSpeedOverride(true, 5);
        var fallback = new EnemyAttackPatternSettings(); fallback.CopyFrom(profile); fallback.Shooting.SetProjectileBaseSpeedOverride(false, 1);
        Set(profile, "patterns", new List<EnemyAttackPatternSettings> { first, fallback }); profile.Validate(); Set(local, "attackPattern", profile);
        local.ApplyAttackPatternOverride(output, 11);
        Near(output.GetPattern(0).Shooting.InitialSpeed, 5, "Explicit local speed wins.");
        Near(output.GetPattern(1).Shooting.InitialSpeed, 11, "Unspecified local speed inherits wave fallback.");
        Set(local, "overrideAttackPattern", false); Set(local, "attackOverrideVersion", 2);
        Set(local, "overrideProjectileBaseSpeed", true); Set(local, "projectileBaseSpeed", -2f);
        Assert(local.EnsureAttackOverrideMigration() && !local.HasAttackPatternOverride, "Disabled slots migrate without enabling shooting.");
        Near(profile.GetPattern(0).Shooting.InitialSpeed, .01f, "Invalid legacy speed is safely clamped.");
        var simple = new EnemyBurstAttackSettings(); simple.Validate(); simple.Shooting.SetProjectileBaseSpeedOverride(true, 7); Set(local, "attackPattern", simple);
    }

    private static void VerifyInitializers()
    {
        var clone = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefub/Enemies Prefab/Enemy.prefab"));
        UnityEditor.Editor editor = null;
        try
        {
            var executor = clone.GetComponent<SimpleEnemyAttackExecutor>(); editor = UnityEditor.Editor.CreateEditor(executor);
            var serialized = editor.serializedObject; var profile = serialized.FindProperty("burstAttackSettings"); var shooting = profile.FindPropertyRelative("shooting");
            shooting.FindPropertyRelative("overrideProjectileBaseSpeed").boolValue = false; shooting.FindPropertyRelative("overrideProjectileLifetime").boolValue = false;
            editor.GetType().GetMethod("RememberProjectileOverrides", Private).Invoke(editor, new object[] { profile });
            shooting.FindPropertyRelative("overrideProjectileBaseSpeed").boolValue = true; shooting.FindPropertyRelative("overrideProjectileLifetime").boolValue = true;
            var type = (EnemyProjectileType)shooting.FindPropertyRelative("projectileType").enumValueIndex;
            Assert(executor.TryGetProjectileBaseSpeed(type, out float speed) && executor.TryGetProjectileBaseLifetime(type, out _), "Selected projectile exposes authored defaults.");
            executor.TryGetProjectileBaseLifetime(type, out float lifetime);
            editor.GetType().GetMethod("InitializeJustEnabledProjectileOverrides", Private).Invoke(editor, new object[] { profile });
            Near(shooting.FindPropertyRelative("projectileBaseSpeed").floatValue, speed, "Base-speed initializer still reads the projectile prefab.");
            Near(shooting.FindPropertyRelative("projectileLifetime").floatValue, lifetime, "Lifetime initializer still reads the projectile prefab.");
        }
        finally { if (editor != null) UnityEngine.Object.DestroyImmediate(editor); UnityEngine.Object.DestroyImmediate(clone); }
    }
    private static HashSet<string> Fields(string method, SerializedProperty property) => new HashSet<string>((IEnumerable<string>)typeof(EnemyShootingSettingsDrawer).GetMethod(method, BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { property }));
    private static void Set(object obj, string field, object value) => obj.GetType().GetField(field, Private).SetValue(obj, value);
    private static void Assert(bool value, string message) { checks++; if (!value) throw new Exception(message); }
    private static void Near(float a, float b, string message) => Assert(Mathf.Abs(a - b) < .01f, message + $" ({a} != {b})");
    private static void Near(Vector3 a, Vector3 b, string message) => Assert((a - b).sqrMagnitude < .00001f, message);
}
