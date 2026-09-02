using System.Collections.Generic;

using UnityEngine;
using Zenject;

public sealed class BossProjectilePool : MonoBehaviour
{
    [Inject] private DiContainer container;
    [SerializeField, Min(0)] private int defaultInitialSize = 32;

    private sealed class PoolEntry
    {
        public BossProjectile Prefab;
        public readonly List<BossProjectile> Available = new();
        public readonly HashSet<BossProjectile> Active = new();
    }

    private readonly Dictionary<BossProjectile, PoolEntry> poolsByPrefab = new();
    private readonly Dictionary<BossProjectile, PoolEntry> poolByInstance = new();

    public BossProjectile Spawn(
        BossProjectile prefab,
        Vector3 position,
        Quaternion rotation,
        in BossProjectileLaunchData launchData)
    {
        if (prefab == null)
            return null;

        PoolEntry entry = GetOrCreatePool(prefab);
        BossProjectile instance = Take(entry);
        entry.Active.Add(instance);

        instance.transform.SetParent(null, true);
        instance.transform.SetPositionAndRotation(position, rotation);
        instance.gameObject.SetActive(true);
        instance.Launch(this, launchData);
        return instance;
    }

    public void Release(BossProjectile projectile)
    {
        if (projectile == null)
            return;

        if (!poolByInstance.TryGetValue(projectile, out PoolEntry entry))
        {
            Destroy(projectile.gameObject);
            return;
        }

        if (!entry.Active.Remove(projectile))
            return;

        projectile.ResetState();
        projectile.transform.SetParent(transform, false);
        projectile.gameObject.SetActive(false);
        entry.Available.Add(projectile);
    }

    private PoolEntry GetOrCreatePool(BossProjectile prefab)
    {
        if (poolsByPrefab.TryGetValue(prefab, out PoolEntry existing))
            return existing;

        PoolEntry entry = new PoolEntry { Prefab = prefab };
        poolsByPrefab.Add(prefab, entry);

        for (int i = 0; i < defaultInitialSize; i++)
            entry.Available.Add(Create(entry));

        return entry;
    }

    private BossProjectile Take(PoolEntry entry)
    {
        while (entry.Available.Count > 0)
        {
            int lastIndex = entry.Available.Count - 1;
            BossProjectile projectile = entry.Available[lastIndex];
            entry.Available.RemoveAt(lastIndex);

            if (projectile != null)
                return projectile;
        }

        return Create(entry);
    }

    private BossProjectile Create(PoolEntry entry)
    {
        BossProjectile projectile = Instantiate(entry.Prefab, transform);
        projectile.gameObject.SetActive(false);
        container.InjectGameObject(projectile.gameObject);
        poolByInstance.Add(projectile, entry);
        return projectile;
    }
}
