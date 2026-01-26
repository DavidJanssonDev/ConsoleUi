using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Pipes;
using System.Text;

namespace ConsoleUI.Demo.Logging;

/// <summary>
/// Helper for connecting to a named pipe with retries.<br/>
///
/// This prevents startup race conditions where the 
/// logger process is not ready yet.
/// </summary>
public static class PipeConnect
{
    /// <summary>
    /// Tries to connect to a named pipe until it succeeds or times out.
    /// </summary>
    /// <param name="pipeName">The name of the pipe to connect to</param>
    /// <param name="totalWaitMs">Maximum total time to keep retrying</param>
    /// <param name="attemptTimeoutMs">Timeout for a single attempt</param>
    public static async Task<NamedPipeClientStream> ConnectWithRetryAsync(
        string pipeName,
        int totalWaitMs = 8000,
        int attemptTimeoutMs = 300)
    {
        // Start a timer to track total wait time
        Stopwatch sw = Stopwatch.StartNew();

        // Keep trying until we run out of total time
        while (sw.ElapsedMilliseconds < totalWaitMs)
        {
            // Create a pipe client that can WRITE to the server
            NamedPipeClientStream client = new(
                ".", // local machine
                pipeName, // pipe name
                PipeDirection.Out, // we only send data
                PipeOptions.Asynchronous
            );

            try
            {
                // Try to connect, but only wait a short time
                await client.ConnectAsync(attemptTimeoutMs);

                // Success! Return the connected pipe
                return client;
            }
            catch (TimeoutException)
            {
                // This attempt failed — clean up
                client.Dispose();

                // Wait a little before trying again
                await Task.Delay(100);
            }
        }

        // If we get here, all retries failed
        throw new TimeoutException(
            $"Could not connect to pipe '{pipeName}' within {totalWaitMs}ms."
        );
    }
}
