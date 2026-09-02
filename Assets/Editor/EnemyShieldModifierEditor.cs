using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(EnemyShieldModifier))]
public sealed class EnemyShieldModifierEditor : Editor
{
    private bool showPreview;

    private void OnEnable()
    {
        SceneView.duringSceneGui += DrawPreview;
    }

    private void OnDisable()
    {
        SceneView.duringSceneGui -= DrawPreview;
    }

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space(4f);
        if (GUILayout.Button(showPreview
                ? "Hide Shield Preview"
                : "Show Shield Preview"))
        {
            showPreview = !showPreview;
            SceneView.RepaintAll();
        }

        if (showPreview)
        {
            EditorGUILayout.HelpBox(
                "The preview shows the shield at full strength. Runtime visibility depends on the shield being enabled and having points remaining.",
                MessageType.None);
        }
    }

    private void DrawPreview(SceneView sceneView)
    {
        if (!showPreview
            || Event.current.type != EventType.Repaint
            || target is not EnemyShieldModifier modifier
            || !modifier.ShowShieldVisual)
        {
            return;
        }

        Matrix4x4 previousMatrix = Handles.matrix;
        Color previousColor = Handles.color;
        try
        {
            Handles.matrix = modifier.transform.localToWorldMatrix
                * Matrix4x4.TRS(
                    modifier.ShieldVisualLocalOffset,
                    Quaternion.identity,
                    new Vector3(
                        modifier.ShieldVisualScale.x,
                        modifier.ShieldVisualScale.y,
                        1f));
            Color color = modifier.ShieldVisualColor;
            color.a = Mathf.Max(0.2f, color.a);
            Handles.color = color;
            Handles.DrawWireDisc(
                Vector3.zero,
                Vector3.forward,
                modifier.ShieldVisualRadius);
        }
        finally
        {
            Handles.matrix = previousMatrix;
            Handles.color = previousColor;
        }
    }
}
