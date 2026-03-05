using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Buff : MonoBehaviour
{
    [SerializeField]
    List<GameObject> childs;
    [SerializeField] protected float speed = 0.35f;
    void Start()
    {
        PointsCollector.MaxBonuses += 1;
    }

    // Update is called once per frame
    void FixedUpdate()
    {
        foreach (GameObject child in childs)
        {
            child.transform.localPosition = (new Vector3(0, 0, 0));
        }
    }
}
