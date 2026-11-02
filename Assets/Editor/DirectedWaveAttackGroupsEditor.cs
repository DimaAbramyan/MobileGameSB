using System;
using UnityEditor;
using UnityEngine;

public sealed partial class DirectedWaveAttackBehaviourEditor
{
    private string boundGroupId;
    private int boundGroupRevision = int.MinValue;
    private string boundGroupPath;
    private int copyFromGroup = 1;
    private readonly System.Collections.Generic.Dictionary<int, string> sceneSourcePaths = new();
    private int sceneSourceDirtyCount = -1;
    private bool EditingAttackGroup => ((DirectedWaveAttackBehaviour)target).UseAttackGroups;
    private string GroupSelectionKey => "WaveAttack.ActiveGroup." + target.GetInstanceID();
    private string SelectedGroupId
    {
        get
        {
            var groups = ((DirectedWaveAttackBehaviour)target).AttackGroups;
            string id = SessionState.GetString(GroupSelectionKey, "");
            foreach (var group in groups) if (group.Id == id) return id;
            return groups.Count > 0 ? groups[0].Id : "";
        }
        set => SessionState.SetString(GroupSelectionKey, value);
    }
    private void BindAttackGroupProperties()
    {
        string id = EditingAttackGroup ? SelectedGroupId : "";
        int revision = ((DirectedWaveAttackBehaviour)target).ConfigurationRevision;
        if (boundGroupId == id && boundGroupRevision == revision) return;
        sceneSourcePaths.Clear();
        // Resolve SerializedProperty paths only. No configuration copies or materialization.
        string path = "";
        if (EditingAttackGroup)
        {
            var groups = serializedObject.FindProperty("attackGroups");
            for (int i = 0; i < groups.arraySize; i++)
                if (groups.GetArrayElementAtIndex(i).FindPropertyRelative("id").stringValue == id)
                { path = groups.GetArrayElementAtIndex(i).propertyPath; break; }
        }
        SerializedProperty Find(string field) => serializedObject.FindProperty(string.IsNullOrEmpty(path) ? field : path + "." + field);
        var single = string.IsNullOrEmpty(path) ? null : Find("single");
        SerializedProperty Pattern(string field) => single != null ? single.FindPropertyRelative(field) : Find(field);
        attackSettings = Pattern("attackSettings"); attackPreset = Pattern("attackPreset");
        overridePresetAttacksPerSecond = Pattern("overridePresetAttacksPerSecond"); presetAttacksPerSecond = Pattern("presetAttacksPerSecond");
        overrideBaseProjectileSpeed = Pattern("overrideBaseProjectileSpeed"); baseProjectileSpeed = Pattern("baseProjectileSpeed");
        attackPatterns = Find(string.IsNullOrEmpty(path) ? "attackPatterns" : "patterns");
        executionMode = Find("executionMode"); attackSequence = Find(string.IsNullOrEmpty(path) ? "attackSequence" : "sequence");
        boundGroupId = id;
        boundGroupRevision = revision;
        boundGroupPath = path;
    }
    private void GroupEdit(string name, Action<DirectedWaveAttackBehaviour> edit)
    {
        serializedObject.ApplyModifiedProperties();
        var behaviour = (DirectedWaveAttackBehaviour)target;
        Undo.RecordObject(behaviour, name); edit(behaviour); EditorUtility.SetDirty(behaviour);
        if (PrefabUtility.IsPartOfPrefabInstance(behaviour)) PrefabUtility.RecordPrefabInstancePropertyModifications(behaviour);
        serializedObject.Update(); BindAttackGroupProperties(); SceneView.RepaintAll(); Repaint(); GUIUtility.ExitGUI();
    }
    private void DrawAttackGroupsPanel()
    {
        var behaviour = (DirectedWaveAttackBehaviour)target;
        var flag = serializedObject.FindProperty("useAttackGroups");
        var toggleRect = EditorGUILayout.GetControlRect();
        var toggleLabel = new GUIContent("Use Attack Groups", "Assign each formation slot to an independent shooting configuration. Attack Timing & Order stays shared.");
        bool enabled, changed;
        using (new EditorGUI.PropertyScope(toggleRect, toggleLabel, flag))
        {
            EditorGUI.BeginChangeCheck();
            enabled = EditorGUI.Toggle(toggleRect, toggleLabel, behaviour.UseAttackGroups);
            changed = EditorGUI.EndChangeCheck();
        }
        if (changed) GroupEdit("Toggle Attack Groups", b => b.SetUseAttackGroups(enabled));
        if (!behaviour.UseAttackGroups) return;
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("Attack Groups", EditorStyles.boldLabel);
        var groups = behaviour.AttackGroups;
        string active = SelectedGroupId;
        for (int i = 0; i < groups.Count; i++)
        {
            string id = groups[i].Id;
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Toggle(active == id, "Group " + (i + 1), EditorStyles.miniButton) && active != id)
                { serializedObject.ApplyModifiedProperties(); SelectedGroupId = id; Repaint(); SceneView.RepaintAll(); GUIUtility.ExitGUI(); }
                using (new EditorGUI.DisabledScope(groups.Count == 1))
                    if (GUILayout.Button("×", GUILayout.Width(26))) GroupEdit("Remove Attack Group", b => b.RemoveAttackGroup(id));
            }
        }
        if (GUILayout.Button("Create Group")) GroupEdit("Create Attack Group", b => SelectedGroupId = b.CreateAttackGroup(active).Id);
        using (new EditorGUILayout.HorizontalScope())
        {
            copyFromGroup = EditorGUILayout.IntField("Source Group", copyFromGroup);
            using (new EditorGUI.DisabledScope(copyFromGroup < 1 || copyFromGroup > groups.Count || groups[copyFromGroup - 1].Id == active))
                if (GUILayout.Button("Copy From Group")) GroupEdit("Copy Attack Group", b => b.CopyAttackGroup(active, groups[copyFromGroup - 1].Id));
        }
        EditorGUILayout.EndVertical();
        var wave = GetWave();
        if (wave == null) return;
        int count = wave.GetConfiguredEnemySlotCount();
        DrawGroupMembers("Unassigned Enemies", false);
        DrawGroupMembers("Selected Group Enemies", true);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Assign All Unassigned")) GroupEdit("Assign Unassigned Enemies", b => {
                for (int i = 0; i < count; i++) if (b.GetAttackGroupForSlot(i) == null) b.AssignAttackGroup(i, active);
            });
            using (new EditorGUI.DisabledScope(selectedLocalOverrideSlot < 0 || selectedLocalOverrideSlot >= count))
                if (GUILayout.Button("Assign Selected Scene Slot")) GroupEdit("Assign Selected Enemy", b => b.AssignAttackGroup(selectedLocalOverrideSlot, active));
        }
        void DrawGroupMembers(string title, bool assigned)
        {
            using var section = new WaveInspectorSections.Scope(serializedObject, active + title, title);
            if (!section.IsExpanded) return;
            bool any = false; int column = 0;
            for (int i = 0; i < count; i++)
            {
                var group = behaviour.GetAttackGroupForSlot(i);
                if (assigned ? group?.Id != active : group != null) continue;
                if (column == 0) EditorGUILayout.BeginHorizontal();
                int slot = i; any = true;
                if (GUILayout.Button((assigned ? "− " : "+ ") + "Slot " + i, EditorStyles.miniButton))
                    GroupEdit("Assign Attack Group Slot", b => b.AssignAttackGroup(slot, assigned ? null : active));
                column++;
                if (column == 4) { EditorGUILayout.EndHorizontal(); column = 0; }
            }
            if (column != 0) EditorGUILayout.EndHorizontal();
            if (!any) EditorGUILayout.LabelField("None", EditorStyles.miniLabel);
        }
    }
    private void DrawSharedAttackTiming(SerializedProperty settings)
    {
        if (EditingAttackGroup)
        {
            var preset = serializedObject.FindProperty("attackPreset");
            EditorGUILayout.PropertyField(preset, new GUIContent("Shared Timing Preset"));
            if (preset.objectReferenceValue != null)
            {
                EditorGUILayout.HelpBox("Shared timing and movement are inherited from this preset. Group shooting uses its own configuration.", MessageType.None);
                var rate = serializedObject.FindProperty("overridePresetAttacksPerSecond");
                EditorGUILayout.PropertyField(rate);
                if (rate.boolValue) EditorGUILayout.PropertyField(serializedObject.FindProperty("presetAttacksPerSecond"));
                return;
            }
        }
        var useStartDelay = settings.FindPropertyRelative("useAttackStartDelay");
        using (var timingSection = new WaveInspectorSections.Scope(serializedObject, settings.propertyPath + ".Timing", "Attack Timing & Order"))
        {
            if (timingSection.IsExpanded)
            {
                EditorGUILayout.PropertyField(
                    useStartDelay,
                    new GUIContent("Delay Attack Start"));
                if (useStartDelay.boolValue)
                {
                    EditorGUILayout.PropertyField(
                        settings.FindPropertyRelative("attackStartDelay"),
                        new GUIContent("Attack Start Delay"));
                }

                EditorGUILayout.Space(3f);
                EditorGUILayout.PropertyField(
                    settings.FindPropertyRelative("attacksPerEnemyPerCycle"),
                    new GUIContent("Attacks Per Enemy Per Cycle"));
                var order = settings.FindPropertyRelative("attackOrder");
                EditorGUILayout.PropertyField(order, new GUIContent("Attack Order"), true);
                if (order.FindPropertyRelative("enabled").boolValue)
                    EditorGUILayout.HelpBox("Attack Order controls group dispatch interval, batches and waiting. The legacy scheduling fields below are inactive.", MessageType.None);
                using (new EditorGUI.DisabledScope(order.FindPropertyRelative("enabled").boolValue))
                {
                EditorGUILayout.PropertyField(
                    settings.FindPropertyRelative("attacksPerSecond"),
                    new GUIContent("Attacks Per Second"));

                SerializedProperty waitForPreviousAttack = settings.FindPropertyRelative(
                    "waitForPreviousAttack");
                EditorGUILayout.PropertyField(
                    waitForPreviousAttack,
                    new GUIContent("Wait For Previous Attack"));
                if (waitForPreviousAttack.boolValue)
                {
                    EditorGUILayout.PropertyField(
                        settings.FindPropertyRelative("delayAfterAttack"),
                        new GUIContent("Delay After Attack"));
                }
                }

            }
        }

    }
    private void DrawAttackGroupMarkers(DirectedEnemySubWave wave)
    {
        var behaviour = (DirectedWaveAttackBehaviour)target;
        string active = SelectedGroupId;
        Color previous = Handles.color;
        for (int i = 0; i < wave.GetConfiguredEnemySlotCount(); i++)
        {
            var group = behaviour.GetAttackGroupForSlot(i);
            if (group == null || group.Id != active) continue;
            Handles.color = new Color(.2f, 1f, .5f, .9f);
            var position = wave.GetConfiguredFormationSlotPosition(i);
            Handles.DrawWireDisc(position, Vector3.forward, .25f);
        }
        Handles.color = previous;
    }
    // Configuration paths only; poses and attack clocks remain in the existing preview.
    private string GetGroupSceneSourcePath(DirectedWaveAttackBehaviour behaviour, int slot)
    {
        int dirtyCount = EditorUtility.GetDirtyCount(behaviour);
        if (sceneSourceDirtyCount != dirtyCount) { sceneSourcePaths.Clear(); sceneSourceDirtyCount = dirtyCount; }
        var assigned = behaviour.GetAttackGroupForSlot(slot);
        if (assigned == null || assigned.Id != boundGroupId) return null;
        int key = behaviour.GroupPreviewCacheKey(slot);
        if (sceneSourcePaths.TryGetValue(key, out var cached)) return cached;
        string path = ResolveGroupSceneSourcePath(behaviour, slot);
        sceneSourcePaths.Add(key, path);
        return path;
    }
    private string ResolveGroupSceneSourcePath(DirectedWaveAttackBehaviour behaviour, int slot)
    {
        var group = behaviour.GetAttackGroupForSlot(slot);
        if (group == null || group.Id != boundGroupId) return null;
        if (group.UsesSequence) return boundGroupPath + ".sequence";
        if (!SceneAttackSettingsCache.TryGet(behaviour, slot, false, out _, out _)) return null;
        if (group.Patterns.Count == 0) return boundGroupPath + ".single";
        for (int p = 0; p < group.Patterns.Count; p++)
        {
            var slots = group.Patterns[p]?.SelectedEnemySlots;
            if (slots == null) continue;
            for (int i = 0; i < slots.Count; i++)
                if (slots[i] == slot) return boundGroupPath + ".patterns.Array.data[" + p + "]";
        }
        return null;
    }
    private void DrawSharedGroupMovement(SerializedProperty movementSettings)
    {
        var movementMode = movementSettings.FindPropertyRelative("movementMode");
        EditorGUILayout.PropertyField(movementMode, new GUIContent("Shared Movement Mode"));
        DrawDiveMovementSettings(movementSettings);
    }
    private void DrawDiveMovementSettings(SerializedProperty movementSettings)
    {
        var movementMode = movementSettings.FindPropertyRelative("movementMode");
        DirectedWaveAttackMovementMode selectedMovementMode =
            (DirectedWaveAttackMovementMode)movementMode.enumValueIndex;
        if (selectedMovementMode == DirectedWaveAttackMovementMode.None)
        {
            return;
        }

        using (var dive = new WaveInspectorSections.Scope(serializedObject, movementSettings.propertyPath + ".Dive", "Dive & Return"))
        {
            if (!dive.IsExpanded) return;
            EditorGUILayout.Space(3f);
            EditorGUILayout.LabelField(
                "Dive Preparation",
                EditorStyles.miniBoldLabel);
            SerializedProperty useDivePreparation = movementSettings.FindPropertyRelative(
                "useDivePreparation");
            EditorGUILayout.PropertyField(
                useDivePreparation,
                new GUIContent("Use Preparation"));
            if (useDivePreparation.boolValue)
            {
                EditorGUILayout.PropertyField(
                    movementSettings.FindPropertyRelative("divePreparationDistance"),
                    new GUIContent("Preparation Distance"));
                EditorGUILayout.PropertyField(
                    movementSettings.FindPropertyRelative("divePreparationDuration"),
                    new GUIContent("Preparation Duration"));
                EditorGUILayout.PropertyField(
                    movementSettings.FindPropertyRelative("divePreparationSpeedCurve"),
                    new GUIContent("Preparation Speed Curve"));
            }

            EditorGUILayout.Space(3f);
            if (selectedMovementMode
                == DirectedWaveAttackMovementMode.FlyThroughDive)
            {
                EditorGUILayout.LabelField(
                    "Fly Through Dive",
                    EditorStyles.miniBoldLabel);
                EditorGUILayout.PropertyField(
                    movementSettings.FindPropertyRelative("flyThroughApproachSpeed"),
                    new GUIContent("Approach Speed"));
                EditorGUILayout.PropertyField(
                    movementSettings.FindPropertyRelative("flyThroughExitPadding"),
                    new GUIContent("Exit Padding"));
                SerializedProperty returnMode = movementSettings.FindPropertyRelative(
                    "flyThroughReturnMode");
                EditorGUILayout.PropertyField(
                    returnMode,
                    new GUIContent("Return Mode"));
                if ((DirectedWaveFlyThroughReturnMode)returnMode.enumValueIndex
                    == DirectedWaveFlyThroughReturnMode.TeleportPosition)
                {
                    EditorGUILayout.PropertyField(
                        movementSettings.FindPropertyRelative(
                            "flyThroughReturnTeleportPosition"),
                        new GUIContent("Return Teleport Position"));
                }
                EditorGUILayout.PropertyField(
                    movementSettings.FindPropertyRelative("flyThroughDiveCooldown"),
                    new GUIContent("Dive Cooldown"));
                EditorGUILayout.PropertyField(
                    movementSettings.FindPropertyRelative("diveSpeedCurve"),
                    new GUIContent("Speed Curve"));
                EditorGUILayout.HelpBox(
                    "Entrance Path teleports the enemy to the start of its entrance route and replays that route using Return Speed Multiplier. Dive Cooldown begins after the full sequence.",
                    MessageType.None);
            }
            else
            {
                EditorGUILayout.LabelField("Dive", EditorStyles.miniBoldLabel);
                EditorGUILayout.PropertyField(movementSettings.FindPropertyRelative("minDiveDepth"));
                EditorGUILayout.PropertyField(movementSettings.FindPropertyRelative("maxDiveDepth"));
                EditorGUILayout.PropertyField(movementSettings.FindPropertyRelative("minDiveSpeed"));
                EditorGUILayout.PropertyField(movementSettings.FindPropertyRelative("maxDiveSpeed"));
                EditorGUILayout.PropertyField(movementSettings.FindPropertyRelative("minimumDiveDuration"), new GUIContent("Minimum Dive Duration"));
                EditorGUILayout.PropertyField(movementSettings.FindPropertyRelative("maximumDiveSpeed"), new GUIContent("Maximum Dive Speed"));
                EditorGUILayout.PropertyField(movementSettings.FindPropertyRelative("diveSpeedCurve"));
                EditorGUILayout.HelpBox("Minimum Dive Duration and Maximum Dive Speed limit only the approach to the player. Zero disables each limit; the Min/Max Dive Speed range still selects the nominal speed.", MessageType.None);

                EditorGUILayout.Space(3f);
                EditorGUILayout.LabelField("Dive Target", EditorStyles.miniBoldLabel);
                SerializedProperty diveTargetMode = movementSettings.FindPropertyRelative(
                    "diveTargetMode");
                EditorGUILayout.PropertyField(diveTargetMode);
                if ((DirectedWaveDiveTargetMode)diveTargetMode.enumValueIndex
                    == DirectedWaveDiveTargetMode.StopAtPlayerRadius)
                {
                    EditorGUILayout.PropertyField(
                        movementSettings.FindPropertyRelative("playerStandoffRadius"));
                }
            }

            EditorGUILayout.Space(3f);
            EditorGUILayout.LabelField("Dive Scheduling", EditorStyles.miniBoldLabel);
            EditorGUILayout.PropertyField(
                movementSettings.FindPropertyRelative("diveSchedulingMode"));

            EditorGUILayout.Space(3f);
            EditorGUILayout.LabelField("Return", EditorStyles.miniBoldLabel);
            EditorGUILayout.PropertyField(
                movementSettings.FindPropertyRelative("returnSpeedMultiplier"));
            EditorGUILayout.PropertyField(
                movementSettings.FindPropertyRelative("returnSpeedCurve"));
        }
    }
}
