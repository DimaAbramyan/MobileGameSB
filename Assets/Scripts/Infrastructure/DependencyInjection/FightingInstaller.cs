using UnityEngine;
using Zenject;

public class FightingInstaller : MonoInstaller
{
    public override void InstallBindings()
    {
        Container.Bind<CreatePlayerShips>()
            .FromComponentInHierarchy()
            .AsSingle();

        Container.Bind<FMODUnity.StudioListener>()
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

        Container.Bind<BossProjectilePool>()
            .FromNewComponentOnNewGameObject()
            .WithGameObjectName("Boss Projectile Pool")
            .AsSingle()
            .NonLazy();

        Container.BindInterfacesAndSelfTo<EnemyProjectileEcsSpawner>()
            .AsSingle()
            .NonLazy();

        Container.BindInterfacesAndSelfTo<PlayerProjectileEcsSpawner>()
            .AsSingle()
            .NonLazy();

        Container.Bind<EnemyProjectileCollisionRegistry>()
            .FromNewComponentOnNewGameObject()
            .WithGameObjectName("Enemy Projectile Collision Registry")
            .AsSingle()
            .NonLazy();

        Container.Bind<PlayerProjectileCollisionRegistry>()
            .FromNewComponentOnNewGameObject()
            .WithGameObjectName("Player Projectile Collision Registry")
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

        Container.Bind<DamageNumberController>()
            .FromComponentInHierarchy()
            .AsSingle()
            .NonLazy();

        Container.Bind<BuffPickupMessageController>()
            .FromComponentInHierarchy()
            .AsCached()
            .NonLazy();

        Container.Bind<ShipKnockbackService>()
            .AsSingle()
            .IfNotBound();

        Container.Bind<EnemyManager>().AsSingle();

        Container.BindInterfacesAndSelfTo<MetalPickupController>()
            .AsSingle()
            .NonLazy();

        Container.BindInterfacesAndSelfTo<EnemyHeatSystem>()
            .AsSingle()
            .NonLazy();

        Container.BindInterfacesAndSelfTo<EnemyTemperatureController>()
            .AsSingle()
            .NonLazy();

          Container.BindInterfacesAndSelfTo<EnemyDisintegrationSystem>()
              .AsSingle()
              .NonLazy();

          Container.BindInterfacesAndSelfTo<EnemyPeriodicDamageSystem>()
              .AsSingle()
              .NonLazy();

          Container.BindInterfacesAndSelfTo<EnemyDebuffController>()
            .AsSingle()
            .NonLazy();
    }
}
