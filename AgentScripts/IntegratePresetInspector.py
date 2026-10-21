from pathlib import Path
p=Path('Assets/Editor/EnemyShootingSettingsDrawer.cs');s=p.read_text(encoding='utf-8-sig')
s=s.replace('yield return "projectileType";', 'yield return "projectileType";\n        yield return "projectilePrefab";')
s=s.replace('yield return "emissionLayout";', 'yield return "emissionLayout";\n        if (property.FindPropertyRelative("presetFiring.enabled").boolValue) yield return "presetFiring";')
a=s.index('    public static float SettingsHeight('); b=s.index('    public static string SpeedSource',a)
s=s[:a]+'''    public static float SettingsHeight(SerializedProperty shooting, SerializedProperty timing = null, bool hasDelay = false)
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

'''+s[b:]
s=s.replace('private static void DrawGroup(', 'public static void DrawGroup(')
s=s.replace('        EditorGUI.indentLevel++;\n        foreach (string field in projectile ? ProjectileFields(property) : BehaviourFields(property))', '''        EditorGUI.indentLevel++;
        EnemyPresetInspector.Draw(ref position, property, timing, projectile);
        bool linked = EnemyPresetInspector.Linked(property, projectile);
        var display = linked ? EnemyPresetInspector.DisplayShooting(property, timing) : property;
        using (new EditorGUI.DisabledScope(linked))
        {
        foreach (string field in projectile ? ProjectileFields(display) : BehaviourFields(display))''')
a=s.index('    public static void DrawGroup('); b=s.index('    private static void DrawModifier(',a)
part=s[a:b]
part=part.replace('var child = property.FindPropertyRelative(field);','var child = display.FindPropertyRelative(field);').replace('IsFragile(property)', 'IsFragile(display)')
part=part.replace('EnemyAttackPatternFields.DrawTiming(ref position, timing, hasDelay)', 'EnemyAttackPatternFields.DrawTiming(ref position, linked ? EnemyPresetInspector.DisplayTiming(property, timing) : timing, hasDelay)')
part=part.replace('GetProjectilesPerVolley(property)', 'GetProjectilesPerVolley(display)').replace('SpeedSource(property)','SpeedSource(display)').replace('GetDnaWarning(property)','GetDnaWarning(display)')
part=part.replace('        EditorGUI.indentLevel--;\n    }', '        }\n        EditorGUI.indentLevel--;\n    }')
s=s[:a]+part+s[b:];p.write_text(s,encoding='utf-8')
for name in ['EnemyBurstAttackSettings.Presets.cs','EnemyAttackPatternSettings.cs']:
 p=Path('Assets/Scripts/Gameplay/Enemies')/name;s=p.read_text();s=s.replace('internal EnemyShootingSettings AuthoredShooting', 'public EnemyShootingSettings AuthoredShooting');p.write_text(s,encoding='utf-8')
print('Shared preset Inspector integrated')
