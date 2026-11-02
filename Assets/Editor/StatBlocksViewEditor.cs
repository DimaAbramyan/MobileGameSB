using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[CustomEditor(typeof(StatBlocksView))]
public sealed class StatBlocksViewEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        var view = (StatBlocksView)target;
        if (view.Meta != null && !(view.Meta is IStatDisplaySource))
            EditorGUILayout.HelpBox("Assign WeaponMetaConfig or ShipMetaConfig.", MessageType.Warning);
        foreach (var warning in view.Warnings) EditorGUILayout.HelpBox(warning, MessageType.Warning);
        using (new EditorGUI.DisabledScope(EditorUtility.IsPersistent(view)))
            if (GUILayout.Button("Rebuild Preview"))
            {
                Undo.RegisterFullObjectHierarchyUndo(view.gameObject, "Rebuild stat blocks");
                view.Rebuild(Undo.DestroyObjectImmediate);
                for (int i = 0; i < view.transform.childCount; i++)
                    for (int r = 0; r < view.transform.GetChild(i).childCount; r++)
                        Undo.RegisterCreatedObjectUndo(view.transform.GetChild(i).GetChild(r).gameObject, "Create stat row");
                EditorSceneManager.MarkSceneDirty(view.gameObject.scene);
            }
    }
}
