using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class StatusEffectView : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private Image effectIcon;
    [SerializeField] private Slider durationSlider;

    public PlayerEffect Effect { get; private set; }

    public void Set(PlayerEffect effect, Sprite icon)
    {
        Effect = effect;

        if (effectIcon != null && icon != null)
            effectIcon.sprite = icon;

        Refresh();
    }

    public void Refresh()
    {
        if (Effect == null || durationSlider == null)
            return;

        durationSlider.SetValueWithoutNotify(Effect.ProgressNormalized);
    }
}
