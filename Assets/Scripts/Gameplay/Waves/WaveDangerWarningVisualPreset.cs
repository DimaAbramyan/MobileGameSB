using UnityEngine;

[CreateAssetMenu(
    fileName = "WaveDangerWarningVisualPreset",
    menuName = "Game/Waves/Wave Danger Warning Visual Preset")]
public sealed class WaveDangerWarningVisualPreset : ScriptableObject
{
    [SerializeField, Min(1)] private int flashCount = 3;
    [SerializeField, Min(0.01f)] private float visibleDuration = 0.16f;
    [SerializeField, Min(0f)] private float hiddenInterval = 0.1f;
    [SerializeField] private bool useAlphaTransition;
    [SerializeField, Min(0f)] private float alphaFadeDuration = 0.08f;
    [SerializeField] private AnimationCurve alphaFadeCurve =
        AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    public int FlashCount => Mathf.Max(1, flashCount);
    public float VisibleDuration => Mathf.Max(0.01f, visibleDuration);
    public float HiddenInterval => Mathf.Max(0f, hiddenInterval);
    public bool UseAlphaTransition => useAlphaTransition;
    public float AlphaFadeDuration => Mathf.Max(0f, alphaFadeDuration);
    public AnimationCurve AlphaFadeCurve => alphaFadeCurve;

    public void SetSettings(
        int sourceFlashCount,
        float sourceVisibleDuration,
        float sourceHiddenInterval,
        bool sourceUseAlphaTransition,
        float sourceAlphaFadeDuration,
        AnimationCurve sourceAlphaFadeCurve)
    {
        flashCount = Mathf.Max(1, sourceFlashCount);
        visibleDuration = Mathf.Max(0.01f, sourceVisibleDuration);
        hiddenInterval = Mathf.Max(0f, sourceHiddenInterval);
        useAlphaTransition = sourceUseAlphaTransition;
        alphaFadeDuration = Mathf.Max(0f, sourceAlphaFadeDuration);
        alphaFadeCurve = CopyCurve(sourceAlphaFadeCurve);
    }

    private void OnValidate()
    {
        flashCount = Mathf.Max(1, flashCount);
        visibleDuration = Mathf.Max(0.01f, visibleDuration);
        hiddenInterval = Mathf.Max(0f, hiddenInterval);
        alphaFadeDuration = Mathf.Max(0f, alphaFadeDuration);
        alphaFadeCurve ??= AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    }

    private static AnimationCurve CopyCurve(AnimationCurve source)
    {
        return source != null && source.length > 0
            ? new AnimationCurve(source.keys)
            : AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    }
}
