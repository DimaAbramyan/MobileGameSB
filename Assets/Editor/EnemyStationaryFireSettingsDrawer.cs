using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(EnemyStationaryFireSettings))]
public sealed class EnemyStationaryFireSettingsDrawer : PropertyDrawer
{
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        => 2f * (EditorGUIUtility.singleLineHeight + 2f) + (property.FindPropertyRelative("legacyForward").boolValue ? 66f : 0f);

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);
        var legacy = property.FindPropertyRelative("legacyForward");
        position.height = EditorGUIUtility.singleLineHeight;
        EditorGUI.BeginChangeCheck();
        var mode = property.FindPropertyRelative("directionMode");
        EditorGUI.PropertyField(position, mode, new GUIContent("Stationary Direction", "Angle — фиксированный мировой угол. World Point — неподвижная точка мира; направление рассчитывается от каждого emitter. Игрок не отслеживается."));
        position.y += position.height + 2f;
        bool point = mode.enumValueIndex == (int)EnemyStationaryDirectionMode.WorldPoint;
        EditorGUI.PropertyField(position, property.FindPropertyRelative(point ? "worldPoint" : "angle"),
            point ? new GUIContent("World Point", "Координаты цели X/Y в мире. Circle стреляет в эту точку от каждого emitter. Fan и Sector добавляют свои угловые смещения.")
                  : new GUIContent("World Angle", "0° вверх, 90° влево, 180° вниз, 270° вправо. Базовое направление не зависит от поворота врага. Fan, Sector, Radial и Layout Rotation применяются поверх него."));
        if (EditorGUI.EndChangeCheck()) legacy.boolValue = false;
        if (legacy.boolValue)
        {
            position.y += position.height + 2f; position.height = 42f;
            EditorGUI.HelpBox(position, "Старый Forward сохранён: используются прежние направления layout. Измените угол/точку или нажмите кнопку ниже, чтобы перейти на Stationary.", MessageType.Info);
            position.y += 44f; position.height = 20f;
            if (GUI.Button(position, new GUIContent("Use Fixed Direction", "Применить показанный угол или точку вместо сохранённого Forward."))) legacy.boolValue = false;
        }
        EditorGUI.EndProperty();
    }
}
