using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class InspectEnemyShootingProfiles
{
    public static object Run()
    {
        var profiles = new List<object>();
        foreach (string path in new[] {
            "Assets/Prefub/Enemies Prefab/Enemy.prefab",
            "Assets/Prefub/Enemies Prefab/DoubleShooter.prefab",
            "Assets/Prefub/Enemies Prefab/FourWayEnemy.prefab",
            "Assets/Prefub/EnemyShipsLegasy/FourWayEnemy.prefab" })
        {
            var root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var executor = root.GetComponent<SimpleEnemyAttackExecutor>();
            var serialized = new SerializedObject(executor);
            var local = executor.BurstAttackSettings;
            profiles.Add(new { path, enemyType = root.GetComponent<Enemy>().GetType().Name,
                type = local.Shooting.ShootingType.ToString(), mode = local.Shooting.ShootingMode.ToString(),
                directions = local.Shooting.DirectionCount, rotates = local.Shooting.Rotates,
                speed = local.Shooting.Rotation.RotationSpeedDegreesPerSecond,
                attackShots = local.AttackShotCount, burstShots = local.BurstShotCount,
                attackCooldown = local.AttackCooldown, autonomous = serialized.FindProperty("autonomousAttack").boolValue,
                rotationTarget = serialized.FindProperty("rotationTarget").objectReferenceValue != null });
        }
        var legacy = JsonUtility.FromJson<DirectedWaveFourWayRotationOverride>(
            "{\"direction\":1,\"rotationMode\":2,\"rotationSpeedDegreesPerSecond\":135,\"rotationAngle\":70,\"rotationFromAngle\":-40,\"resetRotationOnEnable\":false}");
        if (legacy.RotationSpeedDegreesPerSecond != 135 || legacy.RotationFromAngle != -40 || legacy.ResetRotationOnEnable)
            throw new Exception("Legacy point rotation field migration failed");
        var holder = new GameObject("Shooting drawer inspection");
        try
        {
            var point = holder.AddComponent<DirectedWaveEnemyOverride>();
            var serialized = new SerializedObject(point);
            if (serialized.FindProperty("fourWayRotation").FindPropertyRelative("rotationSpeedDegreesPerSecond") == null)
                throw new Exception("Inherited rotation fields unavailable in Inspector");
            var shooting = serialized.FindProperty("burstAttackSettings").FindPropertyRelative("shooting");
            foreach (EnemyShootingType type in Enum.GetValues(typeof(EnemyShootingType)))
            {
                shooting.FindPropertyRelative("shootingType").enumValueIndex = (int)type;
                foreach (EnemyProjectileFireMode mode in Enum.GetValues(typeof(EnemyProjectileFireMode)))
                {
                    shooting.FindPropertyRelative("shootingMode").enumValueIndex = (int)mode;
                    float height = EditorGUI.GetPropertyHeight(serialized.FindProperty("burstAttackSettings"), true);
                    if (height < 100) throw new Exception("Incomplete shared Inspector drawer");
                }
            }
        }
        finally { UnityEngine.Object.DestroyImmediate(holder); }
        return new { profiles, legacyPointRotationPreserved = true, allInspectorPatternHeightsValid = true };
    }
}
