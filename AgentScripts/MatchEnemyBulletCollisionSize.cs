using System;
using UnityEditor;
using UnityEngine;

public static class MatchEnemyBulletCollisionSize
{
    public static object Run()
    {
        if (EditorApplication.isPlaying || EditorApplication.isCompiling || EditorApplication.isUpdating)
            throw new Exception("Editor must be idle in Edit Mode.");
        const string path = "Assets/Prefub/Enemy Projectiles/Bullet.prefab";
        var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        var authoring = go.GetComponent<EnemyProjectileAuthoring>();
        float oldRadius = authoring.Radius;
        Vector2 visualSize = Vector2.Scale(authoring.VisualRenderer.sprite.bounds.size, authoring.VisualScale);
        float radius = Mathf.Max(visualSize.x, visualSize.y) * .5f / authoring.CollisionScale;
        Undo.RecordObject(authoring, "Match enemy bullet collision to visual size");
        var serialized = new SerializedObject(authoring);
        serialized.FindProperty("radius").floatValue = radius;
        serialized.ApplyModifiedProperties();
        EditorUtility.SetDirty(authoring);
        AssetDatabase.SaveAssets();
        return new { path, oldRadius, radius = authoring.Radius, effectiveRadius = authoring.EffectiveRadius };
    }
}
