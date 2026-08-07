using System.Collections;
using UnityEngine;
using Zenject;

public sealed class TwinCloneFleetActiveAbility : ActiveAbility
{
    [InjectOptional] private DiContainer container;

    [SerializeField] private GameObject clonePrefabOverride;
    [SerializeField, Min(1)] private int cloneCount = 5;
    [SerializeField, Min(0f)] private float spawnInterval = 0.2f;
    [SerializeField] private Vector2 spawnOffset = new Vector2(0f, 0.75f);
    [SerializeField, Min(0f)] private float cloneSpeed = 8f;
    [SerializeField, Min(0f)] private float cloneLifetime = 6f;

    private Coroutine spawnRoutine;

    public override bool Activate(ParentShip owner)
    {
        if (owner == null || spawnRoutine != null)
            return false;

        spawnRoutine = StartCoroutine(SpawnFleet(owner));
        return true;
    }

    private IEnumerator SpawnFleet(ParentShip fleetOwner)
    {
        for (int i = 0; i < cloneCount; i++)
        {
            if (fleetOwner == null)
                break;

            SpawnClone(fleetOwner);

            if (spawnInterval > 0f && i < cloneCount - 1)
                yield return new WaitForSeconds(spawnInterval);
        }

        spawnRoutine = null;
    }

    private void SpawnClone(ParentShip fleetOwner)
    {
        Vector3 position = fleetOwner.transform.position
            + fleetOwner.transform.TransformVector(spawnOffset);

        GameObject instance = clonePrefabOverride != null
            ? Instantiate(clonePrefabOverride, position, fleetOwner.transform.rotation)
            : new GameObject($"{fleetOwner.name}_TwinClone");

        instance.transform.SetPositionAndRotation(position, fleetOwner.transform.rotation);

        if (container != null)
            container.InjectGameObject(instance);

        TwinCloneController clone = instance.GetComponent<TwinCloneController>();
        if (clone == null)
            clone = instance.AddComponent<TwinCloneController>();

        clone.Configure(fleetOwner, container, true, cloneSpeed, cloneLifetime);
    }

    protected override void OnDisable()
    {
        base.OnDisable();

        if (spawnRoutine != null)
        {
            StopCoroutine(spawnRoutine);
            spawnRoutine = null;
        }
    }
}
