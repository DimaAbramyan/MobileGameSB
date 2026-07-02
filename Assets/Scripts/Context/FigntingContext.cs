using UnityEngine;
using Zenject;

public class FightingInstaller : MonoInstaller
{
    [SerializeField]
    ProjectilePoolController projectilePoolController;
    [SerializeField]
    PlayerLevelVisualController playerLevelVisualController;
    [SerializeField]
    PlayerController playerController;
    public override void InstallBindings()
    {
        Container.Bind<CreatePlayerShips>()
            .FromComponentInHierarchy()
            .AsSingle();

        Container.BindInterfacesTo<FMODAttenuationService>()
            .AsSingle();

        Container.Bind<PlayerLevelVisualController>()
            .FromComponentInHierarchy()
            .AsSingle();

        Container.Bind<PlayerController>()
            .FromComponentInHierarchy()
            .AsSingle();

        Container.Bind<ShipSelect>()
            .FromComponentInHierarchy()
            .AsSingle();

        Container.Bind<DealDamageManager>()
            .AsSingle()
            .IfNotBound();

        Container.Bind<EnemyManager>().AsSingle();
    }
}
