using UnityEditor;
using UnityEngine;

// A target lives in XY, so its central grip must not depend on Unity's 3D
// PositionHandle plane visibility/orientation. All preview entry points share it.
internal static class WavePreviewTargetHandle
{
    internal static Vector3 Draw(Vector3 position)
    {
        float size = HandleUtility.GetHandleSize(position);
        float z = position.z;
        using (new Handles.DrawingScope(Handles.color, Handles.matrix))
        {
            Handles.color = Handles.xAxisColor;
            position = Handles.Slider(position, Vector3.right, size,
                Handles.ArrowHandleCap, 0f);
            Handles.color = Handles.yAxisColor;
            position = Handles.Slider(position, Vector3.up, size,
                Handles.ArrowHandleCap, 0f);
            Handles.color = Color.cyan;
            position = Handles.Slider2D(position, Vector3.forward,
                Vector3.right, Vector3.up, size * .12f,
                Handles.RectangleHandleCap, Vector2.zero);
        }
        position.z = z;
        return position;
    }
}
