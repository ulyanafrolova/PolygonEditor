using PolygonEditor.Core.Model;

namespace PolygonEditor.Core.Geometry;

/// <summary>
/// Uniform view of an edge as a parametric curve P(t), t in [0, 1], whatever its kind.
/// Derivatives are true dP/dt (not normalized), which is what C1 continuity compares.
/// </summary>
public static class EdgeGeometry
{
    public static Vec2 PointAt(Edge edge, double t)
    {
        switch (edge.Kind)
        {
            case EdgeKind.Bezier:
                double u = 1 - t;
                return edge.Start.Position * (u * u * u)
                     + edge.Control1 * (3 * t * u * u)
                     + edge.Control2 * (3 * t * t * u)
                     + edge.End.Position * (t * t * t);
            case EdgeKind.Arc when ArcGeometry.Compute(edge) is { } arc:
                return arc.PointAt(t);
            default:
                return Vec2.Lerp(edge.Start.Position, edge.End.Position, t);
        }
    }

    /// <summary>dP/dt at the start. Line: B - A. Bezier: 3(V1 - V0).</summary>
    public static Vec2 DerivativeAtStart(Edge edge, Vertex? forceG0 = null, int depth = 0) => edge.Kind switch
    {
        EdgeKind.Bezier => 3 * (edge.Control1 - edge.Start.Position),
        EdgeKind.Arc => ArcGeometry.Compute(edge, forceG0, depth)?.DerivativeAtStart ?? edge.Chord,
        _ => edge.Chord,
    };

    /// <summary>dP/dt at the end. Line: B - A. Bezier: 3(V3 - V2).</summary>
    public static Vec2 DerivativeAtEnd(Edge edge, Vertex? forceG0 = null, int depth = 0) => edge.Kind switch
    {
        EdgeKind.Bezier => 3 * (edge.End.Position - edge.Control2),
        EdgeKind.Arc => ArcGeometry.Compute(edge, forceG0, depth)?.DerivativeAtEnd ?? edge.Chord,
        _ => edge.Chord,
    };
}
