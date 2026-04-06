using System.Collections.Generic;
using UnityEngine;
using FMODUnity;

[CreateAssetMenu(fileName = "NewShipData", menuName = "Game/Weapon Data")]
public class WeaponData : ScriptableObject
{
    [Header("Stats per level")]
    [SerializeField] private List<float> reloadTimeByLevel;
    [SerializeField] private List<float> angleByLevel;
    [SerializeField] private List<float> damageByLevel;
    [SerializeField] private List<float> rangeByLevel;
    [SerializeField] private List<float> speedByLevel;

    [Header("Levels")]
    [SerializeField] private int startLevel;
    [SerializeField] private int maxLevel = 4;

    [Header("Behaviours")]
    [SerializeField] private MovementStrategySO movementStrategy;
    [SerializeField] private ImpactBehaviorSO impactBehavior;
    [SerializeField] private ContiniousImpactBehaviorSO continiousImpactBehavior;
    [SerializeField] private ProjectileBehaviourSO[] projectileBehaviour;

    [Header("Audio")]
    [SerializeField] private EventReference audioClipDefault;
    [SerializeField] private EventReference audioClipProjectileShot;

    // ---------- READ ONLY PROPERTIES ----------

    public IReadOnlyList<float> ReloadTimeByLevel => reloadTimeByLevel;
    public IReadOnlyList<float> AngleByLevel => angleByLevel;
    public IReadOnlyList<float> DamageByLevel => damageByLevel;
    public IReadOnlyList<float> RangeByLevel => rangeByLevel;
    public IReadOnlyList<float> SpeedByLevel => speedByLevel;

    public int StartLevel => startLevel;
    public int MaxLevel => maxLevel;

    public MovementStrategySO MovementStrategy => movementStrategy;
    public ImpactBehaviorSO ImpactBehavior => impactBehavior;
    public ContiniousImpactBehaviorSO ContiniousImpactBehavior => continiousImpactBehavior;
    public IReadOnlyList<ProjectileBehaviourSO> ProjectileBehaviour => projectileBehaviour;

    public EventReference AudioClipDefault => audioClipDefault;
    public EventReference AudioClipProjectileShot => audioClipProjectileShot;

    // ---------- AUDIO HELPERS ----------

    public void PlayDefaultSound(Vector3 position)
    {
        if (audioClipDefault.IsNull)
            return;

        RuntimeManager.PlayOneShot(audioClipDefault, position);
    }

    public void PlayShotSound(Vector3 position)
    {
        if (audioClipProjectileShot.IsNull)
            return;

        RuntimeManager.PlayOneShot(audioClipProjectileShot, position);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (Application.isPlaying)
            Debug.LogError($"{name} ScriptableObject is being modified during Play Mode!");
    }
#endif
}