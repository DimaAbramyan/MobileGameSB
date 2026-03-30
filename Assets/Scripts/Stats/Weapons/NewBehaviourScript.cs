using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewShipData", menuName = "Game/Weapon Data")]
public class WeaponData : ScriptableObject
{
    [SerializeField] public List<float> reloadTimeByLevel;
    [SerializeField] public List<float> angleByLevel;
    [SerializeField] public List<float> damageByLevel;
    [SerializeField] public List<float> rangeByLevel;
    [SerializeField] public List<float> speedByLevel;
    [SerializeField] public int startLevel;
    [SerializeField] public int maxLevel = 4;

    [SerializeField] public MovementStrategySO movementStrategy;
    [SerializeField] public ImpactBehaviorSO impactBehavior;
    [SerializeField] public ContiniousImpactBehaviorSO continiousImpactBehavior;
    [SerializeField] public ProjectileBehaviourSO[] projectileBehaviour;

}
