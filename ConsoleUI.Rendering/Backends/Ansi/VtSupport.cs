using System.Runtime.InteropServices;

namespace ConsoleUi.Rendering.Backends.Ansi;


/// <summary>
/// Enables VT processing on Windows so ANSI escape codes render properly.
/// No-op on non-Windows.
/// </summary>
internal static class VtSupport
{
    public static void Enable()
    {
        if (!OperatingSystem.IsWindows()) return;

        IntPtr h = GetStdHandle(-11);
        GetConsoleMode(h, out uint mode);
        SetConsoleMode(h, mode | 0x0004);
    }

    [DllImport("kernel32.dll")] static extern IntPtr GetStdHandle(int n);
    [DllImport("kernel32.dll")] static extern bool GetConsoleMode(IntPtr h, out uint m);
    [DllImport("kernel32.dll")] static extern bool SetConsoleMode(IntPtr h, uint m);

}
