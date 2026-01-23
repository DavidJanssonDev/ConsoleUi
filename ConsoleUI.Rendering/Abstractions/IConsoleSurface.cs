namespace ConsoleUI.Rendering.Abstractions;

public readonly record struct Cell(char Ch, ConsoleColor Fg, ConsoleColor Bg);

public interface IConsoleSurface : IDisposable
{
    int Width { get; }
    int Height { get; }


    bool ResizeIfNeeded();


    void Clear(ConsoleColor fg = ConsoleColor.Gray, ConsoleColor bg = ConsoleColor.Black);


    void Put(int x, int y, char ch, ConsoleColor fg, ConsoleColor bg);


    void Write(int x, int y, string text, ConsoleColor fg, ConsoleColor bg);


    void Present();
}
