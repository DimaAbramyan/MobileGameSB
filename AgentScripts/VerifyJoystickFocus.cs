using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public static class VerifyJoystickFocus
{
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private static int checks;

    public static object Run()
    {
        checks = 0;
        var scene = EditorSceneManager.NewPreviewScene();
        var focus = ScriptableObject.CreateInstance<PlayerJoystickFocusSettings>();
        var normal = ScriptableObject.CreateInstance<PlayerJoystickSettings>();
        float timeScale = Time.timeScale;
        try
        {
            bool focused = true;
            Near(focus.EvaluateInput(Vector2.right * 0.1f, ref focused).magnitude, 0f, "dead zone");
            Near(focus.EvaluateInput(Vector2.right * 0.2f, ref focused).magnitude, 0.4f, "fixed Focus speed");
            Near(focus.EvaluateInput(Vector2.right * 0.59f, ref focused).magnitude, 0.4f, "below exit stays Focus");
            Near(focus.EvaluateInput(Vector2.right * 0.6f, ref focused).magnitude, 1f, "exit at threshold");
            Check(!focused, "Normal state");
            foreach (float amount in new[] {0.59f, 0.5f, 0.41f, 0.59f, 0.45f})
                Near(focus.EvaluateInput(Vector2.right * amount, ref focused).magnitude, 1f, "hysteresis Normal " + amount);
            Near(focus.EvaluateInput(Vector2.right * 0.4f, ref focused).magnitude, 0.4f, "return at threshold");
            Check(focused, "Focus state");
            foreach (float amount in new[] {0.41f, 0.5f, 0.59f, 0.45f})
                Near(focus.EvaluateInput(Vector2.right * amount, ref focused).magnitude, 0.4f, "hysteresis Focus " + amount);
            Near(focus.EvaluateInput(Vector2.one * 3f, ref focused).magnitude, 1f, "diagonal and out-of-radius clamp");
            Near(focus.EvaluateInput(Vector2.zero, ref focused).magnitude, 0f, "center stops");
            Check(focused, "center returns Focus");

            Near(focus.EvaluateInput(Vector2.right * 0.1f, ref focused, JoystickSpeedMode.Analog).magnitude, 0f, "Analog dead zone");
            Near(focus.EvaluateInput(Vector2.right * 0.2f, ref focused, JoystickSpeedMode.Analog).magnitude, 0.4f / 9f, "Analog Focus scales displacement");
            Near(focus.EvaluateInput(Vector2.right * 0.59f, ref focused, JoystickSpeedMode.Analog).magnitude, 0.4f * 0.49f / 0.9f, "Analog below raw exit threshold");
            Check(focused, "Analog remains Focus below raw threshold");
            Near(focus.EvaluateInput(Vector2.right * 0.6f, ref focused, JoystickSpeedMode.Analog).magnitude, 0.5f / 0.9f, "Analog exits at raw threshold");
            Check(!focused, "Analog enters Normal despite mapped input below exit");
            Near(focus.EvaluateInput(Vector2.right * 0.5f, ref focused, JoystickSpeedMode.Analog).magnitude, 0.4f / 0.9f, "Analog Normal hysteresis");
            Check(!focused, "Analog hysteresis retains Normal");
            Near(focus.EvaluateInput(Vector2.right * 0.4f, ref focused, JoystickSpeedMode.Analog).magnitude, 0.4f / 3f, "Analog returns Focus at raw threshold");
            Check(focused, "Analog return state");
            Near(focus.EvaluateInput(Vector2.one * 3f, ref focused, JoystickSpeedMode.Analog).magnitude, 1f, "Analog full radius and diagonal clamp");
            Near(focus.EvaluateInput(Vector2.zero, ref focused, JoystickSpeedMode.Analog).magnitude, 0f, "Analog release no inertia");

            Set(focus, "normalToFocusThreshold", 0.9f);
            Set(focus, "focusToNormalThreshold", 0.2f);
            Invoke(focus, "OnValidate");
            Check(focus.FocusToNormalThreshold > focus.NormalToFocusThreshold, "crossed thresholds repaired");
            Set(focus, "normalToFocusThreshold", 1f);
            Set(focus, "focusToNormalThreshold", 1f);
            Invoke(focus, "OnValidate");
            Check(focus.FocusToNormalThreshold > focus.NormalToFocusThreshold && focus.FocusToNormalThreshold <= 1f, "upper boundary validation");
            Set(focus, "normalToFocusThreshold", 0.4f);
            Set(focus, "focusToNormalThreshold", 0.6f);
            Invoke(focus, "OnValidate");

            GameObject canvasObject = Create(scene, "Focus test UI", typeof(RectTransform), typeof(Canvas));
            canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            GameObject stickObject = Create(scene, "Focus test stick", typeof(RectTransform));
            stickObject.transform.SetParent(canvasObject.transform, false);
            var surface = (RectTransform)stickObject.transform;
            surface.sizeDelta = Vector2.one * 275f;
            GameObject handleObject = Create(scene, "Focus test handle", typeof(RectTransform));
            handleObject.transform.SetParent(surface, false);
            var joystick = stickObject.AddComponent<PlayerJoystick>();
            Set(joystick, "settings", normal);
            Set(joystick, "focusSettings", focus);
            Set(joystick, "handle", (RectTransform)handleObject.transform);
            Invoke(joystick, "Awake");
            Canvas.ForceUpdateCanvases();
            Vector2 center = RectTransformUtility.WorldToScreenPoint(null, surface.position);
            var pointer = new PointerEventData(EventSystem.current) {pointerId = 7, position = center + Vector2.right * normal.MovementRadius * 0.2f};
            Set(normal, "speedMode", JoystickSpeedMode.Constant);
            joystick.OnPointerDown(pointer);
            Near(joystick.NormalizedDisplacement.magnitude, 0.2f, "raw normalized displacement");
            Near(joystick.InputDirection.magnitude, 1f, "legacy Constant preserved");
            Near(joystick.FocusInputDirection.magnitude, 0.4f, "Focus Constant mapping");
            Set(normal, "speedMode", JoystickSpeedMode.Analog);
            Near(joystick.FocusInputDirection.magnitude, 0.4f / 9f, "Focus uses selected Analog mode immediately");
            Near(joystick.NormalizedDisplacement.magnitude, 0.2f, "Analog preserves raw displacement");
            Set(normal, "speedMode", JoystickSpeedMode.Constant);
            pointer.position = center + Vector2.right * normal.MovementRadius * 0.7f;
            joystick.OnDrag(pointer);
            Near(joystick.FocusInputDirection.magnitude, 1f, "drag switches Normal immediately");
            joystick.OnPointerUp(pointer);
            Check(joystick.IsFocus && !joystick.IsHeld, "release resets Focus");
            Near(joystick.FocusInputDirection.magnitude, 0f, "release zero movement");

            GameObject cameraObject = Create(scene, "Focus test camera", typeof(Camera));
            Camera camera = cameraObject.GetComponent<Camera>();
            camera.enabled = false;
            camera.orthographic = true;
            camera.orthographicSize = 5f;
            cameraObject.transform.position = new Vector3(0, 0, -10f);
            GameObject root = Create(scene, "Focus test player", typeof(Rigidbody2D));
            var body = root.GetComponent<Rigidbody2D>();
            body.simulated = false;
            var player = root.AddComponent<PlayerController>();
            var selection = root.AddComponent<ShipSelect>();
            Set(player, "playerRB", body);
            Set(player, "gameplayCamera", camera);
            Set(player, "shipSelect", selection);
            Set(selection, "playerController", player);
            GameObject buttonObject = Create(scene, "Focus test swap", typeof(RectTransform), typeof(Button));
            var button = buttonObject.GetComponent<Button>();
            Set(player, "joystick", joystick);
            Set(player, "shipSwitchButton", button);
            Set(player, "movementMode", PlayerMovementMode.JoystickFocus);
            Set(player, "overrideMaxSpeed", true);
            Set(player, "maxSpeed", 2.5f);
            Invoke(player, "SynchronizeMovementMode");
            Check(button.gameObject.activeSelf, "button mode shows button");
            Check(body.bodyType == RigidbodyType2D.Kinematic, "Focus has no physics inertia");
            Set(joystick, "normalizedDisplacement", Vector2.right * 0.2f);
            Invoke(player, "MoveByJoystick", 0.1f);
            Near(root.transform.position.x, 0.1f, "Focus movement 2.5 times 0.4");
            Set(joystick, "normalizedDisplacement", Vector2.right * 0.7f);
            Invoke(player, "MoveByJoystick", 0.1f);
            Near(root.transform.position.x, 0.35f, "Normal movement immediate");
            joystick.CancelInput();
            Invoke(player, "MoveByJoystick", 0.1f);
            Near(root.transform.position.x, 0.35f, "release no inertia");

            var first = Create(scene, "Focus test hull A").AddComponent<ParentShip>();
            var second = Create(scene, "Focus test hull B").AddComponent<ParentShip>();
            first.transform.SetParent(root.transform, false);
            second.transform.SetParent(root.transform, false);
            first.ShipData = second.ShipData = AssetDatabase.LoadAssetAtPath<ShipData>("Assets/Prefub/Data/Ship/Arkanoid.asset");
            player.ChangeShipData(first);
            Invoke(player, "OnJoystickReleased");
            Check(player.CurrentShip == first, "Focus default release does not swap");
            joystick.OnPointerDown(pointer);
            button.onClick.Invoke();
            Check(player.CurrentShip == second && joystick.IsHeld, "button swaps while joystick held");
            joystick.CancelInput();
            button.onClick.Invoke();
            Check(player.CurrentShip == first, "button swaps once");
            player.LockShipSwitching(1f);
            button.onClick.Invoke();
            Check(player.CurrentShip == first, "switch lock respected");
            Set(player, "shipSwitchLockedUntil", 0f);
            Set(player, "switchShipOnFocusRelease", true);
            Invoke(player, "SynchronizeMovementMode");
            Check(!button.gameObject.activeSelf, "release mode hides button");
            player.SwitchShipFromButton();
            Check(player.CurrentShip == first, "hidden button route disabled");
            Invoke(player, "OnJoystickReleased");
            Check(player.CurrentShip == second, "checkbox enables release swap");
            Set(player, "switchShipOnFocusRelease", false);
            Invoke(player, "SynchronizeMovementMode");
            button.onClick.Invoke();
            Check(player.CurrentShip == first, "mode toggle no duplicate button subscriptions");
            player.LockControls(1f);
            button.onClick.Invoke();
            Check(player.CurrentShip == first, "control lock respected");
            Set(player, "controlsLockedUntil", 0f);
            Time.timeScale = 0f;
            button.onClick.Invoke();
            Check(player.CurrentShip == first, "pause prevents switch");
            Time.timeScale = timeScale;
            Set(player, "movementMode", PlayerMovementMode.Joystick);
            Invoke(player, "SynchronizeMovementMode");
            Invoke(player, "OnJoystickReleased");
            Check(player.CurrentShip == second, "old Joystick release preserved");
            Set(player, "movementMode", PlayerMovementMode.DirectTouch);
            Invoke(player, "SynchronizeMovementMode");
            Check(!stickObject.activeSelf && !button.gameObject.activeSelf, "old Direct Touch hides Focus UI");
            Set(player, "movementMode", PlayerMovementMode.PhysicsTouch);
            Invoke(player, "SynchronizeMovementMode");
            Check(body.bodyType == RigidbodyType2D.Dynamic, "old physics restored");
            return new {passed=checks, isolatedPreviewScene=true};
        }
        finally
        {
            Time.timeScale = timeScale;
            EditorSceneManager.ClosePreviewScene(scene);
            UnityEngine.Object.DestroyImmediate(focus);
            UnityEngine.Object.DestroyImmediate(normal);
        }
    }

    private static GameObject Create(UnityEngine.SceneManagement.Scene scene, string name, params Type[] types)
    {
        var go = new GameObject(name, types);
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, scene);
        go.hideFlags = HideFlags.HideAndDontSave;
        return go;
    }
    private static void Set(object target, string field, object value) => target.GetType().GetField(field, Flags).SetValue(target,value);
    private static object Invoke(object target, string method, params object[] args) => target.GetType().GetMethod(method, Flags).Invoke(target,args);
    private static void Check(bool value, string message)
    {
        if (!value) throw new Exception(message);
        checks++;
    }
    private static void Near(float actual,float expected,string message) => Check(Mathf.Abs(actual-expected)<0.002f,message+": "+actual+" != "+expected);
}
