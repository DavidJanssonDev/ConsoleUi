using ConsoleUI.Rendering.Abstractions;
using ConsoleUI.Rendering.Backends.Ansi;
using ConsoleUI.Rendering.Backends.Windows32;
using ConsoleUI.Rendering.Surface;

namespace ConsoleUI.Rendering.Factory;

public static class ConsoleSurfaceFactory
{
    public static IConsoleSurface CreateDefault(
        ConsoleBackendKind kind = ConsoleBackendKind.Auto)
    {
        IConsoleBackend backend = kind switch
        {
            ConsoleBackendKind.Ansi => new AnsiConsoleBackend(),
            ConsoleBackendKind.Win32 when OperatingSystem.IsWindows()
                => new Win32ConsoleBackend(),
            _ when OperatingSystem.IsWindows()
                => new Win32ConsoleBackend(),
            _ => new AnsiConsoleBackend()
        };

        return new ConsoleSurface(backend);
    }
}
