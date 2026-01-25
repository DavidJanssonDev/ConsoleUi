namespace ConsoleUi.Rendering.Abstractions;

/// <summary>
/// High-level framebuffer API used by apps (Logger/UI). <br/>
/// Apps draw into surface using Put/Write/Clear, then call Present().
/// </summary>
public interface IConsoleSurface : IDisposable
{
    int Width { get; }
    int Height { get; }

    ///<summary>
    /// Checks console window size and resizes the frame buffer if needed.
    /// </summary>
    bool ResizeIfNeeded();

    void Clear(ConsoleColor fg = ConsoleColor.Gray, ConsoleColor bg = ConsoleColor.Black);
    
    void Put(int x, int y, char ch, ConsoleColor fg, ConsoleColor bg);

    void Write(int x, int y, string text, ConsoleColor fg, ConsoleColor bg);

    ///<summary>
    /// Flush framebuffer to the actual console.
    /// </summary>
    void Present();
}
