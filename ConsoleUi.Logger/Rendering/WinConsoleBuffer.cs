using System;
using System.Runtime.InteropServices; // Windows API calls

namespace ConsoleUi.Logger.Rendering;

/// <summary>
/// A fast off-screen console buffer for Windows.
///</summary>
/// <remarks>
/// This class: <br/>
/// - keeps a full console frame in memory <br/>
/// - allows drawing characters at any position <br/>
/// - presents the whole frame to the console at once <br/>
/// </remarks>
public sealed class WinConsoleBuffer
{
    // Windows constant for the standard output handle
    private const int StdOuputHandle = -11;

    // Handle to the Windows console
    private readonly IntPtr _hConsole;

    // Our off-screen character buffer
    private CharInfo[] _characterBuffer = [];

    // Current console width and height
    private int _currentWidth;
    private int _currentHeight;

    public WinConsoleBuffer()
    {
        // Get access to the Windows console
        _hConsole = GetStdHandle(StdOuputHandle);

        // Hide the blinking cursor (important for UI look)
        Console.CursorVisible = false;
    }

    // Current buffer width
    public int Width => _currentWidth;

    // Current buffer height
    public int Height => _currentHeight;

    /// <summary>
    /// Resizes the internal buffer if the console window size changed.
    /// </summary>
    public bool ResizeIfNeeded()
    {
        int width = Console.WindowWidth;
        int height = Console.WindowHeight;

        if (width <= 0 || height <= 0) return false;
        if (width == _currentWidth && height == _currentHeight) return false;

        _currentWidth = width;
        _currentHeight = height;
        _characterBuffer = new CharInfo[_currentWidth * _currentHeight];
        return true;
    }

    /// <summary>
    /// Clears the entire buffer using the given colors.
    /// </summary>
    public void Clear(ConsoleColor fg = ConsoleColor.Gray, ConsoleColor bg = ConsoleColor.Black)
    {
        FillRect(0, 0, _currentWidth, _currentHeight, ' ', fg, bg);
    }

    /// <summary> 
    /// Fill a rectangle area with the same character and colors
    /// </summary>
    private void FillRect(int x, int y, int w, int h, char ch, ConsoleColor fg, ConsoleColor bg)
    {
        for (int yIndex = y; yIndex < y + h; yIndex++)
        {
            if (yIndex < 0 || yIndex >= _currentHeight) 
                continue;

            for (int xIndex = x; xIndex < x + w; xIndex++)
            {
                if (xIndex < 0 || xIndex >= _currentWidth) 
                    continue;

                Put(xIndex, yIndex, ch, fg, bg);
            }
        } 
    }

    /// <summary>
    /// Draws a single character at a specific position.
    /// </summary>
    public void Put(int x, int y, char ch, ConsoleColor fg, ConsoleColor bg)
    {
        // Ignore anything outside the screen
        if (x < 0 || x >= _currentWidth || y < 0 || y >= _currentHeight) return;
        
        // Convert (x,y) to a single index
        int index = y * _currentWidth + x;
        
        _characterBuffer[index].UnicodeChar = ch;
        _characterBuffer[index].Attributes = ToAttr(fg, bg);
    }

    /// <summary>
    /// Writes a string starting at a given position.
    /// </summary>
    public void Write(int x, int y, string text, ConsoleColor fg, ConsoleColor bg)
    {
        if (y < 0 || y >= _currentHeight) return;
        if (string.IsNullOrEmpty(text)) return;

        int max = Math.Min(text.Length, Math.Max(0,_currentWidth - x));

        for (int i = 0; i < max; i++)
        {
            Put(x + i, y, text[i], fg, bg);
        }
    }

    /// <summary>
    /// Displays the buffer on the real console.
    /// </summary>
    public void Present()
    {
        if (_currentWidth <= 0 || _currentHeight <= 0) return;

        var bufferSize = new Coord((short)_currentWidth, (short)_currentHeight);
        var bufferCoord = new Coord(0, 0);
        var writeRegion = new SmallRect(0, 0, (short)(_currentWidth - 1), (short)(_currentHeight - 1));

        WriteConsoleOutputW(
            _hConsole, 
            _characterBuffer, 
            bufferSize, 
            bufferCoord, 
            ref writeRegion
        );
    }

    /// <summary>
    /// Convert foreground/background colors into a Windows attribute value
    /// </summary>
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

    // Represents a single console cell (character + color)
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
