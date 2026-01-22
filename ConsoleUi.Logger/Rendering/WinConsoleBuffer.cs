using System;
using System.Drawing;
using System.Numerics;
using System.Runtime.InteropServices;

namespace ConsoleUi.Logger.Rendering;

public sealed class WinConsoleBuffer
{
    private const int StdOuputHandle = -11;

    private readonly IntPtr _hConsole;
    private CharInfo[] _buffer = [];
    private int _w;
    private int _h;

    public WinConsoleBuffer()
    {
        _hConsole = GetStdHandle(StdOuputHandle);
        Console.CursorVisible = false;
    }

    public int Width => _w;
    public int Height => _h;

    public bool ResizeIfNeeded()
    {
        int w = Console.WindowWidth;
        int h = Console.WindowHeight;

        if (w <= 0 || h <= 0) return false;
        if (w == _w && h == _h) return false;

        _w = w;
        _h = h;
        _buffer = new CharInfo[_w * _h];
        return true;
    }

    public void Clear(ConsoleColor fg = ConsoleColor.Gray, ConsoleColor bg = ConsoleColor.Black)
    {
        FillRect(0, 0, _w, _h, ' ', fg, bg);
    }

    private void FillRect(int x, int y, int w, int h, char ch, ConsoleColor fg, ConsoleColor bg)
    {
        for (int yy = y; yy < y + h; yy++)
        {
            if (yy <0 || yy >= _h) continue;

            for (int xx = x; xx < x + w; xx++)
            {
                if (xx < 0 || xx >= _w) continue;
                Put(xx, yy, ch, fg, bg);
            }
        } 
    }

    public void Put(int x, int y, char ch, ConsoleColor fg, ConsoleColor bg)
    {
        if (x < 0 || x >= _w || y < 0 || y >= _h) return;

        int i = y * _w + x;
        _buffer[i].UnicodeChar = ch;
        _buffer[i].Attributes = ToAttr(fg, bg);
    }

    public void Write(int x, int y, string text, ConsoleColor fg, ConsoleColor bg)
    {
        if (y < 0 || y >= _h) return;
        if (string.IsNullOrEmpty(text)) return;

        int max = Math.Min(text.Length, Math.Max(0,_w - x));

        for (int i = 0; i < max; i++)
        {
            Put(x + i, y, text[i], fg, bg);
        }
    }

    public void Present()
    {
        if (_w <= 0 || _h <= 0) return;

        var bufferSize = new Coord((short)_w, (short)_h);
        var bufferCoord = new Coord(0, 0);
        var writeRegion = new SmallRect(0, 0, (short)(_w - 1), (short)(_h - 1));

        WriteConsoleOutputW(_hConsole, _buffer, bufferSize, bufferCoord, ref writeRegion);
    }

    private static ushort ToAttr(ConsoleColor fg, ConsoleColor bg)
        => (ushort)(((int)bg << 4) | (int)fg);

    #region PInvoke

    [StructLayout(LayoutKind.Sequential)]
    private struct Coord
    {
        public short X;
        public short Y;

        public Coord(short x, short y)
        {
            X = x;
            Y = y;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SmallRect
    {
        public short Left;
        public short Top;
        public short Right;
        public short Bottom;

        public SmallRect(short left, short top, short right, short bottom)
        {
            Left = left;
            Top = top;
            Right = right;
            Bottom = bottom;
        }
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct CharInfo
    {
        [FieldOffset(0)] public char UnicodeChar;
        [FieldOffset(2)] public ushort Attributes;
    }


    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GetStdHandle(int nStdHandle);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool WriteConsoleOutputW(
        IntPtr hConsoleOutput,
        [MarshalAs(UnmanagedType.LPArray), In] CharInfo[] lpBuffer,
        Coord dwBufferSize,
        Coord dwBufferCoord,
        ref SmallRect lpWriteRegion
    );
    #endregion
}
