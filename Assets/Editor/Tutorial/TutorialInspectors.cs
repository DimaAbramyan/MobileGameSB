using Game.Tutorial;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

[CustomEditor(typeof(TutorialConfiguration))]
public sealed class TutorialConfigurationEditor : Editor
{
    public override void OnInspectorGUI() => TutorialEditorSettings.DrawConfiguration((TutorialConfiguration)target);
}

[CustomEditor(typeof(TutorialDefinition))]
public sealed class TutorialDefinitionEditor : Editor
{
    private ReorderableList steps;
    private void OnEnable()
    {
        steps = new ReorderableList(serializedObject, serializedObject.FindProperty("steps"), true, true, true, true);
        steps.drawHeaderCallback = rect => EditorGUI.LabelField(rect, "Tutorial Steps — stable IDs preserve saved progress");
        steps.elementHeightCallback = index => EditorGUI.GetPropertyHeight(steps.serializedProperty.GetArrayElementAtIndex(index), true) + 6;
        steps.drawElementCallback = (rect, index, active, focused) =>
        {
            var step = steps.serializedProperty.GetArrayElementAtIndex(index);
            rect.y += 2; rect.height -= 6;
            EditorGUI.PropertyField(rect, step, new GUIContent($"{index + 1}. {step.FindPropertyRelative("id").stringValue}"), true);
        };
        steps.onAddCallback = list =>
        {
            int index = list.serializedProperty.arraySize++;
            var item = list.serializedProperty.GetArrayElementAtIndex(index);
            item.FindPropertyRelative("id").stringValue = System.Guid.NewGuid().ToString("N");
            item.isExpanded = true;
        };
    }
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        EditorGUILayout.PropertyField(serializedObject.FindProperty("version"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("level"));
        steps.DoLayoutList();
        serializedObject.ApplyModifiedProperties();
        if (!((TutorialDefinition)target).TryValidate(out string error)) EditorGUILayout.HelpBox(error, MessageType.Warning);
        TutorialEditorSettings.DrawDevelopmentControls();
    }
}

[CustomPropertyDrawer(typeof(TutorialCondition))]
public sealed class TutorialConditionDrawer : PropertyDrawer
{
    private static bool HasValue(TutorialConditionType type) => type != TutorialConditionType.Always
        && type != TutorialConditionType.GameplayReady && type != TutorialConditionType.MainMenuReady
        && type != TutorialConditionType.TwoShipsReady && type != TutorialConditionType.ManualConfirmation;
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        var type = (TutorialConditionType)property.FindPropertyRelative("type").enumValueIndex;
        return (1 + (HasValue(type) ? 1 : 0) + (type == TutorialConditionType.Signal ? 1 : 0)) * (EditorGUIUtility.singleLineHeight + 2);
    }
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);
        position.height = EditorGUIUtility.singleLineHeight;
        var type = property.FindPropertyRelative("type"); EditorGUI.PropertyField(position, type, label);
        var mode = (TutorialConditionType)type.enumValueIndex;
        if (mode == TutorialConditionType.Signal)
        { position.y += position.height + 2; EditorGUI.PropertyField(position, property.FindPropertyRelative("signal"), new GUIContent("Signal ID")); }
        if (HasValue(mode))
        {
            position.y += position.height + 2;
            string caption = mode == TutorialConditionType.TimeElapsed ? "Seconds" : mode == TutorialConditionType.PlayerMoved ? "World Distance" : "Count";
            EditorGUI.PropertyField(position, property.FindPropertyRelative("value"), new GUIContent(caption));
        }
        EditorGUI.EndProperty();
    }
}
