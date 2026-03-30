using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ButtonState : MonoBehaviour
{
    [SerializeField] public List<Sprite> SelectStatus; // 0 - НЕ выбран (бел), 1 - спрашивает (желт), 2 - выбран (зел)
    [SerializeField] private PanelVisual PanelToShow;
    public void ShowingPanel()
    {
        Image image = GetComponent<Image>();

        if (image.GetComponent<Save>().save.shipName != "")
        {
            SwitchImage(gameObject, 2);
        }
        else
        {
            SwitchImage(gameObject, 0);
        }
    }
    public void SwitchImage(GameObject ToChange, int SelectTo)
    {
        // Debug.Log(ToChange);
        if (ToChange.GetComponent<Image>().sprite != null && SelectStatus[0] != null)
            ToChange.GetComponent<Image>().sprite = SelectStatus[SelectTo];
    }
}
