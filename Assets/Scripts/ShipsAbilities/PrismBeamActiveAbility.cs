using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using Zenject;

public sealed class PrismBeamActiveAbility : ActiveAbility
{
    [Inject] private DealDamageManager dealDamageManager;
    [Inject] private EnemyProjectileCollisionRegistry projectileRegistry;

    [FormerlySerializedAs("defaultBeamDamage")]
    [SerializeField, HideInInspector, Min(0f)] private float defaultPlasmaDamage = 150f;
    [FormerlySerializedAs("defaultBeamDamageBonusPercent")]
    [SerializeField, HideInInspector, Min(0f)] private float defaultPlasmaDamageBonusPercent = 15f;
    [FormerlySerializedAs("defaultEnergyDamageBonusPercent")]
    [SerializeField, HideInInspector, Min(0f)] private float defaultElectricDamageBonusPercent = 10f;
    [SerializeField, HideInInspector] private GameObject defaultWavePrefab;

    private PrismShipMetaContract metaContract;
    private float abilityDamageMultiplier = 1f;
    private readonly List<PrismEnergyWave> waves = new();

    protected override void ApplySpecificShipMetaStats(ShipMetaRuntimeStats stats)
    {
        stats.TryGetContract(out PrismShipMetaContract contract);
        metaContract = contract != null ? (PrismShipMetaContract)contract.Clone() : null;
    }

    public override void ApplyBattleAbilityDamageMultiplier(float damageMultiplier)
        => abilityDamageMultiplier = Mathf.Max(1f, damageMultiplier);

    public PrismShipMetaContract CreateDefaultMetaContract()
    {
        var contract = new PrismShipMetaContract();
        contract.Configure(defaultPlasmaDamage, defaultPlasmaDamageBonusPercent, defaultElectricDamageBonusPercent);
        contract.ConfigureWave(defaultWavePrefab, 1f, 4f, .3f, 1.5f, 15f, EnemyDamageType.Plazma, false);
        return contract;
    }

    public override bool Activate(ParentShip activationOwner)
    {
        if (activationOwner == null || activationOwner.IsDestroyed || metaContract == null || dealDamageManager == null)
            return false;
        if (metaContract.WavePrefab == null)
        {
            Debug.LogError("Prism energy wave requires WavePrefab in PrismMeta.", this);
            return false;
        }
        if (metaContract.DestroyProjectiles && projectileRegistry == null)
        {
            Debug.LogError("Prism energy wave requires the enemy projectile registry.", this);
            return false;
        }
        Vector2 direction = activationOwner.transform.up;
        if (direction.sqrMagnitude < .0001f) return false;
        direction.Normalize();
        var root = new GameObject("Prism Energy Wave");
        root.transform.position = activationOwner.transform.position + (Vector3)(direction * metaContract.SpawnOffset);
        root.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f);
        var wave = root.AddComponent<PrismEnergyWave>();
        wave.Initialize(activationOwner, metaContract, abilityDamageMultiplier, dealDamageManager, projectileRegistry);
        for (int i = waves.Count - 1; i >= 0; i--)
            if (waves[i] == null) waves.RemoveAt(i);
        waves.Add(wave);
        return true;
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        for (int i = waves.Count - 1; i >= 0; i--)
        {
            if (waves[i] == null) continue;
            if (Application.isPlaying) Destroy(waves[i].gameObject);
            else DestroyImmediate(waves[i].gameObject);
        }
        waves.Clear();
    }

    private void OnDrawGizmosSelected()
    {
        ParentShip ship = GetComponent<ParentShip>();
        PrismShipMetaContract config = metaContract;
        if (config == null && ship != null && ship.ShipData != null)
            ship.ShipData.GetMetaRuntimeStats(0).TryGetContract(out config);
        if (config == null) return;
        Matrix4x4 previous = Gizmos.matrix; Color previousColor = Gizmos.color;
        Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);
        Gizmos.color = new Color(.1f, .85f, 1f);
        Vector3 size = new Vector3(config.WaveWidth, config.WaveThickness, .01f);
        Vector3 start = Vector3.up * config.SpawnOffset;
        Vector3 end = start + Vector3.up * config.MaxDistance;
        Gizmos.DrawWireCube(start, size); Gizmos.DrawLine(start, end); Gizmos.DrawWireCube(end, size);
        Gizmos.matrix = previous; Gizmos.color = previousColor;
    }
}
