using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(EnemyLaserGeometry))]
public sealed class EnemyLaserGeometryDrawer : PropertyDrawer
{
    public static IEnumerable<string> Fields(SerializedProperty p)
    {
        yield return "#Geometry";
        yield return "lengthMode";
        if (p.FindPropertyRelative("lengthMode").intValue == (int)EnemyLaserLengthMode.Custom) yield return "laserLength";
        else yield return "lengthOffset";
        yield return "laserWidth"; yield return "laserOffset";
        yield return "#Size Behaviour";
        yield return "constantWidth";
        if (!p.FindPropertyRelative("constantWidth").boolValue) yield return "widthCurve";
        yield return "constantLength";
        if (!p.FindPropertyRelative("constantLength").boolValue) yield return "lengthCurve";
    }
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label) => EnemyLaserSettingsDrawer.Height(property, Fields(property));
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        => EnemyLaserSettingsDrawer.Draw(position, property, Fields(property));
}

[CustomPropertyDrawer(typeof(EnemyLaserSettings))]
public sealed class EnemyLaserSettingsDrawer : PropertyDrawer
{
    public static IEnumerable<string> Fields(SerializedProperty p)
    {
        yield return "geometry";
        yield return "#Duration";
        yield return "laserDuration"; yield return "activationDelay";
        yield return "fadeInDuration"; yield return "fadeOutDuration";
        yield return "followOrigin"; yield return "surviveSourceDestruction";
        yield return "#Damage";
        yield return "damage"; yield return "damageMode";
        if (p.FindPropertyRelative("damageMode").intValue == (int)EnemyLaserDamageMode.Periodic) yield return "damageInterval";
        yield return "#Telegraph"; yield return "enableTelegraph";
        if (!p.FindPropertyRelative("enableTelegraph").boolValue) yield break;
        yield return "telegraphDuration"; yield return "telegraphColor"; yield return "telegraphOpacity";
        yield return "telegraphFadeIn"; yield return "telegraphFadeOut"; yield return "overrideTelegraphGeometry";
        if (p.FindPropertyRelative("overrideTelegraphGeometry").boolValue) yield return "telegraphGeometry";
    }
    internal static float Height(SerializedProperty p, IEnumerable<string> fields)
    {
        float height = 0f;
        foreach (string field in fields) height += field[0] == '#' ? EditorGUIUtility.singleLineHeight + 5f
            : EditorGUI.GetPropertyHeight(p.FindPropertyRelative(field), true) + 2f;
        return height;
    }
    internal static void Draw(Rect position, SerializedProperty p, IEnumerable<string> fields)
    {
        EditorGUI.BeginProperty(position, GUIContent.none, p);
        foreach (string field in fields)
        {
            if (field[0] == '#')
            {
                position.height = EditorGUIUtility.singleLineHeight;
                EditorGUI.LabelField(position, field.Substring(1), EditorStyles.boldLabel);
                position.y += position.height + 5f;
            }
            else
            {
                var child = p.FindPropertyRelative(field);
                position.height = EditorGUI.GetPropertyHeight(child, true);
                EditorGUI.PropertyField(position, child, true);
                position.y += position.height + 2f;
            }
        }
        EditorGUI.EndProperty();
    }
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label) => Height(property, Fields(property));
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) => Draw(position, property, Fields(property));
}
