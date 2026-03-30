using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Zenject;

public class HideTabs : MonoBehaviour
{
    [Inject] PanelManager panelManager;
    public void Hide()
    {
        panelManager.CloseEveryPanel();
    }
}
