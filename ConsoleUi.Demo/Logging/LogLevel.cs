using System;
using System.Collections.Generic;
using System.Text;

namespace ConsoleUi.Demo.Logging;

/// <summary>
/// Represents the severity (importance) of a log message. <br/>
///
/// Higher values mean more serious problems.
/// </summary>
public enum LogLevel
{
    // Very detailed information.
    // Usually only enabled when diagnosing tricky bugs.
    Trace = 0,


    // Debug information for developers.
    // Often used inside loops and systems.
    Debug = 1,


    // Normal, important messages.
    // Example: "Renderer started"
    Info = 2,


    // Something unexpected happened, but the app can continue.
    // Example: recalculating layout.
    Warn = 3,


    // A serious problem.
    // Something failed and likely needs attention.
    Error = 4,
}