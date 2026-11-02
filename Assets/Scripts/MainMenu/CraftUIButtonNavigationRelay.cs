using UnityEngine;
using UnityEngine.EventSystems;

// The actual Selectable is a child of CraftUIButton in the existing prefabs.
// Selection events do not bubble to parents, unlike pointer enter/exit events.
public sealed class CraftUIButtonNavigationRelay : MonoBehaviour, ISelectHandler, IDeselectHandler
{
    [SerializeField] private CraftUIButton owner;
    public void OnSelect(BaseEventData eventData) { if (owner != null) owner.OnSelect(eventData); }
    public void OnDeselect(BaseEventData eventData) { if (owner != null) owner.OnDeselect(eventData); }
    private void OnDisable() { if (owner != null) owner.OnDeselect(null); }
}
