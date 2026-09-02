using UnityEngine;
using Zenject;


public class ProjectManagerInstaller : MonoInstaller
{
    [SerializeField] private AudioDatabase audioDatabase;
    [SerializeField] private LevelCatalog levelCatalog;
    [SerializeField] private HullCatalog hullCatalog;
    [SerializeField] private WeaponCatalog weaponCatalog;
    [SerializeField] private AudioServiceHost audioHostPrefab;
    [SerializeField] private AudioVolumeSettings audioVolumeSettings = new();

    public override void InstallBindings()
    {
        Container.Bind<GameSettings>()
                 .AsSingle();

        Container.Bind<TeamSave>()
                 .AsSingle();

        Container.Bind<LevelCatalog>()
                 .FromInstance(levelCatalog)
                 .AsSingle();

        Container.Bind<HullCatalog>()
                 .FromInstance(hullCatalog)
                 .AsSingle();

        Container.Bind<WeaponCatalog>()
                 .FromInstance(weaponCatalog)
                 .AsSingle();

        Container.Bind<ContentCatalogService>()
                 .AsSingle();

        Container.Bind<LevelProgressService>()
                 .AsSingle();

        Container.Bind<PlayerResourceWallet>()
                 .AsSingle();

        Container.Bind<ContentProgressService>()
                 .AsSingle();

        Container.Bind<SaveManager>()
                 .AsSingle();

        Container.Bind<TeamSelectionService>()
                 .AsSingle();

        Container.Bind<BattleLaunchService>()
                 .AsSingle();

        GameObject[] ships = Resources.LoadAll<GameObject>("UI/Ships");
        GameObject[] weapons = Resources.LoadAll<GameObject>("UI/Weapons");
        Container.Bind<PrefabFactory>()
                 .AsSingle()
                 .WithArguments(ships, weapons);

        Container.Bind<AudioDatabase>()
                 .FromInstance(audioDatabase)
                 .AsSingle();

        audioVolumeSettings ??= new AudioVolumeSettings();
        audioVolumeSettings.Validate();
        Container.Bind<AudioVolumeSettings>()
                 .FromInstance(audioVolumeSettings)
                 .AsSingle();
        Container.BindInterfacesAndSelfTo<AudioVolumeService>()
                 .AsSingle()
                 .NonLazy();

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

    }

    private void OnValidate()
    {
        audioVolumeSettings ??= new AudioVolumeSettings();
        audioVolumeSettings.Validate();
    }
}
