using System;
using System.Diagnostics;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;

namespace ConsoleUI.Demo.Logging;

/// <summary>
/// Helper for connecting to a named pipe with retries.
/// Supports cancellation so shutdown is instant.
/// </summary>
public static class PipeConnect
{
    public static async Task<NamedPipeClientStream> ConnectWithRetryAsync(
        string pipeName,
        int totalWaitMs = 8000,
        int attemptTimeoutMs = 300,
        CancellationToken token = default)
    {
        Stopwatch sw = Stopwatch.StartNew();

        while (sw.ElapsedMilliseconds < totalWaitMs)
        {
            token.ThrowIfCancellationRequested();

            var client = new NamedPipeClientStream(
                ".",
                pipeName,
                PipeDirection.Out,
                PipeOptions.Asynchronous
            );

            try
            {
                await client.ConnectAsync(attemptTimeoutMs, token)
                            .ConfigureAwait(false);
                return client;
            }
            catch (OperationCanceledException)
            {
                client.Dispose();
                throw;
            }
            catch
            {
                client.Dispose();
                await Task.Delay(100, token).ConfigureAwait(false);
            }
        }

        throw new TimeoutException(
            $"Could not connect to pipe '{pipeName}' within {totalWaitMs}ms."
        );
    }
}
