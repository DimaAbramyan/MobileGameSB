using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(FourWayEnemyRotationController))]
public sealed class FourWayEnemyRotationControllerEditor : Editor
{
    private SerializedProperty rotationTarget;
    private SerializedProperty direction;
    private SerializedProperty rotationMode;
    private SerializedProperty rotationSpeedDegreesPerSecond;
    private SerializedProperty rotationProgressCurve;
    private SerializedProperty rotationAngle;
    private SerializedProperty rotationFromAngle;
    private SerializedProperty resetRotationOnEnable;

    private void OnEnable()
    {
        rotationTarget = serializedObject.FindProperty("rotationTarget");
        direction = serializedObject.FindProperty("direction");
        rotationMode = serializedObject.FindProperty("rotationMode");
        rotationSpeedDegreesPerSecond = serializedObject.FindProperty("rotationSpeedDegreesPerSecond");
        rotationProgressCurve = serializedObject.FindProperty("rotationProgressCurve");
        rotationAngle = serializedObject.FindProperty("rotationAngle");
        rotationFromAngle = serializedObject.FindProperty("rotationFromAngle");
        resetRotationOnEnable = serializedObject.FindProperty("resetRotationOnEnable");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.PropertyField(rotationTarget);
        EditorGUILayout.Space();
        EditorGUILayout.PropertyField(direction);
        EditorGUILayout.PropertyField(rotationMode);
        EditorGUILayout.PropertyField(rotationSpeedDegreesPerSecond);
        EditorGUILayout.PropertyField(rotationProgressCurve);

        FourWayEnemyRotationMode mode =
            (FourWayEnemyRotationMode)rotationMode.enumValueIndex;
        if (mode == FourWayEnemyRotationMode.ByAngle)
        {
            EditorGUILayout.PropertyField(
                rotationAngle,
                new GUIContent("Rotation Angle"));
        }
        else if (mode == FourWayEnemyRotationMode.PingPongByAngle)
        {
            EditorGUILayout.PropertyField(
                rotationFromAngle,
                new GUIContent("From Angle"));
            EditorGUILayout.PropertyField(
                rotationAngle,
                new GUIContent("To Angle"));
        }

        EditorGUILayout.PropertyField(resetRotationOnEnable);
        EditorGUILayout.HelpBox(
            "Rotation Progress Curve: X is normalized pass time and Y is the completed part of the turn. Use a curve from 0 to 1. It repeats every full turn in Continuous mode and on both passes in Ping Pong By Angle mode.",
            MessageType.Info);

        serializedObject.ApplyModifiedProperties();
    }
}
