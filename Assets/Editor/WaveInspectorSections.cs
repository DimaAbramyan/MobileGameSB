using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>Editor-session presentation state; never writes foldouts into wave settings.</summary>
internal static class WaveInspectorSections
{
    internal static float Line => EditorGUIUtility.singleLineHeight + 2f;

    private static string Key(SerializedProperty owner, string title) =>
        Key(owner.serializedObject, owner.propertyPath + "." + title);

    private static string Key(SerializedObject owner, string title) =>
        "WaveInspector.Sections." + owner.targetObject.GetInstanceID() + "." + title;

    private static void Remember(string key, bool expanded, bool previous)
    {
        if (expanded == previous) return;
        SessionState.SetBool(key, expanded);
        UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
    }

    internal static bool Expanded(SerializedProperty owner, string title, bool defaultExpanded = true) =>
        SessionState.GetBool(Key(owner, title), defaultExpanded && !title.StartsWith("Advanced", StringComparison.Ordinal));

    internal static bool Header(ref Rect rect, SerializedProperty owner, string title, GUIContent label = null, bool defaultExpanded = true)
    {
        rect.height = EditorGUIUtility.singleLineHeight;
        bool changed = GUI.changed;
        bool enabled = GUI.enabled;
        GUI.enabled = true; // Linked presets remain inspectable while their values are read-only.
        string key = Key(owner, title);
        bool previous = SessionState.GetBool(key, defaultExpanded && !title.StartsWith("Advanced", StringComparison.Ordinal));
        bool expanded = EditorGUI.Foldout(rect, previous, label ?? new GUIContent(title), true, EditorStyles.foldoutHeader);
        GUI.enabled = enabled;
        Remember(key, expanded, previous);
        GUI.changed = changed;
        rect.y += Line;
        return expanded;
    }

    // A #title starts a section. The same stream determines both height and drawing,
    // including conditional fields in linked presets and nested child patterns.
    private static readonly Unity.Profiling.ProfilerMarker ProfileHeight = new("WaveEditor.Inspector.SectionHeight");
    internal static float Height(SerializedProperty owner, IEnumerable<string> fields, Func<string, float> fieldHeight)
    {
        using var profileScope = ProfileHeight.Auto();
        float height = 0f;
        bool expanded = true;
        foreach (string field in fields)
        {
            if (field[0] == '#')
            {
                height += Line;
                expanded = Expanded(owner, field.Substring(1));
            }
            else if (expanded) height += fieldHeight(field) + 2f;
        }
        return height;
    }

    private static readonly Unity.Profiling.ProfilerMarker ProfileDraw = new("WaveEditor.Inspector.SectionDraw");
    internal static void Draw(ref Rect rect, SerializedProperty owner, IEnumerable<string> fields,
        Func<string, float> fieldHeight, Action<Rect, string> drawField)
    {
        using var profileScope = ProfileDraw.Auto();
        bool expanded = true, indented = false;
        int indent = EditorGUI.indentLevel;
        try
        {
            foreach (string field in fields)
            {
                if (field[0] == '#')
                {
                    EditorGUI.indentLevel = indent;
                    expanded = Header(ref rect, owner, field.Substring(1));
                    indented = true;
                }
                else if (expanded)
                {
                    EditorGUI.indentLevel = indent + (indented ? 1 : 0);
                    rect.height = fieldHeight(field);
                    drawField(rect, field);
                    rect.y += rect.height + 2f;
                }
            }
        }
        finally { EditorGUI.indentLevel = indent; }
    }

    internal static bool BeginHeaderGroup(SerializedObject owner, string id, string title, bool defaultExpanded = true)
    {
        bool changed = GUI.changed;
        bool previous = SessionState.GetBool(Key(owner, id), defaultExpanded);
        bool expanded = EditorGUILayout.BeginFoldoutHeaderGroup(previous, title);
        Remember(Key(owner, id), expanded, previous);
        GUI.changed = changed;
        return expanded;
    }

    internal static bool LayoutHeader(SerializedObject owner, string id, string title, bool defaultExpanded = true)
    {
        bool changed = GUI.changed;
        bool enabled = GUI.enabled;
        GUI.enabled = true;
        bool previous = SessionState.GetBool(Key(owner, id), defaultExpanded);
        bool expanded = EditorGUILayout.Foldout(previous, title, true, EditorStyles.foldoutHeader);
        GUI.enabled = enabled;
        Remember(Key(owner, id), expanded, previous);
        GUI.changed = changed;
        return expanded;
    }

    internal readonly struct Scope : IDisposable
    {
        private readonly int indent;
        internal bool IsExpanded { get; }

        internal Scope(SerializedObject owner, string id, string title, bool defaultExpanded = true)
        {
            indent = EditorGUI.indentLevel;
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            IsExpanded = LayoutHeader(owner, id, title, defaultExpanded);
            if (IsExpanded) EditorGUI.indentLevel++;
        }

        public void Dispose()
        {
            EditorGUI.indentLevel = indent;
            EditorGUILayout.EndVertical();
        }
    }
}
