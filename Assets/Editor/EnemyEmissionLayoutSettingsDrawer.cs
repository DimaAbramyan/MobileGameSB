using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(EnemyEmissionLayoutSettings))]
public sealed class EnemyEmissionLayoutSettingsDrawer : PropertyDrawer
{
    private static readonly GUIContent[] LaunchOrders = { new("Forward"), new("Reverse"), new("PingPong") };
    private static IEnumerable<string> Fields(SerializedProperty property)
    {
        yield return "type";
        switch ((EnemyEmissionLayoutType)property.FindPropertyRelative("type").enumValueIndex)
        {
            case EnemyEmissionLayoutType.Point:
                yield return "positionOffset";
                break;
            case EnemyEmissionLayoutType.Radial:
                yield return "directionsCount";
                yield return "startAngle";
                if (!EnemyChildAttackSettingsDrawer.IsChildPattern(property)) yield return "rotateShootingPointOffset";
                break;
            case EnemyEmissionLayoutType.Circle:
                yield return "emitterCount";
                yield return "radius";
                yield return "startAngle";
                yield return "centerOffset";
                break;
            case EnemyEmissionLayoutType.Sector:
                yield return "startAngle";
                yield return "endAngle";
                yield return "directionsCount";
                yield return "launchMode";
                if (property.FindPropertyRelative("launchMode").enumValueIndex == (int)EnemySectorLaunchMode.Sequential)
                {
                    yield return "startDelay";
                    yield return "launchInterval";
                    if (!EnemyChildAttackSettingsDrawer.IsChildPattern(property)) yield return "waitUntilSectorReady";
                    yield return "launchOrder";
                    int order = property.FindPropertyRelative("launchOrder").intValue;
                    if (order == (int)EnemySectorLaunchOrder.PingPong)
                    { yield return "initialDirection"; yield return "resetOrderOnAttackStart"; }
                }
                break;
        }
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        float height = EditorGUIUtility.singleLineHeight + 2f;
        foreach (string field in Fields(property))
            height += EditorGUI.GetPropertyHeight(property.FindPropertyRelative(field), true) + 2f;
        return height;
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);
        position.height = EditorGUIUtility.singleLineHeight;
        EditorGUI.LabelField(position, label, EditorStyles.boldLabel);
        EditorGUI.indentLevel++;
        foreach (string field in Fields(property))
        {
            position.y += position.height + 2f;
            var child = property.FindPropertyRelative(field);
            position.height = EditorGUI.GetPropertyHeight(child, true);
            var fieldLabel = field switch
            {
                "type" => new GUIContent("Emission Layout", "Point: одна точка с локальным offset. Radial: равномерные направления на 360° из одной точки. Circle: точки на окружности. Sector: направления между двумя углами. Fan и Stack применяются к каждому направлению/emitter."),
                "positionOffset" => new GUIContent("Position Offset", "Local offset from the shooting point; Layout Rotation rotates this offset."),
                "directionsCount" => new GUIContent("Directions Count", "Radial: evenly spaced over 360 degrees. Sector: includes both boundaries; one direction uses the midpoint. Fan and Stack apply per direction."),
                "emitterCount" => new GUIContent("Emitter Count", "Shooting points evenly distributed around the circle. Fan/Stack apply to each point."),
                "radius" => new GUIContent("Radius", "World distance from the circle center to each shooting point. Non-uniform ship scale does not turn the circle into an ellipse."),
                "centerOffset" => new GUIContent("Center Offset", "Local offset from the enemy. Enemy Facing does not move the emission center."),
                "startAngle" => new GUIContent("Start Angle", "Counterclockwise angle in degrees from layout up. With Aimed, Sector is centered on the target before Layout Rotation and Fan."),
                "endAngle" => new GUIContent("End Angle", "Counterclockwise sweep from Start Angle, including both ends. 350 to 10 crosses zero; 0 to 360 is a full turn. With Aimed, the middle of this sweep points at the target."),
                "launchMode" => new GUIContent("Launch Mode", "Simultaneous: все направления Sector одновременно. Sequential: по очереди с Start Delay и Launch Interval."),
                "startDelay" => new GUIContent("Start Delay", "Seconds before the first direction in each sector volley."),
                "launchInterval" => new GUIContent("Launch Interval", "Seconds between sector directions. Fan/Stack within a direction still fire together."),
                "launchOrder" => new GUIContent("Launch Order", "Forward: Start Angle → End Angle. Reverse: End Angle → Start Angle. PingPong alternates after each completed sector."),
                "initialDirection" => new GUIContent("Initial Direction", "Direction of the first PingPong sector."),
                "resetOrderOnAttackStart" => new GUIContent("Reset Order On Attack Start", "On: every new attack starts from Initial Direction. Off: continue from the last completed sector. Cancelling a sector does not advance the order."),
                "waitUntilSectorReady" => new GUIContent("Wait Until Sector Ready", "Wait for the wave formation to be ready before starting Start Delay. Off allows entrance-time launches. Without a wave, the sector is ready immediately."),
                "rotateShootingPointOffset" => new GUIContent("Rotate Shooting Point Offset", "Preserves the moving shooting origin of old Single Rotation profiles. Independent of Enemy Facing."),
                _ => new GUIContent(child.displayName)
            };
            if (field == "launchOrder")
            {
                var legacyReverse = property.FindPropertyRelative("reverseOrder");
                int selected = child.intValue == (int)EnemySectorLaunchOrder.PingPong ? child.intValue
                    : legacyReverse.boolValue ? (int)EnemySectorLaunchOrder.Reverse : (int)EnemySectorLaunchOrder.Forward;
                EditorGUI.BeginChangeCheck();
                selected = EditorGUI.Popup(position, fieldLabel, selected, LaunchOrders);
                if (EditorGUI.EndChangeCheck())
                { child.intValue = selected; legacyReverse.boolValue = selected == (int)EnemySectorLaunchOrder.Reverse; }
            }
            else EditorGUI.PropertyField(position, child, fieldLabel, true);
        }
        EditorGUI.indentLevel--;
        EditorGUI.EndProperty();
    }
}
