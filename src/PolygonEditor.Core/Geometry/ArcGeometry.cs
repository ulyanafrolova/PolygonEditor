using PolygonEditor.Core.Model;

namespace PolygonEditor.Core.Geometry;

/// <summary>Circular arc: P(t) = Center + Radius * (cos, sin)(StartAngle + Sweep * t), t in [0, 1].</summary>
public readonly record struct ArcShape(Vec2 Center, double Radius, double StartAngle, double Sweep)
{
    public Vec2 PointAt(double t) => Center + Vec2.FromAngle(StartAngle + Sweep * t) * Radius;

    /// <summary>dP/dt at t = 0 and t = 1.</summary>
    public Vec2 DerivativeAtStart => Vec2.FromAngle(StartAngle).Perp() * (Radius * Sweep);
    public Vec2 DerivativeAtEnd => Vec2.FromAngle(StartAngle + Sweep).Perp() * (Radius * Sweep);

    public ArcShape Reversed() => new(Center, Radius, StartAngle + Sweep, -Sweep);
}

/// <summary>
/// An arc edge stores no extra parameters: its circle follows from the endpoints and continuity.
/// - G1 at one end: the arc is tangent there to the neighbouring edge and passes through the other end.
/// - G0 at both ends: a semicircle with radius = half of the chord.
/// Only one end can effectively be G1; if both are, the start wins.
/// </summary>
public static class ArcGeometry
{
    // Guards against infinite recursion when arcs with G1 depend on each other around the polygon.
    private const int MaxDepth = 8;
    private const double Eps = 1e-9;

    /// <param name="forceG0">Vertex whose G1 requirement is ignored (breaks mutual dependency of two arcs).</param>
    /// <returns>null when the arc degenerates to a straight segment.</returns>
    public static ArcShape? Compute(Edge edge, Vertex? forceG0 = null, int depth = 0)
    {
        Vec2 a = edge.Start.Position, b = edge.End.Position;
        Vec2 chord = b - a;
        if (chord.LengthSquared < Eps)
            return null;

        bool g1Allowed = depth < MaxDepth;
        if (g1Allowed && WantsG1(edge.Start, forceG0))
        {
            Vec2 tangent = EdgeGeometry.DerivativeAtEnd(edge.Previous, edge.Start, depth + 1);
            return TangentArc(a, tangent, b);
        }
        if (g1Allowed && WantsG1(edge.End, forceG0))
        {
            Vec2 tangent = EdgeGeometry.DerivativeAtStart(edge.Next, edge.End, depth + 1);
            return TangentArc(b, -tangent, a)?.Reversed();
        }

        Vec2 center = (a + b) * 0.5;
        return new ArcShape(center, chord.Length / 2, Vec2.Angle(a - center), Math.PI);
    }

    /// <summary>
    /// Arc starting at a with direction tangent and ending at b. The center lies on the normal
    /// to the tangent at a and on the perpendicular bisector of ab: C = a + n*s, |C - a| = |C - b|
    /// gives s = |ab|^2 / (2 n·ab).
    /// </summary>
    public static ArcShape? TangentArc(Vec2 a, Vec2 tangent, Vec2 b)
    {
        if (tangent.LengthSquared < Eps)
            return null;

        Vec2 n = tangent.Perp().Normalized();
        Vec2 ab = b - a;
        double denominator = 2 * Vec2.Dot(n, ab);
        if (Math.Abs(denominator) < 1e-9 * ab.Length)
            return null; // tangent along the chord -> infinite radius

        double s = ab.LengthSquared / denominator;
        Vec2 center = a + n * s;
        double startAngle = Vec2.Angle(a - center);
        double sweep = Vec2.Angle(b - center) - startAngle;

        // Increasing angle moves along Perp(a - center); pick the sweep sign matching the tangent.
        bool positive = Vec2.Dot(tangent, (a - center).Perp()) > 0;
        if (positive)
            while (sweep <= 0) sweep += 2 * Math.PI;
        else
            while (sweep >= 0) sweep -= 2 * Math.PI;

        return new ArcShape(center, Math.Abs(s), startAngle, sweep);
    }

    private static bool WantsG1(Vertex vertex, Vertex? forceG0) =>
        vertex != forceG0 && vertex.Continuity != Continuity.G0;
}
