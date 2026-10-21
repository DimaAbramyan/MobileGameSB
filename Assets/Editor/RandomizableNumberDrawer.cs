using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(RandomizableAttribute))]
public sealed class RandomizableNumberDrawer : PropertyDrawer
{
    private static float Line => EditorGUIUtility.singleLineHeight + 2f;
    private static SerializedProperty Range(SerializedProperty property, string name)
    {
        int dot = property.propertyPath.LastIndexOf('.');
        return property.serializedObject.FindProperty((dot >= 0 ? property.propertyPath.Substring(0, dot + 1) : "") + name);
    }
    private static float Height(SerializedProperty range, EnemyNumericScopes scopes)
        => EditorGUIUtility.singleLineHeight + (scopes != EnemyNumericScopes.None && range != null && range.FindPropertyRelative("enabled").boolValue ? Line : 0f);
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        var config = (RandomizableAttribute)attribute;
        float height = Height(Range(property, config.Range), config.Scopes);
        if (config.YRange != null) height += 2f + Height(Range(property, config.YRange), config.Scopes)
            + (property.propertyType == SerializedPropertyType.Vector3 ? Line : 0f);
        return height;
    }
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        var configuration = (RandomizableAttribute)attribute;
        if (configuration.YRange == null)
            DrawValue(position, property, label, Range(property, configuration.Range), configuration.Minimum, configuration.Scopes, configuration.DefaultScope);
        else
        {
            var x = Range(property, configuration.Range); var y = Range(property, configuration.YRange);
            position.height = Height(x, configuration.Scopes);
            DrawValue(position, property.FindPropertyRelative("x"), new GUIContent(label.text + " X", label.tooltip), x, configuration.Minimum, configuration.Scopes, configuration.DefaultScope);
            position.y += position.height + 2f; position.height = Height(y, configuration.Scopes);
            DrawValue(position, property.FindPropertyRelative("y"), new GUIContent(label.text + " Y", label.tooltip), y, configuration.Minimum, configuration.Scopes, configuration.DefaultScope);
            if (property.propertyType == SerializedPropertyType.Vector3)
            { position.y += position.height + 2f; position.height = EditorGUIUtility.singleLineHeight; EditorGUI.PropertyField(position, property.FindPropertyRelative("z"), new GUIContent(label.text + " Z")); }
        }
    }
    public static void Draw(Rect position, SerializedProperty property, GUIContent label, string rangeName, float minimum = float.MinValue)
    {
        DrawValue(position, property, label, Range(property, rangeName), minimum, EnemyNumericScopes.None, default);
    }
    private static void DrawValue(Rect position, SerializedProperty property, GUIContent label, SerializedProperty range,
        float minimum, EnemyNumericScopes scopes, EnemyNumericRandomizeOn defaultScope)
    {
        if (range == null) { EditorGUI.LabelField(position, label.text, "Missing random range"); return; }
        EditorGUI.BeginProperty(position, label, property);
        position.height = EditorGUIUtility.singleLineHeight;
        var enabled = range.FindPropertyRelative("enabled");
        Rect button = new(position.xMax - 24f, position.y, 24f, EditorGUIUtility.singleLineHeight);
        position.width -= 27f;
        bool next = GUI.Toggle(button, enabled.boolValue, new GUIContent("⚄", "Random: switch between fixed value and Min / Max. Both are preserved."), EditorStyles.miniButton);
        if (next != enabled.boolValue)
        {
            enabled.boolValue = next;
            var initialized = range.FindPropertyRelative("initialized");
            if (next && !initialized.boolValue)
            {
                var min = range.FindPropertyRelative("min"); var max = range.FindPropertyRelative("max");
                if (property.propertyType == SerializedPropertyType.Integer) min.intValue = max.intValue = property.intValue;
                else min.floatValue = max.floatValue = property.floatValue;
                initialized.boolValue = true;
                range.FindPropertyRelative("randomizeOn").intValue = (int)defaultScope;
            }
        }
        if (!enabled.boolValue)
        {
            Rect value = EditorGUI.PrefixLabel(position, label);
            if (property.propertyType == SerializedPropertyType.Integer) property.intValue = Mathf.Max(minimum == float.MinValue ? int.MinValue : Mathf.CeilToInt(minimum), EditorGUI.IntField(value, property.intValue));
            else property.floatValue = Mathf.Max(minimum, EditorGUI.FloatField(value, property.floatValue));
        }
        else
        {
            EditorGUI.BeginProperty(position, label, range);
            Rect values = EditorGUI.PrefixLabel(position, label);
            float width = (values.width - 5f) * .5f;
            float savedLabel = EditorGUIUtility.labelWidth; int indent = EditorGUI.indentLevel;
            EditorGUI.indentLevel = 0; EditorGUIUtility.labelWidth = 28f;
            var min = range.FindPropertyRelative("min"); var max = range.FindPropertyRelative("max");
            var a = new Rect(values.x, values.y, width, values.height);
            var b = new Rect(values.x + width + 5f, values.y, width, values.height);
            EditorGUI.PropertyField(a, min, new GUIContent("Min")); EditorGUI.PropertyField(b, max, new GUIContent("Max"));
            if (property.propertyType != SerializedPropertyType.Integer)
            { min.floatValue = Mathf.Max(minimum, min.floatValue); max.floatValue = Mathf.Max(minimum, max.floatValue); }
            else if (minimum != float.MinValue)
            { min.intValue = Mathf.Max(Mathf.CeilToInt(minimum), min.intValue); max.intValue = Mathf.Max(Mathf.CeilToInt(minimum), max.intValue); }
            EditorGUIUtility.labelWidth = savedLabel; EditorGUI.indentLevel = indent;
            EditorGUI.EndProperty();
        }
        if (enabled.boolValue && scopes != EnemyNumericScopes.None)
        {
            position.y += Line; position.width += 27f;
            var frequency = range.FindPropertyRelative("randomizeOn");
            var names = new System.Collections.Generic.List<GUIContent>(); var values = new System.Collections.Generic.List<int>();
            Add(EnemyNumericRandomizeOn.PerAttack, EnemyNumericScopes.PerAttack, "Per Attack");
            Add(EnemyNumericRandomizeOn.PerShot, EnemyNumericScopes.PerShot, "Per Shot");
            Add(EnemyNumericRandomizeOn.PerDirection, EnemyNumericScopes.PerDirection, "Per Direction");
            Add(EnemyNumericRandomizeOn.PerProjectile, EnemyNumericScopes.PerProjectile, "Per Projectile");
            frequency.intValue = EditorGUI.IntPopup(position, new GUIContent("Randomize On"), frequency.intValue, names.ToArray(), values.ToArray());
            void Add(EnemyNumericRandomizeOn value, EnemyNumericScopes flag, string text)
            { if ((scopes & flag) != 0) { names.Add(new GUIContent(text)); values.Add((int)value); } }
        }
        EditorGUI.EndProperty();
    }
}
