using System.Collections;

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Zenject;

public class PlayerController : MonoBehaviour, ISerializationCallbackReceiver
{
    private ParentShip currentShip;
    public ParentShip CurrentShip => currentShip;
    public Camera GameplayCamera => gameplayCamera;
    public event System.Action<ParentShip> OnCurrentShipChanged;
    public event System.Action OnTeamShipsRevived;
    public event System.Action<int> OnTeamBattleLevelChanged;
    private int teamBattleLevel = ParentShip.MinWeaponLevel;
    public int TeamBattleLevel => teamBattleLevel;

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
    private bool hasPreviousShipTouch;
    private float previousShipTouchTime;
    private Vector2 previousShipTouchPosition;
    private int movementTouchId = -1;
    private float controlsLockedUntil;
    private float shipSwitchLockedUntil;
    private float metalDropMultiplier = 1f;
    private float metalDropMultiplierUntil;
    [Header("Movement")]
    [SerializeField, Tooltip("Physics Touch: прежнее управление силами Rigidbody. Direct Touch: перемещение по delta пальца. Joystick: прямое движение по отклонению ручки без инерции.")]
    private PlayerMovementMode movementMode = PlayerMovementMode.DirectTouch;
    [SerializeField] private Camera gameplayCamera;
    [SerializeField] private PlayerJoystick joystick;
    [SerializeField, Tooltip("Для Joystick Focus: включено — переключение повторным нажатием, выключено — отдельной кнопкой.")]
    private bool switchShipOnFocusRelease;
    [SerializeField, Tooltip("Кнопка переключения корабля для Joystick Focus. Скрывается, если выбрано переключение при отпускании.")]
    private Button shipSwitchButton;
    [Header("Ship Switching")]
    [SerializeField, Range(0.05f, 1f), Tooltip("Максимальный интервал от отпускания пальца до следующего нажатия, в секундах.")]
    private float shipSwitchPressWindow = 0.25f;
    [SerializeField, Range(0f, 25f), Tooltip("Максимальное расстояние от последней позиции отпущенного пальца до нового нажатия, в процентах короткой стороны экрана. Кнопка активной способности не участвует; остальной HUD участвует.")]
    private float shipSwitchTouchDistancePercent = 4f;
    [SerializeField, Min(0.001f), Tooltip("Перевод ShipData.MetaSpeed в мировые единицы в секунду для джойстика. Например, 300 × 0.02 = 6 ед/с.")]
    private float joystickSpeedMultiplier = 0.02f;
    [SerializeField, Tooltip("Заменить скорость корпуса общей максимальной скоростью для управления джойстиком.")]
    private bool overrideMaxSpeed;
    [SerializeField, Min(0f), Tooltip("Максимальная скорость джойстиком в мировых единицах в секунду. Не умножается на скорость корпуса или Joystick Speed Multiplier. Analog по-прежнему уменьшает скорость при неполном отклонении ручки.")]
    private float maxSpeed = 6f;
    // Kept solely to migrate the previously serialized bool to the enum.
    [SerializeField, HideInInspector]
    private bool useDirectTouchMovement = true;
    [SerializeField, HideInInspector] private int movementSerializationVersion;
    [SerializeField, Min(0.01f), Tooltip("Finger displacement multiplier in direct mode. 1 follows the finger's world-space displacement. Ship speed, mass and drag only affect the original physics mode.")]
    private float directTouchSensitivity = 1f;
    [SerializeField, Range(-1f, 1f), Tooltip("World-space Y offset added to the touch target in the original physics mode. Direct movement keeps the initial finger-to-ship offset automatically.")]
    private float touchOffsetY;
    private Vector2 previousTouchPosition;
    private bool hasDirectTouchAnchor;
    private readonly List<RaycastResult> movementUIHits = new List<RaycastResult>();
    private PointerEventData movementPointer;
    private EventSystem movementEventSystem;
    private bool movementModeInitialized;
    private PlayerMovementMode appliedMovementMode;
    private PlayerJoystick boundJoystick;
    private Button boundShipSwitchButton;
    private bool appliedSwitchShipOnFocusRelease;
    private RigidbodyType2D originalBodyType;
    private RigidbodyInterpolation2D originalInterpolation;
    private bool originalFullKinematicContacts;
    [Header("Direct Movement Knockback")]
    [SerializeField, Min(0f), Tooltip("Максимальная скорость отталкивания для Direct Touch и Joystick, в мировых единицах в секунду.")]
    private float maximumKnockbackSpeed = 10f;
    [SerializeField, Min(0.01f), Tooltip("Уменьшение скорости отталкивания в секунду. На обычное управление и остановку ручки не влияет.")]
    private float knockbackDeceleration = 30f;
    private Vector2 knockbackVelocity;
    [SerializeField] private PlayerEffectController effectController;
    [SerializeField] private TeamBarrierController teamBarrierController;
    ShipSelect shipSelect;

    public bool ControlsLocked => Time.time < controlsLockedUntil;
    public PlayerActionLocks ActionLocks { get; } = new();
    public bool MovementLocked => ControlsLocked || ActionLocks.Blocks(PlayerActions.Movement);
    public PlayerMovementMode MovementMode => movementMode;
    public float JoystickMaxSpeed => overrideMaxSpeed
        ? Mathf.Max(0f, maxSpeed)
        : Mathf.Max(0f, speed * joystickSpeedMultiplier);
    public bool UsesDirectMovement => movementMode != PlayerMovementMode.PhysicsTouch;
    private bool UsesJoystick => movementMode == PlayerMovementMode.Joystick
        || movementMode == PlayerMovementMode.JoystickFocus;
    private bool UsesShipSwitchButton => movementMode == PlayerMovementMode.JoystickFocus
        && !switchShipOnFocusRelease;
    public bool ShipSwitchLocked => Time.time < shipSwitchLockedUntil || ActionLocks.Blocks(PlayerActions.ShipSwitch);
    public float ShipSwitchPressWindow => Mathf.Clamp(shipSwitchPressWindow, 0.05f, 1f);
    public float ShipSwitchTouchDistancePercent => Mathf.Clamp(shipSwitchTouchDistancePercent, 0f, 25f);
    public PlayerEffectController Effects => EnsureEffectController();
    public TeamBarrierController TeamBarrier => EnsureTeamBarrierController();

    void Awake()
    {
        playerRB = GetComponent<Rigidbody2D>();
        shipSelect = GetComponent<ShipSelect>();
        if (gameplayCamera == null)
            gameplayCamera = Camera.main;
        EnsureEffectController();
        SynchronizeMovementMode();
    }

    private void FixedUpdate()
    {
        SynchronizeMovementMode();
        if (MovementLocked || UsesDirectMovement)
            return;

        PositionController();
    }

    private void Update()
    {
        SynchronizeMovementMode();
        if (UsesDirectMovement)
            CurrentVelocity = Vector2.zero;
        if (!ControlsLocked)
        {
            if (ActionLocks.Blocks(PlayerActions.Movement))
            {
                ResetMovementTouch();
                CurrentVelocity = Vector2.zero;
                if (playerRB != null) playerRB.linearVelocity = Vector2.zero;
            }
            else if (UsesJoystick)
                MoveByJoystick(Time.deltaTime);
            else
            {
                CaptureMovementTouch();
                if (movementMode == PlayerMovementMode.DirectTouch)
                    MoveByTouchDelta();
            }
            ShipController();
        }
        else
            ResetShipSwitchTouch();
        // Contact recoil continues while enemy impact temporarily locks input.
        ApplyDirectKnockback(Time.deltaTime);
    }

    public void LockControls(float duration)
    {
        if (duration <= 0f)
            return;

        controlsLockedUntil = Mathf.Max(
            controlsLockedUntil,
            Time.time + duration);

        ResetShipSwitchTouch();
        ResetMovementTouch();
        joystick?.CancelInput();
        if (UsesDirectMovement)
            CurrentVelocity = Vector2.zero;
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

        ResetShipSwitchTouch();
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
                ResetMovementTouch();
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
                    ResetMovementTouch();
                }

                if (!IsInsideMovementArea(touch.position))
                {
                    ResetMovementTouch();
                    return;
                }

                return;
            }

            ResetMovementTouch();
            return;
        }

        for (int i = 0; i < Input.touchCount; i++)
        {
            Touch touch = Input.GetTouch(i);
            if (touch.phase != TouchPhase.Began
                || !CanStartMovementTouch(touch.position, touch.fingerId))
            continue;

            movementTouchId = touch.fingerId;
            return;
        }
    }

    private void MoveTowardsTouch(Touch touch)
    {
        if (gameplayCamera == null || playerRB == null)
            return;
        Vector3 touchPosition = gameplayCamera.ScreenToWorldPoint(touch.position);
        touchPosition = new Vector2(touchPosition.x, touchPosition.y);
        touchPosition.y += touchOffsetY;

        if ((touchPosition - _currentPosition).magnitude < 0.25f)
            _currentSpeed = (touchPosition - _currentPosition) * speed;
        else
            _currentSpeed = (touchPosition - _currentPosition).normalized * speed;

        playerRB.AddForce(_currentSpeed);
        CurrentVelocity = playerRB.linearVelocity;
    }

    private void MoveByTouchDelta()
    {
        for (int index = 0; index < Input.touchCount; index++)
        {
            Touch touch = Input.GetTouch(index);
            if (touch.fingerId != movementTouchId)
                continue;

            ProcessDirectTouch(touch.position, touch.phase, gameplayCamera, Time.deltaTime);
            return;
        }

        ResetMovementTouch();
    }

    private void ProcessDirectTouch(Vector2 screenPosition, TouchPhase phase,
        Camera gameplayCamera, float deltaTime)
    {
        CurrentVelocity = Vector2.zero;
        if (phase == TouchPhase.Ended || phase == TouchPhase.Canceled)
        {
            ResetMovementTouch();
            CurrentVelocity = Vector2.zero;
            return;
        }

        if (phase == TouchPhase.Began || !hasDirectTouchAnchor)
        {
            previousTouchPosition = screenPosition;
            hasDirectTouchAnchor = true;
            return;
        }

        Vector2 previous = previousTouchPosition;
        previousTouchPosition = screenPosition;
        if (phase != TouchPhase.Moved || gameplayCamera == null || deltaTime <= 0f)
            return;

        Vector3 position = transform.position;
        float depth = gameplayCamera.WorldToScreenPoint(position).z;
        Vector3 from = gameplayCamera.ScreenToWorldPoint(new Vector3(previous.x, previous.y, depth));
        Vector3 to = gameplayCamera.ScreenToWorldPoint(new Vector3(screenPosition.x, screenPosition.y, depth));
        Vector3 next = position + (to - from) * directTouchSensitivity;
        MoveDirectly(next, gameplayCamera, deltaTime);
    }

    private void MoveByJoystick(float deltaTime)
    {
        if (joystick == null || !joystick.isActiveAndEnabled || deltaTime <= 0f)
            return;
        Vector2 direction = movementMode == PlayerMovementMode.JoystickFocus
            ? joystick.FocusInputDirection
            : joystick.InputDirection;
        Vector2 delta = direction * JoystickMaxSpeed * deltaTime;
        MoveDirectly(transform.position + (Vector3)delta, gameplayCamera, deltaTime);
    }

    private void MoveDirectly(Vector3 next, Camera camera, float deltaTime)
    {
        Vector3 position = transform.position;
        if (camera != null)
        {
            Vector3 viewport = camera.WorldToViewportPoint(next);
            viewport.x = Mathf.Clamp01(viewport.x);
            viewport.y = Mathf.Clamp01(viewport.y);
            next = camera.ViewportToWorldPoint(viewport);
        }
        next.z = position.z;
        transform.position = next;
        if (deltaTime > 0f)
            CurrentVelocity += (Vector2)(next - position) / deltaTime;
    }

    public void ApplyKnockback(Vector2 impulse)
    {
        if (!UsesDirectMovement)
        {
            if (playerRB != null)
                playerRB.AddForce(impulse, ForceMode2D.Impulse);
            return;
        }

        float mass = playerRB != null ? Mathf.Max(0.001f, playerRB.mass) : 1f;
        knockbackVelocity = Vector2.ClampMagnitude(
            knockbackVelocity + impulse / mass, maximumKnockbackSpeed);
    }

    private void ApplyDirectKnockback(float deltaTime)
    {
        if (!UsesDirectMovement || deltaTime <= 0f || knockbackVelocity == Vector2.zero)
            return;

        Vector2 before = knockbackVelocity;
        float duration = Mathf.Min(deltaTime, before.magnitude / knockbackDeceleration);
        knockbackVelocity = Vector2.MoveTowards(before, Vector2.zero, knockbackDeceleration * deltaTime);
        Vector2 delta = (before + knockbackVelocity) * (0.5f * duration);
        MoveDirectly(transform.position + (Vector3)delta, gameplayCamera, deltaTime);
    }

    public void SwitchShipFromButton()
    {
        if (UsesShipSwitchButton && isActiveAndEnabled && !ControlsLocked && Time.timeScale > 0f)
            shipSelect?.SwitchShip();
    }

    private void ResetMovementTouch()
    {
        movementTouchId = -1;
        hasDirectTouchAnchor = false;
    }

    private void SynchronizeMovementMode()
    {
        if (movementModeInitialized && appliedMovementMode == movementMode && boundJoystick == joystick
            && boundShipSwitchButton == shipSwitchButton
            && appliedSwitchShipOnFocusRelease == switchShipOnFocusRelease)
            return;

        if (!movementModeInitialized && playerRB != null)
        {
            originalBodyType = playerRB.bodyType;
            originalInterpolation = playerRB.interpolation;
            originalFullKinematicContacts = playerRB.useFullKinematicContacts;
        }

        ResetMovementTouch();
        ResetShipSwitchTouch();
        knockbackVelocity = Vector2.zero;
        CurrentVelocity = Vector2.zero;
        boundJoystick = joystick;
        if (boundJoystick != null)
        {
            boundJoystick.CancelInput();
            boundJoystick.gameObject.SetActive(UsesJoystick);
            if (movementMode == PlayerMovementMode.JoystickFocus && boundJoystick.FocusSettings == null)
                Debug.LogError("Joystick Focus requires PlayerJoystickFocusSettings on PlayerJoystick.", this);
        }
        else if (UsesJoystick)
            Debug.LogError("Joystick movement requires a PlayerJoystick reference.", this);
        if (boundShipSwitchButton != null)
            boundShipSwitchButton.onClick.RemoveListener(SwitchShipFromButton);
        boundShipSwitchButton = shipSwitchButton;
        if (boundShipSwitchButton != null)
        {
            boundShipSwitchButton.onClick.AddListener(SwitchShipFromButton);
            boundShipSwitchButton.gameObject.SetActive(UsesShipSwitchButton);
        }
        else if (UsesShipSwitchButton)
            Debug.LogError("Joystick Focus requires a Ship Switch Button reference when release switching is disabled.", this);
        if (playerRB != null)
        {
            playerRB.linearVelocity = Vector2.zero;
            playerRB.angularVelocity = 0f;
            if (UsesDirectMovement)
            {
                // Keep colliders/trigger contacts active, but physics cannot
                // add gravity, inertia or collision impulses to drag movement.
                playerRB.bodyType = RigidbodyType2D.Kinematic;
                playerRB.interpolation = RigidbodyInterpolation2D.None;
                playerRB.useFullKinematicContacts = true;
            }
            else
            {
                RestoreMovementPhysics();
            }
        }

        appliedMovementMode = movementMode;
        appliedSwitchShipOnFocusRelease = switchShipOnFocusRelease;
        movementModeInitialized = true;
    }

    private void RestoreMovementPhysics()
    {
        if (playerRB == null)
            return;
        playerRB.bodyType = originalBodyType;
        playerRB.interpolation = originalInterpolation;
        playerRB.useFullKinematicContacts = originalFullKinematicContacts;
    }

    private void OnDisable()
    {
        ResetShipSwitchTouch();
        ResetMovementTouch();
        CurrentVelocity = Vector2.zero;
        knockbackVelocity = Vector2.zero;
        if (boundJoystick != null)
        {
            boundJoystick.CancelInput();
        }
        boundJoystick = null;
        if (boundShipSwitchButton != null)
            boundShipSwitchButton.onClick.RemoveListener(SwitchShipFromButton);
        boundShipSwitchButton = null;
        if (movementModeInitialized && appliedMovementMode != PlayerMovementMode.PhysicsTouch)
            RestoreMovementPhysics();
        movementModeInitialized = false;
    }

    private void OnApplicationFocus(bool focused)
    {
        if (focused)
            return;
        ResetShipSwitchTouch();
        ResetMovementTouch();
        joystick?.CancelInput();
        knockbackVelocity = Vector2.zero;
        CurrentVelocity = Vector2.zero;
    }

    public void OnBeforeSerialize() { }

    public void OnAfterDeserialize()
    {
        if (movementSerializationVersion >= 1)
            return;
        movementMode = useDirectTouchMovement
            ? PlayerMovementMode.DirectTouch
            : PlayerMovementMode.PhysicsTouch;
        movementSerializationVersion = 1;
    }

    public void SetTouchOffsetY(float value)
    {
        touchOffsetY = Mathf.Clamp(value, -1f, 1f);
    }

    private void ShipController()
    {
        if (UsesShipSwitchButton || Time.timeScale <= 0f || ShipSwitchLocked)
        {
            ResetShipSwitchTouch();
            return;
        }
        float shortSide = Mathf.Min(Screen.width, Screen.height);
        for (int i = 0; i < Input.touchCount; i++)
        {
            Touch touch = Input.GetTouch(i);
            ProcessShipSwitchTouch(touch.position, touch.phase, touch.fingerId,
                Time.unscaledTime, shortSide, touch.phase != TouchPhase.Began
                    || CanStartShipSwitchTouch(touch.position, touch.fingerId));
        }
        // Mouse presses allow the same gesture to be checked in the Editor.
        if (Input.touchCount == 0 && (Input.GetMouseButtonDown(0) || Input.GetMouseButtonUp(0)))
        {
            TouchPhase phase = Input.GetMouseButtonDown(0) ? TouchPhase.Began : TouchPhase.Ended;
            ProcessShipSwitchTouch(Input.mousePosition, phase, int.MinValue,
                Time.unscaledTime, shortSide, phase != TouchPhase.Began
                    || CanStartShipSwitchTouch(Input.mousePosition, -1));
        }
    }

    private bool ProcessShipSwitchTouch(Vector2 position, TouchPhase phase, int fingerId,
        float timestamp, float shortSide, bool canBegin)
    {
        if (ControlsLocked || ShipSwitchLocked || Time.timeScale <= 0f || UsesShipSwitchButton)
        {
            ResetShipSwitchTouch();
            return false;
        }
        if (phase == TouchPhase.Began)
        {
            if (!canBegin || activeTouchId != -1) return false;
            float distance = ShipSwitchTouchDistancePercent * 0.01f * Mathf.Max(1f, shortSide);
            float interval = timestamp - previousShipTouchTime;
            bool recognized = hasPreviousShipTouch && interval >= 0f && interval <= ShipSwitchPressWindow
                && (position - previousShipTouchPosition).sqrMagnitude <= distance * distance;
            hasPreviousShipTouch = false;
            activeTouchId = fingerId;
            if (recognized) shipSelect?.SwitchShip();
            return recognized;
        }
        if (fingerId != activeTouchId) return false;
        if (phase == TouchPhase.Canceled)
        {
            ResetShipSwitchTouch();
        }
        else if (phase == TouchPhase.Ended)
        {
            previousShipTouchPosition = position;
            previousShipTouchTime = timestamp;
            hasPreviousShipTouch = true;
            activeTouchId = -1;
        }
        return false;
    }

    private void ResetShipSwitchTouch()
    {
        activeTouchId = -1;
        hasPreviousShipTouch = false;
    }

    private bool CanStartShipSwitchTouch(Vector2 position, int fingerId)
    {
        if (position.x < 0f || position.x >= Screen.width || position.y < 0f || position.y >= Screen.height)
            return false;
        RaycastMovementUI(position, fingerId);
        for (int i = 0; i < movementUIHits.Count; i++)
        {
            RaycastResult hit = movementUIHits[i];
            if (!(hit.module is GraphicRaycaster)) continue;
            AbilityController ability = hit.gameObject.GetComponentInParent<AbilityController>();
            if (ability != null && ability.isActiveAndEnabled) return false;
            AbilityButtonView view = hit.gameObject.GetComponentInParent<AbilityButtonView>();
            if (view != null && view.isActiveAndEnabled) return false;
            return boundShipSwitchButton == null
                || !hit.gameObject.transform.IsChildOf(boundShipSwitchButton.transform);
        }
        return true;
    }

    private bool IsInsideMovementArea(Vector2 screenPosition)
    {
        if (movementMode != PlayerMovementMode.DirectTouch)
            return IsInsideGameplayViewport(screenPosition);

        return screenPosition.x >= 0f && screenPosition.x < Screen.width
            && screenPosition.y >= 0f && screenPosition.y < Screen.height;
    }

    private bool CanStartMovementTouch(Vector2 screenPosition, int fingerId)
    {
        if (!IsInsideMovementArea(screenPosition))
            return false;

        EventSystem eventSystem = EventSystem.current;
        if (eventSystem == null)
            return true;
        if (movementMode != PlayerMovementMode.DirectTouch)
            return !eventSystem.IsPointerOverGameObject(fingerId);

        // Raycast the press position directly: EventSystem may not have processed
        // this frame's touch yet. Decorative HUD graphics allow movement.
        RaycastMovementUI(screenPosition, fingerId);
        for (int i = 0; i < movementUIHits.Count; i++)
        {
            RaycastResult hit = movementUIHits[i];
            if (!(hit.module is GraphicRaycaster)) continue;
            return ExecuteEvents.GetEventHandler<IPointerClickHandler>(hit.gameObject) == null;
        }
        return true;
    }

    private void RaycastMovementUI(Vector2 screenPosition, int fingerId)
    {
        movementUIHits.Clear();
        EventSystem eventSystem = EventSystem.current;
        if (eventSystem == null) return;
        if (movementPointer == null || movementEventSystem != eventSystem)
        {
            movementEventSystem = eventSystem;
            movementPointer = new PointerEventData(eventSystem);
        }
        movementPointer.Reset();
        movementPointer.position = screenPosition;
        movementPointer.pointerId = fingerId;
        movementUIHits.Clear();
        eventSystem.RaycastAll(movementPointer, movementUIHits);
    }

    private bool IsInsideGameplayViewport(Vector2 screenPosition)
    {
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
        if (shipSelect == null || !shipSelect.HasTeamShips
            || teamBattleLevel >= ParentShip.MaxWeaponLevel) return 0;
        int upgradedShips = SetTeamBattleLevel(teamBattleLevel + 1);
        currentShip?.PlayBattleLevelUpSound();
        return upgradedShips;
    }

    public int SetTeamBattleLevel(int level)
    {
        int nextLevel = Mathf.Clamp(level, ParentShip.MinWeaponLevel, ParentShip.MaxWeaponLevel);
        if (nextLevel == teamBattleLevel) return 0;
        teamBattleLevel = nextLevel;
        int synchronizedShips = shipSelect != null ? shipSelect.SynchronizeTeamBattleLevel() : 0;
        OnTeamBattleLevelChanged?.Invoke(teamBattleLevel);
        return synchronizedShips;
    }

    public bool HandleShipDeath(ParentShip ship)
    {
        return shipSelect != null && shipSelect.HandleShipDeath(ship);
    }

    public bool IsTeamShip(ParentShip ship)
    {
        return shipSelect != null && shipSelect.ContainsShip(ship);
    }

    public int ReviveDefeatedShips()
    {
        int revived = shipSelect != null ? shipSelect.ReviveDefeatedShips() : 0;
        if (revived > 0)
            OnTeamShipsRevived?.Invoke();
        return revived;
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

    private void OnValidate()
    {
        shipSwitchPressWindow = ShipSwitchPressWindow;
        shipSwitchTouchDistancePercent = ShipSwitchTouchDistancePercent;
        touchOffsetY = Mathf.Clamp(touchOffsetY, -1f, 1f);
        directTouchSensitivity = Mathf.Max(0.01f, directTouchSensitivity);
        joystickSpeedMultiplier = Mathf.Max(0.001f, joystickSpeedMultiplier);
        maxSpeed = Mathf.Max(0f, maxSpeed);
        maximumKnockbackSpeed = Mathf.Max(0f, maximumKnockbackSpeed);
        knockbackDeceleration = Mathf.Max(0.01f, knockbackDeceleration);
    }
}
