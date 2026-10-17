using PolygonEditor.Core.Geometry;
using PolygonEditor.Core.Model;

namespace PolygonEditor.Core.Editing;

/// <summary>Scene element under the cursor.</summary>
public abstract record HitTarget
{
    public abstract Polygon Polygon { get; }
}

public sealed record VertexHit(Vertex Vertex) : HitTarget
{
    public override Polygon Polygon => Vertex.Polygon;
}

/// <summary>Bezier control point; Index is 1 (Control1) or 2 (Control2).</summary>
public sealed record ControlPointHit(Edge Edge, int Index) : HitTarget
{
    public override Polygon Polygon => Edge.Polygon;
    public Vec2 Position => Index == 1 ? Edge.Control1 : Edge.Control2;
}

public sealed record EdgeHit(Edge Edge) : HitTarget
{
    public override Polygon Polygon => Edge.Polygon;
}

/// <summary>Point inside the polygon (even-odd rule, so self-intersecting polygons work too).</summary>
public sealed record PolygonHit(Polygon Target) : HitTarget
{
    public override Polygon Polygon => Target;
}
