using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class NumericScrubProbeWindow : EditorWindow
{
    public SerializedObject Data;
    public Exception Error;
    public int Repaints;
    public Vector2 Probe;
    public bool Probed;
    public static readonly string[] Paths = { "projectile.projectileLateralSpeed", "projectile.fanProjectileCount",
        "projectile.projectileOrbitFixedWorldCenter", "projectile.numericRandomSeed", "projectile.composite.angularSpeed",
        "projectile.projectileAngularSpeed", "projectile.shotGunProjectileCount" };
    public static readonly float[] Rows = { 30, 65, 100, 175, 210, 250, 320 };
    private void OnGUI()
    {
        if (Data == null) return;
        try
        {
            if (Event.current.type == EventType.MouseMove) { Probe = Event.current.mousePosition; Probed = true; }
            Data.Update(); float saved = EditorGUIUtility.labelWidth; EditorGUIUtility.labelWidth = 180;
            try
            {
                for (int i = 0; i < Paths.Length; i++)
                {
                    var field = Data.FindProperty(Paths[i]);
                    EditorGUI.PropertyField(new Rect(15, Rows[i], 570, EditorGUI.GetPropertyHeight(field)), field, new GUIContent("Probe " + i), true);
                }
            }
            finally { EditorGUIUtility.labelWidth = saved; }
            Data.ApplyModifiedProperties();
            if (Event.current.type == EventType.Repaint) Repaints++;
        }
        catch (Exception error) { Error = error; }
    }
}

public static class VerifyNumericScrubbing
{
    private static int checks;
    private static void Check(bool pass, string message) { checks++; if (!pass) throw new Exception(message); }
    public static Task<object> Reproduce() => Run(false);
    public static Task<object> Verify() => Run(true);
    private static async Task<object> Run(bool expectFixed)
    {
        checks = 0;
        var preset = ScriptableObject.CreateInstance<EnemyProjectilePreset>(); preset.Settings.Validate();
        using var data = new SerializedObject(preset);
        var window = ScriptableObject.CreateInstance<NumericScrubProbeWindow>();
        try
        {
            data.FindProperty(NumericScrubProbeWindow.Paths[0]).floatValue = -1.25f;
            data.FindProperty(NumericScrubProbeWindow.Paths[1]).intValue = 7;
            data.FindProperty(NumericScrubProbeWindow.Paths[2]).vector2Value = new Vector2(-2.25f, 1.5f);
            data.FindProperty(NumericScrubProbeWindow.Paths[3]).intValue = -8;
            data.FindProperty(NumericScrubProbeWindow.Paths[4]).floatValue = -1.25f;
            Configure(data, "projectile.projectileAngularSpeedRandom", -3.5f, 4.25f, false);
            Configure(data, "projectile.shotGunProjectileCountRandom", 2, 8, true);
            data.ApplyModifiedPropertiesWithoutUndo();
            window.wantsMouseMove = true; window.Data = data; window.position = new Rect(180, 180, 620, 430); window.ShowUtility(); window.Focus();
            await Paint(window);
            Vector2 probe = new(300, 390); window.SendEvent(new Event { type = EventType.MouseMove, mousePosition = probe });
            await Task.Delay(30); Check(window.Probed, "Mouse probe reached IMGUI"); Vector2 offset = probe - window.Probe;
            double Read(string path) { data.Update(); var field = data.FindProperty(path); return field.propertyType == SerializedPropertyType.Integer ? field.intValue : field.floatValue; }
            async Task<bool> DragCheck(string path, Vector2 label, float delta)
            {
                double before = Read(path); await Drag(window, label + offset, delta); double after = Read(path);
                return delta > 0 ? after > before : after < before;
            }
            bool normalFloat = await DragCheck(NumericScrubProbeWindow.Paths[4], new Vector2(65, 218), 70);
            bool normalInt = await DragCheck(NumericScrubProbeWindow.Paths[3], new Vector2(65, 183), -70);
            Check(normalFloat && normalInt, "Native fields without randomization support scrubbing");
            bool rangeFloat = await DragCheck("projectile.projectileAngularSpeedRandom.min", new Vector2(207, 258), 70);
            bool rangeMax = await DragCheck("projectile.projectileAngularSpeedRandom.max", new Vector2(406, 258), -70);
            bool rangeInt = await DragCheck("projectile.shotGunProjectileCountRandom.max", new Vector2(406, 328), 70);
            Check(rangeFloat && rangeMax && rangeInt, "Float Min/Max and integer range still scrub");
            string fixedPath = NumericScrubProbeWindow.Paths[0]; double original = Read(fixedPath);
            Undo.IncrementCurrentGroup(); int group = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("Transient numeric scrubbing check");
            bool fixedFloat = await DragCheck(fixedPath, new Vector2(65, 38), 70);
            double changed = Read(fixedPath);
            Undo.FlushUndoRecordObjects(); Undo.CollapseUndoOperations(group);
            bool undoRedo = false;
            if (fixedFloat)
            {
                Undo.PerformUndo(); bool undo = Math.Abs(Read(fixedPath) - original) < .00001;
                Undo.PerformRedo(); undoRedo = undo && Math.Abs(Read(fixedPath) - changed) < .00001;
            }
            bool fixedInt = await DragCheck(NumericScrubProbeWindow.Paths[1], new Vector2(65, 73), 70);
            bool vector = await DragCheck(NumericScrubProbeWindow.Paths[2] + ".x", new Vector2(65, 108), -70);
            if (expectFixed)
            {
                Check(fixedFloat && fixedInt && vector, "Fixed float, int and vector X labels scrub in both directions");
                Check(undoRedo, "Undo and Redo restore a label drag exactly");
                await Click(window, new Vector2(260, 38) + offset);
                window.SendEvent(new Event { type = EventType.KeyDown, keyCode = KeyCode.A, modifiers = EventModifiers.Control });
                foreach (char letter in "-2.75") window.SendEvent(new Event { type = EventType.KeyDown, character = letter });
                window.SendEvent(new Event { type = EventType.KeyDown, keyCode = KeyCode.Return, character = '\n' }); await Paint(window);
                Check(Math.Abs(Read(fixedPath) + 2.75) < .00001, "Keyboard input accepts a negative fractional value");
                bool decrease = await DragCheck(fixedPath, new Vector2(65, 38), -70);
                Check(decrease, "Dragging left decreases the fixed float");
                double savedMin = Read("projectile.projectileAngularSpeedRandom.min"), savedMax = Read("projectile.projectileAngularSpeedRandom.max");
                double savedFixed = Read("projectile.projectileAngularSpeed");
                await Click(window, new Vector2(573, 258) + offset);
                data.Update();
                Check(!data.FindProperty("projectile.projectileAngularSpeedRandom.enabled").boolValue
                    && Read("projectile.projectileAngularSpeedRandom.min") == savedMin
                    && Read("projectile.projectileAngularSpeedRandom.max") == savedMax
                    && Read("projectile.projectileAngularSpeed") == savedFixed, "Random button remains independent and preserves both fixed and ranged values");
            }
            else Check(!fixedFloat && !fixedInt && !vector, "Original defect reproduced only on fixed randomizable labels");
            if (window.Error != null) throw window.Error;
            return new { passed = true, checks, expectFixed, fixedFloat, fixedInt, vector, rangeFloat, rangeMax, rangeInt, normalFloat, normalInt, undoRedo };
        }
        finally
        {
            window.Data = null; window.Close(); Object.DestroyImmediate(window);
            Undo.ClearUndo(preset); Object.DestroyImmediate(preset);
        }
    }
    private static void Configure(SerializedObject data, string path, float min, float max, bool integer)
    {
        var range = data.FindProperty(path); range.FindPropertyRelative("enabled").boolValue = true;
        range.FindPropertyRelative("initialized").boolValue = true;
        if (integer) { range.FindPropertyRelative("min").intValue = (int)min; range.FindPropertyRelative("max").intValue = (int)max; }
        else { range.FindPropertyRelative("min").floatValue = min; range.FindPropertyRelative("max").floatValue = max; }
    }
    private static async Task Paint(NumericScrubProbeWindow window)
    {
        int frame = window.Repaints; var watch = System.Diagnostics.Stopwatch.StartNew();
        while (window.Repaints == frame && watch.ElapsedMilliseconds < 3000) { window.Repaint(); await Task.Delay(30); }
        if (window.Error != null) throw window.Error;
        if (window.Repaints == frame) throw new Exception("IMGUI repaint timeout");
    }
    private static async Task Click(NumericScrubProbeWindow window, Vector2 position)
    {
        window.SendEvent(new Event { type = EventType.MouseDown, button = 0, mousePosition = position });
        window.SendEvent(new Event { type = EventType.MouseUp, button = 0, mousePosition = position }); await Paint(window);
    }
    private static async Task Drag(NumericScrubProbeWindow window, Vector2 position, float delta)
    {
        window.SendEvent(new Event { type = EventType.MouseDown, button = 0, mousePosition = position });
        for (int i = 1; i <= 4; i++) window.SendEvent(new Event { type = EventType.MouseDrag, button = 0,
            mousePosition = position + Vector2.right * (delta * i / 4), delta = Vector2.right * (delta / 4) });
        window.SendEvent(new Event { type = EventType.MouseUp, button = 0, mousePosition = position + Vector2.right * delta });
        await Paint(window);
    }
}
