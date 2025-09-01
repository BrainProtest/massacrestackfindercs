using System.Numerics;
using System.Security.AccessControl;

namespace MassacreStackFinderCs;

public struct VoxelCoordinate :  IEquatable<VoxelCoordinate>
{
    public int X { get; set; }
    public int Y { get; set; }
    public int Z { get; set; }

    public VoxelCoordinate()
    {
    }

    public VoxelCoordinate(int x, int y, int z)
    {
        X = x;
        Y = y;
        Z = z;
    }

    public VoxelCoordinate(Vector3 coordinate)
    {
        X = (int)Math.Floor(coordinate.X * 0.05);
        Y = (int)Math.Floor(coordinate.Y * 0.05);
        Z = (int)Math.Floor(coordinate.Z * 0.05);
    }

    public bool Equals(VoxelCoordinate other)
    {
        return X == other.X && Y == other.Y && Z == other.Z;
    }

    public override bool Equals(object? obj)
    {
        return obj is VoxelCoordinate other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(X, Y, Z);
    }

    public VoxelCoordinate Decrement()
    {
        return new VoxelCoordinate(X - 1, Y - 1, Z - 1);
    }

    public VoxelCoordinate Increment()
    {
        return new VoxelCoordinate(X + 1, Y + 1, Z + 1);
    }
}

public struct VoxelAccelerationStructureNode : IEquatable<VoxelAccelerationStructureNode>
{
    public VoxelAccelerationStructureNode()
    {
    }

    public VoxelCoordinate Coordinate { get; set; }
    public HashSet<System> Systems { get; set; } = [];

    public bool Equals(VoxelAccelerationStructureNode other)
    {
        return Coordinate.Equals(other.Coordinate);
    }
}

public class VoxelAccelerationStructure
{
    public Dictionary<VoxelCoordinate, VoxelAccelerationStructureNode> Nodes { get; set; } = new();

    public void RegisterSystem(System system)
    {
        VoxelCoordinate coordinate = new VoxelCoordinate(system.Coords.ToVector());
        if (!Nodes.TryGetValue(coordinate, out var node))
        {
            node = new VoxelAccelerationStructureNode()
            {
                Coordinate = coordinate,
            };
            Nodes.Add(coordinate, node);
        }
        node.Systems.Add(system);
    }

    public HashSet<System> GetNearbySystems(System system, float radius, Func<System, bool>? filter = null)
    {
        HashSet<System> result = [];
        float radiusSquared = radius * radius;

        Vector3 center = system.Coords.ToVector();
        VoxelCoordinate lowerBound = new VoxelCoordinate(center - new Vector3(radius)).Decrement();
        VoxelCoordinate upperBound = new VoxelCoordinate(center + new Vector3(radius)).Increment();

        for (int x = lowerBound.X; x <= upperBound.X; x++)
        {
            for (int y = lowerBound.Y; y <= upperBound.Y; y++)
            {
                for (int z = lowerBound.Z; z <= upperBound.Z; z++)
                {
                    VoxelCoordinate current =  new VoxelCoordinate(x, y, z);
                    if (Nodes.TryGetValue(current, out var node))
                    {
                        foreach (var nearbySystem in node.Systems)
                        {
                            if (Vector3.DistanceSquared(center, nearbySystem.Coords.ToVector()) > radiusSquared)
                            {
                                continue;
                            }

                            if (filter != null)
                            {
                                if (!filter(nearbySystem))
                                {
                                    continue;
                                }
                            }

                            if (system == nearbySystem)
                            {
                                continue;
                            }

                            result.Add(nearbySystem);
                        }
                    }
                }
            }
        }
        return result;
    }
    
    public IEnumerable<System> EnumerateNearbySystems(System system, float radius, Func<System, bool>? filter = null)
    {
        float radiusSquared = radius * radius;

        Vector3 center = system.Coords.ToVector();
        VoxelCoordinate lowerBound = new VoxelCoordinate(center - new Vector3(radius)).Decrement();
        VoxelCoordinate upperBound = new VoxelCoordinate(center + new Vector3(radius)).Increment();

        for (int x = lowerBound.X; x <= upperBound.X; x++)
        {
            for (int y = lowerBound.Y; y <= upperBound.Y; y++)
            {
                for (int z = lowerBound.Z; z <= upperBound.Z; z++)
                {
                    VoxelCoordinate current =  new VoxelCoordinate(x, y, z);
                    if (Nodes.TryGetValue(current, out var node))
                    {
                        foreach (var nearbySystem in node.Systems)
                        {
                            if (Vector3.DistanceSquared(center, nearbySystem.Coords.ToVector()) > radiusSquared)
                            {
                                continue;
                            }

                            if (filter != null)
                            {
                                if (!filter(nearbySystem))
                                {
                                    continue;
                                }
                            }

                            if (system == nearbySystem)
                            {
                                continue;
                            }

                            yield return nearbySystem;
                        }
                    }
                }
            }
        }
    }
}