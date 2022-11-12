﻿using System.Windows;
using ControlzEx.Theming;

namespace SPCode.Utils;

public static class WindowExtensions
{
    public static void ApplyTheme(this Window window)
    {
        var themeName = $"{Program.OptionsObject.Program_Theme}.{Program.OptionsObject.Program_AccentColor}";
        ThemeManager.Current.ChangeTheme(window, themeName);
    }
}