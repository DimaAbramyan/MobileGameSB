using System.Collections.Generic;
using UnityEngine;

public static class ShipBuildValidator
{
    public static bool TryValidate(
        ShipData shipData,
        IReadOnlyList<WeaponDataSerializable> weapons,
        out string message,
        int requiredWeaponCount = -1)
    {
        int weaponCount = 0;
        int totalEnergy = 0;

        if (weapons != null)
        {
            for (int i = 0; i < weapons.Count; i++)
            {
                if (weapons[i] == null)
                    continue;

                weaponCount++;
                totalEnergy += weapons[i].EnergyCost;
            }
        }

        return TryValidate(
            shipData,
            weaponCount,
            totalEnergy,
            out message,
            requiredWeaponCount);
    }

    public static bool TryValidate(
        ShipData shipData,
        IReadOnlyList<WeaponDataSer> weapons,
        out string message,
        int requiredWeaponCount = -1)
    {
        int weaponCount = 0;
        int totalEnergy = 0;

        if (weapons != null)
        {
            for (int i = 0; i < weapons.Count; i++)
            {
                if (weapons[i] == null)
                    continue;

                weaponCount++;
                totalEnergy += Mathf.Max(0, weapons[i].energyCost);
            }
        }

        return TryValidate(
            shipData,
            weaponCount,
            totalEnergy,
            out message,
            requiredWeaponCount);
    }

    private static bool TryValidate(
        ShipData shipData,
        int weaponCount,
        int totalEnergy,
        out string message,
        int requiredWeaponCount)
    {
        if (shipData == null)
        {
            message = "Не указан ShipData для выбранного корпуса.";
            return false;
        }

        int maxWeaponCount = Mathf.Max(0, shipData.maximumWeaponCount);
        int maxEnergy = Mathf.Max(0, shipData.maximumEnergy);
        int requiredCount = Mathf.Max(0, requiredWeaponCount);

        if (requiredWeaponCount >= 0 && weaponCount < requiredCount)
        {
            message =
                $"Не все слоты оружия заполнены: {weaponCount}/{requiredCount}.";
            return false;
        }

        if (weaponCount > maxWeaponCount)
        {
            message =
                $"Слишком много орудий: {weaponCount}/{maxWeaponCount}.";
            return false;
        }

        if (totalEnergy > maxEnergy)
        {
            message =
                $"Недостаточно энергии корабля: {totalEnergy}/{maxEnergy}.";
            return false;
        }

        message =
            $"Сборка корректна: орудия {weaponCount}/{maxWeaponCount}, энергия {totalEnergy}/{maxEnergy}.";
        return true;
    }
}
