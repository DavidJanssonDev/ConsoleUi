using System;
using System.IO; // Path helpers
using System.Runtime.CompilerServices; // Caller info attributes

namespace ConsoleUi.Demo.Logging;

/// <summary>
/// A lightweight logger bound to a specific category (Render, Layout, etc.).
/// </summary>
/// <remarks>
/// This struct: <br/>
/// - formats log messages <br/>
/// - adds metadata (file, line, project) <br/>
/// - sends the final string to AsyncPipeLogger <br/>
/// </remarks>
public readonly struct CategoryLogger
{
    // Reference to the shared root logger
    private readonly AsyncPipeLogger _root;

    // Category name (e.g. "Render", "Layout")
    private readonly string _category;

    // Constructor (called from AsyncPipeLogger.For)
    public CategoryLogger(AsyncPipeLogger root, string category)
    {
        _root = root;
        _category = category;
    }

    // -------------------------------------------

    #region Public Logging Methods

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

    #endregion

    // -------------------------------------------

    #region Core formatter 

    private void Write(string level, string msg, string file, int line)
    {
        // Extract just the file name (Program.cs instead of full path)
        string fileName = Path.GetFileName(file);

        // Guess which project this file belongs to
        string project = InferProjectFromFilePath(file);

        // Build the final message format
        // LEVEL|CATEGORY|PROJECT|FILE|LINE|MESSAGE
        string payload = $"{level}|{_category}|{project}|{fileName}|{line}|{Escape(msg)}";

        // Send it to the async logger
        _root.Enqueue(payload);
    }

    #endregion

    // -------------------------------------------

    #region Helpers

    private static string InferProjectFromFilePath(string filePath)
    {
        // If compiler didn’t give us a file path
        if (string.IsNullOrWhiteSpace(filePath))
            return "Unknown";

        // Split path into folders
        string[] parts = filePath.Split(
            Path.DirectorySeparatorChar, 
            Path.AltDirectorySeparatorChar
        );

        // Look for folders like "ConsoleUi.Demo" or "ConsoleUi.Core"
        foreach (string p in parts)
        {
            if (p.StartsWith("ConsoleUi.", StringComparison.OrdinalIgnoreCase))
                return p; 
        }

        // Fallback: use the parent directory name
        try
        {
            string? dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dir))
                return Path.GetFileName(dir);
        }
        catch { }

        return "Unknown";
    }

    // Replace characters that would break pipe parsing
    private static string Escape(string s)
        => s.Replace("|", "¦").Replace("\n", "\\n");

    #endregion
}
