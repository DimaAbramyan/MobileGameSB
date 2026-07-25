using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;
using Zenject;

public class CollectAllSaves : MonoBehaviour
{
    [Inject] private List<Save> allSaves;
    [Inject] private TeamSave teamSave;
    [Inject] private PrefabFactory prefabFactory;

    [SerializeField] int sceneID;
    [SerializeField] bool needToCheck;
    [SerializeField] GameObject WarningToShow;
    public void Collecting()
    {
        Save[] saves = allSaves
            .OrderBy(save => save.SlotIndex)
            .ToArray();

        Save[] savesToLoad = saves;
        bool canLoad = !needToCheck
            || TryGetValidTeamSaves(saves, out savesToLoad);

        if (canLoad)
        {
            var saveDataArray = savesToLoad.Select(save => new SaveData
            {
                shipId = save.save.shipId,
                shipName = save.save.shipName,
                WeaponData = save.save.weaponData
            }).ToArray();
            teamSave.AllSavesThatLoaded = saveDataArray;
            SceneManager.LoadScene(sceneID);
        }
        else
        {
            WarningToShow.SetActive(true);
        }
    }

    private bool TryGetValidTeamSaves(Save[] saves, out Save[] teamSaves)
    {
        teamSaves = saves
            .Where(IsFilledSaveSlot)
            .OrderBy(save => save.SlotIndex)
            .ToArray();

        LogTeamSaves("Selected team slots", teamSaves);

        if (teamSaves.Length < 2)
        {
            Debug.LogWarning(
                $"Нужно выбрать минимум 2 корабля. Сейчас выбрано: {teamSaves.Length}.");
            return false;
        }

        if (HasDuplicateShipIds(teamSaves))
        {
            Debug.LogWarning(
                "В команде есть повторяющиеся ID кораблей. Выбери разные корпуса.");
            return false;
        }

        for (int i = 0; i < teamSaves.Length; i++)
        {
            if (!IsShipBuildValid(teamSaves[i].save))
                return false;
        }

        return true;
    }

    private bool IsShipBuildValid(SaveShip saveShip)
    {
        ShipData shipData = GetShipData(saveShip.shipId);
        if (shipData == null)
        {
            Debug.Log(
                $"{saveShip.shipName}: ShipData не найден на UI-префабе, проверка энергии при запуске пропущена. "
                + "Энергия проверяется при создании чертежа корабля.");
            return true;
        }

        bool isValid = ShipBuildValidator.TryValidate(
            shipData,
            saveShip.weaponData,
            out string message);

        if (!isValid)
            Debug.LogWarning($"{saveShip.shipName}: {message}");

        return isValid;
    }

    private static bool IsFilledSaveSlot(Save save)
    {
        return save != null
            && save.save != null
            && !string.IsNullOrEmpty(save.save.shipName);
    }

    private static bool HasDuplicateShipIds(Save[] saves)
    {
        HashSet<int> shipIds = new HashSet<int>();
        for (int i = 0; i < saves.Length; i++)
        {
            if (!shipIds.Add(saves[i].save.shipId))
                return true;
        }

        return false;
    }

    private static void LogTeamSaves(string prefix, Save[] saves)
    {
        StringBuilder builder = new StringBuilder(prefix);
        builder.Append(": ");

        if (saves == null || saves.Length == 0)
        {
            builder.Append("empty");
            Debug.Log(builder.ToString());
            return;
        }

        for (int i = 0; i < saves.Length; i++)
        {
            if (i > 0)
                builder.Append(" | ");

            builder
                .Append("slot ")
                .Append(saves[i].SlotIndex)
                .Append(" -> ")
                .Append(saves[i].save.shipName)
                .Append(" id=")
                .Append(saves[i].save.shipId);
        }

        Debug.Log(builder.ToString());
    }

    private ShipData GetShipData(int shipId)
    {
        GameObject shipPrefab = prefabFactory.GetShip(shipId);
        if (shipPrefab == null)
            return null;

        BodyData bodyData = shipPrefab.GetComponentInChildren<BodyData>(true);
        if (bodyData == null || bodyData.VisualConfig == null)
            return null;

        return bodyData.VisualConfig.ShipData;
    }
}
