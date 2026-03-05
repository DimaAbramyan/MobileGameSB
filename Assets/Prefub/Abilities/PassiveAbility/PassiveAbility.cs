using UnityEngine;

public abstract class PassiveAbility : MonoBehaviour
{
    [SerializeField]
    protected ParentShip owner;

    public virtual void Init(ParentShip ship) { }
}