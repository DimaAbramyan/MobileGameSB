using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public class PanelManager : MonoBehaviour
{
    public event Action OnPanelsChanged;
    List<PanelVisual> panelVisuals = new List<PanelVisual>();
    public void RegisterPanel(PanelVisual panel)
    {
        if (!panelVisuals.Contains(panel))
            panelVisuals.Add(panel);
    }
    public void OpenThisPanel(PanelVisual whatToOpen)
    {
        foreach (var panel in panelVisuals)
        {
            panel.gameObject.SetActive(panel == whatToOpen);
        }
        OnPanelsChanged?.Invoke();
    }
    public void CloseEveryPanel()
    {
        foreach (PanelVisual panel in panelVisuals)
        {
            panel.gameObject.SetActive(false);
        }

        OnPanelsChanged?.Invoke();
    }
    public void CloseThisPanel(PanelVisual whatToClose)
    {
        whatToClose.gameObject.SetActive(false);
        OnPanelsChanged?.Invoke();
    }
}
