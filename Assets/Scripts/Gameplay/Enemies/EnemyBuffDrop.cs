using UnityEngine;
using Zenject;

[DisallowMultipleComponent]
public sealed class EnemyBuffDrop : MonoBehaviour
{
    private static readonly Color HealTint = new(0.25f, 1f, 0.4f, 1f);
    private static readonly Color LevelUpTint = new(1f, 0.85f, 0.2f, 1f);
    private const float TintStrength = 0.45f;

    private Enemy enemy;
    private Buff rewardPrefab;
    private DiContainer container;
    private SpriteRenderer[] spriteRenderers;
    private bool wasSpawned;
    private bool tintApplied;

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

    public void Configure(Buff prefab, DiContainer diContainer)
    {
        rewardPrefab = prefab;
        container = diContainer;
        ApplyRewardTint();

        if (enemy != null && enemy.isDead)
            SpawnReward();
    }

    private void HandleEnemyDied(Enemy deadEnemy)
    {
        SpawnReward();
    }

    private void SpawnReward()
    {
        if (wasSpawned || rewardPrefab == null)
            return;

        wasSpawned = true;

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

    private void ApplyRewardTint()
    {
        if (tintApplied || !TryGetRewardTint(out Color tint))
            return;

        spriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            SpriteRenderer spriteRenderer = spriteRenderers[i];
            if (spriteRenderer == null)
                continue;

            Color original = spriteRenderer.color;
            Color tinted = Color.Lerp(original, tint, TintStrength);
            tinted.a = original.a;
            spriteRenderer.color = tinted;
        }

        tintApplied = true;
    }

    private bool TryGetRewardTint(out Color tint)
    {
        if (rewardPrefab is HealBuff)
        {
            tint = HealTint;
            return true;
        }

        if (rewardPrefab is BuffLevel)
        {
            tint = LevelUpTint;
            return true;
        }

        tint = default;
        return false;
    }
}
