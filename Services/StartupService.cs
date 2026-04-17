using System.Reflection;
using Microsoft.Win32;

namespace DailyWords.Services;

public sealed class StartupService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "DailyWordsFloatingNotebook";

    public void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
        if (key is null)
        {
            return;
        }

        if (enabled)
        {
            key.SetValue(ValueName, BuildLaunchCommand());
        }
        else
        {
            key.DeleteValue(ValueName, false);
        }
    }

    public bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
        return key?.GetValue(ValueName) is string value && !string.IsNullOrWhiteSpace(value);
    }

    private static string BuildLaunchCommand()
    {
        var processPath = Environment.ProcessPath;
        var assemblyPath = Assembly.GetEntryAssembly()?.Location;
        if (string.IsNullOrWhiteSpace(processPath))
        {
            return string.IsNullOrWhiteSpace(assemblyPath) ? string.Empty : $"\"{assemblyPath}\"";
        }

        if (processPath.EndsWith("dotnet.exe", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(assemblyPath))
        {
            return $"\"{processPath}\" \"{assemblyPath}\"";
        }

        return $"\"{processPath}\"";
    }
}
