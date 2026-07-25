using System;
using UnityEngine;
using Zenject;

public class FMODAttenuationService : IInitializable, IDisposable
{
    private readonly LazyInject<CreatePlayerShips> _playerSpawner;
    private readonly FMODUnity.StudioListener _listener;

    public FMODAttenuationService(
        LazyInject<CreatePlayerShips> playerSpawner,
        [InjectOptional] FMODUnity.StudioListener listener = null)
    {
        _playerSpawner = playerSpawner;
        _listener = listener;
    }

    public void Initialize()
    {
        _playerSpawner.Value.OnPlayerSpawned += OnPlayerSpawned;
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
