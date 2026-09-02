using UnityEngine;

/// <summary>
/// A named hardpoint on a hull prefab used by the craft editor.
/// </summary>
public sealed class CraftWeaponSlotAnchor : MonoBehaviour
{
    [SerializeField] private string slotId = "weapon_slot";
    [SerializeField] private Transform weaponMount;
    [SerializeField] private Vector2 buttonOffset;
    [SerializeField] private Vector2 buttonSize = new(64f, 64f);
    [SerializeField] private Vector2 weaponIconScale = Vector2.one;

    public string SlotId => slotId;
    public Transform WeaponMount => weaponMount != null ? weaponMount : transform;
    public Vector2 ButtonOffset => buttonOffset;
    public Vector2 ButtonSize => new(
        Mathf.Max(1f, buttonSize.x),
        Mathf.Max(1f, buttonSize.y));
    public Vector2 WeaponIconScale => new(
        Mathf.Max(0.01f, weaponIconScale.x),
        Mathf.Max(0.01f, weaponIconScale.y));

#if UNITY_EDITOR
    private void OnValidate()
    {
        slotId = slotId?.Trim();
        buttonSize.x = Mathf.Max(1f, buttonSize.x);
        buttonSize.y = Mathf.Max(1f, buttonSize.y);
        weaponIconScale.x = Mathf.Max(0.01f, weaponIconScale.x);
        weaponIconScale.y = Mathf.Max(0.01f, weaponIconScale.y);
    }
#endif
}
