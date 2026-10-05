using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(EnemyBurstAttackSettings))]
public sealed class EnemyBurstAttackSettingsDrawer : PropertyDrawer
{
    private const float Spacing = 2f;
    private static float Line => EditorGUIUtility.singleLineHeight + Spacing;
    private static bool Multiple(SerializedProperty p) => p.FindPropertyRelative("attackPatterns").enumValueIndex == (int)EnemyAttackPatternsMode.Multiple;

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        float height = 2 * Line + EnemyFacingFields.Height(property);
        if (!Multiple(property))
            return height + EnemyAttackPatternFields.Height(property, property.name != "waveBurstSettings");
        height += 2 * Line + Line;
        var patterns = property.FindPropertyRelative("patterns");
        for (int i = 0; i < patterns.arraySize; i++)
        {
            var entry = patterns.GetArrayElementAtIndex(i);
            height += Line;
            if (entry.isExpanded)
                height += EnemyAttackPatternFields.Height(entry, true) + Line * .5f;
        }
        return height;
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);
        position.height = EditorGUIUtility.singleLineHeight;
        EditorGUI.LabelField(position, label, EditorStyles.boldLabel);
        EditorGUI.indentLevel++;
        position.y += Line;
        var mode = property.FindPropertyRelative("attackPatterns");
        EditorGUI.BeginChangeCheck();
        EditorGUI.PropertyField(position, mode, new GUIContent("Attack Patterns"));
        var patterns = property.FindPropertyRelative("patterns");
        if (EditorGUI.EndChangeCheck() && Multiple(property) && patterns.arraySize == 0)
            InitializeMultiple(property);
        position.y += Line;
        EnemyFacingFields.Draw(ref position, property);
        if (!Multiple(property))
            EnemyAttackPatternFields.Draw(ref position, property, property.name != "waveBurstSettings");
        else
        {
            position.height = 2 * Line;
            EditorGUI.HelpBox(position, patterns.arraySize == 0
                ? "Add at least one attack pattern. An empty list cannot fire."
                : "Patterns fire together with independent timing. Firing rotation is independent of the visual Facing Mode.",
                patterns.arraySize == 0 ? MessageType.Warning : MessageType.Info);
            position.y += position.height;
            position.height = EditorGUIUtility.singleLineHeight;
            for (int i = 0; i < patterns.arraySize; i++)
            {
                var entry = patterns.GetArrayElementAtIndex(i);
                var header = EditorGUI.IndentedRect(position);
                header.width -= 150f;
                entry.isExpanded = EditorGUI.Foldout(header, entry.isExpanded, $"Pattern {i + 1}", true);
                var buttons = new Rect(position.xMax - 146f, position.y, 64f, position.height);
                if (GUI.Button(buttons, "Duplicate"))
                {
                    Duplicate(patterns, i);
                    break;
                }
                buttons.x += 66f; buttons.width = 24f;
                using (new EditorGUI.DisabledScope(i == 0))
                    if (GUI.Button(buttons, "↑")) { patterns.MoveArrayElement(i, i - 1); break; }
                buttons.x += 26f;
                using (new EditorGUI.DisabledScope(i == patterns.arraySize - 1))
                    if (GUI.Button(buttons, "↓")) { patterns.MoveArrayElement(i, i + 1); break; }
                buttons.x += 26f;
                if (GUI.Button(buttons, "−")) { patterns.DeleteArrayElementAtIndex(i); break; }
                position.y += Line;
                if (entry.isExpanded)
                {
                    EditorGUI.indentLevel++;
                    EnemyAttackPatternFields.Draw(ref position, entry, true);
                    EditorGUI.indentLevel--;
                    position.y += Line * .5f;
                }
                position.height = EditorGUIUtility.singleLineHeight;
            }
            if (GUI.Button(EditorGUI.IndentedRect(position), "Add Pattern"))
            {
                int index = patterns.arraySize;
                patterns.arraySize++;
                patterns.GetArrayElementAtIndex(index).boxedValue = new EnemyAttackPatternSettings();
                patterns.GetArrayElementAtIndex(index).isExpanded = true;
            }
        }
        EditorGUI.indentLevel--;
        EditorGUI.EndProperty();
    }

    public static void InitializeMultiple(SerializedProperty property)
    {
        var patterns = property.FindPropertyRelative("patterns");
        if (patterns.arraySize != 0)
            return;
        var first = new EnemyAttackPatternSettings();
        first.CopyFrom((EnemyBurstAttackSettings)property.boxedValue);
        patterns.arraySize = 1;
        patterns.GetArrayElementAtIndex(0).boxedValue = first;
        patterns.GetArrayElementAtIndex(0).isExpanded = true;
    }

    public static void Duplicate(SerializedProperty patterns, int index)
    {
        var copy = new EnemyAttackPatternSettings();
        copy.CopyFrom((EnemyAttackPatternSettings)patterns.GetArrayElementAtIndex(index).boxedValue);
        patterns.InsertArrayElementAtIndex(index + 1);
        var entry = patterns.GetArrayElementAtIndex(index + 1);
        entry.boxedValue = copy;
        entry.isExpanded = true;
    }
}

internal static class EnemyAttackPatternFields
{
    private const float Spacing = 2f;
    private static bool Repeats(SerializedProperty p) => p.FindPropertyRelative("burstAttack").enumValueIndex == (int)EnemyBurstAttackMode.Enabled;

    public static float Height(SerializedProperty property, bool hasDelay)
    {
        int lines = 6 + (Repeats(property) ? 2 : 0);
        if (hasDelay) lines += property.FindPropertyRelative("useAttackStartDelay").boolValue ? 2 : 1;
        return lines * (EditorGUIUtility.singleLineHeight + Spacing)
            + EditorGUI.GetPropertyHeight(property.FindPropertyRelative("shooting"), true) + Spacing;
    }

    public static void Draw(ref Rect position, SerializedProperty property, bool hasDelay)
    {
        var shooting = property.FindPropertyRelative("shooting");
        Field(ref position, shooting, "Shooting");
        if (hasDelay)
        {
            Field(ref position, property.FindPropertyRelative("useAttackStartDelay"), "Delay Attack Start");
            if (property.FindPropertyRelative("useAttackStartDelay").boolValue)
                Field(ref position, property.FindPropertyRelative("attackStartDelay"), "Attack Start Delay");
        }
        Field(ref position, property.FindPropertyRelative("burstAttack"), "Burst Attack");
        bool repeats = Repeats(property);
        var count = property.FindPropertyRelative("attackShotCount");
        var interval = property.FindPropertyRelative("attackShotInterval");
        var cooldown = property.FindPropertyRelative("attackCooldown");
        var burstCount = property.FindPropertyRelative("burstShotCount");
        var burstInterval = property.FindPropertyRelative("burstShotInterval");
        Field(ref position, count, repeats ? "Bursts Per Attack" : "Shots Per Attack");
        Field(ref position, interval, repeats ? "Burst Interval" : "Shot Interval");
        Field(ref position, cooldown, "Attack Cooldown");
        if (repeats)
        {
            Field(ref position, burstCount, "Shots Per Burst");
            Field(ref position, burstInterval, "Burst Shot Interval");
        }
        int events = Mathf.Max(1, count.intValue) * (repeats ? Mathf.Max(1, burstCount.intValue) : 1);
        int projectiles = events * EnemyShootingSettingsDrawer.GetProjectilesPerVolley(shooting);
        float duration = EnemyBurstAttackSettings.CalculateAttackDuration(repeats, count.intValue,
            interval.floatValue, burstCount.intValue, burstInterval.floatValue);
        position.height = EditorGUIUtility.singleLineHeight;
        EditorGUI.LabelField(position, $"Attack duration: {duration:0.###} s ({events} shot events / {projectiles} projectiles)", EditorStyles.miniLabel);
        position.y += position.height + Spacing;
        EditorGUI.LabelField(position, $"Full attack cycle: {duration + Mathf.Max(0f, cooldown.floatValue):0.###} s (including cooldown)", EditorStyles.miniLabel);
        position.y += position.height + Spacing;
    }

    private static void Field(ref Rect position, SerializedProperty property, string label)
    {
        position.height = EditorGUI.GetPropertyHeight(property, true);
        EditorGUI.PropertyField(position, property, new GUIContent(label), true);
        position.y += position.height + Spacing;
    }
}

internal static class EnemyFacingFields
{
    private static float Line => EditorGUIUtility.singleLineHeight + 2f;
    private static bool Follows(SerializedProperty p) => p.FindPropertyRelative("facingMode").enumValueIndex
        == (int)EnemyFacingMode.FollowPattern;

    public static float Height(SerializedProperty property)
    {
        if (!Follows(property))
            return Line;
        return 4 * Line + (MissingPattern(property) ? 2 * Line : 0f);
    }

    private static bool MissingPattern(SerializedProperty property)
    {
        return Follows(property) && ((EnemyBurstAttackSettings)property.boxedValue).FacingPatternIndex < 0;
    }

    public static void Draw(ref Rect position, SerializedProperty property)
    {
        EditorGUI.PropertyField(position, property.FindPropertyRelative("facingMode"), new GUIContent("Facing Mode"));
        position.y += Line;
        if (!Follows(property))
            return;
        var patterns = property.FindPropertyRelative("patterns");
        bool multiple = property.FindPropertyRelative("attackPatterns").enumValueIndex == (int)EnemyAttackPatternsMode.Multiple;
        var selected = property.FindPropertyRelative("facingPatternId");
        int count = multiple ? patterns.arraySize : 1;
        var labels = new string[count];
        int current = -1;
        for (int i = 0; i < count; i++)
        {
            var shooting = multiple ? patterns.GetArrayElementAtIndex(i).FindPropertyRelative("shooting")
                : property.FindPropertyRelative("shooting");
            string type = shooting.FindPropertyRelative("shootingType").enumDisplayNames[shooting.FindPropertyRelative("shootingType").enumValueIndex];
            string shape = shooting.FindPropertyRelative("shootingMode").enumDisplayNames[shooting.FindPropertyRelative("shootingMode").enumValueIndex];
            labels[i] = $"Pattern {i + 1} ({type} / {shape})";
            if (!multiple || (i == 0 && string.IsNullOrEmpty(selected.stringValue))
                || selected.stringValue == patterns.GetArrayElementAtIndex(i).FindPropertyRelative("patternId").stringValue)
                current = i;
        }
        EditorGUI.BeginChangeCheck();
        using (new EditorGUI.DisabledScope(count == 0))
        {
            int choice = EditorGUI.Popup(position, "Facing Pattern", current, labels);
            if (EditorGUI.EndChangeCheck() && choice >= 0)
                selected.stringValue = multiple ? patterns.GetArrayElementAtIndex(choice).FindPropertyRelative("patternId").stringValue : null;
        }
        // Bind the initial selection to its identity so reordering keeps the same attack selected.
        if (multiple && count > 0 && string.IsNullOrEmpty(selected.stringValue))
            selected.stringValue = patterns.GetArrayElementAtIndex(0).FindPropertyRelative("patternId").stringValue;
        position.y += Line;
        EditorGUI.PropertyField(position, property.FindPropertyRelative("facingSpeed"), new GUIContent("Facing Speed"));
        position.y += Line;
        EditorGUI.PropertyField(position, property.FindPropertyRelative("facingAngleOffset"), new GUIContent("Facing Angle Offset"));
        position.y += Line;
        if (MissingPattern(property))
        {
            position.height = 2 * Line;
            EditorGUI.HelpBox(position, "Facing Pattern is missing. Select an existing attack pattern.", MessageType.Warning);
            position.y += position.height;
            position.height = EditorGUIUtility.singleLineHeight;
        }
    }
}
