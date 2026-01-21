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

    private static readonly List<LogRow> Rows = new(5000);
    private static volatile bool _dirty = true;

    public static async Task Main()
    {
        Console.Title = "UI Logs";
        Console.OutputEncoding = Encoding.UTF8;
        Console.CursorVisible = false;

        var buffer = new WinConsoleBuffer();

        // Render loop (UI thread)
        using var renderCts = new CancellationTokenSource();
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
                using var reader = new StreamReader(server, Encoding.UTF8, false, 4096, leaveOpen: true);

                while (true)
                {
                    var line = await reader.ReadLineAsync();
                    if (line is null) break;

                    var parts = line.Split('|', 3);
                    var level = parts.Length > 0 ? parts[0] : "Info";
                    var cat = parts.Length > 1 ? parts[1] : "General";
                    var msg = parts.Length > 2 ? parts[2] : line;

                    Add(new LogRow(DateTime.Now, level, cat, msg));
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
        // ~30fps
        var delay = TimeSpan.FromMilliseconds(33);

        while (!token.IsCancellationRequested)
        {
            buf.ResizeIfNeeded();

            // redraw on resize or dirty
            if (_dirty)
            {
                Draw(buf);
                buf.Present();
                _dirty = false;
            }

            await Task.Delay(delay, token);
        }
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

        int visible = Math.Max(0, bodyHeight);
        int start = Math.Max(0, Rows.Count - visible);
        int drawY = bodyTop;

        for (int i = start; i < Rows.Count && drawY < h - 1; i++, drawY++)
        {
            DrawRow(b, drawY, Rows[i]);
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

        int msgW = Math.Max(0, (w - 2) - (TimeWidth + LevelWidth + CategoryWidth + 3 * 3)); // approx
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

        int msgW = Math.Max(0, (w - 1) - x);
        b.Write(x, y, Fit(r.Message, msgW), ConsoleColor.White, ConsoleColor.Black);
    }

    private static void Add(LogRow row)
    {
        if (Rows.Count >= 5000) Rows.RemoveAt(0);
        Rows.Add(row);
        _dirty = true;
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

    private readonly record struct LogRow(DateTime Time, string Level, string Category, string Message);

}
