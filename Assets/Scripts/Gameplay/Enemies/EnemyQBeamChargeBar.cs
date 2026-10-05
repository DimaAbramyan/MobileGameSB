using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// World-space UI for the disintegration charge applied by Q-Beam.
/// The charge itself is owned by <see cref="EnemyDisintegrationSystem"/>;
/// this component only displays updates received from its Enemy.
/// </summary>
[DisallowMultipleComponent]
public sealed class EnemyQBeamChargeBar : MonoBehaviour
{
    [SerializeField] private Enemy enemy;
    [SerializeField] private Slider slider;
    [SerializeField] private CanvasGroup canvasGroup;

    public void Configure(
        Enemy targetEnemy,
        Slider targetSlider,
        CanvasGroup targetCanvasGroup)
    {
        enemy = targetEnemy;
        slider = targetSlider;
        canvasGroup = targetCanvasGroup;
        ConfigureSlider();
        SetNormalizedCharge(0f);
    }

    private void Awake()
    {
        ConfigureSlider();
        SetNormalizedCharge(0f);
    }

    private void OnEnable()
    {
        if (enemy == null || !enemy.ShowsQBeamCharge)
            return;

        enemy.OnQBeamChargeChanged += HandleChargeChanged;
        enemy.OnDied += HandleEnemyDied;
    }

    private void OnDisable()
    {
        if (enemy == null)
            return;

        enemy.OnQBeamChargeChanged -= HandleChargeChanged;
        enemy.OnDied -= HandleEnemyDied;
    }

    private void ConfigureSlider()
    {
        if (slider == null)
            return;

        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.direction = Slider.Direction.LeftToRight;
    }

    private void HandleChargeChanged(float charge, float maximumCharge)
    {
        float normalizedCharge = maximumCharge > 0f
            ? charge / maximumCharge
            : 0f;
        SetNormalizedCharge(normalizedCharge);
    }

    private void HandleEnemyDied(Enemy _)
    {
        SetNormalizedCharge(0f);
    }

    private void SetNormalizedCharge(float normalizedCharge)
    {
        float value = Mathf.Clamp01(normalizedCharge);
        if (slider != null)
            slider.SetValueWithoutNotify(value);

        if (canvasGroup != null)
            canvasGroup.alpha = value > 0f ? 1f : 0f;
    }
}
