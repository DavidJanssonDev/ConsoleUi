using System.Text;
using ConsoleUI.Rendering.Abstractions;
using ConsoleUI.Rendering.Surface;

namespace ConsoleUI.Rendering.Backends.Ansi;

/// <summary>
/// Cross-platform backend using ANSI/VT escape sequences.
/// Simple version: full redraw every Present().
/// </summary>
public sealed class AnsiConsoleBackend : IConsoleBackend
{
    public AnsiConsoleBackend()
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        VtSupport.Enable();
        Console.CursorVisible = false;
        Console.Write(AnsiSequences.Clear + AnsiSequences.Home);
    }

    public void Present(ReadOnlySpan<Cell> cells, int w, int h)
    {
        Console.Write(AnsiSequences.Home);

        int i = 0;
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++, i++)
            {
                var c = cells[i];
                Console.Write(
                    $"{AnsiSequences.Csi}{AnsiColor.Fg(c.Fg)};{AnsiColor.Bg(c.Bg)}m{c.Ch}"
                );
            }
            if (y < h - 1) Console.Write('\n');
        }

        Console.Write(AnsiSequences.Reset);
    }
}
