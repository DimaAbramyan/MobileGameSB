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

    // Keep the leaf-field lists usable by existing Inspector diagnostics.
    private static IEnumerable<string> BehaviourFields(SerializedProperty property) => LeafFields(BehaviourSections(property));
    private static IEnumerable<string> ProjectileFields(SerializedProperty property) => LeafFields(ProjectileSections(property));
    private static IEnumerable<string> LeafFields(IEnumerable<string> sections)
    {
        foreach (string field in sections)
            if (field[0] != '#') yield return field;
    }

    private static IEnumerable<string> BehaviourSections(SerializedProperty property) => VisibleBehaviourSections(property, null);
    private static IEnumerable<string> ProjectileSections(SerializedProperty property) => VisibleProjectileSections(property, null);
    private static bool SectionVisible(SerializedProperty owner, string title) => owner == null || WaveInspectorSections.Expanded(owner, title);

    private static IEnumerable<string> VisibleBehaviourSections(SerializedProperty property, SerializedProperty owner)
    {
        if (!EnemyChildAttackSettingsDrawer.IsChildPattern(property)) yield return "emissionOrigin";
        yield return "emissionLayout";
        if (property.FindPropertyRelative("presetFiring.enabled").boolValue) yield return "presetFiring";
        yield return "#Fan / Stack / Spread";
        if (SectionVisible(owner, "Fan / Stack / Spread"))
        {
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
        }
        yield return "#Projectile Spawn Offset";
        if (SectionVisible(owner, "Projectile Spawn Offset"))
        {
            yield return "enableSpawnOffset";
            if (property.FindPropertyRelative("enableSpawnOffset").boolValue)
            {
                yield return "baseOffsetX";
                yield return "baseOffsetY";
                yield return "stepOffsetX";
                yield return "stepOffsetY";
            }
        }
        yield return "#Layout Rotation";
        if (SectionVisible(owner, "Layout Rotation"))
        {
            yield return "layoutRotationEnabled";
            if (property.FindPropertyRelative("layoutRotationEnabled").boolValue)
                yield return "rotation";
        }
    }

    private static IEnumerable<string> VisibleProjectileSections(SerializedProperty property, SerializedProperty owner)
    {
        yield return "projectileType";
        yield return "projectilePrefab";
        yield return "#Appearance & Base Size";
        if (SectionVisible(owner, "Appearance & Base Size"))
        {
            yield return "overrideBaseSize";
            if (property.FindPropertyRelative("overrideBaseSize").boolValue) yield return "baseSize";
            yield return "overwriteProjectileCharacteristic";
            if (property.FindPropertyRelative("overwriteProjectileCharacteristic").boolValue)
                yield return "projectileCharacteristic";
            bool fragile = IsFragile(property);
            if (!fragile) yield return "overwriteBaseColour";
            if (fragile || property.FindPropertyRelative("overwriteBaseColour").boolValue)
                yield return "projectileColour";
        }
        yield return "#Damage";
        if (SectionVisible(owner, "Damage"))
        {
            yield return "overrideProjectileDamage";
            if (property.FindPropertyRelative("overrideProjectileDamage").boolValue) yield return "projectileDamage";
        }
        yield return "#Target Position";
        if (SectionVisible(owner, "Target Position"))
        {
            bool targetPointMovement=property.FindPropertyRelative("projectileMovementPattern").intValue==(int)EnemyProjectileMovementPattern.TargetPoint
                && property.FindPropertyRelative("projectileType").intValue!=(int)EnemyProjectileType.Laser;
            if(!targetPointMovement)yield return "targetScreenBoundary";
            if(!targetPointMovement && property.FindPropertyRelative("targetScreenBoundary").boolValue)
            {yield return "targetBoundaries";yield return "targetBoundaryOffset";}
            if(targetPointMovement || property.FindPropertyRelative("targetScreenBoundary").boolValue)yield return "hideParentOnTargetReached";
        }
        yield return "#Collision Settings";
        if (SectionVisible(owner, "Collision Settings")) yield return "collisionSettings";
        if (property.FindPropertyRelative("projectileType").intValue == (int)EnemyProjectileType.Laser)
        {
            yield return "#Laser";
            if (SectionVisible(owner, "Laser")) yield return "laser";
            yield return "#Lifetime & Boundaries";
            if (SectionVisible(owner, "Lifetime & Boundaries"))
            {
                yield return "screenBoundary";
            }
            yield return "#Child Attack";
            if (SectionVisible(owner, "Child Attack"))
            {
                yield return "useChildAttackPattern";
                if (property.FindPropertyRelative("useChildAttackPattern").boolValue) yield return "childAttack";
            }
            yield return "#Advanced / Randomization";
            if (SectionVisible(owner, "Advanced / Randomization")) yield return "numericRandomSeed";
            yield break;
        }
        if (property.FindPropertyRelative("projectileType").intValue >= (int)EnemyProjectileType.CompositeCircle)
        {
            yield return "#Composite Projectile";
            if (SectionVisible(owner, "Composite Projectile")) yield return "composite";
        }
        yield return "#Base Speed";
        if (SectionVisible(owner, "Base Speed"))
        {
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
        }
        yield return "#Projectile Movement";
        if (SectionVisible(owner, "Projectile Movement"))
        {
            yield return "projectileMovementPattern";
            switch ((EnemyProjectileMovementPattern)property.FindPropertyRelative("projectileMovementPattern").enumValueIndex)
            {
                case EnemyProjectileMovementPattern.TargetPoint:
                    yield return "targetPointSpeedByRemainingDistance";
                    break;
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
            bool targetPoint = property.FindPropertyRelative("projectileMovementPattern").intValue == (int)EnemyProjectileMovementPattern.TargetPoint;
            if (!targetPoint) yield return "projectileSpeedBehavior";
            if (!targetPoint && property.FindPropertyRelative("projectileSpeedBehavior").enumValueIndex == (int)EnemyProjectileSpeedBehavior.Customized)
            {
                if (property.FindPropertyRelative("projectileMovementPattern").enumValueIndex == (int)EnemyProjectileMovementPattern.Orbit
                    || property.FindPropertyRelative("layoutRotationEnabled").boolValue)
                    yield return "projectileSpeedCurveTarget";
                yield return "projectileSpeedChangeY";
                yield return "projectileSpeedByDistance";
            }
        }
        yield return "#Size Over Lifetime";
        if (SectionVisible(owner, "Size Over Lifetime"))
        {
            yield return "projectileSizeBehavior";
            if (property.FindPropertyRelative("projectileSizeBehavior").enumValueIndex
                == (int)EnemyProjectileSizeBehavior.Customized)
            {
                yield return "projectileSizeChangeY";
                yield return "projectileSizeChangeSpeed";
                yield return "projectileSizeChangeCurve";
                yield return "projectileFinalSize";
            }
        }
        yield return "#Lifetime & Boundaries";
        if (SectionVisible(owner, "Lifetime & Boundaries"))
        {
            yield return "overrideProjectileLifetime";
            if (property.FindPropertyRelative("overrideProjectileLifetime").boolValue)
                yield return "projectileLifetime";
            yield return "screenBoundary";
        }
        yield return "#Child Attack";
        if (SectionVisible(owner, "Child Attack"))
        {
            yield return "useChildAttackPattern";
            if (property.FindPropertyRelative("useChildAttackPattern").boolValue) yield return "childAttack";
        }
        yield return "#Advanced / Randomization";
        if (SectionVisible(owner, "Advanced / Randomization")) yield return "numericRandomSeed";
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
    public static void SetGroupExpanded(SerializedProperty property, bool projectile, bool expanded)
    {
        if (IsGroupExpanded(property, projectile) == expanded) return;
        SessionState.SetBool(GroupKey(property, projectile), expanded);
    }

    public static float SettingsHeight(SerializedProperty shooting, SerializedProperty timing = null, bool hasDelay = false)
        => CategoryHeight(shooting, false, timing, hasDelay) + CategoryHeight(shooting, true, timing, hasDelay);

    private static readonly Unity.Profiling.ProfilerMarker ProfileCategoryHeight = new("WaveEditor.Inspector.CategoryHeight");
    public static float CategoryHeight(SerializedProperty shooting, bool projectile, SerializedProperty timing = null, bool hasDelay = false)
    {
        using var profileScope = ProfileCategoryHeight.Auto();
        float height = Line;
        if (!IsGroupExpanded(shooting, projectile)) return height;
        bool linked = EnemyPresetInspector.Linked(shooting, projectile);
        var display = linked ? EnemyPresetInspector.DisplayShooting(shooting, timing) : shooting;
        height += EnemyPresetInspector.Height(shooting, timing, projectile);
        height += WaveInspectorSections.Height(shooting, projectile ? VisibleProjectileSections(display, shooting) : VisibleBehaviourSections(display, shooting),
            field => FieldHeight(display, field));
        if (!projectile)
        {
            height += Line;
            if (timing != null && WaveInspectorSections.Expanded(shooting, "Attack Timing"))
                height += EnemyAttackPatternFields.TimingHeight(linked ? EnemyPresetInspector.DisplayTiming(shooting, timing) : timing, hasDelay) - Line;
        }
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
            if (!SceneAttackSettingsCache.TryGet(behaviour, i, false, out var settings, out _)
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

    private static readonly Unity.Profiling.ProfilerMarker ProfileDrawGroup = new("WaveEditor.Inspector.DrawGroup");
    public static void DrawGroup(ref Rect position, SerializedProperty property, bool projectile, SerializedProperty timing, bool hasDelay)
    {
        using var profileScope = ProfileDrawGroup.Auto();
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
        if (!projectile && timing != null && WaveInspectorSections.Header(ref position, property, "Attack Timing"))
        {
            using (new EditorGUI.DisabledScope(linked))
                EnemyAttackPatternFields.DrawTiming(ref position, linked ? EnemyPresetInspector.DisplayTiming(property, timing) : timing, hasDelay, false);
        }
        WaveInspectorSections.Draw(ref position, property, projectile ? VisibleProjectileSections(display, property) : VisibleBehaviourSections(display, property),
            field => FieldHeight(display, field),
            (fieldRect, field) =>
            {
                using (new EditorGUI.DisabledScope(linked)) DrawField(fieldRect, display, field);
            });
        if (!projectile)
        {
            if (timing == null)
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
        EditorGUI.indentLevel--;
    }

    private static void DrawField(Rect position, SerializedProperty display, string field)
    {
        var child = display.FindPropertyRelative(field);

        GUIContent fieldLabel = field switch
        {
            "useChildAttackPattern" => new GUIContent("Enable Child Attack", "This projectile launches a nested attack. Emission Origin is always the parent projectile plus Spawn Offset."),
            "overwriteProjectileCharacteristic" => new GUIContent("Overwrite projectile characteristic", "Override the selected prefab's Normal/Fragile/Indestructible characteristic for this attack profile."),
            "projectileCharacteristic" => new GUIContent("Projectile Characteristic", "Normal requires an interceptor configured to destroy Normal projectiles. Fragile can also be destroyed by Fragile-only interceptors; colour is locked pink. Indestructible survives weapons, abilities and player contacts; system cleanup still applies."),
            "overwriteBaseColour" => new GUIContent("Overwrite base colour", "Replace the projectile prefab's colour for this attack profile. Disabled keeps the prefab's base colour."),
            "projectileColour" => new GUIContent("Projectile Colour"),
            "targetPointSpeedByRemainingDistance" => new GUIContent("Speed / Remaining Distance", "X = normalized remaining distance: 1 at launch, 0 at the captured player position. Y = base-speed multiplier. The 1% approach floor guarantees arrival; external slows/boosts still multiply speed. TargetPoint overrides prefab Homing/Burst and screen bounce."),
            "overrideProjectileDamage" => new GUIContent("Override Projectile Damage", "Overrides contact damage for this pattern, including Laser and Composite components."),
            "projectileDamage" => new GUIContent("Projectile Damage", "Zero causes no damage. Shield, HP and invulnerability use the normal damage pipeline."),
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
            "projectilePrefab" => new GUIContent(display.FindPropertyRelative("projectileType").intValue >= (int)EnemyProjectileType.CompositeCircle ? "Component Prefab" : "Projectile Prefab"),
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
            "enableSpawnOffset" => new GUIContent("Enable Spawn Offset", "Local X points right of each final flight direction; local Y follows it. Circle has one Fan/Stack group per emitter; Radial/Sector use direction-major order across the complete volley."),
            "layoutRotationEnabled" => new GUIContent("Enable Layout Rotation", "Rotate emission directions and positions over time. Enemy Facing is configured independently."),
            "rotation" => new GUIContent("Layout Rotation Settings"),
            _ => new GUIContent(child.displayName, EnemyAttackInspectorHints.Field(child))
        };
        if (field == "projectileType")
            DrawProjectileType(position, child, fieldLabel);
        else if (HasSectionHeader(field))
            DrawWithoutDecorator(position, child, fieldLabel);
        else if (field == "projectileColour" && IsFragile(display))
        {
            using (new EditorGUI.DisabledScope(true))
                EditorGUI.ColorField(position, new GUIContent("Fragile Colour (Locked)"), EnemyProjectileCharacteristics.FragileColour);
        }
        else if (field == "shotGunMode" || field == "fanMode" || field == "shootingMode")
            DrawModifier(position, child, fieldLabel, field == "shotGunMode" ? StackOptions : field == "fanMode" ? FanOptions : SpreadOptions);
        else
            EditorGUI.PropertyField(position, child, fieldLabel, true);
    }

    // These fields have Runtime Header attributes for the default Inspector. Our section
    // headings replace those decorations, while retaining normal serialized controls.
    private static bool HasSectionHeader(string field) => field == "enableSpawnOffset" || field == "initialSpeedMode"
        || field == "projectileMovementPattern" || field == "projectileSizeBehavior";

    private static float FieldHeight(SerializedProperty shooting, string field) => HasSectionHeader(field)
        ? EditorGUIUtility.singleLineHeight : EditorGUI.GetPropertyHeight(shooting.FindPropertyRelative(field), true);

    private static void DrawWithoutDecorator(Rect rect, SerializedProperty field, GUIContent label)
    {
        label = EditorGUI.BeginProperty(rect, label, field);
        bool mixed = EditorGUI.showMixedValue;
        EditorGUI.showMixedValue = field.hasMultipleDifferentValues;
        EditorGUI.BeginChangeCheck();
        if (field.propertyType == SerializedPropertyType.Boolean)
        {
            bool value = EditorGUI.Toggle(rect, label, field.boolValue);
            if (EditorGUI.EndChangeCheck()) field.boolValue = value;
        }
        else
        {
            string[] names = field.enumDisplayNames;
            var options = new GUIContent[names.Length];
            for (int i = 0; i < names.Length; i++) options[i] = new GUIContent(names[i]);
            int value = EditorGUI.Popup(rect, label, field.enumValueIndex, options);
            if (EditorGUI.EndChangeCheck()) field.enumValueIndex = value;
        }
        EditorGUI.showMixedValue = mixed;
        EditorGUI.EndProperty();
    }

    private static readonly GUIContent[] ProjectileTypeLabels = {
        new("Default"), new("Homing"), new("Burst At Point"), new("Laser"), new("Composite")
    };
    private static readonly int[] ProjectileTypeValues = { 0, 1, 2, 3, 4 };
    private static void DrawProjectileType(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);
        bool mixed = EditorGUI.showMixedValue;
        EditorGUI.showMixedValue = property.hasMultipleDifferentValues;
        int value = property.intValue >= 4 && property.intValue <= 6 ? 4 : property.intValue;
        EditorGUI.BeginChangeCheck();
        int selected = EditorGUI.IntPopup(position, label, value, ProjectileTypeLabels, ProjectileTypeValues);
        if (EditorGUI.EndChangeCheck()) property.intValue = selected;
        EditorGUI.showMixedValue = mixed;
        EditorGUI.EndProperty();
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
