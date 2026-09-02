using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(Magnite))]
public sealed class MagniteEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
    }

    private void OnSceneGUI()
    {
        Magnite magnet = (Magnite)target;
        if (magnet == null || !magnet.enabled)
            return;

        Vector3 center = magnet.GetMagnetCenter();
        float absorptionRadius = magnet.MagnetRadius;
        float attractionRadius = magnet.AttractionRadius;
        Color previousColor = Handles.color;
        Handles.color = new Color(1f, 0.75f, 0.2f, 0.85f);
        Handles.DrawWireDisc(center, Vector3.forward, attractionRadius);
        Handles.Label(
            center + Vector3.up * (attractionRadius + 0.1f),
            $"Attraction: {attractionRadius:0.##}");
        Handles.color = new Color(0.2f, 0.8f, 1f, 0.85f);
        Handles.DrawWireDisc(center, Vector3.forward, absorptionRadius);
        Handles.Label(
            center + Vector3.up * (absorptionRadius + 0.1f),
            $"Absorption: {absorptionRadius:0.##}");
        Handles.color = previousColor;
    }
}
