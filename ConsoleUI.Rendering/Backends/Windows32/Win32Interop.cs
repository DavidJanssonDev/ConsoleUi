using System.Runtime.InteropServices;

namespace ConsoleUi.Rendering.Backends.Windows32;


internal static class Win32Interop
{
    public const int STD_OUTPUT_HANDLE = -11;

    [StructLayout(LayoutKind.Sequential)]
    public struct Coord { public short X, Y; }

    [StructLayout(LayoutKind.Sequential)]
    public struct Rect { public short L, T, R, B; }

    [DllImport("kernel32.dll")]
    public static extern IntPtr GetStdHandle(int n);

    [DllImport("kernel32.dll")]
    public static extern bool WriteConsoleOutputW(
        IntPtr h,
        Win32CharInfo[] buf,
        Coord size,
        Coord zero,
        ref Rect rect
    );
}