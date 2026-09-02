using System.Collections.Generic;
using UnityEngine;
using Zenject;

[RequireComponent(typeof(ChainLightningVisual))]
public sealed class ChainLightningWeapon : Weapon
{
    [Inject] private EnemyManager enemyManager;
    [Inject] private DealDamageManager dealDamageManager;

    private readonly List<Enemy> selectedTargets = new List<Enemy>(4);
    private readonly List<Vector3> targetPositions = new List<Vector3>(4);
    private ChainLightningVisual chainLightningVisual;

    protected override void Awake()
    {
        base.Awake();
        chainLightningVisual = GetComponent<ChainLightningVisual>();
        if (chainLightningVisual != null)
            chainLightningVisual.Prepare(Mathf.Max(1, CurrentStats.MaxTargets));
    }

    protected override bool Fire()
    {
        if (projectileSpawn == null
            || enemyManager == null
            || dealDamageManager == null)
        {
            return false;
        }

        selectedTargets.Clear();
        targetPositions.Clear();

        Vector3 sourcePosition = projectileSpawn.position;
        Vector3 searchOrigin = sourcePosition;
        int maxTargets = CurrentStats.MaxTargets;
        float searchRadius = CurrentStats.TargetSearchRadius;

        for (int targetIndex = 0; targetIndex < maxTargets; targetIndex++)
        {
            Enemy target = enemyManager.FindNearestEnemy(
                searchOrigin,
                searchRadius,
                selectedTargets);

            if (target == null)
                break;

            Vector3 targetPosition = target.transform.position;
            selectedTargets.Add(target);
            targetPositions.Add(targetPosition);
            dealDamageManager.DealDamage(
                target,
                Owner,
                CurrentStats.Damage,
                weaponData.DamageType);
            searchOrigin = targetPosition;
        }

        if (targetPositions.Count == 0)
            return false;

        if (chainLightningVisual != null)
            chainLightningVisual.Play(sourcePosition, targetPositions);

        return true;
    }

    protected override void OnLevelApplied()
    {
        EnsureCapacity(selectedTargets, CurrentStats.MaxTargets);
        EnsureCapacity(targetPositions, CurrentStats.MaxTargets);
        if (chainLightningVisual != null)
            chainLightningVisual.Prepare(CurrentStats.MaxTargets);
    }

    private static void EnsureCapacity<T>(List<T> values, int capacity)
    {
        if (values.Capacity < capacity)
            values.Capacity = capacity;
    }
}
