using ConsoleUI.Rendering.Abstractions;

namespace ConsoleUI.Rendering.Surface;

/// <summary>
/// Shared Framebuffer surface.
/// Apps draw here. Present() delegates to then chosen backend.
/// </summary>
public sealed class ConsoleSurface : IConsoleSurface
{
    private readonly IConsoleBackend _backend;
    private readonly SurfaceBuffer _buffer = new();

    public ConsoleSurface(IConsoleBackend backend)
    {
        _backend = backend;
    }

    public int Width => _buffer.Width;
    public int Height => _buffer.Height;

    public bool ResizeIfNeeded()
         => _buffer.ResizeIfNeeded();
    public void Clear(ConsoleColor fg = ConsoleColor.Gray,
                      ConsoleColor bg = ConsoleColor.Black)
        => _buffer.Clear(fg, bg);

    public void Put(int x, int y, char ch,
                    ConsoleColor fg, ConsoleColor bg)
        => _buffer.Put(x, y, new Cell(ch, fg, bg));

    public void Write(int x, int y, string text,
                      ConsoleColor fg, ConsoleColor bg)
        => _buffer.Write(x, y, text, fg, bg);

    public void Present()
        => _backend.Present(_buffer.Cells, _buffer.Width, _buffer.Height);

    public void Dispose()
    {
        Console.CursorVisible = true;
    }
}
