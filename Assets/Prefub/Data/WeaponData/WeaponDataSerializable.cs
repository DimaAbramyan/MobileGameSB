using UnityEngine;

[System.Serializable]
public class WeaponDataSerializable : MonoBehaviour
{
    public int ID;
    public Vector3 place;

    [SerializeField] private WeaponData weaponData;
    [SerializeField, Min(0)] private int energyCost = 1;

    public int EnergyCost
    {
        get
        {
            WeaponData data = ResolveWeaponData();
            return data != null
                ? data.EnergyCost
                : Mathf.Max(0, energyCost);
        }
    }

    private WeaponData ResolveWeaponData()
    {
        if (weaponData != null)
            return weaponData;

        if (TryGetComponent(out Weapon weapon) && weapon.weaponData != null)
            return weapon.weaponData;

        weapon = GetComponentInChildren<Weapon>(true);
        return weapon != null ? weapon.weaponData : null;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        energyCost = Mathf.Max(0, energyCost);
    }
#endif
}

[System.Serializable]
public class WeaponDataSer
{
    public int ID;
    public Vector3 place;
    public int energyCost;

    public WeaponDataSer(int id, Vector3 position, int energyCost = 0)
    {
        ID = id;
        place = position;
        this.energyCost = Mathf.Max(0, energyCost);
    }

    public WeaponDataSer()
    {
        ID = 0;
        place = new Vector3(0, 0, 0);
        energyCost = 0;
    }

    public int GetID()
    {
        return ID;
    }
}
