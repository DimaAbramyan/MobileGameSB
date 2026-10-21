using System;
using System.Linq;
using UnityEditor.Profiling;
using UnityEditorInternal;
public static class InspectFastSeekProfiler
{
    public static object Markers()
    {
        using var view = ProfilerDriver.GetRawFrameDataView(ProfilerDriver.lastFrameIndex - 50, 0);
        var markers = new System.Collections.Generic.List<UnityEditor.Profiling.FrameDataView.MarkerInfo>(); view.GetMarkers(markers);
        return new { valid = view.valid, view.threadName, count = view.sampleCount,
            markers = markers.Where(m => m.name.Contains("FastSeek") || m.name.Contains("GetComponent")).Select(m => m.name).ToArray(),
            fixedId = view.GetMarkerId("FastSeek.FixedStep"),
            samples = Enumerable.Range(0, view.sampleCount).Where(i => view.GetSampleName(i).Contains("FastSeek") || view.GetSampleName(i).Contains("GetComponentNull")).Take(30).Select(i => new { name = view.GetSampleName(i), id = view.GetSampleMarkerId(i), index = i, children = view.GetSampleChildrenCountRecursive(i) }).ToArray() };
    }
    public static object Run() => new {
        driver = typeof(ProfilerDriver).GetProperties().Select(p => p.Name + ":" + p.PropertyType).ToArray(),
        raw = typeof(RawFrameDataView).GetMethods().Where(m => m.DeclaringType != typeof(object)).Select(m => m.ToString()).ToArray(),
        recorder = typeof(Unity.Profiling.ProfilerRecorderSample).GetMembers().Select(m => m.ToString()).ToArray()
    };
}
