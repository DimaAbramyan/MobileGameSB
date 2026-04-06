using System;
using UnityEngine;
using Zenject;

public class FMODAttenuationService : IInitializable, IDisposable
{
    private readonly LazyInject<CreatePlayerShips> _playerSpawner;
    private FMODUnity.StudioListener _listener;

    public FMODAttenuationService(
        LazyInject<CreatePlayerShips> playerSpawner)
    {
        _playerSpawner = playerSpawner;
    }

    public void Initialize()
    {
        // Находим StudioListener в сцене
        _listener = GameObject.FindObjectOfType<FMODUnity.StudioListener>();

        if (_listener == null)
        {
            Debug.LogError("❌ StudioListener not found in scene! Please add a GameObject with StudioListener component.");
            return;
        }

        _playerSpawner.Value.OnPlayerSpawned += OnPlayerSpawned;
        Debug.Log("✅ FMODAttenuationService initialized with StudioListener");
    }

    private void OnPlayerSpawned(PlayerController player)
    {
        if (_listener != null)
        {
            _listener.attenuationObject = player.gameObject;
        }
    }

    public void Dispose()
    {
        if (_playerSpawner.Value != null)
        {
            _playerSpawner.Value.OnPlayerSpawned -= OnPlayerSpawned;
        }
    }
}