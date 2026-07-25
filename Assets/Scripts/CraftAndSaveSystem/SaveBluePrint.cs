using UnityEngine;

public class SaveBluePrint : MonoBehaviour
{
    [SerializeField] private ShipSwipe shipSelector;
    public BodyData shipBody;
    private WeaponDataSerializable[] weapons;
    public WeaponDataSer[] arr;
    public InputFieldExample SendToText;
    [SerializeField] private GameObject field;
    public void CreateBluePrint()
    {
        shipBody = shipSelector.SelectedBody;
        if (shipBody == null)
        {
            Debug.LogWarning("Не выбран корпус корабля.");
            return;
        }

        weapons = shipBody.GetComponentsInChildren<WeaponDataSerializable>();
        int availableWeaponSlots =
            shipBody.GetComponentsInChildren<SetVeapon>(false).Length;
        ShipData shipData = shipBody.VisualConfig != null
            ? shipBody.VisualConfig.ShipData
            : null;

        if (!ShipBuildValidator.TryValidate(
            shipData,
            weapons,
            out string message,
            availableWeaponSlots))
        {
            Debug.LogWarning(message);
            if (field != null)
                field.SetActive(false);
            return;
        }

        Debug.Log(message);
        if (field != null)
            field.SetActive(true);

        arr = new WeaponDataSer[weapons.Length];
        Debug.Log(weapons.Length);
        for (int i = 0; i < weapons.Length; i++)
        {
            arr[i] = new WeaponDataSer(
                weapons[i].ID,
                weapons[i].place,
                weapons[i].EnergyCost);
        }

        SendToText.GetValue(arr, shipBody);
    }
}
