using System.Numerics;
using System.Security.AccessControl;
using MassacreStackFinderCs.Types;

namespace MassacreStackFinderCs;

// Helper struct to identify a 3D voxel
public struct VoxelCoordinate : IEquatable<VoxelCoordinate>
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
        X = (int)Math.Floor(coordinate.X / VoxelAccelerationStructure.VoxelEdgeLength);
        Y = (int)Math.Floor(coordinate.Y / VoxelAccelerationStructure.VoxelEdgeLength);
        Z = (int)Math.Floor(coordinate.Z / VoxelAccelerationStructure.VoxelEdgeLength);
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

    public override string ToString()
    {
        return $"({X} | {Y} | {Z})";
    }
    public static bool operator ==(VoxelCoordinate left, VoxelCoordinate right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(VoxelCoordinate left, VoxelCoordinate right)
    {
        return !(left == right);
    }
}

// Accelerates spatial lookup by organizing systems into voxel nodes
public class VoxelAccelerationStructure
{
    public const int VoxelEdgeLength = 10; // Voxel size: 10/10/10 ly

    // Maps coordinates to Nodes
    // TODO: Optimization opportunity: This lookup could be faster if it would also make use of the spatial information (e.g. Octree)
    private Dictionary<VoxelCoordinate, Node> Nodes { get; set; } = new();

    /// Adds <paramref name="starSystem"/> to the acceleration structure
    public void RegisterSystem(StarSystem starSystem)
    {
        VoxelCoordinate coordinate = new VoxelCoordinate(starSystem.Coords.ToVector());
        if (!Nodes.TryGetValue(coordinate, out var node))
        {
            node = new Node()
            {
                Coordinate = coordinate,
            };
            Nodes.Add(coordinate, node);
        }

        node.Systems.Add(starSystem);
    }

    // Enumerate systems nearby the given system within a maximum range, matching a filter   
    public IEnumerable<StarSystem> EnumerateNearbySystems(StarSystem starSystem, float radius, Func<StarSystem, bool>? filterFunc = null)
    {
        float radiusSquared = radius * radius;

        // Identify a cube of voxels encompassing all systems which could potentially be within range. This will overestimate slightly.
        Vector3 center = starSystem.Coords.ToVector();
        VoxelCoordinate lowerBound = new VoxelCoordinate(center - new Vector3(radius));
        VoxelCoordinate upperBound = new VoxelCoordinate(center + new Vector3(radius));

        // Enumerate all voxels in our search cube
        for (int x = lowerBound.X; x <= upperBound.X; x++)
        {
            for (int y = lowerBound.Y; y <= upperBound.Y; y++)
            {
                for (int z = lowerBound.Z; z <= upperBound.Z; z++)
                {
                    VoxelCoordinate current = new VoxelCoordinate(x, y, z);

                    // TODO: Optimization opportunity: Do a box-sphere intersection, remove any corner voxels before the costly dict lookup

                    // Look up for a potential real voxel given the coordinates
                    if (Nodes.TryGetValue(current, out var node))
                    {
                        // Enumerate systems contained in that voxel
                        foreach (var nearbySystem in node.Systems)
                        {
                            // filter by distance
                            if (Vector3.DistanceSquared(center, nearbySystem.Coords.ToVector()) > radiusSquared)
                            {
                                continue;
                            }

                            // Filter by filter function (if set)
                            if (filterFunc != null)
                            {
                                if (!filterFunc(nearbySystem))
                                {
                                    continue;
                                }
                            }

                            // Don't yield the input system
                            if (starSystem == nearbySystem)
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

    // Voxel leaf node: Set of systems
    private struct Node : IEquatable<Node>
    {
        public Node()
        {
        }

        public VoxelCoordinate Coordinate { get; set; }
        public HashSet<StarSystem> Systems { get; set; } = [];

        public bool Equals(Node other)
        {
            return Coordinate.Equals(other.Coordinate);
        }

        public override bool Equals(object? obj)
        {
            return obj is Node && Equals((Node)obj);
        }

        public override int GetHashCode()
        {
            return Coordinate.GetHashCode();
        }
    }
}