using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class MovementStrategySO : ScriptableObject, IMovementStrategy
{
    public abstract void Move(Projectile t);
}
