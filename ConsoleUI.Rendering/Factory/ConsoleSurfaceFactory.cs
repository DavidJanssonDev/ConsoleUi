using ConsoleUi.Rendering.Abstractions;
using ConsoleUi.Rendering.Backends.Ansi;
using ConsoleUi.Rendering.Backends.Windows32;
using ConsoleUi.Rendering.Surface;

namespace ConsoleUi.Rendering.Factory;

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
