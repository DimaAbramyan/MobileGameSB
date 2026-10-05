using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(EnemyShootingSettings))]
public sealed class EnemyShootingSettingsDrawer : PropertyDrawer
{
    private static IEnumerable<string> Fields(SerializedProperty property)
    {
        yield return "shootingType";
        yield return "projectileType";
        yield return "overrideProjectileBaseSpeed";
        if (property.FindPropertyRelative("overrideProjectileBaseSpeed").boolValue)
            yield return "projectileBaseSpeed";
        yield return "overrideProjectileLifetime";
        if (property.FindPropertyRelative("overrideProjectileLifetime").boolValue)
            yield return "projectileLifetime";
        bool radial = property.FindPropertyRelative("shootingType").enumValueIndex == (int)EnemyShootingType.Rotation;
        if (radial)
            yield return "directionCount";
        yield return "shootingMode";
        switch ((EnemyProjectileFireMode)property.FindPropertyRelative("shootingMode").intValue)
        {
            case EnemyProjectileFireMode.Random:
                yield return "randomMinAngle";
                yield return "randomMaxAngle";
                break;
        }
        yield return "shotGunMode";
        if (property.FindPropertyRelative("shotGunMode").intValue
            == (int)EnemyProjectileShotGunMode.ShotGun)
        {
            yield return "shotGunProjectileCount";
            yield return "shotGunDirection";
            if (!property.FindPropertyRelative("overrideProjectileBaseSpeed").boolValue)
                yield return "shotGunInitialSpeed";
        }
        yield return "fanMode";
        if (property.FindPropertyRelative("fanMode").enumValueIndex == (int)EnemyProjectileFanMode.Fan)
        {
            yield return "fanProjectileCount";
            yield return "fanMinAngle";
            yield return "fanMaxAngle";
        }
        yield return "initialSpeedMode";
        switch ((EnemyProjectileInitialSpeedMode)property.FindPropertyRelative("initialSpeedMode").enumValueIndex)
        {
            case EnemyProjectileInitialSpeedMode.Random:
                yield return "randomInitialSpeedMinimum";
                yield return "randomInitialSpeedMaximum";
                break;
            case EnemyProjectileInitialSpeedMode.Consistent:
                yield return "consistentInitialSpeedChangePerVolley";
                break;
        }
        yield return "projectileMovementPattern";
        switch ((EnemyProjectileMovementPattern)property.FindPropertyRelative("projectileMovementPattern").enumValueIndex)
        {
            case EnemyProjectileMovementPattern.AngularTurn:
                yield return "projectileAngularSpeed";
                break;
            case EnemyProjectileMovementPattern.Lateral:
                yield return "projectileLateralSpeed";
                break;
            case EnemyProjectileMovementPattern.Orbit:
                yield return "projectileOrbitCenterMode";
                if (property.FindPropertyRelative("projectileOrbitCenterMode").enumValueIndex
                    == (int)EnemyProjectileOrbitCenterMode.FixedWorldPoint)
                {
                    yield return "projectileOrbitFixedWorldCenter";
                }
                yield return "projectileOrbitRadius";
                yield return "projectileAngularSpeed";
                yield return "projectileOrbitRadialSpeed";
                break;
        }
        yield return "projectileSpeedBehavior";
        if (property.FindPropertyRelative("projectileSpeedBehavior").enumValueIndex == (int)EnemyProjectileSpeedBehavior.Customized)
        {
            yield return "projectileSpeedChangeY";
            yield return "projectileSpeedByDistance";
        }
        yield return "projectileSizeBehavior";
        if (property.FindPropertyRelative("projectileSizeBehavior").enumValueIndex
            == (int)EnemyProjectileSizeBehavior.Customized)
        {
            yield return "projectileSizeChangeY";
            yield return "projectileSizeChangeSpeed";
            yield return "projectileSizeChangeCurve";
            yield return "projectileFinalSize";
        }
        if (radial)
        {
            yield return "rotate";
            if (property.FindPropertyRelative("rotate").boolValue)
                yield return "rotation";
        }
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        float height = EditorGUIUtility.singleLineHeight + 2f;
        foreach (string field in Fields(property))
            height += EditorGUI.GetPropertyHeight(property.FindPropertyRelative(field), true) + 2f;
        return height;
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);
        position.height = EditorGUIUtility.singleLineHeight;
        EditorGUI.LabelField(position, label, EditorStyles.boldLabel);
        EditorGUI.indentLevel++;
        foreach (string field in Fields(property))
        {
            position.y += position.height + 2f;
            var child = property.FindPropertyRelative(field);
            position.height = EditorGUI.GetPropertyHeight(child, true);
            GUIContent fieldLabel = field switch
            {
                "directionCount" => new GUIContent("Simultaneous Directions", "Evenly spaced over 360 degrees. Projectiles Per Direction applies to each direction."),
                "shootingMode" => new GUIContent("Projectile Pattern"),
                "shotGunMode" => new GUIContent(
                    "ShotGun Mode",
                    "Adds simultaneous pellets. With Random, each pellet receives its own precomputed spread angle."),
                "fanMode" => new GUIContent("Fan Mode"),
                "overrideProjectileBaseSpeed" => new GUIContent(
                    "Override Projectile Base Speed",
                    "Uses a speed from this attack profile instead of the selected projectile prefab."),
                "projectileBaseSpeed" => new GUIContent("Projectile Base Speed"),
                "overrideProjectileLifetime" => new GUIContent(
                    "Override Projectile Lifetime",
                    "Uses a lifetime from this attack profile instead of the selected projectile prefab."),
                "projectileLifetime" => new GUIContent("Projectile Lifetime"),
                "fanProjectileCount" => new GUIContent("Fan Projectiles Per Shot"),
                "shotGunProjectileCount" => new GUIContent(
                    "ShotGun Projectiles",
                    "Number of pellets per direction. With Fan, each pellet creates its own fan."),
                "shotGunDirection" => new GUIContent(
                    "Direction (Local)",
                    "Base direction for the pellets, relative to the firing pivot."),
                "shotGunInitialSpeed" => new GUIContent("Initial Speed"),
                "projectileMovementPattern" => new GUIContent(
                    "Movement Pattern",
                    "Default, continuous direction turn, constant lateral velocity, or a true orbit around a spawn-time center."),
                "projectileAngularSpeed" => new GUIContent(
                    "Angular Speed (deg/sec)",
                    "Positive rotates clockwise; negative rotates counterclockwise. Projectile speed modifiers proportionally change this turn speed."),
                "projectileLateralSpeed" => new GUIContent(
                    "Lateral Speed (units/sec)",
                    "Constant velocity to the projectile's local right. Negative values move left without turning its forward direction."),
                "projectileOrbitCenterMode" => new GUIContent(
                    "Orbit Center",
                    "Source Position snapshots the enemy position when the projectile is spawned. Fixed World Point uses the configured world coordinate."),
                "projectileOrbitFixedWorldCenter" => new GUIContent("Fixed World Center"),
                "projectileOrbitRadius" => new GUIContent(
                    "Orbit Radius",
                    "Zero uses the projectile's spawn distance from the selected center. A positive value places it at this radius when spawned."),
                "projectileOrbitRadialSpeed" => new GUIContent(
                    "Radial Speed (units/sec)",
                    "Positive expands the orbit, negative contracts it, and zero preserves the radius."),
                "projectileSpeedBehavior" => new GUIContent("Speed Behavior"),
                "projectileSpeedByDistance" => new GUIContent("Speed Multiplier By Distance"),
                "projectileSizeBehavior" => new GUIContent(
                    "Size Behavior",
                    "Default keeps the Scale Behavior configured on the selected projectile prefab. Customized overrides it for this attack only."),
                "projectileSizeChangeY" => new GUIContent(
                    "Size Change Y",
                    "World Y coordinate at which the size change begins."),
                "projectileSizeChangeSpeed" => new GUIContent(
                    "Size Change Speed",
                    "Normalized size-change progress per second after reaching Size Change Y. 1 completes the change in one second."),
                "projectileSizeChangeCurve" => new GUIContent(
                    "Size Change Curve",
                    "Maps normalized progress from 0 to 1 to interpolation between the initial and final size."),
                "projectileFinalSize" => new GUIContent(
                    "Final Size",
                    "Final multiplier of this projectile's authored visual and collision size."),
                "rotate" => new GUIContent("Rotate Ship / Pivot"),
                _ => new GUIContent(child.displayName)
            };
            EditorGUI.PropertyField(position, child, fieldLabel, true);
        }
        EditorGUI.indentLevel--;
        EditorGUI.EndProperty();
    }

    public static int GetProjectilesPerVolley(SerializedProperty property)
    {
        int directions = property.FindPropertyRelative("shootingType").enumValueIndex == (int)EnemyShootingType.Rotation
            ? Mathf.Max(1, property.FindPropertyRelative("directionCount").intValue) : 1;
        int perDirection = property.FindPropertyRelative("shotGunMode").intValue
            == (int)EnemyProjectileShotGunMode.ShotGun
            ? property.FindPropertyRelative("shotGunProjectileCount").intValue
            : 1;
        if (property.FindPropertyRelative("fanMode").enumValueIndex == (int)EnemyProjectileFanMode.Fan)
            perDirection *= property.FindPropertyRelative("fanProjectileCount").intValue;
        return directions * Mathf.Max(1, perDirection);
    }
}
