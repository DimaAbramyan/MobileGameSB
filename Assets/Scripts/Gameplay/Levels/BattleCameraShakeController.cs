using Unity.Cinemachine;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class BattleCameraShakeController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerController playerController;
    [SerializeField] private CinemachineBasicMultiChannelPerlin noise;

    [Header("Damage Shake")]
    [Min(0f)]
    [SerializeField] private float shakeStrength = 0.35f;
    [Min(0.00f)]
    [SerializeField] private float shakeDuration = 0.12f;
    [Min(0.01f)]
    [SerializeField] private float shakeFrequency = 1f;

    private ParentShip observedShip;
    private float shakeEndTime;

    private void OnEnable()
    {
        if (playerController == null)
            return;

        playerController.OnCurrentShipChanged += ObserveShip;
        ObserveShip(playerController.CurrentShip);
    }

    private void OnDisable()
    {
        if (playerController != null)
            playerController.OnCurrentShipChanged -= ObserveShip;

        if (observedShip != null)
            observedShip.OnDamageTaken -= ShakeOnDamage;

        observedShip = null;
        SetNoiseAmplitude(0f);
    }

    private void Update()
    {
        if (noise != null && Time.time >= shakeEndTime)
            SetNoiseAmplitude(0f);
    }

    private void ObserveShip(ParentShip ship)
    {
        if (observedShip == ship)
            return;

        if (observedShip != null)
            observedShip.OnDamageTaken -= ShakeOnDamage;

        observedShip = ship;

        if (observedShip != null)
            observedShip.OnDamageTaken += ShakeOnDamage;
    }

    private void ShakeOnDamage(float _)
    {
        if (noise == null || shakeStrength <= 0f)
            return;

        shakeEndTime = Mathf.Max(shakeEndTime, Time.time + shakeDuration);
        noise.FrequencyGain = shakeFrequency;
        SetNoiseAmplitude(shakeStrength);
    }

    private void SetNoiseAmplitude(float amplitude)
    {
        if (noise != null)
            noise.AmplitudeGain = amplitude;
    }
}
