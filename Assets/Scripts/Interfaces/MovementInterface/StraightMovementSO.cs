using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Movement/Straight")]
public class StraightMovementSO : MovementStrategySO
{
    public override void Move(Projectile projectile)
    {
        projectile.transform.position += (projectile.direction * projectile.speed * Time.deltaTime);
    }
}
