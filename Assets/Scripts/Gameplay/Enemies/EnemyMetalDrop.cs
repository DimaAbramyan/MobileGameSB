using UnityEngine;
using Zenject;

[DisallowMultipleComponent]
public sealed class EnemyMetalDrop : MonoBehaviour
{
    private Enemy enemy;
    private MetalPickup pickupPrefab;
    private MetalPickupController pickupController;
    private PlayerController playerController;
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
        Configure(prefab, amount, controller, null);
    }

    public void Configure(
        MetalPickup prefab,
        int amount,
        MetalPickupController controller,
        PlayerController player)
    {
        pickupPrefab = prefab;
        metalAmount = Mathf.Max(0, amount);
        pickupController = controller;
        playerController = player;

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

        int amount = playerController != null
            ? playerController.GetModifiedMetalDropAmount(metalAmount)
            : metalAmount;
        pickupController?.Spawn(pickupPrefab, transform.position, amount);
    }
}
