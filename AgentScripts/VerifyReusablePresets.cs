using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static class VerifyReusablePresets
{
    private static int checks;
    private static readonly List<Object> objects = new();
    private static T New<T>() where T : ScriptableObject
    { var value = ScriptableObject.CreateInstance<T>(); objects.Add(value); return value; }
    private static void Check(bool value, string message)
    { checks++; if (!value) throw new Exception("Preset check failed: " + message); }
    private static void Near(float actual, float expected, string message) => Check(Mathf.Abs(actual - expected) < .0001f, message + $" ({actual} != {expected})");
    private static void Patch(object value, string json) => JsonUtility.FromJsonOverwrite(json, value);
    private static EnemyBurstAttackSettings Profile(int count = 1)
    { var value = new EnemyBurstAttackSettings(); value.ConfigureLegacySingleAttack(count, 1.5f, .2f); return value; }

    public static string Run()
    {
        checks = 0;
        try
        {
            Independent(); LinkedAndCopy(); NestedAndCycles(); LaserAndLeafOverrides(); MultipleAndRuntimeOverrides();
            return JsonUtility.ToJson(new Result { checks = checks, success = true });
        }
        finally { foreach (var obj in objects) if (obj != null) Object.DestroyImmediate(obj); objects.Clear(); }
    }
    [Serializable] private sealed class Result { public int checks; public bool success; }
    private static EnemyProjectilePreset Orbit()
    {
        var preset = New<EnemyProjectilePreset>(); preset.name = "Orbit boundary";
        var shooting = new EnemyShootingSettings();
        Patch(shooting, "{\"projectileMovementPattern\":3,\"projectileAngularSpeed\":90,\"overrideProjectileBaseSpeed\":true,\"projectileBaseSpeed\":4,\"overrideProjectileLifetime\":true,\"projectileLifetime\":8,\"projectileSpeedBehavior\":1}");
        var child = new EnemyChildAttackSettings();
        Patch(child, "{\"triggerMode\":2,\"eventType\":5,\"triggerBoundaries\":3,\"timeOffset\":0.35,\"repeatCount\":2,\"destroyParent\":true}");
        child.Pattern.AuthoredShooting.ConfigureLegacyFan(5, -30, 30);
        shooting.SetChildAttack(child); preset.SetSettings(shooting); return preset;
    }
    private static EnemyAttackPatternPreset Fan()
    {
        var preset = New<EnemyAttackPatternPreset>(); preset.name = "Rotating Fan";
        var profile = Profile(3); profile.AuthoredShooting.ConfigureLegacyFan(4, -40f, 40f);
        profile.AuthoredShooting.SetLayoutRotationEnabled(true);
        preset.SetSettings(profile, new EnemyPresetFiringSettings { enabled = true, fireMode = DirectedWaveAttackFireMode.Aimed, aimMode = DirectedWaveAttackAimMode.AimOnBurst });
        return preset;
    }
    private static void Independent()
    {
        var projectile = Orbit(); var pattern = Fan(); var profile = Profile(7);
        profile.AuthoredShooting.EmissionLayout.Configure(EnemyEmissionLayoutType.Circle, 8);
        string layout = JsonUtility.ToJson(profile.Shooting.EmissionLayout);
        profile.AuthoredShooting.LinkProjectile(projectile);
        Check(JsonUtility.ToJson(profile.Shooting.EmissionLayout) == layout, "projectile leaves geometry unchanged");
        Check(profile.AttackShotCount == 7, "projectile leaves timing unchanged");
        Check(profile.Shooting.MovementPattern == projectile.Settings.MovementPattern, "projectile movement applied");
        string behaviour = JsonUtility.ToJson(profile.Shooting.ChildAttack);
        profile.ApplyPatternPreset(pattern, EnemyPresetUseMode.Linked);
        Check(profile.Shooting.FanProjectileCount == 4 && profile.AttackShotCount == 3, "pattern geometry/timing");
        Check(JsonUtility.ToJson(profile.Shooting.ChildAttack) == behaviour, "pattern leaves child configuration unchanged");
        Near(profile.Shooting.InitialSpeed, 4f, "pattern leaves projectile speed unchanged");
        Check(profile.Shooting.PresetFiring.aimMode == DirectedWaveAttackAimMode.AimOnBurst, "fire policy captured");
        Check(ReferenceEquals(profile.Shooting, profile.Shooting), "resolved profile cached");
        Check(!ReferenceEquals(profile.Shooting.ChildAttack, projectile.Settings.ChildAttack), "child deep copy");
        Check(!ReferenceEquals(profile.Shooting.SpeedByDistance, projectile.Settings.SpeedByDistance), "curve deep copy");
        var replacement = New<EnemyProjectilePreset>(); var settings = new EnemyShootingSettings(); settings.SetProjectileType(EnemyProjectileType.Laser); replacement.SetSettings(settings);
        layout = JsonUtility.ToJson(profile.Shooting.EmissionLayout);
        profile.AuthoredShooting.LinkProjectile(replacement); profile.Validate();
        Check(profile.Shooting.ProjectileType == EnemyProjectileType.Laser, "projectile replaced");
        Check(JsonUtility.ToJson(profile.Shooting.EmissionLayout) == layout && profile.AttackShotCount == 3, "projectile replacement independent");
        Check(profile.AuthoredShooting.ProjectilePreset == replacement, "reference retained");
    }
    private static void LinkedAndCopy()
    {
        var asset = Orbit(); var pattern = Fan(); var linked = Profile(); linked.AuthoredShooting.LinkProjectile(asset);
        linked.ApplyPatternPreset(pattern, EnemyPresetUseMode.Linked);
        linked.AuthoredShooting.SetProjectileBaseSpeedOverride(true, 7f);
        linked.AuthoredShooting.ProjectilePresetOverrides.Add("projectileBaseSpeed"); linked.Validate();
        Near(linked.Shooting.InitialSpeed, 7f, "local speed override");
        var update = new EnemyShootingSettings(); update.CopyFrom(asset.Settings); update.SetProjectileBaseSpeedOverride(true, 6f);
        Patch(update, "{\"projectileAngularSpeed\":123}"); asset.SetSettings(update);
        Near(linked.Shooting.InitialSpeed, 7f, "override survives asset update"); Near(linked.Shooting.AngularSpeed, 123, "unmodified field follows asset");
        var copied = Profile(); copied.AuthoredShooting.LinkProjectile(asset, EnemyPresetUseMode.Copy);
        Near(copied.Shooting.InitialSpeed, 6f, "copy initial value");
        update.SetProjectileBaseSpeedOverride(true, 9f); asset.SetSettings(update);
        Near(copied.Shooting.InitialSpeed, 6f, "copy independent after asset update");
        Near(linked.Shooting.InitialSpeed, 7f, "linked override still 7");
        linked.AuthoredShooting.ProjectilePresetOverrides.Clear(); linked.Validate(); Near(linked.Shooting.InitialSpeed, 9f, "reset override inherits");
        linked.AuthoredShooting.AttackPatternPresetOverrides.Add("attackCooldown");
        Patch(linked, "{\"attackCooldown\":12}"); linked.Validate(); Near(linked.AttackCooldown, 12f, "local cooldown");
        var changed = Profile(8); changed.AuthoredShooting.ConfigureLegacyFan(9, -90f, 90f); pattern.SetSettings(changed);
        Check(linked.AttackShotCount == 8 && linked.Shooting.FanProjectileCount == 9, "pattern linked updates"); Near(linked.AttackCooldown, 12f, "cooldown remains local");
        linked.UnlinkPreset(true); Check(linked.AuthoredShooting.ProjectilePreset == null && linked.AuthoredShooting.AttackPatternPreset == pattern, "unlink one category");
        Near(linked.Shooting.InitialSpeed, 9f, "unlink retains effective speed");
        update.SetProjectileBaseSpeedOverride(true, 15f); asset.SetSettings(update); Near(linked.Shooting.InitialSpeed, 9f, "unlinked independent");
        var patternCopy = Profile(); patternCopy.ApplyPatternPreset(pattern, EnemyPresetUseMode.Copy);
        pattern.SetSettings(Profile(2)); Check(patternCopy.AttackShotCount == 8, "pattern Copy independent");
        var cloned = Profile(); cloned.CopyFrom(copied); Check(cloned.AuthoredShooting.ProjectilePreset == asset && cloned.AuthoredShooting.ProjectilePresetMode == EnemyPresetUseMode.Copy, "binding copied");
        Check(!ReferenceEquals(cloned.AuthoredShooting.ProjectilePresetOverrides, copied.AuthoredShooting.ProjectilePresetOverrides), "override list cloned");
    }
    private static void NestedAndCycles()
    {
        var leaf = New<EnemyProjectilePreset>(); var leafSettings = new EnemyShootingSettings(); leafSettings.SetProjectileType(EnemyProjectileType.Homing); leaf.SetSettings(leafSettings);
        var parent = Orbit(); parent.Settings.ChildAttack.Pattern.AuthoredShooting.LinkProjectile(leaf);
        var pattern = Fan(); parent.Settings.ChildAttack.Pattern.AuthoredShooting.LinkPattern(pattern);
        parent.NotifyChanged(); Check(!EnemyPresetDependencies.HasCycle(parent), "valid branching graph");
        var profile = Profile(); profile.AuthoredShooting.LinkProjectile(parent);
        Check(profile.Shooting.ChildAttack.Pattern.Shooting.ProjectileType == EnemyProjectileType.Homing, "nested linked projectile");
        Check(profile.Shooting.ChildAttack.Pattern.RuntimeSettings.AttackShotCount == 3, "nested linked timing");
        var child = profile.Shooting.ChildAttack;
        Check(child.TriggerBoundaries == (EnemyChildAttackBoundaries.Left | EnemyChildAttackBoundaries.Right), "boundary selection copied");
        Near(child.TimeOffset, .35f, "boundary delay copied"); Check(child.RepeatCount == 2 && child.DestroyParent, "repeat/destroy copied");
        Check(child.FireMode == DirectedWaveAttackFireMode.Aimed && child.AimMode == DirectedWaveAttackAimMode.AimOnBurst, "child preset firing policy");
        // Simulate a malformed serialized reference; inspectors show this and runtime refuses it.
        leaf.Settings.SetChildAttack(new EnemyChildAttackSettings());
        leaf.Settings.ChildAttack.Pattern.AuthoredShooting.LinkProjectile(parent); leaf.NotifyChanged();
        Check(EnemyPresetDependencies.HasCycle(parent), "cycle detected across child presets");
        bool rejected = false; try { Profile().AuthoredShooting.LinkProjectile(parent); } catch (InvalidOperationException) { rejected = true; }
        Check(rejected, "cycle rejected");
        leaf.Settings.SetChildAttack(null); leaf.NotifyChanged();
        Check(!EnemyPresetDependencies.HasCycle(parent), "cycle removal works");
    }
    private static void LaserAndLeafOverrides()
    {
        var asset = New<EnemyProjectilePreset>(); var settings = new EnemyShootingSettings(); settings.SetProjectileType(EnemyProjectileType.Laser);
        Patch(settings.Laser, "{\"laserDuration\":2,\"geometry\":{\"laserWidth\":0.1},\"enableTelegraph\":true}"); asset.SetSettings(settings);
        var profile = Profile(); profile.AuthoredShooting.LinkProjectile(asset);
        Patch(profile.AuthoredShooting.Laser, "{\"geometry\":{\"laserWidth\":0.75}}");
        profile.AuthoredShooting.ProjectilePresetOverrides.Add("laser.geometry.laserWidth"); profile.Validate();
        Patch(settings.Laser, "{\"laserDuration\":4,\"geometry\":{\"laserWidth\":0.3}}"); asset.SetSettings(settings);
        Check(JsonUtility.ToJson(profile.Shooting.Laser).Contains("0.75"), "nested laser width override");
        Check(JsonUtility.ToJson(profile.Shooting.Laser).Contains("\"laserDuration\":4"), "other laser field follows asset");
        var pattern = Fan(); profile.ApplyPatternPreset(pattern, EnemyPresetUseMode.Linked);
        profile.AuthoredShooting.EmissionOrigin.X.Configure(EnemyEmissionOriginSource.World, 15f);
        profile.AuthoredShooting.AttackPatternPresetOverrides.Add("shooting.emissionOrigin.x.offset"); profile.Validate();
        Near(profile.Shooting.EmissionOrigin.X.Offset, 15f, "individual emission axis override");
        Check(!ReferenceEquals(profile.Shooting.Laser, asset.Settings.Laser), "laser cloned");
    }
    private static void MultipleAndRuntimeOverrides()
    {
        var asset = Orbit(); var pattern = Fan(); var profile = Profile(); profile.AuthoredShooting.LinkProjectile(asset); profile.ApplyPatternPreset(pattern, EnemyPresetUseMode.Linked);
        profile.EnableMultiplePatterns(); var second = new EnemyAttackPatternSettings(); second.CopyFrom(profile.GetPattern(0)); profile.AddPattern(second);
        Check(profile.PatternCount == 2, "multiple retained");
        for (int i = 0; i < 2; i++) { Near(profile.GetPattern(i).Shooting.InitialSpeed, 4f, "multiple projectile binding"); Check(profile.GetPattern(i).AttackShotCount == 3, "multiple pattern binding"); }
        var copy = Profile(); copy.CopyFrom(profile); copy.MaterializePresets(); copy.SetRuntimeProjectileBaseSpeedOverrideForAllPatterns(20f); copy.ScaleAttackTiming(2f);
        for (int i = 0; i < 2; i++)
        { Near(copy.GetPattern(i).Shooting.InitialSpeed, 20f, "runtime shared speed"); Near(copy.GetPattern(i).AttackShotInterval, .1f, "runtime timing multiplier"); }
        var keyProperty = typeof(EnemyEmissionLayoutSettings).GetProperty("OrderKey", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        var keys = new[] { keyProperty.GetValue(profile.GetPattern(0).Shooting.EmissionLayout), keyProperty.GetValue(profile.GetPattern(1).Shooting.EmissionLayout) };
        Check(!keys[0].Equals(keys[1]), "independent launch order source per pattern");
        var plain = Profile(4); string before = JsonUtility.ToJson(plain); Check(ReferenceEquals(plain, plain.ResolvePresets()), "legacy fast path");
        Check(before == JsonUtility.ToJson(plain), "plain resolution unchanged");
    }
}
