using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;

namespace ConsoleUi.Demo.Logging;

public readonly struct CategoryLogger(AsyncPipeLogger root, string category)
{
    private readonly AsyncPipeLogger _root = root;
    private readonly string _category = category;

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

    private void Write(string level, string msg, string file, int line)
    {
        string fileName = Path.GetFileName(file);
        string project = InferProjectFromFilePath(file);

        // LEVEL|CATEGORY|PROJECT|FILE|LINE|MESSAGE
        string payload = $"{level}|{_category}|{project}|{fileName}|{line}|{Escape(msg)}";
        _root.Enqueue(payload);
    }

    private static string InferProjectFromFilePath(string filePath)
    {
        // Example filePath contains: ...\ConsoleUi.Demo\...\Something.cs
        // We pick the folder name that looks like a project.
        // Adjust the prefixes to match your real folder naming.

        if (string.IsNullOrWhiteSpace(filePath))
            return "Unknown";

        string[] parts = filePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        foreach (string p in parts)
        {
            if (p.StartsWith("ConsoleUi.", StringComparison.OrdinalIgnoreCase))
                return p; // e.g. ConsoleUi.Demo, ConsoleUi.Core
        }

        // fallback: directory name above file
        try
        {
            string? dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dir))
                return Path.GetFileName(dir);
        }
        catch { }

        return "Unknown";
    }

    private static string Escape(string s)
        => s.Replace("|", "¦").Replace("\n", "\\n");

}
