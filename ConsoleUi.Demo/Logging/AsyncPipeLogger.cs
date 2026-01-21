using System;
using System.Collections.Concurrent;
using System.IO;
using System.IO.Pipes;
using System.Numerics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ConsoleUi.Demo.Logging;

/// <summary>
/// Non-blocking logger:
/// - Log() is fast (enqueue only)
/// - Background task writes to the named pipe
/// - Optional overload protection (drops oldest messages)
/// </summary>
public sealed class AsyncPipeLogger : IAsyncDisposable
{
    private readonly ConcurrentQueue<string> _queue = new();
    private readonly SemaphoreSlim _signal = new(0, int.MaxValue);
    private readonly CancellationTokenSource _cts = new();

    private readonly StreamWriter _writer;
    private readonly Task _worker;

    private readonly int _maxQueueSize;
    private int _approxCount;

    private readonly NamedPipeClientStream _client;

    private AsyncPipeLogger(StreamWriter writer, int maxQueueSize)
    {
        _writer = writer;
        _maxQueueSize = maxQueueSize;
        _worker = Task.Run(WorkerLoopAsync);
    }

    /// <summary>
    /// Connects to the pipe and starts the background writer.
    /// </summary>
    public static async Task<AsyncPipeLogger> ConnectAsync(string pipeName, int maxQueue = 10_000)
    {
        NamedPipeClientStream client = await PipeConnect.ConnectWithRetryAsync(pipeName);

        StreamWriter writer = new(client, Encoding.UTF8, bufferSize: 1024, leaveOpen: false)
        {
            AutoFlush = false
        };

        return new AsyncPipeLogger(writer, maxQueue);
    }


    // Convenience methods
    public void Trace(string category, string message) => Log(LogLevel.Trace, category, message);
    public void Debug(string category, string message) => Log(LogLevel.Debug, category, message);
    public void Info(string category, string message) => Log(LogLevel.Info, category, message);
    public void Warn(string category, string message) => Log(LogLevel.Warn, category, message);
    public void Error(string category, string message) => Log(LogLevel.Error, category, message);


    /// <summary>
    /// Non-blocking log call.
    /// If queue is too big, it drops old messages to protect performance.
    /// </summary>
    public void Log(LogLevel level, string category, string message)
    {
        category = Escape(category);
        message = Escape(message);

        string line = $"{level}|{category}|{message}";
        _queue.Enqueue(line);

        var count = Interlocked.Increment(ref _approxCount);

        // overload protection: drop oldest messages if queue grows too large
        if (count > _maxQueueSize)
        {
            for (int index = 0; index < 200; index++)
            {
                if (_queue.TryDequeue(out _))
                {
                    Interlocked.Decrement(ref _approxCount);
                }
                else
                {
                    break;
                }
            }
            // Optional: enqueue a "dropped logs" warning
            _queue.Enqueue($"[{DateTime.Now:HH:mm:ss.fff}] [WARN] Log queue overflow, dropping messages...");
            Interlocked.Increment(ref _approxCount);
        }

        _signal.Release();
    }

    private static string Escape(string s)
        => s.Replace("|", "¦").Replace("\r", "\\r").Replace("\n", "\\n");

    private async Task WorkerLoopAsync()
    {
        var token = _cts.Token;

        try
        {
            while (!token.IsCancellationRequested)
            {
                // Wait until we have at least one message
                await _signal.WaitAsync(token);

                // Dain quickly in bathces 
                int drained = 0;
                while (drained < 500 && _queue.TryDequeue(out string? line))
                {
                    Interlocked.Decrement(ref _approxCount);
                    await _writer.WriteLineAsync(line);
                    drained++;
                }

                // Föisj after a batch
                await _writer.FlushAsync();
            }

        }
        catch (OperationCanceledException)
        {
            // Expected on shutdown
        }
        catch (IOException)
        {
            // Pipe disconnected / logger closed.
            // We stop silently so render loop doesn't die.
        } 
        catch (ObjectDisposedException)
        {
            // Writer disposed during shutdown.
        }
    }
    public async ValueTask DisposeAsync()
    {
        _cts.Cancel();

        // Wake worker so it can exit
        try
        {
            _signal.Release();
        }
        catch
        {
            // ignore
        }

        try
        {
            await _worker;
        }
        catch
        {
            // ignore shutdown exceptions
        }

        // Try to flush remaining items (best effort)
        try
        {
            while (_queue.TryDequeue(out var line))
            {
                await _writer.WriteLineAsync(line);
            }

            await _writer.FlushAsync();
        }
        catch
        {
            // ignore
        }

        _writer.Dispose();
        _cts.Dispose();
        _signal.Dispose();
    }

}
