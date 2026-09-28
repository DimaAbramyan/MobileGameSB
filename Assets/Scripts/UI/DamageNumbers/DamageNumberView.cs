using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(TextMeshPro))]
public sealed class DamageNumberView : MonoBehaviour
{
    [SerializeField] private TMP_Text amountText;

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
        transform.position = position;
        transform.localScale = Vector3.one * Mathf.Max(0f, scale);

        if (amountText != null)
            amountText.color = color;
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    private void EnsureText()
    {
        if (amountText == null)
            TryGetComponent(out amountText);
    }
}
