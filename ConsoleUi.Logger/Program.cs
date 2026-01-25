using ConsoleUi.Core.Logging;
using ConsoleUi.Rendering.Abstractions;
using ConsoleUi.Rendering.Factory;
using ConsoleUi.Rendering.Surface;
using System;
using System.IO.Pipes;
using System.Text;

namespace ConsoleUi.Logger;

/// <summary>
/// The main program for the Logger UI.
/// </summary>
/// <remarks>
/// This app does two big jobs at the same time:
/// <list type="bullet">
///   <item><description><b>Receives logs</b> from another process over a named pipe.</description></item>
///   <item><description><b>Draws a console UI</b> that shows those logs in a table, including scrolling.</description></item>
/// </list>
///
/// Architecture:
/// <list type="bullet">
///   <item><description><b>Pipe reader loop</b> (data thread): reads lines from the named pipe and appends to <see cref="Rows"/>.</description></item>
///   <item><description><b>Render loop</b> (UI thread): handles keyboard input + redraws the screen using <see cref="WinConsoleBuffer"/>.</description></item>
/// </list>
/// </remarks>
public static class Program
{
    private const int HeaderHeight = 3;

    private const int TimeWidth = 14;
    private const int LevelWidth = 6;
    private const int CategoryWidth = 14;

    private static readonly List<LogRow> Rows = new(5000);
    private static readonly object _lock = new();

    private static volatile bool _dirty = true;

    public static async Task Main()
    {
        Console.Title = "ConsoleUi Logger";
        Console.OutputEncoding = Encoding.UTF8;

        using IConsoleSurface surface = ConsoleSurfaceFactory.CreateDefault();

        // Render loop (UI thread)
        using var renderCts = new CancellationTokenSource();
        var renderTask = Task.Run(
            () => RenderLoopAsync(surface, renderCts.Token)
        );

        // Pipe server loop (data thread)
        while (true)
        {
            await using var server = new NamedPipeServerStream(
                PipeConstants.PipeName,
                PipeDirection.In,
                1,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous
            );

            AddSystem("Waiting for renderer connection...");
            await server.WaitForConnectionAsync();
            AddSystem("Renderer connected.");

            try
            {
                using var reader = new StreamReader(
                    server, Encoding.UTF8, false, 4096, leaveOpen: true);

                while (true)
                {
                    string? line = await reader.ReadLineAsync();
                    if (line is null) break;

                    var parts = line.Split('|', 3);
                    string level = parts.Length > 0 ? parts[0] : "Info";
                    string category = parts.Length > 1 ? parts[1] : "General";
                    string message = parts.Length > 2 ? parts[2] : line;

                    Add(new LogRow(DateTime.Now, level, category, message));
                }
            }
            catch (IOException ex)
            {
                AddSystem($"Pipe error: {ex.Message}");
            }

            AddSystem("Renderer disconnected.");
        }
    }


    // ============================================================
    // Render loop
    // ============================================================

    private static async Task RenderLoopAsync(IConsoleSurface surface, CancellationToken token)
    {
        var delay = TimeSpan.FromMilliseconds(33); // ~30 FPS

        while (!token.IsCancellationRequested)
        {
            if (surface.ResizeIfNeeded())
                _dirty = true;

            if (_dirty)
            {
                Draw(surface);
                surface.Present();
                _dirty = false;
            }

            await Task.Delay(delay, token);
        }
    }


    // ============================================================
    // Drawing
    // ============================================================

    private static void Draw(IConsoleSurface s)
    {
        int w = s.Width;
        int h = s.Height;
        if (w <= 0 || h <= 0) return;

        s.Clear(ConsoleColor.Gray, ConsoleColor.Black);

        DrawHeader(s);

        int bodyTop = HeaderHeight;
        int bodyBottom = h - 1;
        int bodyHeight = Math.Max(0, bodyBottom - bodyTop);

        // Borders
        s.Write(0, bodyTop - 1,
            "├" + new string('─', Math.Max(0, w - 2)) + "┤",
            ConsoleColor.Gray, ConsoleColor.Black);

        s.Write(0, h - 1,
            "└" + new string('─', Math.Max(0, w - 2)) + "┘",
            ConsoleColor.Gray, ConsoleColor.Black);

        for (int y = bodyTop; y < h - 1; y++)
        {
            s.Put(0, y, '│', ConsoleColor.Gray, ConsoleColor.Black);
            s.Put(w - 1, y, '│', ConsoleColor.Gray, ConsoleColor.Black);
        }

        // Copy visible rows under lock
        List<LogRow> visible;
        lock (_lock)
        {
            int visibleCount = Math.Max(0, bodyHeight);
            int start = Math.Max(0, Rows.Count - visibleCount);
            visible = Rows.Skip(start).ToList();
        }

        int drawY = bodyTop;
        foreach (var row in visible)
        {
            if (drawY >= h - 1) break;
            DrawRow(s, drawY++, row);
        }
    }

    private static void DrawHeader(IConsoleSurface s)
    {
        int w = s.Width;
        if (w < 10) return;

        s.Write(0, 0,
            "┌" + new string('─', Math.Max(0, w - 2)) + "┐",
            ConsoleColor.Gray, ConsoleColor.Black);

        s.Write(0, 1,
            "│" + new string(' ', Math.Max(0, w - 2)) + "│",
            ConsoleColor.Gray, ConsoleColor.Black);

        s.Write(0, 2,
            "├" + new string('─', Math.Max(0, w - 2)) + "┤",
            ConsoleColor.Gray, ConsoleColor.Black);

        int x = 1;
        WriteHeaderCell(s, ref x, "Time", TimeWidth);
        WriteSep(s, ref x);
        WriteHeaderCell(s, ref x, "Lvl", LevelWidth);
        WriteSep(s, ref x);
        WriteHeaderCell(s, ref x, "Category", CategoryWidth);
        WriteSep(s, ref x);

        int msgW = Math.Max(0,
            (w - 2) - (TimeWidth + LevelWidth + CategoryWidth + 3 * 3));

        WriteHeaderCell(s, ref x, "Msg", msgW);
    }

    private static void WriteHeaderCell(
        IConsoleSurface s, ref int x, string text, int width)
    {
        if (width <= 0) return;

        s.Write(x, 1,
            Fit(text, width).PadRight(width),
            ConsoleColor.White, ConsoleColor.Black);

        x += width;
    }

    private static void WriteSep(IConsoleSurface s, ref int x)
    {
        s.Put(x++, 1, ' ', ConsoleColor.Gray, ConsoleColor.Black);
        s.Put(x++, 1, '│', ConsoleColor.Gray, ConsoleColor.Black);
        s.Put(x++, 1, ' ', ConsoleColor.Gray, ConsoleColor.Black);
    }

    private static void DrawRow(IConsoleSurface s, int y, LogRow r)
    {
        int w = s.Width;
        int x = 1;

        s.Write(x, y,
            Fit($"[{r.Time:HH:mm:ss.fff}]", TimeWidth).PadRight(TimeWidth),
            ConsoleColor.DarkCyan, ConsoleColor.Black);
        x += TimeWidth + 3;

        s.Write(x, y,
            Fit(r.Level.ToUpperInvariant(), LevelWidth).PadRight(LevelWidth),
            LevelColor(r.Level), ConsoleColor.Black);
        x += LevelWidth + 3;

        s.Write(x, y,
            Fit(r.Category, CategoryWidth).PadRight(CategoryWidth),
            ConsoleColor.Cyan, ConsoleColor.Black);
        x += CategoryWidth + 3;

        int msgW = Math.Max(0, (w - 1) - x);
        s.Write(x, y, Fit(r.Message, msgW),
            ConsoleColor.White, ConsoleColor.Black);
    }

    // ============================================================
    // Data
    // ============================================================

    private static void Add(LogRow row)
    {
        lock (_lock)
        {
            if (Rows.Count >= 5000)
                Rows.RemoveAt(0);

            Rows.Add(row);
            _dirty = true;
        }
    }

    private static void AddSystem(string msg)
        => Add(new LogRow(DateTime.Now, "Info", "Logger", msg));

    private static ConsoleColor LevelColor(string level) => level switch
    {
        "Trace" => ConsoleColor.DarkGray,
        "Debug" => ConsoleColor.DarkBlue,
        "Info" => ConsoleColor.White,
        "Warn" => ConsoleColor.Yellow,
        "Error" => ConsoleColor.Red,
        _ => ConsoleColor.DarkGray
    };

    private static string Fit(string s, int width)
    {
        if (width <= 0) return string.Empty;
        if (s.Length <= width) return s;
        return width == 1 ? "…" : s[..(width - 1)] + "…";
    }

    private readonly record struct LogRow(
        DateTime Time,
        string Level,
        string Category,
        string Message);
}
