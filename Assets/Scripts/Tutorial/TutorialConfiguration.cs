#if UNITY_EDITOR || INCLUDE_TUTORIAL
using TMPro;
using UnityEngine;
using UnityEngine.Localization;

namespace Game.Tutorial
{
    public enum TutorialAutoStart { MainMenuOrBattle, MainMenu, Battle, Manual }

    [CreateAssetMenu(menuName = "Game/Tutorial/Configuration", fileName = "TutorialConfiguration")]
    public sealed class TutorialConfiguration : ScriptableObject
    {
        public const string EditorConfigKey = "Game.Tutorial.Configuration";
        public bool includeTutorial = true;
        public TutorialDefinition definition;
        public TutorialAutoStart autoStart;
        public bool restartOnNewVersion;
        public bool allowDevelopmentLaunch = true;
        public bool forceStartInDevelopment;
        public TMP_FontAsset font;
        public LocalizedString continueLabel = new("UI", "tutorial.continue");
        public LocalizedString skipLabel = new("UI", "tutorial.skip");
        public LocalizedString skipStepLabel = new("UI", "tutorial.skip_step");
    }
}
#endif
