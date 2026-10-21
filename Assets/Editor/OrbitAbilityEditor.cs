using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(OrbitPassiveAbility))]
public sealed class OrbitPassiveAbilityEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        OrbitAbilityInspector.DrawMetaLink((Component)target);
    }
}

[CustomEditor(typeof(OrbitExpansionActiveAbility))]
public sealed class OrbitExpansionActiveAbilityEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        EditorGUILayout.PropertyField(serializedObject.FindProperty("orbit"),
            new GUIContent("Orbit Passive", "The owning hull's existing satellites."));
        serializedObject.ApplyModifiedProperties();
        OrbitAbilityInspector.DrawMetaLink((Component)target);
        if (((ActiveAbility)target).AbilityMode != UltimateAbilityMode.Toggle)
            EditorGUILayout.HelpBox("Orbit requires the Toggle ability mode on its prefab.", MessageType.Error);
    }
}

internal static class OrbitAbilityInspector
{
    public static void DrawMetaLink(Component component)
    {
        var ship = component.GetComponent<ParentShip>();
        var meta = ship != null && ship.ShipData != null ? ship.ShipData.ShipMetaConfig : null;
        EditorGUILayout.Space();
        using (new EditorGUI.DisabledScope(true))
            EditorGUILayout.ObjectField("Meta Configuration", meta, typeof(ShipMetaConfig), false);
        if (meta == null)
        {
            EditorGUILayout.HelpBox("Assign Orbit ShipData with its Meta configuration to ParentShip.", MessageType.Error);
            return;
        }
        EditorGUILayout.HelpBox("Orbit parameters and held-ability recovery are edited in the Meta configuration. Select this prefab in Scene View to see satellite hitboxes.", MessageType.Info);
        if (GUILayout.Button("Open Orbit Meta Configuration"))
            Selection.activeObject = meta;
    }
}
