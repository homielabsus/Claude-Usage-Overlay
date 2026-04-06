using Microsoft.Win32;
using System.IO;

namespace ClaudeUsageOverlay;

public static class StartupHelper
{
    private const string AppName = "ClaudeUsageOverlay";
    private const string RunKey  = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";

    public static bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey);
        return key?.GetValue(AppName) is not null;
    }

    public static void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
        if (key is null) return;

        if (enabled)
        {
            string path = Environment.ProcessPath
                ?? Path.Combine(AppContext.BaseDirectory, "ClaudeUsage.exe");
            key.SetValue(AppName, $"\"{path}\"");
        }
        else
        {
            key.DeleteValue(AppName, throwOnMissingValue: false);
        }
    }
}
