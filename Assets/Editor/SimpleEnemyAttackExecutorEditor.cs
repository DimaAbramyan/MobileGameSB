using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(SimpleEnemyAttackExecutor))]
[CanEditMultipleObjects]
public sealed class SimpleEnemyAttackExecutorEditor : Editor
{
    private float previewTime;
    private readonly Dictionary<string, bool> projectileBaseSpeedOverrideStates = new();
    private readonly Dictionary<string, bool> projectileLifetimeOverrideStates = new();

    private void OnEnable()
    {
        serializedObject.Update();
        RememberProjectileBaseSpeedOverrideStates(
            serializedObject.FindProperty("burstAttackSettings"));
        RememberProjectileLifetimeOverrideStates(
            serializedObject.FindProperty("burstAttackSettings"));
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        EditorGUILayout.LabelField("Projectile References", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("defaultProjectilePrefab"), new GUIContent("Default Projectile"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("homingProjectilePrefab"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("burstAtPointProjectilePrefab"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("projectileSpawnPoint"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("projectileSpawnOffset"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("autonomousAttack"));
        var pattern = serializedObject.FindProperty("burstAttackSettings");
        var shooting = pattern.FindPropertyRelative("shooting");
        bool multiple = pattern.FindPropertyRelative("attackPatterns").enumValueIndex == (int)EnemyAttackPatternsMode.Multiple;
        if (multiple || shooting.FindPropertyRelative("shootingType").enumValueIndex == (int)EnemyShootingType.Rotation)
            EditorGUILayout.PropertyField(serializedObject.FindProperty("rotationTarget"));
        EditorGUILayout.Space();
        EditorGUILayout.PropertyField(pattern, new GUIContent("Attack Pattern"), true);
        InitializeJustEnabledProjectileBaseSpeedOverrides(pattern);
        InitializeJustEnabledProjectileLifetimeOverrides(pattern);
        if (pattern.FindPropertyRelative("facingMode").enumValueIndex == (int)EnemyFacingMode.FollowPattern)
        {
            var facingTarget = serializedObject.FindProperty("facingTarget");
            EditorGUILayout.PropertyField(facingTarget, new GUIContent("Facing Target"));
            var selectedTarget = facingTarget.objectReferenceValue as Transform;
            var executor = (SimpleEnemyAttackExecutor)target;
            if (selectedTarget != null && selectedTarget != executor.transform && !selectedTarget.IsChildOf(executor.transform))
                EditorGUILayout.HelpBox("Facing Target must be the enemy or one of its children.", MessageType.Warning);
            EditorGUILayout.HelpBox("Empty Facing Target turns the enemy. Assign a visible child to keep the root and hitbox fixed. Radial attacks face the center of arm 1.", MessageType.Info);
        }
        if (multiple)
        {
            var patterns = pattern.FindPropertyRelative("patterns");
            for (int i = 0; i < patterns.arraySize; i++)
                CheckProjectileReference(patterns.GetArrayElementAtIndex(i).FindPropertyRelative("shooting"), $"Pattern {i + 1}");
        }
        else
            CheckProjectileReference(shooting, "Attack Pattern");
        serializedObject.ApplyModifiedProperties();
        if (!Application.isPlaying)
        {
            EditorGUI.BeginChangeCheck();
            previewTime = EditorGUILayout.Slider("Rotation Preview Time", previewTime, 0f, 20f);
            if (EditorGUI.EndChangeCheck())
                SceneView.RepaintAll();
        }
    }

    private void InitializeJustEnabledProjectileBaseSpeedOverrides(
        SerializedProperty pattern)
    {
        if (pattern == null)
            return;

        var executor = (SimpleEnemyAttackExecutor)target;
        bool multiple = pattern.FindPropertyRelative("attackPatterns").enumValueIndex
            == (int)EnemyAttackPatternsMode.Multiple;
        if (multiple)
        {
            SerializedProperty patterns = pattern.FindPropertyRelative("patterns");
            for (int i = 0; i < patterns.arraySize; i++)
            {
                InitializeProjectileBaseSpeedOverride(
                    executor,
                    patterns.GetArrayElementAtIndex(i).FindPropertyRelative("shooting"));
            }
            return;
        }

        InitializeProjectileBaseSpeedOverride(
            executor,
            pattern.FindPropertyRelative("shooting"));
    }

    private void InitializeProjectileBaseSpeedOverride(
        SimpleEnemyAttackExecutor executor,
        SerializedProperty shooting)
    {
        if (shooting == null)
            return;

        SerializedProperty overrideSpeed = shooting.FindPropertyRelative(
            "overrideProjectileBaseSpeed");
        SerializedProperty speed = shooting.FindPropertyRelative(
            "projectileBaseSpeed");
        if (overrideSpeed == null || speed == null)
            return;

        bool enabled = overrideSpeed.boolValue;
        bool wasEnabled = projectileBaseSpeedOverrideStates.TryGetValue(
            shooting.propertyPath,
            out bool remembered)
            ? remembered
            : enabled;
        if (enabled && !wasEnabled
            && executor.TryGetProjectileBaseSpeed(
                (EnemyProjectileType)shooting.FindPropertyRelative(
                    "projectileType").enumValueIndex,
                out float baseSpeed))
        {
            speed.floatValue = baseSpeed;
        }

        projectileBaseSpeedOverrideStates[shooting.propertyPath] = enabled;
    }

    private void InitializeJustEnabledProjectileLifetimeOverrides(
        SerializedProperty pattern)
    {
        if (pattern == null)
            return;

        var executor = (SimpleEnemyAttackExecutor)target;
        bool multiple = pattern.FindPropertyRelative("attackPatterns").enumValueIndex
            == (int)EnemyAttackPatternsMode.Multiple;
        if (multiple)
        {
            SerializedProperty patterns = pattern.FindPropertyRelative("patterns");
            for (int i = 0; i < patterns.arraySize; i++)
            {
                InitializeProjectileLifetimeOverride(
                    executor,
                    patterns.GetArrayElementAtIndex(i).FindPropertyRelative("shooting"));
            }
            return;
        }

        InitializeProjectileLifetimeOverride(
            executor,
            pattern.FindPropertyRelative("shooting"));
    }

    private void InitializeProjectileLifetimeOverride(
        SimpleEnemyAttackExecutor executor,
        SerializedProperty shooting)
    {
        if (shooting == null)
            return;

        SerializedProperty overrideLifetime = shooting.FindPropertyRelative(
            "overrideProjectileLifetime");
        SerializedProperty lifetime = shooting.FindPropertyRelative(
            "projectileLifetime");
        if (overrideLifetime == null || lifetime == null)
            return;

        bool enabled = overrideLifetime.boolValue;
        bool wasEnabled = projectileLifetimeOverrideStates.TryGetValue(
            shooting.propertyPath,
            out bool remembered)
            ? remembered
            : enabled;
        if (enabled && !wasEnabled
            && executor.TryGetProjectileBaseLifetime(
                (EnemyProjectileType)shooting.FindPropertyRelative(
                    "projectileType").enumValueIndex,
                out float baseLifetime))
        {
            lifetime.floatValue = baseLifetime;
        }

        projectileLifetimeOverrideStates[shooting.propertyPath] = enabled;
    }

    private void RememberProjectileBaseSpeedOverrideStates(
        SerializedProperty pattern)
    {
        if (pattern == null)
            return;

        bool multiple = pattern.FindPropertyRelative("attackPatterns").enumValueIndex
            == (int)EnemyAttackPatternsMode.Multiple;
        if (multiple)
        {
            SerializedProperty patterns = pattern.FindPropertyRelative("patterns");
            for (int i = 0; i < patterns.arraySize; i++)
            {
                RememberProjectileBaseSpeedOverrideState(
                    patterns.GetArrayElementAtIndex(i).FindPropertyRelative("shooting"));
            }
            return;
        }

        RememberProjectileBaseSpeedOverrideState(
            pattern.FindPropertyRelative("shooting"));
    }

    private void RememberProjectileBaseSpeedOverrideState(
        SerializedProperty shooting)
    {
        if (shooting == null)
            return;

        SerializedProperty overrideSpeed = shooting.FindPropertyRelative(
            "overrideProjectileBaseSpeed");
        if (overrideSpeed != null)
            projectileBaseSpeedOverrideStates[shooting.propertyPath] =
                overrideSpeed.boolValue;
    }

    private void RememberProjectileLifetimeOverrideStates(
        SerializedProperty pattern)
    {
        if (pattern == null)
            return;

        bool multiple = pattern.FindPropertyRelative("attackPatterns").enumValueIndex
            == (int)EnemyAttackPatternsMode.Multiple;
        if (multiple)
        {
            SerializedProperty patterns = pattern.FindPropertyRelative("patterns");
            for (int i = 0; i < patterns.arraySize; i++)
            {
                RememberProjectileLifetimeOverrideState(
                    patterns.GetArrayElementAtIndex(i).FindPropertyRelative("shooting"));
            }
            return;
        }

        RememberProjectileLifetimeOverrideState(
            pattern.FindPropertyRelative("shooting"));
    }

    private void RememberProjectileLifetimeOverrideState(
        SerializedProperty shooting)
    {
        if (shooting == null)
            return;

        SerializedProperty overrideLifetime = shooting.FindPropertyRelative(
            "overrideProjectileLifetime");
        if (overrideLifetime != null)
        {
            projectileLifetimeOverrideStates[shooting.propertyPath] =
                overrideLifetime.boolValue;
        }
    }

    private void CheckProjectileReference(SerializedProperty shooting, string label)
    {
        var type = (EnemyProjectileType)shooting.FindPropertyRelative("projectileType").enumValueIndex;
        string reference = type switch
        {
            EnemyProjectileType.Homing => "homingProjectilePrefab",
            EnemyProjectileType.BurstAtPoint => "burstAtPointProjectilePrefab",
            _ => "defaultProjectilePrefab"
        };
        if (serializedObject.FindProperty(reference).objectReferenceValue == null)
            EditorGUILayout.HelpBox($"{label}: assign the {type} projectile reference. Wave Override uses these same references.", MessageType.Warning);
    }

    private void OnSceneGUI()
    {
        if (Application.isPlaying || target == null)
            return;
        var executor = (SimpleEnemyAttackExecutor)target;
        var pivot = serializedObject.FindProperty("rotationTarget").objectReferenceValue as Transform;
        var spawn = serializedObject.FindProperty("projectileSpawnPoint").objectReferenceValue as Transform;
        var patterns = executor.BurstAttackSettings;
        for (int i = 0; i < patterns.PatternCount; i++)
        {
            var shooting = patterns.GetPattern(i).Shooting;
            Transform basis = shooting.ShootingType == EnemyShootingType.Rotation && pivot != null ? pivot : executor.transform;
            Vector3 position = spawn != null ? spawn.position : basis.TransformPoint(serializedObject.FindProperty("projectileSpawnOffset").vector3Value);
            EnemyShootingPreview.Draw(
                shooting,
                position,
                basis.rotation,
                previewTime,
                null,
                executor.transform.position);
        }
        int facingIndex = patterns.FacingPatternIndex;
        if (patterns.FacingMode == EnemyFacingMode.FollowPattern && facingIndex >= 0)
        {
            var shooting = patterns.GetPattern(facingIndex).Shooting;
            Quaternion firingBasis = shooting.ShootingType == EnemyShootingType.Rotation && pivot != null
                ? pivot.rotation : executor.transform.rotation;
            var facing = serializedObject.FindProperty("facingTarget").objectReferenceValue as Transform;
            EnemyShootingPreview.DrawFacing(patterns, executor.transform.position,
                facing != null ? facing.rotation : executor.transform.rotation, firingBasis, previewTime);
        }
    }
}
