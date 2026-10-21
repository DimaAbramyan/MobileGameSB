using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
public static class VerifyFastSeekProfiling
{
    public static async Task<object> Run()
    {
        var fixture = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("VerifyWavePreviewFastSeek")).LastOrDefault(t => t != null);
        if (fixture == null) throw new Exception("First run VerifyWavePreviewFastSeek.Run using its existing script.");
        WavePreviewProfiler.Current = new WavePreviewProfiler.Recording();
        try
        {
            var task = (Task)fixture.GetMethod("Run", BindingFlags.Public | BindingFlags.Static).Invoke(null, null);
            await task;
            return new { instrumented = task.GetType().GetProperty("Result").GetValue(task), profilerSteps = WavePreviewProfiler.Current.Steps };
        }
        finally { WavePreviewProfiler.Reset(); }
    }
}
