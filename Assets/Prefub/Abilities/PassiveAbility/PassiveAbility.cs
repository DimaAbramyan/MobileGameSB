using UnityEngine;

public abstract class PassiveAbility : MonoBehaviour
{
    [SerializeField]
    protected ParentShip owner;
    protected bool isActive; 

    public virtual void Init(ParentShip ship) { }
    public virtual void Off()
    {
        isActive = false;
    }
    public virtual void On()
    {
        isActive = true;
    }
}