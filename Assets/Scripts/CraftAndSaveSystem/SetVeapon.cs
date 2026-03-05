using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;

public class SetVeapon : MonoBehaviour
{
    public void CheckDrop(PointerEventData eventData)
    {
        if (eventData != null)
        {
            GameObject dropped = eventData.pointerDrag;
            foreach (Transform childs in gameObject.GetComponentsInChildren<Transform>())
            {
                if (childs != dropped)
                    Destroy(childs);
            }
            DragWeapon draggableItem = dropped.GetComponent<DragWeapon>();
            draggableItem.parentAfterDrag = transform;
            
        }
        else
        {
            Debug.Log("ֵבכאם?");
        }
    }
}
