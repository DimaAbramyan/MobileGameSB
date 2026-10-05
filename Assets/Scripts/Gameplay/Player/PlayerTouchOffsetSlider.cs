using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(Slider))]
public sealed class PlayerTouchOffsetSlider : MonoBehaviour
{
    private const string PreferenceKey = "Fighting.PlayerTouchOffsetY";

    [SerializeField] private Slider slider;
    [SerializeField] private PlayerController playerController;

    private void Reset()
    {
        slider = GetComponent<Slider>();
    }

    private void Awake()
    {
        slider ??= GetComponent<Slider>();
        if (slider == null)
        {
            Debug.LogError("Player touch offset requires a Slider.", this);
            return;
        }

        slider.minValue = -1f;
        slider.maxValue = 1f;
        slider.wholeNumbers = false;

        float offset = Mathf.Clamp(PlayerPrefs.GetFloat(PreferenceKey, 0f), -1f, 1f);
        slider.SetValueWithoutNotify(offset);
        ApplyOffset(offset);
        slider.onValueChanged.AddListener(ApplyOffset);
    }

    private void OnDestroy()
    {
        if (slider != null)
            slider.onValueChanged.RemoveListener(ApplyOffset);

        PlayerPrefs.Save();
    }

    private void OnApplicationPause(bool isPaused)
    {
        if (isPaused)
            PlayerPrefs.Save();
    }

    private void OnValidate()
    {
        slider ??= GetComponent<Slider>();
        if (slider == null)
            return;

        slider.minValue = -1f;
        slider.maxValue = 1f;
        slider.wholeNumbers = false;
    }

    private void ApplyOffset(float value)
    {
        float offset = Mathf.Clamp(value, -1f, 1f);
        playerController?.SetTouchOffsetY(offset);
        PlayerPrefs.SetFloat(PreferenceKey, offset);
    }
}
