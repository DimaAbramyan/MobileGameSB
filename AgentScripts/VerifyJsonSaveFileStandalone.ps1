$ErrorActionPreference = 'Stop'
$saveWorkspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$saveRoot = Join-Path $saveWorkspace ('AgentArtifacts/SaveFileSmoke/' + [Guid]::NewGuid().ToString('N'))
$saveSource = Get-Content -Raw -LiteralPath (Join-Path $saveWorkspace 'Assets/Scripts/Infrastructure/Saving/JsonSaveFile.cs')
$saveHarness = @'
public static class SaveFileStandaloneChecks
{
    public static string[] Run(string directory)
    {
        System.IO.Directory.CreateDirectory(directory);
        var checks = new System.Collections.Generic.List<string>();
        string path = System.IO.Path.Combine(directory, "save.json");
        System.Func<Newtonsoft.Json.Linq.JObject, bool> valid = d => d["value"]?.Type == Newtonsoft.Json.Linq.JTokenType.Integer;
        JsonSaveFile.Write(path, "{\"value\":1}", valid);
        Check(JsonSaveFile.Read(path, valid).Status == SaveLoadStatus.Loaded, "Initial version/checksum read", checks);
        JsonSaveFile.Write(path, "{\"value\":2}", valid);
        System.IO.File.WriteAllText(path, "{broken");
        var recovered = JsonSaveFile.Read(path, valid);
        Check(recovered.Status == SaveLoadStatus.Recovered && recovered.Document["value"].Value<int>() == 1, "Backup recovery", checks);
        JsonSaveFile.Write(path, "{\"value\":3}", valid);
        Check(JsonSaveFile.Read(path, valid).Document["value"].Value<int>() == 3, "Replacement after corruption", checks);
        string current = System.IO.File.ReadAllText(path);
        System.IO.File.WriteAllText(path, current.Replace("\"formatVersion\": 1", "\"formatVersion\": 0"));
        Check(JsonSaveFile.Read(path, valid).Status == SaveLoadStatus.Recovered, "Damaged version cannot bypass checksum", checks);
        System.IO.File.WriteAllText(path, "{\"formatVersion\":1,\"value\":1,\"checksum\":{}}");
        Check(JsonSaveFile.Read(path, valid).Status == SaveLoadStatus.Recovered, "Invalid checksum token recovered without exception", checks);
        System.IO.File.WriteAllText(path, "{\"formatVersion\":999,\"value\":999}");
        Check(JsonSaveFile.Read(path, valid).Status == SaveLoadStatus.UnsupportedVersion, "Future version protected", checks);
        bool blocked = false;
        try { JsonSaveFile.Write(path, "{\"value\":0}", valid); } catch (System.IO.IOException) { blocked = true; }
        Check(blocked && System.IO.File.ReadAllText(path).Contains("999"), "Future version cannot be overwritten", checks);
        JsonSaveFile.Delete(path);
        Check(JsonSaveFile.Read(path, valid).Status == SaveLoadStatus.Missing, "Explicit deletion removes recovery candidates", checks);
        JsonSaveFile.Write(path, "{\"value\":4,\"id\":\"2026-10-21T13:00:00+08:00\"}", valid);
        var literal = JsonSaveFile.Read(path, valid);
        Check(literal.Document["id"].Type == Newtonsoft.Json.Linq.JTokenType.String
            && literal.Document["id"].Value<string>() == "2026-10-21T13:00:00+08:00", "Date-shaped strings remain strings", checks);
        using (var stream = new System.IO.FileStream(path, System.IO.FileMode.Open, System.IO.FileAccess.ReadWrite, System.IO.FileShare.None))
            Check(JsonSaveFile.Read(path, valid).Status == SaveLoadStatus.Unavailable, "Locked primary does not silently fall back", checks);
        var tasks = new System.Threading.Tasks.Task[32];
        for (int i = 0; i < tasks.Length; i++)
        {
            int number = i;
            tasks[i] = System.Threading.Tasks.Task.Run(() => JsonSaveFile.Write(path, "{\"value\":" + number + "}", valid));
        }
        System.Threading.Tasks.Task.WaitAll(tasks);
        Check(JsonSaveFile.Read(path, valid).Status == SaveLoadStatus.Loaded, "32 concurrent writers leave valid JSON/integrity", checks);
        System.IO.File.Copy(path, path + ".tmp");
        System.IO.File.Delete(path);
        Check(JsonSaveFile.Read(path, valid).Status == SaveLoadStatus.Recovered, "Interrupted replace temp recovery", checks);
        return checks.ToArray();
    }
    private static void Check(bool condition, string message, System.Collections.Generic.List<string> checks)
    {
        if (!condition) throw new System.InvalidOperationException(message);
        checks.Add(message);
    }
}
'@
$saveReferences = @(Get-ChildItem -LiteralPath (Join-Path $PSHOME 'ref') -Filter '*.dll' | Select-Object -ExpandProperty FullName)
$saveReferences += [Newtonsoft.Json.Linq.JObject].Assembly.Location
Add-Type -TypeDefinition ($saveSource + [Environment]::NewLine + $saveHarness) -ReferencedAssemblies ($saveReferences | Select-Object -Unique) -CompilerOptions '/nowarn:1701'
$saveChecks = [SaveFileStandaloneChecks]::Run($saveRoot)
[pscustomobject]@{ success = $true; count = $saveChecks.Count; checks = $saveChecks; root = $saveRoot } | ConvertTo-Json -Depth 4
