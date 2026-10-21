using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class VerifyDirectTouchMovement
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    public static object Run()
    {
        if (EditorApplication.isPlaying || EditorApplication.isCompiling || EditorApplication.isUpdating)
            throw new Exception("Editor must be idle in Edit Mode.");

        var checks = new List<string>();
        Scene scene = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
        var root = new GameObject("Player movement fixture");
        var cameraRoot = new GameObject("Touch projection fixture");
        SceneManager.MoveGameObjectToScene(root, scene);
        SceneManager.MoveGameObjectToScene(cameraRoot, scene);
        try
        {
            var rb = root.AddComponent<Rigidbody2D>();
            rb.gravityScale = 3f;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            root.AddComponent<CircleCollider2D>();
            var controller = root.AddComponent<PlayerController>();
            Invoke(controller, "Awake");
            Check(rb.bodyType == RigidbodyType2D.Kinematic && rb.simulated
                && rb.useFullKinematicContacts, "Direct mode retains contacts without dynamic motion", checks);
            Check(rb.interpolation == RigidbodyInterpolation2D.None, "Direct input has no physics interpolation delay", checks);
            var camera = cameraRoot.AddComponent<Camera>();
            camera.transform.position = new Vector3(0f, 0f, -10f);
            camera.orthographic = true;
            camera.orthographicSize = 5f;
            camera.pixelRect = new Rect(0f, 0f, 1000f, 1000f);
            Vector2 start = camera.WorldToScreenPoint(new Vector3(2f, 1f, 0f));
            Touch(controller, camera, start, TouchPhase.Began);
            Check(root.transform.position == Vector3.zero, "Touch anywhere anchors without teleporting", checks);
            Vector2 moved = camera.WorldToScreenPoint(new Vector3(3f, 1.5f, 0f));
            Touch(controller, camera, moved, TouchPhase.Moved);
            Check(Vector2.Distance(root.transform.position, new Vector2(1f, 0.5f)) < 0.0001f,
                "Finger delta moves ship relative to its position", checks);
            Check(Vector2.Distance(controller.CurrentVelocity, new Vector2(10f, 5f)) < 0.001f,
                "Ability velocity is actual displacement per second", checks);
            Touch(controller, camera, moved, TouchPhase.Stationary);
            Check(controller.CurrentVelocity == Vector2.zero, "Stationary finger stops velocity", checks);
            Vector3 before = root.transform.position;
            Physics2D.SyncTransforms();
            scene.GetPhysicsScene2D().Simulate(0.1f);
            Check(Vector3.Distance(before, root.transform.position) < 0.0001f,
                "Gravity and inertia cannot drift direct movement", checks);
            Touch(controller, camera, moved, TouchPhase.Ended);
            Check(controller.CurrentVelocity == Vector2.zero
                && !(bool)Get(controller, "hasDirectTouchAnchor"), "Release clears anchor and velocity", checks);
            Touch(controller, camera, new Vector2(100f, 100f), TouchPhase.Began);
            Check(root.transform.position == before, "New distant press does not teleport", checks);
            Touch(controller, camera, new Vector2(100f, 100f), TouchPhase.Canceled);
            Check(!(bool)Get(controller, "hasDirectTouchAnchor"), "Canceled touch clears drag", checks);
            root.transform.position = Vector3.zero;
            Set(controller, "directTouchSensitivity", 2f);
            Touch(controller, camera, start, TouchPhase.Began);
            Touch(controller, camera, moved, TouchPhase.Moved);
            Check(Vector2.Distance(root.transform.position, new Vector2(2f, 1f)) < 0.0001f,
                "Sensitivity scales displacement", checks);
            Touch(controller, camera, new Vector2(2000f, 2000f), TouchPhase.Moved);
            Vector3 viewport = camera.WorldToViewportPoint(root.transform.position);
            Check(viewport.x <= 1.00001f && viewport.y <= 1.00001f
                && viewport.x >= 0f && viewport.y >= 0f, "Direct drag stays in gameplay viewport", checks);
            controller.LockControls(1f);
            Check(!(bool)Get(controller, "hasDirectTouchAnchor") && controller.CurrentVelocity == Vector2.zero,
                "Control loss clears drag and velocity", checks);
            Set(controller, "useDirectTouchMovement", false);
            Invoke(controller, "SynchronizeMovementMode");
            Check(rb.bodyType == RigidbodyType2D.Dynamic
                && rb.interpolation == RigidbodyInterpolation2D.Interpolate
                && !rb.useFullKinematicContacts, "Bool restores original Rigidbody settings", checks);
            Physics2D.SyncTransforms();
            rb.AddForce(Vector2.right * 10f, ForceMode2D.Impulse);
            before = root.transform.position;
            scene.GetPhysicsScene2D().Simulate(0.1f);
            Check(root.transform.position.x > before.x, "Original physics mode responds to force", checks);
            Set(controller, "useDirectTouchMovement", true);
            Invoke(controller, "SynchronizeMovementMode");
            Check(rb.linearVelocity == Vector2.zero && rb.angularVelocity == 0f,
                "Direct mode removes old momentum", checks);
            Invoke(controller, "OnDisable");
            Check(rb.bodyType == RigidbodyType2D.Dynamic && !rb.useFullKinematicContacts,
                "Disabling controller restores physics", checks);
            return new { passed = checks.Count, checks };
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
            UnityEngine.Object.DestroyImmediate(cameraRoot);
            UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene);
        }
    }

    private static void Touch(PlayerController controller, Camera camera, Vector2 position, TouchPhase phase)
    {
        typeof(PlayerController).GetMethod("ProcessDirectTouch", Private)
            .Invoke(controller, new object[] { position, phase, camera, 0.1f });
    }
    private static void Check(bool value, string name, List<string> checks)
    { if (!value) throw new Exception(name); checks.Add(name); }
    private static object Get(object target, string field) => target.GetType().GetField(field, Private).GetValue(target);
    private static void Set(object target, string field, object value) => target.GetType().GetField(field, Private).SetValue(target, value);
    private static void Invoke(object target, string method) => target.GetType().GetMethod(method, Private).Invoke(target, null);
}
