using System.Text.Json.Serialization;

namespace MassacreStackFinderCs.Types;

// Helper type for serializing a faction
public struct Faction : IEquatable<Faction>
{
    public Faction()
    {
    }

    [JsonPropertyName("name")]
    [JsonRequired]
    public string Name { get; set; } = string.Empty;
    
    [JsonPropertyName("government")]
    public string Government { get; set; } = string.Empty;
    
    [JsonPropertyName("influence")]
    public float Influence { get; set; }
    
    // EDSM still lists factions when they've long left the system
    [JsonIgnore]
    public bool IsDeadFaction => Influence < 0.00001f;

    public bool Equals(Faction other)
    {
        return Name == other.Name;
    }

    public override bool Equals(object? obj)
    {
        return obj is Faction other && Equals(other);
    }

    public override int GetHashCode()
    {
        return Name.GetHashCode();
    }

    public override string ToString()
    {
        return Name;
    }
}