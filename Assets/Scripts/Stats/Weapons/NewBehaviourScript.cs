using System.Collections.Generic;
using UnityEngine;
using FMODUnity;
using UnityEngine.Serialization;

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
    [FormerlySerializedAs("movementMode")]
    [SerializeField] private ProjectileFlightMode flightMode = ProjectileFlightMode.Straight;
    [SerializeField] private ProjectileContactMode contactMode = ProjectileContactMode.DamageAndDestroy;
    [SerializeField] private float homingRotationSpeed = 360f;
    [SerializeField] private bool growDuringFlight;
    [SerializeField] private Vector2 scaleGrowthPerSecond = Vector2.one * 0.5f;

    [Header("Lifetime")]
    [SerializeField, Min(0.02f)] private float projectileLifetime = 10f;
    [SerializeField] private bool disableColliderAfterFirstPhysicsStep;
    [SerializeField] private bool fadeDuringLifetime;
    [SerializeField, Min(0.02f)] private float fadeDuration = 0.5f;

    [Header("Contact")]
    [SerializeField] private Explode explosionPrefab;
    [SerializeField] private float explosionDamage = 30f;
    [SerializeField, Min(0.02f)] private float continuousDamageInterval = 0.25f;

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

    public ProjectileFlightMode FlightMode => flightMode;
    public ProjectileContactMode ContactMode => contactMode;
    public float HomingRotationSpeed => homingRotationSpeed;
    public bool GrowDuringFlight => growDuringFlight;
    public Vector2 ScaleGrowthPerSecond => scaleGrowthPerSecond;
    public float ProjectileLifetime => projectileLifetime;
    public bool DisableColliderAfterFirstPhysicsStep =>
        disableColliderAfterFirstPhysicsStep;
    public bool FadeDuringLifetime => fadeDuringLifetime;
    public float FadeDuration => fadeDuration;
    public Explode ExplosionPrefab => explosionPrefab;
    public float ExplosionDamage => explosionDamage;
    public float ContinuousDamageInterval => continuousDamageInterval;

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
        projectileLifetime = Mathf.Max(0.02f, projectileLifetime);
        fadeDuration = Mathf.Clamp(
            fadeDuration,
            0.02f,
            projectileLifetime);
    }
#endif
}
