using System.Diagnostics;
using System.Numerics;
using System.Text.Json.Serialization;

namespace MassacreStackFinderCs;

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

public struct Ring
{
    
}

public class Body
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
    
    [JsonPropertyName("distanceToArrival")]
    public double DistanceFromEntry { get; set; } = 0.0;
    
    [JsonPropertyName("rings")]
    public List<Ring> Rings { get; set; } = [];

    public override string ToString()
    {
        return Name;
    }
}

public class System
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
    
    // [JsonPropertyName("nearbySystems")]
    // public List<Int64> NearbySystemsIds { get; set; } = [];
    
    // [JsonIgnore]
    // public List<System> NearbySystems { get; set; } = [];
    
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

    [JsonIgnore]
    public Faction? AnarchyFaction
    {
        get
        {
            Faction result = Factions.Find(faction => faction.Government == "Anarchy");
            return result.Equals(default) ? null : result;
        }
    }

    [JsonIgnore]
    public IEnumerable<Faction> NonAnarchyFactions => Factions.Where(faction => faction.Government != "Anarchy");

    [JsonIgnore]
    public IEnumerable<Body> RingedBodies => Bodies.Where(body => body.Rings.Any());

    public float MissionGiverScore
    {
        get
        {
            Station? station = MissionSourceStation;
            if (station == null)
            {
                return 0.0f;
            }

            float baseFactor = (station?.IsMilitaryEconomy ?? false) ? 2.0f : 1.0f; 
            
            int numTargetSystems = MissionTargetSystems.Count();
            if (numTargetSystems == 0)
            {
                return 0.0f;
            }

            return baseFactor / MathF.Pow(numTargetSystems, 1.7f);
        }
    }

    public void Register(VoxelAccelerationStructure accelerationStructure)
    {
        AccelerationStructure = accelerationStructure;
        AccelerationStructure.RegisterSystem(this);
    }

    public HashSet<System> GetNearbySystems(int radius = 10, Func<System, bool>? filter = null)
    {
        return AccelerationStructure?.GetNearbySystems(this,  radius, filter) ?? throw new InvalidOperationException();
    }

    public IEnumerable<System> EnumerateNearbySystems(int radius = 10, Func<System, bool>? filter = null)
    {
        return AccelerationStructure?.EnumerateNearbySystems(this,  radius, filter) ?? throw new InvalidOperationException();
    }

    [JsonIgnore]
    public IEnumerable<System> MissionTargetSystems => EnumerateNearbySystems(10, sys => sys.AnarchyFaction != null) ?? throw new InvalidOperationException();

    public float SystemScore => MissionGiverScore * NonAnarchyFactions.Count();

    public struct IdComparer : IComparer<System>
    {
        public int Compare(System? x, System? y)
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

public struct NearbySystemInfo
{
    [JsonIgnore]
    public System? System { get; set; }
    
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

public class MassacreStackSystem
{
    [JsonPropertyName("score")]
    public float Score { get; }

    [JsonIgnore]
    public System System { get; }

    [JsonIgnore]
    public List<System> MissionGiverSystems { get; } = [];

    [JsonIgnore]
    public Dictionary<Faction, float> Factions { get; } = new();

    [JsonPropertyName("name")]
    public string Name => System.Name;

    [JsonPropertyName("targetFaction")]
    public string? TargetFactionName => System.AnarchyFaction?.Name;

    [JsonPropertyName("systems")]
    public IEnumerable<NearbySystemInfo> NearbySystemInfos =>
        MissionGiverSystems.Select(sys => new NearbySystemInfo() { System = sys });

    [JsonPropertyName("factionCount")]
    public Int32 FactionCount => Factions.Count;

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

    public MassacreStackSystem(System system)
    {
        System = system;

        foreach (var nearbySystem in system.EnumerateNearbySystems())
        {
            float missionScore = nearbySystem.MissionGiverScore;
            if (missionScore <= 0.0f)
            {
                continue;
            }
            MissionGiverSystems.Add(nearbySystem);
            foreach (var faction in nearbySystem.NonAnarchyFactions)
            {
                if (Factions.TryGetValue(faction, out float oldScore))
                {
                    Factions[faction] = oldScore + missionScore;
                }
                else
                {
                    Factions[faction] = missionScore;
                }
            }
        }

        Dictionary<Faction, float> finalFactionScores = new();
        foreach (var kvp in Factions)
        {
            finalFactionScores[kvp.Key] = kvp.Value > 1 ? MathF.Sqrt(kvp.Value) : kvp.Value;
        }
        Factions = finalFactionScores;
                    
        Score = Math.Clamp(Factions.Values.Sum(), 0, Factions.Count);
    }

    public struct ScoreComparer : IComparer<MassacreStackSystem>
    {
        public int Compare(MassacreStackSystem? x, MassacreStackSystem? y)
        {
            if (ReferenceEquals(x, y)) return 0;
            if (y is null) return 1;
            if (x is null) return -1;
            return x.Score.CompareTo(y.Score);
        }
    }

    public override string ToString()
    {
        return System.Name;
    }
}