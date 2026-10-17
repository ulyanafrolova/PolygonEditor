using PolygonEditor.Core.Geometry;

namespace PolygonEditor.Core.Model;

public sealed class Vertex
{
    public Vertex(Polygon polygon, Vec2 position)
    {
        Polygon = polygon;
        Position = position;
    }

    public Polygon Polygon { get; }
    public Vec2 Position { get; set; }

    /// <summary>Required continuity at this vertex; meaningful only when an adjacent edge is a curve.</summary>
    public Continuity Continuity { get; private set; } = Continuity.G0;

    /// <summary>True once the user chose the continuity; defaults no longer override it.</summary>
    public bool IsContinuityExplicit { get; private set; }

    /// <summary>Continuity chosen by the user.</summary>
    public void SetContinuity(Continuity continuity)
    {
        Continuity = continuity;
        IsContinuityExplicit = true;
    }

    /// <summary>Continuity applied automatically (e.g. after switching an edge to Bezier).</summary>
    internal void ApplyDefaultContinuity(Continuity continuity)
    {
        if (!IsContinuityExplicit)
            Continuity = continuity;
    }

    public Edge EdgeBefore => Polygon.EdgeBefore(this);
    public Edge EdgeAfter => Polygon.EdgeAfter(this);

    /// <summary>True when continuity matters here, i.e. at least one adjacent edge is curved.</summary>
    public bool TouchesCurve => EdgeBefore.Kind != EdgeKind.Line || EdgeAfter.Kind != EdgeKind.Line;
}
