#if UNITY_EDITOR || INCLUDE_TUTORIAL
using UnityEngine;
using Zenject;

namespace Game.Tutorial
{
    // Optional explicit reference for HUD or world highlights; never searches the scene.
    public sealed class TutorialAnchor : MonoBehaviour
    {
        [SerializeField] private string anchorId;
        private ITutorialService tutorial;
        [Inject] public void Construct([InjectOptional] ITutorialService service)
        {
            tutorial = service;
            if (isActiveAndEnabled) tutorial?.RegisterAnchor(anchorId, transform);
        }
        private void OnEnable() => tutorial?.RegisterAnchor(anchorId, transform);
        private void OnDisable() => tutorial?.UnregisterAnchor(anchorId, transform);
    }
}
#endif
