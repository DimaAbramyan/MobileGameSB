#if UNITY_EDITOR || INCLUDE_TUTORIAL
using System;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.UI;

namespace Game.Tutorial
{
    // Created lazily. Only the panel/buttons receive pointer events; highlights never block gameplay.
    public sealed class TutorialOverlay : ITutorialPresentation
    {
        private readonly TutorialConfiguration config;
        private GameObject root;
        private RectTransform safe, panel, highlight, pointer;
        private TMP_Text hintText, continueText, skipText, skipStepText;
        private Button continueButton, skipButton, skipStepButton;
        private TutorialStep step;
        private LocalizedString hint;
        private bool labelsBound;
        private Transform cachedTarget;
        private SpriteRenderer cachedRenderer;
        private Canvas cachedCanvas;
        public TutorialOverlay(TutorialConfiguration config) => this.config = config;

        public void Show(TutorialStep value, Action confirm, Action skip, Action skipStep)
        {
            EnsureCreated();
            root.SetActive(true); step = value;
            if (hint != value.hint)
            {
                if (hint != null) hint.StringChanged -= HintChanged;
                hint = value.hint; hintText.text = string.Empty; hint.StringChanged += HintChanged;
            }
            if (!labelsBound)
            {
                labelsBound = true;
                config.continueLabel.StringChanged += ContinueChanged;
                config.skipLabel.StringChanged += SkipChanged;
                config.skipStepLabel.StringChanged += SkipStepChanged;
            }
            continueButton.onClick.RemoveAllListeners(); continueButton.onClick.AddListener(() => confirm());
            skipButton.onClick.RemoveAllListeners(); skipButton.onClick.AddListener(() => skip());
            skipStepButton.onClick.RemoveAllListeners(); skipStepButton.onClick.AddListener(() => skipStep());
            continueButton.gameObject.SetActive(value.completion.Uses(TutorialConditionType.ManualConfirmation));
            skipStepButton.gameObject.SetActive(value.canSkip && !continueButton.gameObject.activeSelf);
            PlacePanel(value.placement == TutorialPlacement.Bottom ? 0f : 1f);
        }
        private void HintChanged(string value) { if (hintText != null) hintText.text = value; }
        private void ContinueChanged(string value) { if (continueText != null) continueText.text = value; }
        private void SkipChanged(string value) { if (skipText != null) skipText.text = value; }
        private void SkipStepChanged(string value) { if (skipStepText != null) skipStepText.text = value; }
        public void Hide() { if (root != null) root.SetActive(false); }
        public void Tick(Transform target, Camera camera)
        {
            if (root == null || !root.activeSelf) return;
            Rect area = Screen.safeArea;
            if (Screen.width <= 0 || Screen.height <= 0) return;
            safe.anchorMin = new Vector2(area.xMin / Screen.width, area.yMin / Screen.height);
            safe.anchorMax = new Vector2(area.xMax / Screen.width, area.yMax / Screen.height);
            bool hasTarget = target != null && (target is RectTransform || camera != null);
            if (target != cachedTarget)
            {
                cachedTarget = target;
                cachedRenderer = target != null && target is not RectTransform ? target.GetComponentInChildren<SpriteRenderer>() : null;
                cachedCanvas = target is RectTransform ? target.GetComponentInParent<Canvas>() : null;
            }
            highlight.gameObject.SetActive(hasTarget && step.highlightTarget);
            pointer.gameObject.SetActive(hasTarget && step.showPointer);
            if (!hasTarget) return;
            Vector2 center, size;
            if (target is RectTransform ui)
            {
                ui.GetWorldCorners(corners);
                var canvas = cachedCanvas;
                Camera uiCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
                Vector2 min = LocalPoint(RectTransformUtility.WorldToScreenPoint(uiCamera, corners[0]));
                Vector2 max = LocalPoint(RectTransformUtility.WorldToScreenPoint(uiCamera, corners[2]));
                center = (min + max) * .5f; size = new Vector2(Mathf.Abs(max.x - min.x), Mathf.Abs(max.y - min.y));
            }
            else
            {
                Vector3 screen = camera.WorldToScreenPoint(target.position);
                if (screen.z <= 0f) { highlight.gameObject.SetActive(false); pointer.gameObject.SetActive(false); return; }
                center = LocalPoint(screen);
                // Size is presentation only; it does not change the actual hitbox.
                var renderer = cachedRenderer;
                Vector3 extent = renderer != null ? renderer.bounds.extents : Vector3.one * .25f;
                size = (LocalPoint(camera.WorldToScreenPoint(target.position + extent)) - center) * 2f;
                size = new Vector2(Mathf.Abs(size.x), Mathf.Abs(size.y));
            }
            highlight.anchoredPosition = center; highlight.sizeDelta = size + Vector2.one * 24f;
            pointer.anchoredPosition = center + step.pointerOffset;
            if (step.placement == TutorialPlacement.NearTarget)
            {
                panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(.5f, .5f);
                panel.sizeDelta = new Vector2(Mathf.Max(100f, safe.rect.width - 40f), 270f);
                panel.sizeDelta = new Vector2(Mathf.Max(100f, safe.rect.width - 40f), 270f);
                float halfHeight = safe.rect.height * .5f;
                panel.anchoredPosition = new Vector2(0f, Mathf.Clamp(center.y + 240f, -halfHeight + 150f, halfHeight - 150f));
            }
        }
        private readonly Vector3[] corners = new Vector3[4];
        private Vector2 LocalPoint(Vector2 screen)
        { RectTransformUtility.ScreenPointToLocalPointInRectangle(safe, screen, null, out var local); return local; }
        private void PlacePanel(float y)
        {
            panel.anchorMin = new Vector2(0f, y); panel.anchorMax = new Vector2(1f, y); panel.pivot = new Vector2(.5f, y);
            panel.sizeDelta = new Vector2(-40f, 270f); panel.anchoredPosition = new Vector2(0f, y > .5f ? -24f : 24f);
        }
        private void EnsureCreated()
        {
            if (root != null) return;
            root = new GameObject("Tutorial Overlay", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            UnityEngine.Object.DontDestroyOnLoad(root);
            var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 30000;
            var scaler = root.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f); scaler.matchWidthOrHeight = .5f;
            safe = Rect("Safe Area", root.transform); Stretch(safe);
            highlight = Rect("Target Highlight", safe);
            highlight.sizeDelta = new Vector2(100f, 100f);
            Border(highlight, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 4));
            Border(highlight, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 4));
            Border(highlight, new Vector2(0, 0), new Vector2(0, 1), new Vector2(4, 0));
            Border(highlight, new Vector2(1, 0), new Vector2(1, 1), new Vector2(4, 0));
            pointer = Rect("Target Pointer", safe); pointer.sizeDelta = new Vector2(22f, 22f);
            pointer.localRotation = Quaternion.Euler(0, 0, 45);
            var pointerImage = pointer.gameObject.AddComponent<Image>(); pointerImage.color = Color.cyan; pointerImage.raycastTarget = false;
            panel = Rect("Hint Panel", safe);
            panel.gameObject.AddComponent<Image>().color = new Color(.035f, .055f, .10f, .94f);
            hintText = Text("Hint", panel, 34);
            hintText.rectTransform.anchorMin = new Vector2(0f, .34f); hintText.rectTransform.anchorMax = Vector2.one;
            hintText.rectTransform.offsetMin = new Vector2(24f, 8f); hintText.rectTransform.offsetMax = new Vector2(-24f, -18f);
            hintText.enableAutoSizing = true; hintText.fontSizeMin = 23; hintText.fontSizeMax = 34;
            continueButton = Button("Continue", panel, .68f, .98f, out continueText);
            skipButton = Button("Skip Tutorial", panel, .02f, .47f, out skipText);
            skipStepButton = Button("Skip Step", panel, .51f, .98f, out skipStepText);
            highlight.gameObject.SetActive(false); pointer.gameObject.SetActive(false);
        }
        private static RectTransform Rect(string name, Transform parent)
        { var value = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>(); value.SetParent(parent, false); return value; }
        private static void Stretch(RectTransform rect)
        { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero; }
        private static void Border(RectTransform parent, Vector2 min, Vector2 max, Vector2 size)
        {
            var rect = Rect("Border", parent); rect.anchorMin = min; rect.anchorMax = max; rect.sizeDelta = size;
            var image = rect.gameObject.AddComponent<Image>(); image.color = Color.cyan; image.raycastTarget = false;
        }
        private TMP_Text Text(string name, Transform parent, float fontSize)
        {
            var text = Rect(name, parent).gameObject.AddComponent<TextMeshProUGUI>();
            if (config.font != null) text.font = config.font;
            text.fontSize = fontSize; text.alignment = TextAlignmentOptions.Center; text.raycastTarget = false; text.color = Color.white;
            return text;
        }
        private Button Button(string name, Transform parent, float left, float right, out TMP_Text label)
        {
            var rect = Rect(name, parent); rect.anchorMin = new Vector2(left, .06f); rect.anchorMax = new Vector2(right, .29f);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            var image = rect.gameObject.AddComponent<Image>(); image.color = new Color(.13f, .22f, .35f, 1f);
            var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = image;
            label = Text("Label", rect, 27); Stretch(label.rectTransform); return button;
        }
        public void Dispose()
        {
            if (hint != null) hint.StringChanged -= HintChanged;
            if (labelsBound)
            {
                config.continueLabel.StringChanged -= ContinueChanged;
                config.skipLabel.StringChanged -= SkipChanged;
                config.skipStepLabel.StringChanged -= SkipStepChanged;
            }
            if (root != null) UnityEngine.Object.Destroy(root);
            root = null;
        }
    }
}
#endif
