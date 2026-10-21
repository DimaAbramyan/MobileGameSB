using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
public static class VerifyRemainingHullMappings
{
    static int checks;
    static void Check(bool value,string name){if(!value)throw new Exception(name);checks++;}
    public static object Run()
    {
        checks=0;var catalog=AssetDatabase.LoadAssetAtPath<HullCatalog>("Assets/GameContent/Catalogs/HullCatalog.asset");var weapons=AssetDatabase.LoadAssetAtPath<WeaponCatalog>("Assets/GameContent/Catalogs/WeaponCatalog.asset");var service=new ContentCatalogService(catalog,weapons);var ids=new HashSet<string>();var shipIds=new HashSet<int>();var rows=new List<object>();int selectable=0;
        foreach(var hull in catalog.Hulls)
        {
            Check(ids.Add(hull.Id),"Unique content ID: "+hull.DisplayName);Check(shipIds.Add(hull.Data.shipId),"Unique Ship ID: "+hull.DisplayName);
            if(!hull.IsAvailableForCrafting)continue;selectable++;
            var game=hull.GameplayPrefab.GetComponent<ParentShip>();Check(game!=null&&game.ShipData==hull.Data,"Gameplay ShipData: "+hull.DisplayName);
            Check(hull.GameplayPrefab.GetComponentsInChildren<ParentShip>(true).Length==1,"Single gameplay owner: "+hull.DisplayName);
            var preview=hull.Prefab.GetComponent<HullLoadoutDefinition>();var runtime=hull.GameplayPrefab.GetComponent<HullLoadoutDefinition>();
            Check(preview!=null&&runtime!=null&&preview.ShipData==hull.Data&&runtime.ShipData==hull.Data,"Blueprint and gameplay loadout data: "+hull.DisplayName);
            Check(preview.IsConfigurationValid(out _)&&runtime.IsConfigurationValid(out _),"Valid platforms: "+hull.DisplayName);
            Check(preview.WeaponPlatforms.Count==runtime.WeaponPlatforms.Count,"Matching slot count: "+hull.DisplayName);
            foreach(var platform in preview.WeaponPlatforms)Check(runtime.TryGetWeaponPlatform(platform.SlotId,out _),"Matching slot ID: "+hull.DisplayName+"/"+platform.SlotId);
            var meta=hull.Data.ShipMetaConfig;Check(meta!=null&&meta.SourceShipData==hull.Data,"Meta data matches hull: "+hull.DisplayName);
            Check(meta.SourceHullPrefab!=null&&meta.SourceHullPrefab.ShipData==hull.Data,"Meta source gameplay matches hull: "+hull.DisplayName);
            foreach(var type in meta.GetSupportedContractTypes())for(int level=0;level<meta.LevelCount;level++)Check(meta.GetLevel(level).ContainsContract(type),"Meta contract present: "+hull.DisplayName+"/"+type.Name);
            Check(service.TryGetHull(hull.Id,hull.Data.shipId,out var resolved)&&resolved==hull,"Blueprint resolves intended gameplay: "+hull.DisplayName);
            rows.Add(new{name=hull.DisplayName,slots=preview.WeaponPlatforms.Count});
        }
        Check(selectable==8,"Eight selectable hulls");
        var huskar=AssetDatabase.LoadAssetAtPath<HullContentDefinition>("Assets/GameContent/Hulls/Data/HuskarContent.asset");Check(!huskar.IsAvailableForCrafting&&service.TryGetHull(huskar.Id,huskar.Data.shipId,out var oldHuskar)&&oldHuskar==huskar,"Huskar excluded while old saves remain loadable");
        var blade=AssetDatabase.LoadAssetAtPath<HullContentDefinition>("Assets/GameContent/Hulls/Data/BladeContent.asset");Check(service.TryGetHull("1",blade.Data.shipId,out var oldBlade)&&oldBlade==blade,"Old duplicated Blade ID recovers by saved ShipData ID");
        return new{passed=checks,hulls=rows};
    }
}
