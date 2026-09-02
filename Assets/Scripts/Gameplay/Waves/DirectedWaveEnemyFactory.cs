using UnityEngine;
using Zenject;

internal sealed class DirectedWaveEnemyFactory
{
    private readonly DiContainer container;

    public DirectedWaveEnemyFactory(DiContainer container)
    {
        this.container = container;
    }

    public Enemy Create(
        Enemy prefab,
        Vector3 position,
        Transform parent)
    {
        if (prefab == null)
            return null;

        GameObject instance = container != null
            ? container.InstantiatePrefab(
                prefab.gameObject,
                position,
                prefab.transform.rotation,
                parent)
            : Object.Instantiate(
                prefab.gameObject,
                position,
                prefab.transform.rotation,
                parent);

        return instance != null ? instance.GetComponent<Enemy>() : null;
    }
}
