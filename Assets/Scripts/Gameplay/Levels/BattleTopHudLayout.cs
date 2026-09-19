using UnityEngine;

[ExecuteAlways]
[DefaultExecutionOrder(-900)]
[DisallowMultipleComponent]
public sealed class BattleTopHudLayout : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private BattleCameraViewport battleCameraViewport;
    [SerializeField] private RectTransform playerShip1;
    [SerializeField] private RectTransform playerShip2;
    [SerializeField] private UnsafeTopArea unsafeTopArea;

    private readonly Vector3[] corners = new Vector3[4];

    private void OnEnable()
    {
        Apply();
    }

    private void LateUpdate()
    {
        Apply();
    }

    [ContextMenu("Apply Top HUD Layout")]
    public void Apply()
    {
        if (battleCameraViewport == null || playerShip1 == null || playerShip2 == null)
            return;

        JoinPlayerShipPanels();

        Rect gameplayRect = battleCameraViewport.GameplayScreenRect;
        if (gameplayRect.height > 0f)
        {
            MovePanelBottomToScreenY(playerShip1, gameplayRect.yMax);
            MovePanelBottomToScreenY(playerShip2, gameplayRect.yMax);
        }

        unsafeTopArea?.Apply();
    }

    private void JoinPlayerShipPanels()
    {
        RectTransform parent = playerShip1.parent as RectTransform;
        if (parent == null || playerShip2.parent != parent)
            return;

        float halfWidth = parent.rect.width * 0.5f;
        playerShip1.SetInsetAndSizeFromParentEdge(RectTransform.Edge.Left, 0f, halfWidth);
        playerShip2.SetInsetAndSizeFromParentEdge(RectTransform.Edge.Right, 0f, halfWidth);
    }

    private void MovePanelBottomToScreenY(RectTransform panel, float targetScreenY)
    {
        RectTransform parent = panel.parent as RectTransform;
        if (parent == null)
            return;

        panel.GetWorldCorners(corners);
        float currentScreenY = float.MaxValue;
        for (int i = 0; i < corners.Length; i++)
        {
            Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(null, corners[i]);
            currentScreenY = Mathf.Min(currentScreenY, screenPoint.y);
        }

        if (Mathf.Abs(targetScreenY - currentScreenY) < 0.01f)
            return;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                parent,
                new Vector2(0f, currentScreenY),
                null,
                out Vector2 currentLocal)
            || !RectTransformUtility.ScreenPointToLocalPointInRectangle(
                parent,
                new Vector2(0f, targetScreenY),
                null,
                out Vector2 targetLocal))
        {
            return;
        }

        panel.anchoredPosition += Vector2.up * (targetLocal.y - currentLocal.y);
    }
}
