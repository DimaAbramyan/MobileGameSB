using UnityEngine;
using Zenject;

/// <summary>Lives on the existing ProjectContext, including across scene changes.</summary>
public sealed class SaveLifecycleHost : MonoBehaviour
{
    private SaveManager saves;
    private float dirtySeconds;
    private const float SaveInterval = 1f;

    [Inject]
    public void Initialize(SaveManager saveManager)
    {
        saves = saveManager;
        saves.InitializeProfile();
    }

    private void Update()
    {
        if (saves == null || !saves.IsDirty)
        {
            dirtySeconds = 0f;
            return;
        }
        dirtySeconds += Time.unscaledDeltaTime;
        if (dirtySeconds < SaveInterval)
            return;
        dirtySeconds = 0f;
        saves.Flush();
    }

    private void OnApplicationPause(bool paused) { if (paused) saves?.Flush(); }
    private void OnApplicationFocus(bool focused) { if (!focused) saves?.Flush(); }
    private void OnApplicationQuit() => saves?.Flush();
    private void OnDestroy() => saves?.Flush();
}
