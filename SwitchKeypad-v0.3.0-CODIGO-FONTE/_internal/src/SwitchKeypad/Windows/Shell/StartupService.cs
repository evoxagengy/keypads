using Microsoft.Win32;
namespace SwitchKeypad.Windows.Shell;
public static class StartupService
{
    private const string KeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string Name = "SwitchKeypad";
    public static bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(KeyPath,false);
        return key?.GetValue(Name) is string;
    }
    public static void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.OpenSubKey(KeyPath,true) ?? Registry.CurrentUser.CreateSubKey(KeyPath,true);
        if (enabled) key.SetValue(Name,$"\"{Environment.ProcessPath}\" --minimized"); else key.DeleteValue(Name,false);
    }
}
