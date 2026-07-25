using UnityEngine;
using Zenject;

public class FightingInstaller : MonoInstaller
{
    public override void InstallBindings()
    {
        Container.Bind<CreatePlayerShips>()
            .FromComponentInHierarchy()
            .AsSingle();

        Container.BindInterfacesTo<FMODAttenuationService>()
            .AsSingle();

        Container.Bind<PlayerController>()
            .FromComponentInHierarchy()
            .AsSingle();

        Container.Bind<ProjectilePoolController>()
            .FromNewComponentOnNewGameObject()
            .WithGameObjectName("Projectile Pool")
            .AsSingle()
            .NonLazy();

        Container.Bind<WaveManager>()
            .FromComponentInHierarchy()
            .AsSingle();

        Container.Bind<ShipSelect>()
            .FromComponentInHierarchy()
            .AsSingle();

        Container.Bind<DealDamageManager>()
            .AsSingle()
            .IfNotBound();

        Container.Bind<ShipKnockbackService>()
            .AsSingle()
            .IfNotBound();

        Container.Bind<EnemyManager>().AsSingle();
    }
}
