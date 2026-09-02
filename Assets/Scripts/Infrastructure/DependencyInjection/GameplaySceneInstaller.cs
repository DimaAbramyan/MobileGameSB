using System.Collections;
using System.Collections.Generic;

using UnityEngine;
using Zenject;

public class GameplaySceneInstaller : MonoInstaller
{
    public PanelManager tabsManager;

    public override void InstallBindings()
    {
        Container.Bind<PanelManager>().FromComponentInHierarchy().AsSingle();
        Container.Bind<Save>().FromComponentsInHierarchy().AsCached();
    }
}
