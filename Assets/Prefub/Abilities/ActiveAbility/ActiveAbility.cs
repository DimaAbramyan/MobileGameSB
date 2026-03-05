using UnityEngine;

public abstract class ActiveAbility : MonoBehaviour
{
    [SerializeField] protected float cooldown;
    protected float cooldownTimer;
    [SerializeField]
    protected ParentShip owner;

    protected virtual void Awake()
    {
        owner = GetComponent<ParentShip>();
    }
    public abstract bool Activate(ParentShip owner);
    public void TryActivate(ParentShip owner)
    {
        if (cooldownTimer > 0)
            return;

        if (Activate(owner))
            cooldownTimer = cooldown;
    }

    protected virtual void Update()
    {
        if (cooldownTimer > 0)
            cooldownTimer -= Time.deltaTime;
    }
}