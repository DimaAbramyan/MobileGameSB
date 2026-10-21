using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

// Field names are shared by the resolver, save/copy operations and Inspector override picker.
// Existing serialized models remain authoritative; no parallel projectile/pattern model is introduced.
public static class EnemyPresetFields
{
    public static readonly string[] Projectile = {
        "projectileOrbitFixedWorldCenterXRandom", "projectileOrbitFixedWorldCenterYRandom",
        "projectileLifetimeRandom", "projectileSpeedChangeYRandom", "projectileAngularSpeedRandom", "projectileLateralSpeedRandom", "projectileOrbitRadiusRandom", "projectileOrbitRadialSpeedRandom", "projectileDnaWeaveSpeedRandom", "projectileSizeChangeYRandom", "projectileSizeChangeSpeedRandom", "projectileFinalSizeRandom", "consistentInitialSpeedChangePerVolleyRandom",
        "projectileType", "projectilePrefab", "independentProjectileSpeed", "laser", "overwriteProjectileCharacteristic", "projectileCharacteristic",
        "overwriteBaseColour", "projectileColour", "overrideProjectileBaseSpeed", "projectileBaseSpeed",
        "projectileBaseSpeedRandom", "speedRandomizeOn", "seededInitialSpeedRandom", "numericRandomSeed",
        "shotGunInitialSpeed", "shotGunInitialSpeedRandom", "overrideProjectileLifetime", "projectileLifetime",
        "initialSpeedMode", "randomInitialSpeedMinimum", "randomInitialSpeedMaximum", "consistentInitialSpeedChangePerVolley",
        "projectileSpeedBehavior", "projectileSpeedChangeY", "projectileSpeedByDistance", "projectileSpeedCurveTarget",
        "projectileMovementPattern", "projectileAngularSpeed", "projectileLateralSpeed", "projectileOrbitCenterMode",
        "projectileOrbitFixedWorldCenter", "projectileOrbitRadius", "projectileOrbitRadialSpeed", "projectileDnaWeaveSpeed",
        "projectileSizeBehavior", "projectileSizeChangeY", "projectileSizeChangeSpeed", "projectileSizeChangeCurve",
        "projectileFinalSize", "useChildAttackPattern"
    };
    public static readonly string[] Layout = {
        "shotGunDirectionXRandom", "shotGunDirectionYRandom",
        "fanProjectileCountRandom", "fanMinAngleRandom", "fanMaxAngleRandom", "shotGunProjectileCountRandom", "randomMinAngleRandom", "randomMaxAngleRandom",
        "shootingType", "emissionOrigin", "emissionLayout", "layoutRotationEnabled", "emissionLayoutVersion",
        "shootingMode", "fanMode", "projectilePatternVersion", "directionCount", "fanProjectileCount", "fanMinAngle",
        "fanMaxAngle", "randomMinAngle", "randomMaxAngle", "shotGunMode", "shotGunProjectileCount", "shotGunDirection",
        "rotate", "rotation", "presetFiring"
    };
    public static readonly string[] Timing = {
        "attackStartDelayRandom", "attackShotCountRandom", "attackShotIntervalRandom", "burstShotCountRandom", "burstShotIntervalRandom",
        "useAttackStartDelay", "attackStartDelay", "burstAttack", "attackShotCount", "attackShotInterval",
        "attackCooldown", "attackCooldownRandom", "burstShotCount", "burstShotInterval"
    };

    // Called only while materializing a configuration, never from a projectile update or per-frame scheduler.
    public static void Copy(object destination, object source, string[] fields, IReadOnlyList<string> keepLocal = null, string prefix = "")
    {
        var data = JObject.Parse(JsonUtility.ToJson(source));
        var patch = new JObject();
        foreach (string field in fields)
        {
            JToken token = data[field]?.DeepClone();
            if (token == null || Contains(keepLocal, prefix + field)) continue;
            if (keepLocal != null)
                for (int i = 0; i < keepLocal.Count; i++)
                {
                    string start = prefix + field + ".";
                    if (keepLocal[i].StartsWith(start, StringComparison.Ordinal))
                        Remove(token, keepLocal[i].Substring(start.Length));
                }
            patch[field] = token;
        }
        JsonUtility.FromJsonOverwrite(patch.ToString(Formatting.None), destination);
    }

    public static bool Contains(IReadOnlyList<string> paths, string path)
    {
        if (paths == null) return false;
        for (int i = 0; i < paths.Count; i++) if (paths[i] == path) return true;
        return false;
    }

    private static void Remove(JToken token, string path)
    {
        int dot = path.IndexOf('.');
        string name = dot < 0 ? path : path.Substring(0, dot);
        if (token is not JObject obj) return;
        if (dot < 0) obj.Remove(name);
        else if (obj[name] != null) Remove(obj[name], path.Substring(dot + 1));
    }
}
