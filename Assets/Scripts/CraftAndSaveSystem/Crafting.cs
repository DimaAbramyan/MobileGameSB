using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class From1to2State : MonoBehaviour
{
    [SerializeField] GameObject[] State1;
    [SerializeField] GameObject[] State2;
    [SerializeField] private ShipSwipe shipSelector;
    [SerializeField] GameObject parent;
    [SerializeField] GameObject body;
    public void ChangeState()
    {
        BodyData spaceCraft = shipSelector.SelectedBody;
        spaceCraft.transform.SetParent(parent.transform);
        foreach (var item in State1)
        {
            item.SetActive(false);
        }
        foreach (var item in State2)
        {
            item.SetActive(true);
        }
        foreach (DragWeapon it in spaceCraft.GetComponentsInChildren<DragWeapon>())
        {
            Destroy(it.gameObject);
        }

        spaceCraft.gameObject.SetActive(true);
    }
}
