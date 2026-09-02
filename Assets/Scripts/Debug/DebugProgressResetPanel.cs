using TMPro;
using UnityEngine;
using Zenject;

public sealed class DebugProgressResetPanel : MonoBehaviour
{
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private string progressResetMessage = "Прогресс уровней сброшен";
    [SerializeField] private string resourcesResetMessage = "Ресурсы сброшены";
    [SerializeField] private string contentResetMessage = "Прогресс вещей сброшен";
    [SerializeField] private string allResetMessage = "Прогресс, вещи и ресурсы сброшены";

    [InjectOptional] private LevelProgressService progressService;
    [InjectOptional] private PlayerResourceWallet resourceWallet;
    [InjectOptional] private ContentProgressService contentProgressService;

    private LevelProgressService Progress =>
        progressService ??= new LevelProgressService();

    private PlayerResourceWallet Resources =>
        resourceWallet ??= new PlayerResourceWallet();

    private ContentProgressService Content =>
        contentProgressService ??= new ContentProgressService(Progress, Resources);

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

    public void ResetContentProgress()
    {
        Content.ResetProgress();
        SetStatus(contentResetMessage);
    }

    public void ResetAll()
    {
        Progress.ResetProgress();
        Resources.Reset();
        Content.ResetProgress();
        SetStatus(allResetMessage);
    }

    private void SetStatus(string message)
    {
        if (statusText != null)
            statusText.text = message;

        Debug.Log(message, this);
    }
}