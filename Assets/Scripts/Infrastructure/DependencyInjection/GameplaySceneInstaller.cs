using System.Collections;
using System.Collections.Generic;

using UnityEngine;
using Zenject;

public class GameplaySceneInstaller : MonoInstaller
{
    public PanelManager tabsManager;
    public SaveManager saveManager;
    public PrefabFactory prefabFactory;

    public override void InstallBindings()
    {
        Container.Bind<PanelManager>().FromComponentInHierarchy().AsSingle();
        Container.Bind<Save>().FromComponentsInHierarchy().AsCached();
        Container.Bind<SaveManager>().AsSingle();

        GameObject[] ships = Resources.LoadAll<GameObject>("UI/Ships");
        GameObject[] weapons = Resources.LoadAll<GameObject>("UI/Weapons");
        Debug.Log(weapons.Length);
        Debug.Log(ships.Length);
        Container.Bind<PrefabFactory>()
            .AsSingle()
            .WithArguments(ships, weapons);
    }
}
