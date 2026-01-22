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
/// AsyncPipeLogger sends log messages to another program using a named pipe
/// </summary>
/// <remarks>
/// Key idea:<br/>
/// - Logging is FAST (enqueue only)<br/>
/// - Writing is SLOW (done on a background worker)
/// </remarks>
public sealed class AsyncPipeLogger : IAsyncDisposable
{
    // A thread-safe queue (many threads can use it safely)
    private readonly ConcurrentQueue<string> _queue = new();

    // A doorbell that wakes up the worker when new data arrives
    private readonly SemaphoreSlim _signal = new(0);

    // Writes text into the named pipe
    private readonly StreamWriter _writer;

    // Background worker task
    private readonly Task _worker;

    // Private constructor: forces usage of ConnectAsync()
    private AsyncPipeLogger(StreamWriter writer)
    {
        _writer = writer;

        // Start the worker immediately
        _worker = Task.Run(WorkerAsync);
    }

    /// <summary>
    /// Connects to the named pipe and creates the logger.
    /// </summary>
    public static async Task<AsyncPipeLogger> ConnectAsync(string pipeName)
    {
        // Connect to the logger program (retrying if needed)
        var pipe = await PipeConnect.ConnectWithRetryAsync(pipeName);

        // Wrap pipe in a text writer
        var writer = new StreamWriter(pipe, Encoding.UTF8) 
        {
            // AutoFlush = true means every WriteLine is sent immediately
            AutoFlush = true 
        };

        return new AsyncPipeLogger(writer);
    }

    /// <summary>
    /// Creates a category-specific logger (Render, Layout, etc.)
    /// </summary>
    public CategoryLogger For(string category) 
        => new(this, category);

    /// <summary>
    /// Adds a log line to the queue (FAST, non-blocking)
    /// </summary>
    internal void Enqueue(string line)
    {
        // Put message in the waiting line
        _queue.Enqueue(line);

        // Ring the doorbell so the worker wakes up
        _signal.Release();
    }


    /// <summary>
    /// Background worker loop
    /// This runs on a separate thread forever.
    /// </summary>
    private async Task WorkerAsync()
    {
        while (true)
        {
            // Sleep until there is at least one log message
            await _signal.WaitAsync();

            // Drain the queue
            while (_queue.TryDequeue(out var line))
                // Writing to the pipe is the slow part
                await _writer.WriteLineAsync(line);
            
        }
    }

    /// <summary>
    /// Cleanup when the app shuts down
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        // Flush any remaining messages
        while (_queue.TryDequeue(out var line))
            await _writer.WriteLineAsync(line);

        // Close the pipe
        _writer.Dispose();
    }

}
