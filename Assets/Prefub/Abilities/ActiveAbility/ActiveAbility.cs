using UnityEngine;
using Zenject;

public abstract class ActiveAbility : MonoBehaviour
{
    [Inject] protected AudioDatabase audioDatabase;
    [Inject] protected SoundManager audioManager;
    [SerializeField] protected float cooldown;
    protected float cooldownTimer;
    [SerializeField]
    protected ParentShip owner;

    protected virtual void Awake()
    {
        owner = GetComponent<ParentShip>();
    }
    public abstract bool Activate(ParentShip owner);

    protected virtual bool StartsCooldownOnActivation => true;

    public void TryActivate(ParentShip owner)
    {
        if (cooldownTimer > 0)
            return;

        if (Activate(owner) && StartsCooldownOnActivation)
            StartCooldown();
    }

    public virtual void Release(ParentShip owner) { }

    protected void StartCooldown()
    {
        cooldownTimer = cooldown;
    }

    protected virtual void Update()
    {
        if (cooldownTimer > 0)
            cooldownTimer -= Time.deltaTime;
    }
}
