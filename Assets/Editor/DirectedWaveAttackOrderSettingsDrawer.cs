using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(DirectedWaveAttackOrderSettings))]
public sealed class DirectedWaveAttackOrderSettingsDrawer : PropertyDrawer
{
    private static readonly string[] Fields = { "mode", "attackIntervalBetweenEnemies", "enemiesPerBatch", "startSlot",
        "reverseOrder", "loopOrder", "randomSeed", "orderSource", "waitForPreviousEnemy", "resetOrderEachCycle", "manualSlots" };
    private static bool Visible(string field, int mode) => (field != "randomSeed" || mode == (int)DirectedWaveAttackOrderMode.Random)
        && (field != "manualSlots" || mode == (int)DirectedWaveAttackOrderMode.Manual)
        && (field != "enemiesPerBatch" || mode != (int)DirectedWaveAttackOrderMode.Simultaneous);
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        float height = EditorGUIUtility.singleLineHeight;
        if (!property.FindPropertyRelative("enabled").boolValue) return height;
        int mode = property.FindPropertyRelative("mode").enumValueIndex;
        foreach (string field in Fields)
            if (Visible(field, mode)) height += EditorGUI.GetPropertyHeight(property.FindPropertyRelative(field), true) + 2f;
        if (mode == (int)DirectedWaveAttackOrderMode.Manual && property.FindPropertyRelative("manualSlots").arraySize == 0) height += 40f;
        return height;
    }
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);
        var row = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
        var enabled = property.FindPropertyRelative("enabled");
        EditorGUI.PropertyField(row, enabled, new GUIContent("Use Attack Order", "Independent of Spawn Order. Disabled preserves legacy scheduling."));
        if (enabled.boolValue)
        {
            EditorGUI.indentLevel++;
            int mode = property.FindPropertyRelative("mode").enumValueIndex;
            foreach (string field in Fields)
            {
                if (!Visible(field, mode)) continue;
                var value = property.FindPropertyRelative(field);
                row.y += row.height + 2f; row.height = EditorGUI.GetPropertyHeight(value, true);
                var fieldLabel = field == "orderSource" ? new GUIContent("Order Source", "Formation Slot Order uses stable IDs/formation geometry. Spatial Order uses current world positions, captured at cycle start.")
                    : field == "waitForPreviousEnemy" ? new GUIContent("Wait For Previous Enemy", "Wait for all enemies of the previous batch, including Burst, Sequential Sector, return and Attack Cooldown. Members of one batch may attack together.")
                    : field == "resetOrderEachCycle" ? new GUIContent("Reset Order Each Cycle", "Re-capture geometry and reset the seed each cycle. Disabled retains the first geometry snapshot and continues the random stream.")
                    : field == "loopOrder" ? new GUIContent("Loop Order", "Repeat order cycles. Disabled runs one cycle. Attack Sequence steps always run one finite cycle per repetition.")
                    : field == "manualSlots" ? new GUIContent("Manual Slots", "Formation slot IDs in firing order, independent of spawn order. Only listed slots attack; duplicates are allowed.")
                    : new GUIContent(ObjectNames.NicifyVariableName(field));
                EditorGUI.PropertyField(row, value, fieldLabel, true);
            }
            if (mode == (int)DirectedWaveAttackOrderMode.Manual && property.FindPropertyRelative("manualSlots").arraySize == 0)
                EditorGUI.HelpBox(new Rect(position.x, row.yMax + 2f, position.width, 36f), "Manual order is empty: this group will not attack.", MessageType.Warning);
            EditorGUI.indentLevel--;
        }
        EditorGUI.EndProperty();
    }
}

internal sealed class DirectedWaveAttackOrderScenePreview
{
    private sealed class Group
    {
        public readonly DirectedWaveAttackSettings Settings = new();
        public readonly List<DirectedWaveAttackOrderSlot> Slots = new();
        public readonly DirectedWaveAttackOrderPlan Plan = new();
    }
    private readonly Dictionary<int, Group> groups = new();
    private readonly Dictionary<int, Vector3> positions = new();
    private readonly DirectedWaveAttackSettings resolved = new();

    public void Draw(DirectedEnemySubWave wave, DirectedWaveAttackBehaviour behaviour)
    {
        if (Event.current.type != EventType.Repaint || !behaviour.enabled) return;
        foreach (var group in groups.Values) group.Slots.Clear();
        positions.Clear();
        for (int slot = 0; slot < wave.GetConfiguredEnemySlotCount(); slot++)
        {
            if (!behaviour.TryCopyResolvedAttackSettingsTo(slot, false, resolved, out int index) || !resolved.UsesAttackOrder) continue;
            if (!groups.TryGetValue(index, out var group)) { group = new Group(); groups.Add(index, group); }
            if (group.Slots.Count == 0) group.Settings.CopyFrom(resolved);
            Vector3 position = wave.GetConfiguredFormationSlotPosition(slot);
            positions[slot] = position; group.Slots.Add(new DirectedWaveAttackOrderSlot(slot, position));
        }
        foreach (var group in groups.Values)
        {
            if (group.Slots.Count == 0) continue;
            group.Plan.Reset(); group.Plan.BuildCycle(group.Slots, group.Settings.AttackOrder, 1);
            DrawPlan(group.Plan, positions, -1);
        }
    }

    public static void DrawPlan(DirectedWaveAttackOrderPlan plan, Dictionary<int, Vector3> positions, int cursor)
    {
        Color previous = Handles.color;
        Vector3? last = null;
        for (int i = 0; i < plan.Entries.Count; i++)
        {
            var entry = plan.Entries[i];
            if (!positions.TryGetValue(entry.Slot, out var point)) continue;
            Handles.color = i == cursor ? Color.yellow : new Color(.3f, 1f, .65f, .8f);
            if (last.HasValue) Handles.DrawDottedLine(last.Value, point, 4f);
            Handles.DrawWireDisc(point, Vector3.forward, .18f);
            Handles.Label(point + Vector3.down * (.2f + .12f * (i / Mathf.Max(1, positions.Count))),
                $"Attack {i + 1} · Batch {entry.Batch + 1} · Slot {entry.Slot}");
            last = point;
        }
        Handles.color = previous;
    }
}
