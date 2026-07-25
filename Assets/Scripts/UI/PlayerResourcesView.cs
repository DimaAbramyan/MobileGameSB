using TMPro;
using UnityEngine;
using Zenject;

public sealed class PlayerResourcesView : MonoBehaviour
{
    [SerializeField] private TMP_Text metalText;
    [SerializeField] private TMP_Text coresText;
    [SerializeField] private string metalFormat = "{0}";
    [SerializeField] private string coresFormat = "{0}";

    [InjectOptional] private PlayerResourceWallet wallet;

    private PlayerResourceWallet Wallet =>
        wallet ??= new PlayerResourceWallet();

    private void OnEnable()
    {
        Wallet.OnChanged += Refresh;
        Refresh(Wallet.Metal, Wallet.Cores);
    }

    private void OnDisable()
    {
        Wallet.OnChanged -= Refresh;
    }

    public void Refresh()
    {
        Refresh(Wallet.Metal, Wallet.Cores);
    }

    private void Refresh(int metal, int cores)
    {
        if (metalText != null)
            metalText.text = string.Format(metalFormat, metal);

        if (coresText != null)
            coresText.text = string.Format(coresFormat, cores);
    }
}
