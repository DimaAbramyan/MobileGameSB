using UnityEditor;
using UnityEngine;

public static partial class EnemyShootingPreview
{
    private static bool drawingRotationPose, drawingRotationFrom;
    private static Color rotationPoseColor;
    private static readonly Color RotationFromColor = new(.2f, .8f, 1f, .7f);
    private static readonly Color RotationToColor = new(1f, .5f, .12f, .95f);
    private static readonly AnimationCurve RotationGuideProgress = AnimationCurve.Linear(0f, 0f, 1f, 1f);
    private const int MaximumCoverageRays = 32;
    private static readonly Vector3[] coverageStarts = new Vector3[MaximumCoverageRays];
    private static readonly Vector3[] coverageEnds = new Vector3[MaximumCoverageRays];
    private static readonly int[] coverageArms = new int[MaximumCoverageRays];
    private static readonly int[] coverageProjectiles = new int[MaximumCoverageRays];
    private static readonly Vector3[] coverageTriangle = new Vector3[3];

    private static Color RotationTint(Color color) => drawingRotationPose
        ? new Color(rotationPoseColor.r, rotationPoseColor.g, rotationPoseColor.b, color.a * rotationPoseColor.a) : color;

    // Carries the existing emission bindings/aim context through all drawing poses.
    // A binding is read only once per pattern, not once per coverage ray or angle.
    private struct ExecutorPreviewContext
    {
        public ExecutorBinding Binding;
        public Vector3 EnemyPosition;
        public Vector3? Target, PlayerPosition, CapturedEnemyPosition, RequestedDirection;
        public float CapturedRotationTime;
        public bool CaptureAtPose, Aimed, Stationary, Detailed;
        public int RayBudget, NumericOwner;
        public uint NumericOrdinal;

        public EnemyEmissionPreviewFrame Frame(float time, ref EnemyShootingSettings shooting,
            EnemyRotationSettings rotation, out Quaternion basis)
        {
            var frame = CreateFrame(Binding, shooting, EnemyPosition, time, rotation, out basis);
            frame.Target = Target; frame.PlayerPosition = PlayerPosition;
            if (EnemyEmissionOriginSettings.TryGetGameplayBounds(WavePreviewGameplayCamera.GameplayCamera, EnemyPosition.z, out var bounds))
                frame.GameplayBounds = bounds;
            shooting = ApplyFirstNumericSample(shooting, ref frame, NumericOwner, NumericOrdinal);
            frame.Origin = EnemyShootingPreviewGeometry.ResolveOrigin(shooting, frame);
            if (CapturedEnemyPosition.HasValue)
            {
                float captureTime = CaptureAtPose ? time : CapturedRotationTime;
                var captured = CreateFrame(Binding, shooting, CapturedEnemyPosition.Value, captureTime, rotation, out _);
                captured.PlayerPosition = frame.PlayerPosition; captured.GameplayBounds = frame.GameplayBounds;
                ApplyFirstNumericSample(shooting, ref captured, NumericOwner, NumericOrdinal);
                frame.HasCapturedAim = true; frame.CapturedEnemyPosition = CapturedEnemyPosition.Value;
                frame.CapturedShootingPoint = captured.ShootingPoint; frame.CapturedRotationTime = captureTime;
                frame.CapturedOrigin = EnemyShootingPreviewGeometry.ResolveOrigin(shooting, captured);
            }
            return frame;
        }
    }

    private static void DrawRotationRange(ExecutorPreviewContext context, EnemyShootingSettings shooting,
        EnemyRotationSettings rotation)
    {
        var range = EnemyShootingPreviewGeometry.GetLayoutRotationRange(shooting, rotation);
        Color oldColor = Handles.color;
        var oldDepth = Handles.zTest;
        Handles.zTest = UnityEngine.Rendering.CompareFunction.Always;
        try
        {
            DrawRotationCoverage(context, shooting, range);
            DrawRotationPose(context, shooting, rotation, range.FromTime, RotationFromColor, true);
            DrawRotationPose(context, shooting, rotation, range.ToTime, RotationToColor, false);
            if (context.Detailed) DrawRotationRangeLabels(context, shooting, rotation, range);
        }
        finally { Handles.color = oldColor; Handles.zTest = oldDepth; }
    }

    private static void DrawRotationPose(ExecutorPreviewContext context, EnemyShootingSettings shooting,
        EnemyRotationSettings rotation, float time, Color color, bool from)
    {
        var frame = context.Frame(time, ref shooting, rotation, out var basis);
        bool wasDrawing = drawingRotationPose, wasFrom = drawingRotationFrom;
        Color oldTint = rotationPoseColor;
        drawingRotationPose = true; drawingRotationFrom = from; rotationPoseColor = color;
        try
        {
            // Reuse the complete ordinary volley renderer, including Fan, Stack, Spread and trajectories.
            Draw(shooting, frame.ShootingPoint, basis, time, rotation, context.EnemyPosition,
                context.RequestedDirection, context.Aimed, frame, context.Stationary, context.Detailed, context.RayBudget);
        }
        finally { drawingRotationPose = wasDrawing; drawingRotationFrom = wasFrom; rotationPoseColor = oldTint; }
    }

    private static void DrawRotationCoverage(ExecutorPreviewContext context, EnemyShootingSettings shooting,
        EnemyLayoutRotationPreviewRange range)
    {
        float start = range.MinimumAngle, sweep = Mathf.Min(360f, range.MaximumAngle - start);
        if (sweep < .001f) return;
        int limit = context.Detailed ? MaximumCoverageRays : Mathf.Min(2, Mathf.Max(1, context.RayBudget));
        int arms = Mathf.Min(shooting.DirectionCount, limit), perArm = Mathf.Max(1, limit / arms), count = 0;
        for (int sample = 0; sample < arms; sample++)
        {
            int arm = SampleIndex(sample, arms, shooting.DirectionCount);
            int unique = shooting.ProjectilesPerDirection / EnemyShootingPreviewGeometry.CoincidentStackCount(shooting);
            int rays = Mathf.Min(unique, perArm);
            for (int ray = 0; ray < rays; ray++)
            {
                coverageArms[count] = arm;
                coverageProjectiles[count++] = SampleIndex(ray, rays, unique);
            }
        }
        // This transient override expresses an angle through the existing runtime rotation API.
        // Sampling angles instead of elapsed time keeps the coverage independent of an easing curve's speed.
        var poseRotation = new EnemyRotationSettings();
        int steps = context.Detailed ? Mathf.Clamp(Mathf.CeilToInt(sweep / 15f), 2, 24) : 8;
        for (int step = 0; step <= steps; step++)
        {
            float angle = start + sweep * step / steps;
            poseRotation.Configure(FourWayEnemyRotationDirection.CounterClockwise, FourWayEnemyRotationMode.ByAngle,
                Mathf.Max(.01f, Mathf.Abs(angle)), RotationGuideProgress, angle, 0f, true);
            var poseShooting = shooting;
            var frame = context.Frame(1f, ref poseShooting, poseRotation, out var basis);
            for (int ray = 0; ray < count; ray++)
            {
                int arm = coverageArms[ray], index = coverageProjectiles[ray];
                var numeric = frame.NumericSpeed.Numeric.ForDirection(arm);
                var geometry = poseShooting.SampleGeometry(numeric, true);
                var emitter = EnemyShootingPreviewGeometry.GetEmitter(geometry, arm, frame, basis, 1f,
                    poseRotation, context.RequestedDirection, context.Aimed, context.Stationary);
                var projectile = EnemyShootingPreviewGeometry.GetProjectile(geometry, emitter, index, numeric.ForProjectile(index));
                Vector3 end = projectile.Position + projectile.Direction * ProjectileRayLength;
                if (step > 0)
                {
                    Handles.color = new Color(.35f, .75f, 1f, context.Detailed ? .025f : .012f);
                    coverageTriangle[0] = coverageStarts[ray]; coverageTriangle[1] = coverageEnds[ray]; coverageTriangle[2] = end;
                    Handles.DrawAAConvexPolygon(coverageTriangle);
                    coverageTriangle[1] = end; coverageTriangle[2] = projectile.Position;
                    Handles.DrawAAConvexPolygon(coverageTriangle);
                    Handles.color = new Color(.35f, .75f, 1f, .22f);
                    Handles.DrawLine(coverageStarts[ray], projectile.Position);
                    Handles.DrawLine(coverageEnds[ray], end);
                }
                coverageStarts[ray] = projectile.Position; coverageEnds[ray] = end;
            }
        }
    }

    private static void DrawRotationRangeLabels(ExecutorPreviewContext context, EnemyShootingSettings shooting,
        EnemyRotationSettings rotation, EnemyLayoutRotationPreviewRange range)
    {
        var fromFrame = context.Frame(range.FromTime, ref shooting, rotation, out var basis);
        var toFrame = context.Frame(range.ToTime, ref shooting, rotation, out _);
        var from = EnemyShootingPreviewGeometry.GetEmitter(shooting, 0, fromFrame, basis, range.FromTime,
            rotation, context.RequestedDirection, context.Aimed, context.Stationary);
        var to = EnemyShootingPreviewGeometry.GetEmitter(shooting, 0, toFrame, basis, range.ToTime,
            rotation, context.RequestedDirection, context.Aimed, context.Stationary);
        Vector3 fromLabel = from.Position + from.BaseDirection * (ProjectileRayLength + .3f);
        Vector3 toLabel = to.Position + to.BaseDirection * (ProjectileRayLength + .3f);
        if ((toLabel - fromLabel).sqrMagnitude < .5f) { fromLabel += Vector3.up * .3f; toLabel += Vector3.down * .3f; }
        Handles.color = RotationFromColor;
        Handles.Label(fromLabel, $"From Position · {range.FromAngle:0.#}°");
        Handles.color = RotationToColor;
        Handles.Label(toLabel, $"To Position · {range.ToAngle:0.#}°" + (range.Continuous ? " (full turn)" : ""));
        Vector3 origin = EnemyShootingPreviewGeometry.ResolveOrigin(shooting, fromFrame);
        float sweep = Mathf.Min(360f, range.MaximumAngle - range.MinimumAngle);
        Vector3 arcStart = Quaternion.Euler(0f, 0f, range.MinimumAngle) * (basis * Vector3.up);
        Handles.color = new Color(.35f, .75f, 1f, .07f);
        Handles.DrawSolidArc(origin, Vector3.forward, arcStart, sweep, .4f);
        Handles.color = new Color(.35f, .75f, 1f, .6f);
        Handles.DrawWireArc(origin, Vector3.forward, arcStart, sweep, .4f);
        string mode = range.Continuous ? "Continuous · 360°" : (rotation ?? shooting.Rotation).RotationMode == FourWayEnemyRotationMode.PingPongByAngle ? "PingPong" : "By Angle";
        Handles.Label(origin + Vector3.down * .8f,
            $"Layout Rotation: {mode}\n{shooting.ProjectilesPerVolley} projectiles / pose");
        int directions = Mathf.Min(shooting.DirectionCount, Mathf.Min(MaxPreviewDirections, Mathf.Max(1, context.RayBudget)));
        int unique = shooting.ProjectilesPerDirection / EnemyShootingPreviewGeometry.CoincidentStackCount(shooting);
        if (directions < shooting.DirectionCount || unique > Mathf.Max(1, context.RayBudget / directions))
            Handles.Label(origin + Vector3.down * 1.2f, "Dense volley: sampled rays. Select a slot for details.");
    }
}
