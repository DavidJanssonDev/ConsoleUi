namespace ConsoleUI.Rendering.Abstractions;

/// <summary>
/// One "pixel" in the console framebuffer.
/// </summary>
public readonly record struct Cell(char Ch, ConsoleColor Fg, ConsoleColor Bg)
{
    public static readonly Cell Empty = new(' ', ConsoleColor.Gray, ConsoleColor.Black);
}