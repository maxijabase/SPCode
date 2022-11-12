using System.Diagnostics;

namespace SPCode.Utils;

public static class UrlUtils
{
    public static void OpenUrl(string url)
    {
        Process.Start(new ProcessStartInfo(url)
        {
            UseShellExecute = true
        });
    }
}