using UnityEngine;

[CreateAssetMenu(fileName = "NewShipData", menuName = "Game/Ship Data")]
public class ShipData : ScriptableObject
{
    [Header("Controllability")]

    public float speed;
    public float mass;
    public float drag;

    [Space(10)]
    [Header("Health")]
    public float maximumHealthPoints;
    public float healthRegenCooldown;
    public float healthRegenRate;

    [Space(10)]
    [Header("Shield")]
    public float maximumShieldPoints;
    public float shieldRegenCooldown;
    public float shieldRegenRate;

    [Space(10)]
    [Header("Build Limits")]
    [Min(0)] public int maximumEnergy = 10;
    [Min(0)] public int maximumWeaponCount = 4;

    [Space(10)]
    [Header("StartLevel")]
    public int currentLvl;

    [Space(10)]
    [Header("Meta")]
    public int shipId;

#if UNITY_EDITOR
    private void OnValidate()
    {
        maximumEnergy = Mathf.Max(0, maximumEnergy);
        maximumWeaponCount = Mathf.Max(0, maximumWeaponCount);
    }
#endif
}

