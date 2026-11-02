using System;
using System.IO;
using UnityEngine;
using Game.Performance;

public static class ComparePerformanceRuns
{
    public static object Run(string before, string after)
    {
        string output = PerformanceSuiteEditor.Compare(before, after);
        return new { output, exists = File.Exists(output) };
    }
}
