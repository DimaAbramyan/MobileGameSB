using System;
using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class VerifyStatBindings
{
    static int checks;
    static readonly List<UnityEngine.Object> disposable = new();
    static void Check(bool value, string message) { checks++; if (!value) throw new Exception(message); }
    static void Set(object target, string field, object value) => target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    static T New<T>() where T : ScriptableObject { var value = ScriptableObject.CreateInstance<T>(); disposable.Add(value); return value; }
    static WeaponProjectileSlot Slot(ProjectileData data, bool child = false)
    { var slot = new WeaponProjectileSlot(); Set(slot, "projectile", data); Set(slot, "spawnedByAnotherProjectile", child); return slot; }
    static void Child(ProjectileData parent, ProjectileData child)
    { parent.TryAddContract(typeof(SpawnSecondaryProjectileContract)); parent.TryGetContract(out SpawnSecondaryProjectileContract c); Set(c, "secondaryProjectile", child); }
    static StatValueDefinition Find(StatValueCatalog c, Type type, string property, int depth = -1)
    {
        foreach (var d in c.Values) if (d.ProviderType == type && d.Property == property && (depth < 0 || d.ProjectileDepth == depth)) return d;
        throw new Exception("Missing descriptor: " + type.Name + "." + property + " depth " + depth);
    }
    static StatDisplayCell Cell(StatValueDefinition d) => new() { useBinding = true, sourcePath = d.SourcePath, statId = d.Id, sourceAsset = d.SourceAsset };
    static float Number(StatValueDefinition d, IStatValueResolver context) => Convert.ToSingle(d.Read(context).Value);

    public static object Run()
    {
        checks = 0;
        var scene = EditorSceneManager.NewPreviewScene();
        try
        {
            var meta = New<WeaponMetaConfig>();
            var root = New<ProjectileData>(); root.name = "Root";
            var child = New<ProjectileData>(); child.name = "Child";
            var grand = New<ProjectileData>(); grand.name = "Grandchild";
            var great = New<ProjectileData>(); great.name = "Greatgrandchild";
            Set(root, "damage", 10f); Set(child, "damage", 7f); Set(grand, "damage", 3f);
            Child(root, child); Child(child, grand); Child(grand, great);
            grand.TryAddContract(typeof(ProjectileExplosionContract));
            var slots = new List<WeaponProjectileSlot> { Slot(root), Slot(root), Slot(child, true) };
            Set(meta, "projectileSlots", slots);
            var catalog = StatCatalogBuilder.Build(meta);
            var depth2 = Find(catalog, typeof(ProjectileExplosionContract), "ExplosionRadius", 2);
            Check(Find(catalog, typeof(ProjectileData), "Damage", 3) != null, "Recursive third child is discoverable");
            int repeated = 0; foreach (var d in catalog.Values) if (d.Projectile == grand && d.Property == "Damage" && d.ProviderType == typeof(ProjectileData)) repeated++;
            Check(repeated == 2, "Shared configuration has distinct source paths");
            Check(catalog.Warnings.Count == 0, "Valid shared graph has no cycle warning");
            var selection = Cell(depth2);
            // Unity JSON asset references require a persistent source; it intentionally drops
            // references to the transient ScriptableObjects used by the recursive fixture above.
            var serializationMeta = New<WeaponMetaConfig>();
            var persistentProjectile = AssetDatabase.LoadAssetAtPath<ProjectileData>(AssetDatabase.GUIDToAssetPath(AssetDatabase.FindAssets("t:ProjectileData")[0]));
            Set(serializationMeta, "projectileSlots", new List<WeaponProjectileSlot> { Slot(persistentProjectile) });
            var serializedSelection = Cell(Find(StatCatalogBuilder.Build(serializationMeta), typeof(ProjectileData), "Damage", 0));
            var configuredRow = new StatDisplayRow(); configuredRow.cells.Add(serializedSelection); serializationMeta.StatDisplay.secondaryStats.rows.Add(configuredRow);
            var restoredMeta = New<WeaponMetaConfig>(); EditorJsonUtility.FromJsonOverwrite(EditorJsonUtility.ToJson(serializationMeta), restoredMeta);
            Check(StatCatalogBuilder.Build(restoredMeta).TryFind(restoredMeta.StatDisplay.secondaryStats.rows[0].cells[0], out _), "Binding path, stat ID and real asset reference serialize together");
            slots.Reverse(); root.name = "Renamed";
            Check(StatCatalogBuilder.Build(meta).TryFind(selection, out _), "Slot reorder and asset rename preserve identity");
            Child(great, root);
            Check(StatCatalogBuilder.Build(meta).Warnings.Count == 2, "Each cyclic root is bounded and diagnosed");
            Set(great, "contracts", new List<ProjectileDataContract>());
            var replacement = New<ProjectileData>(); replacement.TryAddContract(typeof(ProjectileExplosionContract));
            child.TryGetContract(out SpawnSecondaryProjectileContract spawn); Set(spawn, "secondaryProjectile", replacement);
            Check(!StatCatalogBuilder.Build(meta).TryFind(selection, out _), "Replacing source does not silently rebind");
            Set(spawn, "secondaryProjectile", grand);
            grand.TryRemoveContract(typeof(ProjectileExplosionContract));
            Check(!StatCatalogBuilder.Build(meta).TryFind(selection, out _), "Removed contract remains missing");
            grand.TryAddContract(typeof(ProjectileExplosionContract));
            root.TryAddContract(typeof(ProjectileEnemyDebuffsContract)); root.TryGetContract(out ProjectileEnemyDebuffsContract debuffs);
            var heat = New<EnemyHeatDebuffConfig>();
            Set(debuffs, "debuffs", new List<EnemyDebuffApplication> { new(heat, 2) });
            Check(Find(StatCatalogBuilder.Build(meta), typeof(EnemyHeatDebuffConfig), "BurningDamagePerTick") != null, "Nested debuff config is discoverable");
            root.TryAddContract(typeof(ProjectileDamageSourcesContract)); root.TryGetContract(out ProjectileDamageSourcesContract sources);
            Set(sources, "sources", new List<ProjectileDamageSource> { new() });
            Check(Find(StatCatalogBuilder.Build(meta), typeof(ProjectileDamageSource), "Id").Kind == StatValueKind.Text, "Text supported");
            Check(Find(catalog, typeof(ProjectileData), "DamageType").Kind == StatValueKind.Enumeration, "Enums supported");
            Check(depth2.FormatValue(12.25f, "{0:0.0} m").EndsWith(" m"), "Custom composite format");
            bool invalidFormat = false; try { depth2.FormatValue(1f, "{1}"); } catch (FormatException) { invalidFormat = true; }
            Check(invalidFormat, "Bad format is explicit");
            var group = new FanFireAngleGroup(); Check(group.Clone().StatSourceId == group.StatSourceId, "Nested identity survives Meta level clone");
            var spawnGroup = new DictatorTurretSpawnGroup(Vector2.zero); Check(spawnGroup.Clone().StatSourceId == spawnGroup.StatSourceId, "Turret group identity survives clone");

            var liveMeta = New<WeaponMetaConfig>(); Set(liveMeta.GetLevel(0).BasicStats, "damage", 20f);
            var data = New<WeaponData>(); Set(data, "weaponMetaConfig", liveMeta);
            Set(data, "levelBonuses", new List<WeaponLevelBonusConfig> { new(), new() }); data.SetMaxLevel(2);
            Set(data.LevelBonuses[1], "damagePercent", 25f);
            var owner = new GameObject("Stat runtime fixture"); owner.SetActive(false); SceneManager.MoveGameObjectToScene(owner, scene);
            var weapon = owner.AddComponent<Weapon>(); weapon.weaponData = data; weapon.SetLevel(1);
            var controller = owner.AddComponent<WeaponController>();
            var damage = Find(StatCatalogBuilder.Build(liveMeta), typeof(WeaponMetaBasicStats), "Damage");
            var row = new StatDisplayRow(); row.cells.Add(Cell(damage)); liveMeta.StatDisplay.baseStats.rows.Add(row);
            var panel = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(StatBlocksPreviewWindow.PanelPath), scene);
            var view = panel.GetComponent<StatBlocksView>(); view.Bind(weapon, controller);
            var texts = panel.GetComponentsInChildren<TMP_Text>(true);
            Check(Number(damage, new StatRuntimeContext(weapon)) == 20f, "Runtime baseline damage 20");
            int rowCount = panel.transform.GetChild(0).childCount;
            weapon.SetLevel(2);
            Check(Number(damage, new StatRuntimeContext(weapon)) == 25f, "Weapon upgrade reads final damage 25");
            bool has25 = false; foreach (var text in texts) if (text.text == "25") has25 = true;
            Check(has25, "Level event updates UI immediately");
            Check(panel.transform.GetChild(0).childCount == rowCount && panel.GetComponentsInChildren<TMP_Text>(true)[0] == texts[0], "Update keeps row/cell objects");
            var reload = Find(StatCatalogBuilder.Build(liveMeta), typeof(WeaponMetaBasicStats), "ReloadTime");
            var context = new StatRuntimeContext(weapon, controller);
            float initialReload = Number(reload, context); controller.ActivateFireRateMultiplier(2f, 10f);
            Check(Mathf.Approximately(Number(reload, context), initialReload / 2f), "Transient fire-rate modifier uses shooting multiplier");
            Check(Find(StatCatalogBuilder.Build(liveMeta), typeof(WeaponMetaBasicStats), "ShotsPerSecond").Read(context).State == StatValueState.Runtime, "Computed fire rate has runtime resolver");
            row.cells.Clear(); view.Rebuild();
            Check(Mathf.Approximately(weapon.RuntimeProjectileParameters.damage, 25f), "Removing UI cell does not remove gameplay stat");

            Set(data, "weaponMetaConfig", meta); data.SynchronizeProjectileLevelBonuses(); weapon.SetLevel(1);
            typeof(Weapon).GetMethod("SetIdenticalWeaponMultipliers", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(weapon, new object[] { 1, 1f, 2f });
            var firstRoot = slots.Find(s => !s.SpawnedByAnotherProjectile);
            Check(weapon.TryGetFiredProjectileStats(firstRoot.Id, root, 0, out var primary) && primary.Damage == 20f, "Root damage uses firing parameters");
            Check(weapon.TryGetFiredProjectileStats(firstRoot.Id, child, 1, out var first) && first.Damage == 28f, "Mono first child preserves existing override semantics");
            Check(weapon.TryGetFiredProjectileStats(firstRoot.Id, grand, 2, out var second) && second.Damage == 3f, "Second child does not inherit root override");
            Set(root, "simulationBackend", ProjectileSimulationBackend.Entities);
            Check(weapon.TryGetFiredProjectileStats(firstRoot.Id, child, 1, out var ecsFirst) && ecsFirst.Damage == 14f, "ECS child uses existing ECS override path");
            Set(root, "deliveryType", ProjectileDeliveryType.Contact);
            Check(!weapon.TryGetFiredProjectileStats(firstRoot.Id, child, 1, out _), "Contact delivery does not claim the projectile child override");

            var shipMeta = New<ShipMetaConfig>();
            shipMeta.GetLevel(0).TryAddContract(new AbilityRecoveryShipMetaContract());
            shipMeta.GetLevel(0).TryAddContract(new OrbitShipMetaContract());
            var shipCatalog = StatCatalogBuilder.Build(shipMeta);
            Check(Find(shipCatalog, typeof(AbilityMetaRecoverySettings), "Cooldown") != null, "Nested active recovery settings selectable");
            Check(Find(shipCatalog, typeof(OrbitShipMetaContract), "ContactDamage") != null, "Unique passive stats selectable");
            var shipData = New<ShipData>(); Set(shipData, "shipMetaConfig", shipMeta);
            var shipObject = new GameObject("Stat ship fixture"); shipObject.SetActive(false); SceneManager.MoveGameObjectToScene(shipObject, scene);
            var ship = shipObject.AddComponent<ParentShip>(); ship.ShipData = shipData;
            var active = shipObject.AddComponent<BlackHoleActive>(); Set(ship, "activeAbility", active);
            active.ApplyShipMetaStats(shipMeta.GetRuntimeStats(0));
            var liveShip = new StatRuntimeContext(ship);
            var cooldown = Find(shipCatalog, typeof(AbilityMetaRecoverySettings), "Cooldown");
            Check(cooldown.Read(liveShip).State == StatValueState.Runtime && Number(cooldown, liveShip) == active.CooldownDuration, "Active ability reads actual recovery");
            var orbit = shipObject.AddComponent<OrbitPassiveAbility>(); Set(ship, "passiveAbility", orbit);
            orbit.ApplyShipMetaStats(shipMeta.GetRuntimeStats(0)); orbit.ApplyBattleAbilityDamageMultiplier(2f);
            var contact = Find(shipCatalog, typeof(OrbitShipMetaContract), "ContactDamage");
            Check(Number(contact, liveShip) == orbit.RuntimeContactDamage, "Passive damage uses same getter as damage delivery");
            var baseOnly = Find(StatCatalogBuilder.Build(meta), typeof(EnemyHeatDebuffConfig), "BurningDamagePerTick");
            Check(baseOnly.Read(new StatRuntimeContext(weapon)).State == StatValueState.BaseConfiguration, "Target-dependent values marked base rather than final");
            return new { passed = true, checks, childDepth = 3, runtimeUpgrade = "20 -> 25", spawnedProjectiles = 0, editedPersistentAssets = 0 };
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(scene);
            foreach (var value in disposable) if (value != null) UnityEngine.Object.DestroyImmediate(value);
            disposable.Clear();
        }
    }
}
