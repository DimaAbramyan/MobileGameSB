using UnityEngine;

public struct ProjectileParams
{
    public float speed;
    public float damage;
    public float maxLength;
    public float maxAngle;
    public Vector3 direction;
    IMovementStrategy movementStrategy;
    IImpactBehavior impactBehavior;
    ParentShip Owner;
}