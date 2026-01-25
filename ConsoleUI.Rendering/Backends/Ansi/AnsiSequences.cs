namespace ConsoleUi.Rendering.Backends.Ansi;

internal static class AnsiSequences
{
    public const string Csi = "\x1b[";
    public const string Reset = "\x1b[0m";
    public const string Clear = "\x1b[2J";
    public const string Home = "\x1b[H";
    public const string HideCursor = "\x1b[?25l";
    public const string ShowCursor = "\x1b[?25h";
}
