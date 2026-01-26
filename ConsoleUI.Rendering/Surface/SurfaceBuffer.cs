using ConsoleUI.Rendering.Abstractions;
using System.ComponentModel;

namespace ConsoleUI.Rendering.Surface;

public sealed class SurfaceBuffer
{
    private Cell[] _cells = [];
    private int _width;
    private int _height;

    public int Width => _width;
    public int Height => _height;
    public ReadOnlySpan<Cell> Cells => _cells;

    public bool ResizeIfNeeded()
    {
        int width = Console.WindowWidth;
        int height = Console.WindowHeight;

        if (width <= 0 || height <= 0) return false;
        if (width == _width && height == _height) return false;
        
        _width = width;
        _height = height;
        _cells = new Cell[width * height];
        return true;
    }

    public void Clear(ConsoleColor fg, ConsoleColor bg)
    {
        Cell fill = new(' ', fg, bg);
        _cells.AsSpan().Fill(fill);
    }

    public void Put(int x, int y, Cell cell)
    {
        if ((uint)x >= (uint)_width || (uint)y >= (uint)_height) return;
        _cells[(y * _width) + x] = cell;
    }

    public void Write(int x, int y, string text, ConsoleColor fg, ConsoleColor bg)
    {
        if ((uint)y >= (uint)_height || string.IsNullOrEmpty(text)) return;

        int max = Math.Min(text.Length, Math.Max(0, _width - x));
        for (int index = 0; index < max; index++)
            Put(x + index, y, new Cell(text[index], fg, bg));
    }
}
