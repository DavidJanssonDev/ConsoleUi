namespace ConsoleUI.Rendering.Backends.Ansi;

/// <summary>
/// Maps ConsoleColor -> ANSI SGR codes.
/// </summary>
internal static class AnsiColor
{
    public static int Fg(ConsoleColor color) => color switch
    {
        ConsoleColor.Black => 30,
        ConsoleColor.Red => 91,
        ConsoleColor.Green => 92,
        ConsoleColor.Yellow => 93,
        ConsoleColor.Blue => 94,
        ConsoleColor.Magenta => 95,
        ConsoleColor.Cyan => 96,
        ConsoleColor.White => 97,
        _ => 37
    };

    public static int Bg(ConsoleColor color) => Fg(color) + 10;
}
