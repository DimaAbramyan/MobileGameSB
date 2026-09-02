using System;
using System.IO;
using UnityEngine;

public sealed class PlayerResourceWallet
{
    private const string SaveFileName = "player_resources.json";

    private readonly string savePath;
    private bool loaded;
    private int metal;
    private int gold;
    private int cores;

    public event Action<int, int> OnChanged;
    public event Action<int, int, int> OnResourcesChanged;

    public int Metal
    {
        get
        {
            EnsureLoaded();
            return metal;
        }
    }

    public int Cores
    {
        get
        {
            EnsureLoaded();
            return cores;
        }
    }

    public int Gold
    {
        get
        {
            EnsureLoaded();
            return gold;
        }
    }

    public PlayerResourceWallet()
    {
        savePath = Path.Combine(Application.persistentDataPath, SaveFileName);
    }

    public void Add(int metalAmount, int coreAmount)
    {
        EnsureLoaded();

        metal += Mathf.Max(0, metalAmount);
        cores += Mathf.Max(0, coreAmount);

        Save();
        NotifyChanged();
    }

    public void AddGold(int amount)
    {
        EnsureLoaded();

        gold += Mathf.Max(0, amount);

        Save();
        NotifyChanged();
    }

    public bool TrySpend(int metalCost, int coreCost)
    {
        EnsureLoaded();

        metalCost = Mathf.Max(0, metalCost);
        coreCost = Mathf.Max(0, coreCost);

        if (metal < metalCost || cores < coreCost)
            return false;

        metal -= metalCost;
        cores -= coreCost;

        Save();
        NotifyChanged();
        return true;
    }

    public void Reset()
    {
        metal = 0;
        gold = 0;
        cores = 0;
        loaded = true;

        if (File.Exists(savePath))
            File.Delete(savePath);

        NotifyChanged();
    }

    private void EnsureLoaded()
    {
        if (loaded)
            return;

        loaded = true;
        metal = 0;
        gold = 0;
        cores = 0;

        if (!File.Exists(savePath))
            return;

        try
        {
            string json = File.ReadAllText(savePath);
            ResourceSaveData data =
                JsonUtility.FromJson<ResourceSaveData>(json);

            if (data == null)
                return;

            metal = Mathf.Max(0, data.metal);
            gold = Mathf.Max(0, data.gold);
            cores = Mathf.Max(0, data.cores);
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
                metal = metal,
                gold = gold,
                cores = cores
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
        OnChanged?.Invoke(metal, cores);
        OnResourcesChanged?.Invoke(metal, gold, cores);
    }

    [Serializable]
    private sealed class ResourceSaveData
    {
        public int metal;
        public int gold;
        public int cores;
    }
}
