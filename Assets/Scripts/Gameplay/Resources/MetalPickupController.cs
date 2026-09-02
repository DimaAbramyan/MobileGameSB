using System;
using System.Collections.Generic;
using UnityEngine;
using Zenject;

public sealed class MetalPickupController : IInitializable, ITickable, IDisposable
{
    private const float MinimumLaunchSpeed = 0.9f;
    private const float MaximumLaunchSpeed = 1.1f;
    private const float Gravity = 2f;
    private const float MinimumLaunchAngle = -10f;
    private const float MaximumLaunchAngle = 10f;
    private const float DespawnBelowCameraDistance = 2f;

    private readonly DiContainer container;
    private readonly List<TrackedPickup> activePickups = new();

    private Camera gameplayCamera;

    public MetalPickupController(DiContainer container)
    {
        this.container = container;
    }

    public void Initialize()
    {
    }

    public void Spawn(MetalPickup pickupPrefab, Vector3 position, int amount)
    {
        if (pickupPrefab == null || amount <= 0)
            return;

        for (int i = 0; i < amount; i++)
        {
            MetalPickup pickup = container.InstantiatePrefabForComponent<MetalPickup>(
                pickupPrefab,
                position,
                Quaternion.identity,
                null);
            if (pickup == null)
                continue;

            pickup.Configure(1);
            activePickups.Add(new TrackedPickup(
                pickup,
                GetLaunchVelocity()));
        }
    }

    public void Tick()
    {
        float deltaTime = Time.deltaTime;
        for (int i = activePickups.Count - 1; i >= 0; i--)
        {
            TrackedPickup trackedPickup = activePickups[i];
            if (trackedPickup.Pickup == null)
            {
                activePickups.RemoveAt(i);
                continue;
            }

            if (!trackedPickup.Pickup.IsMagneticallyAttracted)
            {
                trackedPickup.Velocity += Vector2.down * Gravity * deltaTime;
                trackedPickup.Pickup.transform.position +=
                    (Vector3)(trackedPickup.Velocity * deltaTime);
            }

            if (IsBelowCamera(trackedPickup.Pickup.transform.position))
            {
                UnityEngine.Object.Destroy(trackedPickup.Pickup.gameObject);
                activePickups.RemoveAt(i);
                continue;
            }

            activePickups[i] = trackedPickup;
        }
    }

    public void Dispose()
    {
        for (int i = 0; i < activePickups.Count; i++)
        {
            MetalPickup pickup = activePickups[i].Pickup;
            if (pickup != null)
                UnityEngine.Object.Destroy(pickup.gameObject);
        }

        activePickups.Clear();
    }

    private static Vector2 GetLaunchVelocity()
    {
        float angle = UnityEngine.Random.Range(
                MinimumLaunchAngle,
                MaximumLaunchAngle)
            * Mathf.Deg2Rad;
        float speed = UnityEngine.Random.Range(
            MinimumLaunchSpeed,
            MaximumLaunchSpeed);
        return new Vector2(Mathf.Sin(angle), Mathf.Cos(angle)) * speed;
    }

    private bool IsBelowCamera(Vector3 position)
    {
        if (gameplayCamera == null)
            gameplayCamera = Camera.main;

        if (gameplayCamera == null || !gameplayCamera.orthographic)
            return false;

        float lowestVisibleY = gameplayCamera.transform.position.y
            - gameplayCamera.orthographicSize;
        return position.y < lowestVisibleY - DespawnBelowCameraDistance;
    }

    private struct TrackedPickup
    {
        public readonly MetalPickup Pickup;
        public Vector2 Velocity;

        public TrackedPickup(MetalPickup pickup, Vector2 velocity)
        {
            Pickup = pickup;
            Velocity = velocity;
        }
    }
}
