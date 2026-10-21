using System;
using System.Collections;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class VerifyWaveShipRevival
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private sealed class StopBeforeAdvance : Exception { }

    public static object Run()
    {
        if (EditorApplication.isPlaying || EditorApplication.isCompiling || EditorApplication.isUpdating)
            throw new Exception("Editor must be idle");
        int checks = 0;
        var root = new GameObject("Ship revival verification");
        root.SetActive(false);
        root.AddComponent<Rigidbody2D>();
        var player = root.AddComponent<PlayerController>();
        var select = root.AddComponent<ShipSelect>();
        var waveRoot = new GameObject("Wave completion verification");
        waveRoot.SetActive(false);
        var manager = waveRoot.AddComponent<WaveManager>();
        var gameOver = new GameObject("Test game over"); gameOver.SetActive(false);
        var wave = waveRoot.AddComponent<Wave>();
        try
        {
            Invoke(player, "Awake"); Invoke(select, "Awake");
            Set(manager, "playerController", player); Set(manager, "GameOver", gameOver);
            Set(manager, "enableDebugLogs", false);
            var first = CreateShip("Assets/Prefub/Ships/Arkanoid.prefab", root.transform, player, manager);
            var second = CreateShip("Assets/GameContent/Hulls/Gameplay/BlackHoleCraft.prefab", root.transform, player, manager);
            var visuals = (IList)Get(select, "shipVisuals");
            Type visualType = typeof(ShipSelect).GetNestedType("ShipVisual", BindingFlags.NonPublic);
            visuals.Add(Activator.CreateInstance(visualType, new object[] { first }));
            visuals.Add(Activator.CreateInstance(visualType, new object[] { second }));
            Invoke(select, "ActivateShip", first);
            var hp = first.GetComponent<HealthRegeneration>();
            var shield = first.GetComponent<ShieldRegeneration>();
            first.SetHealthPoints(2f);
            first.HideShip(); SetElapsed(hp, 20f); Invoke(hp, "Update");
            first.ShowShip();
            Set(hp, "pauseStartedAt", Time.time - 120f);
            SetElapsed(hp, 140f); Invoke(hp, "Update");
            Check(first.CurrentHealthPoints == 2f
                    && Mathf.Abs(first.HealthRegenerationCooldownProgress - 20f / 60f) < 0.0001f,
                "Active hull preserves 20 seconds through 120 seconds of pause", ref checks);
            first.HideShip(); Invoke(hp, "Update");
            Check(first.CurrentHealthPoints == 2f
                    && Mathf.Abs(first.HealthRegenerationCooldownProgress - 20f / 60f) < 0.0001f,
                "Hidden hull resumes saved cooldown with 40 seconds remaining", ref checks);
            first.ShowShip(); first.HideShip(); first.ShowShip(); first.HideShip();
            Check(Mathf.Abs(first.HealthRegenerationCooldownProgress - 20f / 60f) < 0.0001f,
                "Repeated switching neither resets nor advances cooldown", ref checks);
            SetElapsed(hp, 59f); Invoke(hp, "Update");
            Check(first.CurrentHealthPoints == 2f, "Hidden hull waits 60 seconds", ref checks);
            SetElapsed(hp, 60f); Invoke(hp, "Update");
            Check(first.CurrentHealthPoints == 3f, "Hidden hull restores one HP per cooldown", ref checks);
            first.ShowShip(); first.SetHealthPoints(1f); first.SetShieldPoints(0f);
            int died = 0, revivedUi = 0, cleared = 0;
            first.OnDied += () => died++;
            player.OnTeamShipsRevived += () => revivedUi++;
            Check(first.TryTakeDamage(123f), "Fatal enemy hit accepted", ref checks);
            Check(first != null && first.IsDestroyed && !first.gameObject.activeSelf,
                "Destroyed hull retained inactive with its loadout", ref checks);
            Check(player.CurrentShip == second && second.IsVisible && !gameOver.activeSelf,
                "Surviving ship automatically takes over", ref checks);
            Check(first.CurrentHealthPoints == 0f && first.CurrentShieldPoints == 0f && !first.IsVisible,
                "Destroyed hull has zero health/shield and is hidden", ref checks);
            first.Dying();
            Check(died == 1 && !first.TryTakeDamage(1f), "Repeated death/hits rejected", ref checks);
            SetElapsed(hp, 1000f); SetElapsed(shield, 1000f);
            Invoke(hp, "Update"); Invoke(shield, "Update");
            Check(first.CurrentHealthPoints == 0f && first.CurrentShieldPoints == 0f,
                "Regeneration cannot resurrect destroyed hull", ref checks);
            select.SwitchShip();
            Check(player.CurrentShip == second, "Destroyed hull excluded from switching", ref checks);
            float survivorHp = second.CurrentHealthPoints, survivorShield = second.CurrentShieldPoints;
            int firstLevel = first.GetLevel();
            Set(wave, "waveManager", manager); Set(wave, "subWavesLeft", 2);
            Set(wave, "enableDebugLogs", false);
            manager.OnWaveCleared += () => { cleared++; throw new StopBeforeAdvance(); };
            wave.WhenSubWaveCleared();
            Check(first.IsDestroyed && cleared == 0, "Individual subwave does not resurrect", ref checks);
            try { wave.WhenSubWaveCleared(); } catch (StopBeforeAdvance) { }
            Check(cleared == 1 && revivedUi == 1, "Full-wave completion revives and refreshes HUD once", ref checks);
            Check(!first.IsDestroyed && first.gameObject.activeSelf && !first.IsVisible,
                "Revived hull stays in reserve", ref checks);
            Check(first.CurrentHealthPoints == 1f && first.CurrentShieldPoints == 0f,
                "Revival gives exactly 1 HP and zero shield", ref checks);
            Check(first.GetLevel() == firstLevel && player.CurrentShip == second,
                "Weapon level and current ship preserved", ref checks);
            Check(second.CurrentHealthPoints == survivorHp && second.CurrentShieldPoints == survivorShield,
                "Living hull stats unchanged", ref checks);
            Check(first.HealthRegenerationCooldownProgress == 0f && first.ShieldRegenerationCooldownProgress == 0f,
                "Both regeneration timers reset after revival", ref checks);
            Invoke(hp, "Update"); Invoke(shield, "Update");
            Check(first.CurrentHealthPoints == 1f && first.CurrentShieldPoints == 0f,
                "No immediate regeneration after revival", ref checks);
            Check(first.ActiveAbility == null || first.ActiveAbility.RecoveryNeed > 0f,
                "Ability starts recovering after revival", ref checks);
            wave.WhenSubWaveCleared();
            Check(cleared == 1 && player.ReviveDefeatedShips() == 0,
                "Repeated completion cannot reset living hulls", ref checks);
            SetElapsed(shield, 30f); Invoke(shield, "Update");
            Check(first.CurrentShieldPoints == 1f, "Revived shield recovers one unit after 30 seconds", ref checks);
            SetElapsed(hp, 60f); Invoke(hp, "Update");
            Check(first.CurrentHealthPoints == 2f, "Revived hidden hull recovers one HP after 60 seconds", ref checks);
            select.SwitchShip();
            Check(player.CurrentShip == first && first.IsVisible, "Revived hull can be selected again", ref checks);
            VerifyAbilityModes(first.ActiveAbility, ref checks);
            // Last-hull defeat still ends battle and cannot be undone by a delayed clear callback.
            first.Dying(); second.Dying();
            Check(gameOver.activeSelf && first.IsDestroyed && second.IsDestroyed,
                "All hulls destroyed still means game over", ref checks);
            manager.CompleteCurrentWave();
            Check(first.IsDestroyed && second.IsDestroyed && cleared == 1,
                "Completion after defeat cannot resurrect team", ref checks);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
            UnityEngine.Object.DestroyImmediate(waveRoot);
            UnityEngine.Object.DestroyImmediate(gameOver);
        }
        return new { passed = checks };
    }

    private static ParentShip CreateShip(string path, Transform parent, PlayerController player, WaveManager manager)
    {
        var clone = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path), parent);
        var ship = clone.GetComponent<ParentShip>(); ship.Awake();
        Set(ship, "playerController", player); Set(ship, "waveManager", manager);
        Invoke(clone.GetComponent<HealthRegeneration>(), "Awake");
        Invoke(clone.GetComponent<ShieldRegeneration>(), "Awake");
        if (ship.ActiveAbility != null) Set(ship.ActiveAbility, "owner", ship);
        return ship;
    }
    private static void VerifyAbilityModes(ActiveAbility ability, ref int checks)
    {
        if (ability == null) throw new Exception("Fixture requires a real ability");
        foreach (UltimateAbilityMode mode in Enum.GetValues(typeof(UltimateAbilityMode)))
        {
            Set(ability, "abilityMode", mode); Set(ability, "cooldown", 12f);
            Set(ability, "maxCharges", 3); Set(ability, "currentCharges", 3);
            Set(ability, "toggleTimeRemaining", 3f);
            ability.RestartRecovery();
            Check(ability.CooldownRemaining == 12f, "Full cooldown for ability mode " + mode, ref checks);
            if (mode == UltimateAbilityMode.Charges)
                Check(ability.CurrentCharges == 0, "Charges depleted on revival", ref checks);
            if (mode == UltimateAbilityMode.Toggle)
                Check(ability.ToggleTimeRemaining == 0f && !ability.IsToggleActive,
                    "Toggle reserve depleted and ability released", ref checks);
        }
    }
    private static FieldInfo Field(object value, string name)
    {
        for (Type type = value.GetType(); type != null; type = type.BaseType)
        { var field = type.GetField(name, Private | BindingFlags.DeclaredOnly); if (field != null) return field; }
        throw new Exception("Field missing: " + name);
    }
    private static object Get(object value, string name) => Field(value, name).GetValue(value);
    private static void Set(object value, string name, object data) => Field(value, name).SetValue(value, data);
    private static void SetElapsed(object value, float seconds) => Set(value, "lastDamageTime", Time.time - seconds);
    private static void Invoke(object value, string name, params object[] args) => value.GetType().GetMethod(name, Private).Invoke(value, args);
    private static void Check(bool value, string message, ref int checks) { if (!value) throw new Exception(message); checks++; }
}
