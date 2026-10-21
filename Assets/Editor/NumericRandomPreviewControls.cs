using UnityEditor;
using UnityEngine;

public static class NumericRandomPreviewControls
{
    public static int Revision { get; private set; }
    public static int SeedOffset => SessionState.GetInt("Wave.NumericPreviewSeedOffset", 0);
    public static void Repeat() { Revision++; SceneView.RepaintAll(); }
    public static void SetSeedOffset(int value) { SessionState.SetInt("Wave.NumericPreviewSeedOffset", value); Repeat(); }
    public static void Draw()
    {
        EditorGUI.BeginChangeCheck();
        int seed = EditorGUILayout.IntField(new GUIContent("Preview Seed Offset", "0 uses the authored Runtime seeds. A nonzero offset is added to each pattern's Numeric Random Seed."), SeedOffset);
        if (EditorGUI.EndChangeCheck()) SetSeedOffset(seed);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Repeat Seed")) Repeat();
            if (GUILayout.Button("New Seed")) SetSeedOffset(System.Guid.NewGuid().GetHashCode());
            if (GUILayout.Button("Runtime Seed")) SetSeedOffset(0);
        }
    }
}
