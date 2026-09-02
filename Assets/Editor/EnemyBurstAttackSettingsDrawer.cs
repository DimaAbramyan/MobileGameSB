using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(EnemyBurstAttackSettings))]
public sealed class EnemyBurstAttackSettingsDrawer : PropertyDrawer
{
    private const float VerticalSpacing = 2f;

    public override float GetPropertyHeight(
        SerializedProperty property,
        GUIContent label)
    {
        SerializedProperty repeatBurst = property.FindPropertyRelative(
            "repeatBurst");
        SerializedProperty useAttackStartDelay = property.FindPropertyRelative(
            "useAttackStartDelay");
        SerializedProperty useAreaAttack = property.FindPropertyRelative(
            "useAreaAttack");
        bool repeatsBurst = repeatBurst != null && repeatBurst.boolValue;
        bool supportsStartDelay = property.name != "waveBurstSettings";
        bool usesStartDelay = supportsStartDelay
            && useAttackStartDelay != null
            && useAttackStartDelay.boolValue;
        bool usesAreaAttack = useAreaAttack != null && useAreaAttack.boolValue;
        int lineCount = repeatsBurst ? 7 : 5;
        if (supportsStartDelay)
        {
            lineCount++;
            if (usesStartDelay)
                lineCount++;
        }
        lineCount++;
        if (usesAreaAttack)
            lineCount += 3;
        lineCount += 2;
        return lineCount * EditorGUIUtility.singleLineHeight
            + (lineCount - 1) * VerticalSpacing;
    }

    public override void OnGUI(
        Rect position,
        SerializedProperty property,
        GUIContent label)
    {
        SerializedProperty repeatBurst = property.FindPropertyRelative(
            "repeatBurst");
        SerializedProperty useAttackStartDelay = property.FindPropertyRelative(
            "useAttackStartDelay");
        SerializedProperty attackStartDelay = property.FindPropertyRelative(
            "attackStartDelay");
        SerializedProperty useAreaAttack = property.FindPropertyRelative(
            "useAreaAttack");
        SerializedProperty areaAttackProjectileCount = property.FindPropertyRelative(
            "areaAttackProjectileCount");
        SerializedProperty areaAttackMinAngle = property.FindPropertyRelative(
            "areaAttackMinAngle");
        SerializedProperty areaAttackMaxAngle = property.FindPropertyRelative(
            "areaAttackMaxAngle");
        SerializedProperty attackShotCount = property.FindPropertyRelative(
            "attackShotCount");
        SerializedProperty attackShotInterval = property.FindPropertyRelative(
            "attackShotInterval");
        SerializedProperty attackCooldown = property.FindPropertyRelative(
            "attackCooldown");
        SerializedProperty burstShotCount = property.FindPropertyRelative(
            "burstShotCount");
        SerializedProperty burstShotInterval = property.FindPropertyRelative(
            "burstShotInterval");

        if (repeatBurst == null
            || useAttackStartDelay == null
            || attackStartDelay == null
            || useAreaAttack == null
            || areaAttackProjectileCount == null
            || areaAttackMinAngle == null
            || areaAttackMaxAngle == null
            || attackShotCount == null
            || attackShotInterval == null
            || attackCooldown == null
            || burstShotCount == null
            || burstShotInterval == null)
        {
            EditorGUI.PropertyField(position, property, label, true);
            return;
        }

        EditorGUI.BeginProperty(position, label, property);
        Rect line = position;
        line.height = EditorGUIUtility.singleLineHeight;
        EditorGUI.LabelField(line, label, EditorStyles.boldLabel);

        EditorGUI.indentLevel++;
        bool supportsStartDelay = property.name != "waveBurstSettings";
        if (supportsStartDelay)
        {
            line.y += EditorGUIUtility.singleLineHeight + VerticalSpacing;
            EditorGUI.PropertyField(
                line,
                useAttackStartDelay,
                new GUIContent("Delay Attack Start"));

            if (useAttackStartDelay.boolValue)
            {
                line.y += EditorGUIUtility.singleLineHeight + VerticalSpacing;
                EditorGUI.PropertyField(
                    line,
                    attackStartDelay,
                    new GUIContent("Attack Start Delay"));
            }
        }

        line.y += EditorGUIUtility.singleLineHeight + VerticalSpacing;
        EditorGUI.PropertyField(
            line,
            useAreaAttack,
            new GUIContent("Area Attack"));

        if (useAreaAttack.boolValue)
        {
            line.y += EditorGUIUtility.singleLineHeight + VerticalSpacing;
            EditorGUI.PropertyField(
                line,
                areaAttackProjectileCount,
                new GUIContent("Projectiles Per Shot"));

            line.y += EditorGUIUtility.singleLineHeight + VerticalSpacing;
            EditorGUI.PropertyField(
                line,
                areaAttackMinAngle,
                new GUIContent("Min Angle"));

            line.y += EditorGUIUtility.singleLineHeight + VerticalSpacing;
            EditorGUI.PropertyField(
                line,
                areaAttackMaxAngle,
                new GUIContent("Max Angle"));
        }

        line.y += EditorGUIUtility.singleLineHeight + VerticalSpacing;
        EditorGUI.PropertyField(line, repeatBurst, new GUIContent("Repeat Burst"));

        line.y += EditorGUIUtility.singleLineHeight + VerticalSpacing;
        EditorGUI.PropertyField(line, attackShotCount, new GUIContent(
            repeatBurst.boolValue ? "Bursts Per Attack" : "Shots Per Attack"));

        line.y += EditorGUIUtility.singleLineHeight + VerticalSpacing;
        EditorGUI.PropertyField(line, attackShotInterval, new GUIContent(
            repeatBurst.boolValue ? "Burst Interval" : "Shot Interval"));

        line.y += EditorGUIUtility.singleLineHeight + VerticalSpacing;
        EditorGUI.PropertyField(line, attackCooldown,
            new GUIContent("Attack Cooldown"));

        if (repeatBurst.boolValue)
        {
            line.y += EditorGUIUtility.singleLineHeight + VerticalSpacing;
            EditorGUI.PropertyField(
                line,
                burstShotCount,
                new GUIContent("Shots Per Burst"));

            line.y += EditorGUIUtility.singleLineHeight + VerticalSpacing;
            EditorGUI.PropertyField(
                line,
                burstShotInterval,
                new GUIContent("Burst Shot Interval"));
        }

        int shotsPerAttack = Mathf.Max(1, attackShotCount.intValue)
            * (repeatBurst.boolValue
                ? Mathf.Max(1, burstShotCount.intValue)
                : 1);
        int projectilesPerShot = useAreaAttack.boolValue
            ? Mathf.Max(1, areaAttackProjectileCount.intValue)
            : 1;
        float attackDuration = EnemyBurstAttackSettings.CalculateAttackDuration(
            repeatBurst.boolValue,
            attackShotCount.intValue,
            attackShotInterval.floatValue,
            burstShotCount.intValue,
            burstShotInterval.floatValue);
        float cycleDuration = attackDuration
            + Mathf.Max(0f, attackCooldown.floatValue);

        line.y += EditorGUIUtility.singleLineHeight + VerticalSpacing;
        EditorGUI.LabelField(
            line,
            $"Attack duration: {attackDuration:0.###} s "
            + $"({shotsPerAttack} shot events / "
            + $"{shotsPerAttack * projectilesPerShot} projectiles)",
            EditorStyles.miniLabel);
        line.y += EditorGUIUtility.singleLineHeight + VerticalSpacing;
        EditorGUI.LabelField(
            line,
            $"Full attack cycle: {cycleDuration:0.###} s (including cooldown)",
            EditorStyles.miniLabel);

        EditorGUI.indentLevel--;
        EditorGUI.EndProperty();
    }
}
