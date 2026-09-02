using System;
using UnityEngine;

public abstract class CraftContentDefinition : ScriptableObject
{
    [Header("Identity")]
    [SerializeField] private string id;
    [SerializeField] private string displayName;
    [SerializeField, TextArea(2, 5)] private string passiveAbilityDescription;
    [SerializeField, TextArea(2, 5)] private string activeAbilityDescription;
    [SerializeField] private Sprite icon;
    [SerializeField] private GameObject prefab;
    [SerializeField] private ContentRarity rarity = ContentRarity.Common;

    [Header("Availability")]
    [SerializeField, Min(0)] private int purchasePrice;
    [SerializeField, Min(0)] private int purchaseCoresPrice;
    [SerializeField] private ContentUnlockRequirement unlockRequirement = new ContentUnlockRequirement();
    [SerializeField] private bool isStarterUnlocked;

    [Header("Upgrade")]
    [SerializeField, Min(0)] private int maxUpgradeLevel;
    [SerializeField] private ContentPrice[] upgradePrices = Array.Empty<ContentPrice>();

    public string Id => id;
    public string DisplayName => displayName;
    public string PassiveAbilityDescription => passiveAbilityDescription;
    public string ActiveAbilityDescription => activeAbilityDescription;
    public Sprite Icon => icon;
    public GameObject Prefab => prefab;
    public ContentRarity Rarity => rarity;
    public int PurchasePrice => purchasePrice;
    public ContentPrice PurchaseCost => new ContentPrice(purchasePrice, purchaseCoresPrice);
    public ContentUnlockRequirement UnlockRequirement => unlockRequirement;
    public bool IsStarterUnlocked => isStarterUnlocked;
    public int MaxUpgradeLevel => maxUpgradeLevel;

    public bool TryGetUpgradeCost(int targetUpgradeLevel, out ContentPrice price)
    {
        if (targetUpgradeLevel <= 0
            || targetUpgradeLevel > maxUpgradeLevel
            || upgradePrices == null
            || targetUpgradeLevel > upgradePrices.Length)
        {
            price = default;
            return false;
        }

        price = upgradePrices[targetUpgradeLevel - 1];
        return true;
    }

#if UNITY_EDITOR
    protected virtual void OnValidate()
    {
        purchasePrice = Mathf.Max(0, purchasePrice);
        purchaseCoresPrice = Mathf.Max(0, purchaseCoresPrice);
        maxUpgradeLevel = Mathf.Max(0, maxUpgradeLevel);

        if (upgradePrices == null)
            upgradePrices = Array.Empty<ContentPrice>();

        for (int i = 0; i < upgradePrices.Length; i++)
        {
            ContentPrice price = upgradePrices[i];
            price.Validate();
            upgradePrices[i] = price;
        }
    }
#endif
}
