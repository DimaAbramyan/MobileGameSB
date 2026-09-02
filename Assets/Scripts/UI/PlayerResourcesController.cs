using TMPro;
using UnityEngine;
using Zenject;

public sealed class PlayerResourcesController : MonoBehaviour
{
    [SerializeField] private TMP_Text metalAmountText;
    [SerializeField] private TMP_Text goldAmountText;

    [InjectOptional] private PlayerResourceWallet wallet;

    private PlayerResourceWallet Wallet =>
        wallet ??= new PlayerResourceWallet();

    private void OnEnable()
    {
        Wallet.OnResourcesChanged += Refresh;
        Refresh(Wallet.Metal, Wallet.Gold, Wallet.Cores);
    }

    private void OnDisable()
    {
        Wallet.OnResourcesChanged -= Refresh;
    }

    private void Refresh(int metal, int gold, int cores)
    {
        if (metalAmountText != null)
            metalAmountText.text = metal.ToString();

        if (goldAmountText != null)
            goldAmountText.text = gold.ToString();
    }
}
