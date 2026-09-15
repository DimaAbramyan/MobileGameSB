using UnityEngine;
using Zenject;

[DisallowMultipleComponent]
public sealed class SubWaveBuffDropController : MonoBehaviour
{
    [InjectOptional] private DiContainer container;

    [SerializeField, Min(0)] private int maxBuffs = 1;

    private InfoAboutSubWave subWave;
    private WaveBuffDropController waveDropController;
    private int remainingEligibleEnemyDeaths;
    private int issuedBuffCount;
    private int reservedBuffCount;

    public int MaxBuffs => Mathf.Max(0, maxBuffs);

    private void Awake()
    {
        subWave = GetComponent<InfoAboutSubWave>();
        if (subWave == null)
        {
            Debug.LogError(
                $"{nameof(SubWaveBuffDropController)} requires an "
                + $"{nameof(InfoAboutSubWave)} on the same object.",
                this);
        }
    }

    private void OnEnable()
    {
        if (subWave != null)
            subWave.OnEnemySpawned += HandleEnemySpawned;
    }

    private void OnDisable()
    {
        if (subWave != null)
            subWave.OnEnemySpawned -= HandleEnemySpawned;
    }

    internal void PrepareForWave(
        int plannedEnemyCount,
        WaveBuffDropController controller)
    {
        waveDropController = controller;
        remainingEligibleEnemyDeaths = Mathf.Max(0, plannedEnemyCount);
        issuedBuffCount = 0;
        reservedBuffCount = 0;
    }

    internal bool TrySelectReward(out Buff rewardPrefab)
    {
        rewardPrefab = null;
        if (remainingEligibleEnemyDeaths <= 0)
            return false;

        bool shouldDrop = reservedBuffCount > 0
            && Random.value <= (float)reservedBuffCount
            / remainingEligibleEnemyDeaths;
        remainingEligibleEnemyDeaths--;
        if (!shouldDrop || waveDropController == null)
            return false;

        if (!waveDropController.TrySelectReward(out rewardPrefab))
            return false;

        reservedBuffCount--;
        issuedBuffCount++;
        return true;
    }

    internal bool ReserveDropSlot()
    {
        if (issuedBuffCount + reservedBuffCount >= MaxBuffs)
            return false;

        reservedBuffCount++;
        return true;
    }

    private void HandleEnemySpawned(
        Enemy enemy,
        int spawnIndex,
        int plannedEnemyCount)
    {
        if (enemy == null || !enemy.CanContainBuff())
            return;

        EnemyBuffDrop enemyDrop = enemy.GetComponent<EnemyBuffDrop>();
        if (enemyDrop == null)
            enemyDrop = enemy.gameObject.AddComponent<EnemyBuffDrop>();

        enemyDrop.Configure(this, container);
    }

    private void OnValidate()
    {
        maxBuffs = Mathf.Max(0, maxBuffs);
    }
}
