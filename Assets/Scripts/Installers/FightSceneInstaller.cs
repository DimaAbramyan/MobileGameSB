using System.ComponentModel;
using UnityEngine;
using Zenject;

public class FightSceneInstaller : MonoInstaller
{
    [SerializeField] private DealDamageManager dealDamageManager;

    public override void InstallBindings()
    {
        Container.Bind<DealDamageManager>()
            .FromInstance(dealDamageManager)
            .AsSingle();
    }
}
