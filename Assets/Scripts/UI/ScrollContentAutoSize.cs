using UnityEngine;
using UnityEngine.UI;

[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
public sealed class ScrollContentAutoSize : MonoBehaviour
{
    [SerializeField] private RectTransform contentRect;

    private bool isLayoutDirty;

    private void Awake()
    {
        ResolveContentRect();
    }

    private void OnEnable()
    {
        MarkLayoutDirty();
    }

    private void OnValidate()
    {
        ResolveContentRect();
        MarkLayoutDirty();
    }

    private void OnTransformChildrenChanged()
    {
        MarkLayoutDirty();
    }

    private void OnRectTransformDimensionsChange()
    {
        MarkLayoutDirty();
    }

    private void LateUpdate()
    {
        if (!isLayoutDirty)
            return;

        isLayoutDirty = false;
        RebuildHeight();
    }

    private void ResolveContentRect()
    {
        if (contentRect == null)
            contentRect = transform as RectTransform;
    }

    private void MarkLayoutDirty()
    {
        isLayoutDirty = true;
    }

    private void RebuildHeight()
    {
        ResolveContentRect();
        if (contentRect == null)
            return;

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(contentRect);

        float preferredHeight = Mathf.Max(0f, LayoutUtility.GetPreferredHeight(contentRect));
        contentRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, preferredHeight);
    }
}