$ErrorActionPreference = 'Stop'
$auditWorkspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$auditRoot = Join-Path $auditWorkspace ('AgentArtifacts/SaveProtocolAudit/' + [Guid]::NewGuid().ToString('N'))
$auditSource = Get-Content -Raw -LiteralPath (Join-Path $auditWorkspace 'Assets/Scripts/Infrastructure/Saving/JsonSaveFile.cs')
$auditHarness = @'
public static class SaveProtocolAuditChecks
{
    public sealed class Result
    {
        public int checks;
        public System.Collections.Generic.List<string> passed = new();
        public System.Collections.Generic.List<string> failures = new();
    }
    private static void Check(bool value, string message, Result result)
    { result.checks++; if (!value) throw new System.Exception(message); }
    private static void Scenario(string name, System.Action action, Result result)
    { try { action(); result.passed.Add(name); } catch (System.Exception error) { result.failures.Add(name + ": " + error.GetType().Name + ": " + error.Message); } }
    public static Result Run(string directory)
    {
        System.IO.Directory.CreateDirectory(directory);
        var result = new Result();
        System.Func<Newtonsoft.Json.Linq.JObject, bool> valid = d => d["value"]?.Type == Newtonsoft.Json.Linq.JTokenType.Integer;
        string path = System.IO.Path.Combine(directory, "profile.json");
        JsonSaveFile.Write(path, "{\"value\":1,\"text\":\"Тест 简体中文\"}", valid);
        byte[] golden = System.IO.File.ReadAllBytes(path);
        Scenario("Every truncated UTF-8 prefix recovers without mutating files", () => {
            byte[] backup = System.IO.File.ReadAllBytes(path + ".bak");
            for (int length = 0; length < golden.Length; length++) {
                byte[] truncated = new byte[length]; System.Array.Copy(golden, truncated, length);
                System.IO.File.WriteAllBytes(path, truncated);
                Check(JsonSaveFile.Read(path, valid).Status == SaveLoadStatus.Recovered, "Truncated prefix " + length, result);
                Check(System.Convert.ToBase64String(System.IO.File.ReadAllBytes(path)) == System.Convert.ToBase64String(truncated), "Read never rewrites corrupt primary", result);
            }
            Check(System.Convert.ToBase64String(System.IO.File.ReadAllBytes(path + ".bak")) == System.Convert.ToBase64String(backup), "Recovery preserves backup", result);
        }, result);
        Scenario("Malformed metadata and duplicate/trailing JSON recover", () => {
            foreach (string json in new[] {
                "{\"value\":1,\"formatVersion\":{}}", "{\"value\":1,\"formatVersion\":null}",
                "{\"value\":1,\"formatVersion\":1.5}", "{\"value\":1,\"formatVersion\":\"1\"}",
                "{\"value\":1,\"value\":2}", "{\"value\":1}{}", "{\"value\":1,\"formatVersion\":-1}"
            }) {
                System.IO.File.WriteAllText(path, json);
                Check(JsonSaveFile.Read(path, valid).Status == SaveLoadStatus.Recovered, "Invalid metadata/JSON does not escape recovery", result);
            }
        }, result);
        foreach (string version in new[] { "2147483648", "999999999999999999999999999999999999999999999999999" }) {
            Scenario("Oversized future version protected: " + version, () => {
                string json = "{\"value\":999,\"formatVersion\":" + version + "}";
                System.IO.File.WriteAllText(path, json);
                Check(JsonSaveFile.Read(path, valid).Status == SaveLoadStatus.UnsupportedVersion, "Future version must not fall back to an older backup", result);
                bool blocked = false;
                try { JsonSaveFile.Write(path, "{\"value\":0}", valid); } catch (System.IO.IOException) { blocked = true; }
                Check(blocked && System.IO.File.ReadAllText(path) == json, "Future data cannot be overwritten", result);
            }, result);
        }
        Scenario("Oversized negative version recovers without an escaping exception", () => {
            System.IO.File.WriteAllText(path, "{\"value\":1,\"formatVersion\":-99999999999999999999999999999999999999999999999999}");
            Check(JsonSaveFile.Read(path, valid).Status == SaveLoadStatus.Recovered, "Invalid negative BigInteger metadata recovers", result);
        }, result);
        return result;
    }
}
'@
$auditReferences = @(Get-ChildItem -LiteralPath (Join-Path $PSHOME 'ref') -Filter '*.dll' | Select-Object -ExpandProperty FullName) + [Newtonsoft.Json.Linq.JObject].Assembly.Location
Add-Type -TypeDefinition ($auditSource + [Environment]::NewLine + $auditHarness) -ReferencedAssemblies ($auditReferences | Select-Object -Unique) -CompilerOptions '/nowarn:1701'
$auditResult = [SaveProtocolAuditChecks]::Run($auditRoot)
$auditResult | ConvertTo-Json -Depth 6 | Tee-Object -FilePath (Join-Path $auditWorkspace 'AgentArtifacts/technical-audit-save-protocol.json')
if ($auditResult.failures.Count -gt 0) { exit 1 }
