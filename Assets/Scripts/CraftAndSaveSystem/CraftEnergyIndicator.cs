using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class CraftEnergyIndicator : MonoBehaviour
{
    [SerializeField] private ShipSwipe shipSelector;
    [SerializeField] private TMP_Text remainingEnergyText;
    [SerializeField] private Image fillImage;
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color overflowColor = Color.red;
    [SerializeField] private string label = "Energy";

    private Coroutine refreshCoroutine;

    private void Awake()
    {
        if (shipSelector == null)
        {
            shipSelector = FindFirstObjectByType<ShipSwipe>(
                FindObjectsInactive.Include);
        }
    }

    private void OnEnable()
    {
        Refresh();
    }

    public void Refresh()
    {
        Refresh(shipSelector != null ? shipSelector.SelectedBody : null);
    }

    public void Refresh(BodyData body)
    {
        ShipData shipData = body != null && body.VisualConfig != null
            ? body.VisualConfig.ShipData
            : null;

        if (shipData == null)
        {
            SetText(string.Empty);
            SetFill(0f, normalColor);
            return;
        }

        int maximumEnergy = Mathf.Max(0, shipData.maximumEnergy);
        int usedEnergy = GetUsedEnergy(body);
        int remainingEnergy = maximumEnergy - usedEnergy;
        bool overflow = remainingEnergy < 0;

        SetText($"{label}: {remainingEnergy}/{maximumEnergy}");
        SetFill(
            maximumEnergy > 0 ? (float)usedEnergy / maximumEnergy : 0f,
            overflow ? overflowColor : normalColor);
    }

    public void RefreshNextFrame()
    {
        if (!isActiveAndEnabled)
            return;

        if (refreshCoroutine != null)
            StopCoroutine(refreshCoroutine);

        refreshCoroutine = StartCoroutine(RefreshNextFrameRoutine());
    }

    private IEnumerator RefreshNextFrameRoutine()
    {
        yield return null;
        refreshCoroutine = null;
        Refresh();
    }

    private static int GetUsedEnergy(BodyData body)
    {
        if (body == null)
            return 0;

        WeaponDataSerializable[] weapons =
            body.GetComponentsInChildren<WeaponDataSerializable>(true);

        int usedEnergy = 0;
        for (int i = 0; i < weapons.Length; i++)
        {
            if (weapons[i] != null)
                usedEnergy += weapons[i].EnergyCost;
        }

        return usedEnergy;
    }

    private void SetText(string value)
    {
        if (remainingEnergyText != null)
        {
            remainingEnergyText.text = value;
            remainingEnergyText.color =
                value.StartsWith($"{label}: -")
                    ? overflowColor
                    : normalColor;
        }
    }

    private void SetFill(float value, Color color)
    {
        if (fillImage == null)
            return;

        fillImage.fillAmount = Mathf.Clamp01(value);
        fillImage.color = color;
    }
}
