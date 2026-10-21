using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

public struct EnemyEmissionPreviewFrame
{
    public Vector3 EnemyPosition, ShootingPoint, Scale;
    public Quaternion EnemyBasis, PointBasis;
    public EnemyRandomSpeedSample NumericSpeed;
    public Vector3? Target;
    public Vector3? PlayerPosition, Origin, CapturedOrigin;
    public Rect? GameplayBounds;
    public bool HasCapturedAim;
    public Vector3 CapturedEnemyPosition, CapturedShootingPoint;
    public float CapturedRotationTime;
}

[InitializeOnLoad]
public static class EnemyShootingPreview
{
    static EnemyShootingPreview() => SceneView.duringSceneGui += DrawSelectedFormationSlot;

    private static void DrawSelectedFormationSlot(SceneView view)
    {
        var selected = Selection.activeTransform;
        if (selected == null) return;
        var wave = selected.GetComponentInParent<DirectedEnemySubWave>();
        if (wave != null || selected.TryGetComponent<SimpleEnemyAttackExecutor>(out _))
            DrawSceneControls(view);
        if (!CanDraw) return;
        if (wave == null || selected == wave.transform) return;
        for (int i = 0; i < wave.GetConfiguredEnemySlotCount(); i++)
        {
            var host = wave.GetConfiguredEnemyOverrideHostForSlot(i);
            if (host != null && (selected == host || selected.IsChildOf(host)))
            {
                DrawFormationVolley(wave, i);
                return;
            }
        }
    }

    public static bool ShowFormationVolley
    {
        get => SessionState.GetBool("EnemyShootingPreview.ShowFormationVolley", true);
        set { SessionState.SetBool("EnemyShootingPreview.ShowFormationVolley", value); SceneView.RepaintAll(); }
    }

    public static bool ShowTrajectories
    {
        get => SessionState.GetBool("EnemyShootingPreview.ShowTrajectories", true);
        set { SessionState.SetBool("EnemyShootingPreview.ShowTrajectories", value); SceneView.RepaintAll(); }
    }

    private static bool CanDraw => ShowFormationVolley && !Application.isPlaying
        && Event.current != null && Event.current.type == EventType.Repaint;

    public static void DrawPreviewControls()
    {
        if (GUILayout.Button(new GUIContent(ShowFormationVolley ? "Hide Shooting Preview" : "Show Shooting Preview",
            "Показать/скрыть точки выстрела, направления, Fan/Stack, траектории и подсказки поворота в Scene View.")))
            ShowFormationVolley = !ShowFormationVolley;
        if (!ShowFormationVolley) return;
        NumericRandomPreviewControls.Draw();
        bool trajectories = EditorGUILayout.Toggle(new GUIContent("Projectile Trajectories",
            "Подробные траектории выбранного слота/врага. Общий обзор волны показывает только структуру залпа, чтобы Editor оставался отзывчивым."), ShowTrajectories);
        if (trajectories != ShowTrajectories) ShowTrajectories = trajectories;
    }

    private static void DrawSceneControls(SceneView view)
    {
        Handles.BeginGUI();
        if (GUI.Button(GetSceneControlsRect(view),
            new GUIContent(ShowFormationVolley ? "Hide Shooting Preview" : "Show Shooting Preview",
                "Точки выстрела, layout, Fan/Stack, траектории и поворот врага.")))
            ShowFormationVolley = !ShowFormationVolley;
        Handles.EndGUI();
    }

    private static Rect GetSceneControlsRect(SceneView view)
    {
        // Anchor inside the viewport; dock tabs and Scene toolbars are outside its GUI area.
        Vector2 viewport = view.camera.pixelRect.size / EditorGUIUtility.pixelsPerPoint;
        return new Rect(Mathf.Max(8f, viewport.x - 220f), Mathf.Max(8f, viewport.y - 80f), 210f, 24f);
    }

    private const int MaxOverviewSlots = 64;
    private const int MaxOverviewRays = 512;
    private const int MaxDetailedRays = 512;
    private const int MaxPreviewDirections = 128;

    private static int SampleIndex(int sample, int samples, int total)
        => samples <= 1 ? 0 : (int)((long)sample * (total - 1) / (samples - 1));

    public static bool IsOverviewSlot(int index, int count)
    {
        if (index < 0 || index >= count) return false;
        if (count <= MaxOverviewSlots) return true;
        int sample = (int)(((long)index * (MaxOverviewSlots - 1) + count - 2) / (count - 1));
        return index == SampleIndex(sample, MaxOverviewSlots, count);
    }

    public static int GetOverviewRayBudget(int count)
        => Mathf.Max(1, MaxOverviewRays / Mathf.Max(1, Mathf.Min(count, MaxOverviewSlots)));

    private static DirectedEnemySubWave sampledWave;
    private static float sampledElapsed;
    private static int[] sampledSpawnOrder;
    private static Dictionary<int, Vector3> sampledCurrentPositions;
    private static readonly Dictionary<float, int> captureSamples = new();
    private static readonly List<Dictionary<int, Vector3>> capturePositions = new();
    private struct ExecutorBinding
    {
        public Transform Root, Pivot, Point, Facing;
        public Vector3 Offset;
    }
    private static bool reuseBindings;
    private static readonly Dictionary<SimpleEnemyAttackExecutor, ExecutorBinding> executorBindings = new();

    private static ExecutorBinding GetBinding(SimpleEnemyAttackExecutor executor)
    {
        if (reuseBindings && executorBindings.TryGetValue(executor, out var cached)) return cached;
        using var serialized = new SerializedObject(executor);
        var result = new ExecutorBinding { Root = executor.transform,
            Pivot = serialized.FindProperty("rotationTarget").objectReferenceValue as Transform,
            Point = serialized.FindProperty("projectileSpawnPoint").objectReferenceValue as Transform,
            Facing = serialized.FindProperty("facingTarget").objectReferenceValue as Transform,
            Offset = serialized.FindProperty("projectileSpawnOffset").vector3Value };
        if (reuseBindings) executorBindings[executor] = result;
        return result;
    }

    // Share route samples between enemies for this repaint only; authored edits cannot leave stale samples.
    public static void BeginWavePreview(DirectedEnemySubWave wave, float elapsed,
        Dictionary<int, Vector3> currentPositions, int[] spawnOrder)
    {
        sampledWave = wave;
        sampledElapsed = elapsed;
        sampledCurrentPositions = currentPositions;
        sampledSpawnOrder = spawnOrder;
        captureSamples.Clear();
        executorBindings.Clear();
        reuseBindings = true;
    }

    public static void EndWavePreview()
    {
        sampledWave = null;
        sampledCurrentPositions = null;
        sampledSpawnOrder = null;
        reuseBindings = false;
        executorBindings.Clear();
    }

    public static void DrawFormationVolley(DirectedEnemySubWave wave, int onlySlot = -1)
    {
        if (!CanDraw || wave == null) return;
        var previousDepth = Handles.zTest;
        var previousColor = Handles.color;
        Handles.zTest = UnityEngine.Rendering.CompareFunction.Always;
        executorBindings.Clear();
        reuseBindings = true;
        try
        {
            int count = wave.GetConfiguredEnemySlotCount();
            int shown = onlySlot >= 0 ? 1 : Mathf.Min(count, MaxOverviewSlots);
            for (int sample = 0; sample < shown; sample++)
            {
                int i = onlySlot >= 0 ? onlySlot : SampleIndex(sample, shown, count);
                DrawWaveSlot(wave, i, wave.GetConfiguredFormationSlotPosition(i), 0f, 0f, true, onlySlot >= 0,
                    onlySlot >= 0 ? MaxDetailedRays : GetOverviewRayBudget(count));
            }
            if (onlySlot < 0 && count > 0)
                Handles.Label(wave.transform.position + Vector3.down * .5f,
                    count > shown ? $"Shooting overview: {shown}/{count} enemies (sampled). Select a slot for details."
                        : "Shooting overview · Select a slot for trajectories and labels.");
        }
        finally
        {
            reuseBindings = false;
            executorBindings.Clear();
            Handles.zTest = previousDepth;
            Handles.color = previousColor;
        }
    }

    private const int TrajectorySteps = 24;
    private const float ProjectileRayLength = 1.25f;
    private static readonly Vector3[] trajectoryPoints = new Vector3[97];
    private static readonly Color EmitterColor = new Color(.2f, .95f, 1f, .95f);
    private static readonly Color BaseDirectionColor = new Color(.35f, .65f, 1f, .95f);
    private static readonly Color SectorColor = new Color(.95f, .4f, .85f, .9f);
    private static readonly Color FanColor = new Color(1f, .85f, .3f, .85f);
    private static readonly Color ProjectileColor = new Color(1f, .55f, .15f, .95f);

    public static void Draw(EnemyShootingSettings shooting, Vector3 position, Quaternion basis,
        float rotationTime, EnemyRotationSettings rotationOverride = null,
        Vector3? sourcePosition = null, Vector3? requestedDirection = null,
        bool useAimedShotGunDirection = false, EnemyEmissionPreviewFrame? emissionFrame = null, bool stationary = false,
        bool detailed = true, int rayBudget = MaxDetailedRays)
    {
        if (!CanDraw || shooting == null) return;
        var frame = emissionFrame ?? new EnemyEmissionPreviewFrame { EnemyPosition = sourcePosition ?? position,
            ShootingPoint = position, EnemyBasis = basis, PointBasis = basis, Scale = Vector3.one };
        if (!frame.NumericSpeed.Numeric.Active) shooting = ApplyFirstNumericSample(shooting, ref frame);
        else shooting = shooting.SampleGeometry(frame.NumericSpeed.Numeric);
        rotationOverride = rotationOverride?.SampleNumeric(frame.NumericSpeed.Numeric);
        var firstShooting = shooting.SampleGeometry(frame.NumericSpeed.Numeric.ForDirection(0), true);
        Color previousColor = Handles.color;
        var first = EnemyShootingPreviewGeometry.GetEmitter(firstShooting, 0, frame, basis, rotationTime,
            rotationOverride, requestedDirection, useAimedShotGunDirection, stationary);
        DrawLayoutGuide(shooting, frame, first, detailed);
        int directions = Mathf.Min(shooting.DirectionCount, Mathf.Min(MaxPreviewDirections, Mathf.Max(1, rayBudget)));
        int raysPerDirection = Mathf.Max(1, rayBudget / directions);
        for (int sample = 0; sample < directions; sample++)
        {
            int arm = SampleIndex(sample, directions, shooting.DirectionCount);
            var directionShooting = shooting.SampleGeometry(frame.NumericSpeed.Numeric.ForDirection(arm), true);
            var emitter = arm == 0 ? first : EnemyShootingPreviewGeometry.GetEmitter(directionShooting, arm, frame, basis, rotationTime,
                rotationOverride, requestedDirection, useAimedShotGunDirection, stationary);
            DrawEmitter(directionShooting, emitter, detailed);
            DrawEmitterProjectiles(directionShooting, emitter, EnemyShootingPreviewGeometry.ResolveOrigin(shooting, frame), detailed, raysPerDirection, frame.NumericSpeed);
        }
        if (detailed && (directions < shooting.DirectionCount
            || raysPerDirection < shooting.ProjectilesPerDirection / EnemyShootingPreviewGeometry.CoincidentStackCount(shooting)))
            Handles.Label(first.Position + Vector3.down * .45f, "Dense volley: sampled rays; layout/Fan boundaries retained.");
        Handles.color = previousColor;
    }

    private static void DrawLayoutGuide(EnemyShootingSettings shooting, EnemyEmissionPreviewFrame frame, EnemyPreviewEmitter first, bool detailed)
    {
        var layout = shooting.EmissionLayout;
        Vector3 origin = EnemyShootingPreviewGeometry.ResolveOrigin(shooting, frame);
        frame.ShootingPoint += origin - frame.EnemyPosition;
        frame.EnemyPosition = origin;
        Handles.color = new Color(1f, .35f, .9f, .95f);
        Handles.DrawWireDisc(origin, Vector3.forward, .1f);
        Handles.DrawLine(origin - Vector3.right * .15f, origin + Vector3.right * .15f);
        Handles.DrawLine(origin - Vector3.up * .15f, origin + Vector3.up * .15f);
        if (detailed) Handles.Label(origin + Vector3.up * .18f, "Emission Origin"
            + (shooting.EmissionOrigin.X.Source == EnemyEmissionOriginSource.Player
                ? " · X: " + shooting.EmissionOrigin.X.CaptureMode : "")
            + (shooting.EmissionOrigin.Y.Source == EnemyEmissionOriginSource.Player
                ? " · Y: " + shooting.EmissionOrigin.Y.CaptureMode : ""));
        Handles.color = EmitterColor;
        switch (layout.Type)
        {
            case EnemyEmissionLayoutType.Point:
                if ((first.Position - frame.ShootingPoint).sqrMagnitude > .0001f)
                {
                    Handles.DrawWireDisc(frame.ShootingPoint, Vector3.forward, .055f);
                    Handles.DrawDottedLine(frame.ShootingPoint, first.Position, 3f);
                    if (detailed) Handles.Label(frame.ShootingPoint, "Shooting Point");
                }
                break;
            case EnemyEmissionLayoutType.Circle:
                Vector3 center = frame.EnemyPosition + frame.EnemyBasis * Vector3.Scale(layout.CenterOffset, frame.Scale);
                Handles.DrawWireDisc(center, Vector3.forward, layout.Radius);
                Handles.DrawWireDisc(center, Vector3.forward, .045f);
                if (detailed) Handles.Label(center, "Circle Center");
                break;
            case EnemyEmissionLayoutType.Radial:
                Handles.DrawWireDisc(first.Position, Vector3.forward, .45f);
                break;
            case EnemyEmissionLayoutType.Sector:
                Vector3 start = EnemyShootingPreviewGeometry.SectorStartDirection(shooting, first);
                Vector3 end = EnemyShootingPreviewGeometry.SectorEndDirection(shooting, first);
                DrawAngularGuide(first.Position, start, end, layout.SectorSweep, .95f, SectorColor);
                if (!detailed) break;
                if (Vector3.Dot(start, end) > .9999f)
                    Handles.Label(first.Position + start * 1.04f, $"Start {layout.StartAngle:0.#}° / End {layout.EndAngle:0.#}°");
                else
                {
                    Handles.Label(first.Position + start * 1.04f, $"Start {layout.StartAngle:0.#}°");
                    Handles.Label(first.Position + end * 1.04f, $"End {layout.EndAngle:0.#}°");
                }
                if (layout.IsSequential)
                    Handles.Label(first.Position + Vector3.down * .2f,
                        $"Sequential: +{layout.StartDelay:0.###} s, interval {layout.LaunchInterval:0.###} s"
                        + (layout.WaitUntilSectorReady ? " (after formation ready)" : ""));
                break;
        }
    }

    private static void DrawAngularGuide(Vector3 origin, Vector3 start, Vector3 end, float sweep, float radius, Color color)
    {
        Handles.color = new Color(color.r, color.g, color.b, .035f);
        Handles.DrawSolidArc(origin, Vector3.forward, start, Mathf.Min(360f, sweep), radius);
        Handles.color = color;
        Handles.DrawWireArc(origin, Vector3.forward, start, Mathf.Min(360f, sweep), radius);
        Handles.DrawLine(origin, origin + start * radius);
        Handles.DrawLine(origin, origin + end * radius);
    }

    private static void DrawEmitter(EnemyShootingSettings shooting, EnemyPreviewEmitter emitter, bool detailed)
    {
        bool separate = shooting.EmissionLayout.Type == EnemyEmissionLayoutType.Circle;
        if (separate || emitter.Index == 0)
        {
            Handles.color = EmitterColor;
            Handles.DrawSolidDisc(emitter.Position, Vector3.forward, .045f);
            Handles.DrawWireDisc(emitter.Position, Vector3.forward, .075f);
            if (detailed) Handles.Label(emitter.Position + Vector3.right * .09f,
                separate ? $"E{emitter.Index + 1} · {shooting.ProjectilesPerDirection} projectiles" : $"Emitter · {shooting.ProjectilesPerVolley} projectiles");
        }
        Handles.color = BaseDirectionColor;
        DrawDirection(emitter.Position, emitter.BaseDirection, .6f, .10f, true);
        if (!detailed) return;
        string label = shooting.EmissionLayout.IsSequential
            ? $"#{emitter.LaunchOrder + 1}"
            : $"D{emitter.Index + 1}";
        Handles.Label(emitter.Position + emitter.BaseDirection * (.65f + emitter.Index % 2 * .12f), label);
    }

    private static void DrawEmitterProjectiles(EnemyShootingSettings shooting, EnemyPreviewEmitter emitter, Vector3 source,
        bool detailed, int rayBudget, EnemyRandomSpeedSample numericSpeed = default)
    {
        int fanCount = shooting.UsesFan ? shooting.FanProjectileCount : 1;
        int coincident = EnemyShootingPreviewGeometry.CoincidentStackCount(shooting);
        int drawnCount = coincident > 1 ? fanCount : shooting.ProjectilesPerDirection;
        int samples = Mathf.Min(drawnCount, rayBudget);
        int lastStack = -1;
        for (int sample = 0; sample < samples; sample++)
        {
            int index = SampleIndex(sample, samples, drawnCount);
            var numeric = numericSpeed.Numeric.ForDirection(emitter.Index).ForProjectile(index);
            var projectile = EnemyShootingPreviewGeometry.GetProjectile(shooting, emitter, index, numeric);
            if (shooting.ProjectileType == EnemyProjectileType.Laser)
            {
                EnemyLaserScenePreview.Draw(shooting, projectile.Position, projectile.Direction);
                continue;
            }
            if (projectile.StackIndex != lastStack && shooting.UsesFan && fanCount > 1)
            {
                Vector3 start = Quaternion.Euler(0f, 0f, shooting.FanMinAngle + projectile.SpreadAngle) * emitter.BaseDirection;
                Vector3 end = Quaternion.Euler(0f, 0f, shooting.FanMaxAngle + projectile.SpreadAngle) * emitter.BaseDirection;
                DrawAngularGuide(emitter.Position, start, end, shooting.FanMaxAngle - shooting.FanMinAngle, .8f, FanColor);
            }
            lastStack = projectile.StackIndex;
            Handles.color = new Color(ProjectileColor.r, ProjectileColor.g, ProjectileColor.b, .4f);
            if (detailed && ShowTrajectories)
            {
                float? speed = numericSpeed.Active
                    ? numericSpeed.Evaluate(EnemyAttackProjectileBuilder.NumericProjectileIndex(shooting, emitter.Index, index, emitter.Index * shooting.ProjectilesPerDirection))
                    : (float?)null;
                if (!speed.HasValue && numeric.Active && shooting.UsesExtendedNumericRandomization
                    && shooting.InitialSpeedMode == EnemyProjectileInitialSpeedMode.Random)
                    speed = EnemyAttackProjectileBuilder.SampleLegacyRandomSpeed(shooting,
                        shooting.UsesDnaOrbit ? numeric.ForProjectile(index - index % fanCount) : numeric);
                DrawTrajectory(shooting, projectile.Position, source, projectile.Direction, projectile.DnaMedian,
                    speed, numeric);
            }
            Handles.color = ProjectileColor;
            DrawDirection(projectile.Position, projectile.Direction, ProjectileRayLength, .12f, false);
            Handles.DrawSolidDisc(projectile.Position + projectile.Direction * ProjectileRayLength, Vector3.forward, .025f);
            if (detailed && coincident > 1)
                Handles.Label(projectile.Position + projectile.Direction * (ProjectileRayLength + .1f), $"×{coincident}");
        }
    }

    private static void DrawDirection(Vector3 origin, Vector3 direction, float length, float arrowSize, bool dotted)
    {
        Vector3 tip = origin + direction * length;
        if (dotted) Handles.DrawDottedLine(origin, tip, 3f);
        else Handles.DrawLine(origin, tip);
        Handles.ArrowHandleCap(0, tip, Quaternion.LookRotation(direction, Vector3.forward), arrowSize, EventType.Repaint);
    }

    public static EnemyEmissionPreviewFrame CreateFrame(SimpleEnemyAttackExecutor executor,
        EnemyShootingSettings shooting, Vector3 enemyPosition, float time,
        EnemyRotationSettings rotationOverride, out Quaternion basis)
    {
        var binding = GetBinding(executor);
        Transform root = binding.Root;
        var pivot = binding.Pivot;
        if (pivot == null || shooting.EmissionLayout.Type == EnemyEmissionLayoutType.Point) pivot = root;
        basis = pivot.rotation;
        var point = binding.Point;
        Vector3 origin = point != null ? point.position
            : pivot.TransformPoint(binding.Offset);
        bool owned = point == null || point == root || point.IsChildOf(root);
        if (owned) origin += enemyPosition - root.position;
        if (shooting.EmissionLayout.RotateShootingPointOffset && shooting.Rotates
            && (point == null || point == pivot || point.IsChildOf(pivot)))
        {
            Vector3 center = enemyPosition + pivot.position - root.position;
            origin = center + Quaternion.Euler(0f, 0f, shooting.EvaluateLayoutAngle(time, rotationOverride)) * (origin - center);
        }
        return new EnemyEmissionPreviewFrame { EnemyPosition = enemyPosition, ShootingPoint = origin,
            EnemyBasis = root.rotation, PointBasis = point != null ? point.rotation : basis,
            Scale = point != null && shooting.EmissionLayout.Type == EnemyEmissionLayoutType.Point ? point.lossyScale : root.lossyScale };
    }

    public static void DrawExecutorPattern(SimpleEnemyAttackExecutor executor, EnemyShootingSettings shooting,
        Vector3 enemyPosition, float rotationTime, EnemyRotationSettings rotationOverride = null,
        Vector3? requestedDirection = null, bool aimed = false, Vector3? target = null,
        Vector3? capturedEnemyPosition = null, float capturedRotationTime = 0f, bool stationary = false,
        bool detailed = true, int rayBudget = MaxDetailedRays, Vector3? originPlayerPosition = null, int numericOwner = 0, uint numericOrdinal = 1)
    {
        if (!CanDraw || executor == null) return;
        var frame = CreateFrame(executor, shooting, enemyPosition, rotationTime, rotationOverride, out var basis);
        frame.Target = target;
        frame.PlayerPosition = originPlayerPosition ?? target;
        if (EnemyEmissionOriginSettings.TryGetGameplayBounds(Camera.main, enemyPosition.z, out var bounds))
            frame.GameplayBounds = bounds;
        shooting = ApplyFirstNumericSample(shooting, ref frame, numericOwner, numericOrdinal);
        frame.Origin = EnemyShootingPreviewGeometry.ResolveOrigin(shooting, frame);
        if (capturedEnemyPosition.HasValue)
        {
            var captured = CreateFrame(executor, shooting, capturedEnemyPosition.Value,
                capturedRotationTime, rotationOverride, out _);
            frame.HasCapturedAim = true;
            frame.CapturedEnemyPosition = capturedEnemyPosition.Value;
            frame.CapturedShootingPoint = captured.ShootingPoint;
            frame.CapturedRotationTime = capturedRotationTime;
            captured.PlayerPosition = frame.PlayerPosition;
            captured.GameplayBounds = frame.GameplayBounds;
            ApplyFirstNumericSample(shooting, ref captured, numericOwner, numericOrdinal);
            frame.CapturedOrigin = EnemyShootingPreviewGeometry.ResolveOrigin(shooting, captured);
        }
        Draw(shooting, frame.ShootingPoint, basis, rotationTime, rotationOverride, enemyPosition,
            requestedDirection, aimed, frame, stationary, detailed, rayBudget);
    }

    // Static guides represent the first event of a fresh run. Animated Preview owns the full event state.
    public static EnemyShootingSettings ApplyFirstNumericSample(EnemyShootingSettings shooting, ref EnemyEmissionPreviewFrame frame, int owner = 0, uint ordinal = 1)
    {
        if (!shooting.UsesNumericRandomization) return shooting;
        var state = new EnemyAttackNumericState(unchecked(shooting.NumericRandomSeed + NumericRandomPreviewControls.SeedOffset), owner, ordinal);
        state.BeginOrigin(shooting.EmissionOrigin); state.PrepareOrigin(shooting.EmissionOrigin, true); state.BeginSpeed();
        if (!frame.Origin.HasValue)
        {
            Rect bounds = frame.GameplayBounds.GetValueOrDefault();
            if (!frame.GameplayBounds.HasValue && shooting.EmissionOrigin.RequiresBounds)
                EnemyEmissionOriginSettings.TryGetGameplayBounds(Camera.main, frame.EnemyPosition.z, out bounds);
            frame.Origin = shooting.EmissionOrigin.Resolve(frame.EnemyPosition,
                frame.PlayerPosition ?? frame.Target ?? frame.EnemyPosition, bounds, state.Offsets);
        }
        frame.NumericSpeed = state.PrepareSpeed(shooting, true, 0f);
        return shooting.SampleGeometry(frame.NumericSpeed.Numeric);
    }

    private static void DrawTrajectory(
        EnemyShootingSettings shooting,
        Vector3 spawnPosition,
        Vector3 sourcePosition,
        Vector3 direction,
        Vector3 dnaMedian, float? sampledSpeed = null, EnemyNumericSample numeric = default)
    {
        var values = shooting.SampleProjectile(numeric);
        direction = direction.sqrMagnitude > 0.0001f
            ? direction.normalized
            : Vector3.down;
        float previewSpeed = sampledSpeed ?? (shooting.InitialSpeed > 0f
            ? shooting.InitialSpeed
            : 1f);
        float duration = 0.8f / Mathf.Max(0.01f, previewSpeed);
        if (shooting.UsesDnaOrbit && shooting.GetNumericDnaHalfCycleDuration(values, numeric) > 0f)
            duration = shooting.GetNumericDnaHalfCycleDuration(values, numeric) * 4f;
        int steps = shooting.UsesDnaOrbit ? 96 : TrajectorySteps;
        float deltaTime = duration / steps;
        var points = trajectoryPoints;
        Vector3 position = spawnPosition;
        float orbitRadius = 0f;
        float orbitAngle = 0f;
        Vector3 orbitCenter = sourcePosition;
        float distanceSinceSpeedChange = 0f;
        float currentSpeed = previewSpeed;
        bool reachedSpeedChange = false;
        float dnaElapsed = 0f;
        float dnaInitialAngle = Vector3.SignedAngle(dnaMedian, direction, Vector3.forward) * Mathf.Deg2Rad;

        if (shooting.MovementPattern == EnemyProjectileMovementPattern.Orbit && !shooting.UsesDnaOrbit)
        {
            if (shooting.OrbitCenterMode == EnemyProjectileOrbitCenterMode.FixedWorldPoint)
                orbitCenter = values.OrbitFixedWorldCenter;
            Vector3 offset = spawnPosition - orbitCenter;
            Vector3 radialDirection = offset.sqrMagnitude > 0.0001f
                ? offset.normalized
                : direction;
            orbitRadius = values.OrbitRadius > 0f
                ? values.OrbitRadius
                : offset.magnitude;
            position = orbitCenter + radialDirection * orbitRadius;
            orbitAngle = Mathf.Atan2(radialDirection.y, radialDirection.x) * Mathf.Rad2Deg;
            Handles.DrawSolidDisc(orbitCenter, Vector3.forward, 0.025f);
        }

        points[0] = position;
        for (int index = 1; index <= steps; index++)
        {
            float curveMultiplier = 1f;
            if (shooting.SpeedBehavior == EnemyProjectileSpeedBehavior.Customized)
            {
                if (reachedSpeedChange)
                    distanceSinceSpeedChange += currentSpeed * deltaTime;
                else
                    reachedSpeedChange = direction.y > .0001f ? position.y >= values.SpeedChangeY
                        : direction.y < -.0001f && position.y <= values.SpeedChangeY;
                if (reachedSpeedChange)
                    curveMultiplier = Mathf.Max(0f, shooting.SpeedByDistance.Evaluate(distanceSinceSpeedChange));
            }
            bool orbit = shooting.MovementPattern == EnemyProjectileMovementPattern.Orbit;
            currentSpeed = previewSpeed * (orbit && shooting.SpeedCurveTarget == EnemyProjectileSpeedCurveTarget.Radial
                ? 1f : curveMultiplier);
            float speedRatio = currentSpeed / Mathf.Max(.01f, previewSpeed);
            float radialMultiplier = shooting.SpeedCurveTarget == EnemyProjectileSpeedCurveTarget.BaseSpeed
                ? 1f : curveMultiplier;
            switch (shooting.MovementPattern)
            {
                case EnemyProjectileMovementPattern.AngularTurn:
                    direction = Quaternion.Euler(
                        0f,
                        0f,
                        -values.AngularSpeed * speedRatio * deltaTime) * direction;
                    position += direction * (currentSpeed * deltaTime);
                    break;
                case EnemyProjectileMovementPattern.Lateral:
                    Vector3 right = new(direction.y, -direction.x, 0f);
                    position += (direction * currentSpeed
                        + right * values.LateralSpeed) * deltaTime;
                    break;
                case EnemyProjectileMovementPattern.Orbit:
                    if (shooting.UsesDnaOrbit)
                    {
                        var median = new Unity.Mathematics.float2(dnaMedian.x, dnaMedian.y);
                        dnaElapsed += deltaTime * speedRatio;
                        EnemyProjectileDnaMotion.Evaluate(median, dnaInitialAngle, shooting.GetNumericDnaHalfCycleDuration(values, numeric),
                            dnaElapsed, previewSpeed, out var offset, out var forward, out _);
                        direction = new Vector3(forward.x, forward.y, 0f);
                        position = spawnPosition + new Vector3(offset.x, offset.y, 0f);
                        break;
                    }
                    orbitRadius = Mathf.Max(
                        0f,
                        orbitRadius + values.OrbitRadialSpeed * radialMultiplier * deltaTime);
                    orbitAngle -= values.AngularSpeed * speedRatio * deltaTime;
                    float angleRadians = orbitAngle * Mathf.Deg2Rad;
                    Vector3 previousPosition = position;
                    position = orbitCenter + new Vector3(
                        Mathf.Cos(angleRadians),
                        Mathf.Sin(angleRadians),
                        0f) * orbitRadius;
                    if ((position - previousPosition).sqrMagnitude > .0000001f)
                        direction = (position - previousPosition).normalized;
                    break;
                default:
                    position += direction * (currentSpeed * deltaTime);
                    break;
            }

            points[index] = position;
        }

        Handles.DrawAAPolyLine(2f, steps + 1, points);
    }

    public static void DrawFacing(EnemyBurstAttackSettings patterns, Vector3 position, Quaternion initial,
        Quaternion firingBasis, float rotationTime, EnemyRotationSettings rotationOverride = null,
        Vector3? lockedAim = null, bool resolvedDirection = false, bool detailed = true)
    {
        if (!CanDraw || patterns.PatternCount == 0) return;
        if (patterns.FacingMode != EnemyFacingMode.Rotation
            && (patterns.FacingMode != EnemyFacingMode.FollowPattern || patterns.FacingPatternIndex < 0))
            return;
        Color previous = Handles.color;
        var guide = EnemyShootingPreviewGeometry.GetFacingGuide(patterns, initial, firingBasis,
            rotationTime, rotationOverride, lockedAim, resolvedDirection);
        Handles.color = new Color(.35f, 1f, .45f, .95f);
        DrawDirection(position, guide.Direction, .5f, .12f, false);
        bool turning = Mathf.Abs(guide.TurnAngle) > .001f;
        if (turning)
        {
            float sign = Mathf.Sign(guide.TurnAngle);
            DrawTurnArrow(position, guide.Direction, sign * 70f);
            if (guide.PingPong)
            {
                Handles.color = new Color(.35f, 1f, .45f, .35f);
                DrawTurnArrow(position, guide.Direction, -sign * 70f);
            }
        }
        if (detailed)
        {
            string direction = turning ? guide.TurnAngle < 0f ? "CW" : "CCW" : "hold";
            Handles.color = new Color(.35f, 1f, .45f, .95f);
            Handles.Label(position + Vector3.down * .62f,
                $"Enemy Facing: {(guide.PingPong ? "Ping Pong · " : "")}{direction}");
        }
        Handles.color = previous;
    }

    private static void DrawTurnArrow(Vector3 position, Vector3 start, float angle)
    {
        const float radius = .42f;
        Handles.DrawWireArc(position, Vector3.forward, start, angle, radius);
        Vector3 end = Quaternion.Euler(0f, 0f, angle) * start;
        Vector3 tangent = new Vector3(-end.y, end.x, 0f) * Mathf.Sign(angle);
        Handles.ArrowHandleCap(0, position + end * radius,
            Quaternion.LookRotation(tangent, Vector3.forward), .12f, EventType.Repaint);
    }

    public static void DrawPatterns(EnemyBurstAttackSettings patterns, Vector3 position, Quaternion basis,
        float rotationTime, EnemyRotationSettings rotationOverride = null)
    {
        if (!CanDraw) return;
        for (int i = 0; i < patterns.PatternCount; i++)
            Draw(patterns.GetPattern(i).Shooting, position, basis, rotationTime, rotationOverride);
    }

    public static void DrawWaveSlot(DirectedEnemySubWave wave, int slot, Vector3 position,
        float enemyTime, float elapsed, bool staticPreview = false, bool detailed = true, int rayBudget = MaxDetailedRays,
        Vector3? previewTarget = null)
    {
        if (!CanDraw || wave == null) return;
        Enemy prefab = wave.GetConfiguredEnemyPrefabForSlot(slot);
        if (prefab == null || !prefab.TryGetComponent(out SimpleEnemyAttackExecutor executor))
            return;
        var pointOverride = wave.GetConfiguredEnemyOverrideForSlot(slot);
        EnemyBurstAttackSettings patterns = executor.BurstAttackSettings;
        EnemyRotationSettings rotationOverride = pointOverride != null ? pointOverride.RotationOverride : null;
        float rotationTime = enemyTime;
        var resolved = new DirectedWaveAttackSettings();
        bool hasResolvedSettings = false;
        float postStart = staticPreview ? 0f : wave.GetSimulationPreviewPostStartTime();
        var behaviour = wave.AttackPatternBehaviour;
        if (behaviour != null && behaviour.enabled)
        {
            bool entrance = !staticPreview && elapsed < postStart;
            hasResolvedSettings = behaviour.TryCopyResolvedAttackSettingsTo(
                slot,
                entrance,
                resolved);
            if (hasResolvedSettings && !resolved.UsesEnemyBurstSettings
                && (staticPreview || entrance || elapsed >= postStart + resolved.AttackStartDelay))
            {
                patterns = resolved.WaveBurstSettings;
                rotationOverride = null;
                rotationTime = staticPreview ? 0f : entrance ? enemyTime : Mathf.Max(0f, elapsed - postStart - resolved.AttackStartDelay);
            }
        }
        Vector3? requestedDirection = null;
        bool useAimedShotGunDirection = false;
        if (hasResolvedSettings)
        {
            if (!TryGetWaveSlotFireDirection(
                    wave, slot, resolved, position, prefab.transform.up,
                    out Vector3 direction, out useAimedShotGunDirection, previewTarget))
                return;
            requestedDirection = direction;
        }

        if (pointOverride != null && pointOverride.HasAttackPatternOverride
            && (hasResolvedSettings || pointOverride.UsesLegacyFullAttackSettings))
        {
            patterns = EnemyAttackSettingsResolver.CreateRuntimeCopy(
                patterns, pointOverride,
                hasResolvedSettings && resolved.HasRuntimeProjectileBaseSpeedOverride
                    ? resolved.RuntimeProjectileBaseSpeedOverride
                    : (float?)null);
        }
        else if (pointOverride != null && pointOverride.AttackSettingsOverride != null)
        {
            patterns = pointOverride.AttackSettingsOverride;
        }
        else if (hasResolvedSettings && resolved.HasRuntimeProjectileBaseSpeedOverride)
        {
            patterns = EnemyAttackSettingsResolver.CreateRuntimeCopy(
                patterns, null, resolved.RuntimeProjectileBaseSpeedOverride);
        }
        if (hasResolvedSettings && resolved.RuntimeAttackSpeedMultiplier != 1f)
        {
            patterns = EnemyAttackSettingsResolver.CreateRuntimeCopy(patterns, null, null);
            patterns.ScaleAttackTiming(resolved.RuntimeAttackSpeedMultiplier);
            if (rotationOverride != null)
            {
                var scaledRotation = new EnemyRotationSettings(); scaledRotation.CopyFrom(rotationOverride);
                scaledRotation.ScaleAttackTiming(resolved.RuntimeAttackSpeedMultiplier); rotationOverride = scaledRotation;
            }
        }
        if (hasResolvedSettings && resolved.RuntimeProjectileBaseSpeedRandom != null && resolved.RuntimeProjectileBaseSpeedRandom.enabled)
        {
            patterns = EnemyAttackSettingsResolver.CreateRuntimeCopy(patterns, pointOverride, resolved.RuntimeProjectileBaseSpeedOverride);
            EnemyAttackSettingsResolver.ApplyRuntimeSpeedRandomization(patterns, pointOverride, resolved);
        }
        var binding = GetBinding(executor);
        var pivot = binding.Pivot;
        var mode = pointOverride != null ? pointOverride.ResolveFireMode(resolved.FireMode) : resolved.FireMode;
        var fixedSettings = pointOverride != null ? pointOverride.ResolveStationaryFireSettings(resolved.StationaryFireSettings) : resolved.StationaryFireSettings;
        bool stationary = hasResolvedSettings && mode == DirectedWaveAttackFireMode.Stationary && !fixedSettings.PreservesLegacyForward;
        bool fixedPoint = stationary && fixedSettings.DirectionMode == EnemyStationaryDirectionMode.WorldPoint;
        if (stationary)
        {
            requestedDirection = fixedPoint ? (Vector3)fixedSettings.WorldPoint - position : fixedSettings.Direction;
            if (fixedPoint)
            {
                Handles.color = SectorColor;
                Handles.DrawWireDisc(fixedSettings.WorldPoint, Vector3.forward, .15f);
                Handles.Label(fixedSettings.WorldPoint, "Stationary World Point");
            }
        }
        Vector3? facingAim = null;
        uint numericOrdinal = 0;
        for (int i = 0; i < patterns.PatternCount; i++)
        {
            EnemyShootingSettings shooting = patterns.GetPattern(i).Shooting;
            if (shooting.UsesNumericRandomization || patterns.GetPattern(i).AttackCooldownRandom.enabled) numericOrdinal++;
            Quaternion basis = shooting.EmissionLayout.Type != EnemyEmissionLayoutType.Point && pivot != null
                ? pivot.rotation : prefab.transform.rotation;
            Vector3? patternDirection = requestedDirection;
            Vector3? capturedPosition = null;
            float capturedRotationTime = rotationTime;
            if (hasResolvedSettings && useAimedShotGunDirection)
            {
                DirectedWaveAttackAimMode aimMode = pointOverride != null
                    ? pointOverride.ResolveAimMode(resolved.AimMode) : resolved.AimMode;
                float phaseStart = elapsed < postStart ? elapsed - enemyTime
                    : postStart + resolved.AttackStartDelay;
                float captureTime = GetAimCaptureTime(patterns, i, aimMode,
                    Mathf.Max(0f, elapsed - phaseStart)) + phaseStart;
                capturedPosition = staticPreview ? position : GetCapturedAimOrigin(wave, slot, position, elapsed, captureTime);
                DirectedWaveAttackAimState aim = default;
                patternDirection = aim.ResolveDirection(capturedPosition.Value, previewTarget ?? ((IEnemyAttackAimTarget)wave).AttackTargetPosition, false);
                capturedRotationTime = staticPreview ? rotationTime : Mathf.Max(0f, rotationTime - (elapsed - captureTime));
                if (i == patterns.FacingPatternIndex)
                    facingAim = patternDirection;
            }
            if (stationary && i == patterns.FacingPatternIndex)
            {
                var frame = CreateFrame(executor, shooting, position, rotationTime, rotationOverride, out var fixedBasis);
                if (fixedPoint) frame.Target = fixedSettings.WorldPoint;
                frame.PlayerPosition = previewTarget ?? ((IEnemyAttackAimTarget)wave).AttackTargetPosition;
                facingAim = EnemyShootingPreviewGeometry.GetEmitter(shooting, 0, frame, fixedBasis, rotationTime,
                    rotationOverride, patternDirection, fixedPoint, true).BaseDirection;
            }
            DrawExecutorPattern(executor, shooting, position, rotationTime, rotationOverride,
                  patternDirection, useAimedShotGunDirection || fixedPoint,
                  fixedPoint ? (Vector3)fixedSettings.WorldPoint : useAimedShotGunDirection ? previewTarget ?? ((IEnemyAttackAimTarget)wave).AttackTargetPosition : (Vector3?)null,
                  capturedPosition, capturedRotationTime, stationary, detailed,
                  Mathf.Max(1, rayBudget / Mathf.Max(1, patterns.PatternCount)),
                  previewTarget ?? ((IEnemyAttackAimTarget)wave).AttackTargetPosition, slot, numericOrdinal);
        }
        int facingIndex = patterns.FacingPatternIndex;
        if (patterns.FacingMode == EnemyFacingMode.Rotation
            || patterns.FacingMode == EnemyFacingMode.FollowPattern && facingIndex >= 0)
        {
            EnemyShootingSettings shooting = patterns.GetPattern(facingIndex).Shooting;
            Quaternion basis = shooting.EmissionLayout.Type != EnemyEmissionLayoutType.Point && pivot != null
                ? pivot.rotation : prefab.transform.rotation;
            var facing = binding.Facing;
            DrawFacing(patterns, position, facing != null ? facing.rotation : prefab.transform.rotation,
                basis, rotationTime, rotationOverride, facingAim, stationary, detailed);
        }
    }

    private static bool TryGetWaveSlotFireDirection(
        DirectedEnemySubWave wave, int slot, DirectedWaveAttackSettings settings,
        Vector3 position, Vector3 forward, out Vector3 direction,
        out bool usesAim, Vector3? previewTarget = null)
    {
        DirectedWaveEnemyOverride pointOverride = wave.GetConfiguredEnemyOverrideForSlot(slot);
        DirectedWaveAttackFireMode mode = pointOverride != null
            ? pointOverride.ResolveFireMode(settings.FireMode)
            : settings.FireMode;
        usesAim = mode == DirectedWaveAttackFireMode.Aimed;
        direction = forward;
        if (mode == DirectedWaveAttackFireMode.Stationary)
        {
            var fixedSettings = pointOverride != null ? pointOverride.ResolveStationaryFireSettings(settings.StationaryFireSettings) : settings.StationaryFireSettings;
            if (!fixedSettings.PreservesLegacyForward)
                direction = fixedSettings.DirectionMode == EnemyStationaryDirectionMode.WorldPoint ? (Vector3)fixedSettings.WorldPoint - position : fixedSettings.Direction;
        }
        if (mode == DirectedWaveAttackFireMode.None)
            return false;

        Vector3 toPlayer = (previewTarget ?? ((IEnemyAttackAimTarget)wave).AttackTargetPosition) - position;
        if (usesAim)
            direction = toPlayer;
        if (mode != DirectedWaveAttackFireMode.ForwardWhenPlayerAhead)
            return true;

        float halfAngle = pointOverride != null
            ? pointOverride.ResolveForwardFireHalfAngle(settings.ForwardFireHalfAngle)
            : settings.ForwardFireHalfAngle;
        Vector2 planarForward = forward;
        Vector2 planarTarget = toPlayer;
        return planarTarget.sqrMagnitude < 0.0001f
            || planarForward.sqrMagnitude > 0.0001f
                && Vector2.Dot(planarForward.normalized, planarTarget.normalized)
                    >= Mathf.Cos(halfAngle * Mathf.Deg2Rad);
    }

    // Preview samples the route at the aim acquisition time, so a moving enemy
    // does not turn the shot direction between shots of the same burst.
    public static float GetAimCaptureTime(EnemyBurstAttackSettings patterns, int patternIndex,
        DirectedWaveAttackAimMode mode, float elapsed)
    {
        if (mode == DirectedWaveAttackAimMode.AimContinuous)
            return Mathf.Max(0f, elapsed);
        float duration = 0f;
        float firstShot = float.PositiveInfinity;
        for (int i = 0; i < patterns.PatternCount; i++)
        {
            EnemyBurstAttackSettings entry = patterns.GetPattern(i);
            float delay = patterns.HasMultiplePatterns ? entry.AttackStartDelay : 0f;
            firstShot = Mathf.Min(firstShot, delay);
            duration = Mathf.Max(duration, delay + entry.AttackDuration);
        }
        float cycleDuration = Mathf.Max(0.01f, duration + patterns.MaxAttackCooldown);
        float cycleStart = Mathf.Floor(Mathf.Max(0f, elapsed) / cycleDuration) * cycleDuration;
        EnemyBurstAttackSettings pattern = patterns.GetPattern(patternIndex);
        if (mode == DirectedWaveAttackAimMode.AimOnStart)
            return cycleStart + firstShot;
        float patternDelay = patterns.HasMultiplePatterns ? pattern.AttackStartDelay : 0f;
        if (!pattern.RepeatBurst)
            return cycleStart + patternDelay;
        float burstPeriod = (pattern.BurstShotCount - 1) * pattern.BurstShotInterval
            + pattern.AttackShotInterval;
        int burstIndex = burstPeriod > 0f
            ? Mathf.Clamp(Mathf.FloorToInt((elapsed - cycleStart - patternDelay) / burstPeriod),
                0, pattern.AttackShotCount - 1) : 0;
        return cycleStart + patternDelay + burstIndex * burstPeriod;
    }

    private static Vector3 GetCapturedAimOrigin(DirectedEnemySubWave wave, int slot,
        Vector3 position, float elapsed, float captureTime)
    {
        if (elapsed == captureTime) return position;
        Dictionary<int, Vector3> current, captured;
        if (sampledWave == wave && sampledElapsed == elapsed && sampledCurrentPositions != null)
        {
            current = sampledCurrentPositions;
            if (!captureSamples.TryGetValue(captureTime, out int sample))
            {
                sample = captureSamples.Count;
                if (sample == capturePositions.Count) capturePositions.Add(new Dictionary<int, Vector3>());
                captured = capturePositions[sample];
                wave.EvaluateSimulationPreviewNonAlloc(captureTime, null, sampledSpawnOrder, captured, out _);
                captureSamples.Add(captureTime, sample);
            }
            captured = capturePositions[sample];
        }
        else
        {
            current = wave.EvaluateSimulationPreview(elapsed);
            captured = wave.EvaluateSimulationPreview(captureTime);
        }
        Vector3 origin = position;
        if (current.TryGetValue(slot, out Vector3 currentOrigin)
            && captured.TryGetValue(slot, out Vector3 capturedOrigin))
            origin += capturedOrigin - currentOrigin;
        return origin;
    }
}
