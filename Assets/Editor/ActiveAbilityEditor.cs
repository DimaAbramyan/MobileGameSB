using UnityEditor;

[CustomEditor(typeof(ActiveAbility), true)]
public sealed class ActiveAbilityEditor : Editor
{
    private SerializedProperty abilityMode;
    private SerializedProperty cooldown;
    private SerializedProperty toggleMaximumTime;
    private SerializedProperty toggleTimeCostPerSecond;
    private SerializedProperty toggleRechargeStartTime;
    private SerializedProperty toggleRechargeDuration;
    private SerializedProperty toggleTimeRemaining;
    private SerializedProperty maxCharges;
    private SerializedProperty currentCharges;
    private SerializedProperty owner;

    private void OnEnable()
    {
        abilityMode = serializedObject.FindProperty("abilityMode");
        cooldown = serializedObject.FindProperty("cooldown");
        toggleMaximumTime = serializedObject.FindProperty("toggleMaximumTime");
        toggleTimeCostPerSecond = serializedObject.FindProperty("toggleTimeCostPerSecond");
        toggleRechargeStartTime = serializedObject.FindProperty("toggleRechargeStartTime");
        toggleRechargeDuration = serializedObject.FindProperty("toggleRechargeDuration");
        toggleTimeRemaining = serializedObject.FindProperty("toggleTimeRemaining");
        maxCharges = serializedObject.FindProperty("maxCharges");
        currentCharges = serializedObject.FindProperty("currentCharges");
        owner = serializedObject.FindProperty("owner");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        DrawAbilityCore();
        EditorGUILayout.Space(6f);
        DrawSpecificAbilityFields();

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawAbilityCore()
    {
        EditorGUILayout.LabelField("Ultimate Ability", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(abilityMode);
        EditorGUILayout.PropertyField(cooldown);

        UltimateAbilityMode mode =
            (UltimateAbilityMode)abilityMode.enumValueIndex;

        if (mode == UltimateAbilityMode.Toggle)
            DrawToggleFields();

        if (mode == UltimateAbilityMode.Charges)
            DrawChargeFields();

        EditorGUILayout.PropertyField(owner);
    }

    private void DrawToggleFields()
    {
        EditorGUILayout.Space(3f);
        EditorGUILayout.LabelField("Toggle Resource", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(toggleMaximumTime);
        EditorGUILayout.PropertyField(toggleTimeCostPerSecond);
        EditorGUILayout.PropertyField(toggleRechargeStartTime);
        EditorGUILayout.PropertyField(toggleRechargeDuration);

        using (new EditorGUI.DisabledScope(true))
            EditorGUILayout.PropertyField(toggleTimeRemaining);
    }

    private void DrawChargeFields()
    {
        EditorGUILayout.Space(3f);
        EditorGUILayout.LabelField("Charges", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(maxCharges);

        using (new EditorGUI.DisabledScope(true))
            EditorGUILayout.PropertyField(currentCharges);
    }

    private void DrawSpecificAbilityFields()
    {
        EditorGUILayout.LabelField("Ability Settings", EditorStyles.boldLabel);

        SerializedProperty property = serializedObject.GetIterator();
        bool enterChildren = true;
        while (property.NextVisible(enterChildren))
        {
            enterChildren = false;

            if (IsBaseAbilityProperty(property.name))
                continue;

            using (new EditorGUI.DisabledScope(property.propertyPath == "m_Script"))
                EditorGUILayout.PropertyField(property, true);
        }
    }

    private static bool IsBaseAbilityProperty(string propertyName)
    {
        return propertyName == "abilityMode"
            || propertyName == "cooldown"
            || propertyName == "toggleMaximumTime"
            || propertyName == "toggleTimeCostPerSecond"
            || propertyName == "toggleRechargeStartTime"
            || propertyName == "toggleRechargeDuration"
            || propertyName == "toggleTimeRemaining"
            || propertyName == "maxCharges"
            || propertyName == "currentCharges"
            || propertyName == "owner";
    }
}
