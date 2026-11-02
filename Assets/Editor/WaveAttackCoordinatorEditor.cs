using System.Collections.Generic;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

[CustomEditor(typeof(WaveAttackCoordinator))]
public sealed class WaveAttackCoordinatorEditor : Editor
{
    private ReorderableList groups;
    private SerializedProperty settings;
    private void OnEnable()
    {
        settings = serializedObject.FindProperty("settings");
        var list = settings.FindPropertyRelative("groups");
        groups = new ReorderableList(serializedObject, list, true, true, true, true);
        groups.drawHeaderCallback = rect => EditorGUI.LabelField(rect, "Attack groups (drag to reorder)");
        groups.elementHeightCallback = index => EditorGUI.GetPropertyHeight(list.GetArrayElementAtIndex(index), true) + 5f;
        groups.drawElementCallback = (rect, index, active, focused) =>
        {
            rect.y += 2f; rect.height -= 4f;
            EditorGUI.PropertyField(rect, list.GetArrayElementAtIndex(index), new GUIContent($"{index + 1}. {list.GetArrayElementAtIndex(index).FindPropertyRelative("groupName").stringValue}"), true);
        };
        groups.onAddCallback = value =>
        {
            serializedObject.ApplyModifiedProperties();
            var coordinator = (WaveAttackCoordinator)target;
            Undo.RecordObject(coordinator, "Add attack group");
            coordinator.Settings.groups.Add(new WaveAttackCoordinatorGroup());
            coordinator.InvalidateConfiguration();
            EditorUtility.SetDirty(coordinator);
            PrefabUtility.RecordPrefabInstancePropertyModifications(coordinator);
            serializedObject.Update();
            value.index = coordinator.Settings.groups.Count - 1;
        };
    }
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        EditorGUILayout.LabelField("Wave Attack Coordinator", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(settings.FindPropertyRelative("enabled"), new GUIContent("Enabled"));
        using (var playback = new WaveInspectorSections.Scope(serializedObject, "Playback", "Sequence & Switching"))
            if (playback.IsExpanded)
            {
                foreach (string name in new[] { "startDelay", "loopSequence", "loopCount", "globalCooldown", "startGroupIndex", "switchMode", "reactivation" })
                {
                    if (name == "loopCount" && settings.FindPropertyRelative("loopSequence").boolValue) continue;
                    EditorGUILayout.PropertyField(settings.FindPropertyRelative(name));
                }
            }
        using (var groupSection = new WaveInspectorSections.Scope(serializedObject, "Groups", "Attack Groups"))
        {
            if (groupSection.IsExpanded)
            {
                groups.DoLayoutList();
                using (new EditorGUI.DisabledScope(groups.index < 0 || groups.index >= groups.count))
                    if (GUILayout.Button("Duplicate selected group"))
                    {
                        var list = settings.FindPropertyRelative("groups");
                        list.InsertArrayElementAtIndex(groups.index);
                        groups.index++;
                    }
            }
        }
        using (var advanced = new WaveInspectorSections.Scope(serializedObject, "Advanced", "Advanced / Randomization & Help", false))
            if (advanced.IsExpanded)
            {
                EditorGUILayout.PropertyField(settings.FindPropertyRelative("randomSeed"));
                DrawCoordinatorHelp();
            }
        serializedObject.ApplyModifiedProperties();
        DrawWarnings((WaveAttackCoordinator)target);
        if (Application.isPlaying)
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Restart Coordinator")) ((WaveAttackCoordinator)target).RestartCoordination();
                if (GUILayout.Button("Stop Coordinator")) ((WaveAttackCoordinator)target).StopCoordination();
            }
    }
    private static void DrawCoordinatorHelp()
    {
        EditorGUILayout.HelpBox("All Completed = one full cycle in every attack group; Sequence = one traversal (including Repeat Blocks). Released projectiles and child attacks continue. Resume preserves the interrupted attack and cooldown. Overlap allows the next group while outgoing attacks drain; Timed groups can start the next group before Duration ends.", MessageType.Info);
    }
    private static void DrawWarnings(WaveAttackCoordinator coordinator)
    {
        var owner = coordinator.GetComponent<Wave>();
        if (owner == null) return;
        var sources = GetSources(owner);
        if (coordinator.Settings.groups.Count == 0) EditorGUILayout.HelpBox("No groups: the coordinator has nothing to control.", MessageType.Warning);
        else if (coordinator.Settings.startGroupIndex >= coordinator.Settings.groups.Count)
            EditorGUILayout.HelpBox("Start Group Index is outside the list; the last group will be used.", MessageType.Warning);
        for (int i = 0; i < coordinator.Settings.groups.Count; i++)
        {
            var group = coordinator.Settings.groups[i];
            if (group == null) continue;
            if (group.controlledSubWaves.Count == 0) EditorGUILayout.HelpBox($"Group {i + 1} is empty; it will be skipped.", MessageType.Warning);
            if (group.maxWaitDuration > 0f && (group.startDelay >= group.maxWaitDuration
                || group.completionMode == WaveAttackGroupCompletion.Timed && group.startDelay + group.duration > group.maxWaitDuration))
                EditorGUILayout.HelpBox($"Group {i + 1}: Max Wait Duration expires before its configured delays / duration finish.", MessageType.Warning);
            if (group.maxWaitDuration <= 0f && (group.waitUntilReady || group.completionMode != WaveAttackGroupCompletion.Timed))
                EditorGUILayout.HelpBox($"Group {i + 1}: timeout disabled. Missing targets, disabled attacks or an endless entry route may prevent completion.", MessageType.Warning);
            foreach (var reference in group.controlledSubWaves)
            {
                if (reference == null || reference.scheduleIndex < 0 || reference.scheduleIndex >= sources.Count
                    || sources[reference.scheduleIndex] == null)
                { EditorGUILayout.HelpBox($"Group {i + 1}: missing schedule reference.", MessageType.Warning); continue; }
                var source = sources[reference.scheduleIndex];
                if (reference.subWavePrefab != null && reference.subWavePrefab != source)
                    EditorGUILayout.HelpBox($"Group {i + 1}: schedule slot {reference.scheduleIndex + 1} changed. Reselect its participant.", MessageType.Warning);
                var directed = source.GetComponent<DirectedEnemySubWave>();
                if (directed == null || directed.AttackPatternBehaviour == null || !directed.AttackPatternBehaviour.enabled)
                    EditorGUILayout.HelpBox($"{source.name}: requires an enabled Directed Wave Attack Behaviour.", MessageType.Warning);
                else if (group.waitUntilReady && float.IsPositiveInfinity(directed.GetSimulationPreviewPostStartTime()))
                    EditorGUILayout.HelpBox($"{source.name}: its entry never reaches the post timeline. Wait Until Ready needs a timeout or a finite entry route.", MessageType.Warning);
            }
        }
    }
    internal static List<GameObject> GetSources(Wave wave)
    {
        var result = new List<GameObject>();
        if (wave == null) return result;
        if (wave.ScheduledSubWaves.Count > 0)
            foreach (var cue in wave.ScheduledSubWaves) result.Add(cue?.SubWavePrefab);
        else if (wave.SubWavesToCreate != null)
            foreach (var source in wave.SubWavesToCreate) if (source != null) result.Add(source);
        return result;
    }
}

[CustomPropertyDrawer(typeof(WaveAttackSubWaveReference))]
public sealed class WaveAttackSubWaveReferenceDrawer : PropertyDrawer
{
    public override void OnGUI(Rect rect, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(rect, label, property);
        var coordinator = property.serializedObject.targetObject as WaveAttackCoordinator;
        var sources = WaveAttackCoordinatorEditor.GetSources(coordinator != null ? coordinator.GetComponent<Wave>() : null);
        var choices = new GUIContent[sources.Count + 1]; choices[0] = new GUIContent("Missing / select subwave");
        for (int i = 0; i < sources.Count; i++) choices[i + 1] = new GUIContent($"{i + 1}. {(sources[i] != null ? sources[i].name : "Missing")}");
        var index = property.FindPropertyRelative("scheduleIndex");
        int selected = index.intValue >= 0 && index.intValue < sources.Count ? index.intValue + 1 : 0;
        EditorGUI.BeginChangeCheck();
        int value = EditorGUI.Popup(rect, label, selected, choices) - 1;
        if (EditorGUI.EndChangeCheck())
        {
            index.intValue = value;
            property.FindPropertyRelative("subWavePrefab").objectReferenceValue = value >= 0 ? sources[value] : null;
        }
        EditorGUI.EndProperty();
    }
}

[CustomPropertyDrawer(typeof(WaveAttackCoordinatorGroup))]
public sealed class WaveAttackCoordinatorGroupDrawer : PropertyDrawer
{
    private static IEnumerable<string> Fields(SerializedProperty property)
    {
        yield return "groupName"; yield return "controlledSubWaves";
        yield return "#Activation & Completion";
        yield return "startDelay"; yield return "waitUntilReady"; yield return "completionMode";
        var mode = (WaveAttackGroupCompletion)property.FindPropertyRelative("completionMode").enumValueIndex;
        if (mode == WaveAttackGroupCompletion.Timed) yield return "duration";
        if (mode == WaveAttackGroupCompletion.AttackCycles) yield return "attackCyclesCount";
        yield return "onEnemyDeath";
        yield return "#Transition";
        yield return "delayAfterGroup"; yield return "overlapDuration";
        yield return "#Advanced / Safety Timeout";
        yield return "maxWaitDuration";
    }
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label) =>
        WaveInspectorSections.Line + (WaveInspectorSections.Expanded(property, "Group Settings", property.isExpanded)
            ? WaveInspectorSections.Height(property, Fields(property), field => EditorGUI.GetPropertyHeight(property.FindPropertyRelative(field), true)) : 0f);

    public override void OnGUI(Rect rect, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(rect, label, property);
        bool expanded = WaveInspectorSections.Header(ref rect, property, "Group Settings", label, property.isExpanded);
        if (expanded)
        {
            EditorGUI.indentLevel++;
            WaveInspectorSections.Draw(ref rect, property, Fields(property),
                field => EditorGUI.GetPropertyHeight(property.FindPropertyRelative(field), true),
                (row, field) => EditorGUI.PropertyField(row, property.FindPropertyRelative(field), true));
            EditorGUI.indentLevel--;
        }
        EditorGUI.EndProperty();
    }
}
