using System.Text.Json.Serialization;

namespace MassacreStackFinderCs.Types;

// Helper type for serializing a body
public class Body
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
    
    // TODO: Consider factoring in distance to arrival
    // [JsonPropertyName("distanceToArrival")]
    // public double DistanceFromEntry { get; set; } = 0.0;
    
    [JsonPropertyName("rings")]
    public List<Ring> Rings { get; set; } = [];

    public override string ToString()
    {
        return Name;
    }
}