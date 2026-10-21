using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class VerifyDirectTouchUI
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    public static object Run()
    {
        if (EditorApplication.isPlaying || EditorApplication.isCompiling || EditorApplication.isUpdating)
            throw new Exception("Requires idle Edit Mode.");
        var scene = EditorSceneManager.NewPreviewScene();
        var previousEventSystem = EventSystem.current;
        RenderTexture target = null;
        int checks = 0;
        try
        {
            var inputRoot = new GameObject("UI input fixture", typeof(EventSystem), typeof(StandaloneInputModule));
            SceneManager.MoveGameObjectToScene(inputRoot, scene);
            var events = inputRoot.GetComponent<EventSystem>();
            typeof(EventSystem).GetMethod("OnEnable", Private).Invoke(events, null);
            EventSystem.current = events;
            var module = inputRoot.GetComponent<StandaloneInputModule>();
            typeof(StandaloneInputModule).GetMethod("OnEnable", Private).Invoke(module, null);
            var press = typeof(StandaloneInputModule).GetMethod("ProcessTouchPress", Private);
            if (press == null) throw new Exception("UI input method unavailable.");

            var canvasRoot = new GameObject("Bottom HUD fixture", typeof(Canvas), typeof(GraphicRaycaster));
            SceneManager.MoveGameObjectToScene(canvasRoot, scene);
            var canvas = canvasRoot.GetComponent<Canvas>();
            var uiCameraRoot = new GameObject("HUD camera fixture", typeof(Camera));
            SceneManager.MoveGameObjectToScene(uiCameraRoot, scene);
            var uiCamera = uiCameraRoot.GetComponent<Camera>();
            target = new RenderTexture(Screen.width, Screen.height, 24);
            uiCamera.targetTexture = target;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = uiCamera;
            canvas.planeDistance = 1f;
            canvas.sortingOrder = short.MaxValue;
            var background = new GameObject("Decorative HUD", typeof(RectTransform), typeof(Image));
            background.transform.SetParent(canvasRoot.transform, false);
            var rect = (RectTransform)background.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = new Vector2(1f, 0.2f);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            var buttonRoot = new GameObject("Ability", typeof(RectTransform), typeof(Image), typeof(Button));
            buttonRoot.transform.SetParent(background.transform, false);
            var buttonRect = (RectTransform)buttonRoot.transform;
            buttonRect.anchorMin = new Vector2(0.7f, 0f);
            buttonRect.anchorMax = Vector2.one;
            buttonRect.offsetMin = buttonRect.offsetMax = Vector2.zero;
            var button = buttonRoot.GetComponent<Button>();
            button.targetGraphic = buttonRoot.GetComponent<Image>();
            int activations = 0;
            button.onClick.AddListener(() => activations++);
            Canvas.ForceUpdateCanvases();
            GraphicRegistry.RegisterGraphicForCanvas(canvas, background.GetComponent<Image>());
            GraphicRegistry.RegisterGraphicForCanvas(canvas, buttonRoot.GetComponent<Image>());
            typeof(GraphicRaycaster).GetMethod("OnEnable", Private).Invoke(canvasRoot.GetComponent<GraphicRaycaster>(), null);
            uiCamera.Render();

            var player = new GameObject("Direct touch fixture", typeof(Rigidbody2D));
            SceneManager.MoveGameObjectToScene(player, scene);
            var controller = player.AddComponent<PlayerController>();
            var cameraRoot = new GameObject("Camera fixture", typeof(Camera));
            SceneManager.MoveGameObjectToScene(cameraRoot, scene);
            var camera = cameraRoot.GetComponent<Camera>();
            camera.orthographic = true;
            camera.transform.position = new Vector3(0, 0, -10);
            camera.rect = new Rect(0, 0.2f, 1, 0.8f);
            Set(controller, "gameplayCamera", camera);
            Set(controller, "playerRB", player.GetComponent<Rigidbody2D>());
            Vector2 hud = new Vector2(Screen.width * 0.2f, Screen.height * 0.1f);
            Vector2 ability = new Vector2(Screen.width * 0.85f, Screen.height * 0.1f);
            Check((bool)Call(controller, "CanStartMovementTouch", hud, 1), "Decorative bottom UI accepts touch", ref checks);
            var statusBar = background.AddComponent<Slider>();
            statusBar.targetGraphic = background.GetComponent<Image>();
            Check((bool)Call(controller, "CanStartMovementTouch", hud, 1), "HUD status Slider accepts movement touch", ref checks);
            Check(!(bool)Call(controller, "CanStartMovementTouch", ability, 1), "Ability press belongs to UI", ref checks);
            var abilityController = buttonRoot.AddComponent<AbilityController>();
            Check((bool)Call(controller, "CanStartShipSwitchTouch", hud, 10), "Bottom HUD participates in ship switch gesture", ref checks);
            Check(!(bool)Call(controller, "CanStartShipSwitchTouch", ability, 20), "Active ability button is excluded from ship switch gesture", ref checks);
            Call(controller, "ProcessShipSwitchTouch", hud, TouchPhase.Began, 10, 10f, 1000f, true);
            Call(controller, "ProcessShipSwitchTouch", hud, TouchPhase.Ended, 10, 10f, 1000f, true);
            Check(!(bool)Call(controller, "ProcessShipSwitchTouch", ability, TouchPhase.Began, 20, 10.05f, 1000f, false),
                "Ability cannot trigger ship switching", ref checks);
            Call(controller, "ProcessShipSwitchTouch", ability, TouchPhase.Ended, 20, 10.1f, 1000f, true);
            Check((Vector2)Get(controller, "previousShipTouchPosition") == hud, "Ability finger does not contribute HUD distance", ref checks);
            Check((bool)Call(controller, "ProcessShipSwitchTouch", hud, TouchPhase.Began, 10, 10.2f, 1000f, true),
                "Repeated HUD press switches ship", ref checks);
            Call(controller, "ResetShipSwitchTouch");
            abilityController.enabled = false;
            Call(controller, "ProcessDirectTouch", hud, TouchPhase.Began, camera, 0.1f);
            Call(controller, "ProcessDirectTouch", ability, TouchPhase.Moved, camera, 0.1f);
            Check(player.transform.position.x > 0 && controller.CurrentVelocity.x > 0,
                "Drag over ability continues movement below viewport", ref checks);
            Call(controller, "ProcessDirectTouch", ability, TouchPhase.Ended, camera, 0.1f);
            Check(!(bool)Get(controller, "hasDirectTouchAnchor"), "Release clears capture", ref checks);

            var pointer = new PointerEventData(events) { pointerId = 1, position = hud,
                pointerCurrentRaycast = new RaycastResult { gameObject = background } };
            press.Invoke(module, new object[] { pointer, true, false });
            pointer.position = ability;
            pointer.pointerCurrentRaycast = new RaycastResult { gameObject = buttonRoot };
            ExecuteEvents.Execute(buttonRoot, pointer, ExecuteEvents.pointerEnterHandler);
            press.Invoke(module, new object[] { pointer, false, true });
            Check(activations == 0, "Entering/releasing on ability never activates it", ref checks);
            pointer = new PointerEventData(events) { pointerId = 2, position = ability,
                pointerCurrentRaycast = new RaycastResult { gameObject = buttonRoot } };
            press.Invoke(module, new object[] { pointer, true, false });
            press.Invoke(module, new object[] { pointer, false, true });
            Check(activations == 1, "Press and release on ability activates once", ref checks);
            Set(controller, "movementMode", PlayerMovementMode.PhysicsTouch);
            Check(!(bool)Call(controller, "CanStartMovementTouch", hud, 1), "Physics mode keeps viewport boundary", ref checks);
            return new { passed = checks, isolatedPreviewScene = true };
        }
        catch (TargetInvocationException exception)
        {
            throw new Exception(exception.InnerException.ToString());
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(scene);
            if (previousEventSystem != null)
                EventSystem.current = previousEventSystem;
            if (target != null)
                UnityEngine.Object.DestroyImmediate(target);
        }
    }

    private static object Call(object target, string name, params object[] args)
        => target.GetType().GetMethod(name, Private).Invoke(target, args);
    private static object Get(object target, string name) => target.GetType().GetField(name, Private).GetValue(target);
    private static void Set(object target, string name, object value) => target.GetType().GetField(name, Private).SetValue(target, value);
    private static void Check(bool condition, string message, ref int checks)
    { if (!condition) throw new Exception(message); checks++; }
}
