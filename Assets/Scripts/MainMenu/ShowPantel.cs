using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using UnityEngine;
using UnityEngine.UI;
using Zenject;
public class ButtonState : MonoBehaviour
{
    [Inject] PanelManager manager;
    [SerializeField] public List<Sprite> SelectStatus; // 0 - НЕ выбран (бел), 1 - спрашивает (желт), 2 - выбран (зел)
    [SerializeField] private PanelVisual PanelToShow;
    Image Image;
    public void Awake()
    {
        Image = GetComponent<Image>();
    }
    public void Start()
    {
        SwitchImage();
    }
    public void OnEnable()
    {
        manager.OnPanelsChanged += SwitchImage;
    }
    public void OnDisable()
    {
        manager.OnPanelsChanged -= SwitchImage;
    }
    public void ShowingPanel()
    {
        if (PanelToShow.gameObject.activeSelf)
        {
            PanelToShow.gameObject.SetActive(false);
            manager.CloseThisPanel(PanelToShow);
        }
        else
        {
            manager.CloseEveryPanel();
            manager.OpenThisPanel(PanelToShow);
        }
    }
    public void SwitchImage()
    {
        if (PanelToShow.isActiveAndEnabled)
        {
            FinalChange(1);
            return;
        }
        if (Image.GetComponent<Save>().save.shipName != "")
        {
            FinalChange(2);
            return;
        }
        FinalChange(0);
    }
    public void FinalChange(int id)
    {

        if (Image.sprite != null && SelectStatus[0] != null)
            Image.sprite = SelectStatus[id];
    }
}
