using UnityEngine;
using Zenject;

/// <summary>
/// Base class for objects that can be collected by a player ship, including
/// resources and temporary buffs. The magnet moves these objects and invokes
/// <see cref="TryCollect"/> only after they reach its absorption radius.
/// </summary>
public abstract class CollectiblePickup : Buff
{
    [Header("Pickup message")]
    [SerializeField, TextArea(1, 3), Tooltip(
        "World-space text shown above this pickup after it is collected.")]
    private string pickupMessage;

    [InjectOptional] private BuffPickupMessageController pickupMessageController = null;

    private bool isCollected;
    private bool isMagneticallyAttracted;

    public bool IsCollected => isCollected;
    public bool IsMagneticallyAttracted => isMagneticallyAttracted;

    public void StartMagneticAttraction()
    {
        isMagneticallyAttracted = true;
    }

    public bool TryCollect(ParentShip collectorShip)
    {
        if (isCollected || collectorShip == null)
            return false;

        if (!TryApplyCollection(collectorShip))
            return false;

        isCollected = true;
        if (CountsAsBonus)
            PointsCollector.Bonuses += 1;

        pickupMessageController?.Show(pickupMessage, transform.position);
        RemoveFromWorld();
        return true;
    }

    protected abstract bool TryApplyCollection(ParentShip collectorShip);

    protected virtual bool CountsAsBonus => true;

    protected virtual void RemoveFromWorld()
    {
        Destroy(gameObject);
    }

    protected virtual void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision == null)
            return;

        ParentShip collectorShip =
            collision.GetComponentInParent<ParentShip>();
        if (!IsDamageHitbox(collision, collectorShip))
            return;

        TryCollect(collectorShip);
    }

    protected static bool IsDamageHitbox(
        Collider2D collision,
        ParentShip collectorShip)
    {
        return collision != null
            && collectorShip != null
            && collision == collectorShip.DamageHitboxCollider;
    }
}
