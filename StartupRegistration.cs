using Microsoft.Win32;

namespace LGClimaTray;

/// <summary>Starts the app with Windows via the current user's Run key (no admin needed).</summary>
internal static class StartupRegistration
{
    public const string TrayArgument = "--tray";
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "LGClimaTray";

    public static bool IsEnabled
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey);
            return key?.GetValue(ValueName) is string;
        }
    }

    public static void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKey);
        if (enabled) key.SetValue(ValueName, $"\"{Environment.ProcessPath}\" {TrayArgument}");
        else key.DeleteValue(ValueName, throwOnMissingValue: false);
    }
}
