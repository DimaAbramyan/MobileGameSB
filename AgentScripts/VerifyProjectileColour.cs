using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class VerifyProjectileColour
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static int checks;
    private static void Check(bool value, string message)
    { if (!value) throw new Exception(message); checks++; }
    private static void Set(object target, string field, object value)
    {
        for (var type = target.GetType(); type != null; type = type.BaseType)
        {
            var property = type.GetProperty(field, Private | BindingFlags.Public);
            if (property != null) { property.SetValue(target, value); return; }
        }
        Field(target, field).SetValue(target, value);
    }
    private static object Get(object target, string field)
        => Field(target, field).GetValue(target);
    private static FieldInfo Field(object target, string name)
    {
        for (var type = target.GetType(); type != null; type = type.BaseType)
        {
            var field = type.GetField(name, Private);
            if (field != null) return field;
        }
        throw new Exception("Missing field: " + name);
    }
    private static bool Matches(float4 value, Color colour)
        => math.distance(value, EnemyProjectileCharacteristics.ToRenderColour(colour)) < .0001f;

    public static object Run()
    {
        if (EditorApplication.isPlaying || EditorApplication.isCompiling || EditorApplication.isUpdating)
            throw new Exception("Editor must be idle");
        checks = 0;
        var scene = EditorSceneManager.NewPreviewScene();
        var root = new GameObject("Projectile colour verification");
        SceneManager.MoveGameObjectToScene(root, scene);
        var wave = root.AddComponent<DirectedEnemySubWave>();
        var attack = root.AddComponent<DirectedWaveAttackBehaviour>();
        var preset = ScriptableObject.CreateInstance<DirectedWaveAttackPreset>();
        try
        {
            var enemy = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefub/Enemies Prefab/Enemy.prefab").GetComponent<Enemy>();
            Set(wave, "enemyPrefab", enemy);
            Set(wave, "enemyCount", 1);
            Set(wave, "formationLayout", DirectedWaveFormationLayout.HorizontalLine);
            var settings = (DirectedWaveAttackSettings)Get(attack, "attackSettings");
            Set(settings, "fireMode", DirectedWaveAttackFireMode.Aimed);
            Set(settings, "burstSettingsSource", DirectedWaveBurstSettingsSource.WaveOverride);
            Set(settings, "waitForPreviousAttack", true);
            Set(settings, "delayAfterAttack", 1000f);
            settings.WaveBurstSettings.ConfigureLegacySingleAttack(1, 0, 0);
            var shooting = settings.WaveBurstSettings.Shooting;
            shooting.EmissionLayout.Configure(EnemyEmissionLayoutType.Point);
            shooting.SetProjectileBaseSpeedOverride(true, 3f);
            Check(!shooting.OverwritesBaseColour, "Default preserves prefab colour");
            var red = new Color(.9f, .1f, .2f, .7f);
            Set(shooting, "overwriteBaseColour", true);
            Set(shooting, "projectileColour", red);
            preset.SetAttackSettings(settings);
            Check(preset.AttackSettings.WaveBurstSettings.Shooting.OverwritesBaseColour
                && preset.AttackSettings.WaveBurstSettings.Shooting.ProjectileColour == red,
                "Preset retains enabled colour override");
            var copy = new DirectedWaveAttackSettings(); copy.CopyFrom(preset.AttackSettings);
            Check(copy.WaveBurstSettings.Shooting.ProjectileColour == red, "Resolved runtime copy retains colour");
            var prefab = (EnemyBullet)new SerializedObject(enemy.GetComponent<SimpleEnemyAttackExecutor>())
                .FindProperty("defaultProjectilePrefab").objectReferenceValue;
            var baseColour = prefab.GetComponent<EnemyProjectileAuthoring>().VisualRenderer.color;
            var serialized = new SerializedObject(attack);
            var shootingProperty = serialized.FindProperty("attackSettings.waveBurstSettings.shooting");
            Check(shootingProperty != null && shootingProperty.FindPropertyRelative("overwriteBaseColour") != null,
                "Projectile Settings has serialized colour checkbox");
            float enabledHeight = EnemyShootingSettingsDrawer.SettingsHeight(shootingProperty);
            Set(shooting, "overwriteBaseColour", false); serialized.Update();
            Check(EnemyShootingSettingsDrawer.SettingsHeight(shootingProperty) < enabledHeight,
                "Colour field is hidden when checkbox is off");
            Set(shooting, "overwriteBaseColour", true);
            using (var session = new WaveShootingPreviewSession(wave, true, false, 0, new Vector3(0, -5)))
            {
                for (int i = 0; !session.AdvanceTo(.1f, 20); i++)
                    if (i > 100) throw new Exception("Preview failed to advance");
                var manager = session.ProjectileWorld.SimulationWorld.EntityManager;
                using var query = manager.CreateEntityQuery(typeof(EnemyProjectileMovement), typeof(URPMaterialPropertyBaseColor));
                using var colours = query.ToComponentDataArray<URPMaterialPropertyBaseColor>(Allocator.Temp);
                Check(colours.Length > 0, "Preview spawned actual ECS projectile");
                Check(Matches(colours[0].Value, red), "Preview request applies override colour to ECS entity");
                session.ProjectileWorld.CaptureRenderState();
                var batches = (IList)Get(session.ProjectileWorld, "batchList");
                Check(batches.Count > 0, "Preview render batch created");
                var material = (Material)batches[0].GetType().GetField("Material").GetValue(batches[0]);
                Check(Vector4.Distance(material.GetColor("_BaseColor"), red) < .0001f, "Preview render material uses selected colour");
            }
            using (var preview = new EnemyProjectilePreviewWorld())
            {
                var blue = new Color(.1f, .2f, .9f, 1f);
                var requests = new List<EnemyProjectileSpawnRequest>();
                foreach (var colour in new[] { red, blue, baseColour })
                    requests.Add(new EnemyProjectileSpawnRequest(prefab, Vector3.zero, Vector3.down,
                        1f, 3f, EnemyProjectileSpeedBehavior.None, 0f, null,
                        EnemyProjectileSizeBehavior.Default, 0f, 1f, null, 1f,
                        Vector3.zero, EnemyProjectileMovementPattern.Default, 0f, 0f,
                        EnemyProjectileOrbitCenterMode.SourcePosition, Vector2.zero, 0f, 0f,
                        false, 0f, overwritesBaseColour: colour != baseColour, projectileColour: colour));
                preview.Spawn(requests);
                using var query = preview.SimulationWorld.EntityManager.CreateEntityQuery(typeof(EnemyProjectileMovement), typeof(URPMaterialPropertyBaseColor));
                using var colours = query.ToComponentDataArray<URPMaterialPropertyBaseColor>(Allocator.Temp);
                Check(colours.Length == 3 && Matches(colours[0].Value, red) && Matches(colours[1].Value, blue)
                    && Matches(colours[2].Value, baseColour), "Same prefab supports independent colours and disabled override preserves base");
                preview.CaptureRenderState();
                Check(((IList)Get(preview, "batchList")).Count == 3, "Preview separates colour batches");
                preview.CaptureRenderState();
                Check(((IList)Get(preview, "batchList")).Count == 3, "Preview reuses cached materials");
            }
            Check(prefab.GetComponent<EnemyProjectileAuthoring>().VisualRenderer.color == baseColour,
                "Prefab colour remains unchanged");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(preset);
            UnityEngine.Object.DestroyImmediate(root);
            EditorSceneManager.ClosePreviewScene(scene);
        }
        return new { passed = checks };
    }
}

