namespace ConsoleUI.Rendering.Abstractions;

/// <summary>
/// Low-level presenter.
/// Converts a framebuffer into real terminal output (Win32 or ANSI VT).
/// </summary>
public interface IConsoleBackend
{
    void Present(ReadOnlySpan<Cell> cells, int width, int height);
}