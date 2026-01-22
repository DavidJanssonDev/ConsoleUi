using ConsoleUi.Core.Logging;
using ConsoleUi.Logger.Rendering;
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
    // ----- UI layout constants -----
    // HeaderHeight is how many rows the header box occupies.
    private const int HeaderHeight = 3;

    // Column widths for the table layout.
    private const int TimeWidth = 14;
    private const int LevelWidth = 6;
    private const int CategoryWidth = 14;
    private const int FileWidth = 18;
    private const int LineWidth = 6;
    private const int ProjectWidth = 14; // tweak

    // ----- State -----
    /// <summary>
    /// The log history shown in the UI (max 5000 rows).
    /// </summary>
    private static readonly List<LogRow> Rows = new(5000);

    /// <summary>
    /// How far the view is scrolled up from the bottom.
    /// 0 means “follow live logs”.
    /// </summary>
    private static int _scrollOffsetFromBottom = 0;

    /// <summary>
    /// If true, something changed and we must redraw the UI.
    /// </summary>
    private static volatile bool _dirty = true;

    /// <summary>
    /// Protects shared state (<see cref="Rows"/> and <see cref="_scrollOffsetFromBottom"/>) from multi-thread access.
    /// </summary>
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

        // The fast “frame buffer” for flicker-free drawing
        WinConsoleBuffer buffer = new();

        /// Render loop (UI thread) runs in the background
        using CancellationTokenSource renderCts = new ();
        Task renderTask = Task.Run(() => RenderLoopAsync(buffer, renderCts.Token));

        // Pipe server loop(data thread)
        // This loop recreates the server each time so the demo app can reconnect.
        while (true)
        {
            // Create a named pipe server that reads from the client (PipeDirection.In).
            await using NamedPipeServerStream server = new(
                PipeConstants.PipeName,
                PipeDirection.In,
                1,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous
            );

            AddSystem("Waiting for renderer connection...");

            // Wait until the demo app connects
            await server.WaitForConnectionAsync();
            AddSystem("Renderer connected.");

            try
            {
                // Read the pipe as text lines (UTF-8)
                using StreamReader reader = new(server, Encoding.UTF8, false, 4096, leaveOpen: true);

                while (true)
                {
                    // Read one log line
                    string? line = await reader.ReadLineAsync();
                    if (line is null) break;

                    // If null → client disconnected
                    string[] parts = line.Split('|', 6);


                    // Expected format:
                    // LEVEL|CATEGORY|PROJECT|FILE|LINE|MESSAGE
                    //
                    // Split into at most 6 parts so the message can contain extra '|' safely
                    // (we also escape on the sender side).
                    string level = parts.Length > 0 ? parts[0] : "Info";
                    string cat = parts.Length > 1 ? parts[1] : "General";
                    string project = parts.Length > 2 ? parts[2] : "";
                    string file = parts.Length > 3 ? parts[3] : "";
                    string lineNo = parts.Length > 4 ? parts[4] : "";
                    string msg = parts.Length > 5 ? parts[5] : line;

                    // Add a row for the UI to display
                    Add(new LogRow(DateTime.Now, level, cat, project, file, lineNo, msg));
                }
            }
            catch (IOException ex)
            {
                // If the pipe breaks unexpectedly (client crashed, etc.)
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
    private static async Task RenderLoopAsync(WinConsoleBuffer buffer, CancellationToken token)
    {
        TimeSpan delay = TimeSpan.FromMilliseconds(33); // ~30fps

        while (!token.IsCancellationRequested)
        {
            // If window size changed, rebuild the internal buffer and force redraw.
            if (buffer.ResizeIfNeeded())
            {
                _dirty = true; 
            }

            // Read keys and update scrolling.
            HandleInput(buffer);

            // Redraw only when something changed (new logs, scroll, resize).
            if (_dirty)
            {
                Draw(buffer);
                buffer.Present();
                _dirty = false;
            }

            await Task.Delay(delay, token);
        }
    }


    /// <summary>
    /// Handles user scrolling input using arrow keys and page keys.
    /// </summary>
    private static void HandleInput(WinConsoleBuffer buffer)
    {
        // Drain all pending keys each tick.
        // If you hold a key, multiple events might be pending.
        while (Console.KeyAvailable)
        {
            ConsoleKey key = Console.ReadKey(intercept: true).Key;

            // “Page size” = number of visible log lines in the body
            int page = Math.Max(1, (buffer.Height - HeaderHeight - 1)); 

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
                        // Jump to "top": oldest visible. Offset becomes max.
                        _scrollOffsetFromBottom = GetMaxScrollOffset(buffer);
                        break;

                    case ConsoleKey.End:
                        // Jump back to live tail
                        _scrollOffsetFromBottom = 0;
                        break;
                }

                // Clamp the scroll offset to a valid range.
                if (_scrollOffsetFromBottom < 0) _scrollOffsetFromBottom = 0;

                int max = GetMaxScrollOffset(buffer);
                if (_scrollOffsetFromBottom > max) _scrollOffsetFromBottom = max;
            }

            // Input changed what we’re viewing → redraw.
            _dirty = true;
        }
    }

    /// <summary>
    /// Returns the maximum scroll offset possible for the current window height.
    /// </summary>
    private static int GetMaxScrollOffset(WinConsoleBuffer buf)
    {
        int visible = Math.Max(1, (buf.Height - HeaderHeight - 1)); // body lines (excluding bottom border)
        int total = Rows.Count;

        // If everything fits on screen, no scrolling is possible.
        if (total <= visible) return 0;

        // If you want the oldest visible page:
        // offset = total - visible
        return total - visible;
    }


    /// <summary>
    /// Draws the entire UI (header + borders + visible rows) into the buffer.
    /// </summary>
    private static void Draw(WinConsoleBuffer buffer)
    {
        int width = buffer.Width;
        int hight = buffer.Height;
        if (width <= 0 || hight <= 0) return;

        // Clear the whole buffer (like erasing a whiteboard)
        buffer.Clear(ConsoleColor.Gray, ConsoleColor.Black);

        // Header box and column labels
        DrawHeader(buffer);

        // Body area (below header)
        int bodyTop = HeaderHeight;
        int bodyBottom = hight - 1;
        int bodyHeight = Math.Max(0, bodyBottom - bodyTop);

        // Borders around body
        buffer.Write(0, bodyTop - 1, "├" + new string('─', Math.Max(0, width - 2)) + "┤", ConsoleColor.Gray, ConsoleColor.Black);
        buffer.Write(0, hight - 1, "└" + new string('─', Math.Max(0, width - 2)) + "┘", ConsoleColor.Gray, ConsoleColor.Black);

        for (int y = bodyTop; y < hight - 1; y++)
        {
            buffer.Put(0, y, '│', ConsoleColor.Gray, ConsoleColor.Black);
            buffer.Put(width - 1, y, '│', ConsoleColor.Gray, ConsoleColor.Black);
        }

        // Visible log lines (exclude bottom border row)
        int visible = Math.Max(0, bodyHeight);

        int start;
        int end;

        // Decide which slice of Rows to display based on scroll offset.
        lock (_lock)
        {
            int total = Rows.Count;

            // end is exclusive (like typical C# slicing)
            end = total - _scrollOffsetFromBottom;
            if (end < 0) end = 0;
            if (end > total) end = total;

            start = Math.Max(0, end - visible);

            // Keep scroll offset valid if the list size changed.
            int maxOffset = Math.Max(0, total - visible);
            if (_scrollOffsetFromBottom > maxOffset) _scrollOffsetFromBottom = maxOffset;
            if (_scrollOffsetFromBottom < 0) _scrollOffsetFromBottom = 0;
        }

        int drawY = bodyTop;

        // Draw each visible row
        for (int index = start; index < end && drawY < hight - 1; index++, drawY++)
        {
            LogRow row;
            lock (_lock) { row = Rows[index]; } // safe even if another thread adds logs
            DrawRow(buffer, drawY, row);
        }
    }

    /// <summary>
    /// Draws the top header box and column names.
    /// </summary>
    private static void DrawHeader(WinConsoleBuffer buffer)
    {
        int width = buffer.Width;
        if (width < 10) return;

        buffer.Write(0, 0, "┌" + new string('─', Math.Max(0, width - 2)) + "┐", ConsoleColor.Gray, ConsoleColor.Black);
        buffer.Write(0, 1, "│" + new string(' ', Math.Max(0, width - 2)) + "│", ConsoleColor.Gray, ConsoleColor.Black);
        buffer.Write(0, 2, "├" + new string('─', Math.Max(0, width - 2)) + "┤", ConsoleColor.Gray, ConsoleColor.Black);

        // Column labels
        int x = 1;
        WriteHeaderCell(buffer, ref x, "Time", TimeWidth);
        WriteSep(buffer, ref x);
        WriteHeaderCell(buffer, ref x, "Lvl", LevelWidth);
        WriteSep(buffer, ref x);
        WriteHeaderCell(buffer, ref x, "Category", CategoryWidth);
        WriteSep(buffer, ref x);
        WriteHeaderCell(buffer, ref x, "Project", ProjectWidth);
        WriteSep(buffer, ref x);
        WriteHeaderCell(buffer, ref x, "File", FileWidth);
        WriteSep(buffer, ref x);
        WriteHeaderCell(buffer, ref x, "Line", LineWidth);
        WriteSep(buffer, ref x);

        // Message column takes whatever space is left.
        int separators = 6; // Time, Lvl, Category, Project, File, Line => separators before Msg
        int msgW = Math.Max(0, (width - 2) - (TimeWidth + LevelWidth + CategoryWidth + ProjectWidth + FileWidth + LineWidth + separators * 3));
        WriteHeaderCell(buffer, ref x, "Msg", msgW);

        WriteHeaderCell(buffer, ref x, "Msg", msgW);

    }

    /// <summary>
    /// Writes a header cell label, padded/truncated to fit.
    /// </summary>
    private static void WriteHeaderCell(WinConsoleBuffer buffer, ref int x, string text, int width)
    {
        if (width <= 0) return;
        buffer.Write(x, 1, Fit(text, width).PadRight(width), ConsoleColor.White, ConsoleColor.Black);
        x += width;
    }

    /// <summary>
    /// Writes the separator between columns: " │ "
    /// </summary>
    private static void WriteSep(WinConsoleBuffer b, ref int x)
    {
        b.Put(x, 1, ' ', ConsoleColor.Gray, ConsoleColor.Black);
        x++;
        b.Put(x, 1, '│', ConsoleColor.Gray, ConsoleColor.Black);
        x++;
        b.Put(x, 1, ' ', ConsoleColor.Gray, ConsoleColor.Black);
        x++;
    }

    /// <summary>
    /// Draws one log row into the table.
    /// </summary>
    private static void DrawRow(WinConsoleBuffer buffer, int y, LogRow row)
    {
        int width = buffer.Width;
        int x = 1;

        buffer.Write(x, y, Fit($"[{row.Time:HH:mm:ss.fff}]", TimeWidth).PadRight(TimeWidth),
            ConsoleColor.DarkCyan, ConsoleColor.Black);
        x += TimeWidth;
        x += 3;

        buffer.Write(x, y, Fit(row.Level.ToUpperInvariant(), LevelWidth).PadRight(LevelWidth),
            LevelColor(row.Level), ConsoleColor.Black);
        x += LevelWidth;
        x += 3;

        buffer.Write(x, y, Fit(row.Category, CategoryWidth).PadRight(CategoryWidth),
            ConsoleColor.Cyan, ConsoleColor.Black);
        x += CategoryWidth;
        x += 3;

        buffer.Write(x, y, Fit(row.Project, ProjectWidth).PadRight(ProjectWidth),
            ConsoleColor.DarkCyan, ConsoleColor.Black);
        x += ProjectWidth;
        x += 3;

        buffer.Write(x, y, Fit(row.File, FileWidth).PadRight(FileWidth),
            ConsoleColor.Gray, ConsoleColor.Black);
        x += FileWidth;
        x += 3;

        buffer.Write(x, y, Fit(row.Line, LineWidth).PadRight(LineWidth),
            ConsoleColor.DarkGray, ConsoleColor.Black);
        x += LineWidth;
        x += 3;

        int msgWidth = Math.Max(0, width - 1 - x);
        buffer.Write(x, y, Fit(row.Message, msgWidth), ConsoleColor.White, ConsoleColor.Black);
    }

    /// <summary>
    /// Adds a row to the history, keeping max size and adjusting scroll if needed.
    /// </summary>
    private static void Add(LogRow row)
    {
        lock (_lock)
        {
            // Keep only the newest 5000 rows
            if (Rows.Count >= 5000) Rows.RemoveAt(0);

            Rows.Add(row);

            // If user is scrolled up, keep the same content visible by increasing offset
            if (_scrollOffsetFromBottom > 0)
            {
                _scrollOffsetFromBottom++;
            }
        }

        // New data → redraw
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
    private static string Fit(string @string, int width)
    {
        if (width <= 0) return string.Empty;
        if (@string.Length <= width) return @string;
        return width == 1 ? "…" : @string[..(width - 1)] + "…";
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
