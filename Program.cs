// See https://aka.ms/new-console-template for more information

using System.Diagnostics;
using System.Numerics;
using System.Text;
using System.Text.Json;
using CommandLine;
using MassacreStackFinderCs.Types;

namespace MassacreStackFinderCs;

internal class Program
{
    private static VoxelAccelerationStructure _accelerationStructure = new();
    private static List<StarSystem> _systemsRaw = [];
    private static List<MassacreTargetSystem> _massacreTargetSystems = [];

    public static Task<int> Main(string[] args)
    {
        var parserResult = CommandLine.Parser.Default.ParseArguments<CmdOptions>(args);
        return parserResult.MapResult(Run, _ => Task.FromResult(1));
    }

    private static async Task<int> Run(CmdOptions options)
    {
        if (!options.InputFile.Exists)
        {
            await StaticLog.LogLine("Input file does not exist.");
            return 1;
        }

        FileInfo fileToRead;
        bool ingestCacheFile;

        // Use cache if it exists and is newer than the input file (implying it was generated based on that input file)
        if (options.CacheFile.Exists && options.CacheFile.LastWriteTime > options.InputFile.LastWriteTime)
        {
            fileToRead = options.CacheFile;
            ingestCacheFile = true;
        }
        else
        {
            fileToRead = options.InputFile;
            ingestCacheFile = false;
        }

        await IngestSystemsFile(ingestCacheFile, fileToRead);

        if (!ingestCacheFile)
        {
            await EmitSystemsCacheFile(options);
        }

        await ComputeTargetSystems();

        await EmitResults(options);

        return 0;
    }

    // Ingest a json file containing all systems in EDSM systemPopulated format
    private static async Task IngestSystemsFile(bool isCacheFile, FileInfo fileToRead)
    {
        {
            string message = $"Reading from {(isCacheFile ? "cache" : "input")} file '{fileToRead.FullName}'";
            await using LogTask logTask = await LogTask.New(message, 0);

            // Open the file
            await using FileStream stream = fileToRead.OpenRead();

            // Async enumerate all {...} system json objects
            IAsyncEnumerable<StarSystem?> systemStream =
                JsonSerializer.DeserializeAsyncEnumerable<StarSystem>(stream, JsonSerializerOptions);
            await foreach (StarSystem? system in systemStream)
            {
                if (system != null)
                {
                    // Prune all stations which are not of interest to us
                    for (int index = system.Stations.Count - 1; index >= 0; index--)
                    {
                        Station station = system.Stations[index];
                        if (!station.ValidMissionStation)
                        {
                            system.Stations.RemoveAt(index);
                        }
                    }

                    // Prune dead faction entries
                    for (int index = system.Factions.Count - 1; index >= 0; index--)
                    {
                        Faction faction = system.Factions[index];
                        if (faction.IsDeadFaction)
                        {
                            system.Factions.RemoveAt(index);
                        }
                    }

                    // Register system with the acceleration structure
                    system.Register(_accelerationStructure);
                    // Add to the raw array
                    _systemsRaw.Add(system);

                    logTask.Increment();
                }
            }

            // Sort systemsRaw by Id to keep output consistent
            _systemsRaw.Sort(new StarSystem.IdComparer());
        }

        await StaticLog.LogLine($"Discovered {_systemsRaw.Count} systems.");
    }

    // Write a json file with an EDSM systemsPopulated-like format to use as a cache for quicker ingestion
    private static async Task EmitSystemsCacheFile(CmdOptions options)
    {
        string message = $"Writing cache file '{options.CacheFile.FullName}'";
        await using LogTask logTask = await LogTask.New(message, 0);

        {
            await using FileStream stream = options.CacheFile.Create();
            await JsonSerializer.SerializeAsync(stream, _systemsRaw, JsonSerializerOptionsCache);
            logTask.Increment();
        }
    }

    // Enumerates all systems and computes their viability as hunter systems
    // Hunter System: A system which is a viable target for pirate massacre missions from surrounding systems
    private static async Task ComputeTargetSystems()
    {
        await using LogTask logTask = await LogTask.New($"Evaluating Hunter Systems", _systemsRaw.Count);

        foreach (StarSystem system in _systemsRaw)
        {
            // Do some prechecks

            // Must have an anarchy faction to hunt
            bool hasAnarchyFaction = system.AnarchyFaction != null;
            // Must have ringed bodies for RES availability
            bool hasRingedBody = system.RingedBodies.Any();
            // Must have nearby systems capable of generating missions
            bool anyNearbySystems = system.EnumerateNearbySystems(10, sys => sys.MissionGiverScore > 0.0f).Any();

            if (hasAnarchyFaction && hasRingedBody && anyNearbySystems)
            {
                MassacreTargetSystem targetSystem = new MassacreTargetSystem(system);
                StaticScoringHeuristic.CalculateTargetSystemScore(targetSystem);
                if (targetSystem.Score > 0.0f)
                {
                    _massacreTargetSystems.Add(targetSystem);
                }
            }

            logTask.Increment();
        }

        // Sort descending by score
        _massacreTargetSystems.Sort(new MassacreTargetSystem.ScoreComparer());
    }

    // Emit results to console and output files
    private static async Task EmitResults(CmdOptions options)
    {
        await StaticLog.LogLine($"Identified {_massacreTargetSystems.Count} target systems");

        // Log best systems
        foreach (var stackSystem in _massacreTargetSystems.Take(options.NumResults))
        {
            await StaticLog.LogLine($"{stackSystem.System.Name}: {stackSystem.Score}");
        }

        // Write result files
        {
            await using LogTask logTask = await LogTask.New($"Writing out results", 0);
            if (options.OutputDir.Exists)
            {
                options.OutputDir.Delete(true);
            }

            options.OutputDir.Create();
            List<Task> tasks =
            [
                .._massacreTargetSystems.Take(options.NumResults).Select(sys => Task.Run(async () =>
                {
                    await using FileStream stream =
                        new FileInfo(Path.Combine(options.OutputDir.FullName, $"{sys.Name}.json")).Create();
                    await JsonSerializer.SerializeAsync(stream, sys, JsonSerializerOptions);
                    // Disable because we guarantee this task finishes before outer scope is exited
                    // ReSharper disable once AccessToDisposedClosure
                    logTask.Increment();
                })),

                Task.Run(async () =>
                {
                    await using FileStream stream =
                        new FileInfo(Path.Combine(options.OutputDir.FullName, "_overview.txt")).Create();
                    await using StreamWriter writer = new StreamWriter(stream, Encoding.UTF8);
                    foreach (var stackSystem in _massacreTargetSystems.Take(options.NumResults))
                    {
                        await writer.WriteLineAsync($"{stackSystem.System.Name}: {stackSystem.Score}");
                    }

                    // Disable because we guarantee this task finishes before outer scope is exited
                    // ReSharper disable once AccessToDisposedClosure
                    logTask.Increment();
                })
            ];

            await Task.WhenAll(tasks);
        }

        // Look up a specific system the user is interested in
        if (!string.IsNullOrEmpty(options.SystemOfInterest))
        {
            MassacreTargetSystem? sys = _massacreTargetSystems.Find(sys =>
                string.Equals(sys.Name, options.SystemOfInterest,
                    (StringComparison)StringComparison.OrdinalIgnoreCase));
            if (sys != null)
            {
                await using FileStream stream =
                    new FileInfo(Path.Combine(options.OutputDir.FullName, $"{sys.Name}.json")).Create();
                await JsonSerializer.SerializeAsync(stream, sys, JsonSerializerOptions);
                await StaticLog.LogLine($"Emitted file for system of interest '{sys.Name}'");
            }
            else
            {
                await StaticLog.LogLine(
                    $"System of interest '{options.SystemOfInterest}' not found in potential stack targets");
            }
        }
    }

    // Default Json options for reading/writing general files
    public static JsonSerializerOptions JsonSerializerOptions { get; } = new()
    {
        WriteIndented = true,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip
    };

    // Default json options for writing the cache file
    public static JsonSerializerOptions JsonSerializerOptionsCache { get; } = new()
    {
        WriteIndented = false,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip
    };
}