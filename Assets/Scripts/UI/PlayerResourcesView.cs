using TMPro;
using UnityEngine;
using Zenject;

public sealed class PlayerResourcesView : MonoBehaviour
{
    [SerializeField] private TMP_Text metalText;
    [SerializeField] private TMP_Text coresText;
    [SerializeField] private string metalFormat = "{0}";
    [SerializeField] private string coresFormat = "{0}";

    [Inject] private PlayerResourceWallet wallet;

    private void OnEnable()
    {
        wallet.OnResourceChanged += Refresh;
        Refresh(wallet.Get(ResourceKind.Metal));
        Refresh(wallet.Get(ResourceKind.Chip));
    }

    private void OnDisable()
    {
        wallet.OnResourceChanged -= Refresh;
    }

    public void Refresh()
    {
        Refresh(wallet.Get(ResourceKind.Metal));
        Refresh(wallet.Get(ResourceKind.Chip));
    }

    private void Refresh(PlayerResource resource)
    {
        if (resource == null)
            return;

        if (resource.Kind == ResourceKind.Metal && metalText != null)
            metalText.text = string.Format(metalFormat, resource.Amount);

        if (resource.Kind == ResourceKind.Chip && coresText != null)
            coresText.text = string.Format(coresFormat, resource.Amount);
    }
}
