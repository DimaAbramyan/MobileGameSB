using UnityEngine;
using Zenject;

[DisallowMultipleComponent]
public sealed class EnemyMetalDrop : MonoBehaviour
{
    private Enemy enemy;
    private MetalPickup pickupPrefab;
    private MetalPickupController pickupController;
    private int metalAmount;
    private bool wasSpawned;

    private void Awake()
    {
        enemy = GetComponent<Enemy>();
    }

    private void OnEnable()
    {
        if (enemy == null)
            enemy = GetComponent<Enemy>();

        if (enemy != null)
            enemy.OnDied += HandleEnemyDied;
    }

    private void OnDisable()
    {
        if (enemy != null)
            enemy.OnDied -= HandleEnemyDied;
    }

    public void Configure(
        MetalPickup prefab,
        int amount,
        MetalPickupController controller)
    {
        pickupPrefab = prefab;
        metalAmount = Mathf.Max(0, amount);
        pickupController = controller;

        if (enemy != null && enemy.isDead)
            SpawnPickup();
    }

    private void HandleEnemyDied(Enemy deadEnemy)
    {
        SpawnPickup();
    }

    private void SpawnPickup()
    {
        if (wasSpawned || pickupPrefab == null || metalAmount <= 0)
            return;

        wasSpawned = true;

        pickupController?.Spawn(pickupPrefab, transform.position, metalAmount);
    }
}
