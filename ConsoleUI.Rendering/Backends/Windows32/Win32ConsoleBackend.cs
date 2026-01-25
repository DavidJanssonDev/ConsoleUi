using ConsoleUi.Rendering.Abstractions;

namespace ConsoleUi.Rendering.Backends.Windows32;

public sealed class Win32ConsoleBackend : IConsoleBackend
{
    private Win32CharInfo[] _buf = [];
    private readonly IntPtr _h;

    public Win32ConsoleBackend()
    {
        _h = Win32Interop.GetStdHandle(Win32Interop.STD_OUTPUT_HANDLE);
        Console.CursorVisible = false;
    }

    public void Present(ReadOnlySpan<Cell> cells, int w, int h)
    {
        int len = w * h;
        if (_buf.Length != len)
            _buf = new Win32CharInfo[len];

        for (int i = 0; i < len; i++)
        {
            _buf[i].Char = cells[i].Ch;
            _buf[i].Attr = (ushort)(((int)cells[i].Bg << 4) | (int)cells[i].Fg);
        }

        var size = new Win32Interop.Coord { X = (short)w, Y = (short)h };
        var rect = new Win32Interop.Rect { L = 0, T = 0, R = (short)(w - 1), B = (short)(h - 1) };

        Win32Interop.WriteConsoleOutputW(_h, _buf, size, default, ref rect);
    }
}

