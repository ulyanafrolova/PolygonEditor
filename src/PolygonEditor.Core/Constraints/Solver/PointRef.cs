using PolygonEditor.Core.Geometry;
using PolygonEditor.Core.Model;

namespace PolygonEditor.Core.Constraints.Solver;

/// <summary>Uniform reference to a movable point: a vertex or a Bezier control point (Index 1 or 2).</summary>
public readonly record struct PointRef
{
    private PointRef(Vertex? vertex, Edge? edge, int index)
    {
        Vertex = vertex;
        Edge = edge;
        Index = index;
    }

    public Vertex? Vertex { get; }
    public Edge? Edge { get; }
    public int Index { get; }

    public static PointRef Of(Vertex vertex) => new(vertex, null, 0);
    public static PointRef Control(Edge edge, int index) => new(null, edge, index);

    public bool IsVertex => Vertex is not null;

    public Vec2 Position => Vertex?.Position ?? (Index == 1 ? Edge!.Control1 : Edge!.Control2);

    internal void Set(Vec2 position)
    {
        if (Vertex is not null)
            Vertex.Position = position;
        else if (Index == 1)
            Edge!.Control1 = position;
        else
            Edge!.Control2 = position;
    }

    public override string ToString() => IsVertex ? $"V {Vertex!.Name}" : $"C{Index} {Edge!.Name}";
}
