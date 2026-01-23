namespace ConsoleUi.Rendering.Ansi;

internal static class AnsiColor
{
    public static string Fg(ConsoleColor c) => Map(c, false);
    public static string Bg(ConsoleColor c) => Map(c, true);


    private static string Map(ConsoleColor c, bool bg)
    {
        int baseCode = c switch
        {
            ConsoleColor.Black => 30,
            ConsoleColor.DarkRed => 31,
            ConsoleColor.DarkGreen => 32,
            ConsoleColor.DarkYellow => 33,
            ConsoleColor.DarkBlue => 34,
            ConsoleColor.DarkMagenta => 35,
            ConsoleColor.DarkCyan => 36,
            ConsoleColor.Gray => 37,
            ConsoleColor.DarkGray => 90,
            ConsoleColor.Red => 91,
            ConsoleColor.Green => 92,
            ConsoleColor.Yellow => 93,
            ConsoleColor.Blue => 94,
            ConsoleColor.Magenta => 95,
            ConsoleColor.Cyan => 96,
            ConsoleColor.White => 97,
            _ => 37
        };


        if (bg) baseCode += 10;
        return $"\u001b[{baseCode}m";
    }
}