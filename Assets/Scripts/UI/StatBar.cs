using System;
using UnityEngine;
using UnityEngine.UI;

public class StatBar : MonoBehaviour
{
    private float maxValue;

    private Action<Action<float>> currentSubscribe;
    private Action<Action<float>> currentUnsubscribe;

    [SerializeField] private Slider slider;
    [SerializeField] private Gradient gradient;
    [SerializeField] private Image fill;

    public void Setup(
        ParentShip ship,
        float maxValue,
        Action<Action<float>> subscribe,
        Action<Action<float>> unsubscribe,
        float startValue)
    {
        if (currentUnsubscribe != null)
        {
            currentUnsubscribe(SetValue);
        }
        this.maxValue = maxValue;
        currentSubscribe = subscribe;
        currentUnsubscribe = unsubscribe;

        currentSubscribe?.Invoke(SetValue);

        SetValue(startValue);
    }

    private void OnDisable()
    {
        if (currentUnsubscribe != null)
            currentUnsubscribe(SetValue);
    }

    public void SetValue(float value)
    {
        if (maxValue <= 0)
            return;
        float normalized = Mathf.Clamp01(value / maxValue)*100;
        slider.value = normalized;

        if (gradient != null)
            fill.color = gradient.Evaluate(normalized);
    }
}