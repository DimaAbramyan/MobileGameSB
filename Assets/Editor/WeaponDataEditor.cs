using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(WeaponData))]
[CanEditMultipleObjects]
public sealed class WeaponDataEditor : Editor
{
    private SerializedProperty reloadTimeByLevel;
    private SerializedProperty angleByLevel;
    private SerializedProperty damageByLevel;
    private SerializedProperty rangeByLevel;
    private SerializedProperty speedByLevel;

    private SerializedProperty startLevel;
    private SerializedProperty maxLevel;
    private SerializedProperty energyCost;

    private SerializedProperty flightMode;
    private SerializedProperty contactMode;
    private SerializedProperty homingRotationSpeed;
    private SerializedProperty growDuringFlight;
    private SerializedProperty scaleGrowthPerSecond;
    private SerializedProperty projectileLifetime;
    private SerializedProperty disableColliderAfterFirstPhysicsStep;
    private SerializedProperty fadeDuringLifetime;
    private SerializedProperty fadeDuration;
    private SerializedProperty explosionPrefab;
    private SerializedProperty explosionDamage;
    private SerializedProperty continuousDamageInterval;

    private SerializedProperty audioClipDefault;
    private SerializedProperty audioClipProjectileShot;

    private void OnEnable()
    {
        reloadTimeByLevel = serializedObject.FindProperty("reloadTimeByLevel");
        angleByLevel = serializedObject.FindProperty("angleByLevel");
        damageByLevel = serializedObject.FindProperty("damageByLevel");
        rangeByLevel = serializedObject.FindProperty("rangeByLevel");
        speedByLevel = serializedObject.FindProperty("speedByLevel");

        startLevel = serializedObject.FindProperty("startLevel");
        maxLevel = serializedObject.FindProperty("maxLevel");
        energyCost = serializedObject.FindProperty("energyCost");

        flightMode = serializedObject.FindProperty("flightMode");
        contactMode = serializedObject.FindProperty("contactMode");
        homingRotationSpeed = serializedObject.FindProperty("homingRotationSpeed");
        growDuringFlight = serializedObject.FindProperty("growDuringFlight");
        scaleGrowthPerSecond = serializedObject.FindProperty("scaleGrowthPerSecond");
        projectileLifetime = serializedObject.FindProperty("projectileLifetime");
        disableColliderAfterFirstPhysicsStep =
            serializedObject.FindProperty(
                "disableColliderAfterFirstPhysicsStep");
        fadeDuringLifetime =
            serializedObject.FindProperty("fadeDuringLifetime");
        fadeDuration = serializedObject.FindProperty("fadeDuration");
        explosionPrefab = serializedObject.FindProperty("explosionPrefab");
        explosionDamage = serializedObject.FindProperty("explosionDamage");
        continuousDamageInterval = serializedObject.FindProperty("continuousDamageInterval");

        audioClipDefault = serializedObject.FindProperty("audioClipDefault");
        audioClipProjectileShot = serializedObject.FindProperty("audioClipProjectileShot");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        DrawStats();
        DrawLevels();
        DrawBuild();
        DrawBehaviors();
        DrawLifetime();
        DrawAudio();

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawStats()
    {
        EditorGUILayout.LabelField("Stats per level", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(reloadTimeByLevel);
        EditorGUILayout.PropertyField(angleByLevel);
        EditorGUILayout.PropertyField(damageByLevel);
        EditorGUILayout.PropertyField(rangeByLevel);
        EditorGUILayout.PropertyField(speedByLevel);
        EditorGUILayout.Space();
    }

    private void DrawLevels()
    {
        EditorGUILayout.LabelField("Levels", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(startLevel);
        EditorGUILayout.PropertyField(maxLevel);
        EditorGUILayout.Space();
    }

    private void DrawBuild()
    {
        EditorGUILayout.LabelField("Build", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(
            energyCost,
            new GUIContent("Energy Cost"));
        EditorGUILayout.Space();
    }

    private void DrawBehaviors()
    {
        EditorGUILayout.LabelField("Behaviours", EditorStyles.boldLabel);

        EditorGUILayout.PropertyField(flightMode, new GUIContent("Flight Mode"));
        if (IsSelected(flightMode, ProjectileFlightMode.Homing))
            EditorGUILayout.PropertyField(homingRotationSpeed);

        EditorGUILayout.PropertyField(
            growDuringFlight,
            new GUIContent("Grow During Flight"));

        if (growDuringFlight.hasMultipleDifferentValues
            || growDuringFlight.boolValue)
        {
            EditorGUILayout.PropertyField(
                scaleGrowthPerSecond,
                new GUIContent("Scale Growth Per Second"));
        }

        EditorGUILayout.Space();
        EditorGUILayout.PropertyField(
            contactMode,
            new GUIContent("Contact Mode"));

        if (IsSelected(
            contactMode,
            ProjectileContactMode.ExplodeAndSpawn))
        {
            EditorGUILayout.PropertyField(explosionPrefab);
            EditorGUILayout.PropertyField(explosionDamage);
        }

        if (IsSelected(
            contactMode,
            ProjectileContactMode.PierceContinuous))
        {
            EditorGUILayout.PropertyField(continuousDamageInterval);
            EditorGUILayout.HelpBox(
                "Set the interval to 0.02 to deal damage on every physics update.",
                MessageType.Info);
        }

        EditorGUILayout.Space();
    }

    private void DrawLifetime()
    {
        EditorGUILayout.LabelField(
            "Projectile Lifetime",
            EditorStyles.boldLabel);

        EditorGUILayout.PropertyField(
            projectileLifetime,
            new GUIContent("Lifetime"));
        EditorGUILayout.PropertyField(
            disableColliderAfterFirstPhysicsStep,
            new GUIContent("Collider Active For One Physics Step"));

        if (disableColliderAfterFirstPhysicsStep.hasMultipleDifferentValues
            || disableColliderAfterFirstPhysicsStep.boolValue)
        {
            EditorGUILayout.HelpBox(
                "The collider stays enabled for one physics simulation and is disabled before the next one.",
                MessageType.Info);
        }

        EditorGUILayout.PropertyField(
            fadeDuringLifetime,
            new GUIContent("Fade Before Despawn"));

        if (fadeDuringLifetime.hasMultipleDifferentValues
            || fadeDuringLifetime.boolValue)
        {
            EditorGUILayout.PropertyField(
                fadeDuration,
                new GUIContent("Fade Duration"));
        }

        EditorGUILayout.Space();
    }

    private void DrawAudio()
    {
        EditorGUILayout.LabelField("Audio", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(audioClipDefault);
        EditorGUILayout.PropertyField(audioClipProjectileShot);
    }

    private static bool IsSelected<TEnum>(SerializedProperty property, TEnum value)
        where TEnum : System.Enum
    {
        return property.hasMultipleDifferentValues
            || property.intValue == System.Convert.ToInt32(value);
    }

}

[CustomPropertyDrawer(typeof(MovementCommandData))]
public sealed class MovementCommandDataDrawer : PropertyDrawer
{
    private const float Gap = 2f;

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        SerializedProperty type = property.FindPropertyRelative("type");
        Rect line = NextLine(ref position);
        EditorGUI.PropertyField(line, type, GetCommandLabel(property, type));

        EditorGUI.indentLevel++;
        switch ((MovementCommandType)type.enumValueIndex)
        {
            case MovementCommandType.SpawnAt:
                Draw(ref position, property, "position", "World Position");
                break;

            case MovementCommandType.MoveLocal:
                DrawMoveFields(ref position, property, "Local Offset");
                break;

            case MovementCommandType.MoveWorld:
                DrawMoveFields(ref position, property, "World Position");
                break;

            case MovementCommandType.RotateBy:
                Draw(ref position, property, "degrees", "Degrees");
                Draw(ref position, property, "duration", "Duration");
                Draw(ref position, property, "ease", "Ease");
                break;

            case MovementCommandType.Repeat:
                Draw(ref position, property, "fromAction", "From Action");
                Draw(ref position, property, "toAction", "To Action");
                Draw(ref position, property, "infinite", "Infinite");
                if (!property.FindPropertyRelative("infinite").boolValue)
                    Draw(ref position, property, "repeatCount", "Additional Repeats");
                break;

            case MovementCommandType.Wait:
                Draw(ref position, property, "waitDuration", "Duration");
                break;

            case MovementCommandType.DeactivateChildrenFor:
                Draw(ref position, property, "deactivateDuration", "Duration");
                break;
        }
        EditorGUI.indentLevel--;

        EditorGUI.EndProperty();
    }

    public override float GetPropertyHeight(
        SerializedProperty property,
        GUIContent label)
    {
        int lineCount = 1;
        MovementCommandType type =
            (MovementCommandType)property.FindPropertyRelative("type").enumValueIndex;

        switch (type)
        {
            case MovementCommandType.SpawnAt:
            case MovementCommandType.Wait:
            case MovementCommandType.DeactivateChildrenFor:
                lineCount += 1;
                break;

            case MovementCommandType.MoveLocal:
            case MovementCommandType.MoveWorld:
            case MovementCommandType.RotateBy:
                lineCount += 3;
                break;

            case MovementCommandType.Repeat:
                lineCount += property.FindPropertyRelative("infinite").boolValue ? 3 : 4;
                break;
        }

        return lineCount * EditorGUIUtility.singleLineHeight
            + (lineCount - 1) * Gap;
    }

    private static void DrawMoveFields(
        ref Rect position,
        SerializedProperty property,
        string positionLabel)
    {
        Draw(ref position, property, "position", positionLabel);
        Draw(ref position, property, "duration", "Duration");
        Draw(ref position, property, "ease", "Ease");
    }

    private static void Draw(
        ref Rect position,
        SerializedProperty property,
        string propertyName,
        string label)
    {
        Rect line = NextLine(ref position);
        EditorGUI.PropertyField(
            line,
            property.FindPropertyRelative(propertyName),
            new GUIContent(label));
    }

    private static Rect NextLine(ref Rect position)
    {
        Rect line = new Rect(
            position.x,
            position.y,
            position.width,
            EditorGUIUtility.singleLineHeight);

        position.y += EditorGUIUtility.singleLineHeight + Gap;
        return line;
    }

    private static GUIContent GetCommandLabel(
        SerializedProperty property,
        SerializedProperty type)
    {
        int actionNumber = GetArrayIndex(property.propertyPath) + 1;
        string commandName = type.enumDisplayNames[type.enumValueIndex];
        return new GUIContent($"Action {actionNumber}: {commandName}");
    }

    private static int GetArrayIndex(string propertyPath)
    {
        int marker = propertyPath.LastIndexOf("data[");
        if (marker < 0)
            return 0;

        int start = marker + 5;
        int end = propertyPath.IndexOf(']', start);
        if (end < 0)
            return 0;

        return int.TryParse(propertyPath.Substring(start, end - start), out int index)
            ? index
            : 0;
    }
}
