using System.Collections.Generic;
using UnityEngine;
using Zenject;

public class ProjectilePoolController : MonoBehaviour
{
    [Inject] private DiContainer container;
    [SerializeField] private int defaultInitialSize = 50;

    private sealed class PoolEntry
    {
        public Projectile Prefab;
        public readonly List<Projectile> Available = new();
        public readonly HashSet<Projectile> Active = new();

        public void Prewarm(int count, ProjectilePoolController owner)
        {
            for (int i = 0; i < count; i++)
                Available.Add(owner.CreateProjectile(this));
        }
    }

    private readonly Dictionary<Projectile, PoolEntry> poolsByPrefab = new();
    private readonly Dictionary<Projectile, PoolEntry> poolByInstance = new();

    private PoolEntry GetOrCreatePool(Projectile prefab)
    {
        if (poolsByPrefab.TryGetValue(prefab, out PoolEntry existing))
            return existing;

        PoolEntry entry = new PoolEntry { Prefab = prefab };
        poolsByPrefab[prefab] = entry;
        entry.Prewarm(defaultInitialSize, this);
        return entry;
    }

    private Projectile CreateProjectile(PoolEntry entry)
    {
        Projectile projectile = Instantiate(entry.Prefab, transform);
        projectile.gameObject.SetActive(false);
        container.InjectGameObject(projectile.gameObject);
        projectile.SetPoolController(this);
        poolByInstance[projectile] = entry;
        return projectile;
    }

    public Projectile Spawn(Projectile prefab, Vector3 position, Quaternion rotation)
    {
        if (prefab == null)
            return null;

        PoolEntry entry = GetOrCreatePool(prefab);

        Projectile instance = null;
        while (entry.Available.Count > 0 && instance == null)
        {
            int lastIndex = entry.Available.Count - 1;
            instance = entry.Available[lastIndex];
            entry.Available.RemoveAt(lastIndex);
        }

        if (instance == null)
            instance = CreateProjectile(entry);

        entry.Active.Add(instance);

        instance.transform.SetParent(null, true);
        instance.transform.SetPositionAndRotation(position, rotation);
        instance.gameObject.SetActive(true);

        return instance;
    }

    public void Release(Projectile projectile)
    {
        if (projectile == null)
            return;

        if (poolByInstance.TryGetValue(projectile, out PoolEntry entry))
        {
            if (!entry.Active.Remove(projectile))
                return;

            projectile.ResetState();
            projectile.transform.SetParent(transform, false);
            projectile.gameObject.SetActive(false);
            entry.Available.Add(projectile);
            return;
        }

        Destroy(projectile.gameObject);
    }
}
