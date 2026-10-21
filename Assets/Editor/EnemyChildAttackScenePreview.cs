using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// The executor's Layout Preview Time displays one initial volley and its complete descendant simulation.
public sealed class EnemyChildAttackScenePreview : IDisposable
{
    private EnemyProjectilePreviewWorld world;
    private string signature;
    private float previewTime = -1f;
    public int Count => world?.Count ?? 0;
    public long Omitted => world?.Omitted ?? 0;
    public void SetVisible(bool value) => world?.SetVisible(value);

    public void Simulate(SimpleEnemyAttackExecutor executor, float time, Vector3 target)
    {
        string key = NumericRandomPreviewControls.Revision + ":" + EnemyPresetRevision.Value + EditorJsonUtility.ToJson(executor) + target.ToString("R") + executor.transform.localToWorldMatrix.ToString("R");
        var profiles = executor.BurstAttackSettings;
        for (int i = 0; i < profiles.PatternCount; i++)
        {
            var frame = EnemyShootingPreview.CreateFrame(executor, profiles.GetPattern(i).Shooting, executor.transform.position, 0f, null, out var basis);
            key += frame.ShootingPoint.ToString("R") + basis.ToString("R");
        }
        if (Camera.main != null) key += Camera.main.projectionMatrix.ToString("R") + Camera.main.transform.localToWorldMatrix.ToString("R") + Camera.main.rect;
        if (world != null && signature == key && Mathf.Approximately(previewTime, time)) return;
        Dispose(); signature = key; previewTime = time;
        world = new EnemyProjectilePreviewWorld();
        var serialized = new SerializedObject(executor);
        var prefabs = new EnemyProjectilePrefabSet(serialized.FindProperty("defaultProjectilePrefab").objectReferenceValue as EnemyBullet,
            serialized.FindProperty("homingProjectilePrefab").objectReferenceValue as EnemyBullet,
            serialized.FindProperty("burstAtPointProjectilePrefab").objectReferenceValue as EnemyBullet);
        var profile = executor.BurstAttackSettings;
        var next = new int[profile.PatternCount];
        var speeds = new float[profile.PatternCount];
        var numeric = new EnemyRandomSpeedSample[profile.PatternCount];
        var geometries = new EnemyShootingSettings[profile.PatternCount];
        var registry = new EnemyAttackNumericRegistry(); registry.Configure(0, NumericRandomPreviewControls.SeedOffset);
        var requests = new List<EnemyProjectileSpawnRequest>(512);
        for (int p = 0; p < profile.PatternCount; p++)
        {
            var shooting = profile.GetPattern(p).Shooting;
            var numbers = registry.Get(profile.GetPattern(p)); numbers?.BeginSpeed();
            numeric[p] = numbers?.PrepareSpeed(shooting, true, 0f) ?? default;
            shooting = geometries[p] = shooting.SampleGeometry(numeric[p].Numeric);
            var prefab = shooting.ResolveProjectilePrefab(prefabs.Get(shooting.ProjectileType));
            if (prefab == null && shooting.ProjectileType != EnemyProjectileType.Laser) continue;
            var state = new EnemyProjectileInitialSpeedState();
            speeds[p] = state.GetVolleySpeed(shooting, prefab != null ? prefab.GetComponent<EnemyProjectileAuthoring>().BaseSpeed : 1f);
        }
        float current = 0f;
        while (true)
        {
            requests.Clear();
            for (int p = 0; p < profile.PatternCount; p++)
            {
                var shooting = geometries[p];
                var prefab = shooting.ResolveProjectilePrefab(prefabs.Get(shooting.ProjectileType));
                if (prefab == null && shooting.ProjectileType != EnemyProjectileType.Laser) continue;
                int directions = Mathf.Min(64, shooting.DirectionCount);
                var frame = EnemyShootingPreview.CreateFrame(executor, shooting, executor.transform.position, current, null, out var basis);
                frame.PlayerPosition = target;
                while (next[p] < directions)
                {
                    int order = next[p];
                    int arm = shooting.EmissionLayout.IsSequential ? shooting.EmissionLayout.GetLaunchIndex(order) : order;
                    var directionShooting = shooting.SampleGeometry(numeric[p].Numeric.ForDirection(arm), true);
                    var emitter = EnemyShootingPreviewGeometry.GetEmitter(directionShooting, arm, frame, basis, current);
                    if (current + .00001f < emitter.LaunchDelay) break;
                    next[p]++;
                    EnemyAttackProjectileBuilder.AppendEmitter(requests, prefab, directionShooting, emitter.Position,
                        EnemyShootingPreviewGeometry.ResolveOrigin(shooting, frame), emitter.BaseDirection, 1f, speeds[p],
                        shooting.RandomPreviewAngle, prefabs, maximum: Mathf.Max(0, 512 - requests.Count),
                        randomSpeed: numeric[p], directionIndex: arm, projectileOffset: arm * directionShooting.ProjectilesPerDirection,
                        laserOrigin: new EnemyLaserOrigin(executor.transform, executor.transform.position, shooting.EmissionOrigin, Camera.main));
                }
            }
            world.Spawn(requests);
            if (current >= time - .00001f)
            {
                // Trigger zero-time child attacks at time zero without advancing flight.
                if (time <= .00001f) world.Step(0f, 0f, target);
                break;
            }
            float delta = Mathf.Min(.05f, time - current);
            current += delta;
            world.Step(current, delta, target);
        }
        world.CaptureRenderState();
    }

    public void Draw(Camera camera)
    {
        if (world == null || camera == null) return;
        world.RegisterCamera(camera);
        world.Draw(camera);
    }
    public void Dispose() { world?.Dispose(); world = null; }
}
