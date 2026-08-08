using Microsoft.Win32;

namespace ClassHelper.App.Services;

public static class AutoStartService
{
    private const string RegistryPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "ClassHelper";

    public static bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RegistryPath, false);
        return key?.GetValue(ValueName) is string;
    }

    public static void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RegistryPath, true);
        if (enabled)
        {
            var processPath = Environment.ProcessPath
                ?? throw new InvalidOperationException("无法确定课堂助手的程序路径。");
            key.SetValue(ValueName, $"\"{processPath}\"");
        }
        else
        {
            key.DeleteValue(ValueName, false);
        }
    }
}
