using System.Text.Json.Serialization;

namespace MassacreStackFinderCs.Types;

// Helper type for serializing a station
public class Station
{
    [JsonPropertyName("name")]
    [JsonRequired]
    public string Name { get; set; } = string.Empty;
    
    [JsonPropertyName("economy")]
    public string Economy { get; set; } = string.Empty;
    
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;
    
    [JsonPropertyName("distanceToArrival")]
    public double DistanceFromEntry { get; set; } = 0.0;
    
    [JsonPropertyName("otherServices")]
    public List<string> Services { get; set; } = [];

    // Valid types we consider, helps filtering out a lot of noise (e.g. fleet carriers)
    public static HashSet<string> ValidStationTypes { get; } =
    [
        "Outpost", "Coriolis Starport", "Ocellus Starport", "Orbis Starport", "Asteroid base", "Mega ship"
    ];

    [JsonIgnore]
    public bool ValidMissionStation =>
        Services.Contains("Missions") 
        && DistanceFromEntry < 10000.0
        && ValidStationTypes.Contains(Type);
    
    [JsonIgnore]
    public bool IsMilitaryEconomy => Economy == "Military";

    public override string ToString()
    {
        return Name;
    }
}