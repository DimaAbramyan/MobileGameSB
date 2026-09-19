using System.Collections;

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using Zenject;

public class PlayerController : MonoBehaviour
{
    private ParentShip currentShip;
    public ParentShip CurrentShip => currentShip;
    public event System.Action<ParentShip> OnCurrentShipChanged;

    private Vector2 currentVelocity;
    public Vector2 CurrentVelocity
    {
        get => currentVelocity;
        set => currentVelocity = value;
    }
    [SerializeField] private Rigidbody2D playerRB;
    float speed;
    Vector3 _currentSpeed;
    Vector3 _currentPosition;
    private int activeTouchId = -1;
    private int movementTouchId = -1;
    private float controlsLockedUntil;
    private float shipSwitchLockedUntil;
    private float metalDropMultiplier = 1f;
    private float metalDropMultiplierUntil;
    [SerializeField] private PlayerEffectController effectController;
    [SerializeField] private TeamBarrierController teamBarrierController;
    ShipSelect shipSelect;

    public bool ControlsLocked => Time.time < controlsLockedUntil;
    public bool ShipSwitchLocked => Time.time < shipSwitchLockedUntil;
    public PlayerEffectController Effects => EnsureEffectController();
    public TeamBarrierController TeamBarrier => EnsureTeamBarrierController();

    void Awake()
    {
        playerRB = GetComponent<Rigidbody2D>();
        shipSelect = GetComponent<ShipSelect>();
        EnsureEffectController();
    }

    private void FixedUpdate()
    {
        if (ControlsLocked)
            return;

        PositionController();
    }

    private void Update()
    {
        if (ControlsLocked)
            return;

        CaptureMovementTouch();
        ShipController();
    }

    public void LockControls(float duration)
    {
        if (duration <= 0f)
            return;

        controlsLockedUntil = Mathf.Max(
            controlsLockedUntil,
            Time.time + duration);

        activeTouchId = -1;
        movementTouchId = -1;
    }

    public void ApplyControlLoss(ParentShip owner, float duration)
    {
        if (owner == null || duration <= 0f)
            return;

        Effects.ApplyOrRefresh(
            PlayerEffectType.ControlLoss,
            PlayerEffectPolarity.Negative,
            PlayerEffectScope.SingleShip,
            owner,
            duration);
        LockControls(duration);
    }

    public void LockShipSwitching(float duration)
    {
        if (duration <= 0f)
            return;

        shipSwitchLockedUntil = Mathf.Max(
            shipSwitchLockedUntil,
            Time.time + duration);

        activeTouchId = -1;
    }

    private void PositionController()
    {
        _currentPosition = gameObject.transform.position;

        for (int i = 0; i < Input.touchCount; i++)
        {
            Touch touch = Input.GetTouch(i);
            if (touch.fingerId != movementTouchId)
                continue;

            if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
            {
                movementTouchId = -1;
                return;
            }

            MoveTowardsTouch(touch);
            return;
        }

    }

    private void CaptureMovementTouch()
    {
        if (movementTouchId != -1)
        {
            for (int i = 0; i < Input.touchCount; i++)
            {
                Touch touch = Input.GetTouch(i);
                if (touch.fingerId != movementTouchId)
                    continue;

                if (touch.phase == TouchPhase.Ended
                    || touch.phase == TouchPhase.Canceled)
                {
                    movementTouchId = -1;
                }

                if (!IsInsideGameplayViewport(touch.position))
                {
                    movementTouchId = -1;
                    return;
                }

                return;
            }

            return;
        }

        for (int i = 0; i < Input.touchCount; i++)
        {
            Touch touch = Input.GetTouch(i);
            if (touch.phase != TouchPhase.Began
                || IsPointerOverUIObject(touch)
                || !IsInsideGameplayViewport(touch.position))
            continue;

            movementTouchId = touch.fingerId;
            return;
        }
    }

    private void MoveTowardsTouch(Touch touch)
    {
        Vector3 touchPosition = Camera.main.ScreenToWorldPoint(touch.position);
        touchPosition = new Vector2(touchPosition.x, touchPosition.y);

        if ((touchPosition - _currentPosition).magnitude < 0.25f)
            _currentSpeed = (touchPosition - _currentPosition) * speed;
        else
            _currentSpeed = (touchPosition - _currentPosition).normalized * speed;

        playerRB.AddForce(_currentSpeed);
        CurrentVelocity = playerRB.linearVelocity;
    }

    private void ShipController()
    {
        for (int i = 0; i < Input.touchCount; i++)
        {
            Touch touch = Input.GetTouch(i);
            if (IsPointerOverUIObject(touch)
                || !IsInsideGameplayViewport(touch.position))
            {
                if ((touch.phase == TouchPhase.Ended
                        || touch.phase == TouchPhase.Canceled)
                    && touch.fingerId == activeTouchId)
                {
                    activeTouchId = -1;
                }

                continue;
            }

            if (touch.phase == TouchPhase.Began)
                activeTouchId = touch.fingerId;

            if ((touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
                && touch.fingerId == activeTouchId)
            {
                shipSelect.SwitchShip();
                activeTouchId = -1;
            }
        }
    }

    private bool IsPointerOverUIObject(Touch touch)
    {
        return EventSystem.current != null
            && EventSystem.current.IsPointerOverGameObject(touch.fingerId);
    }

    private static bool IsInsideGameplayViewport(Vector2 screenPosition)
    {
        Camera gameplayCamera = Camera.main;
        return gameplayCamera != null
            && gameplayCamera.pixelRect.Contains(screenPosition);
    }

    /// <summary>
    /// Смена текущего корабля
    /// </summary>
    /// <param name="currShip"></param>
    public void ChangeShipData(ParentShip currShip)
    {
        currentShip = currShip;

        playerRB.mass = currShip.ShipData.mass;
        playerRB.linearDamping = currShip.ShipData.drag;
        speed = currShip.ShipData.MetaSpeed;

        OnCurrentShipChanged?.Invoke(currentShip);
    }
    public void ChangeCurrentShip(ParentShip ship)
    {
        OnCurrentShipChanged?.Invoke(ship);
    }

    public int LevelUpAllShips()
    {
        return shipSelect != null ? shipSelect.LevelUpAllShips() : 0;
    }

    public bool HandleShipDeath(ParentShip ship)
    {
        return shipSelect != null && shipSelect.HandleShipDeath(ship);
    }

    public void MultiplyTeamMagnetRadiusForSeconds(
        float multiplier,
        float duration)
    {
        if (multiplier <= 0f || duration <= 0f)
            return;

        Effects.ApplyOrRefresh(
            PlayerEffectType.TeamMagnetBoost,
            PlayerEffectPolarity.Positive,
            PlayerEffectScope.Team,
            null,
            duration);
        shipSelect?.ForEachAvailableShip(ship =>
            ship.MultiplyMagnetRadiusForSeconds(multiplier, duration));
    }

    public void SetTeamDamageInvulnerableForSeconds(float duration)
    {
        if (duration <= 0f)
            return;

        Effects.ApplyOrRefresh(
            PlayerEffectType.TeamInvulnerability,
            PlayerEffectPolarity.Positive,
            PlayerEffectScope.Team,
            null,
            duration);
        shipSelect?.ForEachAvailableShip(ship =>
            ship.SetDamageInvulnerableForSeconds(duration));
    }

    public void MultiplyTeamFireRateForSeconds(
        float multiplier,
        float duration)
    {
        if (multiplier <= 0f || duration <= 0f)
            return;

        Effects.ApplyOrRefresh(
            PlayerEffectType.TeamFireRate,
            PlayerEffectPolarity.Positive,
            PlayerEffectScope.Team,
            null,
            duration);
        shipSelect?.ForEachAvailableShip(ship =>
        {
            WeaponController weaponController =
                ship.GetComponent<WeaponController>();
            weaponController?.ActivateFireRateMultiplier(multiplier, duration);
        });
    }

    public float GetTeamAbilityRecoveryNeed()
    {
        float recoveryNeed = 0f;
        shipSelect?.ForEachAvailableShip(ship =>
            recoveryNeed += ship.AbilityRecoveryNeed);
        return recoveryNeed;
    }

    public bool RestoreTeamAbilityRecoveryResources()
    {
        bool restoredAnyResource = false;
        shipSelect?.ForEachAvailableShip(ship =>
        {
            if (ship.RestoreAbilityRecoveryResources())
                restoredAnyResource = true;
        });

        if (restoredAnyResource)
        {
            Effects.RecordInstant(
                PlayerEffectType.AbilityCooldownReset,
                PlayerEffectPolarity.Positive,
                PlayerEffectScope.Team);
        }

        return restoredAnyResource;
    }

    public bool CreateTeamBarrier(float health)
    {
        return EnsureTeamBarrierController().Activate(health);
    }

    public void MultiplyMetalDropsForSeconds(float multiplier, float duration)
    {
        if (multiplier <= 0f || duration <= 0f)
            return;

        Effects.ApplyOrRefresh(
            PlayerEffectType.TeamMetalDropBoost,
            PlayerEffectPolarity.Positive,
            PlayerEffectScope.Team,
            null,
            duration);

        bool hasActiveMultiplier = Time.time < metalDropMultiplierUntil;
        metalDropMultiplier = hasActiveMultiplier
            ? Mathf.Max(metalDropMultiplier, multiplier)
            : multiplier;
        metalDropMultiplierUntil = Mathf.Max(
            metalDropMultiplierUntil,
            Time.time + duration);
    }

    public int GetModifiedMetalDropAmount(int baseAmount)
    {
        if (baseAmount <= 0)
            return 0;

        float multiplier = Time.time < metalDropMultiplierUntil
            ? metalDropMultiplier
            : 1f;
        return Mathf.Max(1, Mathf.CeilToInt(baseAmount * multiplier));
    }

    private PlayerEffectController EnsureEffectController()
    {
        if (effectController == null)
            effectController = GetComponent<PlayerEffectController>();
        if (effectController == null)
            effectController = gameObject.AddComponent<PlayerEffectController>();

        return effectController;
    }

    private TeamBarrierController EnsureTeamBarrierController()
    {
        if (teamBarrierController == null)
            teamBarrierController = GetComponent<TeamBarrierController>();
        if (teamBarrierController == null)
            teamBarrierController = gameObject.AddComponent<TeamBarrierController>();

        return teamBarrierController;
    }
}
