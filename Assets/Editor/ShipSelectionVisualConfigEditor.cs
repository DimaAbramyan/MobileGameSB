using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(ShipSelectionVisualConfig))]
public sealed class ShipSelectionVisualConfigEditor : Editor
{
    private SerializedProperty shipDataProperty;
    private SerializedProperty shipNameProperty;
    private SerializedProperty activeAbilityDescriptionProperty;
    private SerializedProperty passiveAbilityDescriptionProperty;
    private SerializedProperty radarChartConfigProperty;
    private SerializedProperty radarChartValuesProperty;

    private void OnEnable()
    {
        shipDataProperty = serializedObject.FindProperty("shipData");
        shipNameProperty = serializedObject.FindProperty("shipName");
        activeAbilityDescriptionProperty =
            serializedObject.FindProperty("activeAbilityDescription");
        passiveAbilityDescriptionProperty =
            serializedObject.FindProperty("passiveAbilityDescription");
        radarChartConfigProperty =
            serializedObject.FindProperty("radarChartConfig");
        radarChartValuesProperty =
            serializedObject.FindProperty("radarChartValues");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        DrawIdentity();
        EditorGUILayout.Space();
        DrawText();
        EditorGUILayout.Space();
        DrawRadarChart();

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawIdentity()
    {
        EditorGUILayout.LabelField("Identity", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(shipDataProperty);

        DrawShipDataPreview();
    }

    private void DrawText()
    {
        EditorGUILayout.LabelField("Text", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(shipNameProperty);
        EditorGUILayout.PropertyField(activeAbilityDescriptionProperty);
        EditorGUILayout.PropertyField(passiveAbilityDescriptionProperty);
    }

    private void DrawRadarChart()
    {
        EditorGUILayout.LabelField("Stats chart", EditorStyles.boldLabel);

        EditorGUI.BeginChangeCheck();
        EditorGUILayout.PropertyField(radarChartConfigProperty);
        bool configChanged = EditorGUI.EndChangeCheck();

        RadarChartConfig radarConfig =
            radarChartConfigProperty.objectReferenceValue as RadarChartConfig;

        if (radarConfig == null)
        {
            EditorGUILayout.HelpBox(
                "Assign Radar Chart Config to edit chart values.",
                MessageType.Info);
            DrawRawValuesArray();
            return;
        }

        if (configChanged || radarChartValuesProperty.arraySize
            != radarConfig.ParameterNames.Count)
        {
            SyncValuesArraySize(radarConfig.ParameterNames.Count);
        }

        DrawShipDataAutofillButton(radarConfig);

        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField(
            $"Values, 0..{radarConfig.MaxValue}",
            EditorStyles.miniBoldLabel);

        EditorGUI.indentLevel++;
        for (int i = 0; i < radarConfig.ParameterNames.Count; i++)
        {
            SerializedProperty valueProperty =
                radarChartValuesProperty.GetArrayElementAtIndex(i);
            string parameterName = radarConfig.ParameterNames[i];
            if (string.IsNullOrWhiteSpace(parameterName))
                parameterName = $"Parameter {i + 1}";

            valueProperty.intValue = EditorGUILayout.IntSlider(
                parameterName,
                valueProperty.intValue,
                0,
                radarConfig.MaxValue);
        }
        EditorGUI.indentLevel--;
    }

    private void DrawShipDataPreview()
    {
        ShipData shipData = shipDataProperty.objectReferenceValue as ShipData;
        if (shipData == null)
            return;

        EditorGUILayout.Space(4f);
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField(
                "Ship Data preview",
                EditorStyles.miniBoldLabel);
            EditorGUILayout.LabelField("ID", shipData.shipId.ToString());
            EditorGUILayout.LabelField("Speed", shipData.speed.ToString("0.##"));
            EditorGUILayout.LabelField(
                "Health",
                shipData.maximumHealthPoints.ToString("0.##"));
            EditorGUILayout.LabelField(
                "Shield",
                shipData.maximumShieldPoints.ToString("0.##"));
            EditorGUILayout.LabelField("Mass", shipData.mass.ToString("0.##"));
            EditorGUILayout.LabelField("Drag", shipData.drag.ToString("0.##"));
            EditorGUILayout.LabelField(
                "Energy",
                shipData.maximumEnergy.ToString());
            EditorGUILayout.LabelField(
                "Weapon Count",
                shipData.maximumWeaponCount.ToString());
        }
    }

    private void DrawShipDataAutofillButton(RadarChartConfig radarConfig)
    {
        ShipData shipData = shipDataProperty.objectReferenceValue as ShipData;
        if (shipData == null)
            return;

        if (!GUILayout.Button("Fill radar values from Ship Data"))
            return;

        SyncValuesArraySize(radarConfig.ParameterNames.Count);
        for (int i = 0; i < radarConfig.ParameterNames.Count; i++)
        {
            SerializedProperty valueProperty =
                radarChartValuesProperty.GetArrayElementAtIndex(i);
            valueProperty.intValue = Mathf.Clamp(
                Mathf.RoundToInt(GetShipDataValue(
                    radarConfig.ParameterNames[i],
                    shipData)),
                0,
                radarConfig.MaxValue);
        }
    }

    private static float GetShipDataValue(string parameterName, ShipData shipData)
    {
        string key = NormalizeParameterName(parameterName);

        if (ContainsAny(key, "speed", "скорост"))
            return shipData.speed;

        if (ContainsAny(key, "health", "hp", "жизн", "здоров"))
            return shipData.maximumHealthPoints;

        if (ContainsAny(key, "shield", "щит"))
            return shipData.maximumShieldPoints;

        if (ContainsAny(key, "mass", "мас"))
            return shipData.mass;

        if (ContainsAny(key, "drag", "control", "handling", "маневр", "управ"))
            return shipData.drag;

        if (ContainsAny(key, "regen", "реген"))
            return Mathf.Max(shipData.healthRegenRate, shipData.shieldRegenRate);

        if (ContainsAny(key, "energy", "энерг"))
            return shipData.maximumEnergy;

        if (ContainsAny(key, "weapon", "оруж"))
            return shipData.maximumWeaponCount;

        return 0f;
    }

    private static string NormalizeParameterName(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim().ToLowerInvariant();
    }

    private static bool ContainsAny(string value, params string[] patterns)
    {
        for (int i = 0; i < patterns.Length; i++)
        {
            if (value.Contains(patterns[i]))
                return true;
        }

        return false;
    }

    private void DrawRawValuesArray()
    {
        EditorGUILayout.PropertyField(
            radarChartValuesProperty,
            includeChildren: true);
    }

    private void SyncValuesArraySize(int targetSize)
    {
        targetSize = Mathf.Max(0, targetSize);

        while (radarChartValuesProperty.arraySize < targetSize)
        {
            int index = radarChartValuesProperty.arraySize;
            radarChartValuesProperty.InsertArrayElementAtIndex(index);
            radarChartValuesProperty
                .GetArrayElementAtIndex(index)
                .intValue = 0;
        }

        while (radarChartValuesProperty.arraySize > targetSize)
        {
            radarChartValuesProperty.DeleteArrayElementAtIndex(
                radarChartValuesProperty.arraySize - 1);
        }
    }
}
