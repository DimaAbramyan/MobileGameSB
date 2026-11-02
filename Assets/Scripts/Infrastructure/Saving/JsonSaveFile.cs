using System;
using System.IO;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

public enum SaveLoadStatus
{
    Missing,
    Loaded,
    Recovered,
    Corrupt,
    UnsupportedVersion,
    Unavailable
}

internal sealed class JsonSaveReadResult
{
    public SaveLoadStatus Status;
    public JObject Document;
    public int Version;
    public string Error;
    public bool HasData => Document != null;
}

/// <summary>Shared disk protocol for the existing craft saves and the player profile.</summary>
internal static class JsonSaveFile
{
    public const int CurrentVersion = 1;
    private const string VersionKey = "formatVersion";
    private const string ChecksumKey = "checksum";
    private static readonly object FileGate = new();
    internal static object SyncRoot => FileGate;
    private static readonly UTF8Encoding Utf8 = new(false, true);

    public static JsonSaveReadResult Read(string path, Func<JObject, bool> validate)
    {
        lock (FileGate)
        {
            var primary = ReadOne(path, validate);
            // Never downgrade a newer primary or overwrite a file that cannot be read.
            if (primary.HasData || primary.Status == SaveLoadStatus.UnsupportedVersion
                || primary.Status == SaveLoadStatus.Unavailable)
                return primary;

            foreach (string suffix in new[] { ".tmp", ".bak", ".bak.tmp" })
            {
                var candidate = ReadOne(path + suffix, validate);
                if (candidate.Status == SaveLoadStatus.UnsupportedVersion || candidate.Status == SaveLoadStatus.Unavailable)
                    return candidate;
                if (candidate.HasData)
                {
                    candidate.Status = SaveLoadStatus.Recovered;
                    candidate.Error = $"Recovered '{Path.GetFileName(path)}' from {suffix}.";
                    return candidate;
                }
                if (candidate.Status != SaveLoadStatus.Missing)
                    primary = candidate;
            }
            return primary;
        }
    }

    public static void Write(string path, string json, Func<JObject, bool> validate, bool overwrite = true)
    {
        lock (FileGate)
        {
            JObject document = Parse(json);
            if (!validate(document))
                throw new InvalidDataException("Save data does not match its expected structure.");
            document[VersionKey] = CurrentVersion;
            document.Remove(ChecksumKey);
            document[ChecksumKey] = Checksum(document);
            byte[] bytes = Utf8.GetBytes(document.ToString(Formatting.Indented));

            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var previous = Read(path, validate);
            if (!overwrite && previous.Status != SaveLoadStatus.Missing)
                throw new IOException("A save with this name already exists.");
            if (previous.Status == SaveLoadStatus.UnsupportedVersion || previous.Status == SaveLoadStatus.Unavailable)
                throw new IOException(previous.Error);

            var primary = ReadOne(path, validate);
            if (primary.Status == SaveLoadStatus.Corrupt)
            {
                // Keep the original bytes for diagnosis/manual recovery, outside *.json scans.
                File.Copy(path, path + ".corrupt-" + Guid.NewGuid().ToString("N"));
            }

            // Back up only validated data. A damaged primary must never replace a good backup.
            if (previous.HasData)
            {
                byte[] backup = Utf8.GetBytes(previous.Document.ToString(Formatting.Indented));
                WriteFlushed(path + ".bak.tmp", backup);
                Commit(path + ".bak.tmp", path + ".bak");
            }
            else
            {
                if (File.Exists(path + ".bak"))
                    File.Copy(path + ".bak", path + ".bak.corrupt-" + Guid.NewGuid().ToString("N"));
                WriteFlushed(path + ".bak.tmp", bytes);
                Commit(path + ".bak.tmp", path + ".bak");
            }

            WriteFlushed(path + ".tmp", bytes);
            Commit(path + ".tmp", path);
        }
    }

    public static void Delete(string path)
    {
        lock (FileGate)
        {
            // An explicitly deleted craft must not reappear from a recovery candidate.
            foreach (string suffix in new[] { ".tmp", ".bak.tmp", ".bak", "" })
                File.Delete(path + suffix);
        }
    }

    private static JsonSaveReadResult ReadOne(string path, Func<JObject, bool> validate)
    {
        if (Directory.Exists(path))
            return Failure(SaveLoadStatus.Unavailable, path, "Expected a file, found a directory.");
        if (!File.Exists(path))
            return new JsonSaveReadResult { Status = SaveLoadStatus.Missing };
        string json;
        try { json = File.ReadAllText(path, Utf8); }
        catch (IOException exception) { return Failure(SaveLoadStatus.Unavailable, path, exception.Message); }
        catch (UnauthorizedAccessException exception) { return Failure(SaveLoadStatus.Unavailable, path, exception.Message); }
        catch (System.Security.SecurityException exception) { return Failure(SaveLoadStatus.Unavailable, path, exception.Message); }
        catch (DecoderFallbackException exception) { return Failure(SaveLoadStatus.Corrupt, path, exception.Message); }
        try
        {
            JObject document = Parse(json);
            JToken versionToken = document[VersionKey];
            if (versionToken != null && versionToken.Type != JTokenType.Integer)
                throw new InvalidDataException("Invalid format version.");
            int version = 0;
            if (versionToken != null)
            {
                // Newtonsoft represents oversized integer tokens as Int64/BigInteger.
                // Converting them directly can throw or incorrectly recover an older
                // backup, allowing a newer primary to be overwritten.
                string versionText = versionToken.ToString(Formatting.None);
                if (!int.TryParse(versionText, NumberStyles.Integer, CultureInfo.InvariantCulture, out version))
                {
                    if (versionText.StartsWith("-", StringComparison.Ordinal))
                        throw new InvalidDataException("Invalid negative format version.");
                    return Failure(SaveLoadStatus.UnsupportedVersion, path,
                        $"Format version exceeds the supported range (current {CurrentVersion}).");
                }
            }
            if (version > CurrentVersion)
                return Failure(SaveLoadStatus.UnsupportedVersion, path, $"Format {version} is newer than supported {CurrentVersion}.");
            if (version < 0 || !validate(document))
                throw new InvalidDataException("Invalid save structure.");
            if (version > 0 || document.Property(ChecksumKey) != null)
            {
                if (document[ChecksumKey]?.Type != JTokenType.String)
                    throw new InvalidDataException("Missing or invalid save checksum.");
                string expected = document[ChecksumKey].Value<string>();
                document.Remove(ChecksumKey);
                string actual = Checksum(document);
                document[ChecksumKey] = expected;
                if (!string.Equals(expected, actual, StringComparison.Ordinal))
                    throw new InvalidDataException("Save checksum mismatch.");
            }
            return new JsonSaveReadResult { Status = SaveLoadStatus.Loaded, Document = document, Version = version };
        }
        catch (Exception exception) when (exception is JsonException || exception is InvalidDataException
            || exception is ArgumentException || exception is OverflowException || exception is FormatException)
        {
            return Failure(SaveLoadStatus.Corrupt, path, exception.Message);
        }
    }

    internal static JObject Parse(string json)
    {
        using var input = new StringReader(json);
        using var reader = new JsonTextReader(input) { DateParseHandling = DateParseHandling.None };
        var document = JObject.Load(reader, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
        if (reader.Read())
            throw new InvalidDataException("Unexpected data after JSON object.");
        return document;
    }

    private static string Checksum(JObject document)
    {
        using var hash = SHA256.Create();
        return Convert.ToBase64String(hash.ComputeHash(Utf8.GetBytes(document.ToString(Formatting.None))));
    }

    private static void WriteFlushed(string path, byte[] bytes)
    {
        using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
        stream.Write(bytes, 0, bytes.Length);
        stream.Flush(true);
    }

    private static void Commit(string temporary, string destination)
    {
        if (!File.Exists(destination))
        {
            File.Move(temporary, destination);
            return;
        }
        for (int attempt = 0; ; attempt++)
        {
            try { File.Replace(temporary, destination, null); return; }
            catch (NotSupportedException)
            {
                // Some mobile runtimes lack Replace. The flushed temp + backup survive this gap.
                File.Delete(destination);
                File.Move(temporary, destination);
                return;
            }
            catch (IOException) when (attempt < 2 && File.Exists(temporary) && File.Exists(destination))
            {
                // Windows indexers/antivirus can briefly deny replacement. Do not delete the
                // primary on an I/O error. Persistent errors propagate and retain dirty data.
                System.Threading.Thread.Sleep(10);
            }
        }
    }

    private static JsonSaveReadResult Failure(SaveLoadStatus status, string path, string reason)
        => new() { Status = status, Error = $"Save '{Path.GetFileName(path)}': {reason}" };
}
