using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public sealed class PlayerResourceWallet
{
    private const string SaveFileName = "player_resources.json";

    private readonly string savePath;
    private readonly ResourceCatalog resourceCatalog;
    private readonly Dictionary<ResourceKind, PlayerResource> resources = new();
    private bool loaded;

    public event Action<int, int> OnChanged;
    public event Action<int, int, int> OnResourcesChanged;
    public event Action<PlayerResource> OnResourceChanged;
    public event Action<PlayerResource, int> OnResourceAdded;

    public PlayerResourceWallet(ResourceCatalog resourceCatalog)
    {
        this.resourceCatalog = resourceCatalog
            ?? throw new ArgumentNullException(nameof(resourceCatalog));
        savePath = Path.Combine(Application.persistentDataPath, SaveFileName);
    }

    public int Metal => Get(ResourceKind.Metal).Amount;
    public int Gold => Get(ResourceKind.Gold).Amount;
    public int Chip => Get(ResourceKind.Chip).Amount;

    // Legacy API and save terminology. Cores are now displayed as Chips.
    public int Cores => Chip;

    public PlayerResource Get(ResourceKind kind)
    {
        EnsureLoaded();
        return GetResourceInternal(kind);
    }

    public void Add(ResourceDefinition resource, int amount)
    {
        if (resource == null)
            throw new ArgumentNullException(nameof(resource));

        Add(resource.Kind, amount);
    }

    public void Add(ResourceKind kind, int amount)
    {
        EnsureLoaded();

        int addedAmount = Mathf.Max(0, amount);
        if (addedAmount <= 0)
            return;

        PlayerResource resource = GetResourceInternal(kind);
        resource.Add(addedAmount);
        Save();
        NotifyChanged();
        OnResourceAdded?.Invoke(resource, addedAmount);
    }

    public void Add(int metalAmount, int coreAmount)
    {
        EnsureLoaded();

        int addedMetal = Mathf.Max(0, metalAmount);
        int addedChip = Mathf.Max(0, coreAmount);
        if (addedMetal <= 0 && addedChip <= 0)
            return;

        PlayerResource metal = GetResourceInternal(ResourceKind.Metal);
        PlayerResource chip = GetResourceInternal(ResourceKind.Chip);
        metal.Add(addedMetal);
        chip.Add(addedChip);
        Save();
        NotifyChanged();

        if (addedMetal > 0)
            OnResourceAdded?.Invoke(metal, addedMetal);
        if (addedChip > 0)
            OnResourceAdded?.Invoke(chip, addedChip);
    }

    public void AddGold(int amount)
    {
        Add(ResourceKind.Gold, amount);
    }

    public bool TrySpend(ResourceKind kind, int amount)
    {
        EnsureLoaded();

        PlayerResource resource = GetResourceInternal(kind);
        if (!resource.TrySpend(amount))
            return false;

        Save();
        NotifyChanged();
        return true;
    }

    public bool TrySpend(int metalCost, int coreCost)
    {
        EnsureLoaded();

        int metalAmount = Mathf.Max(0, metalCost);
        int chipAmount = Mathf.Max(0, coreCost);
        PlayerResource metal = GetResourceInternal(ResourceKind.Metal);
        PlayerResource chip = GetResourceInternal(ResourceKind.Chip);
        if (metal.Amount < metalAmount || chip.Amount < chipAmount)
            return false;

        metal.TrySpend(metalAmount);
        chip.TrySpend(chipAmount);
        Save();
        NotifyChanged();
        return true;
    }

    public void Reset()
    {
        EnsureResourceModels();
        foreach (PlayerResource resource in resources.Values)
            resource.SetAmount(0);

        loaded = true;

        if (File.Exists(savePath))
            File.Delete(savePath);

        NotifyChanged();
    }

    private PlayerResource GetResourceInternal(ResourceKind kind)
    {
        if (resources.TryGetValue(kind, out PlayerResource resource))
            return resource;

        resource = new PlayerResource(resourceCatalog.Get(kind));
        resources.Add(kind, resource);
        return resource;
    }

    private void EnsureResourceModels()
    {
        GetResourceInternal(ResourceKind.Metal);
        GetResourceInternal(ResourceKind.Gold);
        GetResourceInternal(ResourceKind.Chip);
    }

    private void EnsureLoaded()
    {
        if (loaded)
            return;

        loaded = true;
        EnsureResourceModels();

        if (!File.Exists(savePath))
            return;

        try
        {
            string json = File.ReadAllText(savePath);
            ResourceSaveData data = JsonUtility.FromJson<ResourceSaveData>(json);
            if (data == null)
                return;

            GetResourceInternal(ResourceKind.Metal).SetAmount(data.metal);
            GetResourceInternal(ResourceKind.Gold).SetAmount(data.gold);
            GetResourceInternal(ResourceKind.Chip).SetAmount(data.cores);
        }
        catch (Exception exception)
        {
            Debug.LogWarning(
                $"Cannot load player resources from {savePath}: {exception.Message}");
        }
    }

    private void Save()
    {
        try
        {
            string directory = Path.GetDirectoryName(savePath);
            if (!string.IsNullOrEmpty(directory)
                && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            ResourceSaveData data = new ResourceSaveData
            {
                metal = GetResourceInternal(ResourceKind.Metal).Amount,
                gold = GetResourceInternal(ResourceKind.Gold).Amount,
                // Keep the previous JSON key to preserve existing player saves.
                cores = GetResourceInternal(ResourceKind.Chip).Amount
            };

            File.WriteAllText(savePath, JsonUtility.ToJson(data, true));
        }
        catch (Exception exception)
        {
            Debug.LogWarning(
                $"Cannot save player resources to {savePath}: {exception.Message}");
        }
    }

    private void NotifyChanged()
    {
        PlayerResource metal = GetResourceInternal(ResourceKind.Metal);
        PlayerResource gold = GetResourceInternal(ResourceKind.Gold);
        PlayerResource chip = GetResourceInternal(ResourceKind.Chip);

        OnChanged?.Invoke(metal.Amount, chip.Amount);
        OnResourcesChanged?.Invoke(metal.Amount, gold.Amount, chip.Amount);
        OnResourceChanged?.Invoke(metal);
        OnResourceChanged?.Invoke(gold);
        OnResourceChanged?.Invoke(chip);
    }

    [Serializable]
    private sealed class ResourceSaveData
    {
        public int metal;
        public int gold;
        public int cores;
    }
}
