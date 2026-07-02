using Zenject;

public class FightSceneInstaller : MonoInstaller
{
    public override void InstallBindings()
    {
        Container.Bind<DealDamageManager>()
            .AsSingle()
            .IfNotBound();
    }
}
