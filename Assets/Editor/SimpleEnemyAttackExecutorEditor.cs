using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(SimpleEnemyAttackExecutor))]
[CanEditMultipleObjects]
public sealed class SimpleEnemyAttackExecutorEditor : Editor
{
    private float previewTime;
    private Vector2 childPreviewTarget = new(0f, -3f);
    private EnemyChildAttackScenePreview childPreview;

    private void OnDisable() { childPreview?.Dispose(); childPreview = null; }
    private static bool HasChildren(EnemyBurstAttackSettings settings)
    {
        for (int i = 0; i < settings.PatternCount; i++)
            if (settings.GetPattern(i).Shooting.ChildAttack != null || settings.GetPattern(i).Shooting.ProjectileType == EnemyProjectileType.Laser) return true;
        return false;
    }
    private readonly Dictionary<string, bool> projectileOverrideStates = new();

    private void OnEnable()
    {
        serializedObject.Update();
        RememberProjectileOverrides(serializedObject.FindProperty("burstAttackSettings"));
    }

    public override void OnInspectorGUI()
    {
        EnemyShootingPreview.DrawPreviewControls();
        childPreview?.SetVisible(EnemyShootingPreview.ShowFormationVolley && !Application.isPlaying);
        serializedObject.Update();
        EditorGUILayout.PropertyField(serializedObject.FindProperty("autonomousAttack"));
        var pattern = serializedObject.FindProperty("burstAttackSettings");
        var shooting = pattern.FindPropertyRelative("shooting");
        bool multiple = pattern.FindPropertyRelative("attackPatterns").enumValueIndex == (int)EnemyAttackPatternsMode.Multiple;
        string referencesKey = "EnemyShootingReferences." + target.GetInstanceID();
        bool references = EditorGUILayout.Foldout(SessionState.GetBool(referencesKey, false), "References & Emitter Binding", true);
        SessionState.SetBool(referencesKey, references);
        if (references)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(serializedObject.FindProperty("defaultProjectilePrefab"), new GUIContent("Default Projectile"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("homingProjectilePrefab"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("burstAtPointProjectilePrefab"));
            var point = serializedObject.FindProperty("projectileSpawnPoint");
            EditorGUILayout.PropertyField(point, new GUIContent("Shooting Point"));
            if (point.objectReferenceValue == null || point.hasMultipleDifferentValues)
                EditorGUILayout.PropertyField(serializedObject.FindProperty("projectileSpawnOffset"),
                    new GUIContent("Fallback Spawn Offset", "Used only when Shooting Point is empty. Position Offset on the layout is added to the resulting point."));
            if (multiple || shooting.FindPropertyRelative("emissionLayout.type").enumValueIndex != (int)EnemyEmissionLayoutType.Point)
                EditorGUILayout.PropertyField(serializedObject.FindProperty("rotationTarget"), new GUIContent("Layout Basis"));
            EditorGUI.indentLevel--;
        }
        EditorGUILayout.Space();
        EditorGUILayout.PropertyField(pattern, new GUIContent("Attack Pattern"), true);
        InitializeJustEnabledProjectileOverrides(pattern);
        if (pattern.FindPropertyRelative("facingMode").enumValueIndex == (int)EnemyFacingMode.FollowPattern
            || pattern.FindPropertyRelative("facingMode").enumValueIndex == (int)EnemyFacingMode.Rotation)
        {
            var facingTarget = serializedObject.FindProperty("facingTarget");
            EditorGUILayout.PropertyField(facingTarget, new GUIContent("Facing Target"));
            var selectedTarget = facingTarget.objectReferenceValue as Transform;
            var executor = (SimpleEnemyAttackExecutor)target;
            if (selectedTarget != null && selectedTarget != executor.transform && !selectedTarget.IsChildOf(executor.transform))
                EditorGUILayout.HelpBox("Facing Target must be the enemy or one of its children.", MessageType.Warning);
            EditorGUILayout.HelpBox("Empty Facing Target turns the enemy. Assign a visible child to keep the root and hitbox fixed. Facing never changes emission positions or directions.", MessageType.Info);
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
            previewTime = EditorGUILayout.Slider("Projectile Preview Time", previewTime, 0f, 20f);
            if (HasChildren(((SimpleEnemyAttackExecutor)target).BurstAttackSettings))
            {
                childPreviewTarget = EditorGUILayout.Vector2Field("Child Preview Target", childPreviewTarget);
                EditorGUILayout.HelpBox("Projectile Preview Time simulates one initial volley and its child attacks. Rotation guides automatically show both extreme volleys and their swept area. Preview Wave simulates the full wave, including Burst and entrance attacks.", MessageType.Info);
            }
            if (EditorGUI.EndChangeCheck())
                SceneView.RepaintAll();
            EditorGUILayout.HelpBox("Preview: cyan = emitters, blue = base directions, pink = Sector, yellow = Fan, orange = final projectiles, green = Enemy Facing and turn direction. ×N marks overlapping Stack members.", MessageType.None);
        }
    }

    private static IEnumerable<SerializedProperty> ShootingProperties(SerializedProperty profile)
    {
        if (profile == null) yield break;
        if (profile.FindPropertyRelative("attackPatterns").enumValueIndex != (int)EnemyAttackPatternsMode.Multiple)
        {
            yield return profile.FindPropertyRelative("shooting");
            yield break;
        }
        var patterns = profile.FindPropertyRelative("patterns");
        for (int i = 0; i < patterns.arraySize; i++)
            yield return patterns.GetArrayElementAtIndex(i).FindPropertyRelative("shooting");
    }

    private void RememberProjectileOverrides(SerializedProperty profile)
    {
        foreach (var shooting in ShootingProperties(profile))
        {
            RememberOverride(shooting, "overrideProjectileBaseSpeed");
            RememberOverride(shooting, "overrideProjectileLifetime");
        }
    }

    private void RememberOverride(SerializedProperty shooting, string field)
    {
        var property = shooting.FindPropertyRelative(field);
        projectileOverrideStates[property.propertyPath] = property.boolValue;
    }

    private void InitializeJustEnabledProjectileOverrides(SerializedProperty profile)
    {
        foreach (var shooting in ShootingProperties(profile))
        {
            InitializeOverride(shooting, false);
            InitializeOverride(shooting, true);
        }
    }

    private void InitializeOverride(SerializedProperty shooting, bool lifetime)
    {
        var property = shooting.FindPropertyRelative(lifetime ? "overrideProjectileLifetime" : "overrideProjectileBaseSpeed");
        bool enabled = property.boolValue;
        bool wasEnabled = projectileOverrideStates.TryGetValue(property.propertyPath, out bool previous) ? previous : enabled;
        if (enabled && !wasEnabled)
        {
            var executor = (SimpleEnemyAttackExecutor)target;
            var type = (EnemyProjectileType)shooting.FindPropertyRelative("projectileType").intValue;
            float value;
            bool found = lifetime ? executor.TryGetProjectileBaseLifetime(type, out value) : executor.TryGetProjectileBaseSpeed(type, out value);
            if (found) shooting.FindPropertyRelative(lifetime ? "projectileLifetime" : "projectileBaseSpeed").floatValue = value;
        }
        projectileOverrideStates[property.propertyPath] = enabled;
    }
    private void CheckProjectileReference(SerializedProperty shooting, string label)
    {
        var type = (EnemyProjectileType)shooting.FindPropertyRelative("projectileType").intValue;
        if (type == EnemyProjectileType.Laser) return;
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
        serializedObject.Update();
        if (target is SimpleEnemyAttackExecutor handleExecutor)
            EnemyLaserScenePreview.HandlesFor(serializedObject, handleExecutor.transform.position, handleExecutor);
        if (Application.isPlaying || target == null || !EnemyShootingPreview.ShowFormationVolley
            || Event.current.type != EventType.Repaint)
            return;
        var executor = (SimpleEnemyAttackExecutor)target;
        var pivot = serializedObject.FindProperty("rotationTarget").objectReferenceValue as Transform;
        var patterns = executor.BurstAttackSettings;
        if (patterns.PatternCount == 0) return;
        if (HasChildren(patterns))
        {
            childPreview ??= new EnemyChildAttackScenePreview();
            childPreview.Simulate(executor, previewTime, childPreviewTarget);
            childPreview.SetVisible(true);
            childPreview.Draw(SceneView.currentDrawingSceneView != null ? SceneView.currentDrawingSceneView.camera : null);
        }
        else { childPreview?.Dispose(); childPreview = null; }
        for (int i = 0; i < patterns.PatternCount; i++)
        {
            var shooting = patterns.GetPattern(i).Shooting;
            EnemyShootingPreview.DrawExecutorPattern(executor, shooting, executor.transform.position, previewTime, staticLayoutPose: true);
        }
        int facingIndex = patterns.FacingPatternIndex;
        if (patterns.FacingMode == EnemyFacingMode.Rotation
            || patterns.FacingMode == EnemyFacingMode.FollowPattern && facingIndex >= 0)
        {
            var shooting = patterns.GetPattern(facingIndex).Shooting;
            Quaternion firingBasis = shooting.EmissionLayout.Type != EnemyEmissionLayoutType.Point && pivot != null
                ? pivot.rotation : executor.transform.rotation;
            var facing = serializedObject.FindProperty("facingTarget").objectReferenceValue as Transform;
            EnemyShootingPreview.DrawFacing(patterns, executor.transform.position,
                facing != null ? facing.rotation : executor.transform.rotation, firingBasis, previewTime);
        }
    }
}
