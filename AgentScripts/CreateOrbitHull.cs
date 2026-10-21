using System;
using UnityEditor;
using UnityEngine;

public static class CreateOrbitHull
{
    public const string DataPath = "Assets/Prefub/Data/Ship/Orbit.asset";
    public const string MetaPath = "Assets/Prefub/Data/ShipMeta/OrbitMeta.asset";
    public const string GameplayPath = "Assets/GameContent/Hulls/Gameplay/OrbitCraft.prefab";
    public const string VisualPath = "Assets/GameContent/Hulls/HighResVisual/OrbitHigh.prefab";
    public const string ContentPath = "Assets/GameContent/Hulls/Data/OrbitContent.asset";
    public const string SatellitePath = "Assets/Prefub/Abilities 1/OrbitSatellite.prefab";
    public const string ProgressionPath = "Assets/Prefub/Data/Ship/AbilityDamageProgression/OrbitAbilityDamageProgression.asset";

    public static string Main()
    {
        if (EditorApplication.isPlaying || EditorApplication.isCompiling || EditorApplication.isUpdating)
            throw new InvalidOperationException("Editor must be idle in Edit Mode.");
        foreach (string path in new[] { DataPath, MetaPath, GameplayPath, VisualPath, ContentPath, SatellitePath, ProgressionPath })
            if (AssetDatabase.LoadMainAssetAtPath(path) != null)
                throw new InvalidOperationException("Refusing to overwrite an existing asset: " + path);

        var ark = AssetDatabase.LoadAssetAtPath<ShipData>("Assets/Prefub/Data/Ship/Arkanoid.asset");
        var arkContent = AssetDatabase.LoadAssetAtPath<HullContentDefinition>("Assets/GameContent/Hulls/Data/ArcanoidContent.asset");
        var ghost = AssetDatabase.LoadAssetAtPath<ShipData>("Assets/Prefub/Data/Ship/Phantom.asset");
        ghost.GetMetaRuntimeStats(0).TryGetContract(out AbilityRecoveryShipMetaContract ghostRecovery);
        if (ark == null || arkContent == null || ghostRecovery == null)
            throw new InvalidOperationException("Missing source configuration.");

        int id = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:ShipData", new[] { "Assets" }))
            id = Mathf.Max(id, AssetDatabase.LoadAssetAtPath<ShipData>(AssetDatabase.GUIDToAssetPath(guid)).shipId + 1);
        foreach (string guid in AssetDatabase.FindAssets("t:HullContentDefinition", new[] { "Assets" }))
            if (int.TryParse(AssetDatabase.LoadAssetAtPath<HullContentDefinition>(AssetDatabase.GUIDToAssetPath(guid)).Id, out int used))
                id = Mathf.Max(id, used + 1);

        Copy(AssetDatabase.GetAssetPath(ark), DataPath);
        var data = AssetDatabase.LoadAssetAtPath<ShipData>(DataPath);
        Undo.RecordObject(data, "Create Orbit ship data");
        data.shipId = id;
        Copy(AssetDatabase.GetAssetPath(ark.AbilityDamageProgressionConfig), ProgressionPath);
        data.SetAbilityDamageProgressionConfig(AssetDatabase.LoadAssetAtPath<ShipAbilityDamageProgressionConfig>(ProgressionPath));

        CreateSatellite();
        Copy(AssetDatabase.GetAssetPath(arkContent.GameplayPrefab), GameplayPath);
        GameObject root = PrefabUtility.LoadPrefabContents(GameplayPath);
        try
        {
            root.name = "OrbitCraft";
            UnityEngine.Object.DestroyImmediate(root.GetComponent<ArkanoidStasisActiveAbility>());
            UnityEngine.Object.DestroyImmediate(root.GetComponent<ArkanoidPassiveAbility>());
            var passive = root.AddComponent<OrbitPassiveAbility>();
            var active = root.AddComponent<OrbitExpansionActiveAbility>();
            var parent = root.GetComponent<ParentShip>();
            Set(parent, "ShipData", data);
            Set(parent, "passiveAbility", passive);
            Set(parent, "activeAbility", active);
            Set(passive, "owner", parent);
            Set(passive, "satellitePrefab", AssetDatabase.LoadAssetAtPath<GameObject>(SatellitePath).GetComponent<OrbitSatellite>());
            Set(active, "owner", parent);
            Set(active, "orbit", passive);
            var activeSO = new SerializedObject(active);
            AbilityMetaRecoverySettings recovery = ghostRecovery.Settings;
            activeSO.FindProperty("abilityMode").enumValueIndex = (int)UltimateAbilityMode.Toggle;
            activeSO.FindProperty("cooldown").floatValue = recovery.Cooldown;
            activeSO.FindProperty("toggleMaximumTime").floatValue = recovery.ToggleMaximumTime;
            activeSO.FindProperty("toggleTimeRemaining").floatValue = recovery.ToggleMaximumTime;
            activeSO.FindProperty("toggleTimeCostPerSecond").floatValue = recovery.ToggleTimeCostPerSecond;
            activeSO.FindProperty("toggleRechargeStartTime").floatValue = recovery.ToggleRechargeStartTime;
            activeSO.FindProperty("toggleRechargeDuration").floatValue = recovery.ToggleRechargeDuration;
            activeSO.ApplyModifiedPropertiesWithoutUndo();
            Set(root.GetComponent<HullLoadoutDefinition>(), "shipData", data);
            root.GetComponent<SpriteRenderer>().color = new Color(0.45f, 0.9f, 1f);
            PrefabUtility.SaveAsPrefabAsset(root, GameplayPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }

        Copy(AssetDatabase.GetAssetPath(ark.ShipMetaConfig), MetaPath);
        var meta = AssetDatabase.LoadAssetAtPath<ShipMetaConfig>(MetaPath);
        Undo.RecordObject(meta, "Create Orbit meta configuration");
        Set(meta, "sourceShipData", data);
        Set(meta, "sourceHullPrefab", AssetDatabase.LoadAssetAtPath<GameObject>(GameplayPath).GetComponent<ParentShip>());
        meta.TryRemoveContractFromAllLevels(typeof(ArkanoidShipMetaContract));
        meta.TryRemoveContractFromAllLevels(typeof(AbilityRecoveryShipMetaContract));
        if (!meta.TryAddContractToAllLevels(typeof(OrbitShipMetaContract))
            || !meta.TryAddContractToAllLevels(typeof(AbilityRecoveryShipMetaContract)))
            throw new InvalidOperationException("Failed to configure Orbit contracts.");
        data.SetShipMetaConfig(meta);
        EditorUtility.SetDirty(data);
        EditorUtility.SetDirty(meta);

        Copy(AssetDatabase.GetAssetPath(arkContent.Prefab), VisualPath);
        root = PrefabUtility.LoadPrefabContents(VisualPath);
        try
        {
            root.name = "OrbitHigh";
            Set(root.GetComponent<HullLoadoutDefinition>(), "shipData", data);
            foreach (var sprite in root.GetComponentsInChildren<SpriteRenderer>(true))
                if (sprite.GetComponent<CraftWeaponSlotAnchor>() == null)
                    sprite.color = new Color(0.45f, 0.9f, 1f);
            PrefabUtility.SaveAsPrefabAsset(root, VisualPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }

        Copy(AssetDatabase.GetAssetPath(arkContent), ContentPath);
        var content = AssetDatabase.LoadAssetAtPath<HullContentDefinition>(ContentPath);
        Undo.RecordObject(content, "Create Orbit hull content");
        var so = new SerializedObject(content);
        so.FindProperty("id").stringValue = id.ToString();
        so.FindProperty("displayName").stringValue = "Orbit";
        so.FindProperty("rarity").enumValueIndex = (int)ContentRarity.Rare;
        so.FindProperty("passiveAbilityDescription").stringValue = "Three orbiting satellites destroy enemy projectiles on direct contact and deal periodic contact damage to enemies. Gaps remain between the satellites.";
        so.FindProperty("activeAbilityDescription").stringValue = "Hold to gradually expand and accelerate the orbit. Release to smoothly return to the normal orbit. Satellites continue intercepting throughout.";
        so.FindProperty("data").objectReferenceValue = data;
        so.FindProperty("prefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(VisualPath);
        so.FindProperty("gameplayPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(GameplayPath);
        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(content);

        foreach (string guid in AssetDatabase.FindAssets("t:HullCatalog", new[] { "Assets" }))
        {
            var catalog = AssetDatabase.LoadAssetAtPath<HullCatalog>(AssetDatabase.GUIDToAssetPath(guid));
            Undo.RecordObject(catalog, "Add Orbit to hull catalog");
            so = new SerializedObject(catalog);
            var hulls = so.FindProperty("hulls");
            hulls.arraySize++;
            hulls.GetArrayElementAtIndex(hulls.arraySize - 1).objectReferenceValue = content;
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(catalog);
        }
        AssetDatabase.SaveAssets();
        return Newtonsoft.Json.JsonConvert.SerializeObject(new { id, data = DataPath, meta = MetaPath, gameplay = GameplayPath, visual = VisualPath, content = ContentPath, satellite = SatellitePath, stats = data.GetMetaRuntimeStats(0) });
    }

    private static void Copy(string source, string destination)
    {
        if (string.IsNullOrEmpty(source) || !AssetDatabase.CopyAsset(source, destination))
            throw new InvalidOperationException("Failed to copy " + source + " to " + destination);
    }

    private static void Set(UnityEngine.Object target, string field, UnityEngine.Object value)
    {
        var so = new SerializedObject(target);
        so.FindProperty(field).objectReferenceValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void CreateSatellite()
    {
        var ball = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefub/Abilities 1/Ball.prefab");
        var root = new GameObject("OrbitSatellite");
        root.SetActive(false);
        try
        {
            root.layer = ball.layer;
            var collider = root.AddComponent<CircleCollider2D>();
            collider.isTrigger = true;
            collider.radius = 0.18f;
            var body = root.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            body.gravityScale = 0f;
            body.useFullKinematicContacts = true;
            var satellite = root.AddComponent<OrbitSatellite>();
            var visual = new GameObject("Visual");
            visual.transform.SetParent(root.transform, false);
            var sprite = visual.AddComponent<SpriteRenderer>();
            sprite.sprite = ball.GetComponentInChildren<SpriteRenderer>(true).sprite;
            sprite.sharedMaterial = ball.GetComponentInChildren<SpriteRenderer>(true).sharedMaterial;
            sprite.color = new Color(0.3f, 0.85f, 1f);
            sprite.sortingOrder = 98;
            visual.transform.localScale = Vector3.one * (0.36f / sprite.sprite.bounds.size.x);
            Set(satellite, "visual", sprite);
            root.SetActive(true);
            PrefabUtility.SaveAsPrefabAsset(root, SatellitePath);
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }
}
