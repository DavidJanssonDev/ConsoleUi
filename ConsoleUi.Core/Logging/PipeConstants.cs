using System;
using System.Collections.Generic;
using System.Text;

namespace ConsoleUi.Core.Logging;

/// <summary>
/// Holds shared constants used by the logging system.
///
/// This class exists so BOTH applications:
/// - ConsoleUi.Demo (sender)
/// - ConsoleUi.Logger (receiver)
/// use the SAME pipe name.
/// </summary>
public static class PipeConstants
{
    // The name of the Named Pipe.
    //
    // Think of this like the name of a secret tunnel.
    // Both programs MUST use the exact same name,
    // otherwise they will never find each other.
    public const string PipeName = "ConsoleUiLoggerPipe";
}
