using UnityEngine;
using Zenject;

[DisallowMultipleComponent]
public sealed class EnemyBuffDrop : MonoBehaviour
{
    private Enemy enemy;
    private SubWaveBuffDropController dropController;
    private DiContainer container;
    private bool wasResolved;

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
        SubWaveBuffDropController controller,
        DiContainer diContainer)
    {
        dropController = controller;
        container = diContainer;

        if (enemy != null && enemy.isDead)
            ResolveDrop();
    }

    private void HandleEnemyDied(Enemy deadEnemy)
    {
        ResolveDrop();
    }

    private void ResolveDrop()
    {
        if (wasResolved || dropController == null)
            return;

        wasResolved = true;
        if (!dropController.TrySelectReward(out Buff rewardPrefab)
            || rewardPrefab == null)
        {
            return;
        }

        if (container != null)
        {
            container.InstantiatePrefab(
                rewardPrefab.gameObject,
                transform.position,
                Quaternion.identity,
                null);
            return;
        }

        Instantiate(
            rewardPrefab.gameObject,
            transform.position,
            Quaternion.identity);
    }
}
