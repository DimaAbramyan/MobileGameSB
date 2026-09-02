using UnityEngine;

public readonly struct CraftWeaponSlotDefinition
{
    public CraftWeaponSlotDefinition(string slotId, Vector3 localPosition)
        : this(slotId, localPosition, 1)
    {
    }

    public CraftWeaponSlotDefinition(
        string slotId,
        Vector3 localPosition,
        int maxWeaponTier)
    {
        SlotId = slotId;
        LocalPosition = localPosition;
        MaxWeaponTier = Mathf.Max(1, maxWeaponTier);
    }

    public string SlotId { get; }
    public Vector3 LocalPosition { get; }
    public int MaxWeaponTier { get; }
}
