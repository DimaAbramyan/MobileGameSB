using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// Screen.width/height, Canvas.renderingDisplaySize and Camera.pixelRect can describe
// the currently rendering Editor window. Read the selected play display directly instead.
internal static class WavePreviewDisplay
{
    private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private const BindingFlags Static = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    private static readonly Type PlayModeView = typeof(EditorWindow).Assembly.GetType("UnityEditor.PlayModeView");
    private static readonly MethodInfo MainView = PlayModeView?.GetMethod("GetMainPlayModeView", Static);
    private static readonly MethodInfo TargetSize = PlayModeView?.GetMethod("GetMainPlayModeViewTargetSize", Static);
    private static Type viewType, mainType, screenType;
    private static FieldInfo simulatorMain;
    private static PropertyInfo simulation, screenWidth, screenHeight, screenSafeArea, screenDpi;

    internal readonly struct Sample : IEquatable<Sample>
    {
        internal readonly int Width, Height;
        internal readonly Rect SafeArea;
        internal readonly float Dpi;
        internal Sample(int width, int height, Rect safeArea, float dpi)
        { Width = width; Height = height; SafeArea = safeArea; Dpi = dpi; }
        public bool Equals(Sample other) => Width == other.Width && Height == other.Height
            && SafeArea == other.SafeArea && Dpi == other.Dpi;
    }

    internal static Sample Read()
    {
        if (MainView == null || TargetSize == null)
            throw new InvalidOperationException("Unity PlayModeView display API is unavailable.");
        object view = MainView.Invoke(null, null);
        if (view == null) throw new InvalidOperationException("Open Game View or Device Simulator to preview camera bounds.");
        if (viewType != view.GetType())
        {
            viewType = view.GetType();
            simulatorMain = viewType.GetField("m_Main", Instance);
        }
        object main = simulatorMain?.GetValue(view);
        if (main != null)
        {
            if (mainType != main.GetType())
            { mainType = main.GetType(); simulation = mainType.GetProperty("ScreenSimulation", Instance); }
            object screen = simulation?.GetValue(main);
            if (screen != null)
            {
                if (screenType != screen.GetType())
                {
                    screenType = screen.GetType();
                    screenWidth = screenType.GetProperty("width", Instance);
                    screenHeight = screenType.GetProperty("height", Instance);
                    screenSafeArea = screenType.GetProperty("safeArea", Instance);
                    screenDpi = screenType.GetProperty("dpi", Instance);
                }
                return new Sample((int)screenWidth.GetValue(screen), (int)screenHeight.GetValue(screen),
                    (Rect)screenSafeArea.GetValue(screen), (float)screenDpi.GetValue(screen));
            }
        }
        var size = (Vector2)TargetSize.Invoke(null, null);
        return new Sample(Mathf.RoundToInt(size.x), Mathf.RoundToInt(size.y), new Rect(Vector2.zero, size), 0f);
    }

    internal static void ApplyScaler(CanvasScaler scaler, Sample display)
    {
        if (!scaler.isActiveAndEnabled || !scaler.TryGetComponent<Canvas>(out var canvas) || !canvas.isRootCanvas)
            return;
        // Same equations as uGUI CanvasScaler, with an explicit display rather than
        // Canvas.renderingDisplaySize (which has the same Editor render-context problem).
        float factor = scaler.scaleFactor;
        float pixelsPerUnit = scaler.referencePixelsPerUnit;
        if (scaler.uiScaleMode == CanvasScaler.ScaleMode.ScaleWithScreenSize)
        {
            var reference = scaler.referenceResolution;
            float x = display.Width / Mathf.Max(.0001f, reference.x);
            float y = display.Height / Mathf.Max(.0001f, reference.y);
            factor = scaler.screenMatchMode switch
            {
                CanvasScaler.ScreenMatchMode.Expand => Mathf.Min(x, y),
                CanvasScaler.ScreenMatchMode.Shrink => Mathf.Max(x, y),
                _ => Mathf.Pow(2f, Mathf.Lerp(Mathf.Log(x, 2f), Mathf.Log(y, 2f), scaler.matchWidthOrHeight))
            };
        }
        else if (scaler.uiScaleMode == CanvasScaler.ScaleMode.ConstantPhysicalSize)
        {
            float unit = scaler.physicalUnit switch
            {
                CanvasScaler.Unit.Centimeters => 2.54f,
                CanvasScaler.Unit.Millimeters => 25.4f,
                CanvasScaler.Unit.Points => 72f,
                CanvasScaler.Unit.Picas => 6f,
                _ => 1f
            };
            factor = (display.Dpi > 0f ? display.Dpi : scaler.fallbackScreenDPI) / unit;
            pixelsPerUnit *= unit / scaler.defaultSpriteDPI;
        }
        canvas.scaleFactor = Mathf.Max(.0001f, factor);
        canvas.referencePixelsPerUnit = pixelsPerUnit;
    }
}
