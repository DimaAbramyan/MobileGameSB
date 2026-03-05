using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
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
    [Header("StartLevel")]
    public int currentLvl;

    [Space(10)]
    [Header("Meta")]
    public int shipId;

}

