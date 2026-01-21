using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Pipes;
using System.Text;

namespace ConsoleUi.Demo.Logging;

public static class PipeConnect
{
    public static async Task<NamedPipeClientStream> ConnectWithRetryAsync(
        string pipeName,
        int totalWaitMs = 8000,
        int attemptTimeoutMs = 300)
    {
        Stopwatch sw = Stopwatch.StartNew();

        while (sw.ElapsedMilliseconds < totalWaitMs)
        {
            NamedPipeClientStream client = new(".", pipeName, PipeDirection.Out, PipeOptions.Asynchronous);
            try
            {
                await client.ConnectAsync(attemptTimeoutMs);
                return client;
            }
            catch (TimeoutException)
            {
                client.Dispose();
                await Task.Delay(100);
            }
        }

        throw new TimeoutException($"Could not connect to pipe '{pipeName}' within {totalWaitMs}ms.");
    }
}
