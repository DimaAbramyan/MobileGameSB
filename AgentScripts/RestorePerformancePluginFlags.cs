using System;
using System.IO;
using UnityEditor;

public static class RestorePerformancePluginFlags
{
    public static object Run()
    {
        int changed = 0, checkedCount = 0;
        foreach (string line in File.ReadAllLines("AgentArtifacts/Performance/fmod-plugin-baseline.tsv"))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            string[] fields = line.Split('\t');
            if (fields.Length != 2 || !fields[0].StartsWith("Assets/Plugins/FMOD/platforms/android/", StringComparison.Ordinal)
                || !fields[0].EndsWith(".so", StringComparison.Ordinal) || (fields[1] != "0" && fields[1] != "1"))
                throw new InvalidDataException("Unexpected importer baseline entry.");
            var importer = AssetImporter.GetAtPath(fields[0]) as PluginImporter;
            if (importer == null) throw new InvalidDataException("Missing importer: " + fields[0]);
            bool expected = fields[1] == "1";
            if (importer.GetCompatibleWithPlatform(BuildTarget.Android) != expected)
            {
                importer.SetCompatibleWithPlatform(BuildTarget.Android, expected);
                importer.SaveAndReimport();
                changed++;
            }
            if (importer.GetCompatibleWithPlatform(BuildTarget.Android) != expected)
                throw new InvalidOperationException("Importer was not restored: " + fields[0]);
            checkedCount++;
        }
        return new { passed = checkedCount == 16, checkedCount, changed };
    }
}
