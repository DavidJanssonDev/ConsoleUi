using System;
using System.Collections.Concurrent;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ConsoleUI.Demo.Logging;

/// <summary>
/// Sends log messages to another program using a named pipe.
///
/// DESIGN GOAL (Mode B):
/// <list type="bullet">
///   <item>
///     <description>Logging should work even if the Logger UI is not running</description>
///   </item>
///   <item>
///     <description>Always enqueue logs immediately</description>
///   </item>
///   <item>
///     <description>A background worker connects/reconnects and flushes the queue when possible</description>
///   </item>
/// </list>
/// </summary>
public sealed class AsyncPipeLogger : IAsyncDisposable
{
    // =========================
    // Queue + signaling
    // =========================
    #region ---
    // Thread-safe queue: many threads can enqueue log lines safely.
    private readonly ConcurrentQueue<string> _queue = new();

    // Doorbell: Release() when new items arrive, worker WaitAsync() sleeps until then.
    private readonly SemaphoreSlim _signal = new(0);

    // Approx queue size (so we can apply a max limit without locking).
    private int _approxQueueCount;

    // Max queue size (overflow protection).
    private readonly int _maxQueue;
    #endregion
    // =========================
    // Pipe connection state
    // =========================
    #region ---
    // Pipe name we connect to (server is the Logger UI process).
    private readonly string _pipeName;

    // Current pipe + writer (null when not connected).
    private NamedPipeClientStream? _pipe;
    private StreamWriter? _writer;

    // Gate to ensure only one connect/disconnect happens at a time.
    private readonly object _connectGate = new();
    #endregion
    // =========================
    // Worker lifetime
    // =========================
    #region ---
    // Used to stop the worker on shutdown.
    private readonly CancellationTokenSource _cts = new();

    // Background worker task.
    private readonly Task _worker;
    #endregion
    // Private constructor: forces usage of ConnectAsync()
    private AsyncPipeLogger(string pipeName, int maxQueue)
    {
        _pipeName = pipeName;
        _maxQueue = Math.Max(1, maxQueue);

        // Start the worker immediately.
        _worker = Task.Run(WorkerAsync);
    }

    /// <summary>
    /// Creates the logger. IMPORTANT: does NOT block waiting for server.
    /// The background worker will connect when the server is available.
    /// </summary>
    public static Task<AsyncPipeLogger> ConnectAsync(string pipeName, int maxQueue = 10_000)
    {
        // Mode B: return immediately.
        AsyncPipeLogger logger = new(pipeName, maxQueue);

        // Optional: kick worker once so it attempts to connect right away.
        logger._signal.Release();

        return Task.FromResult(logger);
    }

    /// <summary>
    /// Creates a category-specific logger (Render, Layout, etc.)
    /// </summary>
    public CategoryLogger For(string category)
        => new(this, category);

    /// <summary>
    /// Adds a log line to the queue (FAST, non-blocking).
    /// </summary>
    internal void Enqueue(string line)
    {
        _queue.Enqueue(line);
        int count = Interlocked.Increment(ref _approxQueueCount);

        // Overflow protection: if queue grows too big, drop oldest items.
        // (Better to drop logs than to freeze your app.)
        if (count > _maxQueue)
        {
            // Drop a chunk quickly.
            for (int i = 0; i < 200; i++)
            {
                if (_queue.TryDequeue(out _))
                    Interlocked.Decrement(ref _approxQueueCount);
                else
                    break;
            }

            // Add one warning line (best-effort).
            _queue.Enqueue($"{LogLevel.Warn}|Logger|Log queue overflow, dropping messages...");
            Interlocked.Increment(ref _approxQueueCount);
        }

        // Wake the worker.
        _signal.Release();
    }

    /// <summary>
    /// Background worker loop:
    /// <list type="bullet">
    ///   <item>
    ///     <description>Waits for work</description>
    ///   </item>
    ///   <item>
    ///     <description>Ensures we are connected</description>
    ///   </item>
    ///   <item>
    ///     <description>Writes a batch</description>
    ///   </item>
    ///   <item>
    ///     <description>Flushes</description>
    ///   </item>
    ///   <item>
    ///     <description>If the pipe breaks, disconnects and retries later</description>
    ///   </item>
    /// </list>
    /// </summary>

    private async Task WorkerAsync()
    {
        CancellationToken token = _cts.Token;

        try
        {
            while (!token.IsCancellationRequested)
            {
                // Sleep until we have at least one signal.
                await _signal.WaitAsync(token).ConfigureAwait(false);

                // Try to ensure connection. If server isn't up, we just wait and retry.
                await EnsureConnectedAsync(token).ConfigureAwait(false);

                // If still no writer, it means we couldn't connect (server not running).
                // We just loop and try again later.
                if (_writer is null)
                {
                    await Task.Delay(150, token).ConfigureAwait(false);
                    continue;
                }

                // Drain a batch for efficiency.
                int batch = 0;

                while (batch < 500 && _queue.TryDequeue(out var line))
                {
                    Interlocked.Decrement(ref _approxQueueCount);

                    try
                    {
                        await _writer.WriteLineAsync(line).ConfigureAwait(false);
                        batch++;
                    }
                    catch (IOException)
                    {
                        // Pipe broke mid-write (server closed/restarted).
                        // Put message back and disconnect so we reconnect next time.
                        Requeue(line);
                        Disconnect();
                        break;
                    }
                    catch (ObjectDisposedException)
                    {
                        // Same idea as IOException: treat as disconnected.
                        Requeue(line);
                        Disconnect();
                        break;
                    }
                }

                // Flush once per batch (faster than AutoFlush=true per line).
                try
                {
                    if (_writer is not null)
                        await _writer.FlushAsync().ConfigureAwait(false);
                }
                catch (IOException)
                {
                    Disconnect();
                }
                catch (ObjectDisposedException)
                {
                    Disconnect();
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown.
        }
    }

    /// <summary>
    /// Ensure we have a connected pipe + writer.
    /// If not connected, attempt to connect (non-throwing).
    /// </summary>
    private async Task EnsureConnectedAsync(CancellationToken token)
    {
        // Fast path
        if (_pipe is { IsConnected: true } && _writer is not null)
            return;

        // Ensure only one connect attempt runs at a time.
        lock (_connectGate)
        {
            // Double-check under lock
            if (_pipe is { IsConnected: true } && _writer is not null)
                return;

            // Clear broken state first
            Disconnect_NoLock();
        }

        // Connect outside lock (so Enqueue() is never blocked).
        try
        {
            // This helper retries for a short while; if it can't connect, it throws TimeoutException.
            // That's OK: we catch and just try again later (Mode B).
            NamedPipeClientStream pipe = await PipeConnect
                .ConnectWithRetryAsync(_pipeName, token: token)
                .ConfigureAwait(false);

            StreamWriter writer = new(pipe, Encoding.UTF8)
            {
                // IMPORTANT: do not flush every line (too expensive + triggers failures sooner)
                AutoFlush = false
            };

            lock (_connectGate)
            {
                _pipe = pipe;
                _writer = writer;
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            // Server not available right now.
            // Mode B: do nothing and let worker retry later.
        }
    }

    /// <summary>
    /// Disconnects safely.
    /// </summary>
    private void Disconnect()
    {
        lock (_connectGate)
        {
            Disconnect_NoLock();
        }
    }

    private void Disconnect_NoLock()
    {
        try { _writer?.Dispose(); } catch { }
        try { _pipe?.Dispose(); } catch { }

        _writer = null;
        _pipe = null;
    }

    /// <summary>
    /// Put a line back if we failed to write it.
    /// (Order may shift slightly; this is acceptable for logging.)
    /// </summary>
    private void Requeue(string line)
    {
        _queue.Enqueue(line);
        Interlocked.Increment(ref _approxQueueCount);

        // Wake worker so it retries after reconnect.
        try { _signal.Release(); } catch { }
    }

    /// <summary>
    /// Cleanup when the app shuts down.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        // Stop worker
        _cts.Cancel();

        // Wake worker so it exits quickly
        try { _signal.Release(); } catch { }

        // Wait for worker
        try { await _worker.ConfigureAwait(false); } catch { }

        // Best-effort: try to flush remaining lines quickly if connected.
        // IMPORTANT: we must NOT block forever during shutdown.
        try
        {
            // Attempt a connection once (optional).
            await EnsureConnectedAsync(CancellationToken.None).ConfigureAwait(false);

            if (_writer is not null)
            {
                while (_queue.TryDequeue(out var line))
                {
                    Interlocked.Decrement(ref _approxQueueCount);
                    await _writer.WriteLineAsync(line).ConfigureAwait(false);
                }

                await _writer.FlushAsync().ConfigureAwait(false);
            }
        }
        catch
        {
            // Ignore shutdown errors.
        }

        Disconnect();
        _cts.Dispose();
        _signal.Dispose();
    }


    /// <summary>
    /// Converts a field into a pipe-safe, single-line string.
    /// </summary>
    /// <remarks>
    /// Rules:
    /// <list type="bullet">
    ///   <item>
    ///     <description><c>|</c> would break our split, so replace it with a similar character</description>
    ///   </item>
    ///   <item>
    ///     <description>Newlines would break <c>ReadLine</c>, so escape them</description>
    ///   </item>
    /// </list>
    /// </remarks>
    private static string EscapeField(string @string)
    {
        if (string.IsNullOrEmpty(@string)) return string.Empty;

        return @string
            .Replace("|", "¦")      // visually similar to |, but not our delimiter
            .Replace("\r", "\\r")   // keep logs single-line
            .Replace("\n", "\\n");
    }

    internal void Enqueue(string level, string category, string project, string file, int line,string message)
    {
        string payload =
            $"{level}|" +
            $"{EscapeField(category)}|" +
            $"{EscapeField(project)}|" +
            $"{EscapeField(file)}|" +
            $"{line}|" +
            $"{EscapeField(message)}";

        Enqueue(payload);
    }

}
