using System.ComponentModel;
using UnityEngine;
using Zenject;

public class AudioInstaller : Installer<AudioInstaller>
{
    public override void InstallBindings()
    {
        Container.Bind<CreatePlayerShips>()
            .FromComponentInHierarchy()
            .AsSingle();

        Container.Bind<FMODAttenuationService>()
            .AsSingle()
            .NonLazy();
    }
}
