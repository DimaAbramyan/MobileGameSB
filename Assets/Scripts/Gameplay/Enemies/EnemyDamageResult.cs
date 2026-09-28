using UnityEngine;

public readonly struct EnemyDamageResult
{
    public static readonly EnemyDamageResult None = new(0f, 0f);

    public float HullDamage { get; }
    public float ShieldDamage { get; }
    public float TotalDamage => HullDamage + ShieldDamage;
    public bool DidDamage => TotalDamage > 0f;
    public bool DidDamageHull => HullDamage > 0f;

    public EnemyDamageResult(float hullDamage, float shieldDamage = 0f)
    {
        HullDamage = Mathf.Max(0f, hullDamage);
        ShieldDamage = Mathf.Max(0f, shieldDamage);
    }
}
