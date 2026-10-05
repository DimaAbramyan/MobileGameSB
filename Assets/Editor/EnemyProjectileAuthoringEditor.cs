using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(EnemyProjectileAuthoring))]
public sealed class EnemyProjectileAuthoringEditor : Editor
{
    private SerializedProperty legacyProjectile;
    private SerializedProperty visualRenderer;
    private SerializedProperty baseDamage;
    private SerializedProperty radius;
    private SerializedProperty baseLifetime;
    private SerializedProperty baseSpeed;
    private SerializedProperty visualScale;
    private SerializedProperty collisionScale;
    private SerializedProperty flightMode;
    private SerializedProperty homingTurnSpeedDegreesPerSecond;
    private SerializedProperty homingDuration;
    private SerializedProperty burstDetonationPoint;
    private SerializedProperty burstDetonationRadius;
    private SerializedProperty burstProjectilePrefab;
    private SerializedProperty burstWaveCount;
    private SerializedProperty burstWaveInterval;
    private SerializedProperty burstProjectilesPerWave;
    private SerializedProperty burstAngleStepDegrees;
    private SerializedProperty burstStartAngleDegrees;
    private SerializedProperty scaleBehavior;
    private SerializedProperty scaleChangeY;
    private SerializedProperty scaleMultiplierByDistance;

    private void OnEnable()
    {
        legacyProjectile = serializedObject.FindProperty("legacyProjectile");
        visualRenderer = serializedObject.FindProperty("visualRenderer");
        baseDamage = serializedObject.FindProperty("baseDamage");
        radius = serializedObject.FindProperty("radius");
        baseLifetime = serializedObject.FindProperty("baseLifetime");
        baseSpeed = serializedObject.FindProperty("baseSpeed");
        visualScale = serializedObject.FindProperty("visualScale");
        collisionScale = serializedObject.FindProperty("collisionScale");
        flightMode = serializedObject.FindProperty("flightMode");
        homingTurnSpeedDegreesPerSecond = serializedObject.FindProperty(
            "homingTurnSpeedDegreesPerSecond");
        homingDuration = serializedObject.FindProperty("homingDuration");
        burstDetonationPoint = serializedObject.FindProperty("burstDetonationPoint");
        burstDetonationRadius = serializedObject.FindProperty("burstDetonationRadius");
        burstProjectilePrefab = serializedObject.FindProperty("burstProjectilePrefab");
        burstWaveCount = serializedObject.FindProperty("burstWaveCount");
        burstWaveInterval = serializedObject.FindProperty("burstWaveInterval");
        burstProjectilesPerWave = serializedObject.FindProperty("burstProjectilesPerWave");
        burstAngleStepDegrees = serializedObject.FindProperty("burstAngleStepDegrees");
        burstStartAngleDegrees = serializedObject.FindProperty("burstStartAngleDegrees");
        scaleBehavior = serializedObject.FindProperty("scaleBehavior");
        scaleChangeY = serializedObject.FindProperty("scaleChangeY");
        scaleMultiplierByDistance = serializedObject.FindProperty(
            "scaleMultiplierByDistance");
    }

    public override void OnInspectorGUI()
    {
        if (flightMode == null || scaleBehavior == null)
        {
            DrawDefaultInspector();
            return;
        }

        serializedObject.Update();
        EditorGUILayout.LabelField("Legacy Source", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(legacyProjectile);
        EditorGUILayout.PropertyField(visualRenderer);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Static Projectile Data", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(baseDamage);
        EditorGUILayout.PropertyField(radius);
        EditorGUILayout.PropertyField(baseLifetime);
        EditorGUILayout.PropertyField(baseSpeed);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("ECS Presentation", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(visualScale);
        EditorGUILayout.PropertyField(collisionScale);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Flight Behavior", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(flightMode, new GUIContent("Flight Mode"));
        DrawFlightSettings();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Scale Behavior", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(scaleBehavior, new GUIContent("Scale Behavior"));
        if ((EnemyProjectileScaleBehavior)scaleBehavior.enumValueIndex
            == EnemyProjectileScaleBehavior.Customized)
        {
            EditorGUILayout.PropertyField(scaleChangeY, new GUIContent("Scale Change Y"));
            EditorGUILayout.PropertyField(
                scaleMultiplierByDistance,
                new GUIContent("Scale Multiplier By Distance"));
        }

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawFlightSettings()
    {
        switch ((EnemyProjectileFlightMode)flightMode.enumValueIndex)
        {
            case EnemyProjectileFlightMode.Homing:
                EditorGUILayout.PropertyField(
                    homingTurnSpeedDegreesPerSecond,
                    new GUIContent("Turn Speed (Degrees / Second)"));
                EditorGUILayout.PropertyField(
                    homingDuration,
                    new GUIContent("Homing Duration (0 = Unlimited)"));
                break;
            case EnemyProjectileFlightMode.BurstAtPoint:
                EditorGUILayout.PropertyField(
                    burstDetonationPoint,
                    new GUIContent("Detonation Point (World)"));
                EditorGUILayout.PropertyField(
                    burstDetonationRadius,
                    new GUIContent("Detonation Radius"));
                EditorGUILayout.PropertyField(
                    burstProjectilePrefab,
                    new GUIContent("Burst Projectile (Empty = Current)"));
                EditorGUILayout.PropertyField(
                    burstWaveCount,
                    new GUIContent("Wave Count"));
                EditorGUILayout.PropertyField(
                    burstWaveInterval,
                    new GUIContent("Wave Interval"));
                EditorGUILayout.PropertyField(
                    burstProjectilesPerWave,
                    new GUIContent("Projectiles Per Wave"));
                EditorGUILayout.PropertyField(
                    burstAngleStepDegrees,
                    new GUIContent("Angle Step (Degrees)"));
                EditorGUILayout.PropertyField(
                    burstStartAngleDegrees,
                    new GUIContent("Start Angle (Degrees)"));
                break;
        }
    }
}
