using System.Text.Json.Serialization;

namespace MassacreStackFinderCs.Types;

// Helper type for serializing a system generating massacre missions
public struct MissionGiverSystem
{
    [JsonIgnore]
    public StarSystem? System { get; set; }
    
    [JsonPropertyName("name")]
    public string Name => System!.Name;

    [JsonPropertyName("station")]
    public string StationInfo
    {
        get
        {
            Station station = System!.MissionSourceStation!;
            if (station.IsMilitaryEconomy)
            {
                return $"{station.Name} (Military, {station.DistanceFromEntry:F1}ls)";
            }
            return  $"{station.Name} ({station.DistanceFromEntry:F1}ls)";
        }
    }

    [JsonPropertyName("score")]
    public float Score => System!.MissionGiverScore;

    [JsonPropertyName("targetSystemCount")]
    public Int32 TargetSystemCount => System!.MissionTargetSystems.Count();
}