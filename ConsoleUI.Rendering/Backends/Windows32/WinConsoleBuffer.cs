using System.Drawing;
using System.Runtime.InteropServices;

namespace ConsoleUI.Rendering.Backends.Windows32;

public sealed class WinConsoleBuffer
{
    private const int STD_OUTPUT_HANDLE = -11;

    private readonly IntPtr _hConsole;
    private CharInfo[] _buffer = [];
    private int _width, _height;

    public int Width => _width;
    public int Height => _height;

    public WinConsoleBuffer()
    {
        _hConsole = GetStdHandle(STD_OUTPUT_HANDLE);
        Console.CursorVisible = false;
    }

    public void ResizeIfNeeded()
    {
        int width = Console.WindowWidth;
        int height = Console.WindowHeight;
    
        if (width <= 0 || height <= 0) return;
        if (width == _width && height == _height) return;


        _width = width;
        _height = height;
        _buffer = new CharInfo[width * height];
    }

    public void Clear(ConsoleColor fg, ConsoleColor bg)
    {
        for (int index = 0; index < _buffer.Length; index++)
        {
            _buffer[index].UnicodeChar = ' ';
            _buffer[index].Attributes = ToAttr(fg, bg);

        }
    }

    public void Put(int x, int y, char ch, ConsoleColor fg, ConsoleColor bg)
    {
        if ((uint)x >= (uint)_width || (uint)y >= (uint)_height) return;
        int index = y * _width + x;
        _buffer[index].UnicodeChar = ch;
        _buffer[index].Attributes = ToAttr(fg, bg);
    }

    public void Write(int x, int y, string text, ConsoleColor fg, ConsoleColor bg)
    {
        if ((uint)y >= (uint)_height) return;

        int max = Math.Min(text.Length, _width - x);
        for (int i = 0; i < max; i++)
            Put(x + i, y, text[i], fg, bg);
    }

    public void Present()
    {
        if (_width <= 0 || _height <= 0) return;


        var size = new Coord((short)_width, (short)_height);
        var zero = new Coord(0, 0);
        var rect = new SmallRect(0, 0, (short)(_width - 1), (short)(_height - 1));


        WriteConsoleOutputW(_hConsole, _buffer, size, zero, ref rect);
    }

    private ushort ToAttr(ConsoleColor fg, ConsoleColor bg)
    {
        throw new NotImplementedException();
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Coord(short x, short y)
    { 
        public short 
            X = x, 
            Y = y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SmallRect(short l, short t, short r, short b)
    { 
        public short 
            Left = l, 
            Top = t, 
            Right = r, 
            Bottom = b;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct CharInfo
    {
        [FieldOffset(0)] public char UnicodeChar;
        [FieldOffset(2)] public ushort Attributes;
    }

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetStdHandle(int nStdHandle);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool WriteConsoleOutputW(IntPtr hConsole, CharInfo[] buffer, Coord size, Coord coord, ref SmallRect rect);

}
