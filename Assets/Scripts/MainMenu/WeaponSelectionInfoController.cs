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

        if (iconImage != null)
        {
            iconImage.sprite = weapon.Icon;
            iconImage.enabled = weapon.Icon != null;
        }

        SetText(weaponNameText, weapon.DisplayName);
        SetText(tierText, $"Tier {weapon.RequiredPlatformTier}");
        SetText(energyCostText, data.EnergyCost.ToString());
        SetText(damageTypeText, $"Type: {data.DamageType}");
        SetText(
            damageText,
            CraftProgressionText.GetWeaponDamageText(
                weapon,
                contentProgressService));
        SetSecondaryDamageText(GetSecondaryDamageText(data));
        SetText(
            cooldownText,
            CraftProgressionText.GetWeaponCooldownText(
                weapon,
                contentProgressService));
        SetText(
            rangeText,
            CraftProgressionText.GetWeaponRangeText(
                weapon,
                contentProgressService));
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
        SetText(descriptionText, string.Empty);
    }

    private static string GetDescription(WeaponContentDefinition weapon)
    {
        if (!string.IsNullOrWhiteSpace(weapon.ActiveAbilityDescription))
            return weapon.ActiveAbilityDescription;

        return weapon.PassiveAbilityDescription;
    }

    private static string GetSecondaryDamageText(WeaponData weaponData)
    {
        if (!weaponData.TryGetPrimaryProjectileData(
                out _,
                out ProjectileData projectileData)
            || projectileData == null)
        {
            return string.Empty;
        }

        StringBuilder builder = new();
        AppendTriggeredDamage(projectileData, builder);
        AppendDebuffDamage(projectileData, builder);
        AppendSecondaryProjectileDamage(projectileData, builder);
        return builder.ToString();
    }

    private static void AppendTriggeredDamage(
        ProjectileData projectileData,
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
            if (source == null || source.Trigger == ProjectileDamageTrigger.Contact)
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
                if (heatDebuff.ExplosionDamage > 0f)
                {
                    AppendSegment(
                        builder,
                        $"Overheat explosion: {heatDebuff.ExplosionDamage:0.#}");
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
}
