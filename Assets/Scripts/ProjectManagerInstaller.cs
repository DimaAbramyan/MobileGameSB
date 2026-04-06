using UnityEngine;
using Zenject;

public class ProjectManagerInstaller : MonoInstaller
{
    [SerializeField] private AudioDatabase audioDatabase;
    [SerializeField] private AudioServiceHost audioHostPrefab;
    [SerializeField] private ImpactBehaviorSO[] impactBehaviours;

    public override void InstallBindings()
    {
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

        foreach (var impact in impactBehaviours)
            Container.Inject(impact);
    }
}