using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class VerifyWaveAuditGeometry
{
    public static object Run()
    {
        int checks = 0;
        var random = new System.Random(6317);
        for (int route = 0; route < 16; route++)
        {
            Vector3 a = Point(random), b = Point(random), c = Point(random), d = Point(random);
            foreach (DirectedWaveSegmentMotion motion in Enum.GetValues(typeof(DirectedWaveSegmentMotion)))
                for (int variant = 0; variant < 3; variant++)
                {
                    var start = new DirectedWaveCurveSettings { bulge = .4f, longArc = variant == 2,
                        tension = variant * .3f, parameterization = (DirectedWaveCurveParameterization)variant,
                        handleMode = variant == 0 ? DirectedWaveBezierHandleMode.Auto : DirectedWaveBezierHandleMode.Free,
                        outHandle = Vector3.right * 2f };
                    var end = start; end.inHandle = Vector3.up;
                    for (int step = 0; step <= 20; step++)
                    {
                        float t = step / 20f;
                        float before = LegacyDistance(a, b, c, d, motion, t, start, end);
                        float after = DirectedWavePathEvaluator.GetParameterAtNormalizedDistance(a, b, c, d, motion, t, start, end);
                        if (before != after) throw new Exception("Arc length changed: " + motion);
                        checks++;
                    }
                }
        }

        var root = PrefabUtility.LoadPrefabContents("Assets/Waves/2/2_3_extrime.prefab");
        long allocated;
        try
        {
            var wave = root.transform.Find("Main").GetComponent<DirectedEnemySubWave>();
            var formation = wave.Formation;
            Set(formation, "formationFrozen", false);
            Set(formation, "formationCoordinateSpace", DirectedWaveCoordinateSpace.World);
            Set(formation, "formationOffsetStep", Vector2.zero);
            Set(formation, "shapePointCount", 19);
            Vector3 center = new Vector3(1f, 2f);
            Set(formation, "formationCenter", center);
            Set(formation, "shapeRadius", 2f);
            Set(formation, "shapeFlattening", new Vector2(1.2f, .7f));
            foreach (var layout in new[] { DirectedWaveFormationLayout.Triangle, DirectedWaveFormationLayout.Square, DirectedWaveFormationLayout.Diamond })
            {
                Set(formation, "formationLayout", layout);
                Vector3[] vertices;
                float x = 2f * 1.2f, y = 2f * .7f;
                if (layout == DirectedWaveFormationLayout.Square)
                    vertices = new[] { center + new Vector3(-x, y), center + new Vector3(x, y), center + new Vector3(x, -y), center + new Vector3(-x, -y) };
                else if (layout == DirectedWaveFormationLayout.Diamond)
                    vertices = new[] { center + Vector3.up * y, center + Vector3.right * x, center + Vector3.down * y, center + Vector3.left * x };
                else
                {
                    vertices = new Vector3[3];
                    for (int i = 0; i < 3; i++)
                    {
                        float angle = (90f + 120f * i) * Mathf.Deg2Rad;
                        vertices[i] = center + new Vector3(Mathf.Cos(angle) * 2f * 1.2f, Mathf.Sin(angle) * 2f * .7f);
                    }
                }
                float perimeter = 0f;
                for (int i = 0; i < vertices.Length; i++) perimeter += Vector3.Distance(vertices[i], vertices[(i + 1) % vertices.Length]);
                for (int slot = 0; slot < 19; slot++)
                {
                    float remaining = perimeter * slot / 19;
                    Vector3 expected = vertices[0];
                    for (int i = 0; i < vertices.Length; i++)
                    {
                        float length = Vector3.Distance(vertices[i], vertices[(i + 1) % vertices.Length]);
                        if (remaining <= length) { expected = Vector3.LerpUnclamped(vertices[i], vertices[(i + 1) % vertices.Length], remaining / length); break; }
                        remaining -= length;
                    }
                    if (wave.GetSimulationFormationPosition(slot) != expected) throw new Exception("Formation changed: " + layout);
                    checks++;
                }
            }
            for (int i = 0; i < 100; i++) wave.GetSimulationFormationPosition(i % 19);
            long beforeAllocation = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 1000; i++) wave.GetSimulationFormationPosition(i % 19);
            allocated = GC.GetAllocatedBytesForCurrentThread() - beforeAllocation;
            if (allocated != 0) throw new Exception("Formation allocated " + allocated + " bytes");
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        return new { passed = true, checks, exactArcLengthCompatibility = true, polygonAllocationsFor1000Slots = allocated };
    }

    private static Vector3 Point(System.Random random) => new Vector3((float)random.NextDouble() * 8f, (float)random.NextDouble() * 8f);
    private static void Set(object value, string name, object field) => value.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).SetValue(value, field);
    private static float LegacyDistance(Vector3 a, Vector3 b, Vector3 c, Vector3 d, DirectedWaveSegmentMotion motion,
        float time, DirectedWaveCurveSettings start, DirectedWaveCurveSettings end)
    {
        if (motion == DirectedWaveSegmentMotion.Linear || time <= 0f || time >= 1f) return time;
        Vector3 previous = b;
        float total = 0f;
        for (int i = 1; i <= 24; i++)
        {
            Vector3 point = DirectedWavePathEvaluator.EvaluateSegment(a, b, c, d, motion, i / 24f, start, end);
            total += Vector3.Distance(previous, point); previous = point;
        }
        if (total <= Mathf.Epsilon) return time;
        float target = total * time, accumulated = 0f;
        previous = b;
        for (int i = 1; i <= 24; i++)
        {
            float parameter = i / 24f;
            Vector3 point = DirectedWavePathEvaluator.EvaluateSegment(a, b, c, d, motion, parameter, start, end);
            float length = Vector3.Distance(previous, point);
            if (accumulated + length >= target)
                return Mathf.Lerp((i - 1) / 24f, parameter, length <= Mathf.Epsilon ? 0f : (target - accumulated) / length);
            accumulated += length; previous = point;
        }
        return 1f;
    }
}
