using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(DirectedWaveAttackOrderSettings))]
public sealed class DirectedWaveAttackOrderSettingsDrawer : PropertyDrawer
{
    private static IEnumerable<string> Fields(SerializedProperty property)
    {
        yield return "#Order & Slots";
        yield return "mode"; yield return "orderSource"; yield return "startSlot"; yield return "reverseOrder";
        if (property.FindPropertyRelative("mode").enumValueIndex == (int)DirectedWaveAttackOrderMode.Manual) yield return "manualSlots";
        yield return "#Batch Timing";
        yield return "attackIntervalBetweenEnemies";
        if (property.FindPropertyRelative("mode").enumValueIndex != (int)DirectedWaveAttackOrderMode.Simultaneous) yield return "enemiesPerBatch";
        yield return "waitForPreviousEnemy";
        yield return "#Cycles & Randomization";
        yield return "loopOrder"; yield return "resetOrderEachCycle";
        if (property.FindPropertyRelative("mode").enumValueIndex == (int)DirectedWaveAttackOrderMode.Random) yield return "randomSeed";
    }
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        float height = EditorGUIUtility.singleLineHeight;
        if (!property.FindPropertyRelative("enabled").boolValue) return height;
        int mode = property.FindPropertyRelative("mode").enumValueIndex;
        height += WaveInspectorSections.Height(property, Fields(property),
            field => EditorGUI.GetPropertyHeight(property.FindPropertyRelative(field), true));
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
            row.y += WaveInspectorSections.Line;
            WaveInspectorSections.Draw(ref row, property, Fields(property),
                field => EditorGUI.GetPropertyHeight(property.FindPropertyRelative(field), true),
                (fieldRect, field) =>
                {
                    var value = property.FindPropertyRelative(field);
                var fieldLabel = field == "orderSource" ? new GUIContent("Order Source", "Formation Slot Order uses stable IDs/formation geometry. Spatial Order uses current world positions, captured at cycle start.")
                    : field == "waitForPreviousEnemy" ? new GUIContent("Wait For Previous Enemy", "Wait for all enemies of the previous batch, including Burst, Sequential Sector, return and Attack Cooldown. Members of one batch may attack together.")
                    : field == "resetOrderEachCycle" ? new GUIContent("Reset Order Each Cycle", "Re-capture geometry and reset the seed each cycle. Disabled retains the first geometry snapshot and continues the random stream.")
                    : field == "loopOrder" ? new GUIContent("Loop Order", "Repeat order cycles. Disabled runs one cycle. Attack Sequence steps always run one finite cycle per repetition.")
                    : field == "manualSlots" ? new GUIContent("Manual Slots", "Formation slot IDs in firing order, independent of spawn order. Only listed slots attack; duplicates are allowed.")
                    : new GUIContent(ObjectNames.NicifyVariableName(field));
                    EditorGUI.PropertyField(fieldRect, value, fieldLabel, true);
                });
            if (mode == (int)DirectedWaveAttackOrderMode.Manual && property.FindPropertyRelative("manualSlots").arraySize == 0)
                EditorGUI.HelpBox(new Rect(position.x, row.y, position.width, 36f), "Manual order is empty: this group will not attack.", MessageType.Warning);
            EditorGUI.indentLevel--;
        }
        EditorGUI.EndProperty();
    }
}

internal sealed class DirectedWaveAttackOrderScenePreview
{
    private sealed class Group
    {
        public DirectedWaveAttackSettings Settings;
        public readonly List<DirectedWaveAttackOrderSlot> Slots = new();
        public readonly DirectedWaveAttackOrderPlan Plan = new();
    }
    private readonly Dictionary<int, Group> groups = new();
    private readonly Dictionary<int, Vector3> positions = new();

    public void Draw(DirectedEnemySubWave wave, DirectedWaveAttackBehaviour behaviour)
    {
        if (Event.current.type != EventType.Repaint || !behaviour.enabled) return;
        foreach (var group in groups.Values) group.Slots.Clear();
        positions.Clear();
        for (int slot = 0; slot < wave.GetConfiguredEnemySlotCount(); slot++)
        {
            if (!SceneAttackSettingsCache.TryGet(behaviour, slot, false, out var resolved, out int index) || !resolved.UsesAttackOrder) continue;
            if (!groups.TryGetValue(index, out var group)) { group = new Group(); groups.Add(index, group); }
            if (group.Slots.Count == 0) group.Settings = resolved;
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
