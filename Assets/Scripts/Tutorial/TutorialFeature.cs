#if UNITY_EDITOR || INCLUDE_TUTORIAL
using UnityEngine;
using Zenject;

namespace Game.Tutorial
{
    public static class TutorialFeature
    {
        public static void Install(DiContainer container)
        {
            TutorialConfiguration config = null;
#if UNITY_EDITOR
            UnityEditor.EditorBuildSettings.TryGetConfigObject(TutorialConfiguration.EditorConfigKey, out config);
#else
            // Configuration is an explicit build root, loaded by Unity before scene startup.
            var loaded = Resources.FindObjectsOfTypeAll<TutorialConfiguration>();
            if (loaded.Length == 1) config = loaded[0];
            else if (loaded.Length > 1) Debug.LogError("Multiple preloaded Tutorial configurations. Fix Project Settings > Game > Tutorial.");
#endif
            Install(container, config);
        }
        public static void Install(DiContainer container, TutorialConfiguration config)
        {
            if (config == null || !config.includeTutorial) return;
            if (config.definition == null || !config.definition.TryValidate(out _))
            { Debug.LogError("Tutorial is enabled but its Definition is missing or invalid."); return; }
            container.Bind<TutorialConfiguration>().FromInstance(config).AsSingle();
            container.Bind<ITutorialPresentation>().To<TutorialOverlay>().AsSingle();
            container.BindInterfacesAndSelfTo<TutorialService>().AsSingle().NonLazy();
        }
    }
}
#endif
