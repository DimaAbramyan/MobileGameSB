using System.Collections.Generic;
using Unity.Profiling.LowLevel.Unsafe;

public static class InspectFreezeProfilerMarkers
{
    public static object Run()
    {
        var handles = new List<ProfilerRecorderHandle>();
        ProfilerRecorderHandle.GetAvailable(handles);
        var result = new List<object>();
        foreach (var handle in handles)
        {
            var d = ProfilerRecorderHandle.GetDescription(handle);
            if (d.Name.Contains("GC.") || d.Name.Contains("Main Thread") || d.Name.Contains("WaitFor")
                || d.Name.Contains("Projectile") || d.Name.Contains("Burst") || d.Name.Contains("Magnite")
                || d.Name.Contains("Physics2D") || d.Name.Contains("Instantiate") || d.Name.Contains("Shader"))
                result.Add(new { name = d.Name, category = d.Category.Name, unit = d.UnitType.ToString() });
        }
        return result;
    }
}
