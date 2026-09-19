using TMPro;
using UnityEngine;
using Zenject;

public sealed class PlayerResourcesController : MonoBehaviour
{
    [SerializeField] private TMP_Text metalAmountText;
    [SerializeField] private TMP_Text goldAmountText;
    [SerializeField] private TMP_Text chipAmountText;

    private PlayerResourceWallet wallet;
    private bool isSubscribed;

    [Inject]
    private void Construct(PlayerResourceWallet injectedWallet)
    {
        if (wallet == injectedWallet)
            return;

        Unsubscribe();
        wallet = injectedWallet;
        Subscribe();
    }

    private void OnEnable()
    {
        Subscribe();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void Subscribe()
    {
        if (!isActiveAndEnabled || wallet == null || isSubscribed)
            return;

        wallet.OnResourceChanged += Refresh;
        isSubscribed = true;
        RefreshAll();
    }

    private void Unsubscribe()
    {
        if (!isSubscribed || wallet == null)
            return;

        wallet.OnResourceChanged -= Refresh;
        isSubscribed = false;
    }

    private void RefreshAll()
    {
        Refresh(wallet.Get(ResourceKind.Metal));
        Refresh(wallet.Get(ResourceKind.Gold));
        Refresh(wallet.Get(ResourceKind.Chip));
    }

    private void Refresh(PlayerResource resource)
    {
        if (resource == null)
            return;

        if (resource.Kind == ResourceKind.Metal && metalAmountText != null)
            metalAmountText.text = resource.Amount.ToString();

        if (resource.Kind == ResourceKind.Gold && goldAmountText != null)
            goldAmountText.text = resource.Amount.ToString();

        if (resource.Kind == ResourceKind.Chip && chipAmountText != null)
            chipAmountText.text = resource.Amount.ToString();
    }
}
