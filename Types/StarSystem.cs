using System.Text.Json.Serialization;

namespace MassacreStackFinderCs.Types;

// Helper type for serializing a system
public class StarSystem
{
    [JsonIgnore]
    public VoxelAccelerationStructure? AccelerationStructure { get; set; }
    
    [JsonPropertyName("id64")]
    [JsonRequired]
    public Int64 Id { get; set; } = 0;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
    
    [JsonPropertyName("coords")]
    [JsonRequired]
    public Coords Coords { get; set; } = new ();
    
    [JsonPropertyName("factions")]
    public List<Faction> Factions { get; set; } = [];
    
    [JsonPropertyName("stations")]
    public List<Station> Stations { get; set; } = [];
    
    [JsonPropertyName("bodies")]
    public List<Body> Bodies { get; set; } = [];

    // Best (if available) station to pick up massacre missions from
    [JsonIgnore]
    public Station? MissionSourceStation
    {
        get
        {
            IEnumerable<Station> normalStations = Stations.Where(station => station.ValidMissionStation);
            IEnumerable<Station> militaryStations = normalStations.Where(station => station.IsMilitaryEconomy);
            return militaryStations.FirstOrDefault() ?? normalStations.FirstOrDefault();
        }
    }

    // Anarchy faction (if present)
    [JsonIgnore]
    public Faction? AnarchyFaction
    {
        get
        {
            Faction result = Factions.Find(faction => faction.Government == "Anarchy");
            return result.Equals(default) ? null : result;
        }
    }

    // Potential factions able to generate massacre missions
    [JsonIgnore]
    public IEnumerable<Faction> NonAnarchyFactions => Factions.Where(faction => faction.Government != "Anarchy");

    // Potential bodies with rings
    [JsonIgnore]
    public IEnumerable<Body> RingedBodies => Bodies.Where(body => body.Rings.Any());

    public float MissionGiverScore => StaticScoringHeuristic.CalculateMissionGiverScore(this);

    public void Register(VoxelAccelerationStructure accelerationStructure)
    {
        AccelerationStructure = accelerationStructure;
        AccelerationStructure.RegisterSystem(this);
    }

    public IEnumerable<StarSystem> EnumerateNearbySystems(float radius = 10, Func<StarSystem, bool>? filter = null)
    {
        return AccelerationStructure?.EnumerateNearbySystems(this,  radius, filter) ?? throw new InvalidOperationException();
    }

    [JsonIgnore]
    public IEnumerable<StarSystem> MissionTargetSystems => EnumerateNearbySystems(10, sys => sys.AnarchyFaction != null);

    // Sort star systems by Id (ascending)
    public struct IdComparer : IComparer<StarSystem>
    {
        public int Compare(StarSystem? x, StarSystem? y)
        {
            if (ReferenceEquals(x, y)) return 0;
            if (y is null) return 1;
            if (x is null) return -1;
            return x.Id.CompareTo(y.Id);
        }
    }

    public override string ToString()
    {
        return Name;
    }
}