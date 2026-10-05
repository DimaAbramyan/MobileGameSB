using UnityEngine;
using Zenject;

public sealed class MetalPickup : CollectiblePickup
{
    [SerializeField, Min(1)] private int metalAmount = 1;

    [Inject] private PlayerResourceWallet resourceWallet;

    public int MetalAmount => Mathf.Max(1, metalAmount);

    public void Configure(int amount)
    {
        metalAmount = Mathf.Max(1, amount);
    }

    protected override bool TryApplyCollection(ParentShip collectorShip)
    {
        if (resourceWallet == null)
        {
            Debug.LogError(
                $"{nameof(MetalPickup)} requires {nameof(PlayerResourceWallet)}.",
                this);
            return false;
        }

        resourceWallet.Add(ResourceKind.Metal, MetalAmount);
        return true;
    }

    protected override bool CountsAsBonus => false;

    private void OnValidate()
    {
        metalAmount = Mathf.Max(1, metalAmount);
    }
}
