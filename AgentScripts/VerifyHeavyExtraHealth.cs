using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public static class VerifyHeavyExtraHealth
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static int checks;
    public static object Run()
    {
        if (EditorApplication.isPlaying || EditorApplication.isCompiling || EditorApplication.isUpdating)
            throw new Exception("Idle Editor required.");
        checks = 0;
        var catalog = AssetDatabase.LoadAssetAtPath<HullCatalog>("Assets/GameContent/Catalogs/HullCatalog.asset");
        foreach (var hull in catalog.Hulls)
            Check(hull.GameplayPrefab.GetComponent<ExtraHealthPassive>() != null, "Catalog prefab supports ExtraHealth: " + hull.name);
        var teamObject = new GameObject("Heavy verification team");
        var hud = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/GameContent/Hulls/HighResVisual/PlayerShipHUD.prefab"));
        object row = null;
        Type rowType = typeof(UIController).GetNestedType("PlayerShipHUD", BindingFlags.NonPublic);
        try
        {
            var team = teamObject.AddComponent<ShipSelect>();
            ParentShip heavy = Spawn("Assets/Prefub/Ships/Heavy.prefab", team.transform);
            ParentShip ally = Spawn("Assets/Prefub/Ships/Prism.prefab", team.transform);
            ParentShip other = Spawn("Assets/Prefub/Ships/Arkanoid.prefab", team.transform);
            heavy.SetHealthPoints(3f); ally.SetHealthPoints(1f); other.SetHealthPoints(2f);
            var ability = heavy.GetComponent<ShieldCreateAbility>();
            var extra = ally.GetComponent<ExtraHealthPassive>();
            Call(ability, "Start");
            Near(ability.CooldownRemaining, 45f, "Heavy starts with its full configured cooldown");
            Near(ability.CooldownRemaining01, 1f, "Initial cooldown is visible to HUD");
            ability.TryActivate(heavy);
            Near(extra.ExtraHealth, 0f, "Initial cooldown blocks activation");
            ability.ReduceCooldown(45f);
            Near(ability.CooldownRemaining, 0f, "Ability becomes ready when initial recovery ends");
            row = Activator.CreateInstance(rowType, true);
            Set(row, "hudRoot", hud.GetComponent<RectTransform>());
            Set(row, "healthBar", hud.transform.Find("HealthBar").GetComponent<StatBar>());
            Set(row, "shieldBar", hud.transform.Find("ShieldBar").GetComponent<StatBar>());
            var extraBar = hud.transform.Find("ExtraHealthBar").GetComponent<StatBar>();
            var slider = extraBar.GetComponent<Slider>();
            var text = extraBar.GetComponentInChildren<TMP_Text>(true);
            Set(row, "extraHealthBar", extraBar); Set(row, "extraHealthValueText", text);
            rowType.GetMethod("Bind").Invoke(row, new object[] { ally, null });
            Near(slider.value, 0f, "Initially empty ExtraHealthBar");
            float heavyShield = heavy.CurrentShieldPoints;
            ability.TryActivate(heavy);
            Near(extra.ExtraHealth, 1f, "Lowest absolute health receives one extra health");
            Near(ally.CurrentHealthPoints, 1f, "Grant does not heal ordinary hull health");
            Near(heavy.CurrentShieldPoints, heavyShield, "Heavy shield is not spent");
            Near(ability.CooldownRemaining, 45f, "Existing standard recovery retained");
            Near(slider.value, slider.maxValue, "ExtraHealthBar updates on grant");
            if (text != null) Check(text.text.Contains("1"), "Optional extra health text updates");
            ability.TryActivate(heavy); Near(extra.ExtraHealth, 1f, "Cooldown blocks repeated activation");
            Check(!ability.Activate(heavy), "A full buffer rejects a grant");
            Near(extra.ExtraHealth, 1f, "Repeated grant cannot exceed maximum");
            Near(extra.MaximumExtraHealth, 1f, "Grant never increases configured maximum");
            ability.RestoreAllRecoveryResources(); ability.TryActivate(heavy);
            Near(ability.CooldownRemaining, 0f, "Full buffer does not spend cooldown");
            extra.MaximumExtraHealth = 2f; Call(extra, "OnValidate");
            Near(extra.AddExtraHealth(20f), 1f, "Oversized grant returns only accepted health");
            Near(extra.ExtraHealth, 2f, "Oversized grant is capped");
            Near(slider.value, slider.maxValue, "Capacity event keeps bar correct");
            extra.Init(ally); extra.Init(ally);
            float shield = ally.CurrentShieldPoints;
            ally.TryTakeDamage(1000f);
            Near(extra.ExtraHealth, 1f, "Damage pipeline subscribes once and consumes one extra health");
            Near(ally.CurrentHealthPoints, 1f, "Extra health protects hull");
            Near(ally.CurrentShieldPoints, shield, "Existing ExtraHealth order protects shield too");
            Near(slider.value, (slider.minValue + slider.maxValue) * .5f, "ExtraHealthBar updates on damage");
            Call(extra, "OnDisable"); Call(extra, "OnEnable"); extra.Init(ally);
            ally.TryTakeDamage(1f); Near(extra.ExtraHealth, 0f, "Disable/enable does not duplicate pipeline subscriptions");
            Near(slider.value, 0f, "Empty bar after extra health is consumed");
            extra.GetType().GetMethod("HandleOverflow", Private).Invoke(extra, new object[] { 1f });
            Near(extra.ExtraHealth, 1f, "Existing overheal handler retained");
            Near(slider.value, (slider.minValue + slider.maxValue) * .5f, "Overheal notifies UI");
            extra.SetExtraHealth(100f); Near(extra.ExtraHealth, 2f, "Direct set is capped");
            extra.MaximumExtraHealth = .5f; Call(extra, "OnValidate");
            Near(extra.ExtraHealth, .5f, "Lowering Inspector maximum clamps current health");
            Near(slider.value, slider.maxValue, "Bar follows a lowered maximum");
            extra.SetExtraHealth(.25f);
            Near(extra.AddExtraHealth(1f), .25f, "Partial remaining capacity accepts only the remainder");
            extra.MaximumExtraHealth = 0f; Call(extra, "OnValidate");
            Near(extra.ExtraHealth, 0f, "Zero maximum clears extra health");
            Near(extra.AddExtraHealth(1f), 0f, "Zero maximum rejects grants");
            Set(ally, "<IsDestroyed>k__BackingField", true);
            var died = (Action)typeof(ParentShip).GetField("OnDied", Private).GetValue(ally);
            died?.Invoke(); Near(extra.ExtraHealth, 0f, "Death clears extra health");
            Near(slider.value, 0f, "Death clears ExtraHealthBar");
            Check(team.GetLowestHealthShip() == other, "Destroyed ship is excluded");
            other.SetHealthPoints(3f);
            var controller = teamObject.AddComponent<PlayerController>();
            Set(controller, "currentShip", other);
            Check(team.GetLowestHealthShip() == other, "Tied health prefers active ship, not ability owner");
            ability.RestoreAllRecoveryResources(); ability.TryActivate(heavy);
            Near(other.GetComponent<ExtraHealthPassive>().ExtraHealth, 1f, "Heavy grants to another active ship when health is tied");
            Near(heavy.GetComponent<ExtraHealthPassive>().ExtraHealth, 0f, "Ability owner is not hard-coded as recipient");
            heavy.SetHealthPoints(2f);
            Check(team.GetLowestHealthShip() == heavy, "Lower health wins over an active ship with higher health");
            heavy.SetHealthPoints(3f);
            Set(controller, "currentShip", heavy);
            Check(team.GetLowestHealthShip() == heavy, "Recipient follows current active ship after switching");
            ability.RestoreAllRecoveryResources(); ability.TryActivate(heavy);
            Near(heavy.GetComponent<ExtraHealthPassive>().ExtraHealth, 1f, "Heavy can grant extra health to itself");
            Set(heavy, "<IsDestroyed>k__BackingField", true); Set(other, "<IsDestroyed>k__BackingField", true);
            ability.RestoreAllRecoveryResources(); ability.TryActivate(heavy);
            Near(ability.CooldownRemaining, 0f, "No eligible target does not spend recovery");
            var meta = heavy.ShipData.GetMetaRuntimeStats(0);
            Check(meta.TryGetContract(out HeavyShieldShipMetaContract contract) && contract.ExtraHealthAmount == 1f,
                "Serialized Heavy Meta grants one unit");
            foreach (var ui in UnityEngine.Object.FindObjectsByType<UIController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (ui.gameObject.scene.path != "Assets/Scenes/Fighting/Fighting.unity") continue;
                var so = new SerializedObject(ui);
                foreach (string field in new[] { "playerShip1Hud", "playerShip2Hud" })
                {
                    Check(so.FindProperty(field + ".extraHealthBar").objectReferenceValue != null, "HUD bar bound: " + field);
                    Check(so.FindProperty(field + ".extraHealthValueText").objectReferenceValue != null, "HUD text bound: " + field);
                }
            }
            return new { passed = checks, tested = "lowest health, ties/self, dead exclusion, maximum/partial/zero limits, damage, overheal, lifecycle, HUD, cooldown, prefab/config references" };
        }
        finally
        {
            if (row != null) rowType.GetMethod("Unbind").Invoke(row, null);
            UnityEngine.Object.DestroyImmediate(hud);
            UnityEngine.Object.DestroyImmediate(teamObject);
        }
    }
    private static ParentShip Spawn(string path, Transform parent)
    {
        var root = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path), parent);
        var ship = root.GetComponent<ParentShip>(); ship.Awake();
        return ship;
    }
    private static void Set(object target, string name, object value) => target.GetType().GetField(name, Private).SetValue(target, value);
    private static void Call(object target, string name) => target.GetType().GetMethod(name, Private).Invoke(target, null);
    private static void Near(float a, float b, string name) => Check(Mathf.Abs(a - b) < .001f, name + ": " + a + " != " + b);
    private static void Check(bool ok, string name) { if (!ok) throw new Exception(name); checks++; }
}
