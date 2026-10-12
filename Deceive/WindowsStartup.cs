using System;
using System.Diagnostics;
using System.Windows.Forms;
using Microsoft.Win32;

namespace Deceive;

/// Registers (or removes) the app in the current user's Windows startup list.
/// Uses HKCU, so no administrator permission is needed.
internal static class WindowsStartup
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "SkoreLoBoostStatus";

    internal static bool IsEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath);
            var value = key?.GetValue(ValueName) as string;
            return !string.IsNullOrEmpty(value);
        }
        catch
        {
            return false;
        }
    }

    internal static void SetEnabled(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath);
            if (key is null)
                return;

            if (enabled)
                key.SetValue(ValueName, "\"" + Application.ExecutablePath + "\"");
            else
                key.DeleteValue(ValueName, false);
        }
        catch (Exception ex)
        {
            Trace.WriteLine(ex);
            MessageBox.Show(
                "Não foi possível alterar a inicialização com o Windows.",
                StartupHandler.DeceiveTitle,
                MessageBoxButtons.OK,
                MessageBoxIcon.Error
            );
        }
    }
}
