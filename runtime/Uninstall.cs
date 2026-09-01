using System;
using System.Diagnostics;
using System.IO;
using Microsoft.Win32;

internal static class Uninstall
{
    private static readonly string InstallRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "{{PUBLISHER_ID}}", "{{APP_ID}}");
    private const string DisplayName = "{{DISPLAY_NAME}}";
    private const string NativeHostName = "{{NATIVE_HOST_NAME}}";
    private const string UninstallId = "{{UNINSTALL_ID}}";

    [STAThread]
    public static int Main()
    {
        try
        {
            Registry.CurrentUser.DeleteSubKeyTree(@"Software\Google\Chrome\NativeMessagingHosts\" + NativeHostName, false);
            Registry.CurrentUser.DeleteSubKeyTree(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\" + UninstallId, false);
            try { Process.Start("chrome.exe", "chrome://extensions/"); } catch { }
            string command = "/d /c ping 127.0.0.1 -n 3 >nul & rmdir /s /q \"" + InstallRoot.Replace("&", "^&") + "\"";
            Process.Start(new ProcessStartInfo("cmd.exe", command) { CreateNoWindow = true, UseShellExecute = false });
            Console.WriteLine(DisplayName + " was unregistered. Remove it from Chrome on the extensions page.");
            return 0;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine(error.Message);
            return 1;
        }
    }
}
