using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zenject;

public sealed class WeaponSelectionInfoController : MonoBehaviour
{
    [Header("Flow")]
    [SerializeField] private CraftCreationFlowController craftCreationFlow;

    [Header("Top")]
    [SerializeField] private Image iconImage;
    [SerializeField] private TMP_Text weaponNameText;
    [SerializeField] private TMP_Text tierText;
    [SerializeField] private TMP_Text energyCostText;

    [Header("Damage")]
    [SerializeField] private TMP_Text damageTypeText;
    [SerializeField] private TMP_Text damageText;
    [SerializeField] private TMP_Text secondaryDamageText;

    [Header("Combat Stats")]
    [SerializeField] private TMP_Text cooldownText;
    [SerializeField] private TMP_Text rangeText;

    [Header("Additional Stats")]
    [SerializeField] private TMP_Text platformTierText;
    [SerializeField] private TMP_Text energyCostInfoText;

    [Header("Description")]
    [SerializeField] private TMP_Text descriptionText;

    private ContentProgressService contentProgressService;

    [Inject]
    private void Construct(ContentProgressService progressService)
    {
        contentProgressService = progressService;

        if (isActiveAndEnabled)
            Refresh(craftCreationFlow != null ? craftCreationFlow.FocusedWeapon : null);
    }

    private void OnEnable()
    {
        if (craftCreationFlow != null)
        {
            craftCreationFlow.WeaponFocusChanged += Refresh;
            craftCreationFlow.WeaponSlotFocusChanged += RefreshAssignedWeapon;
        }

        Refresh(craftCreationFlow != null ? craftCreationFlow.FocusedWeapon : null);
    }

    private void OnDisable()
    {
        if (craftCreationFlow != null)
        {
            craftCreationFlow.WeaponFocusChanged -= Refresh;
            craftCreationFlow.WeaponSlotFocusChanged -= RefreshAssignedWeapon;
        }
    }

    public void Show(WeaponContentDefinition weapon)
    {
        Refresh(weapon);
    }

    private void RefreshAssignedWeapon(string slotId)
    {
        WeaponContentDefinition weapon = craftCreationFlow != null
            ? craftCreationFlow.GetWeaponForSlot(slotId)
            : null;

        if (weapon != null)
            Refresh(weapon);
    }

    private void Refresh(WeaponContentDefinition weapon)
    {
        bool hasWeapon = weapon != null && weapon.Data != null;
        if (!hasWeapon)
        {
            Clear();
            return;
        }

        WeaponData data = weapon.Data;
        int upgradeLevel = CraftProgressionText.GetUpgradeLevel(
            weapon,
            contentProgressService);
        bool canPreviewUpgrade = craftCreationFlow == null
            && contentProgressService != null
            && contentProgressService.GetState(weapon).CanUpgrade;
        int nextUpgradeLevel = Mathf.Min(
            upgradeLevel + 1,
            weapon.MaxUpgradeLevel);
        WeaponRuntimeStats stats = CraftProgressionText.GetWeaponStatsAtLevel(
            weapon,
            upgradeLevel);
        WeaponRuntimeStats nextStats = CraftProgressionText.GetWeaponStatsAtLevel(
            weapon,
            nextUpgradeLevel);
        bool usesExplosionAsPrimaryDamage = TryGetPrimaryExplosionDamage(
            data,
            out ProjectileDamageSource explosionDamageSource);
        float primaryDamage = usesExplosionAsPrimaryDamage
            ? explosionDamageSource.Damage
            : stats.Damage;
        float nextPrimaryDamage = usesExplosionAsPrimaryDamage
            ? explosionDamageSource.Damage
            : nextStats.Damage;
        EnemyDamageType primaryDamageType = usesExplosionAsPrimaryDamage
            ? explosionDamageSource.DamageType
            : data.DamageType;

        if (iconImage != null)
        {
            iconImage.sprite = weapon.Icon;
            iconImage.enabled = weapon.Icon != null;
        }

        SetText(weaponNameText, weapon.DisplayName);
        SetText(
            tierText,
            FormatLevel(upgradeLevel, nextUpgradeLevel, canPreviewUpgrade));
        SetText(energyCostText, $"Rarity: {weapon.Rarity}");
        SetText(damageTypeText, $"Type: {primaryDamageType}");
        SetText(
            damageText,
            FormatStat(
                "Damage",
                primaryDamage,
                nextPrimaryDamage,
                canPreviewUpgrade,
                "0.#"));
        SetSecondaryDamageText(
            GetSecondaryDamageText(data, usesExplosionAsPrimaryDamage));
        SetText(
            cooldownText,
            FormatStat(
                "Cooldown",
                stats.ReloadTime,
                nextStats.ReloadTime,
                canPreviewUpgrade,
                "0.##",
                " s"));
        SetText(
            rangeText,
            FormatStat(
                "Range",
                stats.Range,
                nextStats.Range,
                canPreviewUpgrade,
                "0.#"));
        SetText(platformTierText, $"Platform tier: {weapon.RequiredPlatformTier}");
        SetText(energyCostInfoText, $"Energy cost: {data.EnergyCost}");
        SetText(descriptionText, GetDescription(weapon));
    }

    private void Clear()
    {
        if (iconImage != null)
        {
            iconImage.sprite = null;
            iconImage.enabled = false;
        }

        SetText(weaponNameText, string.Empty);
        SetText(tierText, string.Empty);
        SetText(energyCostText, string.Empty);
        SetText(damageTypeText, string.Empty);
        SetText(damageText, string.Empty);
        SetSecondaryDamageText(string.Empty);
        SetText(cooldownText, string.Empty);
        SetText(rangeText, string.Empty);
        SetText(platformTierText, string.Empty);
        SetText(energyCostInfoText, string.Empty);
        SetText(descriptionText, string.Empty);
    }

    private static string GetDescription(WeaponContentDefinition weapon)
    {
        if (!string.IsNullOrWhiteSpace(weapon.ActiveAbilityDescription))
            return weapon.ActiveAbilityDescription;

        return weapon.PassiveAbilityDescription;
    }

    private static bool TryGetPrimaryExplosionDamage(
        WeaponData weaponData,
        out ProjectileDamageSource explosionDamageSource)
    {
        explosionDamageSource = null;
        if (!weaponData.TryGetPrimaryProjectileData(
                out _,
                out ProjectileData projectileData)
            || projectileData == null
            || projectileData.Damage > Mathf.Epsilon
            || !projectileData.TryGetDamageSource(
                ProjectileDamageTrigger.Explosion,
                out explosionDamageSource)
            || explosionDamageSource.Damage <= Mathf.Epsilon)
        {
            explosionDamageSource = null;
            return false;
        }

        return true;
    }

    private static string GetSecondaryDamageText(
        WeaponData weaponData,
        bool hidePrimaryExplosionDamage)
    {
        if (!weaponData.TryGetPrimaryProjectileData(
                out _,
                out ProjectileData projectileData)
            || projectileData == null)
        {
            return string.Empty;
        }

        StringBuilder builder = new();
        AppendTriggeredDamage(
            projectileData,
            hidePrimaryExplosionDamage,
            builder);
        AppendDebuffDamage(projectileData, builder);
        AppendSecondaryProjectileDamage(projectileData, builder);
        return builder.ToString();
    }

    private static void AppendTriggeredDamage(
        ProjectileData projectileData,
        bool hidePrimaryExplosionDamage,
        StringBuilder builder)
    {
        if (!projectileData.TryGetContract(
                out ProjectileDamageSourcesContract damageSources)
            || damageSources.Sources == null)
        {
            return;
        }

        for (int index = 0; index < damageSources.Sources.Count; index++)
        {
            ProjectileDamageSource source = damageSources.Sources[index];
            if (source == null
                || source.Trigger == ProjectileDamageTrigger.Contact
                || (hidePrimaryExplosionDamage
                    && source.Trigger == ProjectileDamageTrigger.Explosion))
                continue;

            string label = source.Trigger == ProjectileDamageTrigger.Explosion
                ? "Explosion"
                : "Periodic";
            AppendSegment(
                builder,
                $"{label}: {source.Damage:0.#} {source.DamageType}");
        }
    }

    private static void AppendDebuffDamage(
        ProjectileData projectileData,
        StringBuilder builder)
    {
        if (!projectileData.TryGetContract(
                out ProjectileEnemyDebuffsContract debuffsContract)
            || debuffsContract.Debuffs == null)
        {
            return;
        }

        for (int index = 0; index < debuffsContract.Debuffs.Count; index++)
        {
            EnemyDebuffApplication application = debuffsContract.Debuffs[index];
            if (application?.Debuff is not EnemyPeriodicDamageDebuffConfig damageDebuff)
            {
                if (application?.Debuff is not EnemyHeatDebuffConfig heatDebuff)
                    continue;

                AppendSegment(
                    builder,
                    $"Ignite: {heatDebuff.BurningDamagePerTick:0.#}/"
                    + $"{heatDebuff.BurningDamageInterval:0.##} s");
                if (heatDebuff.ExplosionDamagePercent > 0f)
                {
                    AppendSegment(
                        builder,
                        "Thermal explosion: "
                        + $"{heatDebuff.ExplosionDamagePercent:0.#}% max health");
                }
                continue;
            }

            AppendSegment(
                builder,
                $"{damageDebuff.name}: {damageDebuff.DamagePerTick:0.#} "
                + $"{damageDebuff.DamageType}/{damageDebuff.TickInterval:0.##} s");
        }
    }

    private static void AppendSecondaryProjectileDamage(
        ProjectileData projectileData,
        StringBuilder builder)
    {
        if (!projectileData.TryGetContract(
                out SpawnSecondaryProjectileContract secondaryContract)
            || secondaryContract.SecondaryProjectile == null)
        {
            return;
        }

        ProjectileData secondary = secondaryContract.SecondaryProjectile;
        AppendSegment(
            builder,
            $"Secondary hit: {secondary.Damage:0.#} {secondary.DamageType}");
    }

    private static void AppendSegment(StringBuilder builder, string value)
    {
        if (builder.Length > 0)
            builder.Append(" • ");

        builder.Append(value);
    }

    private void SetSecondaryDamageText(string value)
    {
        bool hasSecondaryDamage = !string.IsNullOrEmpty(value);
        if (secondaryDamageText != null)
        {
            secondaryDamageText.gameObject.SetActive(hasSecondaryDamage);
            secondaryDamageText.text = value;
        }
    }

    private static void SetText(TMP_Text text, string value)
    {
        if (text != null)
            text.text = value ?? string.Empty;
    }

    private static string FormatLevel(
        int currentLevel,
        int nextLevel,
        bool canPreviewUpgrade)
    {
        return canPreviewUpgrade
            ? $"Level {currentLevel} → {nextLevel}"
            : $"Level {currentLevel}";
    }

    private static string FormatStat(
        string label,
        float currentValue,
        float nextValue,
        bool canPreviewUpgrade,
        string numberFormat,
        string suffix = "")
    {
        if (!canPreviewUpgrade || Mathf.Approximately(currentValue, nextValue))
            return $"{label}: {currentValue.ToString(numberFormat)}{suffix}";

        return $"{label}: {currentValue.ToString(numberFormat)} → "
            + $"{nextValue.ToString(numberFormat)}{suffix}";
    }
}
