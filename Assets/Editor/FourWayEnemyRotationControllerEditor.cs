using UnityEditor;

[CustomEditor(typeof(FourWayEnemyRotationController))]
public sealed class FourWayEnemyRotationControllerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        EditorGUILayout.HelpBox("Compatibility component. Configure shooting and rotation in Simple Enemy Attack Executor / Attack Pattern / Shooting / Rotation.", MessageType.Info);
    }
}
