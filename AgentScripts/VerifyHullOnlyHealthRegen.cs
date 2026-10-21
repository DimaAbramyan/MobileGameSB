using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
public static class VerifyHullOnlyHealthRegen
{
    const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
    static int checks;
    static void Set(object t,string f,object v)=>t.GetType().GetField(f,Flags).SetValue(t,v);
    static float Clock(object t)=>(float)t.GetType().GetField("lastDamageTime",Flags).GetValue(t);
    static void Invoke(object t,string name)=>t.GetType().GetMethod(name,Flags).Invoke(t,null);
    static void Check(bool value,string name){if(!value)throw new Exception(name);checks++;}
    public static object Run()
    {
        checks=0;var scene=EditorSceneManager.NewPreviewScene();var root=new GameObject("Hull regeneration verification");SceneManager.MoveGameObjectToScene(root,scene);root.SetActive(false);
        try
        {
            var ship=root.AddComponent<ParentShip>();ship.ShipData=AssetDatabase.LoadAssetAtPath<ShipData>("Assets/Prefub/Data/Ship/Arkanoid.asset");
            Set(ship,"<MaximumHealthPoints>k__BackingField",10f);Set(ship,"<MaximumShieldPoints>k__BackingField",10f);Set(ship,"healthDamageProjectileClearRadius",0f);ship.SetHealthPoints(9);ship.SetShieldPoints(2);
            var health=root.AddComponent<HealthRegeneration>();var shield=root.AddComponent<ShieldRegeneration>();Invoke(health,"Awake");Invoke(shield,"Awake");
            float old=Time.time-30f;Set(health,"lastDamageTime",old);Set(shield,"lastDamageTime",old);
            int hullHits=0;ship.OnHealthDamageTaken+=d=>hullHits++;
            Check(ship.TryTakeDamage(1),"Shield hit accepted");
            Check(Mathf.Approximately(Clock(health),old)&&hullHits==0,"Shield hit does not reset health regeneration");
            Check(Mathf.Approximately(Clock(shield),Time.time),"Shield regeneration still resets on shield hit");
            ship.SetShieldPoints(0);Set(ship,"damageInvulnerableUntil",-1f);Set(shield,"lastDamageTime",old);
            Check(ship.TryTakeDamage(1)&&ship.CurrentHealthPoints==8,"Hull hit accepted");
            Check(Mathf.Approximately(Clock(health),Time.time)&&hullHits==1,"Actual hull damage resets health regeneration");
            Check(Mathf.Approximately(Clock(shield),Time.time),"Shield regeneration still resets on hull hit");
            Set(health,"lastDamageTime",old);Check(!ship.TryTakeDamage(1)&&Mathf.Approximately(Clock(health),old),"Invulnerable hit does not reset health regeneration");
            Set(ship,"damageInvulnerableUntil",-1f);Func<float,float> block=d=>0;ship.OnDamagePipeline+=block;
            Check(!ship.TryTakeDamage(1)&&Mathf.Approximately(Clock(health),old)&&hullHits==1,"Rejected pipeline damage does not reset health regeneration");ship.OnDamagePipeline-=block;
            Invoke(health,"OnDestroy");Set(ship,"damageInvulnerableUntil",-1f);
            Check(ship.TryTakeDamage(1)&&Mathf.Approximately(Clock(health),old),"Destroyed regeneration component unsubscribes correctly");
        }
        finally{UnityEngine.Object.DestroyImmediate(root);EditorSceneManager.ClosePreviewScene(scene);}
        return new{passed=checks};
    }
}
