using System.Diagnostics;

namespace MassacreStackFinderCs;

// Helper for logging progress on an async task
public struct LogTask : IAsyncDisposable
{
    public Stopwatch Stopwatch { get; } = new();
    public string Message { get; }
    public long Total { get; }

    private static long Current = 0;
    public CancellationTokenSource CancelHost { get; } = new();

    public LogTask(string message, int total)
    {
        Stopwatch.Start();
        Message = message;
        Total = total;
    }

    public static async Task<LogTask> New(string message, int total)
    {
        var logTask = new LogTask(message, total);
        await logTask.Start();
        return logTask;
    }

    public async Task Start()
    {
        Interlocked.Exchange(ref Current, 0);
        await LogUpdate();
        KickoffUpdate();
    }

    public void Increment()
    {
        Interlocked.Increment(ref Current);
    }

    public void Update(long current)
    {
        Interlocked.Exchange(ref Current, current);
    }

    public void KickoffUpdate()
    {
        CancellationToken token = CancelHost.Token;
        LogTask thisLocal = this;
        _ = Task.Run(() => thisLocal.Update(token), token);
    }

    public async Task Update(CancellationToken cancellationToken = default)
    {
        await Task.Delay(TimeSpan.FromMilliseconds(200), cancellationToken);
        if (!cancellationToken.IsCancellationRequested)
        {
            _ = Task.Run(LogUpdate, cancellationToken);
        }
        if (!cancellationToken.IsCancellationRequested)
        {
            CancellationToken token = CancelHost.Token;
            LogTask thisLocal = this;
            _ = Task.Run(() => thisLocal.Update(token), token);
        }
    }

    Task LogUpdate()
    {
        if (Total == 0)
        {
            return StaticLog.ReplaceLog($"> {Message} {Interlocked.Read(ref Current)} ... ");
        }
        else
        {
            return StaticLog.ReplaceLog($"> {Message} {Interlocked.Read(ref Current)} / {Total} ... ");
        }
    }

    public async ValueTask DisposeAsync()
    {
        await CancelHost.CancelAsync();
        CancelHost.Dispose();
        await LogUpdate();
        await StaticLog.LogLine($"Done ({Stopwatch.Elapsed.TotalSeconds:F3} seconds)");
    }
}