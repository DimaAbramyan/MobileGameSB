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
    ShipSelect shipSelect;

    public bool ControlsLocked => Time.time < controlsLockedUntil;
    public bool ShipSwitchLocked => Time.time < shipSwitchLockedUntil;

    void Awake()
    {
        playerRB = GetComponent<Rigidbody2D>();
        shipSelect = GetComponent<ShipSelect>();
        
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

                return;
            }

            return;
        }

        for (int i = 0; i < Input.touchCount; i++)
        {
            Touch touch = Input.GetTouch(i);
            if (touch.phase != TouchPhase.Began || IsPointerOverUIObject(touch))
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
            if (IsPointerOverUIObject(touch))
                continue;

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

    /// <summary>
    /// Смена текущего корабля
    /// </summary>
    /// <param name="currShip"></param>
    public void ChangeShipData(ParentShip currShip)
    {
        currentShip = currShip;

        playerRB.mass = currShip.ShipData.mass;
        playerRB.linearDamping = currShip.ShipData.drag;
        speed = currShip.ShipData.speed;

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
}
