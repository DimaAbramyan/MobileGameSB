using System.Collections.Generic;
using UnityEditor;

public static class VerifyShipAbilityDamageProgression
{
    public static object Execute()
    {
        var result = new List<object>();
        foreach (string guid in AssetDatabase.FindAssets("t:ShipData"))
        {
            ShipData shipData = AssetDatabase.LoadAssetAtPath<ShipData>(
                AssetDatabase.GUIDToAssetPath(guid));
            if (shipData?.AbilityDamageProgressionConfig == null)
                continue;

            result.Add(new
            {
                ship = shipData.name,
                config = AssetDatabase.GetAssetPath(
                    shipData.AbilityDamageProgressionConfig),
                level1Multiplier = shipData.GetAbilityDamageMultiplier(1),
                level10Multiplier = shipData.GetAbilityDamageMultiplier(10)
            });
        }

        return result.ToArray();
    }
}
