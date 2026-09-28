using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

[MovedFrom(
    true,
    sourceNamespace: "",
    sourceAssembly: "Assembly-CSharp",
    sourceClassName: "EnemyPoisonDebuffConfig")]
[CreateAssetMenu(
    fileName = "PeriodicDamageDebuff",
    menuName = "Game/Enemy Debuffs/Periodic Damage")]
public sealed class EnemyPeriodicDamageDebuffConfig : EnemyDebuffConfig
{
    [Header("Periodic Damage")]
    [SerializeField, Min(0f)] private float damagePerTick = 5f;
    [SerializeField, Min(0.01f)] private float tickInterval = 0.5f;
    [SerializeField, Min(0.01f)] private float duration = 3f;
    [SerializeField] private EnemyDamageType damageType = EnemyDamageType.Chemical;
    [SerializeField] private bool bypassesEnemyShield = true;

    [Header("After Successful Damage Tick")]
    [Tooltip("Applied only when the periodic damage tick deals hull damage. Leave empty for no follow-up debuffs.")]
    [SerializeField] private List<EnemyDebuffApplication> tickDebuffs = new();

    public float DamagePerTick => Mathf.Max(0f, damagePerTick);
    public float TickInterval => Mathf.Max(0.01f, tickInterval);
    public float Duration => Mathf.Max(0.01f, duration);
    public EnemyDamageType DamageType => damageType;
    public bool BypassesEnemyShield => bypassesEnemyShield;
    public IReadOnlyList<EnemyDebuffApplication> TickDebuffs => tickDebuffs;

    private void OnValidate()
    {
        damagePerTick = Mathf.Max(0f, damagePerTick);
        tickInterval = Mathf.Max(0.01f, tickInterval);
        duration = Mathf.Max(0.01f, duration);
        tickDebuffs ??= new List<EnemyDebuffApplication>();
        tickDebuffs.RemoveAll(application => application == null
            || application.Debuff == null
            || !application.IsValid);
    }
}
