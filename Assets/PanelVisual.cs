using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Zenject;

public class PanelVisual : MonoBehaviour
{
    [Inject] private PanelManager manager;

    private void Awake()
    {
        manager.RegisterPanel(this);
        gameObject.SetActive(false);
    }
}
