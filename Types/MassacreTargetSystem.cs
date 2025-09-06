using System.Text.Json.Serialization;

namespace MassacreStackFinderCs.Types;

// Helper type for serializing a system used as a target for massacre stacks, including giver systems and scoring
public class MassacreTargetSystem
{
    [JsonPropertyName("score")]
    public float Score { get; set; }

    [JsonIgnore]
    public StarSystem System { get; }

    [JsonIgnore]
    public List<StarSystem> MissionGiverSystems { get; } = [];

    [JsonIgnore]
    public Dictionary<Faction, float> Factions { get; set; } = new();

    [JsonPropertyName("name")]
    public string Name => System.Name;

    [JsonPropertyName("targetFaction")]
    public string? TargetFactionName => System.AnarchyFaction?.Name;

    [JsonPropertyName("systems")]
    public IEnumerable<MissionGiverSystem> NearbySystemInfos =>
        MissionGiverSystems.Select(sys => new MissionGiverSystem() { System = sys });

    [JsonPropertyName("factionCount")]
    public int FactionCount => Factions.Count;

    [JsonPropertyName("factions")]
    public Dictionary<string, float> FactionNames
    {
        get
        {
            Dictionary<string, float> temp = new();
            foreach (var kvp in Factions)
            {
                temp[kvp.Key.Name] = kvp.Value;
            }
            return temp;
        }
    }

    public MassacreTargetSystem(StarSystem system)
    {
        System = system;

        foreach (var nearbySystem in System.EnumerateNearbySystems())
        {
            float missionScore = nearbySystem.MissionGiverScore;
            if (missionScore <= 0.0f)
            {
                continue;
            }

            MissionGiverSystems.Add(nearbySystem);
        }
    }

    // Sort by score, descending
    public struct ScoreComparer : IComparer<MassacreTargetSystem>
    {
        public int Compare(MassacreTargetSystem? x, MassacreTargetSystem? y)
        {
            if (ReferenceEquals(x, y)) return 0;
            if (y is null) return -1;
            if (x is null) return 1;
            return -x.Score.CompareTo(y.Score);
        }
    }

    public override string ToString()
    {
        return System.Name;
    }
}