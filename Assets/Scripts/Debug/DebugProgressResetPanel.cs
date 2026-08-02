using TMPro;
using UnityEngine;
using Zenject;

public sealed class DebugProgressResetPanel : MonoBehaviour
{
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private string progressResetMessage = "Прогресс уровней сброшен";
    [SerializeField] private string resourcesResetMessage = "Ресурсы сброшены";
    [SerializeField] private string allResetMessage = "Прогресс и ресурсы сброшены";

    [InjectOptional] private LevelProgressService progressService;
    [InjectOptional] private PlayerResourceWallet resourceWallet;

    private LevelProgressService Progress =>
        progressService ??= new LevelProgressService();

    private PlayerResourceWallet Resources =>
        resourceWallet ??= new PlayerResourceWallet();

    public void ResetLevelProgress()
    {
        Progress.ResetProgress();
        SetStatus(progressResetMessage);
    }

    public void ResetPlayerResources()
    {
        Resources.Reset();
        SetStatus(resourcesResetMessage);
    }

    public void ResetAll()
    {
        Progress.ResetProgress();
        Resources.Reset();
        SetStatus(allResetMessage);
    }

    private void SetStatus(string message)
    {
        if (statusText != null)
            statusText.text = message;

        Debug.Log(message, this);
    }
}
