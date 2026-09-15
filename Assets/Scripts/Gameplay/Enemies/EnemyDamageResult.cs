using UnityEngine;

public readonly struct EnemyDamageResult
{
    public static readonly EnemyDamageResult None = new(0f);

    public float HullDamage { get; }
    public bool DidDamageHull => HullDamage > 0f;

    public EnemyDamageResult(float hullDamage)
    {
        HullDamage = Mathf.Max(0f, hullDamage);
    }
}
