using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// A one-source editor preview: runtime schedules, emission geometry, numeric streams,
// Sector order and the same ECS projectile/child simulation as Preview Wave.
public sealed class EnemyPresetPreviewSession : IDisposable
{
    private sealed class Volley
    {
        public EnemyShootingSettings Shooting;
        public float Start, Speed, Spread;
        public int Next;
        public EnemyEmissionPreviewFrame Frame;
        public EnemyRandomSpeedSample Numeric;
        public EnemySectorLaunchTicket Order;
    }
    private readonly EnemyProjectilePreviewWorld world = new();
    private readonly EnemyBurstAttackSettings profile;
    private readonly EnemyShootingSettings shooting;
    private readonly EnemyBullet prefab;
    private readonly EnemyProjectilePrefabSet prefabs;
    private readonly GameObject source;
    private readonly Scene previewScene;
    private readonly GameObject cameraObject;
    private readonly Camera camera;
    private readonly System.Random random;
    private readonly EnemyAttackNumericRegistry numbers = new();
    private readonly EnemySectorLaunchOrderRegistry orders = new();
    private readonly DirectedWaveAttackSettings fireSettings = new();
    private readonly List<Volley> volleys = new();
    private readonly List<EnemyProjectileSpawnRequest> requests = new();
    private EnemyAttackPatternSchedule schedule;
    private EnemyEmissionOriginCaptureState origin;
    private EnemyProjectileInitialSpeedState speed;
    private EnemyAttackNumericState numeric;
    private readonly int revision;
    private float time;
    private bool started, disposed;
    private bool hasAim;
    private EnemyEmissionPreviewFrame capturedAim;
    private long omitted;
    private Vector3 target = new(0f, -3f, 0f);
    private readonly Quaternion basis = Quaternion.Euler(0f, 0f, 180f);
    public bool IsCurrent => revision == EnemyPresetRevision.Value;
    public int Count => disposed ? 0 : world.Count;
    public long Omitted => omitted + (disposed ? 0 : world.Omitted);
    public string Warning { get; private set; }
    public EnemyProjectilePreviewWorld World => world;

    public EnemyPresetPreviewSession(EnemyAttackPatternPreset pattern, EnemyProjectilePreset projectile, EnemyBullet fallback, int seed)
    {
        previewScene = EditorSceneManager.NewPreviewScene();
        source = new GameObject("Preset preview origin") { hideFlags = HideFlags.HideAndDontSave };
        SceneManager.MoveGameObjectToScene(source, previewScene);
        source.transform.position = new Vector3(0f, 2f, 0f);
        camera = Camera.main;
        if (camera == null)
        {
            cameraObject = new GameObject("Preset preview gameplay viewport") { hideFlags = HideFlags.HideAndDontSave };
            SceneManager.MoveGameObjectToScene(cameraObject, previewScene);
            camera = cameraObject.AddComponent<Camera>(); camera.enabled = false;
            camera.orthographic = true; camera.orthographicSize = 8f; camera.aspect = 9f / 16f;
            camera.transform.position = new Vector3(0f, 0f, -10f);
        }
        profile = new EnemyBurstAttackSettings(); profile.Validate();
        if (pattern != null) profile.ApplyPatternPreset(pattern, EnemyPresetUseMode.Linked);
        if (projectile != null) profile.AuthoredShooting.LinkProjectile(projectile);
        profile = profile.ResolvePresets(); shooting = profile.Shooting;
        prefab = shooting.ResolveProjectilePrefab(fallback);
        prefabs = new EnemyProjectilePrefabSet(fallback, fallback, fallback);
        if (prefab == null && shooting.ProjectileType != EnemyProjectileType.Laser) Warning = "Select a projectile prefab in the preset or the preview fallback field.";
        random = new System.Random(seed); numbers.Configure(0, seed);
        revision = EnemyPresetRevision.Value;
    }
    public void Advance(float end)
    {
        if (disposed || Warning != null) return;
        if (!started) { BeginAttack(0f); Step(0f); started = true; }
        end = Mathf.Max(time, end);
        int work = 0;
        while (time + .000001f < end && work++ < 300)
        {
            float delta = Mathf.Min(1f / 60f, end - time); time += delta;
            Step(delta);
        }
        world.CaptureRenderState();
    }
    private void BeginAttack(float now)
    {
        numeric = numbers.CreateDetached(profile, true); numeric.BeginSpeed();
        origin.Begin(shooting.EmissionOrigin, target, numeric);
        speed = default; orders.BeginAttack(shooting.EmissionLayout);
        hasAim = false;
        schedule.Begin(profile, now, 1f, true, numeric);
    }
    private void Step(float delta)
    {
        if (schedule.IsComplete && time >= schedule.NextAttackTime && volleys.Count == 0) BeginAttack(time);
        int events = 0;
        while (schedule.IsDue(time) && events++ < 64)
        {
            Queue(schedule.IsBurstStart);
            schedule.Advance(profile, time, 1f, schedule.IsLastShot ? numeric.SampleCooldown(profile.AttackCooldown, profile.AttackCooldownRandom) : (float?)null);
        }
        requests.Clear();
        for (int v = volleys.Count - 1; v >= 0; v--)
        {
            var volley = volleys[v];
            var shooting = volley.Shooting;
            int count = Mathf.Min(512, shooting.DirectionCount);
            while (volley.Next < count && time + .000001f >= volley.Start + shooting.EmissionLayout.GetLaunchDelayForOrder(volley.Next))
            {
                int arm = volley.Order.GetLaunchIndex(shooting.EmissionLayout, volley.Next++);
                var directionShooting = shooting.SampleGeometry(volley.Numeric.Numeric.ForDirection(arm), true);
                var firing = shooting.PresetFiring;
                bool aimed = firing.enabled && firing.fireMode == DirectedWaveAttackFireMode.Aimed;
                bool stationary = firing.enabled && firing.fireMode == DirectedWaveAttackFireMode.Stationary;
                var frame = volley.Frame;
                Vector3 direction = aimed ? target - frame.Origin.GetValueOrDefault() : Vector3.down;
                if (stationary)
                {
                    bool point = firing.stationaryFireSettings.DirectionMode == EnemyStationaryDirectionMode.WorldPoint;
                    direction = point ? (Vector3)firing.stationaryFireSettings.WorldPoint - frame.Origin.GetValueOrDefault() : firing.stationaryFireSettings.Direction;
                    if (point) frame.Target = firing.stationaryFireSettings.WorldPoint;
                }
                var emitter = EnemyShootingPreviewGeometry.GetEmitter(directionShooting, arm, frame, basis, time, requestedDirection: direction, aimed: aimed, stationary: stationary);
                int remaining = Mathf.Max(0, 512 - requests.Count);
                EnemyAttackProjectileBuilder.AppendEmitter(requests, prefab, directionShooting, emitter.Position, frame.Origin.GetValueOrDefault(), emitter.BaseDirection,
                    1f, volley.Speed, volley.Spread, prefabs, _ => Mathf.Lerp(shooting.RandomMinAngle, shooting.RandomMaxAngle, (float)random.NextDouble()),
                    maximum: remaining, randomSpeed: volley.Numeric, projectileOffset: arm * shooting.ProjectilesPerDirection,
                    directionIndex: arm, targetPoint: shooting.UsesTargetPoint ? target : null,
                    speedSampler: s => Mathf.Lerp(s.RandomInitialSpeedMinimum, s.RandomInitialSpeedMaximum, (float)random.NextDouble()),
                    laserOrigin: new EnemyLaserOrigin(source.transform, source.transform.position, shooting.EmissionOrigin, camera));
                omitted += Mathf.Max(0, shooting.ProjectilesPerDirection - remaining);
            }
            if (volley.Next >= count) { volley.Order.Complete(); volleys.RemoveAt(v); }
        }
        world.Spawn(requests); world.Step(time, delta, target, camera);
    }
    private void Queue(bool burstStart)
    {
        var sample = numeric.PrepareSpeed(this.shooting, burstStart && profile.RepeatBurst, speed.ConsistentDelta);
        var shooting = this.shooting.SampleGeometry(sample.Numeric);
        var player = origin.Prepare(shooting.EmissionOrigin, target, burstStart, burstStart && profile.RepeatBurst);
        EnemyEmissionOriginSettings.TryGetGameplayBounds(camera, 0f, out var bounds);
        Vector3 position = shooting.EmissionOrigin.Resolve(source.transform.position, player, bounds, origin.RandomOffsets.GetValueOrDefault());
        var frame = new EnemyEmissionPreviewFrame { EnemyPosition = source.transform.position, ShootingPoint = source.transform.position,
            Scale = Vector3.one, EnemyBasis = basis, PointBasis = basis, PlayerPosition = player, GameplayBounds = bounds, Origin = position };
        var firing = shooting.PresetFiring;
        if (firing.enabled && firing.fireMode == DirectedWaveAttackFireMode.None) return;
        if (firing.enabled && firing.fireMode == DirectedWaveAttackFireMode.ForwardWhenPlayerAhead
            && !fireSettings.IsPlayerInForwardFireSector(source.transform.position, Vector2.down, target, firing.forwardFireHalfAngle)) return;
        if (firing.enabled && firing.fireMode == DirectedWaveAttackFireMode.Aimed)
        {
            bool continuous = firing.aimMode == DirectedWaveAttackAimMode.AimContinuous;
            if (!hasAim || continuous || firing.aimMode == DirectedWaveAttackAimMode.AimOnBurst && profile.RepeatBurst && burstStart)
            { capturedAim = frame; capturedAim.CapturedRotationTime = time; hasAim = true; }
            frame.Target = target; frame.HasCapturedAim = !continuous;
            frame.CapturedEnemyPosition = capturedAim.EnemyPosition; frame.CapturedShootingPoint = capturedAim.ShootingPoint;
            frame.CapturedOrigin = capturedAim.Origin; frame.CapturedRotationTime = capturedAim.CapturedRotationTime;
        }
        var authoring = prefab != null ? prefab.GetComponent<EnemyProjectileAuthoring>() : null;
        volleys.Add(new Volley { Start = time, Frame = frame, Shooting = shooting, Speed = speed.GetVolleySpeed(shooting, authoring != null ? authoring.BaseSpeed : 1f),
            Spread = shooting.ShootingMode == EnemyProjectileFireMode.Random ? shooting.GenerateRandomSpreadAngle(random) : 0f, Order = orders.Reserve(shooting.EmissionLayout),
            Numeric = sample });
        speed.AdvanceAfterVolley(shooting);
    }
    public void Draw(Camera view)
    {
        if (disposed || view == null) return;
        world.RegisterCamera(view); world.Draw(view);
        Handles.color = Color.cyan; Handles.DrawWireDisc(source.transform.position, Vector3.forward, .15f);
        if (EnemyEmissionOriginSettings.TryGetGameplayBounds(camera, 0f, out var bounds))
            Handles.DrawWireCube(bounds.center, new Vector3(bounds.width, bounds.height, 0f));
    }
    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        foreach (var volley in volleys) volley.Order.Cancel();
        volleys.Clear(); world.Dispose();
        if (source != null) UnityEngine.Object.DestroyImmediate(source);
        if (cameraObject != null) UnityEngine.Object.DestroyImmediate(cameraObject);
        if (previewScene.IsValid()) EditorSceneManager.ClosePreviewScene(previewScene);
    }
}
