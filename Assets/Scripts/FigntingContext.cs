using System.ComponentModel;
using UnityEngine;
using Zenject;

public class FightingInstaller : MonoInstaller
{
    [SerializeField] private CreatePlayerShips shipLoader;
    [SerializeField] private FMODUnity.StudioListener listener;

    public override void InstallBindings()
    {
        Container.Bind<CreatePlayerShips>().FromInstance(shipLoader).AsSingle();
        Camera cam = DontDestroy.instance.GetComponentInChildren<Camera>();
        FMODUnity.StudioListener listener = cam.GetComponent<FMODUnity.StudioListener>();
        Container.BindInterfacesTo<FMODAttenuationService>()
                .AsSingle()
                .WithArguments(listener);
    }
}
