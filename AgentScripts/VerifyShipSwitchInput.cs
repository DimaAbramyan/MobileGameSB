using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class VerifyShipSwitchInput
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static int checks;
    static void Check(bool value, string message) { if (!value) throw new Exception(message); checks++; }
    static void Set(object target, string field, object value) => target.GetType().GetField(field, Private).SetValue(target, value);
    static object Get(object target, string field) => target.GetType().GetField(field, Private).GetValue(target);
    static object Call(object target, string method, params object[] args) => target.GetType().GetMethod(method, Private).Invoke(target, args);
    static bool Touch(PlayerController controller, float time, TouchPhase phase, Vector2 position, int id = 1, bool allowed = true, float shortSide = 1000f)
        => (bool)Call(controller, "ProcessShipSwitchTouch", position, phase, id, time, shortSide, allowed);
    static void Reset(PlayerController controller) => Call(controller, "ResetShipSwitchTouch");

    public static object Run()
    {
        if (EditorApplication.isPlaying || EditorApplication.isCompiling || EditorApplication.isUpdating)
            throw new Exception("Requires idle Editor");
        checks = 0;
        var scene = EditorSceneManager.NewPreviewScene();
        var root = new GameObject("Ship switching verification"); root.SetActive(false);
        SceneManager.MoveGameObjectToScene(root, scene);
        root.AddComponent<Rigidbody2D>();
        var selection = root.AddComponent<ShipSelect>();
        var controller = root.AddComponent<PlayerController>();
        float timeScale = Time.timeScale;
        try
        {
            Time.timeScale = 1f;
            Call(controller, "Awake"); Call(selection, "Awake");
            var ships = new ParentShip[2];
            for (int i = 0; i < ships.Length; i++)
            {
                var child = new GameObject("Ship " + i); child.transform.SetParent(root.transform);
                ships[i] = child.AddComponent<ParentShip>();
                ships[i].ShipData = AssetDatabase.LoadAssetAtPath<ShipData>("Assets/Prefub/Data/Ship/Orbit.asset");
            }
            Set(controller, "currentShip", ships[0]);
            int switches = 0;
            controller.OnCurrentShipChanged += ship => switches++;
            Check(controller.ShipSwitchPressWindow == .25f && controller.ShipSwitchTouchDistancePercent == 4f,
                "Initial settings: 0.25 seconds and 4 percent");
            foreach (string field in new[] { "shipSwitchPressWindow", "shipSwitchTouchDistancePercent" })
                Check(typeof(PlayerController).GetField(field, Private).GetCustomAttribute<RangeAttribute>() != null,
                    "Inspector slider: " + field);
            var a = new Vector2(100, 100); var b = new Vector2(500, 500);
            Check(!Touch(controller, 0f, TouchPhase.Began, a), "First press does not switch");
            Check(!Touch(controller, 10f, TouchPhase.Ended, b) && switches == 0, "Release after prolonged movement does not switch");
            Check(Touch(controller, 10.2f, TouchPhase.Began, b + Vector2.right * 10) && switches == 1
                && controller.CurrentShip == ships[1], "Repeat press switches actual selected ship before release");
            Check(!Touch(controller, 10.21f, TouchPhase.Stationary, b) && switches == 1, "Holding second press cannot switch repeatedly");
            Touch(controller, 10.22f, TouchPhase.Ended, b);
            Check(switches == 1, "Second release does not add another switch");
            Reset(controller);
            Touch(controller, 20f, TouchPhase.Began, a); Touch(controller, 20f, TouchPhase.Ended, a);
            Check(!Touch(controller, 20.251f, TouchPhase.Began, a), "Press outside time window is rejected");
            Reset(controller);
            Touch(controller, 30f, TouchPhase.Began, a); Touch(controller, 30f, TouchPhase.Ended, a);
            Check(!Touch(controller, 30.1f, TouchPhase.Began, a + Vector2.right * 41), "Excess distance is rejected");
            Reset(controller);
            Touch(controller, 40f, TouchPhase.Began, a); Touch(controller, 40f, TouchPhase.Ended, a);
            Check(Touch(controller, 40.25f, TouchPhase.Began, a + Vector2.right * 40), "Exact time and distance boundaries are accepted");
            Reset(controller);
            Touch(controller, 50f, TouchPhase.Began, a); Touch(controller, 50f, TouchPhase.Ended, a);
            Touch(controller, 50.1f, TouchPhase.Began, b, 2, false);
            Touch(controller, 50.15f, TouchPhase.Ended, b, 2);
            Check((Vector2)Get(controller, "previousShipTouchPosition") == a, "Ability press does not change previous touch distance");
            Check(Touch(controller, 50.2f, TouchPhase.Began, a), "Ability finger does not replace prior eligible touch");
            Reset(controller);
            Touch(controller, 60f, TouchPhase.Began, a);
            Check(!Touch(controller, 60.1f, TouchPhase.Began, a, 2), "Simultaneous second finger cannot switch");
            Touch(controller, 60.15f, TouchPhase.Ended, b, 2);
            Check((int)Get(controller, "activeTouchId") == 1, "Other finger release cannot steal gesture");
            Touch(controller, 60.2f, TouchPhase.Canceled, a);
            Check(!Touch(controller, 60.21f, TouchPhase.Began, a), "Canceled gesture cannot prime switching");
            Reset(controller);
            Touch(controller, 70f, TouchPhase.Began, a); Touch(controller, 70f, TouchPhase.Ended, a);
            Time.timeScale = 0f;
            Check(!Touch(controller, 70.1f, TouchPhase.Began, a), "Pause blocks switching");
            Time.timeScale = 1f;
            Check(!Touch(controller, 70.2f, TouchPhase.Began, a), "Pause clears pending gesture");
            Reset(controller);
            Touch(controller, 80f, TouchPhase.Began, a); Touch(controller, 80f, TouchPhase.Ended, a);
            controller.LockShipSwitching(1f);
            Check(!Touch(controller, 80.1f, TouchPhase.Began, a), "Ability switch lock blocks repeat press");
            Set(controller, "shipSwitchLockedUntil", 0f); Reset(controller);
            Set(controller, "movementMode", PlayerMovementMode.Joystick);
            Touch(controller, 90f, TouchPhase.Began, a); Touch(controller, 90f, TouchPhase.Ended, a);
            Check(Touch(controller, 90.2f, TouchPhase.Began, a), "Joystick uses same repeated press gesture");
            Set(controller, "movementMode", PlayerMovementMode.JoystickFocus); Reset(controller);
            Check(!Touch(controller, 100f, TouchPhase.Began, a), "Focus button mode retains dedicated switch button");
            Set(controller, "switchShipOnFocusRelease", true);
            Touch(controller, 100f, TouchPhase.Began, a); Touch(controller, 100f, TouchPhase.Ended, a);
            Check(Touch(controller, 100.2f, TouchPhase.Began, a), "Focus gesture mode switches on press");
            Set(controller, "movementMode", PlayerMovementMode.DirectTouch); Reset(controller);
            Touch(controller, 110f, TouchPhase.Began, a); Touch(controller, 110f, TouchPhase.Ended, a);
            Check(!Touch(controller, 110.1f, TouchPhase.Began, a + Vector2.right * 21, shortSide: 500f),
                "Distance scales with screen resolution");
            return new { passed = checks, actualShipSwitches = switches };
        }
        finally
        {
            Time.timeScale = timeScale;
            UnityEngine.Object.DestroyImmediate(root);
            EditorSceneManager.ClosePreviewScene(scene);
        }
    }

    public static object InspectScene()
    {
        var scene = EditorSceneManager.OpenPreviewScene("Assets/Scenes/Fighting/Fighting.unity");
        var rows = new System.Collections.Generic.List<object>();
        try
        {
            foreach (var root in scene.GetRootGameObjects())
                foreach (var controller in root.GetComponentsInChildren<PlayerController>(true))
                {
                    var serialized = new SerializedObject(controller);
                    rows.Add(new { controller = controller.name,
                        windowSeconds = serialized.FindProperty("shipSwitchPressWindow").floatValue,
                        distancePercent = serialized.FindProperty("shipSwitchTouchDistancePercent").floatValue });
                }
            return new { scene = scene.path, controllers = rows };
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
    }
}
