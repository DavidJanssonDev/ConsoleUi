using ConsoleUI.Rendering.Abstractions;
using ConsoleUi.Rendering.Windows;
using ConsoleUI.Rendering.Ansi;

namespace ConsoleUI.Rendering;

public static class ConsoleSurface
{
    public static IConsoleSurface CreateDefault()
    {
        if (OperatingSystem.IsWindows())
            return new WinConsoleSurface();

        return new AnsiConsoleSurface(); // Linux + macOS
    }
}
