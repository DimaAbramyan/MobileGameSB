using Unity.Mathematics;
using UnityEngine;
using Zenject;

[DefaultExecutionOrder(-600)]
public class BlackHoleActive : ActiveAbility
{
    [Inject] private EnemyProjectileCollisionRegistry projectileCollisionRegistry;
    [Inject] private PlayerController playerController;

    // Preserve references on existing hulls; the old spawned hole is no longer used.
    [SerializeField, HideInInspector] private BlackHolePrefab blackHole;
    [SerializeField, HideInInspector] private float duration;
    private BlackHoleShipMetaContract settings = new();
    private ParentShip activeShip;
    private PassiveAbility suspendedPassive;
    private PlayerEffectController effects;
    private Camera gameplayCamera;
    private Rect gameplayBounds;
    private int boundsFrame = -1;
    private float activeUntil;
    private bool active;

    public float ActiveDuration => settings.ActiveDuration;
    public float ActiveCooldown => CooldownDuration;
    public float FrontSpeedMultiplier => settings.FrontSpeedMultiplier;
    public float RearSpeedMultiplier => settings.RearSpeedMultiplier;
    public bool RewriteAttackSpeed => settings.RewriteAttackSpeed;
    public bool EnablePassiveAbility => settings.EnablePassiveAbility;
    public bool IsActive => active && IsOwnerValid && Time.time < activeUntil;
    private bool IsOwnerValid => activeShip != null && activeShip.isActiveAndEnabled
        && activeShip.IsVisible && !activeShip.IsDestroyed
        && (playerController == null || playerController.CurrentShip == activeShip);

    protected override void ApplySpecificShipMetaStats(ShipMetaRuntimeStats stats)
    {
        if (!stats.TryGetContract(out BlackHoleShipMetaContract contract)) return;
        settings = (BlackHoleShipMetaContract)contract.Clone();
        cooldown = settings.ActiveCooldown;
    }

    public override bool Activate(ParentShip ship)
    {
        if (active && !IsActive) StopActive();
        if (active || ship == null || !ship.IsVisible || ship.IsDestroyed
            || projectileCollisionRegistry == null) return false;
        gameplayCamera = playerController != null ? playerController.GameplayCamera
            : ship.GetComponentInParent<PlayerController>()?.GameplayCamera;
        if (!EnemyEmissionOriginSettings.TryGetGameplayBounds(gameplayCamera, ship.transform.position.z, out gameplayBounds))
        {
            Debug.LogError("BlackHole active ability needs the PlayerController Gameplay Camera to resolve the battle field.", this);
            return false;
        }
        activeShip = ship;
        activeUntil = Time.time + ActiveDuration;
        boundsFrame = Time.frameCount;
        active = true;
        if (!EnablePassiveAbility && ship.PassiveAbility is BlackHolePassive passive
            && passive.TryGetSlowField(out _))
        {
            suspendedPassive = passive;
            suspendedPassive.Off();
        }
        effects = playerController != null ? playerController.Effects
            : ship.GetComponentInParent<PlayerController>()?.Effects;
        effects?.ApplyOrRefresh(PlayerEffectType.BlackHoleActive, PlayerEffectPolarity.Positive,
            PlayerEffectScope.SingleShip, ship, ActiveDuration);
        projectileCollisionRegistry.RegisterRegionSpeedField(this);
        if (audioManager != null && audioDatabase != null)
            audioManager.PlaySound(audioDatabase.blackHole, ship.transform.position);
        return true;
    }

    public bool TryGetRegionSpeedField(out EnemyProjectileRegionSpeedField field)
    {
        field = default;
        if (!IsActive) return false;
        if (boundsFrame != Time.frameCount)
        {
            boundsFrame = Time.frameCount;
            if (!EnemyEmissionOriginSettings.TryGetGameplayBounds(gameplayCamera, activeShip.transform.position.z, out gameplayBounds)) return false;
        }
        field = new EnemyProjectileRegionSpeedField
        {
            Minimum = new float2(gameplayBounds.xMin, gameplayBounds.yMin),
            Maximum = new float2(gameplayBounds.xMax, gameplayBounds.yMax),
            DividerY = activeShip.transform.position.y,
            FrontMultiplier = FrontSpeedMultiplier,
            RearMultiplier = RearSpeedMultiplier
        };
        return true;
    }

    protected override void Update()
    {
        base.Update();
        if (active && !IsActive) StopActive();
    }

    // Restore a suspended passive before the collision registry snapshots the next physics step.
    private void FixedUpdate()
    {
        if (active && !IsActive) StopActive();
    }

    private void OnDisable() => StopActive();

    private void StopActive()
    {
        if (!active) return;
        active = false;
        if (suspendedPassive != null && activeShip != null && activeShip.isActiveAndEnabled
            && activeShip.IsVisible && !activeShip.IsDestroyed)
            suspendedPassive.On();
        suspendedPassive = null;
        projectileCollisionRegistry?.UnregisterRegionSpeedField(this);
        effects?.Remove(PlayerEffectType.BlackHoleActive, PlayerEffectScope.SingleShip, activeShip);
        effects = null;
        activeShip = null;
        NotifyAbilityDeactivated();
    }

    private void OnDrawGizmosSelected()
    {
        if (!TryGetRegionSpeedField(out var field)) return;
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(new Vector3(field.Minimum.x, field.DividerY, transform.position.z),
            new Vector3(field.Maximum.x, field.DividerY, transform.position.z));
    }
}
