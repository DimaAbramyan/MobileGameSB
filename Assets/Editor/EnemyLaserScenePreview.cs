using UnityEditor;
using UnityEngine;

public static class EnemyLaserScenePreview
{
    private static readonly Vector3[] corners = new Vector3[4];
    public static void Draw(EnemyShootingSettings shooting, Vector3 position, Vector3 direction)
    {
        var geometry = shooting.Laser.Geometry;
        Vector3 start = position + (Vector3)geometry.LaserOffset;
        EnemyEmissionOriginSettings.TryGetGameplayBounds(Camera.main, start.z, out var bounds);
        float length = geometry.LengthAt(geometry.ResolveLength(start, direction, bounds), 0f);
        DrawBox(start, direction, length, geometry.WidthAt(0f), new Color(1f, .4f, .15f, .12f));
    }
    public static void DrawBox(Vector3 start, Vector3 direction, float length, float width, Color colour)
    {
        Vector3 side = new Vector3(-direction.y, direction.x) * width * .5f;
        corners[0] = start - side; corners[1] = start + side;
        corners[2] = start + direction * length + side; corners[3] = start + direction * length - side;
        Handles.DrawSolidRectangleWithOutline(corners, colour, new Color(colour.r, colour.g, colour.b, .8f));
    }

    // Uses SerializedProperty so numeric edits, scene handles, Undo and prefab overrides share data.
    public static void HandlesFor(SerializedObject owner, Vector3 enemyPosition, SimpleEnemyAttackExecutor executor = null)
    {
        if (Application.isPlaying || !EnemyShootingPreview.ShowFormationVolley) return;
        var property = owner.GetIterator();
        int shown = 0;
        while (property.Next(true) && shown < 8)
        {
            if (property.type != nameof(EnemyShootingSettings)) continue;
            var shooting = EnemyPresetInspector.DisplayShooting(property, null).boxedValue as EnemyShootingSettings;
            if (shooting == null || shooting.ProjectileType != EnemyProjectileType.Laser) continue;
            shown++;
            var frame = new EnemyEmissionPreviewFrame { EnemyPosition = enemyPosition, ShootingPoint = enemyPosition,
                Scale = Vector3.one, EnemyBasis = Quaternion.identity, PointBasis = Quaternion.identity };
            Quaternion basis = Quaternion.identity;
            if (executor != null) frame = EnemyShootingPreview.CreateFrame(executor, shooting, enemyPosition, 0f, null, out basis);
            var emitter = EnemyShootingPreviewGeometry.GetEmitter(shooting, 0, frame, basis, 0f);
            var stationary = FindStationary(owner, property.propertyPath);
            Vector3 direction = stationary != null ? Quaternion.Euler(0f, 0f, stationary.FindPropertyRelative("angle").floatValue) * Vector3.up : emitter.BaseDirection;
            var firing = shooting.PresetFiring;
            if (firing.enabled && firing.fireMode == DirectedWaveAttackFireMode.Stationary)
            {
                var settings = firing.stationaryFireSettings;
                direction = settings.DirectionMode == EnemyStationaryDirectionMode.WorldPoint
                    ? ((Vector3)settings.WorldPoint - emitter.Position).normalized : (Vector3)settings.Direction;
            }
            var geometry = shooting.Laser.Geometry;
            var p = property.FindPropertyRelative("laser.geometry");
            Vector3 start = emitter.Position + (Vector3)geometry.LaserOffset;
            EnemyEmissionOriginSettings.TryGetGameplayBounds(Camera.main, start.z, out var bounds);
            float length = geometry.ResolveLength(start, direction, bounds), width = geometry.LaserWidth;
            if (EnemyPresetInspector.Linked(property, true) || EnemyPresetInspector.Linked(property, false))
            {
                // Inherited configuration is edited through explicit local overrides in the Inspector.
                DrawBox(start, direction, length, width, new Color(1f, .4f, .15f, .12f));
                continue;
            }
            float handleSize = HandleUtility.GetHandleSize(start) * .07f;
            Handles.color = Color.yellow;
            EditorGUI.BeginChangeCheck();
            Vector3 tip = Handles.Slider(start + direction * length, direction, handleSize, Handles.CubeHandleCap, 0f);
            if (EditorGUI.EndChangeCheck())
            {
                float next = Mathf.Max(0f, Vector3.Dot(tip - start, direction));
                if (geometry.LengthMode == EnemyLaserLengthMode.Custom) p.FindPropertyRelative("laserLength").floatValue = next;
                else p.FindPropertyRelative("lengthOffset").floatValue += next - length;
            }
            Vector3 side = new(-direction.y, direction.x);
            EditorGUI.BeginChangeCheck();
            Vector3 edge = Handles.Slider(start + direction * (length * .5f) + side * width * .5f,
                side, handleSize, Handles.CubeHandleCap, 0f);
            if (EditorGUI.EndChangeCheck()) p.FindPropertyRelative("laserWidth").floatValue = Mathf.Max(0f, Vector3.Dot(edge - start, side) * 2f);
            if (stationary != null || shooting.EmissionLayout.Type != EnemyEmissionLayoutType.Point)
            {
                EditorGUI.BeginChangeCheck();
                Quaternion turn = Handles.Disc(Quaternion.FromToRotation(Vector3.up, direction), start, Vector3.forward,
                    Mathf.Max(.4f, handleSize * 6f), false, 0f);
                if (EditorGUI.EndChangeCheck())
                {
                    float delta = Vector3.SignedAngle(direction, turn * Vector3.up, Vector3.forward);
                    if (stationary != null) stationary.FindPropertyRelative("angle").floatValue += delta;
                    else
                    {
                        var layout = property.FindPropertyRelative("emissionLayout");
                        layout.FindPropertyRelative("startAngle").floatValue += delta;
                        if (shooting.EmissionLayout.Type == EnemyEmissionLayoutType.Sector) layout.FindPropertyRelative("endAngle").floatValue += delta;
                    }
                }
            }
        }
        owner.ApplyModifiedProperties();
    }
    private static SerializedProperty FindStationary(SerializedObject owner, string path)
    {
        while (path.LastIndexOf('.') >= 0)
        {
            path = path.Substring(0, path.LastIndexOf('.'));
            var mode = owner.FindProperty(path + ".fireMode");
            var stationary = owner.FindProperty(path + ".stationaryFireSettings");
            if (mode == null || stationary == null) continue;
            return mode.intValue == (int)DirectedWaveAttackFireMode.Stationary
                && stationary.FindPropertyRelative("directionMode").intValue == (int)EnemyStationaryDirectionMode.Angle
                && !stationary.FindPropertyRelative("legacyForward").boolValue ? stationary : null;
        }
        return null;
    }
}
