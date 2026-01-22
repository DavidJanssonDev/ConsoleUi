using ConsoleUi.Core.Logging;
using ConsoleUi.Logger.Rendering;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading.Tasks;

namespace ConsoleUi.Logger;

public static class Program
{
    private const int HeaderHeight = 3;

    private const int TimeWidth = 14;
    private const int LevelWidth = 6;
    private const int CategoryWidth = 14;
    private const int FileWidth = 18;
    private const int LineWidth = 6;
    private const int ProjectWidth = 14; // tweak


    private static readonly List<LogRow> Rows = new(5000);
    private static int _scrollOffsetFromBottom = 0; // 0 = follow tail
    private static volatile bool _dirty = true;
    private static readonly object _lock = new();


    public static async Task Main()
    {
        Console.Title = "UI Logs";
        Console.OutputEncoding = Encoding.UTF8;
        Console.CursorVisible = false;

        var buffer = new WinConsoleBuffer();

        // Render loop (UI thread)
        using CancellationTokenSource renderCts = new ();
        var renderTask = Task.Run(() => RenderLoopAsync(buffer, renderCts.Token));

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
                using StreamReader reader = new(server, Encoding.UTF8, false, 4096, leaveOpen: true);

                while (true)
                {
                    var line = await reader.ReadLineAsync();
                    if (line is null) break;

                    var parts = line.Split('|', 6);

                    var level = parts.Length > 0 ? parts[0] : "Info";
                    var cat = parts.Length > 1 ? parts[1] : "General";
                    var project = parts.Length > 2 ? parts[2] : "";
                    var file = parts.Length > 3 ? parts[3] : "";
                    var lineNo = parts.Length > 4 ? parts[4] : "";
                    var msg = parts.Length > 5 ? parts[5] : line;

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
    private static async Task RenderLoopAsync(WinConsoleBuffer buf, CancellationToken token)
    {
        TimeSpan delay = TimeSpan.FromMilliseconds(33); // ~30fps

        while (!token.IsCancellationRequested)
        {
            if (buf.ResizeIfNeeded())
            {
                _dirty = true; // forces redraw at new size so borders extend
            }

            HandleInput(buf);

            if (_dirty)
            {
                Draw(buf);
                buf.Present();
                _dirty = false;
            }

            await Task.Delay(delay, token);
        }
    }

    private static void HandleInput(WinConsoleBuffer buf)
    {
        // Drain all pending keys each tick
        while (Console.KeyAvailable)
        {
            ConsoleKey key = Console.ReadKey(intercept: true).Key;

            int page = Math.Max(1, (buf.Height - HeaderHeight - 1)); // visible log lines

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
                        _scrollOffsetFromBottom = GetMaxScrollOffset(buf);
                        break;

                    case ConsoleKey.End:
                        // Jump back to live tail
                        _scrollOffsetFromBottom = 0;
                        break;
                }

                // Clamp
                if (_scrollOffsetFromBottom < 0) _scrollOffsetFromBottom = 0;

                int max = GetMaxScrollOffset(buf);
                if (_scrollOffsetFromBottom > max) _scrollOffsetFromBottom = max;
            }

            _dirty = true;
        }
    }

    private static int GetMaxScrollOffset(WinConsoleBuffer buf)
    {
        int visible = Math.Max(1, (buf.Height - HeaderHeight - 1)); // body lines (excluding bottom border)
        int total = Rows.Count;

        // When total <= visible: no scrolling possible
        if (total <= visible) return 0;

        // Max offset means "show oldest visible page"
        // end = total - offset; start = end - visible; want start = 0 => end = visible => offset = total - visible
        return total - visible;
    }


    private static void Draw(WinConsoleBuffer b)
    {
        int w = b.Width;
        int h = b.Height;
        if (w <= 0 || h <= 0) return;

        b.Clear(ConsoleColor.Gray, ConsoleColor.Black);

        DrawHeader(b);

        int bodyTop = HeaderHeight;
        int bodyBottom = h - 1;
        int bodyHeight = Math.Max(0, bodyBottom - bodyTop);

        // Borders
        b.Write(0, bodyTop - 1, "├" + new string('─', Math.Max(0, w - 2)) + "┤", ConsoleColor.Gray, ConsoleColor.Black);
        b.Write(0, h - 1, "└" + new string('─', Math.Max(0, w - 2)) + "┘", ConsoleColor.Gray, ConsoleColor.Black);

        for (int y = bodyTop; y < h - 1; y++)
        {
            b.Put(0, y, '│', ConsoleColor.Gray, ConsoleColor.Black);
            b.Put(w - 1, y, '│', ConsoleColor.Gray, ConsoleColor.Black);
        }

        // Visible log lines (exclude bottom border line)
        int visible = Math.Max(0, bodyHeight);

        int start;
        int end;

        lock (_lock)
        {
            int total = Rows.Count;

            // end is exclusive
            end = total - _scrollOffsetFromBottom;
            if (end < 0) end = 0;
            if (end > total) end = total;

            start = Math.Max(0, end - visible);

            // Clamp scroll offset so it never goes past top
            int maxOffset = Math.Max(0, total - visible);
            if (_scrollOffsetFromBottom > maxOffset) _scrollOffsetFromBottom = maxOffset;
            if (_scrollOffsetFromBottom < 0) _scrollOffsetFromBottom = 0;
        }

        int drawY = bodyTop;

        for (int i = start; i < end && drawY < h - 1; i++, drawY++)
        {
            LogRow row;
            lock (_lock) { row = Rows[i]; } // safe if another thread adds logs
            DrawRow(b, drawY, row);
        }
    }

    private static void DrawHeader(WinConsoleBuffer b)
    {
        int w = b.Width;
        if (w < 10) return;

        b.Write(0, 0, "┌" + new string('─', Math.Max(0, w - 2)) + "┐", ConsoleColor.Gray, ConsoleColor.Black);
        b.Write(0, 1, "│" + new string(' ', Math.Max(0, w - 2)) + "│", ConsoleColor.Gray, ConsoleColor.Black);
        b.Write(0, 2, "├" + new string('─', Math.Max(0, w - 2)) + "┤", ConsoleColor.Gray, ConsoleColor.Black);

        int x = 1;
        WriteHeaderCell(b, ref x, "Time", TimeWidth);
        WriteSep(b, ref x);
        WriteHeaderCell(b, ref x, "Lvl", LevelWidth);
        WriteSep(b, ref x);
        WriteHeaderCell(b, ref x, "Category", CategoryWidth);
        WriteSep(b, ref x);
        WriteHeaderCell(b, ref x, "Project", ProjectWidth);
        WriteSep(b, ref x);
        WriteHeaderCell(b, ref x, "File", FileWidth);
        WriteSep(b, ref x);
        WriteHeaderCell(b, ref x, "Line", LineWidth);
        WriteSep(b, ref x);

        // Msg width = remaining
        int separators = 6; // Time,Lvl,Category,Project,File,Line => separators before Msg
        int msgW = Math.Max(0, (w - 2) - (TimeWidth + LevelWidth + CategoryWidth + ProjectWidth + FileWidth + LineWidth + separators * 3));
        WriteHeaderCell(b, ref x, "Msg", msgW);

        WriteHeaderCell(b, ref x, "Msg", msgW);

    }

    private static void WriteHeaderCell(WinConsoleBuffer b, ref int x, string text, int width)
    {
        if (width <= 0) return;
        b.Write(x, 1, Fit(text, width).PadRight(width), ConsoleColor.White, ConsoleColor.Black);
        x += width;
    }

    private static void WriteSep(WinConsoleBuffer b, ref int x)
    {
        b.Put(x, 1, ' ', ConsoleColor.Gray, ConsoleColor.Black);
        x++;
        b.Put(x, 1, '│', ConsoleColor.Gray, ConsoleColor.Black);
        x++;
        b.Put(x, 1, ' ', ConsoleColor.Gray, ConsoleColor.Black);
        x++;
    }

    private static void DrawRow(WinConsoleBuffer b, int y, LogRow r)
    {
        int w = b.Width;
        int x = 1;

        b.Write(x, y, Fit($"[{r.Time:HH:mm:ss.fff}]", TimeWidth).PadRight(TimeWidth),
            ConsoleColor.DarkCyan, ConsoleColor.Black);
        x += TimeWidth;
        x += 3;

        b.Write(x, y, Fit(r.Level.ToUpperInvariant(), LevelWidth).PadRight(LevelWidth),
            LevelColor(r.Level), ConsoleColor.Black);
        x += LevelWidth;
        x += 3;

        b.Write(x, y, Fit(r.Category, CategoryWidth).PadRight(CategoryWidth),
            ConsoleColor.Cyan, ConsoleColor.Black);
        x += CategoryWidth;
        x += 3;

        b.Write(x, y, Fit(r.Project, ProjectWidth).PadRight(ProjectWidth),
            ConsoleColor.DarkCyan, ConsoleColor.Black);
        x += ProjectWidth;
        x += 3;

        b.Write(x, y, Fit(r.File, FileWidth).PadRight(FileWidth),
            ConsoleColor.Gray, ConsoleColor.Black);
        x += FileWidth;
        x += 3;

        b.Write(x, y, Fit(r.Line, LineWidth).PadRight(LineWidth),
            ConsoleColor.DarkGray, ConsoleColor.Black);
        x += LineWidth;
        x += 3;

        int msgW = Math.Max(0, (w - 1) - x);
        b.Write(x, y, Fit(r.Message, msgW), ConsoleColor.White, ConsoleColor.Black);
    }

    private static void Add(LogRow row)
    {
        lock (_lock)
        {
            if (Rows.Count >= 5000) Rows.RemoveAt(0);
            Rows.Add(row);

            // If user is scrolled up, keep the same content visible by increasing offset
            if (_scrollOffsetFromBottom > 0)
            {
                _scrollOffsetFromBottom++;
            }
        }

        _dirty = true;
    }


    private static void AddSystem(string msg)
        => Add(new LogRow(DateTime.Now, "Info", "Logger", "Program.cs","ConsoleUi.Demo", "0", msg));


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
        string Project,
        string File,
        string Line,
        string Message
    );



}
