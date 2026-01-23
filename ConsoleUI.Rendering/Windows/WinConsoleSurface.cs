using ConsoleUi.Rendering;
using ConsoleUI.Rendering.Abstractions;

namespace ConsoleUi.Rendering.Windows;

public sealed class WinConsoleSurface : IConsoleSurface
{
    private readonly WinConsoleBuffer _buf = new();

    public int Width => _buf.Width;
    public int Height => _buf.Height;

    public bool ResizeIfNeeded()
    {
        int w = _buf.Width, h = _buf.Height;

        _buf.ResizeIfNeeded();

        return w != _buf.Width || h != _buf.Height;


    }

    public void Clear(ConsoleColor fg = ConsoleColor.Gray, ConsoleColor bg = ConsoleColor.Black)
        => _buf.Clear(fg, bg);

    public void Put(int x, int y, char ch, ConsoleColor fg, ConsoleColor bg)
        => _buf.Put(x, y, ch, fg, bg);

    public void Write(int x, int y, string text, ConsoleColor fg, ConsoleColor bg)
        => _buf.Write(x, y, text, fg, bg);

    public void Present() => _buf.Present();

    public void Dispose()
        => Console.CursorVisible = true;
}
