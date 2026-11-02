using System;
using System.Collections.Generic;
using UnityEngine;

public enum ContentProgressStatus
{
    Locked,
    Purchasable,
    Owned,
    MaxUpgradeLevel
}

public readonly struct ContentProgressState
{
    public ContentProgressState(
        ContentProgressStatus status,
        int upgradeLevel,
        ContentPrice actionCost,
        bool canUpgrade,
        string reason)
    {
        Status = status;
        UpgradeLevel = Mathf.Max(0, upgradeLevel);
        ActionCost = actionCost;
        CanUpgrade = canUpgrade;
        Reason = reason ?? string.Empty;
    }

    public ContentProgressStatus Status { get; }
    public int UpgradeLevel { get; }
    public ContentPrice ActionCost { get; }
    public bool CanUpgrade { get; }
    public string Reason { get; }
    public bool IsOwned => Status == ContentProgressStatus.Owned
        || Status == ContentProgressStatus.MaxUpgradeLevel;
    public bool CanPurchase => Status == ContentProgressStatus.Purchasable;
}

public sealed class ContentProgressService
{
    private readonly SaveManager saveManager;
    private readonly ContentAvailabilityService availabilityService;
    private readonly PlayerResourceWallet resourceWallet;
    private readonly Dictionary<string, ContentProgressEntry> entriesById = new();
    private bool loaded;

    public ContentProgressService(
        LevelProgressService levelProgressService,
        PlayerResourceWallet resourceWallet) : this(levelProgressService, resourceWallet, resourceWallet?.Persistence) { }

    [Zenject.Inject]
    public ContentProgressService(
        LevelProgressService levelProgressService,
        PlayerResourceWallet resourceWallet,
        SaveManager saveManager)
    {
        availabilityService = new ContentAvailabilityService(
            levelProgressService ?? throw new ArgumentNullException(nameof(levelProgressService)));
        this.resourceWallet = resourceWallet
            ?? throw new ArgumentNullException(nameof(resourceWallet));
        this.saveManager = saveManager ?? throw new ArgumentNullException(nameof(saveManager));
    }

    public event Action ProgressChanged;

    public ContentProgressState GetState(CraftContentDefinition content)
    {
        EnsureLoaded();

        if (content == null)
        {
            return new ContentProgressState(
                ContentProgressStatus.Locked,
                0,
                default,
                false,
                "Контент не назначен.");
        }

        if (string.IsNullOrWhiteSpace(content.Id))
        {
            return new ContentProgressState(
                ContentProgressStatus.Locked,
                0,
                default,
                false,
                $"У '{content.DisplayName}' не задан постоянный id.");
        }

        bool hasOwnedEntry = TryGetOwnedEntry(content.Id, out ContentProgressEntry entry);
        bool isOwned = content.IsStarterUnlocked
            || hasOwnedEntry;
        int upgradeLevel = isOwned && entry != null
            ? Mathf.Clamp(entry.upgradeLevel, 0, content.MaxUpgradeLevel)
            : 0;

        if (!isOwned)
        {
            ContentAvailability availability = availabilityService.GetAvailability(content);
            if (!availability.IsAvailable)
            {
                return new ContentProgressState(
                    ContentProgressStatus.Locked,
                    0,
                    default,
                    false,
                    availability.Reason);
            }

            return new ContentProgressState(
                ContentProgressStatus.Purchasable,
                0,
                content.PurchaseCost,
                false,
                "Можно приобрести.");
        }

        if (content.MaxUpgradeLevel <= 0)
        {
            return new ContentProgressState(
                ContentProgressStatus.Owned,
                0,
                default,
                false,
                "Принадлежит игроку.");
        }

        if (upgradeLevel >= content.MaxUpgradeLevel)
        {
            return new ContentProgressState(
                ContentProgressStatus.MaxUpgradeLevel,
                upgradeLevel,
                default,
                false,
                "Достигнут максимальный уровень.");
        }

        if (!content.TryGetUpgradeCost(upgradeLevel + 1, out ContentPrice upgradeCost))
        {
            return new ContentProgressState(
                ContentProgressStatus.Owned,
                upgradeLevel,
                default,
                false,
                "Не задана цена следующего улучшения.");
        }

        return new ContentProgressState(
            ContentProgressStatus.Owned,
            upgradeLevel,
            upgradeCost,
            true,
            "Принадлежит игроку.");
    }

    public bool IsOwned(CraftContentDefinition content)
    {
        return GetState(content).IsOwned;
    }

    public int GetUpgradeLevel(CraftContentDefinition content)
    {
        return GetState(content).UpgradeLevel;
    }

    public bool TryPurchase(CraftContentDefinition content)
    {
        ContentProgressState state = GetState(content);
        if (!state.CanPurchase)
            return false;

        if (!resourceWallet.TrySpend(state.ActionCost.Metal, state.ActionCost.Cores, false))
            return false;

        ContentProgressEntry entry = GetOrCreateEntry(content.Id);
        entry.isOwned = true;
        entry.upgradeLevel = 0;
        Save();
        ProgressChanged?.Invoke();
        return true;
    }

    public bool TryUpgrade(CraftContentDefinition content)
    {
        ContentProgressState state = GetState(content);
        if (!state.IsOwned || !state.CanUpgrade)
            return false;

        if (!resourceWallet.TrySpend(state.ActionCost.Metal, state.ActionCost.Cores, false))
            return false;

        ContentProgressEntry entry = GetOrCreateEntry(content.Id);
        entry.isOwned = true;
        entry.upgradeLevel = Mathf.Min(
            content.MaxUpgradeLevel,
            state.UpgradeLevel + 1);
        Save();
        ProgressChanged?.Invoke();
        return true;
    }

    public void ResetProgress()
    {
        entriesById.Clear();
        loaded = true;

        Save();

        ProgressChanged?.Invoke();
    }

    private bool TryGetOwnedEntry(string contentId, out ContentProgressEntry entry)
    {
        if (entriesById.TryGetValue(contentId, out entry))
            return entry.isOwned;

        entry = null;
        return false;
    }

    private ContentProgressEntry GetOrCreateEntry(string contentId)
    {
        if (entriesById.TryGetValue(contentId, out ContentProgressEntry entry))
            return entry;

        entry = new ContentProgressEntry { contentId = contentId };
        entriesById.Add(contentId, entry);
        return entry;
    }

    private void EnsureLoaded()
    {
        if (loaded)
            return;
        loaded = true;
        entriesById.Clear();
        ContentProgressSaveData data = saveManager.ReadProgression().content;
        for (int i = 0; i < data.entries.Count; i++)
        {
            ContentProgressEntry entry = data.entries[i];
            if (entry == null || string.IsNullOrWhiteSpace(entry.contentId))
                continue;
            entry.upgradeLevel = Mathf.Max(0, entry.upgradeLevel);
            entriesById[entry.contentId] = entry;
        }
    }

    private void Save()
    {
        var data = new ContentProgressSaveData
        {
            entries = new List<ContentProgressEntry>(entriesById.Values)
        };
        saveManager.UpdateProgression(progress => progress.content = data, true);
    }

}
