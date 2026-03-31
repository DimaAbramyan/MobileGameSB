using Zenject;
using UnityEngine;
using FMODUnity;

public class ProjectManagerInstaller : MonoInstaller
{
    [SerializeField] private AudioDatabase audioDatabase;
    [SerializeField] private AudioServiceHost audioHostPrefab;

    public override void InstallBindings()
    {
        Container.Bind<AudioDatabase>()
                 .FromInstance(audioDatabase)
                 .AsSingle();

        Container.Bind<AudioServiceHost>()
                 .FromComponentInNewPrefab(audioHostPrefab)
                 .AsSingle()
                 .NonLazy();

        Container.Bind<AudioManager>()
                 .AsSingle();

        Container.BindInterfacesTo<FMODAttenuationService>()
                 .AsSingle();
        Debug.Log(audioDatabase);
        Debug.Log(audioHostPrefab);
    }
}