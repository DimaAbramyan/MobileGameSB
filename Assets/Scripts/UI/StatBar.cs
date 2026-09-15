using System;
using UnityEngine;
using UnityEngine.UI;

public class StatBar : MonoBehaviour
{
    private Action<Action<float>> currentSubscribe;
    private Action<Action<float>> currentUnsubscribe;

    private float maxValue;
    private float currentValue;
    [SerializeField] private Slider slider;
    [SerializeField] private Gradient gradient;
    [SerializeField] private Image fill;

    public void Setup(
    ParentShip ship,
    Func<float> getMaximumCurrentValue,
    Action<Action<float>> subscribe,
    Action<Action<float>> unsubscribe,
    Func<float> getCurrentValue)
    {
        if (currentUnsubscribe != null)
            currentUnsubscribe(SetValue);

        maxValue = getMaximumCurrentValue();
        currentValue = getCurrentValue();
        currentSubscribe = subscribe;
        currentUnsubscribe = unsubscribe;
        currentSubscribe?.Invoke(SetValue);

        SetValue(getCurrentValue());
    }

    private void OnDisable()
    {
        if (currentUnsubscribe != null)
            currentUnsubscribe(SetValue);
    }

    public void SetValue(float value)
    {
        currentValue = value;

        if (maxValue <= 0 || slider == null)
            return;
        float normalized = Mathf.Clamp01(value / maxValue)*100;
        //Debug.Log("Максимальное здоровье: " + maxValue.ToString());
        slider.value = normalized;

        if (gradient != null && fill != null)
            fill.color = gradient.Evaluate(normalized);
    }

    public void Clear()
    {
        currentUnsubscribe?.Invoke(SetValue);
        currentSubscribe = null;
        currentUnsubscribe = null;
        maxValue = 1f;
        SetValue(0f);
    }

    public void UpdateMax(float newMax)
    {
        maxValue = newMax;
        SetValue(currentValue); // обновляем UI под новый максимум
    }
}
