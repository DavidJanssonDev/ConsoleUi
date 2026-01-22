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
    private readonly SemaphoreSlim _signal = new(0);
    private readonly StreamWriter _writer;
    private readonly Task _worker;

    private AsyncPipeLogger(StreamWriter writer)
    {
        _writer = writer;
        _worker = Task.Run(WorkerAsync);
    }

    public static async Task<AsyncPipeLogger> ConnectAsync(string pipeName)
    {
        var pipe = await PipeConnect.ConnectWithRetryAsync(pipeName);
        var writer = new StreamWriter(pipe, Encoding.UTF8) { AutoFlush = true };
        return new AsyncPipeLogger(writer);
    }

    public CategoryLogger For(string category) => new(this, category);

    internal void Enqueue(string line)
    {
        _queue.Enqueue(line);
        _signal.Release();
    }


    private async Task WorkerAsync()
    {
        while (true)
        {
            await _signal.WaitAsync();
            while (_queue.TryDequeue(out var line))
            {
                await _writer.WriteLineAsync(line);
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        while (_queue.TryDequeue(out var line))
            await _writer.WriteLineAsync(line);

        _writer.Dispose();
    }

}
