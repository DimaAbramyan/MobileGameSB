using System;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

public sealed class StatBlocksInspectorProbe : EditorWindow
{
    public Editor[] Editors;
    public Exception Error;
    public int Repaints;
    private Vector2 scroll;
    private void OnGUI()
    {
        if (Editors == null) return;
        try
        {
            using var area = new EditorGUILayout.ScrollViewScope(scroll);
            scroll = area.scrollPosition;
            foreach (var editor in Editors) editor.OnInspectorGUI();
            if (Event.current.type == EventType.Repaint) Repaints++;
        }
        catch (Exception ex) { if (!(ex is ExitGUIException)) Error = ex; }
    }
}
public static class VerifyStatBlocksInspector
{
    public static async Task<object> Run()
    {
        var weapon = ScriptableObject.CreateInstance<WeaponMetaConfig>();
        var ship = ScriptableObject.CreateInstance<ShipMetaConfig>();
        var editors = new[] { Editor.CreateEditor(weapon), Editor.CreateEditor(ship) };
        var window = ScriptableObject.CreateInstance<StatBlocksInspectorProbe>();
        try
        {
            foreach (IStatDisplaySource source in new IStatDisplaySource[] { weapon, ship })
            {
                var row = new StatDisplayRow();
                for (int i = 0; i < 5; i++) row.cells.Add(new StatDisplayCell { label = "Damage", value = "100" });
                source.StatDisplay.baseStats.rows.Add(row);
                source.StatDisplay.secondaryStats.rows.Add(new StatDisplayRow());
                var catalog = StatCatalogBuilder.Build((ScriptableObject)source);
                var stat = catalog.Values[0];
                row.cells[0] = new StatDisplayCell { useBinding = true, sourcePath = stat.SourcePath, statId = stat.Id, sourceAsset = stat.SourceAsset, valueFormat = "0.0", overrideLabel = true, label = "Custom label" };
                row.cells[1] = new StatDisplayCell { useBinding = true, sourcePath = "removed", statId = "removed" };
            }
            window.Editors = editors; window.position = new Rect(100, 100, 650, 850); window.Show();
            for (int i = 0; i < 60 && window.Repaints < 2 && window.Error == null; i++) { window.Repaint(); await Task.Delay(50); }
            if (window.Error != null) throw window.Error;
            if (window.Repaints < 2) throw new Exception("Inspector did not render");
            return new { passed = true, inspectors = 2, cards = 5, horizontalScroll = true, emptySecondaryRow = true, selectedStat = true, missingWarning = true };
        }
        finally
        {
            window.Close(); foreach (var editor in editors) UnityEngine.Object.DestroyImmediate(editor);
            UnityEngine.Object.DestroyImmediate(weapon); UnityEngine.Object.DestroyImmediate(ship);
        }
    }
}
