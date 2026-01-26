using System;
using System.IO; // Path helpers
using System.Runtime.CompilerServices; // Caller info attributes

namespace ConsoleUI.Demo.Logging;

/// <summary>
/// A lightweight logger bound to a specific category (Render, Layout, etc.).
/// </summary>
/// <remarks>
/// This struct:
/// <list type="bullet">
///   <item>
///     <description>Adds metadata (file, line, project)</description>
///   </item>
///   <item>
///     <description>Delegates formatting and transport to <c>AsyncPipeLogger</c></description>
///   </item>
/// </list>
/// </remarks>

public readonly struct CategoryLogger
{
    private readonly AsyncPipeLogger _root;
    private readonly string _category;

    public CategoryLogger(AsyncPipeLogger root, string category)
    {
        _root = root;
        _category = category;
    }

    // -------------------------------------------------
    // Public API
    // -------------------------------------------------

    public void Trace(string msg,
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0)
        => Write("Trace", msg, file, line);

    public void Debug(string msg,
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0)
        => Write("Debug", msg, file, line);

    public void Info(string msg,
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0)
        => Write("Info", msg, file, line);

    public void Warn(string msg,
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0)
        => Write("Warn", msg, file, line);

    public void Error(string msg,
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0)
        => Write("Error", msg, file, line);

    // -------------------------------------------------
    // Core formatter
    // -------------------------------------------------

    private void Write(string level, string msg, string file, int line)
    {
        string fileName = Path.GetFileName(file);
        string project = InferProjectFromFilePath(file);

        _root.Enqueue(
            level: level,
            category: _category,
            project: project,
            file: fileName,
            line: line,
            message: msg
        );
    }

    // -------------------------------------------------
    // Helpers
    // -------------------------------------------------

    private static string InferProjectFromFilePath(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            return "Unknown";

        string[] parts = filePath.Split(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar
        );

        foreach (string p in parts)
        {
            if (p.StartsWith("ConsoleUi.", StringComparison.OrdinalIgnoreCase))
                return p;
        }

        try
        {
            string? dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dir))
                return Path.GetFileName(dir);
        }
        catch { }

        return "Unknown";
    }
}
