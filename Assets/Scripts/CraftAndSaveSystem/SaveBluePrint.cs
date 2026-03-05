using System.Collections;
using System.Collections.Generic;
using System.IO;
using JetBrains.Annotations;
using UnityEngine;

public class SaveBluePrint : MonoBehaviour
{
    public BodyData shipBody;
    private WeaponDataSerializable[] weapons;
    public WeaponData[] arr;
    public InputFieldExample SendToText;
    [SerializeField] private GameObject field;
    public void CreateBluePrint()
    {
        BodyData shipBody = FindAnyObjectByType<BodyData>();         
        weapons = shipBody.GetComponentsInChildren<WeaponDataSerializable>();
        if (weapons.Length == shipBody.weaponCnt)
        {
            field.SetActive(true);
            WeaponData[] arr = new WeaponData[weapons.Length];
            Debug.Log(weapons.Length);
            for (int i = 0; i < weapons.Length; i++)
            {
                arr[i] = new WeaponData(weapons[i].ID, weapons[i].place);
            }
            SendToText.GetValue(arr, shipBody);
        }
        else
        {
            Debug.Log("Нельзя");
            
        }

            
    }
}
