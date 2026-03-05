using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HideShipAbility : ActiveAbility
{
    private float currentCooldown;
    [SerializeField]
    BoxCollider colliderToHide;
    List<SpriteRenderer> shipRender;
    public override bool Activate(ParentShip owner)
    {
        colliderToHide.enabled = false;
        foreach (SpriteRenderer sprite in shipRender)
        {
            sprite.enabled = false;
        }
        return true;
    }
}

