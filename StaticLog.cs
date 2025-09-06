namespace MassacreStackFinderCs;

// Thread-safe console log utility
public static class StaticLog
{
    private static SemaphoreSlim consoleSemaphore { get; } = new(1, 1);

    public static async Task Log(string message)
    {
        await consoleSemaphore.WaitAsync();
        Console.Write(message);
        consoleSemaphore.Release();
    }

    public static async Task ReplaceLog(string message)
    {
        await consoleSemaphore.WaitAsync();
        Console.Write("\r");
        Console.Write(message);
        consoleSemaphore.Release();
    }

    public static Task LogLine(string message)
    {
        return Log($"{message}\n");
    }
}
