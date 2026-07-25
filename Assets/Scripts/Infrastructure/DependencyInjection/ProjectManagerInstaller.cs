using UnityEngine;
using Zenject;


public class ProjectManagerInstaller : MonoInstaller
{
    [SerializeField] private AudioDatabase audioDatabase;
    [SerializeField] private LevelCatalog levelCatalog;
    [SerializeField] private AudioServiceHost audioHostPrefab;

    public override void InstallBindings()
    {
        Container.Bind<GameSettings>()
                 .AsSingle();

        Container.Bind<TeamSave>()
                 .AsSingle();

        Container.Bind<LevelCatalog>()
                 .FromInstance(levelCatalog)
                 .AsSingle();

        Container.Bind<LevelProgressService>()
                 .AsSingle();

        Container.Bind<PlayerResourceWallet>()
                 .AsSingle();

        Container.Bind<AudioDatabase>()
                 .FromInstance(audioDatabase)
                 .AsSingle();

        if (audioHostPrefab != null)
        {
            Container.Bind<AudioServiceHost>()
                     .FromComponentInNewPrefab(audioHostPrefab)
                     .AsSingle()
                     .NonLazy();
        }

        Container.Bind<SoundManager>()
                 .AsSingle()
                 .NonLazy();

    }
}
