using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(ShipAbilityDamageProgressionConfig))]
public sealed class ShipAbilityDamageProgressionConfigEditor : Editor
{
    private SerializedProperty damageBonusPercentByLevel;

    private void OnEnable()
    {
        damageBonusPercentByLevel = serializedObject.FindProperty(
            "damageBonusPercentByLevel");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.HelpBox(
            "Each value is added when this in-battle ship level is reached. "
            + "Bonuses are cumulative, like weapon progression.",
            MessageType.None);

        if (damageBonusPercentByLevel != null)
        {
            for (int index = 0;
                 index < damageBonusPercentByLevel.arraySize;
                 index++)
            {
                SerializedProperty bonus =
                    damageBonusPercentByLevel.GetArrayElementAtIndex(index);
                bonus.floatValue = Mathf.Max(
                    0f,
                    EditorGUILayout.FloatField(
                        $"Level {index + ParentShip.MinWeaponLevel} Damage Bonus (%)",
                        bonus.floatValue));
            }
        }

        serializedObject.ApplyModifiedProperties();
    }
}
