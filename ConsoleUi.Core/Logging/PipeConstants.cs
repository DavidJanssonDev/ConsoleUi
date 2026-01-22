using System;
using System.Collections.Generic;
using System.Text;

namespace ConsoleUi.Core.Logging;

/// <summary>
/// The name of the "tube" both apps agree to use.
/// If you change this string, BOTH the apps must the same new value.
/// </summary>
public static class PipeConstants
{
    public const string PipeName = "ConsoleUiLoggerPipe";
}
