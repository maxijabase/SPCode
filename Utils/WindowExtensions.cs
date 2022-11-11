using System.Windows;
using MahApps.Metro;

namespace SPCode.Utils;

public static class WindowExtensions
{
    public static void ApplyTheme(this Window window)
    {
        if (Program.OptionsObject.Program_AccentColor != "Red" || Program.OptionsObject.Program_Theme != "BaseDark")
        {
            ThemeManager.ChangeAppStyle(window, ThemeManager.GetAccent(Program.OptionsObject.Program_AccentColor),
                ThemeManager.GetAppTheme(Program.OptionsObject.Program_Theme));
        }
    }
}