using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public static class VerifyBattleControls
{
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private static int checks;

    public static object Run()
    {
        checks = 0;
        var preview = EditorSceneManager.NewPreviewScene();
        var settings = ScriptableObject.CreateInstance<PlayerJoystickSettings>();
        float oldTimeScale = Time.timeScale;
        try
        {
            Check(settings.EvaluateInput(Vector2.zero) == Vector2.zero, "center");
            Check(settings.EvaluateInput(Vector2.right * 5f) == Vector2.zero, "dead zone");
            Near(settings.EvaluateInput(Vector2.right * 40.5f).x, 0.5f, "analog half speed");
            Near(settings.EvaluateInput(Vector2.right * 150f).magnitude, 1f, "clamp full speed");
            Near(settings.EvaluateInput(Vector2.one * 75f).magnitude, 1f, "diagonal speed");
            Set(settings, "speedMode", JoystickSpeedMode.Constant);
            Near(settings.EvaluateInput(Vector2.right * 7f).x, 1f, "constant speed after dead zone");
            Check(settings.EvaluateInput(Vector2.right * 5f) == Vector2.zero, "constant dead zone");
            Set(settings, "speedMode", JoystickSpeedMode.Analog);

            GameObject uiRoot = Create("Joystick verification", preview, typeof(RectTransform), typeof(Canvas));
            uiRoot.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            GameObject stickObject = Create("Stick", preview, typeof(RectTransform), typeof(Image));
            stickObject.transform.SetParent(uiRoot.transform, false);
            var surface = (RectTransform)stickObject.transform;
            surface.sizeDelta = Vector2.one * 225f;
            GameObject knob = Create("Handle", preview, typeof(RectTransform));
            knob.transform.SetParent(surface, false);
            var joystick = stickObject.AddComponent<PlayerJoystick>();
            Set(joystick, "settings", settings);
            Set(joystick, "handle", (RectTransform)knob.transform);
            Invoke(joystick, "Awake");
            Canvas.ForceUpdateCanvases();

            int releases = 0;
            joystick.Released += () => releases++;
            var pointer = new PointerEventData(EventSystem.current) { pointerId = 7, position = RectTransformUtility.WorldToScreenPoint(null, surface.position) + Vector2.right * 75f };
            joystick.OnPointerDown(pointer);
            Check(joystick.IsHeld, "pointer captured");
            Near(joystick.InputDirection.x, 1f, "pointer input");
            joystick.OnPointerUp(new PointerEventData(EventSystem.current) { pointerId = 8 });
            Check(joystick.IsHeld && releases == 0, "second finger ignored");
            joystick.OnPointerUp(pointer);
            Check(!joystick.IsHeld && joystick.InputDirection == Vector2.zero, "release stops input");
            Check(releases == (Application.isFocused ? 1 : 0), "normal release event exactly once");
            joystick.OnPointerUp(pointer);
            Check(releases <= 1, "duplicate release ignored");
            int beforeCancel = releases;
            joystick.OnPointerDown(pointer);
            joystick.OnCancel(new BaseEventData(EventSystem.current));
            Check(!joystick.IsHeld && releases == beforeCancel, "cancel without ship switch");
            joystick.OnPointerDown(pointer);
            Time.timeScale = 0f;
            Invoke(joystick, "Update");
            joystick.OnPointerUp(pointer);
            Check(!joystick.IsHeld && releases == beforeCancel, "pause without ship switch");
            Time.timeScale = oldTimeScale;

            GameObject cameraObject = Create("Test Camera", preview, typeof(Camera));
            Camera camera = cameraObject.GetComponent<Camera>();
            camera.enabled = false;
            camera.orthographic = true;
            camera.orthographicSize = 5f;
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);
            GameObject playerObject = Create("Test player", preview, typeof(Rigidbody2D));
            Rigidbody2D body = playerObject.GetComponent<Rigidbody2D>();
            body.simulated = false;
            body.mass = 5f;
            PlayerController player = playerObject.AddComponent<PlayerController>();
            Set(player, "playerRB", body);
            Set(player, "gameplayCamera", camera);
            Set(player, "movementSerializationVersion", 0);
            Set(player, "useDirectTouchMovement", false);
            player.OnAfterDeserialize();
            Check(player.MovementMode == PlayerMovementMode.PhysicsTouch, "legacy false migration");
            Set(player, "movementSerializationVersion", 0);
            Set(player, "useDirectTouchMovement", true);
            player.OnAfterDeserialize();
            Check(player.MovementMode == PlayerMovementMode.DirectTouch, "legacy true migration");
            Invoke(player, "SynchronizeMovementMode");
            Check(body.bodyType == RigidbodyType2D.Kinematic && body.useFullKinematicContacts, "direct mode contact body");

            foreach (int frames in new[] {30, 60, 120})
            {
                playerObject.transform.position = Vector3.zero;
                Set(player, "knockbackVelocity", Vector2.zero);
                player.ApplyKnockback(Vector2.right * 20f);
                for (int i = 0; i < frames; i++)
                    Invoke(player, "ApplyDirectKnockback", 1f / frames);
                Near(playerObject.transform.position.x, 4f * 4f / 60f, "frame independent recoil " + frames);
                Check((Vector2)Get(player, "knockbackVelocity") == Vector2.zero, "recoil settles " + frames);
            }
            playerObject.transform.position = Vector3.zero;
            player.LockControls(1f);
            player.ApplyKnockback(Vector2.right * 20f);
            Invoke(player, "ApplyDirectKnockback", 0.1f);
            Check(player.ControlsLocked && playerObject.transform.position.x > 0f, "recoil while controls locked");
            Set(player, "controlsLockedUntil", 0f);
            Set(player, "movementMode", PlayerMovementMode.Joystick);
            Set(player, "joystick", joystick);
            Set(player, "speed", 300f);
            Invoke(player, "SynchronizeMovementMode");
            playerObject.transform.position = Vector3.zero;
            Set(joystick, "input", Vector2.right);
            Invoke(player, "MoveByJoystick", 0.1f);
            Near(playerObject.transform.position.x, 0.6f, "joystick ship speed");
            joystick.CancelInput();
            Invoke(player, "MoveByJoystick", 0.1f);
            Near(playerObject.transform.position.x, 0.6f, "zero input no inertia");
            Set(player, "overrideMaxSpeed", true);
            Set(player, "maxSpeed", 3f);
            Set(joystick, "input", Vector2.right);
            playerObject.transform.position = Vector3.zero;
            Invoke(player, "MoveByJoystick", 0.1f);
            Near(playerObject.transform.position.x, 0.3f, "explicit maximum speed");
            Set(player, "speed", 500f);
            Near(player.JoystickMaxSpeed, 3f, "override ignores hull speed");
            Set(joystick, "input", Vector2.right * 0.5f);
            playerObject.transform.position = Vector3.zero;
            Invoke(player, "MoveByJoystick", 0.1f);
            Near(playerObject.transform.position.x, 0.15f, "analog scales overridden speed");
            Set(player, "maxSpeed", 0f);
            Invoke(player, "MoveByJoystick", 0.1f);
            Near(playerObject.transform.position.x, 0.15f, "zero maximum stops joystick movement");
            Set(player, "overrideMaxSpeed", false);
            Near(player.JoystickMaxSpeed, 10f, "disabled override restores hull speed");
            Set(player, "speed", 300f);
            Set(joystick, "input", Vector2.right);
            Invoke(player, "MoveByJoystick", 100f);
            Near(camera.WorldToViewportPoint(playerObject.transform.position).x, 1f, "clamp to battle area");

            playerObject.transform.position = Vector3.zero;
            GameObject enemyObject = Create("Contact test enemy", preview);
            enemyObject.transform.position = Vector3.left;
            EnemyContactImpact impact = enemyObject.AddComponent<EnemyContactImpact>();
            GameObject firstHullObject = Create("First test hull", preview);
            firstHullObject.transform.SetParent(playerObject.transform, false);
            ParentShip firstHull = firstHullObject.AddComponent<ParentShip>();
            firstHull.ShipData = AssetDatabase.LoadAssetAtPath<ShipData>("Assets/Prefub/Data/Ship/Arkanoid.asset");
            Invoke(impact, "ApplyKnockback", firstHull, body, 3f);
            Check(((Vector2)Get(player, "knockbackVelocity")).x > 0f, "enemy impact routes to direct recoil");
            Check(body.linearVelocity == Vector2.zero, "direct recoil without Rigidbody velocity");
            Set(player, "knockbackVelocity", Vector2.zero);
            ShipSelect selection = playerObject.AddComponent<ShipSelect>();
            Set(selection, "playerController", player);
            Set(player, "shipSelect", selection);
            GameObject secondHullObject = Create("Second test hull", preview);
            secondHullObject.transform.SetParent(playerObject.transform, false);
            ParentShip secondHull = secondHullObject.AddComponent<ParentShip>();
            secondHull.ShipData = firstHull.ShipData;
            player.ChangeShipData(firstHull);
            joystick.OnPointerDown(pointer);
            joystick.OnPointerUp(pointer);
            Check(player.CurrentShip == (Application.isFocused ? secondHull : firstHull), "normal stick release switches actual hull");
            ParentShip selected = player.CurrentShip;
            joystick.OnPointerUp(pointer);
            Check(player.CurrentShip == selected, "release switches only once");
            joystick.OnPointerDown(pointer);
            joystick.CancelInput();
            joystick.OnPointerUp(pointer);
            Check(player.CurrentShip == selected, "canceled stick does not switch hull");
            Set(player, "movementMode", PlayerMovementMode.PhysicsTouch);
            Invoke(player, "SynchronizeMovementMode");
            Check(body.bodyType == RigidbodyType2D.Dynamic, "restore original physics mode");

            GameObject viewObject = GameObject.Find("LevelConfig");
            if (viewObject == null)
                return new { passed = checks, skipped = "Fighting viewport: battle scene is not open", sceneObjectsCreatedInPreviewOnly = true };
            BattleCameraViewport view = viewObject.GetComponent<BattleCameraViewport>();
            foreach (var size in new[] {new Vector2(1080, 2400), new Vector2(1170, 2532), new Vector2(1920,1080)})
            {
                Rect available = new Rect(0f, 200f, size.x, size.y - 200f);
                Rect result = (Rect)Invoke(view, "GetGameplayRect", size.x, size.y, available);
                Check(result.xMin >= 0f && result.xMax <= 1.001f && result.yMax <= 1.001f, "viewport inside screen " + size);
                Near(result.yMin * size.y, 200f, "viewport above lower HUD " + size);
                Near(result.width * size.x / (result.height * size.y), 0.52f, "fixed aspect " + size);
            }
            Rect gameplay = view.GameplayScreenRect;
            Check(gameplay.yMin > 0f && gameplay.height > 100f, "configured real bottom HUD viewport");
            var viewData = new SerializedObject(view);
            Check(viewData.FindProperty("bottomHudBoundary").objectReferenceValue != null
                && viewData.FindProperty("topHudBoundary").objectReferenceValue == null, "scene boundary references");
            return new { passed = checks, focused = Application.isFocused, sceneObjectsCreatedInPreviewOnly = true };
        }
        finally
        {
            Time.timeScale = oldTimeScale;
            EditorSceneManager.ClosePreviewScene(preview);
            UnityEngine.Object.DestroyImmediate(settings);
        }
    }

    private static GameObject Create(string name, UnityEngine.SceneManagement.Scene scene, params Type[] types)
    {
        var go = new GameObject(name, types);
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, scene);
        go.hideFlags = HideFlags.HideAndDontSave;
        return go;
    }
    private static object Get(object target, string field) => target.GetType().GetField(field, Flags).GetValue(target);
    private static void Set(object target, string field, object value) => target.GetType().GetField(field, Flags).SetValue(target, value);
    private static object Invoke(object target, string method, params object[] args) => target.GetType().GetMethod(method, Flags).Invoke(target, args);
    private static void Check(bool condition, string name)
    {
        if (!condition) throw new Exception(name);
        checks++;
    }
    private static void Near(float actual, float expected, string name) => Check(Mathf.Abs(actual - expected) < 0.002f, name + ": " + actual + " != " + expected);
}
