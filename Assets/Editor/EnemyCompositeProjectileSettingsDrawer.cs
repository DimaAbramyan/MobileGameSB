using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(EnemyCompositeProjectileSettings))]
public sealed class EnemyCompositeProjectileSettingsDrawer : PropertyDrawer
{
    private static float Line => EditorGUIUtility.singleLineHeight+2;
    private static SerializedProperty Shooting(SerializedProperty p)
    { int dot=p.propertyPath.LastIndexOf('.'); return dot<0 ? null : p.serializedObject.FindProperty(p.propertyPath.Substring(0,dot)); }
    private static EnemyCompositeShape CurrentShape(SerializedProperty p)
    {
        int legacy = Shooting(p)?.FindPropertyRelative("projectileType")?.intValue ?? 4;
        return legacy == 5 ? EnemyCompositeShape.RegularPolygon : legacy == 6 ? EnemyCompositeShape.SegmentedRing
            : (EnemyCompositeShape)p.FindPropertyRelative("shape").intValue;
    }
    private static IEnumerable<string> Shape(SerializedProperty p)
    {
        yield return "shape";
        switch(CurrentShape(p))
        {
            case EnemyCompositeShape.RegularPolygon: yield return "numberOfSides"; yield return "projectilesPerSide"; break;
            case EnemyCompositeShape.SegmentedRing: yield return "segmentCount"; yield return "projectilesPerSegment"; yield return "gapAngle"; break;
            default: yield return "componentCount"; break;
        }
        yield return "radiusMode";
        if(p.FindPropertyRelative("radiusMode").enumValueIndex==1)yield return "radius";
        else yield return "spacingGap";
        yield return "startAngle";
    }
    private static IEnumerable<string> Rotation(SerializedProperty p)
    {
        yield return "rotationMode"; yield return "initialRotation";
        int mode=p.FindPropertyRelative("rotationMode").enumValueIndex;
        if(mode==1 || mode==2)yield return "angularSpeed";
        if(mode==2) {yield return "minAngle";yield return "maxAngle";}
    }
    private static IEnumerable<string> Components(SerializedProperty p)
    {
        yield return "componentScale"; yield return "overrideHitRadius";
        if(p.FindPropertyRelative("overrideHitRadius").boolValue)yield return "hitRadius";
        yield return "damage"; yield return "damageType";
    }
    private static string Warning(SerializedProperty p)
    {
        var shooting=Shooting(p);
        var prefab=shooting?.FindPropertyRelative("projectilePrefab")?.objectReferenceValue as EnemyBullet;
        if(prefab!=null && prefab.TryGetComponent<EnemyProjectileAuthoring>(out var source) && source.FlightMode==EnemyProjectileFlightMode.BurstAtPoint)
            return "Burst At Point component prefab is incompatible. Select Default or Homing; use Child Attack for secondary emission.";
        var child=shooting?.FindPropertyRelative("childAttack");
        if(shooting?.FindPropertyRelative("useChildAttackPattern")?.boolValue==true && child!=null
            && child.FindPropertyRelative("triggerMode").intValue==(int)EnemyChildAttackTriggerMode.OnEvent
            && child.FindPropertyRelative("eventType").intValue==(int)EnemyChildAttackEvent.HitPlayer)
            return "Child Attack Hit Player is incompatible: the center has no hitbox. Choose time, distance, expiry or screen boundary.";
        if(CurrentShape(p)==EnemyCompositeShape.SegmentedRing && p.FindPropertyRelative("gapAngle").floatValue >= 360f/Mathf.Max(1,p.FindPropertyRelative("segmentCount").intValue))
            return "Gap Angle must be smaller than 360 / Segment Count. The effective gap is limited to leave room for components.";
        if(CurrentShape(p)==EnemyCompositeShape.RegularPolygon && p.FindPropertyRelative("numberOfSides").intValue*(p.FindPropertyRelative("projectilesPerSide").intValue-1)>256
            || CurrentShape(p)==EnemyCompositeShape.SegmentedRing && p.FindPropertyRelative("segmentCount").intValue*p.FindPropertyRelative("projectilesPerSegment").intValue>256)
            return "Maximum 256 components. Elements per side/segment are limited uniformly to keep a complete figure.";
        return null;
    }
    private static IEnumerable<string> Sections(SerializedProperty property)
    {
        yield return "#Composite Shape & Spacing";
        foreach (string name in Shape(property)) yield return name;
        yield return "#Composite Rotation";
        foreach (string name in Rotation(property)) yield return name;
        yield return "#Composite Components";
        foreach (string name in Components(property)) yield return name;
    }
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label) =>
        WaveInspectorSections.Height(property, Sections(property), name => EditorGUI.GetPropertyHeight(property.FindPropertyRelative(name)))
        + Line + (WaveInspectorSections.Expanded(property, "Advanced / Composite Limits") ? 60f : 0f)
        + (Warning(property) != null ? 60f : 0f);

    public override void OnGUI(Rect rect, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(rect, label, property);
        WaveInspectorSections.Draw(ref rect, property, Sections(property),
            name => EditorGUI.GetPropertyHeight(property.FindPropertyRelative(name)),
            (row, name) => Fields(ref row, property, new[] { name }));
        if (WaveInspectorSections.Header(ref rect, property, "Advanced / Composite Limits"))
        {
            rect.height = 56f;
            EditorGUI.HelpBox(rect, "Component Prefab, colour and Normal/Fragile use the shared Projectile Settings fields. Size curves scale the whole figure. Child attacks originate at the center; nested compositions inside components are not supported. Max 256 elements per figure.", MessageType.Info);
            rect.y += 60f;
        }
        string warning = Warning(property);
        if (warning != null)
        {
            rect.height = 56f;
            EditorGUI.HelpBox(rect, warning, MessageType.Warning);
        }
        EditorGUI.EndProperty();
    }
    private static void Fields(ref Rect r,SerializedProperty p,IEnumerable<string> fields)
    {
        foreach(string name in fields)
        {
            var field=p.FindPropertyRelative(name);
            r.height=EditorGUI.GetPropertyHeight(field);
            if(name == "shape")
            {
                bool mixed = EditorGUI.showMixedValue;
                EditorGUI.showMixedValue = field.hasMultipleDifferentValues;
                EditorGUI.BeginChangeCheck();
                var selected = (EnemyCompositeShape)EditorGUI.EnumPopup(r, new GUIContent("Composite Shape"), CurrentShape(p));
                if(EditorGUI.EndChangeCheck())
                {
                    field.intValue = (int)selected;
                    var type = Shooting(p)?.FindPropertyRelative("projectileType");
                    if(type != null) type.intValue = (int)EnemyProjectileType.Composite;
                }
                EditorGUI.showMixedValue = mixed;
            }
            else EditorGUI.PropertyField(r,field);
            r.y+=r.height+2;
            r.height=EditorGUIUtility.singleLineHeight;
        }
    }
}
