// See https://aka.ms/new-console-template for more information

using System.Diagnostics;
using System.Numerics;
using System.Text;
using System.Text.Json;
using CommandLine;

namespace MassacreStackFinderCs;

internal class Program
{
    public static Task<int> Main(string[] args)
    {
        var parserResult = CommandLine.Parser.Default.ParseArguments<CmdOptions>(args);
        return parserResult.MapResult(options => Run(options), errs => Task.FromResult(1));
    }

    private static SemaphoreSlim consoleSemaphore { get; } = new(1, 1);

    private static async Task Log(string message)
    {
        await consoleSemaphore.WaitAsync();
        Console.Write(message);
        consoleSemaphore.Release();
    }

    private static async Task ReplaceLog(string message)
    {
        await consoleSemaphore.WaitAsync();
        // Console.SetCursorPosition(0, Console.CursorTop);
        // Console.Write(new string(' ', Console.WindowWidth));
        // Console.SetCursorPosition(0, Console.CursorTop);
        Console.Write("\r");
        Console.Write(message);
        consoleSemaphore.Release();
    }

    private static Task LogLine(string message)
    {
        return Log($"{message}\n");
    }

    private struct LogTask : IAsyncDisposable
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
                return ReplaceLog($"> {Message} {Interlocked.Read(ref Current)} ... ");
            }
            else
            {
                return ReplaceLog($"> {Message} {Interlocked.Read(ref Current)} / {Total} ... ");
            }
        }

        public async ValueTask DisposeAsync()
        {
            await CancelHost.CancelAsync();
            CancelHost.Dispose();
            await LogUpdate();
            await LogLine($"Done ({Stopwatch.Elapsed.TotalSeconds:F3} seconds)");
        }
    }

    private static async Task<int> Run(CmdOptions options)
    {
        if (!options.InputFile.Exists)
        {
            await LogLine("Input file does not exist.");
            return 1;
        }

        FileInfo fileToRead;
        bool useCacheFile;

        if (options.CacheFile.Exists && options.CacheFile.LastWriteTime > options.InputFile.LastWriteTime)
        {
            fileToRead = options.CacheFile;
            useCacheFile = true;
        }
        else
        {
            fileToRead = options.InputFile;
            useCacheFile = false;
        }

        VoxelAccelerationStructure accelerationStructure = new();
        List<System> systemsRaw = [];

        {
            string message = $"Reading from {(useCacheFile ? "cache" : "input")} file '{fileToRead.FullName}'";
            await using LogTask logTask =
                await LogTask.New(message, 0);
            await using FileStream stream = fileToRead.OpenRead();
            IAsyncEnumerable<System?> systemStream = JsonSerializer.DeserializeAsyncEnumerable<System>(stream, JsonSerializerOptions);
            await foreach (System? system in systemStream)
            {
                if (system != null)
                {
                    for (int index = system.Stations.Count - 1; index >= 0; index--)
                    {
                        Station station = system.Stations[index];
                        if (!station.ValidMissionStation)
                        {
                            system.Stations.RemoveAt(index);
                        }
                    }
                    for (int index = system.Factions.Count - 1; index >= 0; index--)
                    {
                        Faction faction = system.Factions[index];
                        if (faction.Influence < 0.00001f)
                        {
                            system.Factions.RemoveAt(index);
                        }
                    }

                    system.Register(accelerationStructure);
                    systemsRaw.Add(system);
                    
                    logTask.Increment();
                }
            }

            systemsRaw.Sort(new System.IdComparer());
        }

        await LogLine($"Discovered {systemsRaw.Count} systems.");

        if (!useCacheFile)
        {
            string message = $"Writing cache file '{options.CacheFile.FullName}'";
            await using LogTask logTask = await LogTask.New(message, 0);
            
            {
                await using FileStream stream = options.CacheFile.Create();
                await JsonSerializer.SerializeAsync(stream, systemsRaw, JsonSerializerOptionsCache);
                logTask.Increment();
            }
        }


        List<MassacreStackSystem> massacreStackSystems = [];
        {
            await using LogTask logTask = await LogTask.New($"Evaluating Hunter Systems", systemsRaw.Count);
            foreach (System system in systemsRaw)
            {
                bool hasAnarchy = system.AnarchyFaction != null;
                bool hasRingedBody = system.RingedBodies.Any();
                var nearbySystems = system.EnumerateNearbySystems(10, sys => sys.MissionGiverScore > 0.0f);
                bool anyNearbySystems = nearbySystems.Any();
                if (hasAnarchy && hasRingedBody && anyNearbySystems)
                {
                    MassacreStackSystem stackSystem = new MassacreStackSystem(system);
                    if (stackSystem.Score > 0.0f)
                    {
                        massacreStackSystems.Add(stackSystem);
                    }
                }
                logTask.Increment();
            }
            massacreStackSystems.Sort(new MassacreStackSystem.ScoreComparer());
            massacreStackSystems.Reverse();
        }
        
        await LogLine($"Identified {massacreStackSystems.Count} stack systems");

        foreach (var stackSystem in massacreStackSystems.Take(options.NumResults))
        {
            await LogLine($"{stackSystem.System.Name}: {stackSystem.Score}");
        }

        {
            await using LogTask logTask = await LogTask.New($"Writing out results", 0);
            if (options.OutputDir.Exists)
            {
                options.OutputDir.Delete(true);
            }
            options.OutputDir.Create();
            List<Task> tasks =
            [

                ..massacreStackSystems.Take(options.NumResults).Select(sys => Task.Run(async () =>
                {
                    await using FileStream stream =
                        new FileInfo(Path.Combine(options.OutputDir.FullName, $"{sys.Name}.json")).Create();
                    await JsonSerializer.SerializeAsync(stream, sys, JsonSerializerOptions);
                    logTask.Increment();
                })),

                Task.Run(async () =>
                {
                    await using FileStream stream =
                        new FileInfo(Path.Combine(options.OutputDir.FullName, "_overview.txt")).Create();
                    await using StreamWriter writer = new StreamWriter(stream, Encoding.UTF8);
                    foreach (var stackSystem in massacreStackSystems.Take(options.NumResults))
                    {
                        await writer.WriteLineAsync($"{stackSystem.System.Name}: {stackSystem.Score}");
                    }
                    logTask.Increment();
                })
            ];

            await Task.WhenAll(tasks);
        }

        if (!string.IsNullOrEmpty(options.SystemOfInterest))
        {
            MassacreStackSystem? sys = massacreStackSystems.Find(sys =>
                string.Equals(sys.Name, options.SystemOfInterest, (StringComparison)StringComparison.OrdinalIgnoreCase));
            if (sys != null)
            {
                await using FileStream stream = new FileInfo(Path.Combine(options.OutputDir.FullName, $"{sys.Name}.json")).Create();
                await JsonSerializer.SerializeAsync(stream, sys, JsonSerializerOptions);
                await LogLine($"Emitted file for system of interest '{sys.Name}'");
            }
            else
            {
                await LogLine($"System of interest '{options.SystemOfInterest}' not found in potential stack targets");
            }
        }

        return 0;
    }

    public static JsonSerializerOptions JsonSerializerOptions { get; } = new()
    {
        WriteIndented = true,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip
    };

    public static JsonSerializerOptions JsonSerializerOptionsCache { get; } = new()
    {
        WriteIndented = false,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip
    };
}