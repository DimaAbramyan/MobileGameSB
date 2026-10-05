using UnityEditor;

[CustomEditor(typeof(FourWayEnemy))]
public sealed class FourWayEnemyEditor : Editor
{
    public override void OnInspectorGUI()
    {
        EditorGUILayout.HelpBox("Attack and rotation settings have moved to Simple Enemy Attack Executor / Attack Pattern.", MessageType.Info);
        serializedObject.Update();
        DrawPropertiesExcluding(serializedObject, "m_Script", "_fireRate");
        serializedObject.ApplyModifiedProperties();
    }
}
