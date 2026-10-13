using Microsoft.Win32;

namespace WinDock.Services;

public static class Autostart
{
    const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    const string Name = "WinDock";

    public static string Command(string exe) => $"\"{exe}\" --tray";

    public static bool Enabled
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey);
            return key?.GetValue(Name) is not null;
        }
        set
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
            if (key is null) return;
            if (value && Environment.ProcessPath is { } exe) key.SetValue(Name, Command(exe));
            else if (!value) key.DeleteValue(Name, throwOnMissingValue: false);
        }
    }
}
