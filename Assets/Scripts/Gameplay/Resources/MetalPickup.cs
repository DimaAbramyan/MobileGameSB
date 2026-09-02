using UnityEngine;
using Zenject;

public sealed class MetalPickup : Buff
{
    [SerializeField, Min(1)] private int metalAmount = 1;

    [Inject] private PlayerResourceWallet resourceWallet;

    private bool isCollected;
    private bool isMagneticallyAttracted;

    public int MetalAmount => Mathf.Max(1, metalAmount);
    public bool IsMagneticallyAttracted => isMagneticallyAttracted;

    public void Configure(int amount)
    {
        metalAmount = Mathf.Max(1, amount);
    }

    public void StartMagneticAttraction()
    {
        isMagneticallyAttracted = true;
    }

    public bool TryCollect(ParentShip collectorShip)
    {
        if (isCollected
            || collectorShip == null
            || collectorShip.IsIntangible)
        {
            return false;
        }

        if (resourceWallet == null)
        {
            Debug.LogError(
                $"{nameof(MetalPickup)} requires {nameof(PlayerResourceWallet)}.",
                this);
            return false;
        }

        isCollected = true;
        resourceWallet.Add(MetalAmount, 0);
        Destroy(gameObject);
        return true;
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (isCollected)
            return;

        ParentShip colliderShip =
            collision.GetComponentInParent<ParentShip>();
        TryCollect(colliderShip);
    }

    private void OnValidate()
    {
        metalAmount = Mathf.Max(1, metalAmount);
    }
}
