using System;

// Converts the two ships selected in the main menu into the legacy battle
// payload consumed by CreatePlayerShips in the Fighting scene.
public sealed class BattleLaunchService
{
    private readonly TeamSelectionService selectedShips;
    private readonly TeamSave teamSave;

    public BattleLaunchService(
        TeamSelectionService selectedShips,
        TeamSave teamSave)
    {
        this.selectedShips = selectedShips;
        this.teamSave = teamSave;
    }

    public bool HasCompleteShipSelection
    {
        get
        {
            if (selectedShips == null)
                return false;

            for (int i = 0; i < TeamSelectionService.SelectedShipCount; i++)
            {
                if (selectedShips.GetSelectedShip(i) == null)
                    return false;
            }

            return true;
        }
    }

    public bool TryPrepareBattle(out string failureReason)
    {
        if (selectedShips == null || teamSave == null)
        {
            failureReason = "Не удалось получить данные выбранных кораблей.";
            return false;
        }

        var selectedData = new SaveData[TeamSelectionService.SelectedShipCount];
        for (int i = 0; i < selectedData.Length; i++)
        {
            SaveShip ship = selectedShips.GetSelectedShip(i);
            if (ship == null)
            {
                failureReason = "Перед началом уровня выбери два корабля.";
                return false;
            }

            selectedData[i] = ToBattleSaveData(ship);
        }

        teamSave.AllSavesThatLoaded = selectedData;
        failureReason = string.Empty;
        return true;
    }

    private static SaveData ToBattleSaveData(SaveShip ship)
    {
        return new SaveData
        {
            shipId = ship.shipId,
            shipName = ship.shipName,
            WeaponData = CopyWeapons(ship.weaponData)
        };
    }

    private static WeaponDataSer[] CopyWeapons(WeaponDataSer[] source)
    {
        if (source == null || source.Length == 0)
            return Array.Empty<WeaponDataSer>();

        int count = 0;
        for (int i = 0; i < source.Length; i++)
        {
            if (source[i] != null)
                count++;
        }

        if (count == 0)
            return Array.Empty<WeaponDataSer>();

        var copy = new WeaponDataSer[count];
        int copyIndex = 0;
        for (int i = 0; i < source.Length; i++)
        {
            WeaponDataSer weapon = source[i];
            if (weapon == null)
                continue;

            copy[copyIndex++] = new WeaponDataSer(
                weapon.ID,
                weapon.place,
                weapon.energyCost,
                weapon.contentId,
                weapon.usesShipLocalPosition,
                weapon.slotId);
        }

        return copy;
    }
}
