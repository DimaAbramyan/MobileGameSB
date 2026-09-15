using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(ShipData))]
public sealed class ShipDataEditor : Editor
{
    private SerializedProperty speed;
    private SerializedProperty mass;
    private SerializedProperty drag;
    private SerializedProperty maximumHealthPoints;
    private SerializedProperty overrideHealthRegeneration;
    private SerializedProperty healthRegenCooldown;
    private SerializedProperty healthRegenRate;
    private SerializedProperty maximumShieldPoints;
    private SerializedProperty overrideShieldRegeneration;
    private SerializedProperty shieldRegenCooldown;
    private SerializedProperty shieldRegenRate;
    private SerializedProperty maximumEnergy;
    private SerializedProperty maximumWeaponCount;
    private SerializedProperty currentLevel;
    private SerializedProperty shipId;
    private SerializedProperty shipMetaConfig;

    private void OnEnable()
    {
        speed = serializedObject.FindProperty("speed");
        mass = serializedObject.FindProperty("mass");
        drag = serializedObject.FindProperty("drag");
        maximumHealthPoints = serializedObject.FindProperty("maximumHealthPoints");
        overrideHealthRegeneration = serializedObject.FindProperty(
            "overrideHealthRegeneration");
        healthRegenCooldown = serializedObject.FindProperty(
            "healthRegenCooldown");
        healthRegenRate = serializedObject.FindProperty("healthRegenRate");
        maximumShieldPoints = serializedObject.FindProperty("maximumShieldPoints");
        overrideShieldRegeneration = serializedObject.FindProperty(
            "overrideShieldRegeneration");
        shieldRegenCooldown = serializedObject.FindProperty(
            "shieldRegenCooldown");
        shieldRegenRate = serializedObject.FindProperty("shieldRegenRate");
        maximumEnergy = serializedObject.FindProperty("maximumEnergy");
        maximumWeaponCount = serializedObject.FindProperty("maximumWeaponCount");
        currentLevel = serializedObject.FindProperty("currentLvl");
        shipId = serializedObject.FindProperty("shipId");
        shipMetaConfig = serializedObject.FindProperty("shipMetaConfig");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.LabelField("Meta Progression", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(
            shipMetaConfig,
            new GUIContent("Ship Meta Config"));
        bool hasMetaConfig = shipMetaConfig.objectReferenceValue != null;
        if (hasMetaConfig)
        {
            EditorGUILayout.HelpBox(
                "Level 0 of Ship Meta Config supplies health, shield, "
                + "regeneration, speed and energy.",
                MessageType.Info);
        }

        DrawControllability(hasMetaConfig);
        EditorGUILayout.Space();
        DrawHealth(hasMetaConfig);
        EditorGUILayout.Space();
        DrawShield(hasMetaConfig);
        EditorGUILayout.Space();
        DrawBuildLimits(hasMetaConfig);
        EditorGUILayout.Space();
        EditorGUILayout.PropertyField(currentLevel, new GUIContent("Start Level"));
        EditorGUILayout.PropertyField(shipId, new GUIContent("Ship ID"));

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawControllability(bool hasMetaConfig)
    {
        EditorGUILayout.LabelField("Controllability", EditorStyles.boldLabel);
        DrawMetaFieldOrInfo(
            hasMetaConfig,
            speed,
            "Speed is configured in Ship Meta Config.");
        EditorGUILayout.PropertyField(mass);
        EditorGUILayout.PropertyField(drag);
    }

    private void DrawHealth(bool hasMetaConfig)
    {
        EditorGUILayout.LabelField("Health", EditorStyles.boldLabel);
        DrawMetaFieldOrInfo(
            hasMetaConfig,
            maximumHealthPoints,
            "Maximum health is configured in Ship Meta Config.");
        DrawRegeneration(
            overrideHealthRegeneration,
            healthRegenCooldown,
            healthRegenRate,
            "Override Health Regeneration",
            ShipData.DefaultHealthRegenCooldown,
            hasMetaConfig);
    }

    private void DrawShield(bool hasMetaConfig)
    {
        EditorGUILayout.LabelField("Shield", EditorStyles.boldLabel);
        DrawMetaFieldOrInfo(
            hasMetaConfig,
            maximumShieldPoints,
            "Maximum shield is configured in Ship Meta Config.");
        DrawRegeneration(
            overrideShieldRegeneration,
            shieldRegenCooldown,
            shieldRegenRate,
            "Override Shield Regeneration",
            ShipData.DefaultShieldRegenCooldown,
            hasMetaConfig);
    }

    private static void DrawRegeneration(
        SerializedProperty overrideRegeneration,
        SerializedProperty cooldown,
        SerializedProperty ratePercent,
        string overrideLabel,
        float defaultCooldown,
        bool hasMetaConfig)
    {
        EditorGUILayout.PropertyField(
            overrideRegeneration,
            new GUIContent(
                hasMetaConfig ? "Override Recovery Delay" : overrideLabel,
                hasMetaConfig
                    ? "Only the recovery delay stays on Ship Data; recovery speed comes from Ship Meta Config."
                    : "Use values specific to this ship instead of the shared defaults."));

        if (!overrideRegeneration.boolValue)
        {
            EditorGUILayout.HelpBox(
                $"Shared defaults: {defaultCooldown:0.#} s delay, "
                + (hasMetaConfig
                    ? "regeneration speed in Ship Meta Config."
                    : $"{ShipData.DefaultRegenRatePercent:0.#}% of maximum per second."),
                MessageType.None);
            return;
        }

        EditorGUI.indentLevel++;
        EditorGUILayout.PropertyField(
            cooldown,
            new GUIContent("Recovery Delay (seconds)"));
        if (hasMetaConfig)
        {
            EditorGUILayout.HelpBox(
                "Regeneration speed is configured in Ship Meta Config.",
                MessageType.None);
        }
        else
        {
            EditorGUILayout.PropertyField(
                ratePercent,
                new GUIContent("Recovery (% of maximum per second)"));
        }
        EditorGUI.indentLevel--;
    }

    private void DrawBuildLimits(bool hasMetaConfig)
    {
        EditorGUILayout.LabelField("Build Limits", EditorStyles.boldLabel);
        DrawMetaFieldOrInfo(
            hasMetaConfig,
            maximumEnergy,
            "Maximum energy is configured in Ship Meta Config.");
        EditorGUILayout.PropertyField(maximumWeaponCount);
    }

    private static void DrawMetaFieldOrInfo(
        bool hasMetaConfig,
        SerializedProperty property,
        string metaInfo)
    {
        if (hasMetaConfig)
        {
            EditorGUILayout.HelpBox(metaInfo, MessageType.None);
            return;
        }

        EditorGUILayout.PropertyField(property);
    }
}
