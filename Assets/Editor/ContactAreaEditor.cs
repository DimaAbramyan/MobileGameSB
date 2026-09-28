using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(ContactArea))]
public sealed class ContactAreaEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        DrawDefaultInspector();
        serializedObject.ApplyModifiedProperties();

        ContactArea contactArea = (ContactArea)target;
        PolygonCollider2D polygonCollider = contactArea.PolygonCollider;
        if (polygonCollider == null)
        {
            EditorGUILayout.HelpBox(
                "A PolygonCollider2D is required for a Contact Area.",
                MessageType.Error);
            return;
        }

        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("Contact Polygon", EditorStyles.boldLabel);
        if (polygonCollider.pathCount != 1)
        {
            EditorGUILayout.HelpBox(
                "Contact delivery supports exactly one closed PolygonCollider2D path.",
                MessageType.Error);
            return;
        }

        EditorGUILayout.HelpBox(
            "These are local coordinates. You can edit them here, add a vertex, or use "
            + "Edit Collider on the PolygonCollider2D component in Scene View.",
            MessageType.Info);

        Vector2[] vertices = polygonCollider.points;
        EditorGUI.BeginChangeCheck();
        for (int index = 0; index < vertices.Length; index++)
        {
            vertices[index] = EditorGUILayout.Vector2Field(
                $"Vertex {index}",
                vertices[index]);
        }

        if (EditorGUI.EndChangeCheck())
            SetVertices(polygonCollider, vertices, "Edit Contact Area Vertex");

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Add Vertex"))
                AddVertex(polygonCollider, vertices);

            using (new EditorGUI.DisabledScope(vertices.Length <= 3))
            {
                if (GUILayout.Button("Remove Last Vertex"))
                {
                    Vector2[] reducedVertices = new Vector2[vertices.Length - 1];
                    for (int index = 0; index < reducedVertices.Length; index++)
                        reducedVertices[index] = vertices[index];
                    SetVertices(
                        polygonCollider,
                        reducedVertices,
                        "Remove Contact Area Vertex");
                }
            }
        }
    }

    private static void AddVertex(
        PolygonCollider2D polygonCollider,
        Vector2[] vertices)
    {
        if (vertices.Length < 3)
        {
            SetVertices(
                polygonCollider,
                new[]
                {
                    new Vector2(0f, 0.5f),
                    new Vector2(-0.5f, -0.5f),
                    new Vector2(0.5f, -0.5f)
                },
                "Create Contact Area Polygon");
            return;
        }

        Vector2[] expandedVertices = new Vector2[vertices.Length + 1];
        for (int index = 0; index < vertices.Length; index++)
            expandedVertices[index] = vertices[index];

        expandedVertices[^1] = vertices.Length > 0
            ? (vertices[vertices.Length - 1] + vertices[0]) * 0.5f
            : Vector2.zero;
        SetVertices(polygonCollider, expandedVertices, "Add Contact Area Vertex");
    }

    private static void SetVertices(
        PolygonCollider2D polygonCollider,
        Vector2[] vertices,
        string undoName)
    {
        Undo.RecordObject(polygonCollider, undoName);
        polygonCollider.points = vertices;
        EditorUtility.SetDirty(polygonCollider);
        PrefabUtility.RecordPrefabInstancePropertyModifications(polygonCollider);
    }
}
