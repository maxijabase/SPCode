using System.Windows;
using ControlzEx.Theming;

namespace SPCode.Utils;

public static class WindowExtensions
{
    /// <summary>
    /// Applies the user-defined theme to the specified window
    /// </summary>
    /// <param name="window">Window that will receive the theme</param>
    public static void ApplyTheme(this Window window)
    {
        var themeName = $"{Program.OptionsObject.Program_Theme}.{Program.OptionsObject.Program_AccentColor}";
        
        // Prevent previous versions from loading old "Base" prefix nomenclature
        themeName = themeName.Replace("Base", string.Empty);
        
        ThemeManager.Current.ChangeTheme(window, themeName);
    }
}