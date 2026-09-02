using UnityEngine;

public sealed class CrossfireCompanionWeaponsPassiveAbility : PassiveAbility
{
    [Header("Synchronized firing")]
    [SerializeField, Min(1)] private int triggerEveryShots = 1;

    [Header("Follow")]
    [SerializeField, Min(0.01f)] private float followSpeed = 12f;
    [SerializeField] private bool useInitialWeaponOffsets = true;
    [SerializeField] private Vector2 leftFollowOffset = new(-0.7f, 0f);
    [SerializeField] private Vector2 rightFollowOffset = new(0.7f, 0f);
    [SerializeField] private bool copyOwnerRotation = true;

    private Weapon leftWeapon;
    private Weapon rightWeapon;
    private WeaponController weaponController;
    private Vector2 capturedLeftOffset;
    private Vector2 capturedRightOffset;
    private int leftShotCount;
    private int rightShotCount;
    private bool edgePatrolActive;
    private bool missingWeaponWarningLogged;

    public Weapon LeftWeapon => leftWeapon;
    public Weapon RightWeapon => rightWeapon;
    public bool IsReadyForCombat => isActive && leftWeapon != null && rightWeapon != null;

    public override void Init(ParentShip ship)
    {
        owner = ship;
        weaponController = owner != null
            ? owner.GetComponent<WeaponController>()
            : null;
    }

    public override void On()
    {
        base.On();

        if (owner == null)
            owner = GetComponent<ParentShip>();

        if (owner == null || !EnsureCompanionWeapons())
            return;

        leftWeapon.ShowWeapon();
        rightWeapon.ShowWeapon();
    }

    public override void Off()
    {
        base.Off();
        edgePatrolActive = false;
    }

    public bool TryEnterEdgePatrol(out Weapon left, out Weapon right)
    {
        left = leftWeapon;
        right = rightWeapon;

        if (!IsReadyForCombat)
            return false;

        edgePatrolActive = true;
        return true;
    }

    public void SetEdgePatrolPose(
        Vector3 leftPosition,
        Quaternion leftRotation,
        Vector3 rightPosition,
        Quaternion rightRotation)
    {
        if (leftWeapon != null)
            leftWeapon.transform.SetPositionAndRotation(leftPosition, leftRotation);

        if (rightWeapon != null)
            rightWeapon.transform.SetPositionAndRotation(rightPosition, rightRotation);
    }

    public void ExitEdgePatrol()
    {
        edgePatrolActive = false;
    }

    private void LateUpdate()
    {
        if (!IsReadyForCombat || edgePatrolActive)
            return;

        FollowOwner(leftWeapon, GetFollowOffset(capturedLeftOffset, leftFollowOffset));
        FollowOwner(rightWeapon, GetFollowOffset(capturedRightOffset, rightFollowOffset));
    }

    private bool EnsureCompanionWeapons()
    {
        if (leftWeapon != null && rightWeapon != null)
            return true;

        Weapon[] weapons = owner.GetComponentsInChildren<Weapon>(true);
        if (weapons.Length < 2)
        {
            LogMissingWeaponWarning();
            return false;
        }

        Weapon leftCandidate = null;
        Weapon rightCandidate = null;
        float leftX = float.PositiveInfinity;
        float rightX = float.NegativeInfinity;

        for (int i = 0; i < weapons.Length; i++)
        {
            Weapon candidate = weapons[i];
            if (candidate == null)
                continue;

            float localX = owner.transform.InverseTransformPoint(candidate.transform.position).x;
            if (localX < leftX)
            {
                leftX = localX;
                leftCandidate = candidate;
            }

            if (localX > rightX)
            {
                rightX = localX;
                rightCandidate = candidate;
            }
        }

        if (leftCandidate == null
            || rightCandidate == null
            || leftCandidate == rightCandidate
            || Mathf.Approximately(leftX, rightX))
        {
            LogMissingWeaponWarning();
            return false;
        }

        ConfigureCompanions(leftCandidate, rightCandidate);
        return leftWeapon != null && rightWeapon != null;
    }

    private void ConfigureCompanions(Weapon leftCandidate, Weapon rightCandidate)
    {
        UnsubscribeFromShots();

        leftWeapon = leftCandidate;
        rightWeapon = rightCandidate;
        capturedLeftOffset = owner.transform.InverseTransformPoint(leftWeapon.transform.position);
        capturedRightOffset = owner.transform.InverseTransformPoint(rightWeapon.transform.position);

        weaponController ??= owner.GetComponent<WeaponController>();
        DetachAndRegister(leftWeapon);
        DetachAndRegister(rightWeapon);

        leftWeapon.OnShot += HandleLeftWeaponShot;
        rightWeapon.OnShot += HandleRightWeaponShot;
    }

    private void DetachAndRegister(Weapon weapon)
    {
        if (weapon == null)
            return;

        weapon.transform.SetParent(null, true);
        weapon.SetOwner(owner);
        weaponController?.RegisterExternalWeapon(weapon);
    }

    private void HandleLeftWeaponShot(Weapon weapon)
    {
        if (!IsReadyForCombat || weapon != leftWeapon)
            return;

        leftShotCount++;
        if (leftShotCount < triggerEveryShots)
            return;

        leftShotCount = 0;
        rightWeapon.TryShootImmediately(GetReloadMultiplier());
    }

    private void HandleRightWeaponShot(Weapon weapon)
    {
        if (!IsReadyForCombat || weapon != rightWeapon)
            return;

        rightShotCount++;
        if (rightShotCount < triggerEveryShots)
            return;

        rightShotCount = 0;
        leftWeapon.TryShootImmediately(GetReloadMultiplier());
    }

    private float GetReloadMultiplier()
    {
        return weaponController != null ? weaponController.reloadMultiplier : 1f;
    }

    private Vector2 GetFollowOffset(Vector2 capturedOffset, Vector2 configuredOffset)
    {
        return useInitialWeaponOffsets ? capturedOffset : configuredOffset;
    }

    private void FollowOwner(Weapon weapon, Vector2 offset)
    {
        if (weapon == null)
            return;

        Vector3 targetPosition = owner.transform.TransformPoint(offset);
        weapon.transform.position = Vector3.MoveTowards(
            weapon.transform.position,
            targetPosition,
            followSpeed * Time.deltaTime);

        if (copyOwnerRotation)
            weapon.transform.rotation = owner.transform.rotation;
    }

    private void LogMissingWeaponWarning()
    {
        if (missingWeaponWarningLogged)
            return;

        missingWeaponWarningLogged = true;
        Debug.LogWarning(
            "Crossfire requires two constructed weapons placed on opposite sides of the hull.",
            this);
    }

    private void UnsubscribeFromShots()
    {
        if (leftWeapon != null)
            leftWeapon.OnShot -= HandleLeftWeaponShot;

        if (rightWeapon != null)
            rightWeapon.OnShot -= HandleRightWeaponShot;
    }

    private void OnDestroy()
    {
        UnsubscribeFromShots();

        if (weaponController != null)
        {
            weaponController.UnregisterExternalWeapon(leftWeapon);
            weaponController.UnregisterExternalWeapon(rightWeapon);
        }

        if (leftWeapon != null)
            Destroy(leftWeapon.gameObject);

        if (rightWeapon != null)
            Destroy(rightWeapon.gameObject);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        triggerEveryShots = Mathf.Max(1, triggerEveryShots);
        followSpeed = Mathf.Max(0.01f, followSpeed);
    }
#endif
}
