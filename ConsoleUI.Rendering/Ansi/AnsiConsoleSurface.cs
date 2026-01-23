using ConsoleUi.Rendering.Ansi;
using ConsoleUI.Rendering.Abstractions;
using Microsoft.VisualBasic;
using System;
using System.Collections.Generic;
using System.Text;

namespace ConsoleUI.Rendering.Ansi;

public sealed class AnsiConsoleSurface : IConsoleSurface
{
    private Cell[] _buffer = [];
    private int _width, _height;

    public int Width => _width;

    public int Height => _height;

    public AnsiConsoleSurface()
    {
        Console.CursorVisible = false;
        ResizeIfNeeded();
        Console.Write("\u001b[2J \u001b[H");
    }

    public bool ResizeIfNeeded()
    {
        int width = Console.WindowWidth;
        int height = Console.WindowHeight;

        if (width <= 0 || height <= 0) return false;
        if (width == _width && height == _height) return false;

        _width = width;
        _height = height;
        _buffer = new Cell[width * height];
        return true;
    }
    public void Clear(ConsoleColor fg = ConsoleColor.Gray, ConsoleColor bg = ConsoleColor.Black)
        => Array.Fill(_buffer, new Cell(' ', fg, bg));
  

    public void Put(int x, int y, char ch, ConsoleColor fg, ConsoleColor bg)
    {
        if ((uint)x >= (uint)_width || (uint)y >= (uint)_height) return;

        _buffer[y * _width + x] = new Cell(ch, fg, bg);
    }

   
    public void Write(int x, int y, string text, ConsoleColor fg, ConsoleColor bg)
    {
        if ((uint)y >= (uint)_height) return;

        int max = Math.Min(text.Length, _width - x);

        for (int index = 0; index < max; index++)
            Put(x + index, y, text[index], fg, bg);
    }


    public void Present()
    {
        StringBuilder stringBuilder = new(_width * _height * 2);
        stringBuilder.Append("\u001B[H");

        ConsoleColor? fg = null, bg = null;

        for (int y = 0; y < _height; y++)
        {
            for (int x = 0; x < _width; x++)
            {
                Cell cell = _buffer[y * _width + x];
                
                if (fg != cell.Fg)
                {
                    stringBuilder.Append(AnsiColor.Fg(cell.Fg));
                    fg = cell.Fg;
                }
                
                if (bg != cell.Bg)
                {
                    stringBuilder.Append(AnsiColor.Bg(cell.Bg));
                    bg = cell.Bg;
                }
            }

            if (y < _height - 1) stringBuilder.Append('\n');
        }
    }

    public void Dispose()
    {
        Console.Write("\u001b[0m");
        Console.CursorVisible = true;
    }

}
