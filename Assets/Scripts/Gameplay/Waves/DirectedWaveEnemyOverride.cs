using UnityEngine;

public sealed class DirectedWaveEnemyOverride : MonoBehaviour
{
    [SerializeField] private Enemy enemyPrefabOverride;

    public Enemy EnemyPrefabOverride => enemyPrefabOverride;
}
