using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(PlayerLocalStatusIndicator))]
public sealed class PlayerLocalStatusIndicatorEditor : Editor
{
    private bool showPreview;

    private void OnDisable()
    {
        SetPreviewForTargets(false);
    }

    public override void OnInspectorGUI()
    {
        EditorGUI.BeginChangeCheck();
        DrawDefaultInspector();
        if (EditorGUI.EndChangeCheck())
            RefreshPreviewForTargets();

        EditorGUILayout.Space(4f);
        if (GUILayout.Button(showPreview
                ? "Hide Status Indicator Preview"
                : "Show Status Indicator Preview"))
        {
            showPreview = !showPreview;
            SetPreviewForTargets(showPreview);
        }

        if (showPreview)
        {
            EditorGUILayout.HelpBox(
                "Preview Health Fraction and Preview Shield Fraction control the filled parts. "
                + "The preview uses the same LineRenderer as the game and does not modify the prefab.",
                MessageType.None);
        }
    }

    private void RefreshPreviewForTargets()
    {
        if (!showPreview)
            return;

        foreach (Object selectedTarget in targets)
        {
            if (selectedTarget is PlayerLocalStatusIndicator indicator)
                indicator.RefreshEditorPreview();
        }
    }

    private void SetPreviewForTargets(bool shouldShow)
    {
        foreach (Object selectedTarget in targets)
        {
            if (selectedTarget is PlayerLocalStatusIndicator indicator)
                indicator.SetEditorPreview(shouldShow);
        }
    }
}
