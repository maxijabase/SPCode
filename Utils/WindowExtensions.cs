using System;
using System.Linq;
using System.Windows;
using ControlzEx.Theming;
using MahApps.Metro;

namespace SPCode.Utils;

public static class WindowExtensions
{
    public static void ApplyTheme(this Window window)
    {
        var themeName = $"{Program.OptionsObject.Program_Theme}.{Program.OptionsObject.Program_AccentColor}";
        Console.WriteLine(themeName);
        var theme = ThemeManager.Current;
        theme.ChangeTheme(window, themeName);
    }
}