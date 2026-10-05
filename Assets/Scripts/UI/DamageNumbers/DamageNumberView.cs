using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(TextMeshPro))]
public sealed class DamageNumberView : MonoBehaviour
{
    [SerializeField] private TMP_Text amountText;

    private Vector3 lastPosition;
    private Color lastColor;
    private float lastScale;
    private bool hasPresentation;

    private void Awake()
    {
        EnsureText();
    }

    private void Reset()
    {
        EnsureText();
    }

    public void Show(Vector3 position, float damage, Color color, float scale)
    {
        EnsureText();
        gameObject.SetActive(true);
        SetAmount(damage);
        SetPresentation(position, color, scale);
    }

    public void SetAmount(float damage)
    {
        EnsureText();
        if (amountText != null)
            amountText.SetText("{0:0}", Mathf.Ceil(damage));
    }

    public void SetPresentation(Vector3 position, Color color, float scale)
    {
        float clampedScale = Mathf.Max(0f, scale);
        if (!hasPresentation || lastPosition != position)
            transform.position = position;

        if (!hasPresentation || !Mathf.Approximately(lastScale, clampedScale))
            transform.localScale = Vector3.one * clampedScale;

        if (amountText != null && (!hasPresentation || lastColor != color))
            amountText.color = color;

        lastPosition = position;
        lastColor = color;
        lastScale = clampedScale;
        hasPresentation = true;
    }

    public void Hide()
    {
        hasPresentation = false;
        gameObject.SetActive(false);
    }

    private void EnsureText()
    {
        if (amountText == null)
            TryGetComponent(out amountText);
    }
}
