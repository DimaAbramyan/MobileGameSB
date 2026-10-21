using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
public static class VerifyArkanoidBounceBoost
{
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
    static int checks;
    static void Check(bool condition, string name) { if (!condition) throw new Exception(name); checks++; }
    static void Set(object target, string field, object value) => target.GetType().GetField(field, Flags).SetValue(target, value);
    static void Invoke(object target, string name, params object[] args) => target.GetType().GetMethod(name, Flags).Invoke(target, args);
    static bool Near(float a, float b) => Mathf.Abs(a-b) < .001f;
    public static object Run()
    {
        checks = 0;
        var scene = EditorSceneManager.NewPreviewScene();
        var root = new GameObject("Arkanoid boost verification"); SceneManager.MoveGameObjectToScene(root, scene); root.SetActive(false);
        try
        {
            var contract = new ArkanoidShipMetaContract();
            Check(Near(contract.PaddleBounceSpeedMultiplier, 1.5f) && Near(contract.PaddleBounceRecoveryDuration, 1f), "Default boost settings");
            Set(contract, "ballSpeed", 5f); Set(contract, "paddleBounceSpeedMultiplier", 2f); Set(contract, "paddleBounceRecoveryDuration", 2f);
            Set(contract, "paddleBounceRecoveryCurve", AnimationCurve.Linear(0,1,1,0));
            Check(Near(contract.EvaluatePaddleBounceSpeedMultiplier(0),2), "Initial multiplier");
            Check(Near(contract.EvaluatePaddleBounceSpeedMultiplier(1),1.5f), "Normalized midpoint");
            Check(Near(contract.EvaluatePaddleBounceSpeedMultiplier(2),1), "Exact base at duration");
            Set(contract, "paddleBounceRecoveryCurve", AnimationCurve.Linear(0,3,1,3));
            Check(Near(contract.EvaluatePaddleBounceSpeedMultiplier(1),2), "Curve clamped above one");
            Set(contract, "paddleBounceRecoveryCurve", AnimationCurve.Linear(0,-1,1,-1));
            Check(Near(contract.EvaluatePaddleBounceSpeedMultiplier(1),1), "Curve clamped below zero");
            Set(contract, "paddleBounceRecoveryCurve", AnimationCurve.Linear(0,1,1,0));
            var clone = (ArkanoidShipMetaContract)contract.Clone();
            Set(contract, "paddleBounceRecoveryCurve", AnimationCurve.Linear(0,0,1,0));
            Check(Near(clone.EvaluatePaddleBounceSpeedMultiplier(1),1.5f), "Clone preserves independent curve");
            var ballObj = new GameObject("Ball"); ballObj.transform.SetParent(root.transform);
            var body = ballObj.AddComponent<Rigidbody2D>(); body.gravityScale=0; body.bodyType=RigidbodyType2D.Kinematic;
            ballObj.AddComponent<CircleCollider2D>(); var ball = ballObj.AddComponent<ArkanoidBall>(); Invoke(ball,"Awake"); ball.ApplyShipMetaStats(clone);
            var paddleObj = new GameObject("Paddle"); paddleObj.transform.SetParent(root.transform); paddleObj.AddComponent<BoxCollider2D>(); var paddle = paddleObj.AddComponent<ArkanoidPaddle>(); Invoke(paddle,"Awake");
            var cameraObj = new GameObject("Test camera"); cameraObj.transform.SetParent(root.transform); var camera = cameraObj.AddComponent<Camera>(); camera.orthographic=true; camera.orthographicSize=100; camera.transform.position=new Vector3(0,0,-10); Set(ball,"mainCamera",camera);
            root.SetActive(true); Set(ball,"mainCamera",camera);
            Check(Near(ball.CurrentSpeed,5), "Launch has no boost");
            Invoke(ball,"BounceFromPaddle",paddle,true);
            Check(Near(ball.CurrentSpeed,10) && Near(body.linearVelocity.magnitude,10), "Paddle bounce immediately boosts actual velocity");
            Set(ball,"bounceBoostElapsed",1f); Invoke(ball,"MoveBall");
            Check(Near(ball.CurrentSpeed,7.5f) && Near(body.linearVelocity.magnitude,7.5f), "Movement uses decaying boost");
            ball.NotifyEnemyProjectileIntercepted();
            Check(Near(ball.CurrentSpeed,7.5f), "Intercept does not reset boost");
            Invoke(ball,"BounceFromPaddle",paddle,true);
            Check(Near(ball.CurrentSpeed,10), "Repeated paddle bounce restarts without stacking");
            for(int i=0;i<Mathf.CeilToInt(2f/Time.fixedDeltaTime)+1;i++) Invoke(ball,"FixedUpdate");
            Check(Near(ball.CurrentSpeed,5) && Near(body.linearVelocity.magnitude,5), "FixedUpdate returns actual velocity to base at duration");
            Invoke(ball,"BounceFromPaddle",paddle,true); ball.ResetAndLaunch(Vector3.zero,Vector2.up);
            Check(Near(ball.CurrentSpeed,5), "Respawn clears boost");
        }
        finally { UnityEngine.Object.DestroyImmediate(root); EditorSceneManager.ClosePreviewScene(scene); }
        return new { passed = checks };
    }
}
