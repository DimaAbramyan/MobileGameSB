using UnityEngine;

public class PassiveWeaponSpeed : PassiveAbility
{
    PlayerController controller;
    WeaponController weaponController;
    float speed;
    float maxSpeed;
    bool start = false;
    float normalizedSpeed;
    float multiplier;

    private float passiveSpeedDivisor = 40f;
    private float passiveSpeedMultiplier = 1.2f;
    private float maximumReloadMultiplier = 2f;
    private float minimumReloadMultiplier = 0.25f;

    public override void ApplyShipMetaStats(ShipMetaRuntimeStats stats)
    {
        if (!stats.TryGetContract(out BladeShipMetaContract contract))
            return;

        passiveSpeedDivisor = contract.PassiveSpeedDivisor;
        passiveSpeedMultiplier = contract.PassiveSpeedMultiplier;
        maximumReloadMultiplier = contract.MaximumReloadMultiplier;
        minimumReloadMultiplier = contract.MinimumReloadMultiplier;
    }

    private void Start()
    {
        controller = GetComponentInParent<PlayerController>();
        weaponController = GetComponent<WeaponController>();
        maxSpeed = GetComponent<ParentShip>().ShipData.MetaSpeed
            / Mathf.Max(0.01f, passiveSpeedDivisor);
        start = true;
    }
    private void FixedUpdate()
    {
        if (!start)
        {
            return;
        }
        speed = controller.CurrentVelocity.magnitude * passiveSpeedMultiplier;
        normalizedSpeed = speed / maxSpeed;
        multiplier = Mathf.Lerp(
            maximumReloadMultiplier,
            minimumReloadMultiplier,
            normalizedSpeed);
        Debug.Log("Multiplier: "+ multiplier);
        Debug.Log(speed);
        Debug.Log(maxSpeed);
        weaponController.SetReloadMultiplier(multiplier);
    }
}
