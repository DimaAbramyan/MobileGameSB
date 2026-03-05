using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class From1to2State : MonoBehaviour
{
    [SerializeField] GameObject[] State1;
    [SerializeField] GameObject[] State2;
    [SerializeField] BodyData SpaceCraft;
    [SerializeField] GameObject parent;
    [SerializeField] GameObject body;
    public void ChangeState()
    {
        SpaceCraft = FindAnyObjectByType<BodyData>();
        //Debug.Log(SpaceCraft);
        SpaceCraft.transform.SetParent(parent.transform);
        foreach (var item in State1)
        {
            item.SetActive(false);
        }
        foreach (var item in State2)
        {
            item.SetActive(true);
        }
        foreach (DragWeapon it in SpaceCraft.GetComponentsInChildren<DragWeapon>()) 
        {
            Destroy(it.gameObject);
        }

        SpaceCraft.gameObject.SetActive(true);
    }
}
