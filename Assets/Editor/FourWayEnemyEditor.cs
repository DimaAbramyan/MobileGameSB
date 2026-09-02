using UnityEditor;

[CustomEditor(typeof(FourWayEnemy))]
public sealed class FourWayEnemyEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        DrawPropertiesExcluding(serializedObject, "m_Script", "_fireRate");
        serializedObject.ApplyModifiedProperties();
    }
}
