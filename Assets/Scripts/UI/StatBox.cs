using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class StatBox : MonoBehaviour
{
    [SerializeField] private TMP_Text label;
    [SerializeField] private TMP_Text value;

    public void SetContent(string labelText, string valueText)
    {
        label.text = labelText ?? "";
        value.text = valueText ?? "";
    }
}
