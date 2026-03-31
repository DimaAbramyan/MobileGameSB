using System;
using UnityEngine;
using Zenject;

public class FMODAttenuationService : IInitializable, IDisposable
{
    private readonly CreatePlayerShips playerSpawner;
    private readonly FMODUnity.StudioListener listener;

    private Transform playerTransform;

    public FMODAttenuationService(
        CreatePlayerShips playerSpawner,
        FMODUnity.StudioListener listener)
    {
        this.playerSpawner = playerSpawner;
        this.listener = listener;
    }

    public void Initialize()
    {
        playerSpawner.OnPlayerSpawned += OnPlayerSpawned;
    }

    private void OnPlayerSpawned(PlayerController player)
    {
        listener.attenuationObject = player.gameObject;
    }

    public void Dispose()
    {
        listener.attenuationObject = null;
        playerSpawner.OnPlayerSpawned -= OnPlayerSpawned;
    }
}