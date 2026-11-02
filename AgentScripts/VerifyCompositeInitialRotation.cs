using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
public static class VerifyCompositeInitialRotation
{
    static int checks;
    static void Check(bool condition,string name) { checks++; if(!condition) throw new Exception(name); }
    public static object Run()
    {
        checks=0;
        var shooting=new EnemyShootingSettings(); shooting.SetProjectileType(EnemyProjectileType.Composite);
        var settings=shooting.Composite;
        JsonUtility.FromJsonOverwrite("{\"initialRotation\":-17.25,\"overrideHitRadius\":true}",settings);
        var sample=new EnemyNumericSample(123u,456u);
        Check(settings.Resolve(shooting.ProjectileType,sample).InitialRotation==-17.25f,"Legacy fixed angle");
        Check(!shooting.UsesNumericRandomization,"Default random disabled");
        var range=(RandomFloatRange)typeof(EnemyCompositeProjectileSettings).GetField("initialRotationRandom",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(settings);
        range.enabled=true;range.min=-70;range.max=42;
        Check(shooting.UsesNumericRandomization && shooting.UsesExtendedNumericRandomization,"Scheduler enables numeric stream");
        foreach(var scope in new[]{EnemyNumericRandomizeOn.PerAttack,EnemyNumericRandomizeOn.PerShot,EnemyNumericRandomizeOn.PerDirection,EnemyNumericRandomizeOn.PerProjectile})
        {
            range.randomizeOn=scope;
            float a=settings.Resolve(shooting.ProjectileType,sample).InitialRotation;
            Check(a>=-70&&a<=42,"Bounds "+scope);
            Check(a==settings.Resolve(shooting.ProjectileType,new EnemyNumericSample(123,456)).InitialRotation,"Repeat seed "+scope);
            float b=settings.Resolve(shooting.ProjectileType,sample.ForProjectile(1)).InitialRotation;
            Check(scope==EnemyNumericRandomizeOn.PerProjectile?a!=b:a==b,"Scope "+scope);
        }
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefub/Enemy Projectiles/Bullet.prefab").GetComponent<EnemyBullet>();
        var requests=new List<EnemyProjectileSpawnRequest>();
        EnemyAttackProjectileBuilder.AppendEmitter(requests,prefab,shooting,Vector3.zero,Vector3.zero,Vector3.up,1,1,0,default,
            maximum:1,randomSpeed:EnemyRandomSpeedSample.Identity(5).WithNumeric(sample));
        float expected=settings.Resolve(shooting.ProjectileType,sample).InitialRotation;
        Check(requests.Count==1&&requests[0].Composite.InitialRotation==expected,"Spawn builder sampled angle");
        Check(EnemyShootingPreview.TryGetCompositeSilhouette(shooting,prefab,Vector3.up,out var shape,sample)&&shape.Spec.InitialRotation==expected,"Scene silhouette matches runtime");
        var preset=ScriptableObject.CreateInstance<EnemyProjectilePreset>();
        try
        {
            preset.SetSettings(shooting);
            Check(preset.Settings.Composite.Resolve(shooting.ProjectileType,sample).InitialRotation==expected,"Preset copy");
            using var serialized=new SerializedObject(preset);
            var composite=serialized.FindProperty("projectile.composite");
            var drawer=new EnemyCompositeProjectileSettingsDrawer();
            float expanded=drawer.GetPropertyHeight(composite,GUIContent.none);
            composite.FindPropertyRelative("initialRotationRandom.enabled").boolValue=false;
            Check(Mathf.Abs(expanded-drawer.GetPropertyHeight(composite,GUIContent.none)-(EditorGUIUtility.singleLineHeight+2))<.01f,"Drawer reserves frequency row");
        }
        finally { UnityEngine.Object.DestroyImmediate(preset); }
        var copy=settings.Copy();
        Check(copy.Resolve(shooting.ProjectileType,sample).InitialRotation==expected,"Serialized copy");
        range.min=range.max=-12.5f;
        Check(settings.Resolve(shooting.ProjectileType,sample).InitialRotation==-12.5f,"Equal range");
        range.enabled=false;
        Check(settings.Resolve(shooting.ProjectileType,sample).InitialRotation==-17.25f,"Disable restores original angle");
        return new {passed=true,checks};
    }
}
