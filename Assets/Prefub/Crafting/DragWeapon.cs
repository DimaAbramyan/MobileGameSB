using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class DragWeapon : MonoBehaviour, IDragHandler, IBeginDragHandler, IEndDragHandler //сам объект
{
    [SerializeField] private RectTransform rectTransform;
    public Transform parentAfterDrag;
    private CanvasGroup canvasGroup;
    private void Start()
    {
        rectTransform = GetComponent<RectTransform>();
        canvasGroup = GetComponent<CanvasGroup>();
    }
    public void OnBeginDrag(PointerEventData eventData) //Начали нести
    {
      //  Instantiate(gameObject);
        canvasGroup.alpha = 0.6f;
        parentAfterDrag = transform.parent;
       // Debug.Log("Начали нести");
        transform.SetParent(transform.root);
       // Debug.Log(transform.root);
    }
public void OnDrag(PointerEventData eventData)
{
        rectTransform.position = eventData.position;

        canvasGroup.blocksRaycasts = false;
}
    public void OnEndDrag(PointerEventData eventData)
    {
        canvasGroup.alpha = 1f;
        canvasGroup.blocksRaycasts = true;

        GameObject target = eventData.pointerEnter;

        if (target != null)
        {
            // Ищем слот корабля выше по иерархии
            var slot = target.GetComponentInParent<SetVeapon>();

            if (slot != null)
            {
                Debug.Log("Попали в слот корабля");

                // Ищем уже установленное оружие
                var oldWeapon = slot.GetComponentInChildren<weapon_Control>();

                if (oldWeapon != null)
                {
                    Debug.Log("Удаляем старое оружие");
                    var wholeWeapon = oldWeapon.GetComponentInParent<DragWeapon>();
                    Debug.Log(wholeWeapon.name);
                    Destroy(wholeWeapon.gameObject);
                }

                transform.SetParent(slot.transform);
                rectTransform.localPosition = Vector3.zero;

                return;
            }
        }

        // Если не попали в слот — вернуть обратно
        Destroy(gameObject);
    }

}
