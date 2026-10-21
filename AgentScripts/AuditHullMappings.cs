using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
public static class AuditHullMappings
{
    static object Describe(GameObject prefab, ShipData expected)
    {
        if(prefab==null)return null;
        var loadout=prefab.GetComponent<HullLoadoutDefinition>();var body=prefab.GetComponentInChildren<BodyData>(true);var ship=prefab.GetComponent<ParentShip>();
        var slots=new List<string>();if(loadout!=null)foreach(var p in loadout.WeaponPlatforms)slots.Add(p.SlotId);
        var sprites=new List<string>();foreach(var r in prefab.GetComponentsInChildren<SpriteRenderer>(true))if(r.sprite!=null)sprites.Add(r.name+":"+r.sprite.name);
        var issues=new List<string>();if(loadout!=null)loadout.CollectValidationIssues(issues);
        return new{path=AssetDatabase.GetAssetPath(prefab),loadoutData=loadout!=null?AssetDatabase.GetAssetPath(loadout.ShipData):null,loadoutMatches=loadout==null||loadout.ShipData==expected,bodyData=body!=null&&body.VisualConfig!=null?AssetDatabase.GetAssetPath(body.VisualConfig.ShipData):null,bodyMatches=body==null||body.VisualConfig!=null&&body.VisualConfig.ShipData==expected,slots,issues,sprites,shipCount=prefab.GetComponentsInChildren<ParentShip>(true).Length,active=ship!=null&&ship.ActiveAbility!=null?ship.ActiveAbility.GetType().Name:null,passive=ship!=null&&ship.PassiveAbility!=null?ship.PassiveAbility.GetType().Name:null};
    }
    public static object Run()
    {
        var catalog=AssetDatabase.LoadAssetAtPath<HullCatalog>("Assets/GameContent/Catalogs/HullCatalog.asset");var rows=new List<object>();foreach(var h in catalog.Hulls)rows.Add(new{name=h.DisplayName,id=h.Id,data=AssetDatabase.GetAssetPath(h.Data),meta=h.Data!=null?AssetDatabase.GetAssetPath(h.Data.ShipMetaConfig):null,blueprint=Describe(h.Prefab,h.Data),gameplay=Describe(h.GameplayPrefab,h.Data)});return rows;
    }
}
