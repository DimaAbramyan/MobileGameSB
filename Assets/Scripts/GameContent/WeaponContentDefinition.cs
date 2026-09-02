using UnityEngine;

[CreateAssetMenu(fileName = "WeaponContent", menuName = "Game Content/Weapon")]
public sealed class WeaponContentDefinition : CraftContentDefinition
{
    [SerializeField] private WeaponData data;

    [Header("Platform Requirements")]
    [SerializeField, Min(1)] private int requiredPlatformTier = 1;

    public WeaponData Data => data;
    public int RequiredPlatformTier => Mathf.Max(1, requiredPlatformTier);

#if UNITY_EDITOR
    protected override void OnValidate()
    {
        base.OnValidate();
        requiredPlatformTier = Mathf.Max(1, requiredPlatformTier);
    }
#endif
}
