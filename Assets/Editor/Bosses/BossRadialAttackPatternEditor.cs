using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(BossRadialAttackPattern))]
public sealed class BossRadialAttackPatternEditor : Editor
{
    private const float PreviewHeight = 240f;

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        DrawDefaultInspector();
        serializedObject.ApplyModifiedProperties();

        BossRadialAttackPattern pattern =
            (BossRadialAttackPattern)target;

        if (pattern.ProjectilePrefab == null)
        {
            EditorGUILayout.HelpBox(
                "Assign a prefab with BossProjectile before using this attack.",
                MessageType.Warning);
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Attack Preview", EditorStyles.boldLabel);
        Rect rect = GUILayoutUtility.GetRect(
            10f,
            PreviewHeight,
            GUILayout.ExpandWidth(true));
        DrawPreview(rect, pattern);
    }

    private static void DrawPreview(
        Rect rect,
        BossRadialAttackPattern pattern)
    {
        EditorGUI.DrawRect(rect, new Color(0.10f, 0.11f, 0.13f));

        Vector2 center = rect.center;
        float radius = Mathf.Min(rect.width, rect.height) * 0.38f;
        Handles.BeginGUI();

        Handles.color = new Color(1f, 1f, 1f, 0.12f);
        Handles.DrawWireDisc(center, Vector3.forward, radius);
        Handles.DrawWireDisc(center, Vector3.forward, 4f);

        DrawVolley(pattern, center, radius, 0, new Color(0.25f, 0.8f, 1f));

        if (pattern.VolleyCount > 1)
        {
            DrawVolley(
                pattern,
                center,
                radius * 0.72f,
                pattern.VolleyCount - 1,
                new Color(1f, 0.55f, 0.22f, 0.85f));
        }

        Handles.EndGUI();
    }

    private static void DrawVolley(
        BossRadialAttackPattern pattern,
        Vector2 center,
        float radius,
        int volleyIndex,
        Color color)
    {
        Handles.color = color;

        for (int i = 0; i < pattern.ProjectileCount; i++)
        {
            float angle = pattern.GetProjectileAngleDegrees(
                i,
                volleyIndex,
                0f);
            float radians = angle * Mathf.Deg2Rad;
            Vector2 direction = new Vector2(
                Mathf.Cos(radians),
                -Mathf.Sin(radians));
            Vector2 end = center + direction * radius;

            Handles.DrawLine(center, end);
            Handles.DrawSolidDisc(end, Vector3.forward, 3.5f);
        }
    }
}
