using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Web.Script.Serialization;

internal static class Updater
{
    private static readonly string InstallRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "{{PUBLISHER_ID}}", "{{APP_ID}}");
    private const string MetadataUrl = "{{METADATA_URL}}";

    public static int Main()
    {
        try
        {
            Stream input = Console.OpenStandardInput();
            byte[] sizeBytes = ReadExact(input, 4);
            int size = BitConverter.ToInt32(sizeBytes, 0);
            if (size < 2 || size > 1024 * 1024) throw new InvalidDataException("Invalid message size.");
            string json = Encoding.UTF8.GetString(ReadExact(input, size));
            var serializer = new JavaScriptSerializer();
            var message = serializer.Deserialize<Dictionary<string, object>>(json);
            object actionValue;
            string action = message.TryGetValue("action", out actionValue) ? Convert.ToString(actionValue) : "";
            if (action == "checkUpdate")
            {
                var latest = GetLatest(serializer);
                string current = ReadCurrentVersion(serializer);
                Reply(serializer, new { ok = true, currentVersion = current, latestVersion = latest["version"], updateAvailable = current != Convert.ToString(latest["version"]) });
                return 0;
            }
            if (action == "update")
            {
                var latest = GetLatest(serializer);
                InstallLatest(latest, serializer);
                Reply(serializer, new { ok = true, version = Convert.ToString(latest["version"]), needsReload = true });
                return 0;
            }
            throw new InvalidOperationException("Unsupported action.");
        }
        catch (Exception error)
        {
            try { Reply(new JavaScriptSerializer(), new { ok = false, error = error.Message }); } catch { }
            return 1;
        }
    }

    private static Dictionary<string, object> GetLatest(JavaScriptSerializer serializer)
    {
        using (var client = NewWebClient())
            return serializer.Deserialize<Dictionary<string, object>>(client.DownloadString(MetadataUrl));
    }

    private static string ReadCurrentVersion(JavaScriptSerializer serializer)
    {
        string manifest = Path.Combine(InstallRoot, "extension", "manifest.json");
        if (!File.Exists(manifest)) return "0.0.0";
        var value = serializer.Deserialize<Dictionary<string, object>>(File.ReadAllText(manifest));
        return Convert.ToString(value["version"]);
    }

    private static void InstallLatest(Dictionary<string, object> latest, JavaScriptSerializer serializer)
    {
        string version = Required(latest, "version");
        string url = Required(latest, "url");
        string expectedHash = Required(latest, "sha256").ToLowerInvariant();
        if (!url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Update URL must use HTTPS.");
        if (expectedHash.Length != 64) throw new InvalidDataException("Invalid SHA-256 value.");

        string temp = Path.Combine(Path.GetTempPath(), "{{APP_ID}}-update-" + Guid.NewGuid().ToString("N"));
        string zip = Path.Combine(temp, "dist.zip");
        string staged = Path.Combine(temp, "extension");
        string extension = Path.Combine(InstallRoot, "extension");
        string backup = Path.Combine(InstallRoot, "extension.old");
        Directory.CreateDirectory(temp);
        try
        {
            using (var client = NewWebClient()) client.DownloadFile(url, zip);
            if (!FixedEquals(Hash(zip), expectedHash)) throw new InvalidDataException("SHA-256 verification failed.");
            ZipFile.ExtractToDirectory(zip, staged);
            string manifestPath = Path.Combine(staged, "manifest.json");
            if (!File.Exists(manifestPath)) throw new InvalidDataException("dist.zip must contain manifest.json at its root.");
            var manifest = serializer.Deserialize<Dictionary<string, object>>(File.ReadAllText(manifestPath));
            if (Required(manifest, "version") != version) throw new InvalidDataException("Manifest version does not match latest.json.");
            if (Directory.Exists(backup)) Directory.Delete(backup, true);
            if (Directory.Exists(extension)) Directory.Move(extension, backup);
            try
            {
                Directory.Move(staged, extension);
                if (Directory.Exists(backup)) Directory.Delete(backup, true);
            }
            catch
            {
                if (Directory.Exists(extension)) Directory.Delete(extension, true);
                if (Directory.Exists(backup)) Directory.Move(backup, extension);
                throw;
            }
        }
        finally { if (Directory.Exists(temp)) Directory.Delete(temp, true); }
    }

    private static WebClient NewWebClient()
    {
        ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
        var client = new WebClient();
        client.Headers.Add(HttpRequestHeader.UserAgent, "extension-local-packager/{{APP_ID}}");
        return client;
    }

    private static string Required(Dictionary<string, object> data, string key)
    {
        object value;
        if (!data.TryGetValue(key, out value) || String.IsNullOrWhiteSpace(Convert.ToString(value))) throw new InvalidDataException("Missing " + key + ".");
        return Convert.ToString(value);
    }

    private static string Hash(string file)
    {
        using (var sha = SHA256.Create()) using (var stream = File.OpenRead(file))
            return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
    }

    private static bool FixedEquals(string left, string right)
    {
        if (left.Length != right.Length) return false;
        int result = 0;
        for (int i = 0; i < left.Length; i++) result |= left[i] ^ right[i];
        return result == 0;
    }

    private static byte[] ReadExact(Stream stream, int count)
    {
        byte[] data = new byte[count];
        int offset = 0;
        while (offset < count)
        {
            int read = stream.Read(data, offset, count - offset);
            if (read == 0) throw new EndOfStreamException();
            offset += read;
        }
        return data;
    }

    private static void Reply(JavaScriptSerializer serializer, object value)
    {
        byte[] body = Encoding.UTF8.GetBytes(serializer.Serialize(value));
        Stream output = Console.OpenStandardOutput();
        byte[] size = BitConverter.GetBytes(body.Length);
        output.Write(size, 0, size.Length);
        output.Write(body, 0, body.Length);
        output.Flush();
    }
}
