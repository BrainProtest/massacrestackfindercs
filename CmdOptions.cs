using CommandLine;

namespace MassacreStackFinderCs;

public class CmdOptions
{
    [Value(0, Required = true, HelpText = "EDSM dump systemsPopulated.json")]
    public FileInfo InputFile { get; set; } = new("./systemsPopulated.json");
    
    [Option('c', "cachefile", HelpText = "A file to use for a cache")]
    public FileInfo CacheFile { get; set; } = new("./systemsPopulatedCache.json");
    
    [Option('o', "outputFile", HelpText = "Output file")]
    public DirectoryInfo OutputDir { get; set; } = new("./massacreStackLocations");
    
    [Option('s', "system", HelpText = "If defined, also yield a file for the system (assuming it passed initial selection)")]
    public string SystemOfInterest { get; set; } = string.Empty;
    
    [Option('n', "numResults", HelpText = "How many systems to return")]
    public Int32 NumResults { get; set; } = 50;
}