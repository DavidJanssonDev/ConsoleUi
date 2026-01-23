using ConsoleUi.Core.Logging;
using ConsoleUi.Rendering;
using ConsoleUi.Rendering.Abstractions;
using ConsoleUI.Rendering;
using ConsoleUI.Rendering.Abstractions;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

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
    private const int FileWidth = 18;
    private const int LineWidth = 6;
    private const int ProjectWidth = 14;

    private static readonly List<LogRow> Rows = new(5000);
    private static int _scrollOffsetFromBottom = 0;
    private static volatile bool _dirty = true;

    private static readonly Lock _lock = new();

    /// <summary>
    /// Program entry point.
    /// </summary>
    /// <remarks>
    /// Starts the render loop in the background, then continuously waits for pipe connections.<br/>
    /// When a client connects, reads log lines until disconnect, then waits for a new connection again.
    /// </remarks>
    public static async Task Main()
    {
        // Basic console setup
        Console.Title = "UI Logs";
        Console.OutputEncoding = Encoding.UTF8;
        Console.CursorVisible = false;

        // The fast “frame buffer” for flicker-free drawinusing IConsoleSurface surface = ConsoleSurface.CreateDefault();
        using IConsoleSurface surface = ConsoleSurface.CreateDefault();

        /// Render loop (UI thread) runs in the background
        using CancellationTokenSource renderCts = new();
        Task renderTask = Task.Run(() => RenderLoopAsync(surface, renderCts.Token));

        // Pipe server loop (data thread)
        while (true)
        {
            await using NamedPipeServerStream server = new(
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
                using StreamReader reader = new(server, Encoding.UTF8, false, 4096, leaveOpen: true);

                while (true)
                {
                    string? line = await reader.ReadLineAsync();
                    if (line is null) break;

                    // Expected format:
                    // LEVEL|CATEGORY|PROJECT|FILE|LINE|MESSAGE
                    string[] parts = line.Split('|', 6);

                    string level = parts.Length > 0 ? parts[0] : "Info";
                    string cat = parts.Length > 1 ? parts[1] : "General";
                    string project = parts.Length > 2 ? parts[2] : "";
                    string file = parts.Length > 3 ? parts[3] : "";
                    string lineNo = parts.Length > 4 ? parts[4] : "";
                    string msg = parts.Length > 5 ? parts[5] : line;

                    Add(new LogRow(DateTime.Now, level, cat, project, file, lineNo, msg));
                }
            }
            catch (IOException ex)
            {
                AddSystem($"Pipe error: {ex.Message}");
            }

            AddSystem("Renderer disconnected.");
        }
    }

    /// <summary>
    /// The UI render loop.
    /// </summary>
    /// <remarks>
    /// Runs ~30 FPS:
    /// <list type="number">
    ///   <item><description>Resizes buffer if the window size changed</description></item>
    ///   <item><description>Handles keyboard input (scrolling)</description></item>
    ///   <item><description>Redraws only when <see cref="_dirty"/> is true</description></item>
    /// </list>
    /// </remarks>
    private static async Task RenderLoopAsync(IConsoleSurface surface, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(surface);
        TimeSpan delay = TimeSpan.FromMilliseconds(33);

        while (!token.IsCancellationRequested)
        {
            if (surface.ResizeIfNeeded())
                _dirty = true;

            HandleInput(surface);

            if (_dirty)
            {
                Draw(surface);
                surface.Present();
                _dirty = false;
            }

            await Task.Delay(delay, token);
        }
    }


    /// <summary>
    /// Handles user scrolling input using arrow keys and page keys.
    /// </summary>
    private static void HandleInput(IConsoleSurface surface)
    {
        while (Console.KeyAvailable)
        {
            ConsoleKey key = Console.ReadKey(intercept: true).Key;
            int page = Math.Max(1, (surface.Height - HeaderHeight - 1));

            lock (_lock)
            {
                switch (key)
                {
                    case ConsoleKey.UpArrow:
                        _scrollOffsetFromBottom += 1;
                        break;

                    case ConsoleKey.DownArrow:
                        _scrollOffsetFromBottom -= 1;
                        break;

                    case ConsoleKey.PageUp:
                        _scrollOffsetFromBottom += page;
                        break;

                    case ConsoleKey.PageDown:
                        _scrollOffsetFromBottom -= page;
                        break;

                    case ConsoleKey.Home:
                        _scrollOffsetFromBottom = GetMaxScrollOffset(surface);
                        break;

                    case ConsoleKey.End:
                        _scrollOffsetFromBottom = 0;
                        break;
                }

                if (_scrollOffsetFromBottom < 0) _scrollOffsetFromBottom = 0;

                int max = GetMaxScrollOffset(surface);
                if (_scrollOffsetFromBottom > max) _scrollOffsetFromBottom = max;
            }

            _dirty = true;
        }
    }

    /// <summary>
    /// Returns the maximum scroll offset possible for the current window height.
    /// </summary>
    private static int GetMaxScrollOffset(IConsoleSurface surface)
    {
        int visible = Math.Max(1, (surface.Height - HeaderHeight - 1));
        int total = Rows.Count;

        if (total <= visible) return 0;
        return total - visible;
    }


    /// <summary>
    /// Draws the entire UI (header + borders + visible rows) into the buffer.
    /// </summary>
    private static void Draw(IConsoleSurface surface)
    {
        int width = surface.Width;
        int height = surface.Height;
        if (width <= 0 || height <= 0) return;

        surface.Clear(ConsoleColor.Gray, ConsoleColor.Black);

        DrawHeader(surface);

        int bodyTop = HeaderHeight;
        int bodyBottom = height - 1;
        int bodyHeight = Math.Max(0, bodyBottom - bodyTop);

        surface.Write(0, bodyTop - 1, "├" + new string('─', Math.Max(0, width - 2)) + "┤", ConsoleColor.Gray, ConsoleColor.Black);
        surface.Write(0, height - 1, "└" + new string('─', Math.Max(0, width - 2)) + "┘", ConsoleColor.Gray, ConsoleColor.Black);

        for (int y = bodyTop; y < height - 1; y++)
        {
            surface.Put(0, y, '│', ConsoleColor.Gray, ConsoleColor.Black);
            surface.Put(width - 1, y, '│', ConsoleColor.Gray, ConsoleColor.Black);
        }

        int visible = Math.Max(0, bodyHeight);

        int start;
        int end;

        lock (_lock)
        {
            int total = Rows.Count;

            end = total - _scrollOffsetFromBottom;
            if (end < 0) end = 0;
            if (end > total) end = total;

            start = Math.Max(0, end - visible);

            int maxOffset = Math.Max(0, total - visible);
            if (_scrollOffsetFromBottom > maxOffset) _scrollOffsetFromBottom = maxOffset;
            if (_scrollOffsetFromBottom < 0) _scrollOffsetFromBottom = 0;
        }

        int drawY = bodyTop;

        for (int index = start; index < end && drawY < height - 1; index++, drawY++)
        {
            LogRow row;
            lock (_lock) { row = Rows[index]; }
            DrawRow(surface, drawY, row);
        }
    }

    /// <summary>
    /// Draws the top header box and column names.
    /// </summary>
    private static void DrawHeader(IConsoleSurface surface)
    {
        int width = surface.Width;
        if (width < 10) return;

        surface.Write(0, 0, "┌" + new string('─', Math.Max(0, width - 2)) + "┐", ConsoleColor.Gray, ConsoleColor.Black);
        surface.Write(0, 1, "│" + new string(' ', Math.Max(0, width - 2)) + "│", ConsoleColor.Gray, ConsoleColor.Black);
        surface.Write(0, 2, "├" + new string('─', Math.Max(0, width - 2)) + "┤", ConsoleColor.Gray, ConsoleColor.Black);

        int x = 1;
        WriteHeaderCell(surface, ref x, "Time", TimeWidth);
        WriteSep(surface, ref x);
        WriteHeaderCell(surface, ref x, "Lvl", LevelWidth);
        WriteSep(surface, ref x);
        WriteHeaderCell(surface, ref x, "Category", CategoryWidth);
        WriteSep(surface, ref x);
        WriteHeaderCell(surface, ref x, "Project", ProjectWidth);
        WriteSep(surface, ref x);
        WriteHeaderCell(surface, ref x, "File", FileWidth);
        WriteSep(surface, ref x);
        WriteHeaderCell(surface, ref x, "Line", LineWidth);
        WriteSep(surface, ref x);

        int separators = 6;
        int msgW = Math.Max(0, (width - 2) - (TimeWidth + LevelWidth + CategoryWidth + ProjectWidth + FileWidth + LineWidth + separators * 3));
        WriteHeaderCell(surface, ref x, "Msg", msgW);
    }

    /// <summary>
    /// Writes a header cell label, padded/truncated to fit.
    /// </summary>
    private static void WriteHeaderCell(IConsoleSurface surface, ref int x, string text, int width)
    {
        if (width <= 0) return;
        surface.Write(x, 1, Fit(text, width).PadRight(width), ConsoleColor.White, ConsoleColor.Black);
        x += width;
    }

    /// <summary>
    /// Writes the separator between columns: " │ "
    /// </summary>
    private static void WriteSep(IConsoleSurface surface, ref int x)
    {
        surface.Put(x, 1, ' ', ConsoleColor.Gray, ConsoleColor.Black);
        x++;
        surface.Put(x, 1, '│', ConsoleColor.Gray, ConsoleColor.Black);
        x++;
        surface.Put(x, 1, ' ', ConsoleColor.Gray, ConsoleColor.Black);
        x++;
    }

    /// <summary>
    /// Draws one log row into the table.
    /// </summary>
    private static void DrawRow(IConsoleSurface surface, int y, LogRow row)
    {
        int width = surface.Width;
        int x = 1;

        surface.Write(x, y, Fit($"[{row.Time:HH:mm:ss.fff}]", TimeWidth).PadRight(TimeWidth),
            ConsoleColor.DarkCyan, ConsoleColor.Black);
        x += TimeWidth;
        x += 3;

        surface.Write(x, y, Fit(row.Level.ToUpperInvariant(), LevelWidth).PadRight(LevelWidth),
            LevelColor(row.Level), ConsoleColor.Black);
        x += LevelWidth;
        x += 3;

        surface.Write(x, y, Fit(row.Category, CategoryWidth).PadRight(CategoryWidth),
            ConsoleColor.Cyan, ConsoleColor.Black);
        x += CategoryWidth;
        x += 3;

        surface.Write(x, y, Fit(row.Project, ProjectWidth).PadRight(ProjectWidth),
            ConsoleColor.DarkCyan, ConsoleColor.Black);
        x += ProjectWidth;
        x += 3;

        surface.Write(x, y, Fit(row.File, FileWidth).PadRight(FileWidth),
            ConsoleColor.Gray, ConsoleColor.Black);
        x += FileWidth;
        x += 3;

        surface.Write(x, y, Fit(row.Line, LineWidth).PadRight(LineWidth),
            ConsoleColor.DarkGray, ConsoleColor.Black);
        x += LineWidth;
        x += 3;

        int msgWidth = Math.Max(0, width - 1 - x);
        surface.Write(x, y, Fit(row.Message, msgWidth), ConsoleColor.White, ConsoleColor.Black);
    }

    /// <summary>
    /// Adds a row to the history, keeping max size and adjusting scroll if needed.
    /// </summary>
    private static void Add(LogRow row)
    {
        lock (_lock)
        {
            if (Rows.Count >= 5000) Rows.RemoveAt(0);
            Rows.Add(row);

            if (_scrollOffsetFromBottom > 0)
                _scrollOffsetFromBottom++;
        }

        _dirty = true;
    }

    /// <summary>
    /// Adds an internal logger/system message (not coming from the pipe).
    /// </summary>
    private static void AddSystem(string msg)
        => Add(new LogRow(DateTime.Now, "Info", "Logger", "ConsoleUi.Logger", "Program.cs", "0", msg));

    /// <summary>
    /// Maps log levels to console colors.
    /// </summary>
    private static ConsoleColor LevelColor(string level) => level switch
    {
        "Trace" => ConsoleColor.DarkGray,
        "Debug" => ConsoleColor.DarkBlue,
        "Info" => ConsoleColor.White,
        "Warn" => ConsoleColor.Yellow,
        "Error" => ConsoleColor.Red,
        _ => ConsoleColor.DarkGray
    };

    /// <remarks>
    /// 
    /// Makes a string fit a fixed width:
    /// <list type="bullet">
    ///   <item><description><b> if shorter</b>: returns as-is.</description></item>
    ///   <item><description><b> if longer</b> : truncates and adds an ellipsis.</description></item>
    /// </list>
    /// </remarks>
    private static string Fit(string s, int width)
    {
        if (width <= 0) return string.Empty;
        if (s.Length <= width) return s;
        return width == 1 ? "…" : s[..(width - 1)] + "…";
    }


    /// <summary>
    /// One row of the table UI.
    /// </summary>
    private readonly record struct LogRow(
        DateTime Time,
        string Level,
        string Category,
        string Project,
        string File,
        string Line,
        string Message
    );
}
