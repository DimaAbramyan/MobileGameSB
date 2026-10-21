using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(EnemyShootingSettings))]
public sealed class EnemyShootingSettingsDrawer : PropertyDrawer
{
    private static readonly GUIContent[] StackOptions = { new GUIContent("Off"), new GUIContent("Stack") };
    private static readonly GUIContent[] FanOptions = { new GUIContent("Off"), new GUIContent("Fan") };
    private static readonly GUIContent[] SpreadOptions = { new GUIContent("Fixed"), new GUIContent("Random") };
    private static IEnumerable<string> Fields(SerializedProperty property)
    {
        foreach (string field in BehaviourFields(property)) yield return field;
        foreach (string field in ProjectileFields(property)) yield return field;
    }

    private static IEnumerable<string> BehaviourFields(SerializedProperty property)
    {
        if (!EnemyChildAttackSettingsDrawer.IsChildPattern(property)) yield return "emissionOrigin";
        yield return "emissionLayout";
        if (property.FindPropertyRelative("presetFiring.enabled").boolValue) yield return "presetFiring";
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
        }
        yield return "fanMode";
        if (property.FindPropertyRelative("fanMode").enumValueIndex == (int)EnemyProjectileFanMode.Fan)
        {
            yield return "fanProjectileCount";
            yield return "fanMinAngle";
            yield return "fanMaxAngle";
        }
        yield return "layoutRotationEnabled";
        if (property.FindPropertyRelative("layoutRotationEnabled").boolValue)
            yield return "rotation";
    }

    private static IEnumerable<string> ProjectileFields(SerializedProperty property)
    {
        yield return "numericRandomSeed";
        yield return "projectileType";
        yield return "projectilePrefab";
        yield return "overwriteProjectileCharacteristic";
        if (property.FindPropertyRelative("overwriteProjectileCharacteristic").boolValue)
            yield return "projectileCharacteristic";
        bool fragile = IsFragile(property);
        if (!fragile) yield return "overwriteBaseColour";
        if (fragile || property.FindPropertyRelative("overwriteBaseColour").boolValue)
            yield return "projectileColour";
        if (property.FindPropertyRelative("projectileType").intValue == (int)EnemyProjectileType.Laser)
        {
            yield return "laser";
            yield return "useChildAttackPattern";
            if (property.FindPropertyRelative("useChildAttackPattern").boolValue) yield return "childAttack";
            yield break;
        }
        yield return "initialSpeedMode";
        bool random = property.FindPropertyRelative("initialSpeedMode").enumValueIndex == (int)EnemyProjectileInitialSpeedMode.Random;
        if (!random)
        {
            yield return "overrideProjectileBaseSpeed";
            if (property.FindPropertyRelative("overrideProjectileBaseSpeed").boolValue)
                yield return "projectileBaseSpeed";
            else if (!property.FindPropertyRelative("independentProjectileSpeed").boolValue && property.FindPropertyRelative("shotGunMode").intValue == (int)EnemyProjectileShotGunMode.ShotGun)
                yield return "shotGunInitialSpeed";
        }
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
        if (random) yield return "seededInitialSpeedRandom";
        bool ownSpeedRandom = property.FindPropertyRelative("overrideProjectileBaseSpeed").boolValue
            ? property.FindPropertyRelative("projectileBaseSpeedRandom.enabled").boolValue
            : !property.FindPropertyRelative("independentProjectileSpeed").boolValue && property.FindPropertyRelative("shotGunMode").intValue == (int)EnemyProjectileShotGunMode.ShotGun
                && property.FindPropertyRelative("shotGunInitialSpeedRandom.enabled").boolValue;
        if (random ? property.FindPropertyRelative("seededInitialSpeedRandom").boolValue : ownSpeedRandom)
            yield return "speedRandomizeOn";
        yield return "overrideProjectileLifetime";
        if (property.FindPropertyRelative("overrideProjectileLifetime").boolValue)
            yield return "projectileLifetime";
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
                if (property.FindPropertyRelative("projectileOrbitCenterMode").enumValueIndex
                    == (int)EnemyProjectileOrbitCenterMode.DnaLike)
                {
                    yield return "projectileAngularSpeed";
                    yield return "projectileDnaWeaveSpeed";
                }
                else
                {
                    yield return "projectileOrbitRadius";
                    yield return "projectileAngularSpeed";
                    yield return "projectileOrbitRadialSpeed";
                }
                break;
        }
        yield return "projectileSpeedBehavior";
        if (property.FindPropertyRelative("projectileSpeedBehavior").enumValueIndex == (int)EnemyProjectileSpeedBehavior.Customized)
        {
            if (property.FindPropertyRelative("projectileMovementPattern").enumValueIndex == (int)EnemyProjectileMovementPattern.Orbit
                || property.FindPropertyRelative("layoutRotationEnabled").boolValue)
                yield return "projectileSpeedCurveTarget";
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
        yield return "useChildAttackPattern";
        if (property.FindPropertyRelative("useChildAttackPattern").boolValue) yield return "childAttack";
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        return SettingsHeight(property);
    }

    private static float Line => EditorGUIUtility.singleLineHeight + 2f;
    private static bool IsFragile(SerializedProperty property) =>
        property.FindPropertyRelative("overwriteProjectileCharacteristic").boolValue
        && property.FindPropertyRelative("projectileCharacteristic").intValue == (int)EnemyProjectileCharacteristic.Fragile;
    private static string GroupKey(SerializedProperty property, bool projectile) =>
        "EnemyShootingInspector." + property.serializedObject.targetObject.GetInstanceID() + "." + property.propertyPath
        + (projectile ? ".Projectile" : ".Behaviour");
    public static bool IsGroupExpanded(SerializedProperty property, bool projectile) => SessionState.GetBool(GroupKey(property, projectile), true);
    public static void SetGroupExpanded(SerializedProperty property, bool projectile, bool expanded) => SessionState.SetBool(GroupKey(property, projectile), expanded);

    public static float SettingsHeight(SerializedProperty shooting, SerializedProperty timing = null, bool hasDelay = false)
        => CategoryHeight(shooting, false, timing, hasDelay) + CategoryHeight(shooting, true, timing, hasDelay);

    public static float CategoryHeight(SerializedProperty shooting, bool projectile, SerializedProperty timing = null, bool hasDelay = false)
    {
        float height = Line;
        if (!IsGroupExpanded(shooting, projectile)) return height;
        bool linked = EnemyPresetInspector.Linked(shooting, projectile);
        var display = linked ? EnemyPresetInspector.DisplayShooting(shooting, timing) : shooting;
        height += EnemyPresetInspector.Height(shooting, timing, projectile);
        foreach (string field in projectile ? ProjectileFields(display) : BehaviourFields(display))
            height += EditorGUI.GetPropertyHeight(display.FindPropertyRelative(field), true) + 2f;
        if (!projectile) height += timing != null ? EnemyAttackPatternFields.TimingHeight(linked ? EnemyPresetInspector.DisplayTiming(shooting, timing) : timing, hasDelay) : Line;
        else { height += Line; if (GetDnaWarning(display) != null) height += 62f; }
        return height;
    }

    public static string SpeedSource(SerializedProperty shooting)
    {
        if (shooting.FindPropertyRelative("projectileType").intValue == (int)EnemyProjectileType.Laser)
            return "Laser: released direction is locked. Timing uses the existing attack schedule.";
        if (shooting.FindPropertyRelative("initialSpeedMode").enumValueIndex == (int)EnemyProjectileInitialSpeedMode.Random)
            return "Initial speed: Random range (base speed is unused)";
        if (shooting.FindPropertyRelative("overrideProjectileBaseSpeed").boolValue)
            return "Base speed: This pattern";
        if (TryGetSharedWaveSpeed(shooting, out float sharedSpeed))
            return $"Base speed: Shared wave override ({sharedSpeed:0.###})";
        if (!shooting.FindPropertyRelative("independentProjectileSpeed").boolValue && shooting.FindPropertyRelative("shotGunMode").intValue == (int)EnemyProjectileShotGunMode.ShotGun)
            return "Base speed: Stack initial speed";
        return "Base speed: Selected projectile prefab";
    }

    private static bool TryGetSharedWaveSpeed(SerializedProperty shooting, out float speed)
    {
        speed = 0f;
        if (!(shooting.serializedObject.targetObject is DirectedWaveEnemyOverride local)) return false;
        var wave = local.GetComponentInParent<DirectedEnemySubWave>();
        if (wave == null) return false;
        var behaviour = wave.GetComponent<DirectedWaveAttackBehaviour>();
        if (behaviour == null) return false;
        for (int i = 0; i < wave.GetConfiguredEnemySlotCount(); i++)
        {
            if (wave.GetConfiguredEnemyOverrideForSlot(i) != local) continue;
            var settings = new DirectedWaveAttackSettings();
            if (!behaviour.TryCopyResolvedAttackSettingsTo(i, false, settings)
                || !settings.HasRuntimeProjectileBaseSpeedOverride) return false;
            speed = settings.RuntimeProjectileBaseSpeedOverride;
            return true;
        }
        return false;
    }

    private static string GetDnaWarning(SerializedProperty property)
    {
        if (property.FindPropertyRelative("projectileType").intValue == (int)EnemyProjectileType.Laser) return null;
        if (property.FindPropertyRelative("projectileMovementPattern").enumValueIndex != (int)EnemyProjectileMovementPattern.Orbit
            || property.FindPropertyRelative("projectileOrbitCenterMode").enumValueIndex != (int)EnemyProjectileOrbitCenterMode.DnaLike)
            return null;
        if (property.FindPropertyRelative("fanMode").enumValueIndex != (int)EnemyProjectileFanMode.Fan
            || property.FindPropertyRelative("fanProjectileCount").intValue < 2)
            return "DNA-Like needs Fan with at least two projectiles to show intertwining paths.";
        if (Mathf.Abs(property.FindPropertyRelative("fanMaxAngle").floatValue
            - property.FindPropertyRelative("fanMinAngle").floatValue) >= EnemyProjectileDnaMotion.MaximumHalfAngleDegrees * 2f)
            return "DNA-Like needs a fan narrower than 359.8 degrees. A full circle has no unique median; extreme rays are limited to 179.9 degrees from it.";
        if (Mathf.Abs(property.FindPropertyRelative("projectileAngularSpeed").floatValue) < 0.0001f)
            return "Angular Velocity is zero: DNA-Like projectiles travel straight without weaving.";
        return null;
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);
        DrawSettings(ref position, property);
        EditorGUI.EndProperty();
    }

    public static void DrawSettings(ref Rect position, SerializedProperty property, SerializedProperty timing = null, bool hasDelay = false)
    {
        DrawGroup(ref position, property, false, timing, hasDelay);
        DrawGroup(ref position, property, true, timing, hasDelay);
    }

    public static void DrawGroup(ref Rect position, SerializedProperty property, bool projectile, SerializedProperty timing, bool hasDelay)
    {
        position.height = EditorGUIUtility.singleLineHeight;
        bool expanded = EditorGUI.Foldout(position, IsGroupExpanded(property, projectile),
            projectile ? "Projectile Settings" : "Shooting Behaviour", true, EditorStyles.foldoutHeader);
        SetGroupExpanded(property, projectile, expanded);
        position.y += Line;
        if (!expanded) return;
        EditorGUI.indentLevel++;
        EnemyPresetInspector.Draw(ref position, property, timing, projectile);
        bool linked = EnemyPresetInspector.Linked(property, projectile);
        var display = linked ? EnemyPresetInspector.DisplayShooting(property, timing) : property;
        using (new EditorGUI.DisabledScope(linked))
        {
        foreach (string field in projectile ? ProjectileFields(display) : BehaviourFields(display))
        {
            var child = display.FindPropertyRelative(field);
            position.height = EditorGUI.GetPropertyHeight(child, true);
            GUIContent fieldLabel = field switch
            {
                "useChildAttackPattern" => new GUIContent("Child Attack Pattern", "This projectile launches a nested attack. Emission Origin is always the parent projectile plus Spawn Offset."),
                "overwriteProjectileCharacteristic" => new GUIContent("Overwrite projectile characteristic", "Override the selected prefab's Normal/Fragile characteristic for this attack profile."),
                "projectileCharacteristic" => new GUIContent("Projectile Characteristic", "Normal requires an interceptor configured to destroy Normal projectiles. Fragile can also be destroyed by Fragile-only interceptors; colour is locked pink."),
                "overwriteBaseColour" => new GUIContent("Overwrite base colour", "Replace the projectile prefab's colour for this attack profile. Disabled keeps the prefab's base colour."),
                "projectileColour" => new GUIContent("Projectile Colour"),
                "directionCount" => new GUIContent("Simultaneous Directions", "Evenly spaced over 360 degrees. Projectiles Per Direction applies to each direction."),
                "shootingMode" => new GUIContent("Spread Mode", "Fixed uses the authored directions. Random adds an angular spread; Fan and Stack remain separate modifiers."),
                "shotGunMode" => new GUIContent(
                    "Stack",
                    "Adds simultaneous pellets. With Random, each pellet receives its own precomputed spread angle."),
                "fanMode" => new GUIContent("Fan", EnemyAttackInspectorHints.Field(child)),
                "overrideProjectileBaseSpeed" => new GUIContent(
                    "Override Projectile Base Speed",
                    "Uses this pattern's base speed instead of the projectile prefab. In a local slot override, it also takes priority over the shared wave base speed."),
                "projectileBaseSpeed" => new GUIContent("Projectile Base Speed"),
                "overrideProjectileLifetime" => new GUIContent(
                    "Override Projectile Lifetime",
                    "Uses a lifetime from this attack profile instead of the selected projectile prefab."),
                "projectileLifetime" => new GUIContent("Projectile Lifetime"),
                "fanProjectileCount" => new GUIContent("Projectiles Per Fan", EnemyAttackInspectorHints.Field(child)),
                "shotGunProjectileCount" => new GUIContent(
                    "Stack Count",
                    "Number of pellets per direction. With Fan, each pellet creates its own fan."),
                "shotGunDirection" => new GUIContent(
                    "Stack Direction (Local)",
                    "Base direction relative to the firing pivot. Aimed can replace this direction."),
                "shotGunInitialSpeed" => new GUIContent("Stack Initial Speed", "Existing base speed for Stack when the pattern does not override Base Speed. Random speed mode uses its own range."),
                "projectileMovementPattern" => new GUIContent(
                    "Movement Pattern",
                    "Default, continuous direction turn, constant lateral velocity, or a true orbit around a spawn-time center."),
                "projectileAngularSpeed" => new GUIContent(
                    "Angular Speed (deg/sec)",
                    "Positive rotates clockwise; negative rotates counterclockwise. DNA-Like uses the magnitude for a common rhythm and automatically selects the sign and speed for each fan ray."),
                "projectileDnaWeaveSpeed" => new GUIContent("DNA Weave Speed", "Multiplier of Angular Velocity. 2 doubles the weaving frequency. Random initial speed is shared by each fan to keep its crossings synchronized."),
                "projectileLateralSpeed" => new GUIContent(
                    "Lateral Speed (units/sec)",
                    "Constant velocity to the projectile's local right. Negative values move left without turning its forward direction."),
                "projectileOrbitCenterMode" => new GUIContent(
                    "Orbit Center",
                    "Source Position snapshots the enemy. Fixed World Point uses a world coordinate. DNA-Like alternates synchronized arcs along the median of each fan."),
                "projectileOrbitFixedWorldCenter" => new GUIContent("Fixed World Center"),
                "projectileOrbitRadius" => new GUIContent(
                    "Orbit Radius",
                    "Zero uses the projectile's spawn distance from the selected center. A positive value places it at this radius when spawned."),
                "projectileOrbitRadialSpeed" => new GUIContent(
                    "Radial Speed (units/sec)",
                    "Positive expands the orbit, negative contracts it, and zero preserves the radius."),
                "projectileSpeedBehavior" => new GUIContent("Speed Behavior", EnemyAttackInspectorHints.Field(child)),
                "projectileSpeedByDistance" => new GUIContent("Speed Multiplier By Distance"),
                "projectileSpeedCurveTarget" => new GUIContent("Speed Curve Target",
                    "Orbit: Base Speed changes travel around the orbit; Radial changes radius expansion/contraction; Both changes both. Without Orbit only Base Speed applies. DNA-Like has no radial speed."),
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
                "layoutRotationEnabled" => new GUIContent("Layout Rotation", "Rotate emission directions and positions over time. Enemy Facing is configured independently."),
                "rotation" => new GUIContent("Layout Rotation Settings"),
                _ => new GUIContent(child.displayName, EnemyAttackInspectorHints.Field(child))
            };
            if (field == "projectileColour" && IsFragile(display))
            {
                using (new EditorGUI.DisabledScope(true))
                    EditorGUI.ColorField(position, new GUIContent("Fragile Colour (Locked)"), EnemyProjectileCharacteristics.FragileColour);
            }
            else if (field == "shotGunMode" || field == "fanMode" || field == "shootingMode")
                DrawModifier(position, child, fieldLabel, field == "shotGunMode" ? StackOptions : field == "fanMode" ? FanOptions : SpreadOptions);
            else
                EditorGUI.PropertyField(position, child, fieldLabel, true);
            position.y += position.height + 2f;
        }
        if (!projectile)
        {
            if (timing != null) EnemyAttackPatternFields.DrawTiming(ref position, linked ? EnemyPresetInspector.DisplayTiming(property, timing) : timing, hasDelay);
            else
            {
                position.height = EditorGUIUtility.singleLineHeight;
                EditorGUI.LabelField(position, $"Volley: {GetProjectilesPerVolley(display)} projectiles", EditorStyles.miniLabel);
                position.y += Line;
            }
        }
        else
        {
            position.height = EditorGUIUtility.singleLineHeight;
            EditorGUI.LabelField(position, SpeedSource(display), EditorStyles.miniLabel);
            position.y += Line;
            string warning = GetDnaWarning(display);
            if (warning != null)
            {
                position.height = 60f;
                EditorGUI.HelpBox(position, warning, MessageType.Warning);
                position.y += 62f;
            }
        }
        }
        EditorGUI.indentLevel--;
    }

    private static void DrawModifier(Rect position, SerializedProperty property, GUIContent label, GUIContent[] choices)
    {
        label = EditorGUI.BeginProperty(position, label, property);
        bool previousMixed = EditorGUI.showMixedValue;
        EditorGUI.showMixedValue = property.hasMultipleDifferentValues;
        EditorGUI.BeginChangeCheck();
        int choice = EditorGUI.Popup(position, label, property.enumValueIndex, choices);
        if (EditorGUI.EndChangeCheck()) property.enumValueIndex = choice;
        EditorGUI.showMixedValue = previousMixed;
        EditorGUI.EndProperty();
    }

    public static int GetProjectilesPerVolley(SerializedProperty property)
    {
        var layout = property.FindPropertyRelative("emissionLayout");
        var type = (EnemyEmissionLayoutType)layout.FindPropertyRelative("type").enumValueIndex;
        int directions = type == EnemyEmissionLayoutType.Radial || type == EnemyEmissionLayoutType.Sector
            ? Mathf.Max(1, layout.FindPropertyRelative("directionsCount").intValue)
            : type == EnemyEmissionLayoutType.Circle ? Mathf.Max(1, layout.FindPropertyRelative("emitterCount").intValue) : 1;
        int perDirection = property.FindPropertyRelative("shotGunMode").intValue
            == (int)EnemyProjectileShotGunMode.ShotGun
            ? property.FindPropertyRelative("shotGunProjectileCount").intValue
            : 1;
        if (property.FindPropertyRelative("fanMode").enumValueIndex == (int)EnemyProjectileFanMode.Fan)
            perDirection *= property.FindPropertyRelative("fanProjectileCount").intValue;
        return directions * Mathf.Max(1, perDirection);
    }
}
