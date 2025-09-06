using System.Numerics;
using System.Text.Json.Serialization;

namespace MassacreStackFinderCs.Types;

// Helper type for serializing a system coordinate (ly relative to Sol)
public struct Coords
{
    [JsonPropertyName("x")]
    public float X { get; set; }

    [JsonPropertyName("y")]
    public float Y { get; set; }

    [JsonPropertyName("z")]
    public float Z { get; set; }
    
    public Vector3 ToVector() { return new Vector3(X, Y, Z); }

    public override string ToString()
    {
        return $"({X}, {Y}, {Z})";
    }
}