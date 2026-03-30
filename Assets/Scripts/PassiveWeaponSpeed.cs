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
    private void Start()
    {
        controller = GetComponentInParent<PlayerController>();
        weaponController = GetComponent<WeaponController>();
        maxSpeed = GetComponent<ParentShip>().ShipData.speed/40;
        start = true;
    }
    private void FixedUpdate()
    {
        if (!start)
        {
            return;
        }
        speed = controller.CurrentVelocity.magnitude;
        normalizedSpeed = speed / maxSpeed;
        multiplier =  Mathf.Lerp(1.5f, 0.3f, normalizedSpeed);
        Debug.Log("Multiplier: "+ multiplier);
        Debug.Log(speed);
        Debug.Log(maxSpeed);
        weaponController.SetReloadMultiplier(multiplier);
    }
}
