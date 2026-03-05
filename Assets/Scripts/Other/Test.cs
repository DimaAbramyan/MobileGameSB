using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

public class InputFieldExample : MonoBehaviour
{
    public TMP_InputField inputField;
    public TMP_InputField inputFieldDescr;
    private SaveBluePrint Save;
    public void ReadInput()
    {
        SaveShip save = new SaveShip(Save.shipBody.shipId, Save.arr, inputField.text, inputFieldDescr.text);
        Debug.Log(save.weaponData.Length + "ﬂ œ»ƒŒ–¿—");
        SaveSystem.SaveShipData(save);  

    }
    public void GetValue(WeaponData[] w, BodyData b)
    {
        Debug.Log(w.Length);
        Save = new SaveBluePrint();
        Save.arr = w;
        Save.shipBody = b;
    }
}
