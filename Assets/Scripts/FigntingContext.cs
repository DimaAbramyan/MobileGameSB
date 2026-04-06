using UnityEngine;
using Zenject;

public class FightingInstaller : MonoInstaller
{
    public override void InstallBindings()
    {
        Debug.Log("========== FightingInstaller InstallBindings ==========");

        // Регистрируем CreatePlayerShips (нужен для FMODAttenuationService)
        var createPlayerShips = FindObjectOfType<CreatePlayerShips>();
        if (createPlayerShips != null)
        {
            Container.Bind<CreatePlayerShips>()
                     .FromInstance(createPlayerShips)
                     .AsSingle();
            Debug.Log("✅ CreatePlayerShips registered");
        }
        else
        {
            Debug.LogError("❌ CreatePlayerShips not found in scene!");
        }

        // Регистрируем FMODAttenuationService
        Container.BindInterfacesTo<FMODAttenuationService>()
                 .AsSingle();
        Debug.Log("✅ FMODAttenuationService registered");

        Debug.Log("========== FightingInstaller Complete ==========");
    }
}