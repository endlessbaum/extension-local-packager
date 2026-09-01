using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Web.Script.Serialization;
using Microsoft.Win32;

internal static class Setup
{
    private const string AppId = "{{APP_ID}}";
    private const string DisplayName = "{{DISPLAY_NAME}}";
    private static readonly string InstallRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "{{PUBLISHER_ID}}", "{{APP_ID}}");
    private const string MetadataUrl = "{{METADATA_URL}}";
    private const string ExtensionId = "{{EXTENSION_ID}}";
    private const string NativeHostName = "{{NATIVE_HOST_NAME}}";
    private const string UninstallId = "{{UNINSTALL_ID}}";
    private const string UpdaterBase64 = "{{UPDATER_BASE64}}";
    private const string UninstallerBase64 = "{{UNINSTALLER_BASE64}}";

    [STAThread]
    public static int Main()
    {
        string temp = Path.Combine(Path.GetTempPath(), AppId + "-install-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(InstallRoot);
            Directory.CreateDirectory(temp);
            string updater = Path.Combine(InstallRoot, "updater.exe");
            string uninstaller = Path.Combine(InstallRoot, "uninstall.exe");
            File.WriteAllBytes(updater, Convert.FromBase64String(UpdaterBase64));
            File.WriteAllBytes(uninstaller, Convert.FromBase64String(UninstallerBase64));

            var serializer = new JavaScriptSerializer();
            Dictionary<string, object> latest;
            using (var client = NewWebClient()) latest = serializer.Deserialize<Dictionary<string, object>>(client.DownloadString(MetadataUrl));
            InstallExtension(latest, serializer, temp);
            RegisterNativeHost(updater, serializer);
            RegisterUninstaller(uninstaller);

            string extensionPath = Path.Combine(InstallRoot, "extension");
            try { System.Windows.Forms.Clipboard.SetText(extensionPath); } catch { }
            try { Process.Start("chrome.exe", "chrome://extensions/"); } catch { }
            Console.WriteLine("Installed " + DisplayName + ".");
            Console.WriteLine("The extension folder was copied to the clipboard: " + extensionPath);
            Console.WriteLine("Enable Developer mode and select Load unpacked.");
            return 0;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine("Installation failed: " + error.Message);
            return 1;
        }
        finally { try { if (Directory.Exists(temp)) Directory.Delete(temp, true); } catch { } }
    }

    private static void InstallExtension(Dictionary<string, object> latest, JavaScriptSerializer serializer, string temp)
    {
        string version = Required(latest, "version");
        string url = Required(latest, "url");
        string expectedHash = Required(latest, "sha256").ToLowerInvariant();
        if (!url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Update URL must use HTTPS.");
        string zip = Path.Combine(temp, "dist.zip");
        string staged = Path.Combine(temp, "extension");
        using (var client = NewWebClient()) client.DownloadFile(url, zip);
        if (Hash(zip) != expectedHash) throw new InvalidDataException("SHA-256 verification failed.");
        ZipFile.ExtractToDirectory(zip, staged);
        string manifestPath = Path.Combine(staged, "manifest.json");
        if (!File.Exists(manifestPath)) throw new InvalidDataException("dist.zip must contain manifest.json at its root.");
        var manifest = serializer.Deserialize<Dictionary<string, object>>(File.ReadAllText(manifestPath));
        if (Required(manifest, "version") != version) throw new InvalidDataException("Manifest version does not match latest.json.");
        if (ExtensionIdForKey(Required(manifest, "key")) != ExtensionId) throw new InvalidDataException("Extension key does not match the packaged Extension ID.");

        string extension = Path.Combine(InstallRoot, "extension");
        string backup = Path.Combine(InstallRoot, "extension.old");
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

    private static void RegisterNativeHost(string updater, JavaScriptSerializer serializer)
    {
        string hostManifest = Path.Combine(InstallRoot, "native-host.json");
        var document = new Dictionary<string, object> {
            { "name", NativeHostName }, { "description", "Updater for " + DisplayName },
            { "path", updater }, { "type", "stdio" },
            { "allowed_origins", new [] { "chrome-extension://" + ExtensionId + "/" } }
        };
        File.WriteAllText(hostManifest, serializer.Serialize(document), new UTF8Encoding(false));
        using (RegistryKey key = Registry.CurrentUser.CreateSubKey(@"Software\Google\Chrome\NativeMessagingHosts\" + NativeHostName))
            key.SetValue(null, hostManifest, RegistryValueKind.String);
    }

    private static void RegisterUninstaller(string uninstaller)
    {
        using (RegistryKey key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\" + UninstallId))
        {
            key.SetValue("DisplayName", DisplayName);
            key.SetValue("DisplayVersion", "{{PACKAGE_VERSION}}");
            key.SetValue("Publisher", "{{PUBLISHER_ID}}");
            key.SetValue("InstallLocation", InstallRoot);
            key.SetValue("UninstallString", "\"" + uninstaller + "\"");
            key.SetValue("NoModify", 1, RegistryValueKind.DWord);
            key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
        }
    }

    private static WebClient NewWebClient()
    {
        ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
        var client = new WebClient();
        client.Headers.Add(HttpRequestHeader.UserAgent, "extension-local-packager/" + AppId);
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

    private static string ExtensionIdForKey(string key)
    {
        byte[] der = Convert.FromBase64String(key);
        byte[] hash;
        using (var sha = SHA256.Create()) hash = sha.ComputeHash(der);
        var result = new StringBuilder(32);
        for (int i = 0; i < 16; i++) { result.Append((char)('a' + (hash[i] >> 4))); result.Append((char)('a' + (hash[i] & 15))); }
        return result.ToString();
    }
}
