using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "ProjectileBehaviour/LaserBehaviour")]
public class LaserBehaviour : ProjectileBehaviourSO
{
    public float fadeSpeed = 1f;

    public override void Tick(Projectile projectile)
    {
        projectile.Fade(fadeSpeed);
    }
}
