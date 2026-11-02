using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

public static class EnemyLaserScenePreview
{
    private static readonly Vector3[] corners = new Vector3[4];
    private static readonly List<EnemyLaserState> segments = new();
    public static void Draw(EnemyShootingSettings shooting, Vector3 position, Vector3 direction)
    {
        var geometry = shooting.Laser.Geometry;
        Vector3 start = position + (Vector3)geometry.LaserOffset;
        EnemyEmissionOriginSettings.TryGetGameplayBounds(WavePreviewGameplayCamera.GameplayCamera, start.z, out var bounds);
        float length = geometry.LengthAt(geometry.ResolveLength(start, direction, bounds), 0f);
        DrawPath(start, direction, length, geometry.WidthAt(0f), bounds, shooting.ScreenBoundary, new Color(1f, .4f, .15f, .12f));
        if (shooting.ScreenBoundary.BounceOnScreenBounds && shooting.Laser.EnableTelegraph)
        {
            geometry = shooting.Laser.TelegraphGeometry;
            start = position + (Vector3)geometry.LaserOffset;
            length = geometry.LengthAt(geometry.ResolveLength(start, direction, bounds), 0f);
            var colour = shooting.Laser.TelegraphColor;
            colour.a *= shooting.Laser.TelegraphOpacity;
            DrawPath(start, direction, length, geometry.WidthAt(0f), bounds, shooting.ScreenBoundary, colour);
        }
    }
    private static void DrawPath(Vector3 start, Vector3 direction, float length, float width, Rect bounds,
        EnemyProjectileScreenSettings screen, Color colour)
    {
        EnemyLaserGeometryUtility.BuildPath(start, direction, length, width, bounds, screen, segments);
        for (int i = 0; i < segments.Count; i++)
        {
            var segment = segments[i];
            DrawBox(new Vector3(segment.Start.x, segment.Start.y, start.z),
                new Vector3(segment.Direction.x, segment.Direction.y), segment.Length, segment.Width, colour);
        }
    }
    public static void DrawBox(Vector3 start, Vector3 direction, float length, float width, Color colour)
    {
        Vector3 side = new Vector3(-direction.y, direction.x) * width * .5f;
        corners[0] = start - side; corners[1] = start + side;
        corners[2] = start + direction * length + side; corners[3] = start + direction * length - side;
        Handles.DrawSolidRectangleWithOutline(corners, colour, new Color(colour.r, colour.g, colour.b, .8f));
    }

    // Uses SerializedProperty so numeric edits, scene handles, Undo and prefab overrides share data.
    public static void HandlesFor(SerializedObject owner, Vector3 enemyPosition, SimpleEnemyAttackExecutor executor = null, string rootPath = null, int slot = -1)
    {
        if (Application.isPlaying || !EnemyShootingPreview.ShowFormationVolley) return;
        using var property = rootPath == null ? owner.GetIterator() : owner.FindProperty(rootPath);
        using var end = rootPath == null ? null : property.GetEndProperty();
        int shown = 0;
        bool enterChildren = true;
        while (property.Next(enterChildren) && shown < 8 && (end == null || !SerializedProperty.EqualContents(property, end)))
        {
            enterChildren = true;
            // Stored inactive groups are not additional editable shooting sources.
            if (rootPath == null && property.propertyPath == "attackGroups") { enterChildren = false; continue; }
            if (property.type != nameof(EnemyShootingSettings)) continue;
            if (slot >= 0 && !AllowsSequenceSource(owner, property.propertyPath, slot)) continue;
            var shooting = EnemyPresetInspector.DisplaySettings(property, null);
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
            EnemyEmissionOriginSettings.TryGetGameplayBounds(WavePreviewGameplayCamera.GameplayCamera, start.z, out var bounds);
            float length = geometry.ResolveLength(start, direction, bounds), width = geometry.LaserWidth;
            if (EnemyPresetInspector.Linked(property, true) || EnemyPresetInspector.Linked(property, false))
            {
                // Inherited configuration is edited through explicit local overrides in the Inspector.
                if (shooting.ScreenBoundary.BounceOnScreenBounds) Draw(shooting, emitter.Position, direction);
                else DrawBox(start, direction, length, width, new Color(1f, .4f, .15f, .12f));
                continue;
            }
            if (shooting.ScreenBoundary.BounceOnScreenBounds) Draw(shooting, emitter.Position, direction);
            EnemyLaserGeometryUtility.BuildPath(start, direction, length, width, bounds, shooting.ScreenBoundary, segments);
            var last = segments[segments.Count - 1];
            var tipDirection = new Vector3(last.Direction.x, last.Direction.y);
            var endpoint = new Vector3(last.Start.x, last.Start.y, start.z) + tipDirection * last.Length;
            float firstLength = segments[0].Length;
            float handleSize = HandleUtility.GetHandleSize(start) * .07f;
            Handles.color = Color.yellow;
            EditorGUI.BeginChangeCheck();
            Vector3 tip = Handles.Slider(endpoint, tipDirection, handleSize, Handles.CubeHandleCap, 0f);
            if (EditorGUI.EndChangeCheck())
            {
                float next = Mathf.Max(0f, length + Vector3.Dot(tip - endpoint, tipDirection));
                if (geometry.LengthMode == EnemyLaserLengthMode.Custom) p.FindPropertyRelative("laserLength").floatValue = next;
                else p.FindPropertyRelative("lengthOffset").floatValue += next - length;
            }
            Vector3 side = new(-direction.y, direction.x);
            EditorGUI.BeginChangeCheck();
            Vector3 edge = Handles.Slider(start + direction * (firstLength * .5f) + side * width * .5f,
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
    private static bool AllowsSequenceSource(SerializedObject owner, string path, int slot)
    {
        while (path.LastIndexOf('.') is int dot && dot >= 0)
        {
            path = path.Substring(0, dot);
            var restricted = owner.FindProperty(path + ".overrideTargetSlots");
            if (restricted == null) continue;
            var stepType = owner.FindProperty(path + ".type");
            if (stepType != null && stepType.intValue != (int)DirectedWaveAttackSequenceStepType.Attack) return false;
            if (!restricted.boolValue) return true;
            var slots = owner.FindProperty(path + ".targetSlots");
            if (slots == null) return false;
            for (int i = 0; i < slots.arraySize; i++)
                if (slots.GetArrayElementAtIndex(i).intValue == slot) return true;
            return false;
        }
        return true;
    }
}
