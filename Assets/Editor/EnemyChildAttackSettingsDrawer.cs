using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(EnemyChildAttackSettings))]
public sealed class EnemyChildAttackSettingsDrawer : PropertyDrawer
{
    private static float Line => EditorGUIUtility.singleLineHeight + 2f;
    public static bool IsChildPattern(SerializedProperty property) => property.propertyPath.Contains(".childAttack.");
    private static int Depth(SerializedProperty property)
    {
        int count = 0, offset = 0;
        while ((offset = property.propertyPath.IndexOf("childAttack", offset, System.StringComparison.Ordinal)) >= 0)
        { count++; offset += 11; }
        return count;
    }
    private static IEnumerable<string> Fields(SerializedProperty p)
    {
        yield return "#Trigger";
        yield return "triggerMode";
        if (p.FindPropertyRelative("triggerMode").intValue == (int)EnemyChildAttackTriggerMode.OnEvent)
        {
            yield return "eventType";
            if (p.FindPropertyRelative("eventType").intValue == (int)EnemyChildAttackEvent.OnScreenBoundaryReached)
            {
                yield return "triggerBoundaries"; yield return "boundaryOffset";
                yield return "triggerOnce"; yield return "timeOffset";
            }
            if(p.FindPropertyRelative("eventType").intValue==(int)EnemyChildAttackEvent.OnTargetReached)yield return "timeOffset";
        }
        else yield return "triggerValue";
        yield return "#Repetition & Parent Lifetime";
        yield return "repeatMode";
        if (p.FindPropertyRelative("repeatMode").intValue == (int)EnemyChildAttackRepeatMode.FixedCount)
            yield return "repeatCount";
        yield return "repeatInterval"; yield return "destroyParent";
        yield return "#Child Emission Origin";
        yield return "spawnOffset"; yield return "offsetSpace";
        bool fixedTargetOrigin=p.FindPropertyRelative("triggerMode").intValue==(int)EnemyChildAttackTriggerMode.OnEvent
            && p.FindPropertyRelative("eventType").intValue==(int)EnemyChildAttackEvent.OnTargetReached;
        if(!fixedTargetOrigin)yield return "originMode";
        yield return "#Child Aiming";
        yield return "fireMode";
        int mode = p.FindPropertyRelative("fireMode").intValue;
        if (mode == (int)DirectedWaveAttackFireMode.Aimed) yield return "aimMode";
        if (mode == (int)DirectedWaveAttackFireMode.Stationary) yield return "stationaryFireSettings";
    }
    private static readonly Unity.Profiling.ProfilerMarker ProfileGetPropertyHeight = new("WaveEditor.Inspector.ChildHeight");
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        using var profileScope = ProfileGetPropertyHeight.Auto();
        if (Depth(property) > EnemyChildAttackSettings.MaximumDepth) return 46f;
        if (string.IsNullOrEmpty(property.managedReferenceFullTypename)) return Line;
        float height = 2 * Line + WaveInspectorSections.Height(property, Fields(property),
            name => FieldHeight(property.FindPropertyRelative(name)));
        if (WaveInspectorSections.Expanded(property, "Child Shooting Pattern"))
            height += EnemyAttackPatternFields.Height(property.FindPropertyRelative("pattern"), true);
        if (WaveInspectorSections.Expanded(property, "Advanced / Child Limits")) height += 46f;
        return height;
    }
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);
        if (Depth(property) > EnemyChildAttackSettings.MaximumDepth)
        {
            EditorGUI.HelpBox(position, "Maximum nested child attack depth is 8. Further child attacks are not executed.", MessageType.Warning);
            EditorGUI.EndProperty(); return;
        }
        if (string.IsNullOrEmpty(property.managedReferenceFullTypename))
        {
            property.managedReferenceValue = EnemyChildAttackSettings.CreateDefault();
            property.serializedObject.ApplyModifiedProperties();
            EditorGUI.EndProperty(); return;
        }
        position.height = EditorGUIUtility.singleLineHeight;
        WaveInspectorSections.Draw(ref position, property, Fields(property),
            name => FieldHeight(property.FindPropertyRelative(name)),
            (fieldRect, name) => DrawField(fieldRect, property, name));
        if (WaveInspectorSections.Header(ref position, property, "Child Shooting Pattern"))
            EnemyAttackPatternFields.Draw(ref position, property.FindPropertyRelative("pattern"), true);
        if (WaveInspectorSections.Header(ref position, property, "Advanced / Child Limits"))
        {
            position.height = 44f;
            bool untilDestroyed = property.FindPropertyRelative("repeatMode").intValue == (int)EnemyChildAttackRepeatMode.UntilParentDestroyed;
            EditorGUI.HelpBox(position, untilDestroyed
                ? "Repeats while the parent exists; Destroy Parent does not apply. Interval starts after all shots finish. Limits: 8 generations, 2048 descendants per repetition, 512 child spawns per step."
                : "Origin is Parent + Spawn Offset. Repeat Interval starts after all shots and Sector launches finish. Limits: 8 generations, 2048 descendants per root, 512 child spawns per simulation step.", MessageType.Info);
            position.y += 46f;
        }
        EditorGUI.EndProperty();
    }

    private static void DrawField(Rect position, SerializedProperty property, string name)
    {
        var field = property.FindPropertyRelative(name);
        GUIContent fieldLabel = name == "triggerValue"
            ? new GUIContent("Trigger Value", property.FindPropertyRelative("triggerMode").intValue == (int)EnemyChildAttackTriggerMode.AfterTime ? "Seconds since this projectile was created (scaled game time)." : "Actual travelled path length, including curved movement and slow fields.")
            : new GUIContent(field.displayName, field.tooltip);
        if (name == "triggerBoundaries") DrawTriggerBoundaries(position, property, field);
        else if (name == "fireMode")
        {
            int selected = field.intValue == (int)DirectedWaveAttackFireMode.Aimed ? 1 : field.intValue == (int)DirectedWaveAttackFireMode.Stationary ? 2 : 0;
            EditorGUI.BeginChangeCheck();
            selected = EditorGUI.Popup(position, "Fire Mode", selected, new[] { "Default (Parent Direction)", "Aimed", "Stationary" });
            if (EditorGUI.EndChangeCheck()) field.intValue = selected == 1 ? (int)DirectedWaveAttackFireMode.Aimed : selected == 2 ? (int)DirectedWaveAttackFireMode.Stationary : (int)DirectedWaveAttackFireMode.Default;
        }
        else
        {
            using (new EditorGUI.DisabledScope(name == "destroyParent"
                && property.FindPropertyRelative("repeatMode").intValue == (int)EnemyChildAttackRepeatMode.UntilParentDestroyed))
                EditorGUI.PropertyField(position, field, fieldLabel, true);
        }
    }

    private static float FieldHeight(SerializedProperty field) => field.name == "triggerBoundaries"
        ? EditorGUIUtility.singleLineHeight * 3f + 4f : EditorGUI.GetPropertyHeight(field, true);

    private static void DrawTriggerBoundaries(Rect position, SerializedProperty owner, SerializedProperty field)
    {
        var legacy = owner.FindPropertyRelative("boundary");
        var selection = EnemyChildAttackBoundarySelection.Resolve(field.intValue, (EnemyChildAttackBoundary)legacy.intValue);
        var label = new GUIContent("Trigger Boundaries", "Only checked sides trigger the attack. Unchecked sides do not block further travel. No checked sides disables this event.");
        EditorGUI.BeginProperty(position, label, field);
        position.height = EditorGUIUtility.singleLineHeight;
        EditorGUI.LabelField(position, label, EditorStyles.boldLabel);
        Rect row = EditorGUI.IndentedRect(position);
        row.y += Line;
        float half = row.width * .5f;
        bool mixed = EditorGUI.showMixedValue;
        EditorGUI.showMixedValue = field.hasMultipleDifferentValues || field.intValue < 0 && legacy.hasMultipleDifferentValues;
        EditorGUI.BeginChangeCheck();
        bool left = Toggle(row, 0f, "Left", EnemyChildAttackBoundaries.Left);
        bool right = Toggle(row, half, "Right", EnemyChildAttackBoundaries.Right);
        row.y += Line;
        bool top = Toggle(row, 0f, "Top", EnemyChildAttackBoundaries.Top);
        bool bottom = Toggle(row, half, "Bottom", EnemyChildAttackBoundaries.Bottom);
        if (EditorGUI.EndChangeCheck())
            field.intValue = (left ? (int)EnemyChildAttackBoundaries.Left : 0)
                | (right ? (int)EnemyChildAttackBoundaries.Right : 0)
                | (top ? (int)EnemyChildAttackBoundaries.Top : 0)
                | (bottom ? (int)EnemyChildAttackBoundaries.Bottom : 0);
        EditorGUI.showMixedValue = mixed;
        EditorGUI.EndProperty();

        bool Toggle(Rect rect, float x, string text, EnemyChildAttackBoundaries side)
        {
            rect.x += x; rect.width = half;
            return EditorGUI.ToggleLeft(rect, text, (selection & side) != 0);
        }
    }
}
